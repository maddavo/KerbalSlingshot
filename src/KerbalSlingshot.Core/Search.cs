using System;
using System.Collections.Generic;
using System.Threading;

namespace KerbalSlingshot.Core
{
    public enum SearchStatus { OfflineFeasible, NoSolutionFoundWithinBounds, InvalidInput, Cancelled }
    public sealed class SearchResult
    {
        public SearchStatus Status { get; }
        public string Message { get; }
        public Burn? Candidate { get; }
        public Evaluation? Evaluation { get; }
        public int Evaluations { get; }
        public IReadOnlyDictionary<string,int> Rejections { get; }
        public bool KspValidated => false;
        public SearchResult(SearchStatus status,string message,Burn? candidate,Evaluation? evaluation,int count,Dictionary<string,int> rejections)
        { Status=status; Message=message; Candidate=candidate; Evaluation=evaluation; Evaluations=count;
          Rejections=new System.Collections.ObjectModel.ReadOnlyDictionary<string,int>(rejections); }
    }

    public static class Search
    {
        // Bounded coordinate pattern search, deterministic seed order. Seeds carry no feasibility authority.
        // This is a local increment, not a Lambert generator or a global optimiser.
        public static SearchResult Solve(Request r,IReadOnlyList<Burn> seeds,int budget,double timeStep,double velocityStep,
            CancellationToken cancellation=default)
        {
            var rejected=new Dictionary<string,int>();
            int count=0;
            Burn? best=null;
            Evaluation? accepted=null;
            string? invalid=r.InvalidReason();
            SearchResult End(SearchStatus s,string message) => new SearchResult(s,message,best,accepted,count,rejected);
            if (invalid!=null || seeds.Count==0 || budget<=0 || !Numeric.Finite(timeStep) || timeStep<=0 ||
                !Numeric.Finite(velocityStep) || velocityStep<=0)
                return End(SearchStatus.InvalidInput,invalid ?? "invalid search controls");
            foreach (Burn seed in seeds)
                if (!Numeric.Finite(seed.UT) || !seed.DeltaV.Finite) return End(SearchStatus.InvalidInput,"nonfinite seed");
            Evaluation Evaluate(Burn b)
            {
                count++;
                Evaluation e=Trajectory.Evaluate(r,b,cancellation);
                if (e.Status!=EvaluationStatus.OfflineFeasible)
                { rejected.TryGetValue(e.Reason,out int n); rejected[e.Reason]=n+1; }
                else if (!best.HasValue || b.DeltaV.Length<best.Value.DeltaV.Length ||
                    (b.DeltaV.Length==best.Value.DeltaV.Length &&
                    e.Events[e.Events.Count-1].UT-b.UT<accepted!.Events[accepted.Events.Count-1].UT-best.Value.UT))
                { best=b; accepted=e; }
                return e;
            }
            foreach (Burn seed in seeds)
            {
                if (cancellation.IsCancellationRequested) return End(SearchStatus.Cancelled,"cancelled");
                if (count>=budget) break;
                Burn current=seed;
                Evaluation ce=Evaluate(current);
                double ts=timeStep,vs=velocityStep;
                while (count<budget && vs>=1e-5 && ts>=1e-5)
                {
                    if (cancellation.IsCancellationRequested) return End(SearchStatus.Cancelled,"cancelled");
                    bool improved=false;
                    for (int axis=0;axis<4 && count<budget;axis++)
                        for (int sign=-1;sign<=1 && count<budget;sign+=2)
                        {
                            V3 offset=new V3(axis==1?sign*vs:0,axis==2?sign*vs:0,axis==3?sign*vs:0);
                            Burn trial=new Burn(current.UT+(axis==0?sign*ts:0),current.DeltaV+offset);
                            if (trial.UT<r.Earliest || trial.UT>r.Latest || trial.DeltaV.Length>r.MaxDeltaV) continue;
                            Evaluation te=Evaluate(trial);
                            if (te.Status==EvaluationStatus.Cancelled) return End(SearchStatus.Cancelled,"cancelled");
                            if (te.Status!=EvaluationStatus.NumericalFailure && te.Status!=EvaluationStatus.PropagationBudgetExhausted &&
                                te.Status!=EvaluationStatus.InvalidInput && te.Score<ce.Score)
                            { current=trial; ce=te; improved=true; }
                        }
                    if (!improved) { ts/=2; vs/=2; }
                }
            }
            return best.HasValue?End(SearchStatus.OfflineFeasible,"offline constraints passed; KSP validation pending"):
                End(SearchStatus.NoSolutionFoundWithinBounds,"no solution found within bounds");
        }
    }
}
