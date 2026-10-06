# Mod scope

Product contract clarified 6 October 2026: the pilot selects the assist body, destination body, and destination periapsis altitude. KerbalSlingshot itself searches from the active vessel's current orbit and creates the departure manoeuvre node for a validated result. The pilot is never required to design, import, or type a preliminary manoeuvre node.

The installed 0.2.1 prototype does not yet meet this contract. It refines a supplied estimate, has a reported native burn-frame import failure, does not validate candidates against KSP, and cannot create nodes. It is an interim engineering prototype, not a usable implementation of the requested workflow. See [Milestone 3](MILESTONE-3.md) and the next implementation gates in [DEVELOPMENT.md](DEVELOPMENT.md).

## Goal

From the active vessel's actual orbit, automatically find a bounded, feasible single-burn trajectory that enters the selected gravity-assist body's SOI, performs a safe unpowered flyby, exits that SOI unbound, then encounters the selected destination at the requested periapsis altitude. After the user reviews the fully KSP-validated candidate and chooses **Create Node**, add the departure manoeuvre node to the vessel.

The search may generate and refine any number of internal guesses. Those guesses are solver details, not user inputs or required setup. If automatic search has not found a candidate, the interface must explain its bounded failure; it must not ask the pilot to supply a manoeuvre as a way to make the planner work.

An assist exit means escape from that body's SOI, not necessarily escape from its parent body's SOI or the whole planetary system. Arrival is a periapsis encounter, not automatic capture.

## Initial supported route

The first complete route to implement and test is the screenshot scenario:

- The active vessel is coasting in Kerbin's SOI and outside the SOIs of Kerbin's child bodies.
- Mun is selected as the gravity-assist target.
- Minmus is selected as the intercept target.
- The destination periapsis altitude is selected in kilometres above Minmus's reference surface.
- The mod generates candidate departure burns from the captured vessel state, validates a complete Mun-to-Minmus route, and offers one departure node for explicit creation.

The same-parent sibling-body route family may support other bodies when validated. Parking-orbit departure, departure across the vessel's current reference-body SOI, nested target-body encounters, multiple assists, and planet-to-planet routes are outside this first complete route unless separately implemented and verified. Report unsupported geometry explicitly.

## Pilot inputs and actions

Required inputs are only:

| Input | Meaning |
| --- | --- |
| Gravity Assist Target | Celestial body to fly past without thrust |
| Intercept Target | Celestial body whose SOI and periapsis must be reached next |
| Intercept Target Distance | Periapsis altitude in km above the destination reference surface |

The mod derives initial guesses from the active vessel's current state and celestial ephemerides, explores departure times and burn directions within displayed/default search bounds, and refines candidates internally. It may expose optional departure-window, journey, delta-v, clearance, tolerance, and computation limits in Advanced settings. Safe defaults must allow a normal calculation to start without the pilot configuring algorithm internals. Any explicit safety assumption, such as a conservative terrain ceiling, must be explained in user language.

Convert altitude to periapsis radius using `radius = body.Radius + altitude`. Reject nonfinite values, identical targets, unsupported body hierarchies, unsafe periapsis requests, stale snapshots, and routes outside configured bounds. Do not substitute a user-authored seed burn when automatic search fails.

## Candidate, validation, and node contract

A proposed candidate includes departure UT, total delta-v and native node components, assist SOI entry/periapsis/exit, destination SOI entry/periapsis, achieved altitude and error, arrival speed, journey duration, and relevant bounds.

An offline candidate is not a node-ready solution. Before **Create Node** becomes available, KSP must confirm the same ordered patch/body sequence and destination periapsis within tolerance, the departure vector must round-trip through KSP's native manoeuvre frame, the result must still match the current vessel and node plan, and no unsafe conflict may exist. Preserve existing nodes. Never silently replace or delete them; block creation with a clear explanation when the current plan prevents trustworthy validation.

Creating a node adds exactly one departure node after an explicit user action. It never executes the burn, changes time warp, or mutates the save beyond the node KSP itself records. If node creation or the subsequent KSP prediction check fails, remove only the node created by this operation and report the failure.

Outcomes must distinguish a KSP-validated solution, offline candidate awaiting validation, invalid request, unsupported route, no solution found within bounds, cancelled search, stale state, validation disagreement, and node-creation failure. A timeout or failed seed set is not proof that no physical route exists. Never claim global optimality.

## Acceptance criteria for the first usable route

1. Dave can begin from a vessel already in Kerbin's SOI with no pre-existing manoeuvre node and no manually supplied burn estimate.
2. Selecting Mun, Minmus, and a destination periapsis altitude is sufficient to start automatic candidate search; optional safety settings are explained and have usable defaults.
3. The search itself generates and refines multiple departure/geometry candidates within finite compute and trajectory bounds, remains cancellable, and reports its bounds and honest failure state.
4. Any accepted trajectory enters Mun's SOI, clears terrain/atmosphere by the configured margin, reaches Mun periapsis on an unpowered hyperbola, exits Mun's SOI unbound, then enters Minmus's SOI and reaches the requested periapsis within tolerance.
5. Unexpected encounters, wrong event order, incomplete patch chains, stale vessel states, and coordinate-frame disagreement prevent node creation.
6. KSP's predicted patch sequence and periapsis agree with the candidate before the node is offered for creation.
7. Pressing **Create Node** adds exactly one node with the validated UT and delta-v. Existing conflicting nodes remain untouched and cause a clear refusal.
8. A recoverable-save in-game test starts with no preparatory node and demonstrates target selection, automatic calculation, validation, and node insertion. Record predicted versus KSP patch events and errors.
9. Offline fixtures cover generated seeds, reachable route refinement, constrained search exhaustion, unsafe flyby, invalid input, cancellation, freshness, and native-frame conversion. Offline tests do not count as KSP or flight validation.
10. Publish measured accuracy, search duration, and supported route limits. Do not call a route supported based only on synthetic fixtures or successful compilation.

## Excluded from the initial route

- Pilot-created/imported/manual starting burns as a required workflow.
- More than one assist, powered flybys, correction burns, capture, circularisation, rendezvous, and landing.
- Automatic burn execution, staging, or time warp.
- Spacecraft, stations, asteroids, or comets as targets.
- Aerobraking, atmosphere traversal, impact planning, N-body dynamics, Principia, and KSP 2.
- Guaranteed compatibility with planet packs or rescaled systems before validation.

If a desired route needs additional burns or cannot be searched reliably, state that limitation. Do not ask the pilot to create an approximate node to compensate for missing automatic search.
