# KerbalSlingshot

A proposed gravity-assist manoeuvre planner for **Kerbal Space Program 1**.

Choose a **Gravity Assist Target**, an **Intercept Target**, and an **Intercept Target Distance**. KerbalSlingshot will search for one departure manoeuvre that sends the active vessel through the assist body's sphere of influence (SOI), leaves it on an escape trajectory, and subsequently encounters the destination at the requested periapsis altitude.

**Status: compact candidate-planning test prototype, version 0.2.1.** The flight plugin builds for .NET Framework 4.8 against KSP 1.12.5. This UI iteration uses the stock toolbar, a grouped pilot workflow and collapsed Advanced settings. The user's screenshots show the prior 0.2.0 panel loaded in flight; the revised UI and trajectory agreement still need the combined KSP check. No published release or enabled node creation.

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

## Test prototype use

1. Install the two authored DLLs using [INSTALL.md](docs/INSTALL.md), then open **Slingshot from KSP's stock mod toolbar** in a recoverable flight.
2. **Read vessel / bodies** and select two distinct direct children of the current parent. The vessel must already be outside all child SOIs. Selected targets with their own children are not yet supported.
3. Enter the destination periapsis altitude beside its **km** unit. Open **Advanced** to review the conservative terrain ceiling and safety/search bounds; acknowledge the terrain assumption after checking it, then collapse Advanced.
4. Type a starting estimate's departure UT and native radial/normal/prograde m/s, or **Import first future node** from a manually prepared single node on the current patch. An imported node is only a starting estimate and is never changed.
5. **Evaluate estimate** or **Refine estimate**. Review the complete offline-feasible prediction or clearly labelled rejected/partial diagnostics. Cancellation and bounded computation are available.
6. Use **Read existing KSP patches** and the [combined test procedure](docs/KSP-TEST.md) for manual comparison. The plugin never calls a result KSP-validated and **does not create nodes**. Write diagnostics to capture exact settings and evidence.

There is no general seed generator. The prototype performs local refinement around the supplied estimate; arbitrary requests can return no solution found within bounds. Parking-orbit departures, source SOI changes, and moon-assisted planetary escape remain unimplemented. Automatic creation of a KSP-validated node remains a future product requirement.

## Build and offline checks

With the .NET 8 SDK installed, run from the repository root:

```powershell
./check.ps1 -OfflineOnly
```

To also build and inspect the actual flight plugin, provide your local `KSP_x64_Data/Managed` directory:

```powershell
./check.ps1 -KspManagedPath 'C:/path/to/Kerbal Space Program/KSP_x64_Data/Managed'
```

The core targets `net48` and `net8.0`; the harness runs on .NET 8, and the flight plugin targets `net48`. Game assemblies stay outside the repository and are not copied to outputs. From a clean commit, `./package.ps1 -KspManagedPath 'C:/path/to/KSP_x64_Data/Managed'` produces a hashed local test ZIP with only the two mod DLLs and handoff documents. These commands do not launch KSP, install a DLL, or publish a release. See [current UI milestone evidence](docs/MILESTONE-3.md).

## Documentation

- [Scope and requirements](docs/SCOPE.md): supported routes, inputs, exclusions, and acceptance criteria.
- [Solver and integration design](docs/DESIGN.md): trajectory search, physical constraints, KSP validation, and node lifecycle.
- [Development stages](docs/DEVELOPMENT.md): feasibility gates and evidence required before release.
- [Research references](docs/REFERENCES.md): primary sources and limits of current evidence.
- [Milestone 1 evidence](docs/MILESTONE-1.md): implemented behaviour, reproducible checks, and unresolved questions.
- [Milestone 2 evidence](docs/MILESTONE-2.md), [installation/rollback](docs/INSTALL.md), and [combined KSP test](docs/KSP-TEST.md).
- [Milestone 3 UI evidence](docs/MILESTONE-3.md) and the [next-session visual checklist](docs/UI-ACCEPTANCE.md).
- [Dependency/licence review](docs/DEPENDENCIES.md) and [fixture provenance](fixtures/README.md).
- [Change log](CHANGELOG.md).

## Project decisions

The proposed first version is a standalone planner for celestial bodies, with one assist and one departure node. KSP supplies the celestial system data. MechJeb is not a planned runtime dependency. Further decisions and their rationale are recorded in the scope and design documents.

No project licence has been selected. Any future reuse of third-party code requires a licence review; reference links do not imply permission to copy it. KSP and Unity assemblies must not be distributed in this repository.
