using System;
using System.Collections.Generic;
using System.Linq;

namespace KerbalSlingshot.Core
{
    public sealed class Body
    {
        public string Id { get; }
        public double Mu { get; }
        public double Radius { get; }
        public double Soi { get; }
        public double TerrainCeiling { get; }
        public double AtmosphereHeight { get; }
        public State EpochState { get; }
        public Body(string id, double mu, double radius, double soi, State state,
            double terrainCeiling = 0, double atmosphereHeight = 0)
        { Id=id; Mu=mu; Radius=radius; Soi=soi; EpochState=state; TerrainCeiling=terrainCeiling; AtmosphereHeight=atmosphereHeight; }
        public double SafeRadius(double margin) => Radius + Math.Max(TerrainCeiling, AtmosphereHeight) + margin;
    }

    // A deliberately bounded sibling-body route snapshot. Bodies and vessel share Epoch and parent frame.
    public sealed class Request
    {
        public double Epoch { get; }
        public double ParentMu { get; }
        public double ParentSafeRadius { get; }
        public double ParentSoi { get; }
        public State Vessel { get; }
        public IReadOnlyList<Body> Bodies { get; }
        public string Assist { get; }
        public string Destination { get; }
        public double TargetAltitude { get; }
        public double MinimumAssistAltitude { get; }
        public double ClearanceMargin { get; }
        public double AltitudeTolerance { get; }
        public double Earliest { get; }
        public double Latest { get; }
        public double Duration { get; }
        public double MaxDeltaV { get; }
        public double MaxStep { get; }
        public int MaxSteps { get; }
        public Request(double epoch, double parentMu, double parentSafeRadius, double parentSoi, State vessel,
            IEnumerable<Body> bodies, string assist, string destination, double targetAltitude,
            double minimumAssistAltitude, double clearanceMargin, double altitudeTolerance,
            double earliest, double latest, double duration, double maxDeltaV, double maxStep=10, int maxSteps=20000)
        {
            Epoch=epoch; ParentMu=parentMu; ParentSafeRadius=parentSafeRadius; ParentSoi=parentSoi; Vessel=vessel;
            Bodies=Array.AsReadOnly(bodies.ToArray()); Assist=assist; Destination=destination;
            TargetAltitude=targetAltitude; MinimumAssistAltitude=minimumAssistAltitude; ClearanceMargin=clearanceMargin;
            AltitudeTolerance=altitudeTolerance; Earliest=earliest; Latest=latest; Duration=duration;
            MaxDeltaV=maxDeltaV; MaxStep=maxStep; MaxSteps=maxSteps;
        }
        public string? InvalidReason()
        {
            double[] values={Epoch,ParentMu,ParentSafeRadius,ParentSoi,TargetAltitude,MinimumAssistAltitude,
                ClearanceMargin,AltitudeTolerance,Earliest,Latest,Duration,MaxDeltaV,MaxStep};
            if (values.Any(x=>!Numeric.Finite(x)) || !Vessel.Finite) return "nonfinite or singular input";
            if (ParentMu<=0 || ParentSafeRadius<0 || ParentSoi<=ParentSafeRadius || Vessel.R.Length<=ParentSafeRadius ||
                Vessel.R.Length>=ParentSoi || Earliest<Epoch || Latest<Earliest || Duration<=0 ||
                MaxDeltaV<0 || MaxStep<=0 || MaxSteps<=0 || TargetAltitude<0 || MinimumAssistAltitude<0 ||
                ClearanceMargin<0 || AltitudeTolerance<=0 || !Numeric.Finite(Latest+Duration)) return "invalid bounds";
            if (Assist==Destination || Bodies.Select(b=>b.Id).Distinct().Count()!=Bodies.Count ||
                !Bodies.Any(b=>b.Id==Assist) || !Bodies.Any(b=>b.Id==Destination)) return "invalid target identities";
            foreach (Body b in Bodies)
            {
                double[] data={b.Mu,b.Radius,b.Soi,b.TerrainCeiling,b.AtmosphereHeight};
                if (string.IsNullOrWhiteSpace(b.Id) || data.Any(x=>!Numeric.Finite(x)) || b.Mu<=0 || b.Radius<=0 ||
                    b.Soi<=b.Radius || b.TerrainCeiling<0 || b.AtmosphereHeight<0 || !b.EpochState.Finite ||
                    b.SafeRadius(ClearanceMargin)>=b.Soi || b.EpochState.R.Length-b.Soi<=ParentSafeRadius ||
                    b.EpochState.R.Length+b.Soi>=ParentSoi) return "invalid body snapshot";
                if ((Vessel.R-b.EpochState.R).Length<=b.Soi) return "vessel must start outside child SOIs";
                double energy=Numeric.Energy(b.EpochState,ParentMu);
                double pe=Numeric.Periapsis(b.EpochState,ParentMu);
                double ap=energy<0?-ParentMu/energy-pe:double.PositiveInfinity;
                if (energy>=0 || pe-b.Soi<=ParentSafeRadius || ap+b.Soi>=ParentSoi)
                    return "body ephemeris leaves supported parent interval";
            }
            Body a=Bodies.Single(b=>b.Id==Assist), d=Bodies.Single(b=>b.Id==Destination);
            if (a.Radius+MinimumAssistAltitude>=a.Soi || d.Radius+TargetAltitude<d.SafeRadius(ClearanceMargin) ||
                d.Radius+TargetAltitude>=d.Soi) return "requested periapsis outside safe SOI interval";
            return null;
        }
    }

    public readonly struct Burn
    {
        public readonly double UT;
        public readonly V3 DeltaV;
        public Burn(double ut, V3 deltaV) { UT=ut; DeltaV=deltaV; }
    }
    public enum EvaluationStatus { OfflineFeasible, Rejected, InvalidInput, Cancelled, NumericalFailure, PropagationBudgetExhausted }
    public sealed class EncounterEvent
    {
        public string Kind { get; }
        public string Body { get; }
        public double UT { get; }
        public State RelativeState { get; }
        public State ParentState { get; }
        public EncounterEvent(string kind, string body, double ut, State relative, State parent)
        { Kind=kind; Body=body; UT=ut; RelativeState=relative; ParentState=parent; }
    }
    public sealed class Evaluation
    {
        public EvaluationStatus Status { get; }
        public string Reason { get; }
        public IReadOnlyList<EncounterEvent> Events { get; }
        public double Score { get; }
        public double? DestinationAltitude { get; }
        public int Steps { get; }
        public bool KspValidated => false;
        public bool CanCreateNode => false;
        public Evaluation(EvaluationStatus status, string reason, IEnumerable<EncounterEvent> events, double score,
            int steps, double? altitude=null)
        { Status=status; Reason=reason; Events=Array.AsReadOnly(events.ToArray()); Score=score; Steps=steps; DestinationAltitude=altitude; }
    }
}
