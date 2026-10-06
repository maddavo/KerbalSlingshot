using KerbalSlingshot.Core;
using UnityEngine;

namespace KerbalSlingshot.KSP
{
    // Read on the Unity main thread. In-game frame/patch agreement remains a test requirement.
    public static class ApiContract
    {
        public static State ReadRawOrbitState(Orbit orbit,double ut)
        {
            Vector3d r=orbit.getRelativePositionAtUT(ut),v=orbit.getOrbitalVelocityAtUT(ut);
            return new State(new V3(r.x,r.y,r.z),new V3(v.x,v.y,v.z));
        }

        public static State ReadInertialState(Orbit orbit,double ut)
        {
            // Avoid the future-rotation overload; request a true-anomaly state in the current world frame.
            orbit.GetOrbitalStateVectorsAtTrueAnomaly(orbit.TrueAnomalyAtT(orbit.getObtAtUT(ut)),ut,false,
                out Vector3d position,out Vector3d velocity);
            return new State(ToCore(Planetarium.Zup.WorldToLocal(position)),ToCore(Planetarium.Zup.WorldToLocal(velocity)));
        }

        public static V3 ToCore(Vector3d v) => new V3(v.x,v.y,v.z);

        public static BurnFrame ReadBurnFrame(Orbit orbit,double ut)
        {
            orbit.GetOrbitalStateVectorsAtUT(ut,out Orbit.State state);
            QuaternionD rotation=QuaternionD.LookRotation(state.vel.xzy,Vector3d.Cross(-state.pos.xzy,state.vel.xzy));
            V3 Axis(Vector3d axis) => ToCore(Planetarium.Zup.WorldToLocal((rotation*axis).xzy));
            var frame=new BurnFrame(Axis(new Vector3d(1,0,0)),Axis(new Vector3d(0,1,0)),Axis(new Vector3d(0,0,1)));
            BurnFrame reference=NativeFrame.FromFixed(new State(ToCore(state.pos),ToCore(state.vel)),
                v=>ToCore(Planetarium.Zup.WorldToLocal(ToNative(v))));
            if((frame.Radial-reference.Radial).Length>1e-8 || (frame.Normal-reference.Normal).Length>1e-8 ||
                (frame.Prograde-reference.Prograde).Length>1e-8) throw new System.InvalidOperationException("Native quaternion frame disagrees with fixed-axis reference");
            return frame;
        }

        public static Vector3d ToNative(V3 v) => new Vector3d(v.X,v.Y,v.Z);

        public static Orbit BurnOrbit(Orbit source,Burn burn,out V3 components,out double roundTripError)
        {
            components=ReadBurnFrame(source,burn.UT).ToComponents(burn.DeltaV);
            source.GetOrbitalStateVectorsAtUT(burn.UT,out Orbit.State state);
            QuaternionD rotation=QuaternionD.LookRotation(state.vel.xzy,Vector3d.Cross(-state.pos.xzy,state.vel.xzy));
            Vector3d fixedDelta=(rotation*ToNative(components)).xzy;
            var initial=new Orbit(); initial.UpdateFromFixedVectors(state.pos,state.vel,source.referenceBody,burn.UT);
            var after=new Orbit(); after.UpdateFromFixedVectors(state.pos,state.vel+fixedDelta,source.referenceBody,burn.UT);
            var probe=new ManeuverNode { UT=burn.UT,DeltaV=ToNative(components),patch=initial,nextPatch=after };
            // GetBurnVector is a raw Z-up patch-velocity difference, then swapped to world order.
            V3 actual=ToCore(probe.GetBurnVector(initial).xzy);
            roundTripError=(actual-burn.DeltaV).Length;
            after.StartUT=burn.UT;
            return after;
        }

        public static double ReadPeriapsisAltitude(Orbit orbit) => orbit.PeA;

        // Detached caller-owned patches only. This method has not been invoked in KSP.
        public static bool CalculatePatch(Orbit temporaryOrbit,Orbit next,double ut)
            => PatchedConics.CalculatePatch(temporaryOrbit,next,ut,new PatchedConics.SolverParameters(),null);
    }
}
