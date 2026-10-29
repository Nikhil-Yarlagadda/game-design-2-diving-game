# New Game Initialization

## Behavior

- The title screen resets game data before loading Introduction.
- The same initialization runs once when the application starts or Unity enters a new Play session, including with Domain Reload disabled.
- Initialization restores day 1, 100 credits, zero tellurium, empty dialogue and mining progress, no pending purchases, and fresh delivery tutorial eligibility.
- It also clears the paused flag and previous resume scene, and restores the time scale to 1.
- Resuming a pause or loading another scene does not invoke new-game initialization.
- Stopping Play and starting again begins a fresh session. Pausing Play and continuing keeps the current session.
- Opening the ocean scene directly for debugging starts with default data; start from the title screen to test the complete first-day flow.

## Verification

Verified locally in Unity 6000.6.4f1 with Domain Reload disabled on 2026-10-29:

- `Diving Prototype > Check New Game Initialization`: seven assertions covering data reset, pause/time reset, repeated initialization, the startup hook, and title button callback order. The check restores the pre-existing global state afterward.
- Seeded stale day, resources, purchases, mining and tutorial state before entering Play; verified defaults were restored.
- Seeded day 8 and paused/time-zero state during Play, then stopped and entered Play again; verified day 1 and normal time. Frames advanced from 1 to 230 during a 300 ms observation.
- Invoked the actual title Start button with stale paused state; verified Introduction, day 1, 100 credits, zero tellurium, and normal time.
- Editor pause/resume and the existing game pause/resume path preserved seeded global day/resource values.
- An ordinary scene transition preserved seeded global day/resource values.
- Entered Play directly from Ocean Interactables after seeding stale state; verified initialization and advancing frames (645 to 802).

## Boundaries

No save system, restart menu, or other gameplay bug fixes were added. A standalone player build was not tested.

The ocean pause implementation was subsequently changed to retain the live ocean scene; see `OCEAN-PAUSE-FIX.md`. New-game initialization remains separate from pause/resume.

Unity Version Control temporarily blocked script reload. The editor was restarted with user approval; checks completed after the component became responsive. No version-control settings were changed.
