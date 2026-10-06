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
    public sealed class PlannerPlugin : MonoBehaviour
    {
        private const string InputLock="KerbalSlingshot.UI";
        private readonly Dictionary<string,string> fields=PlannerSettings.Defaults();
        private readonly RevisionGate gate=new RevisionGate();
        private string[] bodyIds=Array.Empty<string>();
        private string context="Read the active vessel to choose its sibling bodies.";
        private string status="Starting estimates are not solutions. No node creation in this prototype.";
        private string report="",patchReport="";
        private int assistIndex,destinationIndex;
        private bool visible=true,terrainConfirmed;
        private Rect window=new Rect(20,80,680,680);
        private Vector2 scroll;
        private float lastFreshness;
        private Job? job;
        private CapturedPlan? displayedPlan;
        private Burn? displayedBurn;
        private string contextVessel="",contextParent="";
        private static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
        private static string Build => typeof(PlannerPlugin).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.2.0";
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
            Debug.Log("[KerbalSlingshot] Flight planning plugin "+Build+"; KSP validation pending; node creation unavailable.");
            Try(ReadContext);
        }
        public void OnDestroy()
        {
            AbandonJob(); gate.Invalidate(); InputLockManager.RemoveControlLock(InputLock);
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
            gate.Invalidate(); AbandonJob(); displayedPlan=null; displayedBurn=null; report=""; patchReport=""; status=reason;
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
            Invalidate("Vessel/body list refreshed. Enter or import a starting estimate.");
            Vessel v=SnapshotAdapter.Active();
            CelestialBody parent=v.orbit.referenceBody;
            bodyIds=SnapshotAdapter.Children(parent).Select(b=>b.bodyName).ToArray();
            terrainConfirmed=false;
            contextVessel=v.id.ToString(); contextParent=parent.bodyName;
            assistIndex=0; destinationIndex=bodyIds.Length>1?1:0;
            double now=Planetarium.GetUniversalTime();
            fields["Departure UT"]=SnapshotAdapter.F(now+600);
            context=v.vesselName+" | parent "+parent.bodyName+" | captured UT "+SnapshotAdapter.F(now)+" | children "+bodyIds.Length;
            if (bodyIds.Length<2) status="Unsupported route: this parent has fewer than two direct children. Parking-orbit departures/planetary escape are not supported.";
        }
        private void ImportNode()
        {
            ManeuverNode node=SnapshotAdapter.SingleFutureNode();
            Invalidate("Imported existing node as an estimate only. Native frame check passed; KSP trajectory validation still pending.");
            fields["Departure UT"]=SnapshotAdapter.F(node.UT);
            fields["Radial m/s"]=SnapshotAdapter.F(node.DeltaV.x);
            fields["Normal m/s"]=SnapshotAdapter.F(node.DeltaV.y);
            fields["Prograde m/s"]=SnapshotAdapter.F(node.DeltaV.z);
        }
        private void Begin(bool refine)
        {
            Vessel v=SnapshotAdapter.Active();
            if (v.id.ToString()!=contextVessel || v.orbit.referenceBody.bodyName!=contextParent)
                throw new ArgumentException("Vessel/parent changed: read vessel / bodies again");
            if (bodyIds.Length<2) throw new ArgumentException("Unsupported shared-parent route");
            PlannerSettings settings=PlannerSettings.Parse(fields);
            CapturedPlan plan=SnapshotAdapter.Capture(settings,bodyIds[assistIndex],bodyIds[destinationIndex],terrainConfirmed);
            Invalidate(refine?"Refining a starting estimate...":"Evaluating the exact starting estimate...");
            int revision=gate.Invalidate();
            var next=new Job(plan,settings,revision,refine);
            job=next;
            next.Cancellation.CancelAfter(TimeSpan.FromSeconds(settings.WallSeconds));
            // Only detached numeric state and cancellation/progress primitives enter the worker.
            next.Work=Task.Run(()=>
            {
                if (refine)
                    return Search.Solve(plan.Request,new[]{plan.Seed},settings.Budget,settings.TimeStep,settings.VelocityStep,
                        next.Cancellation.Token,(count,reason)=>Volatile.Write(ref next.Progress,new Progress(count,reason)));
                Evaluation evaluation=NumericalTrajectory.Evaluate(plan.Request,plan.Seed,next.Cancellation.Token);
                Volatile.Write(ref next.Progress,new Progress(1,evaluation.Reason));
                SearchStatus resultStatus=evaluation.Status==EvaluationStatus.OfflineFeasible?SearchStatus.OfflineFeasible:
                    evaluation.Status==EvaluationStatus.Cancelled?SearchStatus.Cancelled:
                    evaluation.Status==EvaluationStatus.InvalidInput?SearchStatus.InvalidInput:SearchStatus.NoSolutionFoundWithinBounds;
                bool feasible=evaluation.Status==EvaluationStatus.OfflineFeasible;
                return new SearchResult(resultStatus,evaluation.Reason,feasible?(Burn?)plan.Seed:null,
                    feasible?evaluation:null,1,new Dictionary<string,int>{{evaluation.Reason,1}},plan.Seed,evaluation);
            });
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
                if (result.Status==SearchStatus.Cancelled)
                    status="Wall budget exhausted: no solution found within bounds. Recalculate with suitable bounds/estimate.";
                else if (result.Status==SearchStatus.OfflineFeasible)
                    status="OFFLINE-FEASIBLE PREDICTION — KSP validation pending. Node creation unavailable. "+result.Message;
                else status=finished.Refine?result.Message:"Estimate rejected: "+result.Message;
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
            text.AppendLine("Starting estimate UT "+SnapshotAdapter.F(plan.Seed.UT)+" | inertial DV "+Vector(plan.Seed.DeltaV));
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
            text.AppendLine("KSP-validated = false. No live node was created/edited/deleted.");
            return text.ToString();
        }

        private void ReadPatches()
        {
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

        public void OnGUI()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            GUISkin previousSkin=GUI.skin;
            try
            {
            GUI.skin=HighLogic.Skin;
            if (GUI.Button(new Rect(Screen.width-125,40,115,28),"Slingshot")) visible=!visible;
            if (!visible) { InputLockManager.RemoveControlLock(InputLock); return; }
            window.height=Math.Min(720,Math.Max(300,Screen.height-100));
            window=GUILayout.Window(GetInstanceID(),window,DrawWindow,"KerbalSlingshot "+Build,GUILayout.Width(680),GUILayout.Height(window.height));
            bool lockControls=window.Contains(Event.current.mousePosition) || GUI.GetNameOfFocusedControl().StartsWith("slingshot:",StringComparison.Ordinal);
            if (lockControls) InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS,InputLock);
            else InputLockManager.RemoveControlLock(InputLock);
            }
            finally { GUI.skin=previousSkin; }
        }
        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Read vessel / bodies")) Try(ReadContext);
            if (GUILayout.Button("Import first future node")) Try(ImportNode);
            if (GUILayout.Button("Hide")) { visible=false; GUI.FocusControl(""); }
            GUILayout.EndHorizontal();
            scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label(context);
            GUILayout.Label("Shared-parent route only. No parking-orbit departure or planetary escape. UT seconds; altitudes km.");
            if (bodyIds.Length>0)
            {
                GUILayout.Label("Gravity Assist Target");
                int next=GUILayout.SelectionGrid(assistIndex,bodyIds,Math.Min(3,bodyIds.Length));
                if (next!=assistIndex) { assistIndex=next; Invalidate("Assist changed; recalculate."); }
                GUILayout.Label("Intercept Target");
                next=GUILayout.SelectionGrid(destinationIndex,bodyIds,Math.Min(3,bodyIds.Length));
                if (next!=destinationIndex) { destinationIndex=next; Invalidate("Destination changed; recalculate."); }
            }
            GUILayout.Label("Starting estimate: native node radial / normal / prograde in m/s. Import a manually planned node or type an estimate. No general seed generator.");
            foreach (string key in fields.Keys.ToArray())
            {
                GUILayout.BeginHorizontal(); GUILayout.Label(key,GUILayout.Width(190));
                GUI.SetNextControlName("slingshot:"+key);
                string value=GUILayout.TextField(fields[key],GUILayout.Width(230));
                if (value!=fields[key]) { fields[key]=value; Invalidate("Input changed; recalculate."); }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Terrain ceiling applies to the parent and every child. Supply a conservative maximum; this build does not measure terrain maxima.");
            bool confirmed=GUILayout.Toggle(terrainConfirmed,"I have checked that the terrain ceiling is conservative for this system");
            if (confirmed!=terrainConfirmed) { terrainConfirmed=confirmed; Invalidate("Safety assumption changed; recalculate."); }
            GUILayout.BeginHorizontal();
            bool priorEnabled=GUI.enabled;
            GUI.enabled=job==null && bodyIds.Length>=2;
            if (GUILayout.Button("Evaluate estimate")) Try(()=>Begin(false));
            if (GUILayout.Button("Refine estimate")) Try(()=>Begin(true));
            GUI.enabled=priorEnabled;
            if (GUILayout.Button("Cancel")) Invalidate("Cancelled; no nodes changed.");
            GUILayout.EndHorizontal();
            if (job!=null)
            {
                Progress progress=Volatile.Read(ref job.Progress);
                GUILayout.Label("Working: "+progress.Count+" / "+job.Settings.Budget+" evaluations | "+progress.Reason);
            }
            GUILayout.Label(status); GUILayout.Label(report);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Read existing KSP patches")) Try(ReadPatches);
            if (GUILayout.Button("Write diagnostics")) Try(WriteDiagnostics);
            GUILayout.EndHorizontal();
            GUILayout.Label(patchReport);
            GUILayout.Label("Node creation is unavailable. Predictions remain unvalidated against KSP until actually compared in game.");
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0,0,window.width,24));
        }
    }
}
