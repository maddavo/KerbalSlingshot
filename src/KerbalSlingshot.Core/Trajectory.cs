using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KerbalSlingshot.Core
{
    public static class Trajectory
    {
        public static State BodyAt(Request r, Body b, double ut) => Kepler.Propagate(b.EpochState,r.ParentMu,ut-r.Epoch);

        public static Evaluation Evaluate(Request r, Burn burn, CancellationToken cancellation=default)
        {
            var events=new List<EncounterEvent>();
            int steps=0;
            double score=1e6;
            Evaluation End(EvaluationStatus status,string reason,double? alt=null) => new Evaluation(status,reason,events,score,steps,alt);
            string? invalid=r.InvalidReason();
            if (invalid!=null || !Numeric.Finite(burn.UT) || !burn.DeltaV.Finite || burn.UT<r.Earliest ||
                burn.UT>r.Latest || burn.DeltaV.Length>r.MaxDeltaV)
                return End(EvaluationStatus.InvalidInput,invalid ?? "burn outside bounds");
            try
            {
                // This increment cannot traverse child SOIs before departure. Check the unburned interval.
                double coast=r.Epoch;
                while (coast<burn.UT)
                {
                    if (cancellation.IsCancellationRequested) return End(EvaluationStatus.Cancelled,"cancelled");
                    if (++steps>r.MaxSteps) return End(EvaluationStatus.PropagationBudgetExhausted,"preburn scan budget exhausted");
                    State s=Kepler.Propagate(r.Vessel,r.ParentMu,coast-r.Epoch);
                    double h=Math.Min(r.MaxStep,burn.UT-coast);
                    foreach (Body b in r.Bodies)
                    {
                        State bs=BodyAt(r,b,coast);
                        h=Math.Min(h,b.Soi/(16*((s.V-bs.V).Length+Math.Sqrt(b.Mu/b.Soi))));
                    }
                    State next=Kepler.Propagate(r.Vessel,r.ParentMu,coast+h-r.Epoch);
                    if (ParentUnsafe(r,s,next,h)) return End(EvaluationStatus.Rejected,"preburn parent clearance or escape");
                    foreach (Body b in r.Bodies)
                    {
                        State Relative(double ut)
                        {
                            State vs=Kepler.Propagate(r.Vessel,r.ParentMu,ut-r.Epoch),bs=BodyAt(r,b,ut);
                            return new State(vs.R-bs.R,vs.V-bs.V);
                        }
                        State ra=Relative(coast),rb=Relative(coast+h);
                        bool hit=ra.R.Length<=b.Soi || rb.R.Length<=b.Soi;
                        if (!hit && V3.Dot(ra.R,ra.V)<0 && V3.Dot(rb.R,rb.V)>0)
                        {
                            double closest=Root(ut=> { State rr=Relative(ut); return V3.Dot(rr.R,rr.V); },coast,coast+h);
                            hit=Relative(closest).R.Length<=b.Soi;
                        }
                        if (hit) return End(EvaluationStatus.Rejected,"preburn encounter is unsupported");
                    }
                    coast+=h;
                }
                State departure=Kepler.Propagate(r.Vessel,r.ParentMu,burn.UT-r.Epoch);
                State patch=new State(departure.R,departure.V+burn.DeltaV);
                double patchUT=burn.UT,t=patchUT,limit=burn.UT+r.Duration;
                Body? active=null;
                int stage=0; // 0 before assist, 1 inside assist, 2 after assist, 3 inside destination
                bool assistPeriapsis=false;
                double assistEnergy=0;

                while (t<limit)
                {
                    if (cancellation.IsCancellationRequested) return End(EvaluationStatus.Cancelled,"cancelled");
                    if (++steps>r.MaxSteps) return End(EvaluationStatus.PropagationBudgetExhausted,"propagation scan budget exhausted");
                    if (events.Count>8) return End(EvaluationStatus.Rejected,"patch event limit");
                    double mu=active?.Mu ?? r.ParentMu;
                    State At(double ut) => Kepler.Propagate(patch,mu,ut-patchUT);
                    State ParentAt(double ut)
                    {
                        State ss=At(ut);
                        if (active==null) return ss;
                        State bs=BodyAt(r,active,ut);
                        return new State(ss.R+bs.R,ss.V+bs.V);
                    }
                    State now=At(t),parent=ParentAt(t);
                    double h=Math.Min(r.MaxStep,limit-t);
                    foreach (Body b in r.Bodies)
                    {
                        State bs=BodyAt(r,b,t);
                        h=Math.Min(h,b.Soi/(16*((parent.V-bs.V).Length+Math.Sqrt(b.Mu/b.Soi))));
                    }
                    if (!Numeric.Finite(h) || h<=1e-9) return End(EvaluationStatus.NumericalFailure,"event step underflow");
                    double nextT=t+h;
                    State next=At(nextT);
                    if (active==null && ParentUnsafe(r,now,next,h)) return End(EvaluationStatus.Rejected,"parent clearance or escape");
                    if (active!=null && (parent.R.Length<=r.ParentSafeRadius || parent.R.Length>=r.ParentSoi))
                        return End(EvaluationStatus.Rejected,"unsupported parent boundary");

                    string? kind=null;
                    Body? hit=null;
                    double eventT=nextT+1;
                    void Choose(string k,Body? body,Func<double,double> f,double? upper=null)
                    {
                        double root=Root(f,t,upper ?? nextT);
                        if (root<eventT) { eventT=root; kind=k; hit=body; }
                    }
                    // Find competing entries in parent coordinates, including wrong-order encounters.
                    foreach (Body b in r.Bodies)
                    {
                        if (b==active) continue;
                        double Boundary(double ut) => (ParentAt(ut).R-BodyAt(r,b,ut).R).Length-b.Soi;
                        double g0=Boundary(t),g1=Boundary(nextT);
                        if (g0>=-1e-5 && g1< -1e-5) Choose("entry",b,Boundary);
                        // A grazing entry and exit can both lie inside one scan interval.
                        // Bracket a closest approach using relative radial velocity before declaring a miss.
                        if (g0>=0 && g1>=0)
                        {
                            double Radial(double ut)
                            {
                                State ss=ParentAt(ut),bs=BodyAt(r,b,ut);
                                return V3.Dot(ss.R-bs.R,ss.V-bs.V);
                            }
                            if (Radial(t)<0 && Radial(nextT)>0)
                            {
                                double closest=Root(Radial,t,nextT);
                                double gc=Boundary(closest);
                                if (gc< -1e-5) Choose("entry",b,Boundary,closest);
                                g1=Math.Min(g1,gc);
                            }
                        }
                        if (g0< -1e-5) return End(EvaluationStatus.Rejected,"overlapping or unexpected SOI");
                        if (active==null && b.Id==(stage==0?r.Assist:r.Destination))
                            score=Math.Min(score,(stage==0?3e4:1e4)+Math.Max(0,Math.Min(g0,g1))/b.Soi);
                    }
                    if (active!=null)
                    {
                        if (now.R.Length<=active.Soi+1e-5 && next.R.Length>active.Soi+1e-5)
                            Choose("exit",active,ut=>At(ut).R.Length-active.Soi);
                        if (V3.Dot(now.R,now.V)<0 && V3.Dot(next.R,next.V)>=0)
                            Choose("periapsis",active,ut=>V3.Dot(At(ut).R,At(ut).V));
                    }
                    if (kind==null) { t=nextT; continue; }
                    State at=At(eventT),par=ParentAt(eventT);
                    if (kind=="entry")
                    {
                        if (active!=null || hit==null || (stage==0?hit.Id!=r.Assist:stage!=2 || hit.Id!=r.Destination))
                            return End(EvaluationStatus.Rejected,"unexpected encounter or event order");
                        State bs=BodyAt(r,hit,eventT);
                        State rel=new State(par.R-bs.R,par.V-bs.V);
                        events.Add(new EncounterEvent(kind,hit.Id,eventT,rel,par));
                        if (stage==0)
                        {
                            assistEnergy=Numeric.Energy(rel,hit.Mu);
                            if (assistEnergy<=0) return End(EvaluationStatus.Rejected,"assist entry is not unbound");
                            double pe=Numeric.Periapsis(rel,hit.Mu);
                            if (pe<Math.Max(hit.SafeRadius(r.ClearanceMargin),hit.Radius+r.MinimumAssistAltitude))
                                return End(EvaluationStatus.Rejected,"unsafe assist periapsis");
                            stage=1; score=2.5e4;
                        }
                        else { stage=3; score=5e3; }
                        active=hit; patch=rel; patchUT=eventT; t=eventT;
                    }
                    else if (kind=="periapsis" && active!=null)
                    {
                        events.Add(new EncounterEvent(kind,active.Id,eventT,at,par));
                        double altitude=at.R.Length-active.Radius;
                        if (at.R.Length<active.SafeRadius(r.ClearanceMargin)) return End(EvaluationStatus.Rejected,"unsafe periapsis");
                        if (stage==1)
                        {
                            if (altitude<r.MinimumAssistAltitude) return End(EvaluationStatus.Rejected,"unsafe assist altitude");
                            assistPeriapsis=true;
                        }
                        else if (stage==3)
                        {
                            score=Math.Abs(altitude-r.TargetAltitude);
                            string[] sequence=events.Select(e=>e.Kind+":"+e.Body).ToArray();
                            string[] expected={"entry:"+r.Assist,"periapsis:"+r.Assist,"exit:"+r.Assist,"entry:"+r.Destination,"periapsis:"+r.Destination};
                            if (!sequence.SequenceEqual(expected) || events.Zip(events.Skip(1),(a,b)=>a.UT<b.UT).Any(x=>!x))
                                return End(EvaluationStatus.Rejected,"invalid complete event sequence",altitude);
                            return End(score<=r.AltitudeTolerance?EvaluationStatus.OfflineFeasible:EvaluationStatus.Rejected,
                                score<=r.AltitudeTolerance?"offline constraints passed; KSP validation pending":"destination periapsis outside tolerance",altitude);
                        }
                        t=eventT; // At root rdot is ~0; advance a tiny amount to avoid redetecting the same event.
                        t=Math.Min(limit,t+1e-7);
                    }
                    else if (kind=="exit" && active!=null)
                    {
                        if (stage!=1 || !assistPeriapsis || Numeric.Energy(at,active.Mu)<=0 ||
                            V3.Dot(at.R,at.V)<=0 || Math.Abs(Numeric.Energy(at,active.Mu)-assistEnergy)>1e-7*Math.Max(1,assistEnergy))
                            return End(EvaluationStatus.Rejected,"assist exit is not a continuous unpowered escape");
                        events.Add(new EncounterEvent(kind,active.Id,eventT,at,par));
                        patch=par; patchUT=eventT; t=eventT; active=null; stage=2; score=2e4;
                    }
                }
                return End(EvaluationStatus.Rejected,"journey bound exhausted without complete encounter");
            }
            catch (ArithmeticException e) { return End(EvaluationStatus.NumericalFailure,e.Message); }
            catch (ArgumentException e) { return End(EvaluationStatus.NumericalFailure,e.Message); }
        }

        private static bool ParentUnsafe(Request r,State a,State b,double h)
        {
            if (a.R.Length<=r.ParentSafeRadius || b.R.Length<=r.ParentSafeRadius ||
                a.R.Length>=r.ParentSoi || b.R.Length>=r.ParentSoi) return true;
            if (V3.Dot(a.R,a.V)<0 && V3.Dot(b.R,b.V)>=0)
            {
                double peri=Root(dt=> { State s=Kepler.Propagate(a,r.ParentMu,dt); return V3.Dot(s.R,s.V); },0,h);
                return Kepler.Propagate(a,r.ParentMu,peri).R.Length<=r.ParentSafeRadius;
            }
            if (V3.Dot(a.R,a.V)>0 && V3.Dot(b.R,b.V)<=0)
            {
                double apo=Root(dt=> { State s=Kepler.Propagate(a,r.ParentMu,dt); return V3.Dot(s.R,s.V); },0,h);
                return Kepler.Propagate(a,r.ParentMu,apo).R.Length>=r.ParentSoi;
            }
            return false;
        }

        private static double Root(Func<double,double> f,double lo,double hi)
        {
            double flo=f(lo);
            for (int i=0;i<60 && hi-lo>1e-9;i++)
            {
                double mid=(lo+hi)/2, fm=f(mid);
                if ((fm>=0)==(flo>=0)) { lo=mid; flo=fm; } else hi=mid;
            }
            return (lo+hi)/2;
        }
    }
}
