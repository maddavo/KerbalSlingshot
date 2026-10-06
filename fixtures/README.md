# Offline fixtures

All JSON values use **metres, seconds, m/s, and m³/s²** in one right-handed inertial frame. Epoch and burn times are game-like UT seconds. This internal fixture interface uses altitude in metres; the planned UI will convert kilometres explicitly.

These are synthetic sibling-body systems, not stock KSP saves or captured in-game ephemerides. SOIs and terrain/atmosphere bounds are supplied model values. They exercise one burn and finite-SOI unpowered encounters without claiming stock-system reachability.

| File | Expected result |
| --- | --- |
| `reachable.json` | Full ordered assist/destination sequence, solver refinement from a deliberately perturbed seed |
| `reachable-inclined.json` | Same requirement with a destination in a different orbital plane |
| `constrained-miss.json` | No solution found within the delta-v/journey bounds |
| `unsafe-flyby.json` | Known burn rejected for assist clearance; one-evaluation search has no feasible result |
| `invalid-input.json` | Identical target identities rejected before search |

`KnownBurn` is an offline oracle input, **not** injected into a positive fixture's solver seed list. Expected event times and radii are frozen. Both the known and solved routes are checked with an independent RK4 integrator, including patch position/velocity continuity, SOI radii, periapsis conditions, and unbound assist energy. Normal checks never regenerate expected values; `check.ps1` verifies the JSON hashes remain unchanged.

Fixture construction is intentionally inverse design: start with a safe assist hyperbola, place the destination on the resulting outgoing route, measure and freeze its periapsis, and perturb the departure seed. This proves local refinement on constructed reachable cases. It does not prove general seed discovery or an optimal trajectory. The RK4 checks and analytic orbit checks supply independent numerical evidence in addition to frozen regression values.

For intentional fixture maintenance only:

```powershell
dotnet run --project tests/KerbalSlingshot.Harness -c Release -- --construct fixtures
```

Review every regenerated difference and rerun independent checks before accepting it. This command is not part of the build/check procedure.
