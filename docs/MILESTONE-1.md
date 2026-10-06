# Milestone 1: offline prototype evidence

Historical implementation baseline: 6 October 2026, commit `3c39b85b8c2df1c623c6c6cecc3673974143d86b`. Build identity: assembly version **0.1.0.0**, Release. This report records that milestone's evidence. [Milestone 2](MILESTONE-2.md) supersedes its compile-only integration status with a functional 0.2.0 planning plugin; current checks/manifests describe the current commit, not the old 0.1.0 build.

## Implemented boundary

- Detached, immutable numeric request/body snapshots with finite values, identities, SI units, bounds, conservative terrain/atmosphere clearance, and supported-parent ephemeris checks.
- Universal-variable two-body conic propagation in three dimensions, including backward propagation for fixture construction.
- Finite-SOI event scanning and bracketed roots for assist entry/periapsis/exit and destination entry/periapsis. Body ephemerides evolve on parent conics.
- Position/velocity frame translation at SOI boundaries with no impulses after the departure burn; positive assist entry/exit energy and energy continuity required.
- Full ordered-sequence and destination-altitude validation, bounded journey/scan limits, competing-body encounter rejection, and parent clearance/boundary rejection.
- Deterministic local coordinate pattern search over departure UT and three inertial delta-v components. Imported approximate seeds have no feasibility authority. Feasible candidates are ranked by delta-v, then journey duration, among those actually evaluated.
- Repeatable .NET 8 console harness and frozen synthetic JSON fixtures; no test-framework package.
- .NET Framework 4.8 core and KSP API-contract library compiled against local KSP 1.12.5 references. No startup hook, UI, node creation, or installation.

The numerical increment supports a vessel already outside child SOIs in a shared parent SOI. Parking-orbit departures and moon-assisted planetary escape remain subsequent route work. A preburn encounter is rejected as unsupported rather than propagated through a different route.

## Local API investigation before algorithm selection

The local KSP `readme.txt` identifies version **1.12.5**, and `buildID64.txt` identifies build **03190**. The installed `mscorlib` assembly is version 4.0.0.0. Local MechJeb projects target `net48`; that existing runtime precedent and the successful local reference build establish the compile target, not runtime compatibility proof.

Inspected MechJeb commit: `aeee32212801b987e0fffcad800e0cb565584abb`:

- `MechJeb2/OrbitalManeuverCalculator.cs`, `PatchedConicInterceptBody`: temporary Orbit state, `PatchedConics.CalculatePatch`, reference-body checking, arrival horizon, axis swap, and missing-next-patch cautions.
- `MechJeb2/OrbitExtensions.cs`, `RightHandedStateVectorsAtUT`: cautions about future rotation, true-anomaly state retrieval, and `Planetarium.Zup.WorldToLocal` conversion.
- `MechJeb2/OrbitExtensions.cs`, `DeltaVToManeuverNodeCoordinates`: radial/normal/prograde projections with a normal sign convention.
- `MechJebLib/Maneuvers/InterplanetaryTransfer.cs`: departure/arrival geometry and constrained periapsis work; this is not an unpowered two-encounter gravity-assist solver.

The KSP contract compiles calls to raw Orbit position/velocity, `PeA`, and `PatchedConics.CalculatePatch` with solver parameters. None of these calls has been executed in KSP. Raw API coordinates are explicitly not asserted to match the detached frame yet.

Given discontinuous SOI encounters and limited route evidence, the first solver uses a small deterministic derivative-free pattern search around supplied seeds. This avoids asserting that a pair of unconstrained centre-to-centre Lambert arcs constitutes a flyby. There is no general Lambert seed generator, broad launch-window exploration, or optimality claim in this milestone.

## Repeatable build and checks

Prerequisite: .NET 8 SDK. NuGet restore obtains pinned development-only .NET Framework reference packages.

Offline builds and checks, requiring no game files:

```powershell
./check.ps1 -OfflineOnly
```

Also compile the KSP contract against an owned local game installation:

```powershell
./check.ps1 -KspManagedPath 'C:/Program Files (x86)/Steam/steamapps/common/Kerbal Space Program/KSP_x64_Data/Managed'
```

Alternatively set `KspManagedPath` in an ignored root `local.props` file. No machine-specific path is required in tracked configuration. `build.ps1` builds without running fixtures; `check.ps1` builds, runs checks, verifies fixture hashes and binary exclusions, and records `artifacts/build-manifest.json` plus `artifacts/offline-results.json`.

Outputs are under each project's `bin/Release/<target>/`. They are development artifacts, not an installable mod package. Game/Unity references have `Private=false`; the KSP output directory contains only the authored core and contract DLLs.

## Observed checks

All three Release build targets passed with **zero warnings and zero errors**: core `net48`/`net8.0`, harness `net8.0`, and KSP contract `net48`.

The harness passed **18 check groups, zero failures**:

- Analytic circular quarter orbit and backward round trip.
- Elliptic, parabolic, and hyperbolic propagation against independent RK4, with energy/angular-momentum conservation.
- Explicit rejection of singular conic input.
- Known-burn constraints and bounded/repeated search for all five frozen fixtures.
- Positive route SOI/periapsis regression values and independent RK4 comparisons for every segment and body ephemeris, including both returned solver candidates.
- Refined candidates retain feasibility when scan step is halved.
- Positive fixture seed is initially infeasible, demonstrating actual refinement.
- Cancellation, one-evaluation budget exhaustion, and propagation-step exhaustion.
- Nonfinite values, invalid target identities, mu/time/step bounds, out-of-SOI altitude, and terrain clearance.
- Wrong event order, atmosphere intersection, destination altitude near miss, and short journey horizon.
- An unexpected grazing SOI whose entry and exit both lie between outside scan endpoints; closest-approach bracketing detects and rejects it.

| Fixture | Result | Evaluations | Requested/achieved destination altitude |
| --- | --- | --- | --- |
| Reachable tilted route | OfflineFeasible | 157 | 1445.431944356 m / 1445.502141907 m |
| Reachable inclined destination | OfflineFeasible | 174 | 1478.974101743 m / 1478.893967046 m |
| Constrained miss | NoSolutionFoundWithinBounds | 35 | No accepted candidate |
| Unsafe flyby | NoSolutionFoundWithinBounds | 1 | Known burn rejected for unsafe assist periapsis |
| Invalid identities | InvalidInput | 0 | No search performed |

Searches are bounded by maximum evaluations and maximum propagation steps per evaluation; they can be cancelled. They may stop after local step contraction before the evaluation cap. A no-result outcome means **no solution found within bounds**, never proof of physical impossibility. Search timing is not yet a performance guarantee.

SHA-256 of the verified local `Assembly-CSharp.dll` reference:

```text
D9E42483F25EE80A9C11D6C1C0A0D29B4EC78C1E08D76C971B71580C9CCE51E4
```

The verified Release `net48` outputs are `KerbalSlingshot.Core.dll` and `KerbalSlingshot.KSP.dll`, assembly version `0.1.0.0`, not installed. Their exact SHA-256 values are recorded alongside the source commit and working-tree state in `artifacts/build-manifest.json` by each check run. The SDK includes source-revision metadata in builds, so artifact hashes must be associated with the actual commit built rather than copied from a preceding uncommitted build.

## Unresolved technical uncertainties

1. KSP runtime loading, detached-versus-KSP coordinate conventions, SOI event agreement, API object lifecycle/thread safety, and patch-depth behaviour remain untested in game.
2. The event scanner uses finite geometric steps, bracketed boundary roots, and relative-radial closest-approach bracketing for grazing encounters hidden between endpoints. Tested positive routes are stable under a halved scan step, but this is not a proof that every fast/repeated encounter or multiple extrema in one interval is detected. General event coverage and robust seed discovery need more evidence before live planning.
3. Synthetic fixtures use supplied SOIs and conservative terrain ceilings. They do not establish stock-system reachability or availability of safe terrain bounds from KSP.
4. The local search can remain in a basin or miss reachable solutions. It searches no powered flyby/correction/capture alternatives and proves no optimum.
5. Real vessel snapshots, stale state, game calendar display, planning restrictions, UI, node-frame conversion, insertion/rollback, and finite-burn execution are not implemented.
6. The harness executes `net8.0`; `net48` compilation passes, but Unity/Mono runtime execution has not been checked.

**No KSP session was launched, no DLL was installed, and no result is KSP-validated.** Offline results always report `KspValidated=false` and cannot create nodes.

## First useful combined KSP session

Do not run a load-only session for this contract library. Prepare the first session once a minimal live adapter and candidate display/validator can read a vessel/celestial snapshot and compare a complete route. Before installation, write the exact game version/mod set, recoverable save/vessel starting state, targets/altitude/bounds, build hashes, steps, expected events, tolerances, and evidence capture.

Combine loading with snapshot/frame checks, an offline-versus-KSP full patch comparison, a constrained miss/incomplete-patch check, and node prediction only if insertion is then implemented. This milestone has not passed the in-game exit requirements of Gates 1 or 2, and a calendar session is not scheduled by this document.
