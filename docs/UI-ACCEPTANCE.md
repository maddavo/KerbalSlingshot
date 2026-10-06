# Pilot workflow UI acceptance

Run this checklist in the same consolidated KSP session as [KSP-TEST.md](KSP-TEST.md), after automatic candidate search and node creation are implemented. The installed 0.2.1 interface still exposes supplied-estimate controls and is not the target workflow.

Target: KSP 1.12.5 at 1920×1080, initially normal UI scale, using a recoverable Kerbin-orbit test save with no preparatory manoeuvre node. Record the UI scale and build manifest.

1. Open Slingshot with the stock KSP toolbar icon. The main view clearly presents Gravity Assist Target, Intercept Target, destination periapsis altitude with km, and **Find Trajectory**. There are no required node-import or manual-burn fields.
2. Select Mun, Minmus, and the requested periapsis. The active vessel/current parent state is visible enough to catch an unsuitable starting orbit. Required selections and next action are immediately clear without scrolling.
3. Expand/collapse Advanced safety/search settings. Ordinary use has understandable defaults; terrain assumptions, if required, explain what the user is acknowledging. Collapsing Advanced preserves the values.
4. Start a search. Inputs that would invalidate the run are locked or safely invalidate it. Progress and Cancel stay visible; the flight view remains usable and the UI does not freeze.
5. A candidate result prominently distinguishes **KSP validated** from searching, rejected, incomplete, or offline-only results. It shows departure, delta-v, Mun entry/periapsis/exit, Minmus entry/periapsis, altitude error/tolerance, and journey duration. A missing event is never rendered as zero or as success.
6. **Create Node** is enabled only for a current, complete KSP-validated candidate. The button text makes clear that it adds a manoeuvre node. No other UI action adds, changes, or removes a node.
7. After insertion, the candidate/result shows the KSP-confirmed node and patch chain. A conflict or failed verification reports why and preserves pre-existing nodes.
8. Check readable contrast, field units, focus, window dragging, toolbar show/hide and scene lifecycle. At common resolution, the core inputs, action, status and result fit without becoming a large developer form; Advanced content alone may scroll.

Capture ready, searching, validated-result, and constrained-failure states. Include clipping, overlap with flight controls, stale-result behaviour, result-legibility issues, and node-count/value differences. Passing this visual checklist does not by itself establish flight performance or accuracy.
