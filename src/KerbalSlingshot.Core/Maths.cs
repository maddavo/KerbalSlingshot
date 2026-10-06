using System;

namespace KerbalSlingshot.Core
{
    // All vectors use one inertial, right-handed Cartesian frame; SI units throughout.
    public readonly struct V3
    {
        public readonly double X, Y, Z;
        public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double Length => Math.Sqrt(Dot(this, this));
        public bool Finite => Numeric.Finite(X) && Numeric.Finite(Y) && Numeric.Finite(Z);
        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator -(V3 a) => a * -1;
        public static V3 operator *(V3 a, double s) => new V3(a.X * s, a.Y * s, a.Z * s);
        public static V3 operator /(V3 a, double s) => a * (1 / s);
        public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static V3 Cross(V3 a, V3 b) => new V3(a.Y*b.Z-a.Z*b.Y, a.Z*b.X-a.X*b.Z, a.X*b.Y-a.Y*b.X);
        public override string ToString() => FormattableString.Invariant($"({X:G12},{Y:G12},{Z:G12})");
    }

    public readonly struct State
    {
        public readonly V3 R, V;
        public State(V3 r, V3 v) { R = r; V = v; }
        public bool Finite => R.Finite && V.Finite && R.Length > 0;
    }

    public static class Numeric
    {
        public static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        public static double Energy(State s, double mu) => .5 * V3.Dot(s.V, s.V) - mu / s.R.Length;
        public static double Periapsis(State s, double mu)
        {
            double h2 = V3.Dot(V3.Cross(s.R, s.V), V3.Cross(s.R, s.V));
            double e = Math.Sqrt(Math.Max(0, 1 + 2 * Energy(s, mu) * h2 / (mu * mu)));
            return h2 / (mu * (1 + e));
        }
    }

    public static class Kepler
    {
        // Universal-variable f/g propagation. Bisection uses dF/dchi = radius > 0.
        // No third-party implementation; fails explicitly if its finite bracket is exhausted.
        public static State Propagate(State s, double mu, double dt)
        {
            if (!s.Finite || !Numeric.Finite(mu) || mu <= 0 || !Numeric.Finite(dt))
                throw new ArgumentException("Invalid conic state, mu, or duration");
            if (dt == 0) return s;
            if (dt < 0)
            {
                State back = Propagate(new State(s.R, -s.V), mu, -dt);
                return new State(back.R, -back.V);
            }
            double r0 = s.R.Length, sqrtMu = Math.Sqrt(mu);
            double alpha = 2 / r0 - V3.Dot(s.V, s.V) / mu;
            double rv = V3.Dot(s.R, s.V) / sqrtMu;
            double Equation(double x)
            {
                Stumpff(alpha*x*x, out double c, out double ss);
                return rv*x*x*c + (1-alpha*r0)*x*x*x*ss + r0*x - sqrtMu*dt;
            }
            double lo = 0, hi = Math.Max(1, sqrtMu * dt / r0);
            int expansions = 0;
            while (Equation(hi) < 0 && expansions++ < 80) hi *= 2;
            if (!Numeric.Finite(Equation(hi)) || Equation(hi) < 0)
                throw new ArithmeticException("Universal-variable bracket exhausted");
            for (int i = 0; i < 90; i++)
            {
                double mid = (lo + hi) / 2;
                if (Equation(mid) > 0) hi = mid; else lo = mid;
            }
            double chi = (lo + hi) / 2;
            Stumpff(alpha*chi*chi, out double cc, out double sc);
            double f = 1 - chi*chi*cc/r0;
            double g = dt - chi*chi*chi*sc/sqrtMu;
            V3 r = s.R*f + s.V*g;
            double rn = r.Length;
            double fdot = sqrtMu/(rn*r0)*(alpha*chi*chi*chi*sc-chi);
            double gdot = 1 - chi*chi*cc/rn;
            State result = new State(r, s.R*fdot + s.V*gdot);
            if (!result.Finite) throw new ArithmeticException("Nonfinite propagated state");
            return result;
        }

        private static void Stumpff(double z, out double c, out double s)
        {
            if (Math.Abs(z) < 1e-3)
            {
                c = .5; s = 1.0/6;
                double ct = c, st = s;
                for (int k = 1; k <= 8; k++)
                {
                    ct *= -z/((2*k+1.0)*(2*k+2));
                    st *= -z/((2*k+2.0)*(2*k+3));
                    c += ct; s += st;
                }
            }
            else if (z > 0)
            {
                double q = Math.Sqrt(z);
                c = (1-Math.Cos(q))/z; s = (q-Math.Sin(q))/(q*q*q);
            }
            else
            {
                double q = Math.Sqrt(-z);
                c = (Math.Cosh(q)-1)/(-z); s = (Math.Sinh(q)-q)/(q*q*q);
            }
        }
    }
}
