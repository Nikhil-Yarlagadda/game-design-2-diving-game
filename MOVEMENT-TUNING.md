# Movement Tuning

Updated both Ocean Interactables and Ocean Floor, plus new-component defaults:

- Maximum speed remains 2.5 units/second.
- Acceleration increases from 1 to 5 units/second squared.
- Deceleration increases from 1.5 to 12.5 units/second squared.
- Reversal retains the existing gradual velocity change. Kelp continues to halve maximum speed.

## Verification

Verified in Unity 6000.6.4f1 on 2026-10-29:

- Swimming setup checks use the serialized parameters from both ocean scenes, covering acceleration, diagonal speed, braking distance, stationary input, reversal and the half-speed cap.
- A Play-mode keyboard-input check measured 0.50 seconds to full speed, 0.20 seconds to stop, and 0.225 units of stopping distance.
- Full speed measured 2.50 units/second; speed inside actual kelp measured 1.25 units/second.
- Reversing direction retained initial momentum before changing direction. The player loop advanced throughout the check.

These values are a first playtest setting, not final balance. No camera, map scale or illumination changes were made. Standalone player performance and subjective controller feel were not tested.
