# Development stages

Current stage: **replace the estimate-driven prototype with automatic route search and node creation**. The 0.2.1 build is installed for testing, but its workflow is incomplete: it requires a supplied estimate, the screenshot shows a native burn-frame import failure, it does not validate a candidate against KSP, and it cannot create a node. Dave's requested route starts from his current Kerbin orbit with Mun assist and Minmus destination and no preparatory node. The next development work must make automatic candidate generation, full KSP validation, and explicit node creation work before asking him to repeat trajectory setup. See [SCOPE.md](SCOPE.md), [MILESTONE-3.md](MILESTONE-3.md), and [KSP-TEST.md](KSP-TEST.md).

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

## Current implementation goal: automatic Mun-to-Minmus node

Keep building and checking offline between game sessions, but solve the actual missing product capability. Start from the live active-vessel state, generate candidate departure times and burn geometries internally, and search the complete Kerbin-SOI -> Mun unpowered flyby -> Minmus periapsis route. A caller-supplied estimate, imported node, or hand-entered burn must not be necessary for the normal pilot flow. Preserve and expand the deterministic harness for candidate generation, route constraints, no-solution outcomes, cancellation, and regression cases.

Use the installed DLL and screenshot as evidence of two specific integration issues: the workflow still exposes estimate import and the native burn-frame check fails. Diagnose/fix these in development; do not tell Dave to plan another node or troubleshoot the frame conversion manually. Defer another KSP session until a recovered full candidate can be checked through KSP's patch sequence and the mod can safely add its departure node. Combine loading, calculation, validation, and insertion into one recoverable-save session with explicit pass criteria.

## Gate 1: KSP trajectory feasibility

Confirm the KSP 1.12.5 build/runtime references. Resolve snapshot and native-frame conventions, KSP patch propagation, SOI event handling, terrain safety data, and patch limits using offline inspection first where possible.

Exit evidence: a live snapshot produces the same state and burn frame expected by KSP; detached propagation and KSP patch events agree for a controlled case. Include missing-encounter and incomplete-patch cases. A screenshot showing an import-frame failure is an open defect, not a passed check.

## Gate 2: One assist in a shared parent SOI

Build automatic seed generation and bounded search from the active vessel state for a vessel already orbiting the shared parent. Exercise the actual intended case: vessel in Kerbin's SOI, with no existing manoeuvre node; Mun assist; Minmus destination. Search departure epochs and internally generated transfer/flyby geometries. Validate full three-dimensional geometry, excess-speed continuity, safe flyby, assist escape, destination entry, and requested periapsis.

Exit evidence: the automatic search finds at least one reproducible route from the no-preparatory-node fixture; also cover constrained no-result, too-low flyby, invalid/unsupported inputs, cancellation, and search-bound exhaustion. Confirm a candidate against KSP before it is called validated. A solver returning a near miss does not pass this gate.

## Deferred work: departure from a parking orbit

Do not start this route expansion until the automatic Mun-to-Minmus route in the shared Kerbin parent SOI passes its KSP acceptance test. Parking-orbit departure toward another planet and planet-assisted interplanetary routes are later scope, not prerequisites for the user's requested route.

If later authorised and implemented, require a reproducible KSP-confirmed case for each added route family, including its full departure/assist/destination patch chain. Review feasibility evidence before claiming support.

## Gate 4: Planner UI and safe node creation

Keep the pilot flow to assist target, intercept target, and requested periapsis, with safe defaults and optional advanced constraints. The planner itself generates candidate seeds; remove node-import and manual-seed requirements. Show progress, cancellation, result events and validation state. Enable **Create Node** only for a complete KSP-validated candidate; handle stale results, scene/vessel changes, conflicting future nodes, planning restrictions, and rollback of only the newly created node on insertion failure.

Exit evidence: cancellation and recalculation leave the node plan unchanged; successful insertion adds exactly one node and matches the validated candidate. The game remains responsive during a measured bounded search.

## Gate 5: Flight validation and first release

Follow representative inserted nodes through assist SOI entry, periapsis, exit, and destination periapsis. Use a disposable test save or recoverable copy. Record planned versus flown events, residual burn error, and final altitude error. Do not describe numerical tests or KSP node predictions as flown validation.

Exit evidence: demonstrated route families, measured search duration and accuracy, documented limitations, selected project licence, installation/removal instructions, and a versioned package containing only redistributable mod files.

## Validation matrix

| Case | Required behaviour |
| --- | --- |
| No-node live starting state | Generates its own candidate starts; pilot does not import, create, or type a preliminary node |
| Constructed reachable flyby | Finds a complete continuous route within configured tolerance |
| Kerbin / Mun / Minmus live route | Finds and KSP-validates a complete route from the active orbit |
| Parking orbit departures | Includes all required source SOI transitions |
| Inclined/eccentric orbit | Uses actual 3D vessel state |
| Excessive requested bending | Rejects an unsafe/impossible seed |
| Atmosphere/terrain intersection | Rejects candidate before node insertion |
| Missed destination or wrong event order | Reports no validated solution |
| Search budget exhausted | Says no solution found within bounds; retains search diagnostics |
| Cancel or scene/vessel change | Stops safely with no node mutation |
| Orbit/input/node-plan change | Invalidates stale result |
| Existing conflicting future nodes | Refuses insertion without deleting those nodes, while still allowing search from the captured current orbit |
| Displayed patch chain too short | Reports/handles truncation; never treats it as confirmed success |
| Long burn or imperfect execution | Separates impulsive prediction from observed execution error |

## Current source layout

```text
src/KerbalSlingshot.Core/       detached numerical model and bounded local search
src/KerbalSlingshot.KSP/        flight addon, snapshot adapter, planner UI, read-only comparison
tests/KerbalSlingshot.Harness/  repeatable offline numerical/constraint checks
fixtures/                     frozen synthetic scenarios and provenance
docs/                         scope, design, evidence, dependency review
```

The solution and build/check/package scripts use the .NET 8 SDK. Core targets are `net48;net8.0`; the actual flight plugin uses `net48` and local non-copying references. `check.ps1` also inspects addon/reference metadata without executing game assemblies. `package.ps1` requires a clean commit, verifies the offline checks, and copies only the two authored DLLs to a local test ZIP with build/hash evidence. Keep machine-specific KSP paths in ignored `local.props` or pass `KspManagedPath`. Development-only framework reference packages and source research are recorded in [DEPENDENCIES.md](DEPENDENCIES.md).

These gates are project documentation, not scheduled reminders or entries in a personal task system. No calendar commitments or effort estimates are assigned.
