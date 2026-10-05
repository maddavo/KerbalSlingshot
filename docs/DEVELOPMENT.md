# Development stages

Current stage: **documentation baseline**. No source, builds, installation, or flight checks have been completed for KerbalSlingshot.

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
