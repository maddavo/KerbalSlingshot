using System;
using System.Globalization;
using System.Linq;
using KerbalSlingshot.Core;

namespace KerbalSlingshot.KSP
{
    internal sealed class CapturedPlan
    {
        public Request Request { get; }
        public Burn Seed { get; }
        public string VesselId { get; }
        public string VesselName { get; }
        public string ParentId { get; }
        public string BodySignature { get; }
        public string NodeSignature { get; }
        public bool CappedParent { get; }
        public CapturedPlan(Request request,Burn seed,Vessel vessel,bool capped)
        {
            Request=request; Seed=seed; VesselId=vessel.id.ToString(); VesselName=vessel.vesselName;
            ParentId=vessel.orbit.referenceBody.bodyName; CappedParent=capped;
            BodySignature=SnapshotAdapter.BodySignature(vessel.orbit.referenceBody);
            NodeSignature=SnapshotAdapter.NodeSignature(vessel);
        }
    }

    internal static class SnapshotAdapter
    {
        public static Vessel Active()
        {
            Vessel v=FlightGlobals.ActiveVessel;
            if (v==null || v.orbit==null || v.orbit.referenceBody==null) throw new InvalidOperationException("No active orbital vessel");
            if (v.LandedOrSplashed) throw new InvalidOperationException("Unsupported route: vessel must be coasting in orbit");
            return v;
        }
        public static CelestialBody[] Children(CelestialBody parent) => FlightGlobals.Bodies
            .Where(b=>b!=parent && b.orbit!=null && b.orbit.referenceBody==parent)
            .OrderBy(b=>b.bodyName,StringComparer.Ordinal).ToArray();

        public static CapturedPlan Capture(PlannerSettings settings,string assist,string destination,bool terrainConfirmed)
        {
            if (!terrainConfirmed) throw new ArgumentException("Review the changed terrain ceiling before calculating");
            Vessel v=Active();
            if(v.ctrlState.mainThrottle>1e-6f) throw new ArgumentException("Coast with throttle zero before planning");
            CelestialBody parent=v.orbit.referenceBody;
            CelestialBody[] children=Children(parent);
            if(parent.bodyName!="Kerbin" || assist!="Mun" || destination!="Minmus")
                throw new ArgumentException("Initial route supports Kerbin SOI -> Mun -> Minmus only");
            if(Math.Abs(parent.Radius-600000)>1 || Math.Abs(parent.gravParameter/3.5316e12-1)>1e-5 ||
                children.Length!=2 || !children.Any(b=>b.bodyName=="Mun" && Math.Abs(b.Radius-200000)<1) ||
                !children.Any(b=>b.bodyName=="Minmus" && Math.Abs(b.Radius-60000)<1))
                throw new ArgumentException("Stock Kerbin/Mun/Minmus safety defaults do not cover this system");
            if (children.Length<2 || !children.Any(b=>b.bodyName==assist) || !children.Any(b=>b.bodyName==destination))
                throw new ArgumentException("Unsupported route: choose two direct children of the vessel's current parent SOI");
            if (children.Where(b=>b.bodyName==assist || b.bodyName==destination).Any(b=>Children(b).Length>0))
                throw new ArgumentException("Unsupported nested route: selected targets have children whose SOI encounters are not modelled");
            double now=Planetarium.GetUniversalTime();
            State vessel=ApiContract.ReadInertialState(v.orbit,now);
            Body[] bodies=children.Select(b=>new Body(b.bodyName,b.gravParameter,b.Radius,b.sphereOfInfluence,
                ApiContract.ReadInertialState(b.orbit,now),settings.TerrainCeiling,b.atmosphere?b.atmosphereDepth:0)).ToArray();
            bool capped=!Numeric.Finite(parent.sphereOfInfluence);
            double parentSoi=capped?1e15:parent.sphereOfInfluence;
            double safe=parent.Radius+Math.Max(settings.TerrainCeiling,parent.atmosphere?parent.atmosphereDepth:0)+settings.Margin;
            var request=new Request(now,parent.gravParameter,safe,parentSoi,vessel,bodies,assist,destination,
                settings.TargetAltitude,settings.AssistAltitude,settings.Margin,settings.Tolerance,
                now+600,now+600+settings.Window,settings.Duration,
                settings.MaxDeltaV,settings.MaxStep,settings.MaxSteps);
            string? invalid=request.InvalidReason();
            if (invalid!=null) throw new ArgumentException("Snapshot rejected: "+invalid);
            return new CapturedPlan(request,new Burn(request.Earliest,new V3(0,0,0)),v,capped);
        }

        public static ManeuverNode SingleFutureNode()
        {
            Vessel v=Active(); double now=Planetarium.GetUniversalTime();
            ManeuverNode[] nodes=v.patchedConicSolver==null?Array.Empty<ManeuverNode>():v.patchedConicSolver.maneuverNodes.Where(n=>n.UT>now).ToArray();
            if (nodes.Length!=1) throw new InvalidOperationException("Import/comparison requires exactly one existing future node");
            ManeuverNode node=nodes[0];
            if (node.patch==null || node.patch.referenceBody!=v.orbit.referenceBody)
                throw new InvalidOperationException("Existing node is on another SOI patch");
            BurnFrame frame=ApiContract.ReadBurnFrame(v.orbit,node.UT);
            V3 expected=frame.ToInertial(ApiContract.ToCore(node.DeltaV));
            V3 actual=ApiContract.ToCore(node.GetBurnVector(v.orbit).xzy);
            if ((expected-actual).Length>Math.Max(1e-6,actual.Length*1e-8))
                throw new InvalidOperationException("Native burn-frame check failed; report the node and KSP log");
            return node;
        }

        public static string NodeSignature(Vessel v) => v.patchedConicSolver==null?"none":string.Join(";",
            v.patchedConicSolver.maneuverNodes.Select(n=>F(n.UT)+":"+F(n.DeltaV.x)+","+F(n.DeltaV.y)+","+F(n.DeltaV.z)));
        public static string BodySignature(CelestialBody parent) => parent.bodyName+":"+F(parent.gravParameter)+":"+F(parent.Radius)+":"+
            F(parent.sphereOfInfluence)+"|"+string.Join(";",Children(parent).Select(b=>b.bodyName+":"+F(b.gravParameter)+":"+
                F(b.Radius)+":"+F(b.sphereOfInfluence)+":"+F(b.atmosphereDepth)+":"+F(b.orbit.semiMajorAxis)+":"+
                F(b.orbit.eccentricity)+":"+F(b.orbit.inclination)+":"+F(b.orbit.LAN)+":"+F(b.orbit.argumentOfPeriapsis)+":"+
                F(b.orbit.meanAnomalyAtEpoch)+":"+F(b.orbit.epoch)));
        public static string F(double n) => n.ToString("G17",CultureInfo.InvariantCulture);

        public static bool Fresh(CapturedPlan plan,double? burnUT)
        {
            Vessel v=Active(); double now=Planetarium.GetUniversalTime();
            return v.id.ToString()==plan.VesselId && v.orbit.referenceBody.bodyName==plan.ParentId &&
                (!burnUT.HasValue || now<burnUT.Value) && NodeSignature(v)==plan.NodeSignature &&
                BodySignature(v.orbit.referenceBody)==plan.BodySignature &&
                SnapshotFreshness.Matches(plan.Request.Vessel,plan.Request.Epoch,ApiContract.ReadInertialState(v.orbit,now),now,plan.Request.ParentMu);
        }
    }
}
