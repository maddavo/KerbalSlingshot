# Development stages

Current stage: **first offline implementation milestone complete**. The detached sibling-body core, bounded solver increment, frozen fixtures, and repeatable harness are implemented and checked. The core and KSP API contract build for `net48` against local KSP 1.12.5; the harness executes on `net8.0`. No installation or in-game checks have been performed. Gates 1 and 2 retain their in-game exit requirements. See [MILESTONE-1.md](MILESTONE-1.md) for exact evidence and limitations.

## Development iteration procedure

Use this cycle for each implementation iteration. Keep each code change focused, but batch compatible in-game checks into an occasional KSP session. A successful build or offline test is useful evidence; it is not evidence that the plugin loads or that KSP predicts the same trajectory.

1. **Agree on the question.** State the behaviour being added or investigated, its reason, and the change's boundaries. Check any scope change against [SCOPE.md](SCOPE.md).
2. **Choose the cheapest useful check.** Add offline numerical fixtures or harness checks for orbital maths, optimisation, and failure cases. Build against the intended KSP references. Specify an in-game test only when it answers a question that offline checks cannot, such as actual API behaviour, plugin loading, patch propagation, or node insertion.
3. **Write any in-game test before implementation.** When a KSP session is warranted, record the game version, required mods, test save/vessel and starting state, exact steps, expected result, pass/fail criteria, and evidence to capture. Group related checks into one session. Use a disposable or recoverable save when a test changes flight state.
4. **Implement and build.** Make the agreed change, report build errors, and produce a clearly identified DLL. Record the commit, build configuration, KSP/runtime target, DLL version or build ID, and SHA-256. Do not hand over a DLL that did not build successfully.
5. **Install for the planned KSP session.** When an in-game test is ready, confirm KSP has exited, preserve the installed working DLL for rollback, then replace only KerbalSlingshot's files. Record the installed path and verify that the installed DLL's SHA-256 matches the build artifact. Never overwrite another mod's files.
6. **Run and report.** Perform the planned checks with the identified build. Capture the observed outcome and requested evidence. Report build ID/hash, test case, expected versus observed behaviour, and pass/fail status. Distinguish failure from an inconclusive run, such as a different DLL being loaded or a missing prerequisite. Keep saves unchanged unless a test explicitly uses a disposable copy.
7. **Choose the next iteration.** Compare results with the criteria. Fix, simplify, or roll back as evidence supports, then define the next question and checks. Preserve the last known-good DLL until its replacement passes the relevant checks.

An iteration passes only when its pre-agreed observable criteria pass. A useful failure still provides the exact build and reproducible evidence. Avoid unrelated code changes; batching multiple related in-game checks is encouraged when it saves a KSP launch. Numerical harness results, successful builds, DLL-load checks, node predictions, and flown trajectory outcomes are separate evidence; report each at the level actually observed. Do not label a result KSP-validated until it has been checked in KSP.

## First implementation milestone: offline-first prototype

Begin with the useful trajectory work: build the numerical core, a repeatable offline harness, and deterministic fixtures for reachable cases, constrained misses, unsafe flybys, and invalid inputs. Implement the first bounded solver increment against those fixtures while establishing the KSP build target. This milestone requires no standalone KSP launch.

Schedule the first in-game session when the prototype has a meaningful end-to-end check: load the planner, read the active vessel and celestial data, calculate or validate a candidate, and inspect its trajectory or node as supported by the implementation. Combine the load/UI check with those trajectory checks. Use a recoverable test save and a written procedure with explicit expected results. If a narrower KSP-only uncertainty blocks progress, explain what it will resolve and bundle it with the next useful check where practical.

## Gate 1: KSP trajectory feasibility

Confirm the intended KSP version and build/runtime references. Investigate an isolated patch-propagation adapter, node coordinate conversion, SOI event handling, terrain safety data, and patch limits.

Exit evidence: offline fixtures cover a captured vessel state plus known burns without changing any live vessel plan. In a planned KSP session, confirm the predicted SOI events and periapsis against a created-node trajectory. Include a missing-encounter case and an incomplete-patch case in the appropriate offline or in-game checks. Record actual errors and runtime assumptions.

## Gate 2: One assist in a shared parent SOI

Build the detached numerical core and bounded seed/refinement search for a vessel already orbiting the shared parent. Use a deliberately constructed reachable case before arbitrary live saves. Validate full three-dimensional geometry, excess-speed continuity, safe flyby, assist escape, and destination periapsis.

Exit evidence: one reproducible positive case confirmed by KSP, a constrained no-result case, a too-low flyby case, and an impossible or unsupported input case. A solver returning a near miss does not pass this gate.

## Gate 3: Departure from a parking orbit

Add real departure SOI transitions and support the two parking-orbit route families in SCOPE.md: moon-assisted departure toward another planet, and planet-assisted travel toward a third planet. Use actual vessel inclination and eccentricity.

Exit evidence: at least one reproducible KSP-confirmed case per route family, including the full departure/assist/destination patch chain. If a family needs extra burns or cannot converge reliably, document the evidence and review its scope before calling it supported.

## Gate 4: Planner UI and safe node creation

Implement target selection, explicit units/defaults, search bounds, progress, cancellation, result diagnostics, and Create Node. Handle stale results, scene/vessel changes, conflicting future nodes, planning restrictions, and rollback of the newly created node on insertion failure.

Exit evidence: cancellation and recalculation leave the node plan unchanged; successful insertion adds exactly one node and matches the validated candidate. The game remains responsive during a measured bounded search.

## Gate 5: Flight validation and first release

Follow representative inserted nodes through assist SOI entry, periapsis, exit, and destination periapsis. Use a disposable test save or recoverable copy. Record planned versus flown events, residual burn error, and final altitude error. Do not describe numerical tests or KSP node predictions as flown validation.

Exit evidence: demonstrated route families, measured search duration and accuracy, documented limitations, selected project licence, installation/removal instructions, and a versioned package containing only redistributable mod files.

## Validation matrix

| Case | Required behaviour |
| --- | --- |
| Constructed reachable flyby | Finds a complete continuous route within configured tolerance |
| Parking orbit departures | Includes all required source SOI transitions |
| Inclined/eccentric orbit | Uses actual 3D vessel state |
| Excessive requested bending | Rejects an unsafe/impossible seed |
| Atmosphere/terrain intersection | Rejects candidate before node insertion |
| Missed destination or wrong event order | Reports no validated solution |
| Search budget exhausted | Says no solution found within bounds; retains search diagnostics |
| Cancel or scene/vessel change | Stops safely with no node mutation |
| Orbit/input/node-plan change | Invalidates stale result |
| Existing conflicting future nodes | Refuses insertion without deleting those nodes |
| Displayed patch chain too short | Reports/handles truncation; never treats it as confirmed success |
| Long burn or imperfect execution | Separates impulsive prediction from observed execution error |

## Current source layout

```text
src/KerbalSlingshot.Core/       detached numerical model and bounded local search
src/KerbalSlingshot.KSP/        compile-only API contract; no live planner yet
tests/KerbalSlingshot.Harness/  repeatable offline numerical/constraint checks
fixtures/                     frozen synthetic scenarios and provenance
docs/                         scope, design, evidence, dependency review
```

The solution and build/check scripts use the .NET 8 SDK. Core targets are `net48;net8.0`; the KSP contract uses `net48` and local non-copying references. Keep machine-specific KSP paths in ignored `local.props` or pass `KspManagedPath` to the scripts. Reference the user's game assemblies without committing them. Development-only framework reference packages and source research are recorded in [DEPENDENCIES.md](DEPENDENCIES.md).

These gates are project documentation, not scheduled reminders or entries in a personal task system. No calendar commitments or effort estimates are assigned.
