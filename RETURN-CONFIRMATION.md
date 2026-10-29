# Return Confirmation

## Interaction

- A nearby submarine interaction opens a confirmation instead of immediately ending the excursion.
- Continue exploring closes the dialog and resumes the ocean simulation.
- End excursion restores simulation time and enters the existing Results flow.
- The dialog reports the current number of deployed mining machines and warns that they will be left behind. With no deployed machines, it states that none will be left behind.
- Only the two mouse-operated buttons accept a choice. Esc, other gameplay keys, and clicking the backdrop do not close or confirm it. Button navigation is disabled and UI selection is cleared so keyboard submit does not activate a default choice.
- The click used to cancel is consumed before world interaction resumes. Repeated confirmation cannot end the excursion twice.

## Pausing

The dialog saves and sets the time scale to zero. Gas consumption, mining, cooldowns, tutorial timers and physics stop. Movement is explicitly locked, world interaction is blocked, and flashlight aiming is frozen. UI input continues to run. Cancel restores the previous time scale and unlocks movement; unloading the ocean while the dialog is open also restores time.

The ordinary Esc pause menu is blocked while this modal pause is active. Ocean state preservation for that menu was subsequently fixed separately in `OCEAN-PAUSE-FIX.md`.

Day-two training must still finish before the dialog is available. Gas exhaustion bypasses confirmation: day two retries training; later days use the existing Results path.

## Scene Wiring

Ocean Interactables now has an EventSystem with the existing Input System UI module and a Graphic Raycaster on its HUD canvas. UI action references reuse the same installed default input asset already used by Title Screen. No package or external dependency was added.

## Verification

Verified in Unity 6000.6.4f1 on 2026-10-29:

- Return confirmation Play check: 37 assertions passed using actual synthetic mouse/keyboard input, including both buttons, outside clicks and Esc/Enter/Space/Tab/W/E, simulation freeze/resume, deployed/zero-machine messages, click-through prevention, and duplicate confirmation protection.
- Text and dialog bounds passed at 1280x720, 1920x1080, 1920x1200 and 2560x1080. The 1280x720 one-machine screenshot was visually inspected.
- Day-two Play check: 19 assertions passed, including the new confirmation after completed training.
- Day/delivery Play regression: 50 assertions passed, including manual confirmation and automatic gas-depletion behavior on later days.
- Existing mining/tutorial and new-game initialization setup checks passed.

## Boundaries

Scene fades were added separately in `SCENE-TRANSITIONS.md` and deployed-machine markers in `MACHINE-MINIMAP.md`; tutorial animation was not added. Ordinary ocean pause-state preservation was fixed separately in `OCEAN-PAUSE-FIX.md`. A standalone player build was not tested. The previously reported Rock 12 contact assertion was resolved as a check issue; the updated full Ocean Play check passed 142 assertions. See `ROCK12-COLLISION-CHECK.md`.
