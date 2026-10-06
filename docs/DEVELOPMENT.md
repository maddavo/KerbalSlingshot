# Development stages

Current stage: **documentation baseline**. No source, builds, installation, or flight checks have been completed for KerbalSlingshot.

## DLL development iteration procedure

Use this cycle for each in-game development iteration. Keep the iteration small enough that its test result can be attributed to a specific change.

1. **Agree on the question.** State the behaviour being added or investigated, its reason, and the change's boundaries. Check any scope change against [SCOPE.md](SCOPE.md).
2. **Write the test before implementation.** Record the KSP version, required mods, test save/vessel and starting state, exact steps, expected result, pass/fail criteria, and useful evidence to capture if it fails. Use a disposable or recoverable save when a test changes flight state.
3. **Implement and build.** Make the agreed change, report build errors, and produce a clearly identified DLL. Record the commit, build configuration, KSP/runtime target, DLL version or build ID, and SHA-256. Do not hand over a DLL that did not build successfully.
4. **Install while KSP is closed.** Confirm the game has exited, preserve the installed working DLL for rollback, then replace only KerbalSlingshot's files. Record the installed path and verify that the installed DLL's SHA-256 matches the build artifact. Never overwrite another mod's files.
5. **Run the written test.** Follow the agreed steps and criteria with the identified build. Capture the observed outcome and any requested screenshot or relevant `KSP.log` excerpt. Keep the save unchanged unless the test explicitly uses its disposable copy.
6. **Report the evidence.** Return the build ID/hash, test case, expected and observed behaviour, pass/fail result, and evidence. Distinguish a test failure from an inconclusive run, such as a different DLL being loaded or a missing prerequisite.
7. **Choose the next iteration.** Compare the result with the criteria. Fix, simplify, or roll back as evidence supports, then define the next question and test. Preserve the last known-good DLL until its replacement passes the relevant smoke test.

An iteration passes only when its pre-agreed observable criteria pass. A useful failure still provides the exact build and reproducible evidence. Do not bundle unrelated changes into a test intended to isolate one behaviour. Numerical harness results, successful builds, DLL-load checks, node predictions, and flown trajectory outcomes are separate evidence; report each at the level actually observed.

## First in-game iteration: load and observe

Before attempting trajectory solving, build a minimal plugin and check that it loads in the agreed KSP version, opens its planner, and displays the active vessel and selected celestial bodies. It must not change the vessel, create a manoeuvre node, or mutate the save. This smoke test validates the game integration boundary before trajectory calculations become a second possible source of failure.

Write the concrete setup and pass criteria after the initial plugin entry point and UI are known. At minimum, pass means the expected DLL is identified in the log, the planner opens, vessel and body names match the current game state, and no node or save-state change occurs. The exact log signature and menu access steps must be specified by that build's test procedure, not guessed in advance.

## Gate 1: KSP trajectory feasibility

Confirm the intended KSP version and build/runtime references. Investigate an isolated patch-propagation adapter, node coordinate conversion, SOI event handling, terrain safety data, and patch limits.

Exit evidence: a captured vessel state plus a known burn can be propagated without changing the live vessel plan; the predicted SOI events and periapsis agree with KSP's created-node trajectory. Include a missing-encounter case and an incomplete-patch case. Record actual errors and runtime assumptions.

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

## Proposed source layout once implementation begins

```text
src/KerbalSlingshot.Core/       detached numerical model and search
src/KerbalSlingshot.KSP/        game adapter, validator, UI, node controller
tests/                        numerical and lifecycle validation
fixtures/                     reproducible numeric scenarios
docs/                         scope, design, evidence, release instructions
```

No build toolchain is committed yet. Verify the game's runtime before choosing the C# target framework and reference configuration. Keep machine-specific KSP paths in ignored local configuration and reference the user's game assemblies without committing them. Record third-party licences before adding solver code or dependencies.

These gates are project documentation, not scheduled reminders or entries in a personal task system. No calendar commitments or effort estimates are assigned.
