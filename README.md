# KerbalSlingshot

A proposed gravity-assist manoeuvre planner for **Kerbal Space Program 1**.

Choose a **Gravity Assist Target**, an **Intercept Target**, and an **Intercept Target Distance**. KerbalSlingshot will search for one departure manoeuvre that sends the active vessel through the assist body's sphere of influence (SOI), leaves it on an escape trajectory, and subsequently encounters the destination at the requested periapsis altitude.

**Status: milestone 1 offline prototype implemented.** A detached numerical core, bounded local search, five deterministic fixtures, and a repeatable harness are available. The KSP API-contract library builds for .NET Framework 4.8 against KSP 1.12.5. There is no live planner, downloadable release, or in-game trajectory validation yet.

## Intended flight sequence

```text
Current vessel orbit
    -> departure manoeuvre node
    -> assist body SOI entry
    -> unpowered flyby at a safe periapsis
    -> assist body SOI exit
    -> destination SOI entry
    -> destination periapsis at the requested altitude
```

The node describes the departure burn. Gravity and the encounter geometry produce the later trajectory; there is no planned burn at the assist body or destination. Arrival is an encounter, not automatic orbital capture.

## Intended use

1. Open the planner for the active vessel in flight or map view.
2. Select the assist body and destination body.
3. Enter the destination periapsis altitude in kilometres above its reference surface.
4. Calculate using the displayed departure window, journey limit, delta-v limit, and assist safety clearance. Change these bounds if needed.
5. Review a validated candidate's departure time, delta-v, flyby altitude, arrival time, arrival speed, and achieved periapsis.
6. Choose **Create Node** to add the departure manoeuvre to the vessel.

These controls describe the planned product; they are not currently implemented. The three main selections express the goal, while bounded search settings make the calculation practical. Some requests will have no reachable solution within those bounds.

## Build and offline checks

With the .NET 8 SDK installed, run from the repository root:

```powershell
./check.ps1 -OfflineOnly
```

To also build the KSP contract, provide your local `KSP_x64_Data/Managed` directory:

```powershell
./check.ps1 -KspManagedPath 'C:/path/to/Kerbal Space Program/KSP_x64_Data/Managed'
```

The core targets `net48` and `net8.0`; the harness runs on .NET 8, and the KSP contract targets `net48`. Game assemblies stay outside the repository and are not copied to build outputs. These commands do not launch KSP or install a DLL. See [milestone evidence and limitations](docs/MILESTONE-1.md).

## Documentation

- [Scope and requirements](docs/SCOPE.md): supported routes, inputs, exclusions, and acceptance criteria.
- [Solver and integration design](docs/DESIGN.md): trajectory search, physical constraints, KSP validation, and node lifecycle.
- [Development stages](docs/DEVELOPMENT.md): feasibility gates and evidence required before release.
- [Research references](docs/REFERENCES.md): primary sources and limits of current evidence.
- [Milestone 1 evidence](docs/MILESTONE-1.md): implemented behaviour, reproducible checks, and unresolved questions.
- [Dependency/licence review](docs/DEPENDENCIES.md) and [fixture provenance](fixtures/README.md).
- [Change log](CHANGELOG.md).

## Project decisions

The proposed first version is a standalone planner for celestial bodies, with one assist and one departure node. KSP supplies the celestial system data. MechJeb is not a planned runtime dependency. Further decisions and their rationale are recorded in the scope and design documents.

No project licence has been selected. Any future reuse of third-party code requires a licence review; reference links do not imply permission to copy it. KSP and Unity assemblies must not be distributed in this repository.
