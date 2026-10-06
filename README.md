# KerbalSlingshot

A proposed gravity-assist manoeuvre planner for **Kerbal Space Program 1**.

Choose a **Gravity Assist Target**, an **Intercept Target**, and an **Intercept Target Distance**. KerbalSlingshot will search for one departure manoeuvre that sends the active vessel through the assist body's sphere of influence (SOI), leaves it on an escape trajectory, and subsequently encounters the destination at the requested periapsis altitude.

**Status: automatic-planning test build 0.3.0.** Internal seed generation, full-route numerical search, temporary KSP validation and guarded **Create Node** are implemented and build for KSP 1.12.5 / .NET Framework 4.8. Forty offline check groups pass, including automatic stock-scale Mun/Minmus recovery without supplied seeds. Live KSP search/validation/insertion remain untested. Installation is held at Dave's request; the currently installed files are unchanged. See [Milestone 4](docs/MILESTONE-4.md) and the [single-session handoff](docs/AUTOMATIC-HANDOFF.md).

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

## Intended pilot workflow

1. Open **Slingshot** from KSP's stock mod toolbar while controlling a supported vessel.
2. Select the Gravity Assist Target, Intercept Target, and desired destination periapsis altitude in **km**.
3. The mod automatically searches from the active vessel's current orbit. The pilot is not expected to create or import a preliminary maneuver node or supply a burn estimate.
4. Review the complete predicted flyby and destination encounter. The mod enables **Create Node** only after KSP validates the full route, frame conversion, safety, and periapsis.
5. Press **Create Node** to add the single departure node. The mod never executes the burn or changes time warp.

The initial live implementation is specifically Kerbin SOI -> Mun -> Minmus, outside child SOIs. No preliminary node or burn input is requested. A numerical candidate alone never enables Create Node: runtime KSP checks, freshness and absence of conflicting future nodes are required. This describes implemented code, not an asserted live test pass.

## Build and offline checks

With the .NET 8 SDK installed, run from the repository root:

```powershell
./check.ps1 -OfflineOnly
```

To also build and inspect the actual flight plugin, provide your local `KSP_x64_Data/Managed` directory:

```powershell
./check.ps1 -KspManagedPath 'C:/path/to/Kerbal Space Program/KSP_x64_Data/Managed'
```

The core targets `net48` and `net8.0`; the harness runs on .NET 8, and the flight plugin targets `net48`. Game assemblies stay outside the repository and are not copied to outputs. From a clean commit, `./package.ps1 -KspManagedPath 'C:/path/to/KSP_x64_Data/Managed'` produces a hashed local test ZIP with only the two mod DLLs and handoff documents. These commands do not launch KSP, install a DLL, or publish a release. See [current automatic-planning evidence](docs/MILESTONE-4.md).

## Documentation

- [Scope and requirements](docs/SCOPE.md): supported routes, inputs, exclusions, and acceptance criteria.
- [Solver and integration design](docs/DESIGN.md): trajectory search, physical constraints, KSP validation, and node lifecycle.
- [Development stages](docs/DEVELOPMENT.md): feasibility gates and evidence required before release.
- [Research references](docs/REFERENCES.md): primary sources and limits of current evidence.
- [Milestone 1 evidence](docs/MILESTONE-1.md): implemented behaviour, reproducible checks, and unresolved questions.
- [Milestone 2 evidence](docs/MILESTONE-2.md), [installation/rollback](docs/INSTALL.md), and [combined KSP test](docs/KSP-TEST.md).
- [Milestone 3 UI evidence and product correction](docs/MILESTONE-3.md), [automatic-planning KSP test](docs/KSP-TEST.md), and [UI acceptance checklist](docs/UI-ACCEPTANCE.md).
- [Milestone 4](docs/MILESTONE-4.md) and [concise automatic-search/node handoff](docs/AUTOMATIC-HANDOFF.md).
- [Dependency/licence review](docs/DEPENDENCIES.md) and [fixture provenance](fixtures/README.md).
- [Change log](CHANGELOG.md).

## Project decisions

The proposed first version is a standalone planner for celestial bodies, with one assist and one departure node. KSP supplies the celestial system data. MechJeb is not a planned runtime dependency. Further decisions and their rationale are recorded in the scope and design documents.

No project licence has been selected. Any future reuse of third-party code requires a licence review; reference links do not imply permission to copy it. KSP and Unity assemblies must not be distributed in this repository.
