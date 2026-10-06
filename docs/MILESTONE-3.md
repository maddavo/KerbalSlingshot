# Milestone 3 — compact pilot UI and stock toolbar

Version **0.2.1**, assembly version **0.2.1.0**, Release `net48`, KSP 1.12.5. Exact clean-commit identity and DLL SHA-256 are supplied in the packaged build manifest. This is a focused UI iteration; numerical propagation, search, route coverage, snapshot capture, and node safety are unchanged.

## Feedback and implemented changes

The user's two first-session screenshots (`KSP_x64_giV0bvteLH.jpg`, `KSP_x64_vbnKbiDlmk.jpg`) show 0.2.0 loaded in flight. Targets and estimate occupy one scroll position; settings, actions, and result status require another. Bright yellow labels, the long revision in the title, and the floating Slingshot button compete with the flight view. The images are visual feedback only; they do not demonstrate a validated trajectory.

- Replaced the floating button with a **stock KSP ApplicationLauncher toolbar icon** available in flight/map view. The window starts closed. Toolbar-ready/destroyed callbacks register/remove one owned button, and component destruction unregisters callbacks and destroys its authored procedural icon/GUI texture. Hiding releases the ship-control input lock; GUI focus clearing is deferred to OnGUI.
- Reduced the primary window from 680×720 to **520×684** GUI pixels. It starts beside the right-side flight area, leaving a 320-pixel toolbar/resource margin at 1920 width, and remains movable/clamped to the screen. Near-opaque dark background, white field/label text, subdued notes and blue section headings replace the dense yellow developer appearance. The short title shows version; full build identity stays in Details/diagnostics.
- Targets are three aligned rows, with compact previous/next selectors and **km beside the periapsis field**.
- **Import existing node (recommended)** is the prominent estimate action. Departure summary stays visible. Optional Manual estimate keeps UT seconds and native radial/normal/prograde m/s together; it does not change the seed interpretation.
- Evaluate and Refine have distinct labels/tooltips. Refresh/import/targets/settings/manual inputs and competing calculation controls are disabled during a job. Cancel remains visible/enabled, progress is in the status card, and existing cancellation/stale-revision safeguards remain active.
- The result card contains prominent status, an explicit **Unvalidated — compare with KSP** label, departure UT/total delta-v, five ordered event rows with UT seconds and altitude km, and periapsis error/tolerance. Rejected partial routes retain missing-value markers; they never display success or fabricated zero events.
- Advanced starts collapsed and retains every original safety/search field and value. Terrain ceiling and acknowledgement appear first when opened. Calculation requires reviewed safety; the collapsed row/readiness text makes this requirement visible. Changing the terrain ceiling clears its acknowledgement. Only the secondary Advanced panel scrolls.
- Details retains full precision, original reports, read-only existing KSP patch comparison, and diagnostic export. Advanced and Details close one another to prevent stacked secondary panels overtaking the primary flow. These presentation toggles do not alter values or cancel jobs.

## Offline evidence

All Release builds pass with zero warnings/errors. **32 harness check groups pass, zero failures**: the prior 27 checks plus five focused presentation checks for search/ready/blocked button states, complete result units/events/unvalidated label, partial/missing-route honesty, primary/expanded geometric bounds at 1920×1080, and unchanged parsing/default values. Frozen trajectory fixtures and expected positive solver evaluation counts remain unchanged.

Actual addon startup/reference metadata is inspected without executing KSP. Native toolbar and styled GUI APIs compile against the installed 1.12.5 reference set. Additional Animation/TextRendering modules are local non-copying compile references. Existing packaging verifies only the two authored DLLs are copied and their ZIP bytes match the recorded hashes; no KSP/Unity assemblies or external icon assets are packaged.

## Remaining verification and limitations

**The revised 0.2.1 screen has not been run in KSP by this chat.** The first-session images establish that the old 0.2.0 UI appeared, not that the new layout, toolbar lifecycle, text contrast, focus handling, scaling, or trajectory predictions have passed an in-game check. Fixed geometry checks do not render Unity fonts or establish behaviour under nondefault UI scale/smaller resolutions.

Use the included **UI-ACCEPTANCE.md** in one consolidated KSP flight. Check no scrolling is needed for the primary flow/result at 1920×1080, toolbar toggle/scene lifecycle, foldouts, search/cancel locks, readable results, and unchanged node/save state. Include screenshots, UI scale, manifest, and failures; do not perform a standalone load-only run.

The same shared-parent route restriction, explicit supplied estimate, conservative user-reviewed terrain bounds, unsupported nested target children, and no-solution-within-bounds semantics remain. No node creation/edit/deletion, burn execution, time warp, save mutation, general seed generation, or extra route family was added. No revised DLL was installed and KSP was not launched during this work.
