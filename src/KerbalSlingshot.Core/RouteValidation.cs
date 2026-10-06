using System;
using System.Collections.Generic;
using System.Linq;

namespace KerbalSlingshot.Core
{
    public sealed class ValidationResult
    {
        public bool Passed { get; }
        public string Reason { get; }
        public IReadOnlyList<EncounterEvent> Events { get; }
        public ValidationResult(bool passed,string reason,IEnumerable<EncounterEvent>? events=null)
        { Passed=passed; Reason=reason; Events=Array.AsReadOnly((events??Array.Empty<EncounterEvent>()).ToArray()); }
    }
    public static class RouteValidation
    {
        // Independent event evidence gate used for temporary KSP patches and the inserted live plan.
        public static ValidationResult Check(Request r,Burn burn,Evaluation numerical,IEnumerable<EncounterEvent> predicted,double timeTolerance=5)
        {
            var events=predicted.ToArray();
            string[] expected={"entry:"+r.Assist,"periapsis:"+r.Assist,"exit:"+r.Assist,"entry:"+r.Destination,"periapsis:"+r.Destination};
            ValidationResult Fail(string message) => new ValidationResult(false,message,events);
            if(numerical.Status!=EvaluationStatus.OfflineFeasible || !events.Select(e=>e.Kind+":"+e.Body).SequenceEqual(expected) || numerical.Events.Count!=5)
                return Fail("Incomplete or wrong KSP event/body sequence");
            if(burn.UT<r.Earliest || burn.UT>r.Latest || burn.DeltaV.Length>r.MaxDeltaV || !burn.DeltaV.Finite)
                return Fail("Departure outside validated bounds");
            for(int i=0;i<5;i++)
            {
                EncounterEvent e=events[i]; Body b=r.Bodies.Single(x=>x.Id==e.Body);
                if(!e.RelativeState.Finite || !Numeric.Finite(e.UT) || e.UT<=burn.UT || e.UT>burn.UT+r.Duration ||
                    (i>0 && e.UT<=events[i-1].UT)) return Fail("Invalid KSP event time/state");
                if(Math.Abs(e.UT-numerical.Events[i].UT)>timeTolerance) return Fail("KSP event time disagreement");
                if(e.Kind=="periapsis" && e.RelativeState.R.Length<b.SafeRadius(r.ClearanceMargin)) return Fail("KSP periapsis fails clearance");
                if(e.Kind!="periapsis" && Math.Abs(e.RelativeState.R.Length-b.Soi)>Math.Max(10,b.Soi*1e-5)) return Fail("KSP SOI boundary disagreement");
            }
            Body a=r.Bodies.Single(b=>b.Id==r.Assist),d=r.Bodies.Single(b=>b.Id==r.Destination);
            if(events[1].RelativeState.R.Length-a.Radius<r.MinimumAssistAltitude || Numeric.Energy(events[0].RelativeState,a.Mu)<=0 ||
                Numeric.Energy(events[2].RelativeState,a.Mu)<=0 || V3.Dot(events[2].RelativeState.R,events[2].RelativeState.V)<=0)
                return Fail("KSP assist is unsafe or not an unbound passage");
            if(Math.Abs(events[4].RelativeState.R.Length-d.Radius-r.TargetAltitude)>r.AltitudeTolerance)
                return Fail("KSP destination periapsis outside requested tolerance");
            double energy=Numeric.Energy(events[0].RelativeState,a.Mu);
            if(Math.Abs(Numeric.Energy(events[2].RelativeState,a.Mu)-energy)>Math.Max(.01,Math.Abs(energy)*1e-5))
                return Fail("KSP assist energy discontinuity");
            return new ValidationResult(true,"KSP complete route and periapsis agree",events);
        }
    }
}
