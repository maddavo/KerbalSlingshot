using KerbalSlingshot.Core;

namespace KerbalSlingshot.Harness;

internal static class AutomaticChecks
{
    public static void Run(string directory,Action<string,Action> check)
    {
        static void Require(bool x,string reason) { if(!x) throw new Exception(reason); }
        check("automatic Lambert seeds hit the requested 3D endpoint",()=>
        {
            State start=new(new V3(1e6,0,0),new V3(0,80,60));
            State end=Kepler.Propagate(start,1e10,2500);
            Require(Lambert.Try(start.R,end.R,1e10,2500,false,out State velocities),"no Lambert arc");
            State actual=Kepler.Propagate(new State(start.R,velocities.R),1e10,2500);
            Require((actual.R-end.R).Length<.01,"Lambert endpoint mismatch");
        });
        check("internal seeds deterministic, bounded and cancellable",()=>
        {
            Fixture f=Fixture.Read(Path.Combine(directory,"reachable.json"));
            var first=AutomaticSearch.Seeds(f.Request(),20); var second=AutomaticSearch.Seeds(f.Request(),20);
            Require(first.Count>0 && first.Count<=20 && first.Select(x=>x.DeltaV.ToString()+x.UT).SequenceEqual(second.Select(x=>x.DeltaV.ToString()+x.UT)),"seed determinism");
            Require(first.All(s=>s.DeltaV.Finite && s.DeltaV.Length<=f.MaxDeltaV && s.UT>=f.Earliest && s.UT<=f.Latest),"seed bounds");
            using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
            Require(AutomaticSearch.Solve(f.Request(),100,cancelled.Token).Status==SearchStatus.Cancelled,"generation cancellation");
        });
        check("automatic complete route without passing oracle burn to planner",()=>
        {
            Fixture f=Fixture.Read(Path.Combine(directory,"reachable.json"));
            SearchResult result=AutomaticSearch.Solve(f.Request(),6000);
            Require(result.Status==SearchStatus.OfflineFeasible && result.Evaluation?.Events.Count==5,"automatic route not found");
            Require(result.Candidate.HasValue && !result.KspValidated,"missing candidate or false KSP claim");
        });
        check("automatic invalid, unsafe and exhausted outcomes",()=>
        {
            Fixture f=Fixture.Read(Path.Combine(directory,"invalid-input.json"));
            Require(AutomaticSearch.Solve(f.Request(),50).Status==SearchStatus.InvalidInput,"invalid request accepted");
            f=Fixture.Read(Path.Combine(directory,"constrained-miss.json"));
            SearchResult miss=AutomaticSearch.Solve(f.Request(),2);
            Require(miss.Status==SearchStatus.NoSolutionFoundWithinBounds && miss.Evaluations<=2,"exhaustion outcome");
            f=Fixture.Read(Path.Combine(directory,"unsafe-flyby.json"));
            Require(AutomaticSearch.Solve(f.Request(),1).Status!=SearchStatus.OfflineFeasible,"unsafe guess promoted");
        });
        check("KSP evidence gate requires full matching safe sequence",()=>
        {
            Fixture f=Fixture.Read(Path.Combine(directory,"reachable.json"));
            Burn burn=f.KnownBurn.Burn(); Evaluation e=Trajectory.Evaluate(f.Request(),burn);
            Require(RouteValidation.Check(f.Request(),burn,e,e.Events).Passed,"controlled evidence gate rejected");
            Require(!RouteValidation.Check(f.Request(),burn,e,e.Events.Take(4)).Passed,"incomplete accepted");
            var wrong=e.Events.Reverse(); Require(!RouteValidation.Check(f.Request(),burn,e,wrong).Passed,"wrong order accepted");
            var changed=e.Events.ToArray(); var last=changed[4];
            changed[4]=new EncounterEvent(last.Kind,last.Body,last.UT,new State(last.RelativeState.R*1.1,last.RelativeState.V),last.ParentState);
            Require(!RouteValidation.Check(f.Request(),burn,e,changed).Passed,"wrong periapsis accepted");
        });
        check("fixed native frame applies axis swap and Z-up transform exactly once",()=>
        {
            State s=new(new V3(680000,0,0),new V3(0,2200,1200));
            V3 Rotate(V3 v) => new V3(.8*v.X-.6*v.Y,.6*v.X+.8*v.Y,v.Z);
            BurnFrame frame=NativeFrame.FromFixed(s,Rotate);
            V3 normal=V3.Cross(s.R,s.V); normal=normal/normal.Length;
            Require((frame.Radial-Rotate(new V3(1,0,0))).Length<1e-12 && (frame.Normal-Rotate(normal)).Length<1e-12,"native sign or double transform");
            V3 components=new V3(150,-250,900);
            Require((frame.ToComponents(frame.ToInertial(components))-components).Length<1e-10,"native vector round trip");
        });
        check("node authorization blocks offline, stale, conflicted and running states",()=>
        {
            Require(NodeAuthorization.Allowed(true,true,false,false),"fully validated node blocked");
            Require(!NodeAuthorization.Allowed(false,true,false,false) && !NodeAuthorization.Allowed(true,false,false,false) &&
                !NodeAuthorization.Allowed(true,true,true,false) && !NodeAuthorization.Allowed(true,true,false,true),"unsafe node enabled");
        });
        check("automatic stock-scale 80 km Kerbin to Mun to inclined Minmus",()=>
        {
            Fixture f=Fixture.Read(Path.Combine(directory,"automatic","kerbin-mun-minmus.json"));
            Require(f.Seeds.Length==0 && f.Assist=="Mun" && f.Destination=="Minmus" && f.TargetAltitude==30000,"fixture requires seed or wrong route");
            var timer=System.Diagnostics.Stopwatch.StartNew();
            SearchResult result=AutomaticSearch.Solve(f.Request(),f.SearchBudget);
            Console.WriteLine("  automatic stock-scale: "+result.Status+" evaluations="+result.Evaluations+" seconds="+timer.Elapsed.TotalSeconds.ToString("F3")+" Pe="+result.Evaluation?.DestinationAltitude);
            Require(result.Status==SearchStatus.OfflineFeasible && result.Evaluation?.Events.Count==5,"stock-scale automatic route not recovered");
            Require(Math.Abs(result.Evaluation!.DestinationAltitude!.Value-30000)<=100 && !result.KspValidated,"periapsis error/authority");
        });
    }

    public static void ConstructStock(string output)
    {
        const double mu=3.5316e12;
        double[] Arr(V3 v)=>new[]{v.X,v.Y,v.Z};
        var f=new Fixture {Name="Constructed stock-scale Kerbin Mun Minmus, no input manoeuvre",Provenance="Stock-scale masses/radii and circular 80 km vessel orbit; destination phase inverse-designed for a generated safe flyby. Not a captured live game state.",
            ParentMu=mu,ParentSafeRadius=671000,ParentSoi=84159286,VesselR=new[]{680000.0,0,0},VesselV=new[]{0,Math.Sqrt(mu/680000),0},
            Bodies=new[]{new BodyData { Id="Mun",Mu=6.5138398e10,Radius=200000,Soi=2429559,R=new[]{12000000.0,0,0},V=new[]{0,Math.Sqrt(mu/12000000),0},TerrainCeiling=10000 },
                new BodyData {Id="Minmus",Mu=1.7658e9,Radius=60000,Soi=2247428,R=new[]{0,47000000.0,0},V=new[]{-Math.Sqrt(mu/47000000),0,0},TerrainCeiling=10000}},
            Assist="Mun",Destination="Minmus",Earliest=600,Latest=87000,Duration=1200000,MaxDeltaV=2000,MaxStep=300,MaxSteps=30000,
            TargetAltitude=30000,MinimumAssistAltitude=20000,ClearanceMargin=1000,AltitudeTolerance=100,SearchBudget=12000};
        var seeds=AutomaticSearch.Seeds(f.Request(),512);
        foreach(Burn seed in seeds)
        {
            Evaluation assist=Trajectory.Evaluate(f.Request(),seed,default,true);
            if(assist.Events.Count!=3) continue;
            EncounterEvent exit=assist.Events[2];
            double before=0,after=0;
            for(double dt=1000;dt<f.Duration;dt+=5000)
            {
                State current=Kepler.Propagate(exit.ParentState,mu,dt);
                if(current.R.Length>=47000000) { after=dt; break; }
                before=dt;
            }
            if(after==0) continue;
            for(int k=0;k<50;k++) { double mid=(before+after)/2; if(Kepler.Propagate(exit.ParentState,mu,mid).R.Length<47000000) before=mid; else after=mid; }
            double ut=exit.UT+(before+after)/2;
            State ship=Kepler.Propagate(exit.ParentState,mu,ut-exit.UT);
            V3 normal=new V3(0,0,1),velocity=V3.Cross(normal,ship.R); velocity=velocity/velocity.Length*Math.Sqrt(mu/47000000);
            V3 side=V3.Cross(ship.V-velocity,normal); side=side/side.Length;
            foreach(double miss in new[]{100000.0,150000,250000,400000})
            {
                V3 centre=ship.R-side*miss; centre=centre/centre.Length*47000000;
                V3 around=V3.Cross(normal,centre); around=around/around.Length;
                V3 tilted=normal*Math.Cos(Math.PI/30)+around*Math.Sin(Math.PI/30);
                V3 tangent=V3.Cross(tilted,centre); tangent=tangent/tangent.Length*Math.Sqrt(mu/47000000);
                State initial=Kepler.Propagate(new State(centre,tangent),mu,-ut);
                f.Bodies[1].R=Arr(initial.R); f.Bodies[1].V=Arr(initial.V);
                Evaluation full=Trajectory.Evaluate(f.Request(),seed);
                if(!full.DestinationAltitude.HasValue || full.DestinationAltitude<11000) continue;
                f.TargetAltitude=full.DestinationAltitude.Value;
                f.KnownBurn=new SeedData {UT=seed.UT,DeltaV=Arr(seed.DeltaV)};
                f.Seeds=Array.Empty<SeedData>();
                f.ExpectedTimes=full.Events.Select(e=>e.UT).ToArray();f.ExpectedRadii=full.Events.Select(e=>e.RelativeState.R.Length).ToArray();
                Console.WriteLine("Constructed: burn="+seed.UT+" dv="+seed.DeltaV+" pe="+f.TargetAltitude);
                File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(f,Fixture.JsonOptions)+"\n"); return;
            }
        }
        throw new Exception("No safe constructed stock-scale route from generated set");
    }
}
