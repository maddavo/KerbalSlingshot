# Research references

Reviewed on 5 October 2026. References support feasibility research; they do not establish that KerbalSlingshot exists, works, or outperforms these tools.

## KSP integration precedent

- [MechJeb OrbitalManeuverCalculator](https://github.com/MuMech/MechJeb2/blob/dev/MechJeb2/OrbitalManeuverCalculator.cs): contains `PatchedConicInterceptBody`, a patch-propagation helper, and `OptimizeEjectionToTarget`, a departure optimiser. The helper checks successive patch reference bodies; comments highlight missing-patch hazards. This is evidence for investigating KSP patch APIs, not a ready-made one-assist solver.
- [MechJeb repository](https://github.com/MuMech/MechJeb2): possible integration and numerical reference. The initial standalone scope does not require a MechJeb installation.

## Gravity-assist planning precedents

- [KSP Trajectory Optimization Tool](https://github.com/Arrowstar/ksptot): a standalone toolkit with a Multi-Flyby Maneuver Sequencer for exploring gravity-assist missions.
- [KSP Multiple Gravity Assist Planner](https://github.com/nmisyats/KSP-MGA-Planner): an online KSP trajectory planner supporting multiple gravity assists.

These projects demonstrate existing work in the problem domain. Their functionality does not prove that an arbitrary request can be satisfied by one burn from a particular live vessel orbit. KerbalSlingshot's proposed focus is in-game planning from the active vessel, with one selected assist, a destination periapsis constraint, and direct creation of one validated node.

## Evidence boundaries

- Public source and the local MechJeb source were inspected for trajectory-propagation precedent.
- The new KerbalSlingshot repository was confirmed empty before this documentation baseline.
- Milestone 1 now implements and checks detached synthetic trajectories and local bounded refinement. See [MILESTONE-1.md](MILESTONE-1.md) for the actual results and local source/API investigation.
- KSP API calls are compiled but have not been executed in game; no trajectory was flown. Runtime compatibility, stock-system route coverage, and live validation remain development questions.
- Review the applicable licences before reusing any implementation. No external solver code was copied for this documentation baseline.
