using System;

namespace KerbalSlingshot.Core
{
    public static class NativeFrame
    {
        public static V3 Swap(V3 v) => new V3(v.X,v.Z,v.Y);
        // KSP's LookRotation(state.vel.xzy, Cross(-state.pos.xzy,state.vel.xzy)), followed by .xzy and Z-up mapping.
        public static BurnFrame FromFixed(State state,Func<V3,V3> fixedToCore)
        {
            V3 forward=Swap(state.V); forward=forward/forward.Length;
            V3 up=V3.Cross(-Swap(state.R),forward); up=up/up.Length;
            V3 right=V3.Cross(up,forward); right=right/right.Length;
            return new BurnFrame(fixedToCore(Swap(right)),fixedToCore(Swap(up)),fixedToCore(Swap(forward)));
        }
    }
    public static class NodeAuthorization
    {
        public static bool Allowed(bool completeKspValidation,bool fresh,bool futureNodes,bool busy)
            => completeKspValidation && fresh && !futureNodes && !busy;
    }
}
