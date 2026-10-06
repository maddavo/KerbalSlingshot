using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace KerbalSlingshot.Core
{
    // Basis columns are supplied by the main-thread adapter, not inferred from Unity on a worker.
    public sealed class BurnFrame
    {
        public V3 Radial { get; }
        public V3 Normal { get; }
        public V3 Prograde { get; }
        public BurnFrame(V3 radial,V3 normal,V3 prograde)
        {
            V3[] axes={radial,normal,prograde};
            foreach (V3 axis in axes)
                if (!axis.Finite || Math.Abs(axis.Length-1)>1e-6) throw new ArgumentException("Invalid burn-frame axis");
            if (Math.Abs(V3.Dot(radial,normal))>1e-6 || Math.Abs(V3.Dot(radial,prograde))>1e-6 ||
                Math.Abs(V3.Dot(normal,prograde))>1e-6) throw new ArgumentException("Burn frame is not orthogonal");
            Radial=radial; Normal=normal; Prograde=prograde;
        }
        public V3 ToInertial(V3 components) => Radial*components.X+Normal*components.Y+Prograde*components.Z;
        public V3 ToComponents(V3 inertial) => new V3(V3.Dot(inertial,Radial),V3.Dot(inertial,Normal),V3.Dot(inertial,Prograde));
    }

    public sealed class PlannerSettings
    {
        public double SeedUT { get; }
        public V3 SeedComponents { get; }
        public double TargetAltitude { get; }
        public double AssistAltitude { get; }
        public double TerrainCeiling { get; }
        public double Margin { get; }
        public double Tolerance { get; }
        public double Window { get; }
        public double Duration { get; }
        public double MaxDeltaV { get; }
        public double MaxStep { get; }
        public int MaxSteps { get; }
        public int Budget { get; }
        public double WallSeconds { get; }
        public double TimeStep { get; }
        public double VelocityStep { get; }
        private PlannerSettings(IReadOnlyDictionary<string,string> fields)
        {
            double Read(string key)
            {
                if (!fields.TryGetValue(key,out string? value) || !double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out double n) || !Numeric.Finite(n))
                    throw new ArgumentException("Invalid number: "+key+" (use a decimal point, without commas)");
                return n;
            }
            double Positive(string key,bool zero=false)
            {
                double n=Read(key);
                if (zero?n<0:n<=0) throw new ArgumentException("Invalid bound: "+key);
                return n;
            }
            int Integer(string key,int maximum)
            {
                double n=Positive(key);
                if (n!=Math.Floor(n) || n>maximum) throw new ArgumentException(key+" must be an integer from 1 to "+maximum);
                return (int)n;
            }
            SeedUT=Positive("Departure UT",true);
            SeedComponents=new V3(Read("Radial m/s"),Read("Normal m/s"),Read("Prograde m/s"));
            TargetAltitude=Positive("Intercept Pe km",true)*1000;
            AssistAltitude=Positive("Assist min km",true)*1000;
            TerrainCeiling=Positive("Terrain ceiling km",true)*1000;
            Margin=Positive("Clearance km",true)*1000;
            Tolerance=Positive("Pe tolerance m");
            Window=Positive("Departure +/- s",true);
            Duration=Positive("Journey limit s");
            MaxDeltaV=Positive("Delta-v limit m/s",true);
            MaxStep=Positive("Scan step s");
            MaxSteps=Integer("Scan steps",200000);
            Budget=Integer("Evaluations",2000);
            WallSeconds=Positive("Wall budget s");
            TimeStep=Positive("Time step s");
            VelocityStep=Positive("DV step m/s");
            if (WallSeconds>120 || MaxStep>10000 || Duration>1e9 || MaxDeltaV>1e5 ||
                !Numeric.Finite(TargetAltitude+AssistAltitude+TerrainCeiling+Margin+SeedUT+Window+Duration))
                throw new ArgumentException("Prototype bounds exceeded");
        }
        public static PlannerSettings Parse(IReadOnlyDictionary<string,string> fields) => new PlannerSettings(fields);
        public static Dictionary<string,string> Defaults() => new Dictionary<string,string>
        {
            ["Departure UT"]="0", ["Radial m/s"]="0", ["Normal m/s"]="0", ["Prograde m/s"]="0",
            ["Intercept Pe km"]="30", ["Assist min km"]="20", ["Terrain ceiling km"]="10",
            ["Clearance km"]="1", ["Pe tolerance m"]="100", ["Departure +/- s"]="60",
            ["Journey limit s"]="200000", ["Delta-v limit m/s"]="2000", ["Scan step s"]="300",
            ["Scan steps"]="30000", ["Evaluations"]="200", ["Wall budget s"]="30",
            ["Time step s"]="10", ["DV step m/s"]="1"
        };
    }

    public sealed class RevisionGate
    {
        private int revision;
        public int Invalidate() => Interlocked.Increment(ref revision);
        public bool IsCurrent(int captured) => Volatile.Read(ref revision)==captured;
    }

    public static class SnapshotFreshness
    {
        public static bool Matches(State snapshot,double epoch,State live,double now,double mu)
        {
            if (now<epoch || !live.Finite) return false;
            try
            {
                State expected=Kepler.Propagate(snapshot,mu,now-epoch);
                return (expected.R-live.R).Length<=Math.Max(50,expected.R.Length*1e-9) &&
                    (expected.V-live.V).Length<=.05;
            }
            catch (ArithmeticException) { return false; }
            catch (ArgumentException) { return false; }
        }
    }

    public static class PlannerCompletion
    {
        // Apply only to a still-current job whose wall timer expired. User cancellation abandons the revision.
        public static SearchResult FinishWallBounded(SearchResult result)
        {
            if (result.Status!=SearchStatus.Cancelled || !result.Candidate.HasValue ||
                result.Evaluation?.Status!=EvaluationStatus.OfflineFeasible) return result;
            return new SearchResult(SearchStatus.OfflineFeasible,"wall budget exhausted; retained complete offline candidate",
                result.Candidate,result.Evaluation,result.Evaluations,result.Rejections.ToDictionary(p=>p.Key,p=>p.Value),
                result.DiagnosticBurn,result.DiagnosticEvaluation);
        }
    }
}
