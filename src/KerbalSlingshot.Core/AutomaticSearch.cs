using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KerbalSlingshot.Core
{
    public static class Lambert
    {
        // Authored zero-revolution universal-variable solution. Both arc directions are explicit.
        public static bool Try(V3 r1,V3 r2,double mu,double dt,bool longArc,out State velocities)
        {
            velocities=default;
            if (!r1.Finite || !r2.Finite || mu<=0 || dt<=0) return false;
            double a=r1.Length,b=r2.Length,cos=V3.Dot(r1,r2)/(a*b);
            cos=Math.Max(-1,Math.Min(1,cos));
            double A=(longArc?-1:1)*Math.Sqrt(a*b*(1+cos));
            if (Math.Abs(A)<1e-6 || 1-cos<1e-12) return false;
            double Value(double z,out double y)
            {
                Kepler.Stumpff(z,out double c,out double s);
                y=a+b+A*(z*s-1)/Math.Sqrt(c);
                if (c<=0 || y<0) return double.NaN;
                return Math.Pow(y/c,1.5)*s+A*Math.Sqrt(y)-Math.Sqrt(mu)*dt;
            }
            double lo=0,hi=0,last=double.NaN,lastZ=0;
            bool bracket=false;
            for (int i=0;i<=120;i++)
            {
                double z=-4*Math.PI*Math.PI+(8*Math.PI*Math.PI-1e-4)*i/120;
                double value=Value(z,out _);
                if (Numeric.Finite(value) && Numeric.Finite(last) && last<=0 && value>=0)
                { lo=lastZ; hi=z; bracket=true; break; }
                if (Numeric.Finite(value)) { last=value; lastZ=z; }
            }
            if (!bracket) return false;
            for (int i=0;i<65;i++) { double mid=(lo+hi)/2; if (Value(mid,out _)>0) hi=mid; else lo=mid; }
            Value((lo+hi)/2,out double radius);
            double f=1-radius/a,g=A*Math.Sqrt(radius/mu),gd=1-radius/b;
            if (!Numeric.Finite(g) || Math.Abs(g)<1e-10) return false;
            velocities=new State((r2-r1*f)/g,(r2*gd-r1)/g);
            return velocities.R.Finite && velocities.V.Finite;
        }
    }

    public static class AutomaticSearch
    {
        public static IReadOnlyList<Burn> Seeds(Request r,int limit,CancellationToken token=default)
        {
            if (r.InvalidReason()!=null || limit<=0) return Array.Empty<Burn>();
            Body assist=r.Bodies.Single(b=>b.Id==r.Assist),destination=r.Bodies.Single(b=>b.Id==r.Destination);
            var guesses=new List<Tuple<double,Burn>>();
            for(int epoch=0;epoch<9;epoch++)
            {
                token.ThrowIfCancellationRequested();
                double t=r.Earliest+(r.Latest-r.Earliest)*epoch/8;
                State vessel=Kepler.Propagate(r.Vessel,r.ParentMu,t-r.Epoch);
                guesses.Add(Tuple.Create(1e8,new Burn(t,new V3(0,0,0))));
                double nominal=Math.PI*Math.Sqrt(Math.Pow((vessel.R.Length+assist.EpochState.R.Length)/2,3)/r.ParentMu);
                double close=(vessel.R-Trajectory.BodyAt(r,assist,t).R).Length/Math.Max(1,(vessel.V-Trajectory.BodyAt(r,assist,t).V).Length);
                foreach(double tof in new[]{close*.75,close,close*1.25,nominal*.5,nominal*.75,nominal,nominal*1.5,nominal*2})
                {
                    if(tof<1 || tof>r.Duration*.65) continue;
                    State moon=Trajectory.BodyAt(r,assist,t+tof);
                    foreach(bool longArc in new[]{false,true})
                    {
                        if(!Lambert.Try(vessel.R,moon.R,r.ParentMu,tof,longArc,out State initial)) continue;
                        V3 incoming=initial.V-moon.V;
                        double speed=incoming.Length;
                        if(speed<1e-6) continue;
                        V3 direction=incoming/speed;
                        V3 cross=V3.Cross(direction,new V3(0,0,1));
                        if(cross.Length<.01) cross=V3.Cross(direction,new V3(0,1,0));
                        V3 u=cross/cross.Length,w=V3.Cross(direction,u);
                        foreach(double altitude in new[]{Math.Max(r.MinimumAssistAltitude,assist.SafeRadius(r.ClearanceMargin)-assist.Radius),assist.Soi*.08,assist.Soi*.2,assist.Soi*.4})
                        {
                            double rp=assist.Radius+altitude;
                            if(rp>=assist.Soi || rp<assist.SafeRadius(r.ClearanceMargin)) continue;
                            double impact=rp*Math.Sqrt(1+2*assist.Mu/(rp*speed*speed));
                            double turn=2*Math.Asin(1/(1+rp*speed*speed/assist.Mu));
                            for(int azimuth=0;azimuth<12;azimuth++)
                            {
                                token.ThrowIfCancellationRequested();
                                double angle=azimuth*Math.PI/6;
                                V3 side=u*Math.Cos(angle)+w*Math.Sin(angle);
                                V3 aim=moon.R+side*impact;
                                if(!Lambert.Try(vessel.R,aim,r.ParentMu,tof,longArc,out State transfer)) continue;
                                V3 dv=transfer.R-vessel.V;
                                if(dv.Length>r.MaxDeltaV) continue;
                                V3 outgoing=moon.V+incoming*Math.Cos(turn)-side*(speed*Math.Sin(turn));
                                var arc=new State(aim,outgoing);
                                double rank=1e9;
                                for(int sample=1;sample<=12;sample++)
                                {
                                    double dt=(r.Duration-tof)*sample/12;
                                    try
                                    {
                                        State predicted=Kepler.Propagate(arc,r.ParentMu,dt);
                                        State target=Trajectory.BodyAt(r,destination,t+tof+dt);
                                        rank=Math.Min(rank,(predicted.R-target.R).Length/destination.Soi);
                                    }
                                    catch(ArithmeticException) { }
                                }
                                guesses.Add(Tuple.Create(rank+dv.Length/Math.Max(1,r.MaxDeltaV)*.01,new Burn(t,dv)));
                            }
                        }
                    }
                }
            }
            // Include a coasting seed (not a supplied burn) for very-near encounter starts.
            return new[]{new Burn(r.Earliest,new V3(0,0,0))}.Concat(guesses.OrderBy(g=>g.Item1).Select(g=>g.Item2))
                .GroupBy(b=>b.UT.ToString("G17")+b.DeltaV.ToString()).Select(g=>g.First()).Take(limit).ToArray();
        }

        public static SearchResult Solve(Request r,int budget,CancellationToken token=default,Action<int,string>? progress=null,double? timeStep=null,double? velocityStep=null)
        {
            var reasons=new Dictionary<string,int>();
            if(r.InvalidReason()!=null || budget<=0) return new SearchResult(SearchStatus.InvalidInput,r.InvalidReason()??"invalid budget",null,null,0,reasons);
            int count=0;
            SearchResult? best=null,diagnostic=null;
            var tighter=new Request(r.Epoch,r.ParentMu,r.ParentSafeRadius,r.ParentSoi,r.Vessel,r.Bodies,r.Assist,r.Destination,
                r.TargetAltitude,r.MinimumAssistAltitude,r.ClearanceMargin,r.AltitudeTolerance*.25,r.Earliest,r.Latest,r.Duration,r.MaxDeltaV,r.MaxStep,r.MaxSteps);
            try
            {
                progress?.Invoke(0,"Generating departure epochs and flyby geometry internally");
                var seeds=Seeds(r,48,token);
                // Give each basin a finite budget so the first guess cannot consume the whole search.
                foreach(Burn seed in seeds)
                {
                    if(count>=budget) break;
                    token.ThrowIfCancellationRequested();
                    int slice=Math.Min(budget-count,Math.Max(160,budget/12));
                    SearchResult result=Search.Solve(tighter,new[]{seed},slice,timeStep??Math.Max(1,(r.Latest-r.Earliest)/80),
                        velocityStep??Math.Max(.1,r.MaxDeltaV/100),token,(n,s)=>progress?.Invoke(count+n,s));
                    count+=result.Evaluations;
                    foreach(var pair in result.Rejections) { reasons.TryGetValue(pair.Key,out int n); reasons[pair.Key]=n+pair.Value; }
                    if(result.DiagnosticEvaluation!=null && (diagnostic?.DiagnosticEvaluation==null || result.DiagnosticEvaluation.Score<diagnostic.DiagnosticEvaluation.Score)) diagnostic=result;
                    if(result.Evaluation!=null && result.Candidate.HasValue &&
                        (best?.Candidate==null || result.Candidate.Value.DeltaV.Length<best.Candidate.Value.DeltaV.Length)) best=result;
                    if(result.Status==SearchStatus.Cancelled) break;
                    if(best!=null) break; // A feasible bounded result, never a global-optimum claim.
                }
            }
            catch(OperationCanceledException) { }
            return new SearchResult(best!=null?SearchStatus.OfflineFeasible:token.IsCancellationRequested?SearchStatus.Cancelled:SearchStatus.NoSolutionFoundWithinBounds,
                best!=null?"Complete automatic offline candidate; KSP validation pending":token.IsCancellationRequested?"cancelled":"no solution found within bounds",
                best?.Candidate,best?.Evaluation,count,reasons,diagnostic?.DiagnosticBurn,diagnostic?.DiagnosticEvaluation);
        }
    }
}
