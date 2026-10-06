# Milestone 2: functional candidate-planning plugin

Assembly version **0.2.0.0**, Release, target **KSP 1.12.5 / .NET Framework 4.8**. The clean source commit and exact DLL SHA-256 are recorded by `check.ps1` in `artifacts/build-manifest.json` and included in the test ZIP. The window and `[KerbalSlingshot]` log lines display the SDK informational version with source revision.

## Implemented

- Actual public `MonoBehaviour` flight addon with `KSPAddon(Flight, false)`, an on-screen Slingshot toggle, scrollable planner, and ship-control input lock while interacting with the window.
- Main-thread active-vessel and celestial snapshot: parent identity, game UT, conic position/velocity, all direct-child ephemerides, radii, SOIs, mu, and atmosphere depths. The detached worker receives no live Orbit, Vessel, CelestialBody, or Unity objects.
- Choice of assist/destination bodies and requested destination periapsis **altitude in km**, explicitly converted to metres. Game times are absolute UT / durations in seconds, not wall-clock dates.
- An explicit native radial/normal/prograde burn estimate and departure UT, typed or imported **read-only** from exactly one existing future node on the current parent patch. Import checks its converted vector against KSP's `GetBurnVector`; a discrepancy blocks import.
- **Evaluate estimate** propagates the exact entered burn. **Refine estimate** invokes the existing bounded local solver around it, with progress, evaluation/scan/time caps, cancellation, and rejection diagnostics. There is no general seed generator.
- Full five-event routes are labelled only **offline-feasible, KSP validation pending**. Approximate seeds, partial routes, unsafe paths, and altitude misses are diagnostic/rejected results. A wall-bounded search retains an already-complete feasible candidate; expiry without one says no solution found within bounds.
- Candidate UT, total/inertial DV, native node components, predicted SOI/periapsis event UTs, altitudes/speeds, final altitude error/tolerance, search settings and rejection counts.
- Vessel/parent/body/node-plan checks and numerical coast freshness. Target/input changes, cancel, scene destruction, and stale state abandon the job revision so old worker results cannot publish. Passed departure times invalidate results.
- Read-only summary of an **existing** future node's KSP patch chain for manual comparison; it is never called validation of a refined candidate. An eight-patch cap and truncation caveat are explicit.
- User-requested diagnostic text files with build/settings/seed/result and detached vessel/body numeric states under `GameData/KerbalSlingshot/Diagnostics/`, plus log evidence.
- Clean-commit packaging with only the authored core and plugin DLLs, hash verification of copied and ZIP-contained bytes, build/check manifests, dependency notes, installation/rollback guide, and one combined KSP test.

## Boundary and assumptions

The solver still starts with a vessel **outside all child SOIs in one shared parent SOI**. It does not implement parking-orbit departures, departure SOI changes, or moon-assisted planetary escape. The live adapter rejects selected bodies that themselves have children, because their nested SOI encounters are not modelled. It includes other direct children as competing encounters.

An infinite central-parent SOI is represented by a visible/documented finite numerical boundary of **1e15 m**; this is not a claimed stellar physical SOI. Each child ephemeris must remain within the supported parent interval.

Terrain maxima are not measured from PQS in this build. The user must supply and acknowledge one conservative terrain ceiling in km covering the parent and **all** children. Defaults are editable starting values, not verified terrain measurements. Parent/child safety radii include that ceiling, KSP atmosphere depth, and the clearance margin. Unsupported systems or bounds are rejected before computation.

The UI estimate is native node components at its specified UT. The search varies inertial components and UT; the returned native components are recomputed at the candidate time. Axis conversion is implemented from locally inspected KSP/MechJeb conventions and compiled against installed APIs, but detached-versus-game frame and event agreement remain pending. Import's live vector check is a consistency guard, not a full trajectory check.

**No node creation or modification is implemented or enabled.** No auto execution, time warp, capture, or extra burns are added. A user may manually compare a recorded candidate with a node in a disposable save. The plugin itself never marks a result KSP-validated.

## Offline and compile evidence

Release builds passed with zero warnings/errors: detached core `net48;net8.0`, harness `net8.0`, actual flight plugin `net48`. The existing five frozen fixtures remain byte-for-byte unchanged.

**27 harness check groups passed, zero failed:** the prior 18 numerical/constraint checks plus invariant UI parsing and SI conversion; bad/nonfinite/ambiguous and over-budget inputs; native-basis round trip; coasting freshness versus altered state; abandoned async completion; estimate-to-fixture pipeline; partial diagnostic separation/progress; mid-search cancellation; and wall-budget candidate handling. Positive synthetic searches still converge in 157 and 174 evaluations within their 0.1 m destination-altitude tolerances.

The metadata inspector reads PE metadata without loading/executing game assemblies. It confirms the actual public addon type derives from UnityEngine.MonoBehaviour, carries the installed KSP **Flight** startup enum and nonpersistent flag, and references matching installed assembly versions: KSP Assembly-CSharp, Unity Core/IMGUI modules, mscorlib/System/System.Core, and packaged core 0.2.0.0. The typed compiler resolves snapshot, frame, node-read, patch-read, GUI, lifecycle, and input-lock APIs against the local KSP 1.12.5 assembly set. This is runtime-relevant compile/metadata evidence, **not runtime execution**.

The package excludes harness DLLs, PDBs, NuGet reference assemblies, and KSP/Unity DLLs. Packaging verifies its two DLL hashes against the built copies and compressed bytes. No third-party numerical code/library was introduced; dependency review includes the additional local IMGUI reference and SDK metadata-inspection libraries.

Repeatable commands:

```powershell
./check.ps1 -OfflineOnly
./check.ps1 -KspManagedPath 'C:/path/to/Kerbal Space Program/KSP_x64_Data/Managed'
./package.ps1 -KspManagedPath 'C:/path/to/Kerbal Space Program/KSP_x64_Data/Managed'
```

## Pending in-game evidence and next same-task check

KSP has not been launched for this implementation; no installed DLL was overwritten. Unity/Mono loading, GUI interaction, snapshot/frame agreement, live worker responsiveness/freshness, and actual encounter agreement await **KSP-TEST.md**. The test combines live data, body selection, seed import/evaluation/refinement, read-only patch comparison, and failure/cancellation checks in one recoverable flight.

General seed discovery and difficult event coverage remain limitations of the existing numerical core. A reachable fixture does not prove an arbitrary stock orbit/date can satisfy the chosen route with one burn. Missing results mean no solution found within the stated bounds; no physical impossibility or global-optimum claim is made. In-game Gates 1/2 remain open until the recorded trajectory comparison is actually performed.
