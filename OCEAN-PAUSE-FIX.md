# Ocean Pause State Preservation

## Behavior

Pausing Ocean Interactables now loads the existing Pause Screen additively. The original ocean scene and its runtime objects remain loaded. Resume unloads only the pause menu and restores the previous time scale.

Position, gas, deployed machines, stored ore, inventory counts, collected/uncollected packages, tutorial progress and an active scan are retained. Simulation time stops while paused. World interaction is blocked, including the frame used to resume, so pause-menu input does not become ocean interaction.

The retained ocean's UI EventSystem is disabled while the menu is open and restored after the menu unloads. For this additive use, the pause menu canvas becomes an overlay; its extra camera, audio listener and world ClickHandler are disabled. This preserves the existing menu appearance without duplicate input or audio listeners.

An input-frame guard prevents the ocean and menu PauseManager instances from toggling pause twice on the same key press. Repeated Resume calls are ignored while the menu is unloading.

The submarine return confirmation remains a separate modal pause. Esc cannot open the ordinary pause menu while that dialog is active.

## Verification

Verified locally in Unity 6000.6.4f1 on 2026-10-29:

- Ocean pause Play check passed 26 assertions using actual keyboard input and the Resume button. It verified the same ocean instance before/after pause; frozen and retained state; a scan continuing from previous progress; two repeated Esc pause/resume cycles; one active EventSystem/audio listener; and restored gas consumption/mining afterward.
- The existing Shop pause/resume path also passed its single-scene behavior check.
- Return confirmation Play check passed 37 assertions after this change.
- Complete Ocean Play regression passed 142 assertions after this change.
- New-game initialization setup checks passed, including reset of pause state.
- The live pause-menu screenshot was visually inspected at 1920x1080.

## Boundaries

Non-ocean scenes retain their previous scene-switching pause implementation; this change does not add state persistence to those scenes. The pause menu UI was reused, not redesigned. A standalone player build was not tested.

Scene transition fades were added separately in `SCENE-TRANSITIONS.md`. Deployed-machine minimap markers were added in `MACHINE-MINIMAP.md`.
