using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KerbalSlingshot.Core
{
    // Presentation only: no trajectory evaluation, live objects, or changed safety defaults.
    public sealed class PlannerButtons
    {
        public bool Edit { get; }
        public bool Import { get; }
        public bool Calculate { get; }
        public bool Cancel { get; }
        public PlannerButtons(bool busy,bool routeAvailable,bool distinctTargets,bool estimateReady,bool safetyConfirmed,bool fieldsValid)
        { Edit=!busy; Import=!busy && routeAvailable; Calculate=!busy && routeAvailable && distinctTargets && estimateReady && safetyConfirmed && fieldsValid; Cancel=busy; }
    }

    public static class PlannerLayout
    {
        public const int Width=520, PrimaryHeight=684, ManualHeight=80, AdvancedHeight=200, DetailsHeight=150;
        public const int ResultTop=342, EventTop=436, FooterTop=654;
        public static int Height(bool manual,bool advanced,bool details,int screenHeight)
            => Math.Min(PrimaryHeight+(manual?ManualHeight:0)+(advanced?AdvancedHeight:0)+(details?DetailsHeight:0),Math.Max(PrimaryHeight,screenHeight-100));
    }

    public sealed class PlannerEventRow
    {
        public string Label { get; }
        public string Time { get; }
        public string Altitude { get; }
        public PlannerEventRow(string label,string time,string altitude) { Label=label; Time=time; Altitude=altitude; }
    }

    public sealed class PlannerResultCard
    {
        public string Heading { get; }
        public string Validation => Accepted?"Unvalidated — compare with KSP":"Unvalidated — partial diagnostics only";
        public bool Accepted { get; }
        public string Departure { get; }
        public string DeltaV { get; }
        public string Error { get; }
        public IReadOnlyList<PlannerEventRow> Events { get; }
        private static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
        public static string Time(double ut) => ut.ToString("0.000",Invariant);
        public static string Number(double value,string format="0.00") => value.ToString(format,Invariant);
        public static string Short(string text,int maximum) => text.Length<=maximum?text:text.Substring(0,Math.Max(0,maximum-3))+"...";
        public PlannerResultCard(SearchResult result,Request request,bool refine)
        {
            string[] sequence={"entry:"+request.Assist,"periapsis:"+request.Assist,"exit:"+request.Assist,"entry:"+request.Destination,"periapsis:"+request.Destination};
            Accepted=result.Status==SearchStatus.OfflineFeasible && result.Candidate.HasValue &&
                result.Evaluation?.Status==EvaluationStatus.OfflineFeasible &&
                result.Evaluation.Events.Select(e=>e.Kind+":"+e.Body).SequenceEqual(sequence);
            Heading=Accepted?"Offline-feasible prediction":result.Status==SearchStatus.Cancelled?"Calculation cancelled":
                result.Status==SearchStatus.InvalidInput?"Invalid request":refine?"No solution found within bounds":"Estimate rejected";
            Burn? burn=Accepted?result.Candidate:result.DiagnosticBurn;
            Evaluation? evaluation=Accepted?result.Evaluation:result.DiagnosticEvaluation;
            Departure=burn.HasValue?Time(burn.Value.UT)+" UT":"--";
            DeltaV=burn.HasValue?Number(burn.Value.DeltaV.Length)+" m/s":"--";
            Error=evaluation?.DestinationAltitude!=null?
                Number(evaluation.DestinationAltitude.Value-request.TargetAltitude)+" m  (tolerance "+Number(request.AltitudeTolerance)+" m)":"Not reached";
            string[] labels={"Assist entry","Assist periapsis","Assist exit","Destination entry","Destination periapsis"};
            var rows=new List<PlannerEventRow>();
            for (int i=0;i<sequence.Length;i++)
            {
                EncounterEvent? ev=evaluation?.Events.FirstOrDefault(e=>e.Kind+":"+e.Body==sequence[i]);
                Body? body=ev==null?null:request.Bodies.FirstOrDefault(b=>b.Id==ev.Body);
                rows.Add(new PlannerEventRow(labels[i],ev==null?"--":Time(ev.UT),ev==null || body==null?"--":Number((ev.RelativeState.R.Length-body.Radius)/1000,"0.000")));
            }
            Events=rows.AsReadOnly();
        }
    }
}
