using System.Globalization;
using KerbalSlingshot.Core;

namespace KerbalSlingshot.Harness;

internal static class PlanningChecks
{
    public static void Run(string directory,Action<string,Action> check)
    {
        static void Require(bool value,string reason) { if (!value) throw new Exception(reason); }
        check("planning UI SI conversion and invariant parsing",()=>
        {
            var fields=PlannerSettings.Defaults(); fields["Departure UT"]="12345.25";
            fields["Intercept Pe km"]="12.5"; fields["Normal m/s"]="-3.75";
            CultureInfo previous=CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");
                PlannerSettings settings=PlannerSettings.Parse(fields);
                Require(settings.TargetAltitude==12500 && settings.SeedUT==12345.25 && settings.SeedComponents.Y==-3.75,"unit/culture conversion");
            }
            finally { CultureInfo.CurrentCulture=previous; }
        });
        check("planning input rejects ambiguity, nonfinite values and unbounded work",()=>
        {
            foreach ((string key,string value) in new[]{("Departure UT","NaN"),("Radial m/s","Infinity"),("Intercept Pe km","1,5"),
                ("Journey limit s","0"),("Scan steps","2.5"),("Evaluations","20001"),("Wall budget s","121"),
                ("Clearance km","-1"),("Terrain ceiling km","1e308")})
            {
                var fields=PlannerSettings.Defaults(); fields[key]=value;
                try { PlannerSettings.Parse(fields); throw new Exception("Accepted bad field: "+key); }
                catch (ArgumentException) { }
            }
        });
        check("native burn-frame round trip preserves signs and magnitude",()=>
        {
            var frame=new BurnFrame(new V3(.6,.8,0),new V3(0,0,-1),new V3(-.8,.6,0));
            V3 components=new(4,-7,13),inertial=frame.ToInertial(components);
            Require((frame.ToComponents(inertial)-components).Length<1e-12,"native conversion");
            Require(Math.Abs(inertial.Length-components.Length)<1e-12,"magnitude changed");
            try { _=new BurnFrame(new V3(1,0,0),new V3(1,0,0),new V3(0,1,0)); throw new Exception("invalid frame accepted"); }
            catch (ArgumentException) { }
        });
        check("ordinary conic coast stays fresh; burn and time reversal invalidate",()=>
        {
            State start=new(new V3(1e6,0,0),new V3(0,100,0));
            State coast=Kepler.Propagate(start,1e10,600);
            Require(SnapshotFreshness.Matches(start,100,coast,700,1e10),"coast marked stale");
            Require(!SnapshotFreshness.Matches(start,100,new State(coast.R,coast.V+new V3(.2,0,0)),700,1e10),"burn kept fresh");
            Require(!SnapshotFreshness.Matches(start,100,new State(coast.R+new V3(100,0,0),coast.V),700,1e10),"position change kept fresh");
            Require(!SnapshotFreshness.Matches(start,100,start,99,1e10),"backward time kept fresh");
        });
        check("abandoned worker completion cannot publish into a new revision",()=>
        {
            var gate=new RevisionGate();
            int old=gate.Invalidate();
            var finish=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool> oldWorker=Task.Run(async()=> { await finish.Task; return gate.IsCurrent(old); });
            int current=gate.Invalidate();
            finish.SetResult(1);
            Require(!oldWorker.GetAwaiter().GetResult() && gate.IsCurrent(current),"old result was publishable");
            gate.Invalidate(); Require(!gate.IsCurrent(current),"cancel did not invalidate");
        });
        check("UI estimate translation still reproduces a complete fixture",()=>
        {
            Fixture f=Fixture.Read(Path.Combine(directory,"reachable-inclined.json"));
            var frame=new BurnFrame(new V3(1,0,0),new V3(0,.6,-.8),new V3(0,.8,.6));
            V3 native=frame.ToComponents(f.KnownBurn.Burn().DeltaV);
            Evaluation evaluation=Trajectory.Evaluate(f.Request(),new Burn(f.KnownBurn.UT,frame.ToInertial(native)));
            Require(evaluation.Status==EvaluationStatus.OfflineFeasible && evaluation.Events.Count==5,"estimate pipeline lost route");
            Require(!evaluation.KspValidated && !evaluation.CanCreateNode,"offline pipeline gained node authority");
        });
        check("rejected search retains partial diagnostics without promoting a seed",()=>
        {
            Fixture f=Fixture.Read(Path.Combine(directory,"constrained-miss.json"));
            int callbacks=0;
            SearchResult result=Search.Solve(f.Request(),f.Seeds.Select(s=>s.Burn()).ToArray(),f.SearchBudget,f.TimeStep,f.VelocityStep,
                default,(count,_)=> { Require(count==++callbacks,"progress count"); });
            Require(result.Status==SearchStatus.NoSolutionFoundWithinBounds && result.Candidate==null && result.Evaluation==null,"diagnostic promoted");
            Require(result.DiagnosticBurn.HasValue && result.DiagnosticEvaluation!=null && result.DiagnosticEvaluation.Events.Count>=3,"missing partial route");
            Require(callbacks==result.Evaluations && !result.KspValidated,"progress/validation state");
        });
        check("running search honours cancellation delivered through progress",()=>
        {
            Fixture f=Fixture.Read(Path.Combine(directory,"reachable.json"));
            using var cancellation=new CancellationTokenSource();
            SearchResult result=Search.Solve(f.Request(),f.Seeds.Select(s=>s.Burn()).ToArray(),f.SearchBudget,f.TimeStep,f.VelocityStep,
                cancellation.Token,(count,_)=> { if (count==3) cancellation.Cancel(); });
            Require(result.Status==SearchStatus.Cancelled && result.Evaluations<=4 && !result.KspValidated,"mid-search cancel failed");
        });
        check("wall-bounded completion retains only a fully feasible candidate",()=>
        {
            Fixture f=Fixture.Read(Path.Combine(directory,"reachable.json"));
            Burn burn=f.KnownBurn.Burn(); Evaluation e=Trajectory.Evaluate(f.Request(),burn);
            var timed=new SearchResult(SearchStatus.Cancelled,"cancelled",burn,e,2,new Dictionary<string,int>());
            SearchResult complete=PlannerCompletion.FinishWallBounded(timed);
            Require(complete.Status==SearchStatus.OfflineFeasible && !complete.KspValidated,"complete bounded candidate discarded/promoted to KSP");
            var empty=new SearchResult(SearchStatus.Cancelled,"cancelled",null,null,2,new Dictionary<string,int>(),burn,e);
            Require(PlannerCompletion.FinishWallBounded(empty).Status==SearchStatus.Cancelled,"diagnostic alone promoted");
        });
    }
}
