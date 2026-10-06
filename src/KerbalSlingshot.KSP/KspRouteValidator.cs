using System;
using System.Collections.Generic;
using System.Linq;
using KerbalSlingshot.Core;

namespace KerbalSlingshot.KSP
{
    internal static class KspRouteValidator
    {
        public static ValidationResult Validate(CapturedPlan plan,Burn burn,Evaluation evaluation,out V3 components)
        {
            components=default;
            try
            {
                Vessel vessel=SnapshotAdapter.Active();
                if(vessel.patchedConicSolver==null) return new ValidationResult(false,"Manoeuvre planning unavailable; upgrade planning capability first");
                if(!SnapshotAdapter.Fresh(plan,burn.UT)) return new ValidationResult(false,"Stale vessel or node plan");
                if(HasFutureNodes(vessel)) return new ValidationResult(false,"Existing future node makes validation ambiguous; it was not changed");
                var coast=new Orbit(); double now=Planetarium.GetUniversalTime();
                coast.UpdateFromOrbitAtUT(vessel.orbit,now,vessel.orbit.referenceBody); coast.StartUT=now; coast.EndUT=burn.UT;
                PatchedConics.CalculatePatch(coast,new Orbit(),now,new PatchedConics.SolverParameters { FollowManeuvers=false },null);
                if(coast.patchEndTransition!=Orbit.PatchTransitionType.FINAL && coast.EndUT<=burn.UT)
                    return new ValidationResult(false,"KSP predicts an unsupported SOI transition before departure");
                Orbit after=ApiContract.BurnOrbit(vessel.orbit,burn,out components,out double error);
                if(error>Math.Max(1e-6,burn.DeltaV.Length*1e-8)) return new ValidationResult(false,"Native fixed-frame round trip disagrees: "+SnapshotAdapter.F(error)+" m/s");
                return Collect(plan.Request,burn,evaluation,after,true);
            }
            catch(Exception ex) { return new ValidationResult(false,"KSP validation failed: "+ex.Message); }
        }
        public static bool HasFutureNodes(Vessel vessel) => vessel.patchedConicSolver!=null &&
            vessel.patchedConicSolver.maneuverNodes.Any(n=>n.UT>=Planetarium.GetUniversalTime());

        private static ValidationResult Collect(Request request,Burn burn,Evaluation evaluation,Orbit first,bool calculate)
        {
            var events=new List<EncounterEvent>();
            Orbit patch=first;
            var seen=new HashSet<Orbit>();
            double horizon=burn.UT+request.Duration;
            var parameters=new PatchedConics.SolverParameters { FollowManeuvers=false };
            for(int i=0;i<8 && patch!=null && seen.Add(patch);i++)
            {
                string body=patch.referenceBody?.bodyName ?? "unknown";
                if(i==0 && body!="Kerbin") return new ValidationResult(false,"Unexpected departure reference body",events);
                Orbit next=calculate?new Orbit():patch.nextPatch;
                if(calculate)
                {
                    patch.EndUT=horizon;
                    PatchedConics.CalculatePatch(patch,next,patch.StartUT,parameters,null);
                }
                if(body=="Kerbin" && patch.PeR<request.ParentSafeRadius)
                {
                    double parentPe=patch.TimeOfTrueAnomaly(0,patch.StartUT);
                    if(parentPe>=patch.StartUT && parentPe<=Math.Min(patch.EndUT,horizon))
                        return new ValidationResult(false,"KSP parent patch violates terrain/atmosphere clearance",events);
                }
                if(body==request.Assist || body==request.Destination)
                {
                    EncounterEvent At(string kind,double ut)
                    {
                        State relative=ApiContract.ReadInertialState(patch,ut);
                        Body snapshot=request.Bodies.Single(b=>b.Id==body);
                        State planet=Core.Trajectory.BodyAt(request,snapshot,ut);
                        return new EncounterEvent(kind,body,ut,relative,new State(relative.R+planet.R,relative.V+planet.V));
                    }
                    events.Add(At("entry",patch.StartUT));
                    double pe=patch.TimeOfTrueAnomaly(0,patch.StartUT);
                    if(!Numeric.Finite(pe) || pe<=patch.StartUT || pe>=Math.Min(patch.EndUT,horizon))
                        return new ValidationResult(false,"KSP periapsis is outside the encounter patch",events);
                    events.Add(At("periapsis",pe));
                    if(body==request.Destination) return RouteValidation.Check(request,burn,evaluation,events);
                    if(patch.patchEndTransition!=Orbit.PatchTransitionType.ESCAPE || patch.eccentricity<=1)
                        return new ValidationResult(false,"Mun does not exit on an unbound KSP patch",events);
                    events.Add(At("exit",patch.EndUT));
                }
                if(patch.patchEndTransition==Orbit.PatchTransitionType.FINAL || !Numeric.Finite(patch.EndUT) || patch.EndUT>=horizon)
                    return new ValidationResult(false,"Incomplete KSP encounter chain within journey bounds",events);
                string expected=i==0?request.Assist:i==1?"Kerbin":i==2?request.Destination:"unexpected";
                if(next==null || next.referenceBody?.bodyName!=expected) return new ValidationResult(false,"Wrong KSP patch/body sequence",events);
                patch=next;
            }
            return new ValidationResult(false,"KSP patch chain exhausted",events);
        }

        public static ValidationResult Insert(CapturedPlan plan,Burn burn,Evaluation evaluation,V3 validatedComponents)
        {
            Vessel vessel=SnapshotAdapter.Active();
            if(vessel.patchedConicSolver==null) return new ValidationResult(false,"Manoeuvre planning is unavailable for this vessel");
            ValidationResult check=Validate(plan,burn,evaluation,out V3 components);
            if(!check.Passed || (components-validatedComponents).Length>1e-6) return new ValidationResult(false,check.Passed?"Native components changed":check.Reason);
            ManeuverNode? created=null;
            var original=vessel.patchedConicSolver.maneuverNodes.ToArray();
            try
            {
                created=vessel.patchedConicSolver.AddManeuverNode(burn.UT);
                created.DeltaV=ApiContract.ToNative(components);
                vessel.patchedConicSolver.UpdateFlightPlan();
                if(vessel.patchedConicSolver.maneuverNodes.Count!=original.Length+1 || original.Any(n=>!vessel.patchedConicSolver.maneuverNodes.Contains(n)))
                    throw new InvalidOperationException("Node-list integrity check failed");
                V3 actual=ApiContract.ToCore(created.GetBurnVector(created.patch).xzy);
                if(Math.Abs(created.UT-burn.UT)>1e-6 || (actual-burn.DeltaV).Length>Math.Max(1e-5,burn.DeltaV.Length*1e-8))
                    throw new InvalidOperationException("Inserted node time/vector mismatch");
                ValidationResult inserted=Collect(plan.Request,burn,evaluation,created.nextPatch,false);
                if(!inserted.Passed) throw new InvalidOperationException(inserted.Reason);
                return new ValidationResult(true,"One departure node created; resulting KSP patches verified",inserted.Events);
            }
            catch(Exception ex)
            {
                if(created==null)
                {
                    var added=vessel.patchedConicSolver.maneuverNodes.Where(n=>!original.Contains(n)).ToArray();
                    if(added.Length==1 && Math.Abs(added[0].UT-burn.UT)<1e-6) created=added[0];
                }
                try
                {
                    if(created!=null && vessel.patchedConicSolver.maneuverNodes.Contains(created)) created.RemoveSelf();
                    if(created!=null && vessel.patchedConicSolver.maneuverNodes.Contains(created))
                        return new ValidationResult(false,"Insertion failed and rollback could not remove its node: "+ex.Message);
                }
                catch(Exception rollback) { return new ValidationResult(false,"Insertion failed; rollback also failed: "+ex.Message+" / "+rollback.Message); }
                return new ValidationResult(false,"Insertion failed; operation's node absent/removed, unrelated nodes untouched: "+ex.Message);
            }
        }
    }
}
