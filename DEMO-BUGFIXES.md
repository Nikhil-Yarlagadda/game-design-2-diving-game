# Ocean Demo Playtest Bug Fixes

Branch: `fix/ocean-demo`

Base: `520aab1` (main, Demo Prototype merge)

Verified locally: 2026-10-29, Unity 6000.6.4f1

## Changes

1. **Movement response:** retain maximum speed at 2.5 units/second; reach it in about 0.5 seconds and stop in about 0.2 seconds, with approximately 0.225 units of release glide. Kelp still halves speed.
2. **Prospecting:** holding E can begin a scan while moving or coasting. Starting the scan stops the diver and locks movement. Release cancels; completion retains the three-second duration and five-second cooldown.
3. **Tutorial instructions:** retain the existing text panel, add initial movement/aim guidance and its one-unit movement milestone, complete the mining/return instructions, and add day-four depletion guidance after deliveries. Text reflects the new scan controls, return confirmation and orange machine markers. No animated tutorial or tutorial popup was added.
4. **Day-two progression:** prevent returning before training completes. Gas exhaustion restarts the day-two excursion and training without Results, settlement or advancing the day. Restore the failed excursion's starting mining state and deliveries.
5. **Return confirmation:** nearby left-click opens a two-button dialog with the number of deployed machines and a reminder that they will be left behind. Continue exploring resumes; End excursion proceeds to Results. Simulation, gas, mining and timers freeze. Esc, outside clicks and keyboard submit do not choose an answer.
6. **Ocean pause:** retain the live ocean scene while the existing pause menu is loaded additively. Resume preserves position, gas, machines, collected packages, tutorial and active scan progress. Prevent duplicate input/audio and repeated toggles.
7. **Scene transitions:** fade out for 0.35 seconds, load under black, then fade in for 0.35 seconds for ocean-to-Results and the bed's NewDay-to-ocean path. Freeze simulation and block repeated actions until complete. New-game reset cancels pending transitions safely.
8. **Machine minimap markers:** show orange squares for deployed machines, hide them on retrieval, reuse them on placement, and apply the existing border clamping and dimming outside range. Preserve cyan submarine diamonds and white package dots.

Additional fixes:

- Use one new-game initializer for title Start and fresh Play sessions, including with Domain Reload disabled. Initialize before loading Introduction; restore normal time and clear stale pause state.
- Correct the Rock 12 collision check to observe contact and penetration over the whole push rather than require contact on the final frame. Runtime sampling showed normal sliding around the edge, not passage through the rock.

## Validation

These are assertion counts from the recorded Editor checks, not unique manual test cases:

- Complete Ocean Play regression: **142 passed** after the final machine-marker change.
- Machine minimap Play check: **20 passed**.
- Ocean pause Play check: **26 passed**.
- Return confirmation Play check: **37 passed**.
- Scene transition Play check: **18 passed**.
- Day-two Play check: **19 passed**.
- Day/delivery Play regression: **50 passed**.
- New-game setup check: **7 passed**; existing swimming and mining/tutorial setup checks also passed.

Relevant checks cover 1280x720, 1920x1080, 1920x1200 and 2560x1080 layouts. Runtime screenshots of the dialogs, minimap, pause menu and fades were inspected. Detailed results and scope are recorded in the linked notes below.

## Expected Behavior and Remaining Scope

- Discovered mining spots remain marked across days within the current game, as specified by the complete design document. Their displayed tier remains the last prospected tier until scanned again.
- Mining depletion and partial tier output persist across days. The separate design rule that a machine abandoned overnight causes an additional tier drop is **not implemented** by this change.
- Non-ocean scenes retain their previous single-scene pause implementation.
- No standalone player build or new performance profile was run. The fade check invoked the existing bed NewDay Yarn command; it did not manually play the entire bed dialogue/choice sequence.
- Existing local tutorial-framework settings, generated fallback-font changes and the editor's TimeManager serialization migration are excluded from this commit.

## Detailed Notes

- [New game initialization](NEW-GAME-INITIALIZATION.md)
- [Movement tuning](MOVEMENT-TUNING.md)
- [Prospecting](PROSPECTING-FIX.md)
- [Tutorial guidance](TUTORIAL-GUIDANCE-NOTES.md)
- [Day-two return and retry](DAY-TWO-TUTORIAL-FIX.md)
- [Return confirmation](RETURN-CONFIRMATION.md)
- [Ocean pause](OCEAN-PAUSE-FIX.md)
- [Scene fades](SCENE-TRANSITIONS.md)
- [Machine minimap markers](MACHINE-MINIMAP.md)
- [Rock 12 collision check](ROCK12-COLLISION-CHECK.md)
