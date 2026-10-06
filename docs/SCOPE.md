# Mod scope

Scope baseline: 5 October 2026. These are the agreed product boundaries. Milestone 1 implements a detached sibling-body prototype; live integration, other route families, and flight validation remain outstanding. See [milestone evidence](MILESTONE-1.md).

## Goal

Given the active vessel's actual orbit, a selected gravity-assist body, a selected destination body, and a requested destination periapsis altitude, find and create one departure manoeuvre node whose natural trajectory performs the selected flyby and reaches that destination.

The assist must be an unpowered passage through the body's SOI with positive incoming and outgoing hyperbolic excess energy. The destination must be encountered after the assist exit. Escaping the assist body's SOI does **not** require escaping its parent body or the entire planetary system.

## First-version boundaries

| Area | Proposed scope |
| --- | --- |
| Game | KSP 1, initially version 1.12.5 |
| Operation | Active vessel in flight/map view with manoeuvre planning available |
| Targets | Two distinct celestial bodies with finite SOIs; the central star is not a selectable encounter target |
| Departure | Actual vessel state on a supported current conic; no assumed ideal parking orbit |
| Burns | One impulsive departure burn; zero powered flyby or arrival burns |
| Assist | Exactly one selected, unpowered flyby with safe clearance |
| Arrival | Destination SOI entry and a subsequent periapsis at the requested altitude |
| Search | Finite time and delta-v bounds, progress, cancellation, and explicit failure reasons |
| Node | Review first, then add one node only from a validated result |
| Integration | Standalone plugin; no required MechJeb integration |
| Celestial data | Read from the running game; stock system is the initial validation baseline |

The first version aims to cover these route families, delivered in stages:

- A vessel already in the shared parent SOI, encountering two bodies orbiting that parent. This isolates the flyby problem, for example a vessel in Kerbin's SOI using Mun before encountering Minmus, if a reachable geometry is found.
- Departure from a planet's parking orbit, a flyby of one of its moons, departure from the planet's SOI, and encounter with another planet. Kerbin -> Mun assist -> Duna encounter is a candidate feasibility case, not a promised solution at any date.
- Departure from a planet's parking orbit, followed by a different planet's flyby and a third planet's encounter. The initial departure SOI transition must be included.

These families are part of the intended scope, but support for each requires the development gates in [DEVELOPMENT.md](DEVELOPMENT.md). Arbitrary nested moon/planet routes, repeated encounters, and resonance tours are outside the initial version. Unsupported body hierarchies must be reported before starting a search.

## Input contract

| Input | Meaning |
| --- | --- |
| Gravity Assist Target | Celestial body to fly past without thrust |
| Intercept Target | Celestial body whose SOI and periapsis must be reached next |
| Intercept Target Distance | **Periapsis altitude in km above the destination's reference surface**, not separation from its centre or another vessel |
| Departure window | Earliest/latest allowed departure UT; defaults derived from now and the relevant orbital periods |
| Maximum journey duration | Latest permitted destination periapsis relative to departure |
| Maximum departure delta-v | Upper bound on the single burn; not a guarantee the vessel has enough fuel |
| Minimum assist altitude | Lower bound above terrain and any atmosphere, with a configurable clearance margin |
| Periapsis tolerance | Permitted absolute error in the requested destination altitude |
| Search budget | Maximum computation time or candidate evaluations |

Only the first three inputs need prominent controls. Advanced settings must display their effective values and units; their numerical defaults will be chosen from measured feasibility results. Store time internally as game universal time in seconds. Display time using the game's calendar, not Windows wall-clock time.

Convert requested altitude to internal periapsis radius with `radius = body.Radius + altitude`. Reject nonfinite values, invalid time bounds, identical targets, an assist that is the vessel's current reference body, and destination altitudes outside the SOI or below the configured safe clearance. Atmosphere entry and impact trajectories are excluded from the initial version. Terrain safety requires an explicit conservative bound or terrain evaluation; the body's reference radius alone is insufficient.

## User-visible result

A successful candidate must include departure UT, prograde/normal/radial delta-v, total delta-v, assist SOI entry/periapsis/exit times, assist altitude and clearance, destination SOI entry/periapsis times, achieved destination altitude and error, destination periapsis speed, and total journey duration. Include the search bounds and whether the result has passed KSP trajectory validation.

Rank feasible candidates by departure delta-v, with shorter journey duration breaking ties. Do not claim a global optimum. A speed increase is not required: a useful assist can reduce speed or change direction/inclination.

Distinct outcomes must include: validated solution, invalid request, unsupported route, no solution found within bounds, cancelled, stale vessel state, and KSP validation failure. An approximate near miss may be shown for diagnosis but must not enable Create Node. A search timeout is not proof of physical impossibility.

## Acceptance criteria for the first release

1. The candidate starts from the active vessel's captured orbit and contains only one planned velocity impulse.
2. Propagation enters the selected assist SOI, passes a safe periapsis, and exits it on an unbound conic.
3. Propagation then enters the selected destination SOI and reaches its periapsis within the requested tolerance and journey limit.
4. All departure, flyby, safety, and time bounds hold; unexpected intermediate encounters invalidate the supported sequence.
5. KSP patch propagation confirms the same body sequence and final periapsis within the configured tolerance before the candidate is marked valid.
6. Creating a node preserves unrelated nodes and refuses incompatible existing future nodes; recalculation and cancellation do not mutate the vessel's plan.
7. Vessel changes, orbit changes, target changes, or a passed burn time invalidate the result before insertion.
8. Each claimed supported route family has at least one reproducible positive fixture and one deliberately invalid or constrained failure fixture.
9. Flight evidence follows a representative created node through assist exit and destination encounter, recording execution error separately from prediction error.
10. Search remains cancellable and does not freeze the game; measured timing and accuracy limits are documented with the release.

## Excluded from the first version

- Multiple assists, automatic assist-body selection, and resonant return tours.
- Powered flybys, deep-space correction nodes, capture, circularisation, rendezvous, and landing.
- Spacecraft, stations, asteroids, or comets as intercept targets.
- Autopilot, automatic burn execution, staging, or automatic time warp.
- Finite-thrust trajectory optimisation and guaranteed accuracy for long burns.
- Aerobraking, aerogravity assists, atmosphere traversal, or impact planning.
- N-body dynamics, Principia support, and KSP 2.
- Guaranteed compatibility with planet packs or rescaled systems before validation.

## Decisions still requiring evidence

- Numerical search bounds, tolerances, clearance margins, and performance targets.
- Whether all three route families can converge robustly with a single burn.
- Exact KSP API behaviour, isolated validation strategy, and patch-depth limits.
- Build framework and packaging details for the installed KSP/Unity runtime.
- Project licence and any permitted third-party solver reuse.

If a route needs a second burn to be practical, report that limitation and review the scope explicitly. Do not silently add correction burns to a one-node result.
