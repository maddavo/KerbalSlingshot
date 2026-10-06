# Solver and integration design

This is the product architecture required by the clarified user workflow in [SCOPE.md](SCOPE.md). The 0.2.1 prototype has live snapshots and a local solver, but still requires a pilot-supplied estimate, has a reported native burn-frame import failure, lacks automatic seed generation and full candidate validation, and cannot create a node. Those are implementation gaps, not pilot setup requirements. Its offline-feasible results retain no KSP-validation authority.

The 0.3.0 test implementation now generates its own Lambert/impact-plane seeds, numerically refines the complete route, validates temporary KSP patches/native fixed frames, and offers guarded explicit insertion with post-insertion verification/rollback. See [MILESTONE-4.md](MILESTONE-4.md) for implemented details and evidence; its live KSP acceptance remains pending. This does not loosen the contract below or expand route coverage.

## Components

| Component | Responsibility |
| --- | --- |
| Planner UI | Assist target, destination target, periapsis altitude, understandable safety choices, automatic calculation, result review, and explicit Create Node |
| KSP state adapter | Snapshot the live vessel orbit, UT, celestial hierarchy, radii, SOIs, gravitational parameters, atmosphere/terrain data, and existing node plan |
| Seed generator | Create and diversify internal departure-time and burn-geometry guesses from the live snapshot; never require a pilot-created seed node |
| Numerical core | Conic propagation, flyby constraints, bounded refinement, and candidate ranking |
| KSP trajectory validator | Independently check the complete candidate body sequence, native burn frame, safety, and periapsis through KSP patch calculations |
| Node controller | Recheck freshness/conflicts, add exactly one explicitly requested node, and verify the resulting KSP patch plan |

The numerical core should operate on immutable numeric snapshots without Unity objects. Capture and interact with KSP/Unity state on the main thread. Only detached numerical work may run in a background worker; KSP propagation must run on the main thread in bounded batches if needed. Cancellation and a computation budget apply to every stage.

## Physical constraints

An unpowered flyby changes the direction of velocity relative to the assist body. In the ideal two-body approximation, incoming and outgoing hyperbolic excess speed magnitudes are equal. Adding the body's parent-frame velocity can then produce a different parent-frame speed and direction.

For an ideal hyperbola with gravitational parameter `mu`, periapsis radius `rp`, and hyperbolic excess speed `vInf`:

```text
eccentricity = 1 + rp * vInf^2 / mu
turnAngle = 2 * asin(1 / eccentricity)
```

These relations can constrain initial guesses. They do not validate finite-SOI KSP trajectories. Preserve the actual entry/exit positions and velocities when propagating across SOI boundaries.

A pair of Lambert transfers through the assist body's centre is only a seed: independently chosen arrival and departure velocities may require an impossible turn angle, unequal excess speeds, or an unsafe flyby. The solver must enforce a continuous, unpowered passage and the final destination periapsis.

## Search pipeline

1. **Snapshot and validate.** Check the three pilot inputs, supported body hierarchy, vessel planning capability, existing node plan, and clearance bounds. Capture a stable vessel/body state fingerprint.
2. **Generate internal seeds.** Automatically sample departure epochs and generate transfer/flyby geometry from the captured vessel state and target ephemerides. Use Lambert or equivalent conic transfers only to suggest initial candidates; filter impossible excess-speed/turn-angle combinations and vary flyby orientations/branches. A seed is never presented as a result and never requested from the pilot.
3. **Resolve departure.** Convert approximate departure geometry into a burn from the vessel's actual orbit. A planet parking orbit requires a departure conic and finite SOI exit; moon-assisted escape must retain the moon's position within the planet's SOI.
4. **Refine the burn.** Optimise departure UT plus three burn-vector components. Every evaluation propagates the complete supported patch sequence. Encounter epochs and flyby-plane parameters may guide a seed, but the final epochs must emerge from this propagation.
5. **Enforce constraints.** Require the selected assist entry, safe periapsis, unbound exit, and later destination entry/periapsis. Reject unexpected encounters, collision/atmosphere paths, nonfinite states, exceeded bounds, or truncated propagation.
6. **Validate in KSP.** Evaluate promising candidates through isolated KSP patch calculations and compare sequence, times, and periapsis. Refine or reject disagreement rather than passing it off as a valid solution.
7. **Rank and present.** Return validated candidates ordered by departure delta-v and journey duration. Record the search budget and residuals.
8. **Validate, then insert on request.** Require KSP agreement on full event order, periapsis, safety bounds, and native-frame delta-v before enabling Create Node. Recheck the fingerprint and node plan. Add exactly one node after the user action, compare its resulting prediction with the accepted result, and on insertion failure remove only the node created by this operation.

Encounter boundaries make the objective discontinuous: most arbitrary burns miss the assist entirely. Use staged objectives (assist approach, safe passage/exit, destination approach, destination periapsis) and multiple seeds. Penalties can guide exploration, but only explicit constraints determine validity. Start with a bounded derivative-free optimiser; algorithm and tuning choices remain a feasibility decision.

The current increment uses deterministic coordinate pattern search from caller-supplied approximate burns, with finite evaluation and propagation-step budgets. This is an implementation limitation, not the intended user interaction. The next solver increment must automatically generate and diversify seeds from the live vessel state, then retain the existing full finite-SOI constraints. `OfflineFeasible` means only detached constraints passed; it is not `KspValidated` and cannot justify node creation. The harness tests synthetic known/refined routes against independent RK4 propagation. The universal-variable conic model and finite event scanner remain subject to the coverage limits in the milestone report.

There is no guarantee that four departure variables can satisfy every chosen route and safety constraint. Search bounds, launch geometry, and available flyby bending can leave no feasible solution. The planner must expose this result honestly.

## KSP patch integration

Public MechJeb source demonstrates initial-state shooting through `PatchedConics.CalculatePatch`, including the need to check that the returned orbit really references the target body. It also contains cautions around missing next patches. This supports investigating the API, but does not establish a complete two-encounter solver; see [REFERENCES.md](REFERENCES.md).

Resolve these details in the first technical gate:

- Position/velocity coordinate conventions, axis ordering, epoch, and manoeuvre-node frame conversion.
- How to construct temporary orbit/patch state without modifying the vessel's live solver or node list.
- SOI event ordering, patch end conditions, and finite propagation horizons.
- Whether required encounters are hidden by the game's displayed patch limit; distinguish display truncation from a trajectory miss.
- How terrain clearance can be bounded conservatively at flyby and destination periapsis.
- Thread safety and cleanup of temporary orbit objects.

Do not assume copied KSP `Orbit` objects, pooled objects, or live solver data remain valid after reuse. Export scalar state for diagnostics. Cap patch count as well as elapsed journey time to avoid loops or repeated unintended encounters.

## Node and result lifecycle

Search and preview must not create temporary live manoeuvre nodes. The pilot does not need a future node as a solver seed. Existing future nodes that would change the trajectory must block insertion in the initial version; do not delete or replace them automatically. Preserve unrelated nodes and explain the conflict.

A result fingerprint should include vessel identity, current conic/epoch, reference body, relevant celestial data, target selections, settings, and node-plan state. Ordinary UT advancement does not alone invalidate an unchanged conic, but a passed departure time does. Burns, SOI changes, vessel switches, node edits, docking, and changed inputs require a fresh search.

The accepted node's delta-v is an inertial velocity impulse converted to the game's local manoeuvre frame at the burn epoch. Demonstrate this conversion with an independent propagation comparison before flight use. User execution, finite burn duration, and residual burn errors can alter the eventual encounter; prediction accuracy and execution accuracy must be reported separately.

## Diagnostics

Record snapshot identity, celestial-system identity, input units/bounds, search seed, evaluation count, solver elapsed time, rejection counts, candidate delta-v, event states, periapsis residual, and KSP validation differences. Diagnostics should make a failed route reproducible without including saves or proprietary game assemblies in the repository.

No performance or numerical-accuracy claims are justified until measured. Numerical tolerances need explicit units and should be chosen from end-to-end fixtures, not just mathematical residuals in an approximate seed model.
