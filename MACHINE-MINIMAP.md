# Deployed Mining Machine Minimap Markers

## Behavior

- Deployed machines use orange, unrotated squares, distinct from the cyan submarine diamond and white purchased-item dots.
- Discovering a mining spot alone does not show a machine marker. The marker is visible only while that spot has a machine.
- Retrieving a machine hides its marker. Placing it again reuses the existing marker.
- Position, direction, out-of-range border clamping and dimming use the existing minimap projection and range.
- Markers cannot intercept mouse input. The submarine retains its existing priority over overlapping target markers.
- Each mining spot gets one initially hidden marker when the HUD starts. Placement and retrieval do not create additional UI objects.
- Tutorial production guidance explains the orange machine squares.

## Verification

Verified in Unity 6000.6.4f1 on 2026-10-29:

- Machine minimap Play check passed 20 assertions covering empty/discovered spots, actual placement and retrieval handlers, two deployed machines, color/shape differences, relative positions, border clamping/dimming, full opacity on return, pause preservation, instance reuse and absence of phantom markers after reloading.
- The two-machine screenshot was visually inspected at 1920x1080.
- Existing mining/tutorial setup checks passed.
- Complete Ocean Play regression passed 142 assertions after this change, including the existing submarine/item minimap behavior and real input interactions.

## Boundaries

No minimap labels, new map range, new machine persistence rules, new art or external dependency were added. No standalone player build or new performance profile was run.

All eight items in the original demo bug checklist now have local implementations and the documented checks. Publishing remains separate.
