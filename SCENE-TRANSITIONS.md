# Basic Scene Fades

## Routes

- Confirmed ocean return fades to black, loads Results, then fades in. Later-day gas exhaustion uses the same Results transition.
- The bed's existing NewDay Yarn command fades to black, advances the day and clears conversation history while the screen is black, then loads and reveals Ocean Interactables.
- Day-two gas-exhaustion tutorial retries retain their existing immediate reload. Other ordinary scene changes remain immediate.

Each fade lasts 0.35 seconds. Loading occurs under a fully opaque black cover, so a longer load extends the black interval. The cover spans the full output window, including any letterbox area.

## Input and State

Simulation time is paused during the transition; fade timing uses unscaled time. The cover blocks mouse UI input, world/dialogue input is guarded, and Results cannot settle or leave during fade-in. Repeated sleep/transition requests do not start another transition or advance the day twice.

When the fade ends, the cover is removed and the previous time scale is restored. Destroying a pending fade restores time. New-game reset cancels an in-progress transition and clears its pending day change.

The existing test callers now wait for transitions to finish rather than assuming scenes change after a fixed 100-150 milliseconds.

## Verification

Verified locally in Unity 6000.6.4f1 on 2026-10-29:

- Scene fade Play check passed 18 assertions covering fade-out, full-black loading, fade-in, simulation/input guards, repeated sleep, Results settlement, interrupted transitions and new-game cleanup (including reset from a non-default time scale).
- Return confirmation Play check passed 37 assertions.
- Day-two Play check passed 19 assertions.
- Day/delivery Play regression passed 50 assertions.
- Ocean pause Play check passed 26 assertions.
- Source fade-out, fully black loading, and visible Results screenshots were captured and visually inspected at 1920x1080.

## Boundaries

Sleep verification invokes the bed's existing Yarn command entry point; the complete bed dialogue and UI choice sequence was not manually played. No audio transition, loading UI, tutorial animation, or new art was added. A standalone player build was not tested.

Deployed-machine minimap markers were added separately in `MACHINE-MINIMAP.md`.
