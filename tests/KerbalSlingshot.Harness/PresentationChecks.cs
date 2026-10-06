using KerbalSlingshot.Core;

namespace KerbalSlingshot.Harness;

internal static class PresentationChecks
{
    public static void Run(string directory,Action<string,Action> check)
    {
        static void Require(bool value,string reason) { if (!value) throw new Exception(reason); }
        check("UI search locks conflicting actions while keeping cancellation available",()=>
        {
            var busy=new PlannerButtons(true,true,true,true,true,true);
            Require(!busy.Edit && !busy.Import && !busy.Calculate && busy.Cancel,"busy button states");
            var ready=new PlannerButtons(false,true,true,true,true,true);
            Require(ready.Edit && ready.Import && ready.Calculate && !ready.Cancel,"ready button states");
            foreach(var blocked in new[] {
                new PlannerButtons(false,false,true,true,true,true),new PlannerButtons(false,true,false,true,true,true),
                new PlannerButtons(false,true,true,false,true,true),new PlannerButtons(false,true,true,true,false,true),
                new PlannerButtons(false,true,true,true,true,false) }) Require(!blocked.Calculate,"unready calculation enabled");
        });
        check("UI complete result preserves five event rows and unvalidated status",()=>
        {
            Fixture f=Fixture.Read(Path.Combine(directory,"reachable.json"));
            Evaluation e=Trajectory.Evaluate(f.Request(),f.KnownBurn.Burn());
            var result=new SearchResult(SearchStatus.OfflineFeasible,"offline",f.KnownBurn.Burn(),e,1,new());
            var card=new PlannerResultCard(result,f.Request(),false);
            Require(card.Accepted && card.Heading=="Offline-feasible prediction" && card.Validation.Contains("Unvalidated"),"result authority");
            Require(card.Events.Count==5 && card.Events.All(r=>r.Time!="--" && r.Altitude!="--"),"missing events");
            Require(card.Departure.EndsWith("UT") && card.DeltaV.EndsWith("m/s") && card.Error.Contains("tolerance"),"result units");
        });
        check("UI partial/missed route never fills missing events or claims success",()=>
        {
            Fixture f=Fixture.Read(Path.Combine(directory,"constrained-miss.json"));
            Evaluation e=Trajectory.Evaluate(f.Request(),f.KnownBurn.Burn());
            var result=new SearchResult(SearchStatus.NoSolutionFoundWithinBounds,"no solution",null,null,1,new(),f.KnownBurn.Burn(),e);
            var card=new PlannerResultCard(result,f.Request(),true);
            Require(!card.Accepted && card.Heading=="No solution found within bounds","miss label");
            Require(card.Validation.Contains("diagnostics"),"partial result lacks diagnostic label");
            Require(card.Events.Count==5 && card.Events[3].Time=="--" && card.Events[4].Altitude=="--" && card.Error=="Not reached","missing values fabricated");
            var bad=new SearchResult(SearchStatus.OfflineFeasible,"wrong",f.KnownBurn.Burn(),e,1,new());
            Require(!new PlannerResultCard(bad,f.Request(),false).Accepted,"rejected evaluation promoted");
        });
        check("UI fixed primary geometry fits 1920x1080 without primary scrolling",()=>
        {
            Require(PlannerLayout.Width<680 && PlannerLayout.PrimaryHeight<720,"window not reduced");
            Require(PlannerLayout.EventTop+5*22+24<=574,"events/error overlap Advanced");
            Require(PlannerLayout.FooterTop+24<PlannerLayout.PrimaryHeight,"footer clipped");
            Require(110+PlannerLayout.Height(false,false,false,1080)<=1080,"initial bounds");
            Require(PlannerLayout.Height(true,true,false,1080)<=980 && PlannerLayout.Height(true,false,true,1080)<=980,"expanded panels exceed viewport");
        });
        check("UI grouping leaves parsing and existing default safety settings intact",()=>
        {
            var fields=PlannerSettings.Defaults();
            var saved=new Dictionary<string,string>(fields);
            PlannerSettings.Parse(fields);
            Require(fields.SequenceEqual(saved),"presentation/parsing changed defaults");
            Require(fields["Terrain ceiling km"]=="10" && fields["Clearance km"]=="1" && fields["Pe tolerance m"]=="100","safe defaults changed");
            Require(PlannerResultCard.Time(26626535.550196137)=="26626535.550","UT format");
        });
    }
}
