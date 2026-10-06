using System.Text.Json;
using KerbalSlingshot.Core;

namespace KerbalSlingshot.Harness;

public sealed class BodyData
{
    public string Id { get; set; } = "";
    public double Mu { get; set; }
    public double Radius { get; set; }
    public double Soi { get; set; }
    public double TerrainCeiling { get; set; }
    public double AtmosphereHeight { get; set; }
    public double[] R { get; set; } = [];
    public double[] V { get; set; } = [];
    public Body Body() => new(Id,Mu,Radius,Soi,new State(Vec(R),Vec(V)),TerrainCeiling,AtmosphereHeight);
    public static V3 Vec(double[] v) => v.Length==3?new(v[0],v[1],v[2]):throw new ArgumentException("Vector requires 3 components");
}
public sealed class SeedData
{
    public double UT { get; set; }
    public double[] DeltaV { get; set; } = [];
    public Burn Burn() => new(UT,BodyData.Vec(DeltaV));
}
public sealed class Fixture
{
    public string Name { get; set; } = "";
    public string Provenance { get; set; } = "";
    public double Epoch { get; set; }
    public double ParentMu { get; set; }
    public double ParentSafeRadius { get; set; }
    public double ParentSoi { get; set; }
    public double[] VesselR { get; set; } = [];
    public double[] VesselV { get; set; } = [];
    public BodyData[] Bodies { get; set; } = [];
    public string Assist { get; set; } = "assist";
    public string Destination { get; set; } = "destination";
    public double TargetAltitude { get; set; }
    public double MinimumAssistAltitude { get; set; }
    public double ClearanceMargin { get; set; }
    public double AltitudeTolerance { get; set; }
    public double Earliest { get; set; }
    public double Latest { get; set; }
    public double Duration { get; set; }
    public double MaxDeltaV { get; set; }
    public double MaxStep { get; set; } = 10;
    public int MaxSteps { get; set; } = 20000;
    public int SearchBudget { get; set; } = 250;
    public double TimeStep { get; set; } = 1;
    public double VelocityStep { get; set; } = .2;
    public SeedData[] Seeds { get; set; } = [];
    public SeedData KnownBurn { get; set; } = new();
    public string ExpectedSearchStatus { get; set; } = "OfflineFeasible";
    public string ExpectedEvaluationStatus { get; set; } = "OfflineFeasible";
    public string ExpectedReasonContains { get; set; } = "";
    public double[] ExpectedTimes { get; set; } = [];
    public double[] ExpectedRadii { get; set; } = [];
    public Request Request(double? maxStep=null) => new(Epoch,ParentMu,ParentSafeRadius,ParentSoi,
        new State(BodyData.Vec(VesselR),BodyData.Vec(VesselV)),Bodies.Select(b=>b.Body()),Assist,Destination,
        TargetAltitude,MinimumAssistAltitude,ClearanceMargin,AltitudeTolerance,Earliest,Latest,Duration,
        MaxDeltaV,maxStep??MaxStep,MaxSteps);
    public static readonly JsonSerializerOptions JsonOptions=new() { WriteIndented=true };
    public static Fixture Read(string file) => JsonSerializer.Deserialize<Fixture>(File.ReadAllText(file),JsonOptions)
        ?? throw new ArgumentException("Empty fixture");
    public Fixture Copy() => JsonSerializer.Deserialize<Fixture>(JsonSerializer.Serialize(this,JsonOptions),JsonOptions)!;
}
