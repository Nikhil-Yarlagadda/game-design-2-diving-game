# Tutorial Guidance Update

## Scope

The existing bottom text panel is retained. No popup, animation, new control, or redesigned UI is added.

Day-two guidance now follows this sequence:

1. Swim with WASD and aim the flashlight with the mouse. Explain that left-click acts on the nearest highlighted object. Advance after moving at least one world unit from the excursion starting position.
2. Explain holding E for three seconds, automatic stopping and movement lock when a scan starts, releasing to cancel, the five-second cooldown, and trying another area after an empty scan.
3. Explain revealed tiers and placing a machine near a highlighted spot.
4. Wait for ore production; explain that machines operate while the player explores, that the interaction prompt reports stored ore, and that orange minimap squares mark deployed machines.
5. Retrieve the machine and its ore with left-click.
6. Place the machine again and watch the Hydrox meter.
7. Remind the player to collect all machines before ending the excursion.
8. Explain the submarine minimap marker, distance display, and left-click return interaction.

Action-based steps retain the existing milestone buffering and five-second reading time. The movement and discovery steps advance on their corresponding actions. The final two warnings retain thirty-second display periods.

Purchased-package guidance explains the white minimap dots and nearby left-click collection, and remains visible until all packages are collected.

On day four, depletion guidance follows the final package collection. It appears immediately if there is no delivery guidance to show. It explains the existing actual-tier/last-prospected-tier distinction and dismisses after thirty seconds. Thirty seconds is a local presentation choice, not a specified duration in the design document.

## Verification

Verified in Unity 6000.6.4f1 on 2026-10-29:

- Updated mining/tutorial checks cover the one-unit movement threshold, empty scans, all eight stages, reading periods, milestone buffering, and restarting the sequence. Existing mining simulations also check actual tier depletion and display refresh after re-prospecting.
- Updated day/delivery checks cover day-four guidance without a purchase, display duration, and blocked deliveries preserving first-delivery eligibility.
- The real-scene day/delivery Play check passed 47 assertions, including delivery collection switching to depletion guidance and no repeated tutorial on later excursions.
- A focused Play/layout check passed 47 assertions: all ten message variants fit the existing panel at 1280x720, 1920x1080, 1920x1200, and 2560x1080; the panel stays within the fitted game viewport. Live movement-stage advancement and advancing game frames were also checked.
- The day-two movement panel was captured and visually inspected at 1280x720.

## Remaining Work

Deployed-machine minimap markers were added in `MACHINE-MINIMAP.md`. Scene fades were added in `SCENE-TRANSITIONS.md`. Ocean pause-state preservation was fixed in `OCEAN-PAUSE-FIX.md`. Return confirmation was added in `RETURN-CONFIRMATION.md`, and final tutorial text now explains the confirmation button. Day-two early-return/death handling was fixed in `DAY-TWO-TUTORIAL-FIX.md`. Acceleration/deceleration was tuned separately; see `MOVEMENT-TUNING.md`. Starting a scan while moving was enabled, and both tutorial text and HUD now describe the automatic movement lock.

No standalone player build or full manual campaign was tested. Existing overnight machine-loss behavior was not redesigned.
