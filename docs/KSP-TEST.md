# Automatic Mun-to-Minmus node acceptance test

This is the required user workflow for the next functional implementation. **Do not run this procedure against 0.2.1:** that prototype requires an estimate, reports a native burn-frame import failure, does not validate a candidate against KSP, and cannot create nodes. It is not Dave's job to prepare a Mun flyby node to make the planner testable.

Use the **0.3.0** package only after Dave releases his installation hold. [AUTOMATIC-HANDOFF.md](AUTOMATIC-HANDOFF.md) supplies the exact default bounds and concise combined steps. Existing Space Camp (~80 km Kerbin orbit) is the intended live capture; its celestial phase is not the constructed offline fixture, so no live convergence/pass is promised without testing. No preparatory node is setup.

## Test setup

Use KSP 1.12.5 with stock patched conics and without Principia. Use a recoverable copy of a save with a coasting active vessel in Kerbin's SOI, outside Mun's and Minmus's SOIs. Start with **no manoeuvre nodes**. Record game version, loaded mods, save/vessel name, UT, orbit, UI scale, and the installed package build/hash manifest.

The development handoff must identify a reproducible starting state and the departure/journey/search bounds used. If the live state is not a reachable fixture for the claimed acceptance case, the developer must supply a recoverable test save or another reproducible state; do not ask Dave to make a preliminary flyby node.

The numeric automatic fixture is reproducible offline but not a passing game-save claim. The live Space Camp capture remains untested; if its phase/bounds fail to yield a complete validated route, record the full generated snapshot/diagnostics for the next offline iteration rather than asking Dave to prepare a manoeuvre. Do not mark this acceptance gate passed on synthetic recovery alone.

## One combined KSP session

1. Open **Slingshot** from the stock toolbar. Confirm the active vessel and current parent body are identified correctly.
2. Select **Mun** as Gravity Assist Target, **Minmus** as Intercept Target, and a safe destination periapsis altitude in km. Do not import a node or enter a departure burn.
3. Review any required terrain/safety assumptions in Advanced settings. Ordinary search bounds should have usable defaults; the UI must not demand tuning solver internals before calculation.
4. Start **Find Trajectory** (or equivalent). Confirm the UI remains responsive, shows progress, and offers cancellation. Confirm target changes, vessel changes, cancellation, and stale state cannot publish an old result.
5. A complete result must show the generated departure UT and delta-v, Mun SOI entry, safe unpowered periapsis and unbound exit, Minmus SOI entry, destination periapsis, achieved altitude/error, journey duration, and validation status. A partial or detached-only prediction must not enable node creation.
6. KSP validation must confirm the same ordered body/patch sequence, native burn frame, flyby safety, and destination periapsis tolerance. If validation disagrees or the patch chain is incomplete, expect an explicit failure and no node insertion.
7. For a fully validated result, press **Create Node**. Expect exactly one departure node at the returned UT and delta-v, with KSP's resulting patch sequence matching the candidate. No burn executes, time warp starts, or unrelated node is changed.
8. Try a deliberately constrained search and invalid/unsafe periapsis request. Expect clear invalid-input or **No solution found within bounds** outcomes, never a false success. This result does not prove a route is physically impossible.
9. Cancel a longer search and change a target or vessel during another run if practical. Expect cancellation/stale work to be discarded. Record node count and node values before and after each action.

## Pass criteria and evidence

The central acceptance criterion is automatic planning from the no-node starting state. Dave only selects the two bodies and requested periapsis; he does not design, import, or type a seed manoeuvre. The mod finds the candidate, validates it in KSP, and creates one node only after explicit user action.

Capture screenshots of ready inputs, search progress/cancellation, the complete result, and any rejected case. Include the exact inputs/bounds, build manifest and installed DLL hashes, before/after node count and node values, displayed and actual KSP patch events, `[KerbalSlingshot]` log lines and exceptions, and the diagnostic report if available. Report offline solver results, KSP prediction agreement, and flown trajectory results separately. Do not claim the burn was flown unless it was actually executed and observed.

If no complete candidate is found in the stated bounds, preserve the diagnostics and report that the acceptance case did not pass. Do not turn the result into a request for Dave to create a seed node.
