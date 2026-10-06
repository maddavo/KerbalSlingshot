# Combined planning prototype test

Written before live integration implementation. Target: KSP 1.12.5, stock patched conics, no Principia. No optional mod is required. This procedure is pending; no successful in-game run is asserted.

## Setup and state to record

Use a copied/recoverable save with a vessel in **Kerbin's SOI, outside Mun and Minmus SOIs**. The vessel must be coasting with throttle zero. Record game version, loaded mods, save/vessel name, vessel UT and orbit, and the build identity/hashes from the package manifest. Parking orbits around a body without two direct children are unsupported by this solver.

For a useful starting estimate, manually prepare one future node on the current Kerbin conic that encounters Mun and leaves it on an escape conic. Place it far enough in the future to allow the bounded calculation to finish. A subsequent Minmus encounter is useful if one is available, but not assumed reachable at this date. Record its UT and radial/normal/prograde components. Do not execute the node during this test. Use the game's patch display to inspect the chain, increasing visible patches through your existing settings if necessary.

## Steps and expected evidence

1. Install only the two authored DLLs using INSTALL.md. Launch KSP once for this whole procedure. Enter the recoverable flight and open **Slingshot** using its stock toolbar icon. For the revised UI iteration, combine this with UI-ACCEPTANCE.md.
2. **Read vessel / bodies**. Expect the vessel identity, UT, Kerbin parent, Mun and Minmus selections, and count of relevant child bodies. Choose Mun as assist and Minmus as destination. Confirm the displayed captured orbit/parent are correct.
3. Set desired destination periapsis altitude in km. Set conservative terrain ceilings for the parent and all children, minimum assist altitude, clearance margin, departure/journey limits, and search/time budgets. Confirm the terrain-ceiling acknowledgement only after checking the assumptions. Atmospheres are read from KSP and included in safety constraints.
4. **Import first future node**. Expect its UT/components and a frame-consistency check. The plugin must not add, edit, or delete any node. An earlier burn, a node on another patch, or an unsupported route must be rejected with a reason.
5. **Evaluate estimate**. Expect a fresh live snapshot and numerical evaluation of that exact burn. Compare any displayed Mun entry/periapsis/exit with the existing node's KSP patches. Press **Read existing KSP patches** for the read-only chain summary. These patches describe the existing node, not a changed solver candidate. Missing destination / unsafe passage is a rejected partial prediction, never success.
6. **Refine estimate**. Expect bounded progress and a responsive UI. A result is labelled **Offline-feasible prediction; KSP validation pending** only when the full ordered assist entry/periapsis/escape/destination entry/periapsis sequence passes the detached constraints and requested altitude tolerance. Otherwise expect the actual failure reason and partial diagnostic events. No reachable solution is promised for an arbitrary live orbit/seed.
7. If a full candidate is found, record its UT and displayed native node components. In the copied save, manually adjust the existing node to these values and inspect its patches. This edits the game's node by your action; the plugin never creates one. Compare body sequence, SOI boundary times, assist periapsis, and final periapsis within the displayed tolerance. Record differences; do not call agreement established if the chain is truncated or components were rounded too far.
8. Start a longer refinement and **Cancel**. Expect cancellation and no node changes. Then change targets/settings or switch vessel while a calculation is running. Expect stale work to be discarded, not reused. A passed burn time or changed vessel orbit invalidates the displayed result.
9. Try identical targets, invalid numeric input, an altitude below the conservative safe bound, and a deliberately short journey or one-evaluation budget. Expect invalid-input or no-solution-within-bounds outcomes, not a false success.

## Pass/fail and report

The integration check passes only if live snapshots and controls work, numerical outcomes are honestly labelled, cancellation/staleness work, and no node is modified. Trajectory agreement is a **separate** result: it passes only for a recorded full candidate actually compared with the matching KSP patch chain. A partial route can establish useful integration evidence but cannot pass the full trajectory criterion.

Capture window screenshots, the exact settings/seed/result, existing node components before/after, patch sequence/UTs/periapses, package build manifest, and `[KerbalSlingshot]` lines from KSP.log (including exceptions). The plugin's **Write diagnostics** button saves a text report under `GameData/KerbalSlingshot/Diagnostics/`; include that report. Missing UI, frame-check failure, snapshot rejection, cancellation, no result, truncated patches, and numerical-versus-KSP disagreement should be reported distinctly. Restore the copied save or remove the two prototype DLLs after testing if rollback is needed.
