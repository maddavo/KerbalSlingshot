# Change log

## Unreleased

### 2026-10-06 — corrected automatic-planning requirement

- Clarified that the pilot selects assist body, destination body, and destination periapsis; the mod must generate its own candidate burns and create a validated departure node.
- Marked supplied-estimate/import and native burn-frame failure as prototype gaps, not pilot setup requirements.
- Replaced the next-session test procedure with a no-preparatory-node Mun-to-Minmus acceptance test.

### 0.2.1 — compact pilot UI and stock toolbar

- Replaced the floating Slingshot button with an owned stock KSP toolbar icon and scene/lifecycle cleanup.
- Reduced the primary window to 520×684, grouped targets/estimate/calculate/results, and added consistent white text on a dark panel.
- Promoted node import; grouped optional manual inputs; collapsed Advanced and Details while retaining all safety/search defaults.
- Added prominent unvalidated result status and five-event summary, locked conflicting controls during jobs, and kept Cancel visible.
- Added five focused offline presentation checks and a next-session visual checklist; numerical solver and route coverage unchanged.
- Revised rendering/toolbar behaviour remain unverified in KSP; no installed files or vessel nodes/saves were changed.

### 0.2.0 — functional planning test prototype

- Added the actual KSP 1.12.5 flight addon, live vessel/body snapshots, target/altitude controls, and typed/read-only imported node estimates.
- Connected bounded calculation with progress, cancellation, stale-result handling, complete offline-feasible predictions, and rejected partial-route diagnostics.
- Added read-only existing-patch comparison, evidence export, nine integration-plumbing check groups, and addon/reference metadata inspection.
- Added clean-commit local test packaging, reversible installation steps, and one combined in-game test. Only the two authored DLLs are packaged.
- No node creation, installed-DLL overwrite, KSP launch, in-game-validation claim, or published release.

### 2026-10-06

- Implemented detached 3D conic propagation, finite-SOI event handling, ordered flyby/destination constraints, and bounded deterministic local refinement.
- Added five frozen synthetic fixtures, repeatable offline checks, independent RK4 verification, and build/evidence scripts.
- Established `net48` core/KSP API-contract builds against local KSP 1.12.5 and a `net8.0` harness, without copying game assemblies.
- Recorded dependency/licence review and milestone evidence. No KSP launch, DLL installation, live planner, or release.

### 2026-10-05

- Established the proposed one-burn, one-assist, destination-periapsis product scope.
- Defined altitude units, supported route families, search constraints, result states, and exclusions.
- Documented the proposed solver architecture, KSP validation, and safe node lifecycle.
- Added staged development gates, validation criteria, and primary research references.
- Documentation only; no plugin implementation or release.
