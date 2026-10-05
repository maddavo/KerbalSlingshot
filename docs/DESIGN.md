# Solver and integration design

This is a proposed architecture, not an implemented or verified algorithm.

## Components

| Component | Responsibility |
| --- | --- |
| Planner UI | Targets, altitude, bounds, progress, result review, Create Node |
| KSP state adapter | Snapshot vessel orbit, UT, celestial hierarchy, radii, SOIs, gravitational parameters, atmosphere/terrain data, and existing nodes |
| Numerical core | Conic propagation, seed generation, flyby constraints, bounded optimisation, and candidate ranking |
| KSP trajectory validator | Independently check candidate body sequence and periapsis through KSP patch calculations |
| Node controller | Recheck freshness, translate delta-v into node coordinates, add the accepted node, and verify the inserted plan |

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

1. **Snapshot and validate.** Check input units, supported body hierarchy, future-node conflicts, vessel planning capability, and clearance bounds. Capture a stable vessel/body state fingerprint.
2. **Generate seeds.** Sample departure and encounter epochs within finite bounds. Use Lambert or equivalent conic transfers for approximate candidate geometry. Filter impossible excess-speed/turn-angle combinations. Include both viable flyby orientations and transfer branches rather than assuming a coplanar orbit.
3. **Resolve departure.** Convert approximate departure geometry into a burn from the vessel's actual orbit. A planet parking orbit requires a departure conic and finite SOI exit; moon-assisted escape must retain the moon's position within the planet's SOI.
4. **Refine the burn.** Optimise departure UT plus three burn-vector components. Every evaluation propagates the complete supported patch sequence. Encounter epochs and flyby-plane parameters may guide a seed, but the final epochs must emerge from this propagation.
5. **Enforce constraints.** Require the selected assist entry, safe periapsis, unbound exit, and later destination entry/periapsis. Reject unexpected encounters, collision/atmosphere paths, nonfinite states, exceeded bounds, or truncated propagation.
6. **Validate in KSP.** Evaluate promising candidates through isolated KSP patch calculations and compare sequence, times, and periapsis. Refine or reject disagreement rather than passing it off as a valid solution.
7. **Rank and present.** Return validated candidates ordered by departure delta-v and journey duration. Record the search budget and residuals.
8. **Insert on request.** Recheck the fingerprint and burn time, create one node, then compare its predicted trajectory with the accepted result. If insertion fails or disagrees, remove only the node created by this operation and report the failure.

Encounter boundaries make the objective discontinuous: most arbitrary burns miss the assist entirely. Use staged objectives (assist approach, safe passage/exit, destination approach, destination periapsis) and multiple seeds. Penalties can guide exploration, but only explicit constraints determine validity. Start with a bounded derivative-free optimiser; algorithm and tuning choices remain a feasibility decision.

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

Search and preview must not create temporary live manoeuvre nodes. Existing future nodes that would change the trajectory must block insertion in the initial version; do not delete or replace them automatically. Preserve unrelated past nodes.

A result fingerprint should include vessel identity, current conic/epoch, reference body, relevant celestial data, target selections, settings, and node-plan state. Ordinary UT advancement does not alone invalidate an unchanged conic, but a passed departure time does. Burns, SOI changes, vessel switches, node edits, docking, and changed inputs require a fresh search.

The accepted node's delta-v is an inertial velocity impulse converted to the game's local manoeuvre frame at the burn epoch. Demonstrate this conversion with an independent propagation comparison before flight use. User execution, finite burn duration, and residual burn errors can alter the eventual encounter; prediction accuracy and execution accuracy must be reported separately.

## Diagnostics

Record snapshot identity, celestial-system identity, input units/bounds, search seed, evaluation count, solver elapsed time, rejection counts, candidate delta-v, event states, periapsis residual, and KSP validation differences. Diagnostics should make a failed route reproducible without including saves or proprietary game assemblies in the repository.

No performance or numerical-accuracy claims are justified until measured. Numerical tolerances need explicit units and should be chosen from end-to-end fixtures, not just mathematical residuals in an approximate seed model.
