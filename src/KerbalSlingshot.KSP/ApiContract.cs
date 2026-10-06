using KerbalSlingshot.Core;

namespace KerbalSlingshot.KSP
{
    // Compile-only contract against installed KSP 1.12.5. No startup hook, UI, node writes, or runtime validation.
    // The raw API frame must be compared with the detached inertial frame in the planned game session.
    public static class ApiContract
    {
        public static State ReadRawOrbitState(Orbit orbit,double ut)
        {
            Vector3d r=orbit.getRelativePositionAtUT(ut),v=orbit.getOrbitalVelocityAtUT(ut);
            return new State(new V3(r.x,r.y,r.z),new V3(v.x,v.y,v.z));
        }

        public static double ReadPeriapsisAltitude(Orbit orbit) => orbit.PeA;

        // Detached caller-owned patches only. This method has not been invoked in KSP.
        public static bool CalculatePatch(Orbit temporaryOrbit,Orbit next,double ut)
            => PatchedConics.CalculatePatch(temporaryOrbit,next,ut,new PatchedConics.SolverParameters(),null);
    }
}
