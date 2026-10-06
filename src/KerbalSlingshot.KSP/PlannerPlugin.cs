using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KerbalSlingshot.Core;
using UnityEngine;
using NumericalTrajectory=KerbalSlingshot.Core.Trajectory;

namespace KerbalSlingshot.KSP
{
    [KSPAddon(KSPAddon.Startup.Flight,false)]
    public sealed partial class PlannerPlugin : MonoBehaviour
    {
        private const string InputLock="KerbalSlingshot.UI";
        private readonly Dictionary<string,string> fields=PlannerSettings.Defaults();
        private readonly RevisionGate gate=new RevisionGate();
        private string[] bodyIds=Array.Empty<string>();
        private string context="Read the active vessel to choose its sibling bodies.";
        private string status="Select Mun, Minmus and periapsis, then Find Trajectory.";
        private string report="",patchReport="";
        private int assistIndex,destinationIndex;
        private bool visible,terrainConfirmed;
        private Rect window=new Rect(30,110,PlannerLayout.Width,PlannerLayout.PrimaryHeight);
        private float lastFreshness;
        private Job? job;
        private CapturedPlan? displayedPlan;
        private Burn? displayedBurn;
        private Evaluation? candidateEvaluation;
        private ValidationResult? validation;
        private V3 validatedComponents;
        private bool nodeCreated;
        private string contextVessel="",contextParent="";
        private static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
        private static string Build => typeof(PlannerPlugin).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.3.0";
        private static string Vector(V3 v) => "("+SnapshotAdapter.F(v.X)+","+SnapshotAdapter.F(v.Y)+","+SnapshotAdapter.F(v.Z)+")";

        private sealed class Progress
        {
            public readonly int Count;
            public readonly string Reason;
            public Progress(int count,string reason) { Count=count; Reason=reason; }
        }
        private sealed class Job
        {
            public readonly CapturedPlan Plan;
            public readonly PlannerSettings Settings;
            public readonly int Revision;
            public readonly bool Refine;
            public readonly CancellationTokenSource Cancellation=new CancellationTokenSource();
            public Task<SearchResult>? Work;
            public Progress Progress=new Progress(0,"starting");
            public readonly System.Diagnostics.Stopwatch Timer=System.Diagnostics.Stopwatch.StartNew();
            public Job(CapturedPlan plan,PlannerSettings settings,int revision,bool refine)
            { Plan=plan; Settings=settings; Revision=revision; Refine=refine; }
        }

        public void Start()
        {
            fields["Departure +/- s"]="86400"; fields["Journey limit s"]="1200000";
            fields["Evaluations"]="6000"; fields["Wall budget s"]="120";
            fields["Time step s"]="1080"; fields["DV step m/s"]="20";
            Debug.Log("[KerbalSlingshot] Flight planning plugin "+Build+"; KSP validation pending; node creation unavailable.");
            window.x=Math.Max(20,Screen.width-PlannerLayout.Width-320);
            GameEvents.onGUIApplicationLauncherReady.Add(EnsureToolbar);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(RemoveToolbar);
            EnsureToolbar();
            Try(ReadContext);
        }
        public void OnDestroy()
        {
            AbandonJob(); gate.Invalidate(); InputLockManager.RemoveControlLock(InputLock);
            GameEvents.onGUIApplicationLauncherReady.Remove(EnsureToolbar);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(RemoveToolbar);
            RemoveToolbar();
            if (toolbarIcon!=null) Destroy(toolbarIcon);
            if (panelTexture!=null) Destroy(panelTexture);
        }
        private void AbandonJob()
        {
            Job? previous=job; job=null;
            if (previous==null) return;
            previous.Cancellation.Cancel();
            if (previous.Work!=null) previous.Work.ContinueWith(completed=> { _=completed.Exception; previous.Cancellation.Dispose(); },TaskScheduler.Default);
            else previous.Cancellation.Dispose();
        }
        private void Invalidate(string reason)
        {
            gate.Invalidate(); AbandonJob(); displayedPlan=null; displayedBurn=null; candidateEvaluation=null; validation=null; nodeCreated=false; resultCard=null; report=""; patchReport=""; status=reason;
        }
        private void Try(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                Invalidate(ex.Message);
                Debug.LogError("[KerbalSlingshot] "+ex);
            }
        }
        private void ReadContext()
        {
            Invalidate("Ready for automatic Mun-to-Minmus search.");
            Vessel v=SnapshotAdapter.Active();
            CelestialBody parent=v.orbit.referenceBody;
            bodyIds=SnapshotAdapter.Children(parent).Select(b=>b.bodyName).ToArray();
            terrainConfirmed=false;
            terrainConfirmed=true;
            contextVessel=v.id.ToString(); contextParent=parent.bodyName;
            assistIndex=Math.Max(0,Array.IndexOf(bodyIds,"Mun")); destinationIndex=Math.Max(0,Array.IndexOf(bodyIds,"Minmus"));
            double now=Planetarium.GetUniversalTime();
            fields["Departure UT"]=SnapshotAdapter.F(now+600);
            context=v.vesselName+" | parent "+parent.bodyName+" | captured UT "+SnapshotAdapter.F(now)+" | children "+bodyIds.Length;
            if (bodyIds.Length<2) status="Unsupported route: this parent has fewer than two direct children. Parking-orbit departures/planetary escape are not supported.";
        }
        private void BeginAutomatic()
        {
            Vessel vessel=SnapshotAdapter.Active();
            if(vessel.id.ToString()!=contextVessel || vessel.orbit.referenceBody.bodyName!=contextParent)
                throw new ArgumentException("Vessel/parent changed: refresh before searching");
            PlannerSettings settings=PlannerSettings.Parse(fields);
            CapturedPlan plan=SnapshotAdapter.Capture(settings,bodyIds[assistIndex],bodyIds[destinationIndex],terrainConfirmed);
            Invalidate("Automatic search running...");
            var next=new Job(plan,settings,gate.Invalidate(),true); job=next;
            next.Cancellation.CancelAfter(TimeSpan.FromSeconds(settings.WallSeconds));
            next.Work=Task.Run(()=>AutomaticSearch.Solve(plan.Request,settings.Budget,next.Cancellation.Token,
                (count,reason)=>Volatile.Write(ref next.Progress,new Progress(count,reason)),settings.TimeStep,settings.VelocityStep));
        }
        public void Update()
        {
            if (Time.realtimeSinceStartup-lastFreshness>.5f)
            {
                lastFreshness=Time.realtimeSinceStartup;
                CapturedPlan? current=job?.Plan ?? displayedPlan;
                if (current!=null)
                {
                    try
                    {
                        double? burn=displayedBurn?.UT ?? current.Request.Earliest;
                        if (!SnapshotAdapter.Fresh(current,burn)) Invalidate("Stale result/work discarded: vessel, orbit, bodies, nodes, or departure time changed. Read vessel and recalculate.");
                    }
                    catch (Exception ex) { Invalidate("Stale snapshot: "+ex.Message); }
                }
            }
            Job? finished=job;
            if (finished?.Work==null || !finished.Work.IsCompleted) return;
            job=null; finished.Timer.Stop();
            try
            {
                SearchResult result=PlannerCompletion.FinishWallBounded(finished.Work.GetAwaiter().GetResult());
                if (!gate.IsCurrent(finished.Revision)) return;
                Burn? burn=result.Candidate ?? result.DiagnosticBurn;
                if (!SnapshotAdapter.Fresh(finished.Plan,burn?.UT)) { Invalidate("Completed work is stale; recalculate."); return; }
                displayedPlan=finished.Plan; displayedBurn=burn;
                resultCard=new PlannerResultCard(result,finished.Plan.Request,finished.Refine);
                candidateEvaluation=result.Status==SearchStatus.OfflineFeasible?result.Evaluation:null;
                if(candidateEvaluation!=null && displayedBurn.HasValue)
                    validation=KspRouteValidator.Validate(finished.Plan,displayedBurn.Value,candidateEvaluation,out validatedComponents);
                if (result.Status==SearchStatus.Cancelled)
                    status="Wall budget exhausted: no solution found within bounds. See search limits in Advanced.";
                else if (result.Status==SearchStatus.OfflineFeasible)
                    status="OFFLINE-FEASIBLE PREDICTION — KSP validation pending.  "+result.Message;
                else status=finished.Refine?result.Message:"Estimate rejected: "+result.Message;
                if(validation!=null) status=validation.Passed?"KSP-VALIDATED full route. Review, then Create Node.":"Node disabled: "+validation.Reason;
                report=Describe(finished,result);
                Debug.Log("[KerbalSlingshot] "+report);
            }
            catch (Exception ex) { Invalidate("Calculation failed: "+ex.Message); Debug.LogError("[KerbalSlingshot] "+ex); }
            finally { finished.Cancellation.Dispose(); }
        }

        private string Describe(Job completed,SearchResult result)
        {
            CapturedPlan plan=completed.Plan;
            var text=new StringBuilder();
            text.AppendLine("Build "+Build+" | "+status);
            text.AppendLine(plan.VesselName+" ["+plan.VesselId+"] | parent "+plan.ParentId+" | snapshot UT "+SnapshotAdapter.F(plan.Request.Epoch));
            text.AppendLine("Route "+plan.Request.Assist+" -> "+plan.Request.Destination+" | target Pe "+SnapshotAdapter.F(plan.Request.TargetAltitude/1000)+" km");
            text.AppendLine("Automatic departure interval UT "+SnapshotAdapter.F(plan.Request.Earliest)+" to "+SnapshotAdapter.F(plan.Request.Latest));
            text.AppendLine("Evaluations "+result.Evaluations+" / "+completed.Settings.Budget+" | elapsed "+completed.Timer.Elapsed.TotalSeconds.ToString("F3",Invariant)+" s");
            foreach (var field in fields) text.AppendLine(field.Key+" = "+field.Value);
            if (plan.CappedParent) text.AppendLine("Central parent infinite SOI replaced by a documented numeric limit of 1e15 m.");
            text.AppendLine("Terrain ceilings are user-supplied conservative bounds, not measured PQS maxima.");
            bool accepted=result.Status==SearchStatus.OfflineFeasible;
            Burn? burn=accepted?result.Candidate:result.DiagnosticBurn;
            Evaluation? evaluation=accepted?result.Evaluation:result.DiagnosticEvaluation;
            if (burn.HasValue && evaluation!=null)
            {
                text.AppendLine(accepted?"Complete offline candidate:":"DIAGNOSTIC ESTIMATE ONLY — not an accepted solution:");
                V3 components=ApiContract.ReadBurnFrame(SnapshotAdapter.Active().orbit,burn.Value.UT).ToComponents(burn.Value.DeltaV);
                text.AppendLine("Departure UT "+SnapshotAdapter.F(burn.Value.UT)+" | total DV "+SnapshotAdapter.F(burn.Value.DeltaV.Length)+" m/s");
                text.AppendLine("Native node radial, normal, prograde (m/s): "+SnapshotAdapter.F(components.X)+", "+SnapshotAdapter.F(components.Y)+", "+SnapshotAdapter.F(components.Z));
                text.AppendLine("Inertial DV "+Vector(burn.Value.DeltaV)+" | "+evaluation.Status+": "+evaluation.Reason);
                foreach (EncounterEvent ev in evaluation.Events)
                {
                    Body body=plan.Request.Bodies.Single(b=>b.Id==ev.Body);
                    text.AppendLine(ev.Body+" "+ev.Kind+" | UT "+SnapshotAdapter.F(ev.UT)+" | burn +"+SnapshotAdapter.F(ev.UT-burn.Value.UT)+" s | altitude "+
                        SnapshotAdapter.F((ev.RelativeState.R.Length-body.Radius)/1000)+" km | speed "+SnapshotAdapter.F(ev.RelativeState.V.Length)+" m/s");
                }
                if (evaluation.DestinationAltitude.HasValue)
                    text.AppendLine("Destination Pe error "+SnapshotAdapter.F(evaluation.DestinationAltitude.Value-plan.Request.TargetAltitude)+" m; tolerance "+SnapshotAdapter.F(plan.Request.AltitudeTolerance)+" m");
            }
            foreach (var pair in result.Rejections) text.AppendLine("Rejection: "+pair.Key+" = "+pair.Value);
            text.AppendLine("KSP-validated = "+(validation?.Passed==true)+"; calculation has not modified the live node plan.");
            text.AppendLine("Runtime KSP validation: "+(validation==null?"not attempted":validation.Passed+" — "+validation.Reason));
            if(validation!=null) foreach(EncounterEvent ev in validation.Events)
                text.AppendLine("KSP event "+ev.Body+" "+ev.Kind+" UT="+SnapshotAdapter.F(ev.UT)+" radius="+SnapshotAdapter.F(ev.RelativeState.R.Length)+" speed="+SnapshotAdapter.F(ev.RelativeState.V.Length));
            return text.ToString();
        }

        private void CreateNode()
        {
            if(job!=null || displayedPlan==null || !displayedBurn.HasValue || candidateEvaluation==null || validation?.Passed!=true)
                throw new InvalidOperationException("No current fully KSP-validated candidate");
            ValidationResult inserted=KspRouteValidator.Insert(displayedPlan,displayedBurn.Value,candidateEvaluation,validatedComponents);
            string evidence=report+"\n"+inserted.Reason;
            PlannerResultCard? previous=resultCard;
            Invalidate(inserted.Reason); report=evidence;
            if(inserted.Passed) { resultCard=previous; nodeCreated=true; }
            Debug.Log("[KerbalSlingshot] "+inserted.Reason);
        }

        private bool CanCreateCurrent()
        {
            if(validation?.Passed!=true || displayedPlan==null || !displayedBurn.HasValue || candidateEvaluation==null) return false;
            try { return NodeAuthorization.Allowed(validation.Passed,SnapshotAdapter.Fresh(displayedPlan,displayedBurn.Value.UT),KspRouteValidator.HasFutureNodes(SnapshotAdapter.Active()),job!=null); }
            catch(Exception) { return false; }
        }

        private void ReadPatches()
        {
            Vessel active=SnapshotAdapter.Active();
            int future=active.patchedConicSolver==null?0:active.patchedConicSolver.maneuverNodes.Count(n=>n.UT>Planetarium.GetUniversalTime());
            if(future!=1)
            {
                patchReport="No single existing future node to inspect. Automatic validation uses temporary patches; no preliminary node is needed.";
                return;
            }
            ManeuverNode node=SnapshotAdapter.SingleFutureNode();
            var text=new StringBuilder("EXISTING NODE KSP PATCHES (not validation of a refined candidate)\n");
            text.AppendLine("Node UT "+SnapshotAdapter.F(node.UT)+" | native DV "+node.DeltaV);
            Orbit? patch=node.nextPatch;
            var seen=new HashSet<Orbit>();
            int count=0;
            while (patch!=null && count<8 && seen.Add(patch))
            {
                text.AppendLine((patch.referenceBody?.bodyName ?? "unknown")+" | start UT "+SnapshotAdapter.F(patch.StartUT)+
                    " | end UT "+SnapshotAdapter.F(patch.EndUT)+" | conic PeA "+SnapshotAdapter.F(patch.PeA/1000)+" km");
                count++; patch=patch.nextPatch;
            }
            text.AppendLine("Read "+count+" patches; visible chain may be truncated. Conic periapsis may fall outside patch interval.");
            patchReport=text.ToString(); Debug.Log("[KerbalSlingshot] "+patchReport);
        }
        private void WriteDiagnostics()
        {
            string directory=Path.Combine(KSPUtil.ApplicationRootPath,"GameData","KerbalSlingshot","Diagnostics");
            Directory.CreateDirectory(directory);
            string file=Path.Combine(directory,"planning-"+Guid.NewGuid().ToString("N")+".txt");
            var snapshot=new StringBuilder();
            CapturedPlan? evidencePlan=job?.Plan ?? displayedPlan;
            if (evidencePlan!=null)
            {
                Request r=evidencePlan.Request;
                snapshot.AppendLine("Snapshot UT="+SnapshotAdapter.F(r.Epoch)+" vessel="+evidencePlan.VesselId+" parent="+evidencePlan.ParentId);
                snapshot.AppendLine("Snapshot SI units: parent mu="+SnapshotAdapter.F(r.ParentMu)+" safe radius="+SnapshotAdapter.F(r.ParentSafeRadius)+" SOI="+SnapshotAdapter.F(r.ParentSoi));
                snapshot.AppendLine("Vessel R="+Vector(r.Vessel.R)+" V="+Vector(r.Vessel.V));
                foreach (Body b in r.Bodies)
                    snapshot.AppendLine("Body "+b.Id+" mu="+SnapshotAdapter.F(b.Mu)+" radius="+SnapshotAdapter.F(b.Radius)+" SOI="+SnapshotAdapter.F(b.Soi)+
                        " terrain="+SnapshotAdapter.F(b.TerrainCeiling)+" atmosphere="+SnapshotAdapter.F(b.AtmosphereHeight)+" R="+Vector(b.EpochState.R)+" V="+Vector(b.EpochState.V));
            }
            File.WriteAllText(file,"Build "+Build+"\n"+context+"\n"+status+"\n"+report+"\n"+patchReport+"\n"+
                string.Join("\n",fields.Select(p=>p.Key+" = "+p.Value))+"\nTerrain ceiling confirmed = "+terrainConfirmed+"\n"+snapshot);
            status="Diagnostics written: "+file; Debug.Log("[KerbalSlingshot] "+status);
        }

    }
}
