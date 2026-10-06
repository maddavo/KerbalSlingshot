using System.Globalization;
using System.Text.Json;
using KerbalSlingshot.Core;
using KerbalSlingshot.Harness;

CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
if (args.Length==3 && args[0]=="--inspect-plugin")
{
    PackageInspection.Check(args[1],args[2]);
    return;
}
if (args.Length==2 && args[0]=="--construct")
{
    // Maintenance-only, never called by checks. Fixtures remain frozen during normal validation.
    Construct(args[1]);
    return;
}
string directory=args.Length==1?args[0]:"fixtures";
int passed=0,failed=0;
var report=new List<object>();
void Check(string name,Action action)
{
    try { action(); passed++; Console.WriteLine("PASS "+name); }
    catch (Exception e) { failed++; Console.WriteLine("FAIL "+name+": "+e.Message); }
}
void Require(bool condition,string message) { if (!condition) throw new Exception(message); }

Check("Kepler analytic circular orbit + round trip",()=>
{
    double mu=1e10,rad=1e6,period=2*Math.PI*Math.Sqrt(rad*rad*rad/mu);
    State s=new(new(rad,0,0),new(0,100,0));
    State q=Kepler.Propagate(s,mu,period/4);
    Require((q.R-new V3(0,rad,0)).Length<1e-6,"quarter orbit position");
    State back=Kepler.Propagate(q,mu,-period/4);
    Require((back.R-s.R).Length<1e-6 && (back.V-s.V).Length<1e-9,"round trip");
});
Check("elliptic, parabolic, hyperbolic propagation vs independent RK4",()=>
{
    foreach (double speed in new[]{70.0,Math.Sqrt(20000),180.0})
    {
        State s=new(new(1e6,0,0),new(0,.8*speed,.6*speed));
        State a=Kepler.Propagate(s,1e10,500),b=Rk4(s,1e10,500,.25);
        Require((a.R-b.R).Length<1e-5 && (a.V-b.V).Length<1e-8,"RK4 mismatch");
        Require(Math.Abs(Numeric.Energy(a,1e10)-Numeric.Energy(s,1e10))<1e-8,"energy drift");
        Require((V3.Cross(a.R,a.V)-V3.Cross(s.R,s.V)).Length<1e-5,"angular momentum drift");
    }
});
Check("invalid conic fails explicitly",()=>
{
    try { Kepler.Propagate(new State(new(0,0,0),new(0,0,0)),1,1); throw new Exception("accepted singular orbit"); }
    catch (ArgumentException) { }
});

string[] files=Directory.GetFiles(directory,"*.json").OrderBy(x=>x,StringComparer.Ordinal).ToArray();
Require(files.Length>=4,"Missing deterministic fixtures");
foreach (string file in files)
{
    Fixture f=Fixture.Read(file);
    Check(f.Name+" known-burn constraints",()=>
    {
        Evaluation e=Trajectory.Evaluate(f.Request(),f.KnownBurn.Burn());
        Require(e.Status.ToString()==f.ExpectedEvaluationStatus,$"{e.Status}: {e.Reason}");
        Require(e.Reason.Contains(f.ExpectedReasonContains,StringComparison.Ordinal),e.Reason);
        Require(!e.KspValidated && !e.CanCreateNode,"offline result enabled KSP authority");
        if (e.Status==EvaluationStatus.OfflineFeasible)
        {
            Require(e.Events.Count==5,"event count");
            Require(Math.Abs(e.DestinationAltitude!.Value-f.TargetAltitude)<=f.AltitudeTolerance,"periapsis tolerance");
            Require(f.ExpectedTimes.Length==5 && f.ExpectedRadii.Length==5,"missing frozen gold values");
            for (int i=0;i<5;i++)
            {
                Require(Math.Abs(e.Events[i].UT-f.ExpectedTimes[i])<1e-5,"gold event time "+i);
                Require(Math.Abs(e.Events[i].RelativeState.R.Length-f.ExpectedRadii[i])<1e-4,"gold radius "+i);
            }
            CheckIndependentSegments(f,e,f.KnownBurn.Burn(),Require);
        }
    });
    Check(f.Name+" bounded solver + repeatability",()=>
    {
        SearchResult Run() => Search.Solve(f.Request(),f.Seeds.Select(s=>s.Burn()).ToArray(),f.SearchBudget,f.TimeStep,f.VelocityStep);
        var timer=System.Diagnostics.Stopwatch.StartNew();
        SearchResult a=Run(); timer.Stop();
        SearchResult b=Run();
        Require(a.Status.ToString()==f.ExpectedSearchStatus,$"{a.Status}: {a.Message}");
        Require(a.Evaluations<=f.SearchBudget,"search budget overrun");
        Require(a.Status==b.Status && a.Evaluations==b.Evaluations && a.Candidate?.UT==b.Candidate?.UT &&
            a.Candidate?.DeltaV.ToString()==b.Candidate?.DeltaV.ToString(),"not deterministic");
        Require(!a.KspValidated,"KSP validation asserted");
        if (a.Status==SearchStatus.OfflineFeasible)
        {
            Require(a.Candidate.HasValue && a.Evaluation?.Status==EvaluationStatus.OfflineFeasible,"unvalidated seed accepted");
            Require(a.Evaluation!.Events.Count==5,"missing complete candidate sequence");
            Evaluation fine=Trajectory.Evaluate(f.Request(f.MaxStep/2),a.Candidate!.Value);
            Require(fine.Status==EvaluationStatus.OfflineFeasible,"finer scan invalidated candidate");
            Require(Math.Abs(fine.DestinationAltitude!.Value-a.Evaluation.DestinationAltitude!.Value)<1e-4,"resolution dependence");
            CheckIndependentSegments(f,a.Evaluation,a.Candidate.Value,Require);
        }
        Console.WriteLine($"  {a.Status}; evaluations={a.Evaluations}; altitude={a.Evaluation?.DestinationAltitude:G12}; dv={a.Candidate?.DeltaV}");
        report.Add(new { f.Name,Status=a.Status.ToString(),a.Evaluations,ElapsedMilliseconds=timer.Elapsed.TotalMilliseconds,
            Altitude=a.Evaluation?.DestinationAltitude,DeltaV=a.Candidate?.DeltaV.ToString(),Reasons=a.Rejections,
            Events=a.Evaluation?.Events.Select(e=>new { e.Kind,e.Body,e.UT,Radius=e.RelativeState.R.Length,
                Speed=e.RelativeState.V.Length }) });
    });
}
Fixture reach=Fixture.Read(Path.Combine(directory,"reachable.json"));
Check("reachable requires refinement from approximate seed",()=>
{
    Require(reach.Seeds.All(s=>Trajectory.Evaluate(reach.Request(),s.Burn()).Status!=EvaluationStatus.OfflineFeasible),"fixture seed already feasible");
    SearchResult s=Search.Solve(reach.Request(),reach.Seeds.Select(x=>x.Burn()).ToArray(),reach.SearchBudget,reach.TimeStep,reach.VelocityStep);
    Require(s.Status==SearchStatus.OfflineFeasible,"did not refine");
});
Check("cancellation, evaluation budget, and scan budget",()=>
{
    using var cts=new CancellationTokenSource(); cts.Cancel();
    Require(Search.Solve(reach.Request(),reach.Seeds.Select(x=>x.Burn()).ToArray(),100,1,.1,cts.Token).Status==SearchStatus.Cancelled,"cancel ignored");
    SearchResult limited=Search.Solve(reach.Request(),reach.Seeds.Select(x=>x.Burn()).ToArray(),1,1,.1);
    Require(limited.Status==SearchStatus.NoSolutionFoundWithinBounds && limited.Evaluations==1,"evaluation limit");
    Fixture small=reach.Copy(); small.MaxSteps=1;
    Require(Trajectory.Evaluate(small.Request(),small.KnownBurn.Burn()).Status==EvaluationStatus.PropagationBudgetExhausted,"scan budget");
});
Check("finite and identity validation",()=>
{
    foreach (Action<Fixture> mutate in new Action<Fixture>[] { f=>f.TargetAltitude=double.NaN, f=>f.ParentMu=double.PositiveInfinity,
        f=>f.Destination=f.Assist, f=>f.Bodies[0].Mu=0, f=>f.Latest=-1, f=>f.MaxStep=0,
        f=>f.TargetAltitude=f.Bodies[1].Soi, f=>f.Bodies[1].TerrainCeiling=f.TargetAltitude+1 })
    {
        Fixture bad=reach.Copy(); mutate(bad);
        Require(Trajectory.Evaluate(bad.Request(),bad.KnownBurn.Burn()).Status==EvaluationStatus.InvalidInput,"bad input accepted");
    }
});
Check("complete event ordering, atmosphere, destination tolerance, journey limit",()=>
{
    Fixture bad=reach.Copy(); (bad.Assist,bad.Destination)=(bad.Destination,bad.Assist);
    Require(Trajectory.Evaluate(bad.Request(),bad.KnownBurn.Burn()).Reason.Contains("order"),"wrong-order encounter accepted");
    bad=reach.Copy(); bad.Bodies[0].AtmosphereHeight=1500;
    Require(Trajectory.Evaluate(bad.Request(),bad.KnownBurn.Burn()).Reason.Contains("unsafe"),"atmosphere flyby accepted");
    bad=reach.Copy(); bad.TargetAltitude+=100;
    Require(Trajectory.Evaluate(bad.Request(),bad.KnownBurn.Burn()).Reason.Contains("tolerance"),"near miss accepted");
    bad=reach.Copy(); bad.Duration=100;
    Require(Trajectory.Evaluate(bad.Request(),bad.KnownBurn.Burn()).Status==EvaluationStatus.Rejected,"short horizon accepted");
});
Check("unexpected grazing SOI entry between scan endpoints",()=>
{
    Fixture f=reach.Copy();
    double closestUT=20.123;
    Burn burn=f.KnownBurn.Burn();
    State ship=Kepler.Propagate(new State(BodyData.Vec(f.VesselR),BodyData.Vec(f.VesselV)+burn.DeltaV),f.ParentMu,closestUT);
    V3 normal=new(0,-.6,.8);
    V3 velocity=V3.Cross(normal,ship.R); velocity=velocity/velocity.Length*Math.Sqrt(f.ParentMu/ship.R.Length);
    V3 offset=V3.Cross(ship.V-velocity,normal); offset=offset/offset.Length*999.999;
    State body=Kepler.Propagate(new State(ship.R-offset,velocity),f.ParentMu,-closestUT);
    f.Bodies=[..f.Bodies,new BodyData { Id="unexpected",Mu=1,Radius=10,Soi=1000,
        R=[body.R.X,body.R.Y,body.R.Z],V=[body.V.X,body.V.Y,body.V.Z] }];
    Request request=f.Request();
    double scan=0,end=0;
    while(scan<closestUT)
    {
        State ss=Kepler.Propagate(new State(BodyData.Vec(f.VesselR),BodyData.Vec(f.VesselV)+burn.DeltaV),f.ParentMu,scan);
        double h=f.MaxStep;
        foreach(Body b in request.Bodies)
        {
            State bs=Trajectory.BodyAt(request,b,scan);
            h=Math.Min(h,b.Soi/(16*((ss.V-bs.V).Length+Math.Sqrt(b.Mu/b.Soi))));
        }
        end=scan+h;
        if(end>=closestUT) break;
        scan=end;
    }
    double Boundary(double ut)
    {
        State ss=Kepler.Propagate(new State(BodyData.Vec(f.VesselR),BodyData.Vec(f.VesselV)+burn.DeltaV),f.ParentMu,ut);
        return (ss.R-Trajectory.BodyAt(request,request.Bodies[2],ut).R).Length-1000;
    }
    Require(Boundary(scan)>0 && Boundary(end)>0 && Boundary(closestUT)<0,"case is not hidden between outside endpoints");
    Evaluation e=Trajectory.Evaluate(request,burn);
    Require(e.Status==EvaluationStatus.Rejected && e.Reason.Contains("unexpected"),$"grazing body missed: {e.Status} {e.Reason}");
});
PlanningChecks.Run(directory,Check);
PresentationChecks.Run(directory,Check);
Directory.CreateDirectory("artifacts");
File.WriteAllText("artifacts/offline-results.json",JsonSerializer.Serialize(new { Passed=passed,Failed=failed,Fixtures=report },Fixture.JsonOptions));
Console.WriteLine($"Checks: {passed} passed, {failed} failed. KSP checks: not run.");
Environment.ExitCode=failed==0?0:1;

static State Rk4(State state,double mu,double dt,double maxStep)
{
    int n=(int)Math.Ceiling(Math.Abs(dt)/maxStep);
    if (n==0) return state;
    double h=dt/n;
    V3 Acc(V3 r) => r*(-mu/Math.Pow(r.Length,3));
    for (int i=0;i<n;i++)
    {
        V3 r=state.R,v=state.V,a1=Acc(r),v2=v+a1*(h/2),r2=r+v*(h/2),a2=Acc(r2);
        V3 v3=v+a2*(h/2),r3=r+v2*(h/2),a3=Acc(r3),v4=v+a3*h,r4=r+v3*h,a4=Acc(r4);
        state=new State(r+(v+v2*2+v3*2+v4)*(h/6),v+(a1+a2*2+a3*2+a4)*(h/6));
    }
    return state;
}
static void CheckIndependentSegments(Fixture f,Evaluation e,Burn burn,Action<bool,string> require)
{
    Request r=f.Request();
    State start=Kepler.Propagate(r.Vessel,r.ParentMu,burn.UT-r.Epoch);
    start=new(start.R,start.V+burn.DeltaV);
    State entry=Rk4(start,r.ParentMu,e.Events[0].UT-burn.UT,.05);
    require((entry.R-e.Events[0].ParentState.R).Length<1e-4,"independent departure segment");
    for(int i=1;i<e.Events.Count;i++)
    {
        EncounterEvent previous=e.Events[i-1],current=e.Events[i];
        bool parent=previous.Kind=="exit";
        double mu=parent?r.ParentMu:r.Bodies.Single(b=>b.Id==previous.Body).Mu;
        State initial=parent?previous.ParentState:previous.RelativeState;
        State expected=parent?current.ParentState:current.RelativeState;
        State independent=Rk4(initial,mu,current.UT-previous.UT,.01);
        require((independent.R-expected.R).Length<1e-3 && (independent.V-expected.V).Length<1e-5,"independent patch segment "+i);
    }
    foreach (EncounterEvent ev in e.Events)
    {
        Body body=r.Bodies.Single(b=>b.Id==ev.Body);
        State independent=Rk4(body.EpochState,r.ParentMu,ev.UT-r.Epoch,.05);
        require((independent.R+ev.RelativeState.R-ev.ParentState.R).Length<1e-4,"SOI position continuity");
        require((independent.V+ev.RelativeState.V-ev.ParentState.V).Length<1e-7,"SOI velocity continuity");
        if(ev.Kind=="periapsis")
        {
            require(Math.Abs(V3.Dot(ev.RelativeState.R,ev.RelativeState.V))<.01,"periapsis radial motion");
            require(Math.Abs(Numeric.Periapsis(ev.RelativeState,body.Mu)-ev.RelativeState.R.Length)<1e-5,"analytic periapsis radius");
        }
        else require(Math.Abs(ev.RelativeState.R.Length-body.Soi)<1e-4,"SOI event boundary");
    }
    require(Numeric.Energy(e.Events[0].RelativeState,r.Bodies[0].Mu)>0 &&
        Numeric.Energy(e.Events[2].RelativeState,r.Bodies[0].Mu)>0,"unbound entry/exit");
}

static void Construct(string directory)
{
    static double[] Arr(V3 v) => [v.X,v.Y,v.Z];
    // Tilt the complete constructed system around X so the golden route is genuinely 3D.
    static V3 Tilt(V3 v) => new(v.X,.8*v.Y-.6*v.Z,.6*v.Y+.8*v.Z);
    State approach=Kepler.Propagate(new State(new(2000,0,0),new(0,Math.Sqrt(20000),0)),1e7,-130);
    V3 ar=new(1e6,0,0),av=new(0,100,0),dv=new(1,2,.3);
    var f=new Fixture { Name="reachable tilted sibling-body route",Provenance="Synthetic inverse-designed assist hyperbola; destination placed on outgoing conic. Frozen after construction; independent RK4 segment checks. Not a KSP save or stock-system validation.",
        ParentMu=1e10,ParentSafeRadius=1000,ParentSoi=1e8,VesselR=Arr(Tilt(ar+approach.R)),VesselV=Arr(Tilt(av+approach.V-dv)),
        Bodies=[new BodyData { Id="assist",Mu=1e7,Radius=1000,Soi=10000,R=Arr(Tilt(ar)),V=Arr(Tilt(av)),TerrainCeiling=50 },
            new BodyData { Id="destination",Mu=1e6,Radius=500,Soi=5000,R=Arr(Tilt(new(0,1.3e6,0))),V=Arr(Tilt(new(-Math.Sqrt(1e10/1.3e6),0,0))),TerrainCeiling=20 }],
        TargetAltitude=1000,MinimumAssistAltitude=300,ClearanceMargin=50,AltitudeTolerance=.1,Earliest=0,Latest=2,Duration=1000,MaxDeltaV=5,
        KnownBurn=new SeedData { UT=0,DeltaV=Arr(Tilt(dv)) },Seeds=[new SeedData { UT=0,DeltaV=Arr(Tilt(dv+new V3(.05,-.03,.02))) }],SearchBudget=400,VelocityStep=.1 };
    Evaluation initial=Trajectory.Evaluate(f.Request(),f.KnownBurn.Burn());
    if(initial.Events.Count<3) throw new Exception("Constructed assist failed: "+initial.Reason);
    EncounterEvent exit=initial.Events[2];
    double arrival=exit.UT+300;
    State vessel=Kepler.Propagate(exit.ParentState,f.ParentMu,300);
    V3 circular=V3.Cross(Tilt(new(0,0,1)),vessel.R);
    circular=circular/circular.Length*Math.Sqrt(f.ParentMu/vessel.R.Length);
    V3 rel=vessel.V-circular;
    V3 offset=V3.Cross(rel,Tilt(new(0,0,1))); offset=offset/offset.Length*2000;
    State dest=Kepler.Propagate(new State(vessel.R-offset,circular),f.ParentMu,-arrival);
    f.Bodies[1].R=Arr(dest.R); f.Bodies[1].V=Arr(dest.V);
    Evaluation route=Trajectory.Evaluate(f.Request(),f.KnownBurn.Burn());
    Console.WriteLine(route.Status+": "+route.Reason);
    foreach(var ev in route.Events) Console.WriteLine($"{ev.Kind} {ev.Body} {ev.UT:G17} radius={ev.RelativeState.R.Length:G17}");
    if(!route.DestinationAltitude.HasValue) throw new Exception("Constructed destination failed");
    f.TargetAltitude=route.DestinationAltitude.Value;
    f.ExpectedTimes=route.Events.Select(x=>x.UT).ToArray();
    f.ExpectedRadii=route.Events.Select(x=>x.RelativeState.R.Length).ToArray();
    Directory.CreateDirectory(directory);
    void Save(string name,Fixture item) => File.WriteAllText(Path.Combine(directory,name+".json"),JsonSerializer.Serialize(item,Fixture.JsonOptions).Replace("\r\n","\n")+"\n");
    Save("reachable",f);
    Fixture inclined=f.Copy(); inclined.Name="reachable mutually inclined destination";
    inclined.Bodies[1].V=Arr(BodyData.Vec(inclined.Bodies[1].V)+Tilt(new(0,0,.7)));
    Evaluation inclinedRoute=Trajectory.Evaluate(inclined.Request(),inclined.KnownBurn.Burn());
    if(!inclinedRoute.DestinationAltitude.HasValue) throw new Exception("Inclined construction failed: "+inclinedRoute.Reason);
    inclined.TargetAltitude=inclinedRoute.DestinationAltitude.Value;
    inclined.ExpectedTimes=inclinedRoute.Events.Select(x=>x.UT).ToArray();
    inclined.ExpectedRadii=inclinedRoute.Events.Select(x=>x.RelativeState.R.Length).ToArray();
    Save("reachable-inclined",inclined);
    Fixture miss=f.Copy(); miss.Name="constrained delta-v and journey miss"; miss.MaxDeltaV=.1; miss.Duration=300; miss.Seeds=[new SeedData { UT=0,DeltaV=[0,0,0] }];
    miss.KnownBurn=miss.Seeds[0]; miss.SearchBudget=100; miss.ExpectedEvaluationStatus="Rejected"; miss.ExpectedSearchStatus="NoSolutionFoundWithinBounds"; miss.ExpectedReasonContains="journey bound"; miss.ExpectedTimes=[];miss.ExpectedRadii=[];
    Save("constrained-miss",miss);
    Fixture unsafeCase=f.Copy(); unsafeCase.Name="unsafe flyby clearance"; unsafeCase.MinimumAssistAltitude=1500;
    unsafeCase.Seeds=[unsafeCase.KnownBurn]; unsafeCase.SearchBudget=1; unsafeCase.ExpectedEvaluationStatus="Rejected";unsafeCase.ExpectedSearchStatus="NoSolutionFoundWithinBounds";
    unsafeCase.ExpectedReasonContains="unsafe assist";unsafeCase.ExpectedTimes=[];unsafeCase.ExpectedRadii=[];Save("unsafe-flyby",unsafeCase);
    Fixture invalid=f.Copy();invalid.Name="invalid identical targets";invalid.Destination=invalid.Assist;invalid.ExpectedEvaluationStatus="InvalidInput";
    invalid.ExpectedSearchStatus="InvalidInput";invalid.ExpectedReasonContains="identities";invalid.ExpectedTimes=[];invalid.ExpectedRadii=[];Save("invalid-input",invalid);
}
