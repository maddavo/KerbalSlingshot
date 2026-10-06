using KerbalSlingshot.Core;

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
            Vector3d prograde=orbit.getOrbitalVelocityAtUT(ut).xzy.normalized;
            Vector3d radial=orbit.getRelativePositionAtUT(ut).xzy.normalized;
            radial=(radial-prograde*Vector3d.Dot(radial,prograde)).normalized;
            // Native node's positive normal axis, including KSP world-axis ordering.
            Vector3d normal=orbit.GetOrbitNormal().xzy.normalized;
            return new BurnFrame(ToCore(Planetarium.Zup.WorldToLocal(radial)),
                ToCore(Planetarium.Zup.WorldToLocal(normal)),ToCore(Planetarium.Zup.WorldToLocal(prograde)));
        }

        public static double ReadPeriapsisAltitude(Orbit orbit) => orbit.PeA;

        // Detached caller-owned patches only. This method has not been invoked in KSP.
        public static bool CalculatePatch(Orbit temporaryOrbit,Orbit next,double ut)
            => PatchedConics.CalculatePatch(temporaryOrbit,next,ut,new PatchedConics.SolverParameters(),null);
    }
}
