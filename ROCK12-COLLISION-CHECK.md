# Rock 12 Collision Check

## Reproduction

Reproduced in Unity 6000.6.4f1 on 2026-10-29 using real keyboard input and the original test's spawn point and 3.1-second rightward push. A temporary probe sampled every physics step.

- Current acceleration (5): 155 samples, 147 with Rock 12 contact. First contact occurred at about 0.04 seconds; the last was at about 3.04 seconds. The character then slid clear of the lower edge, so the final contact query returned false. The character center was never inside the rock.
- Previous acceleration (1): 155 samples, 134 with Rock 12 contact. The character was still contacting the rock at 3.1 seconds. The character center was never inside the rock.
- Chasm 4 with current acceleration: 155 samples, 144 with contact; no sample placed the character center inside the chasm.

The Rock 12 failure was a false failure in the final-frame-only assertion. The faster acceleration allowed the character to slide around the sloping boundary before that assertion ran. No gameplay collision change was required for the reproduced case.

Detailed temporary observations are in `Temp/rock12-review/baseline.json`. Unity may clear its Temp folder; the findings above are retained here.

## Change

`OceanPlayCheck` now observes the whole push interval. It requires actual contact at some point, checks that the character center never enters the obstacle, and checks movement segments for crossing it. It permits the character to slide free afterward.

The check also waits for the scene's Start methods after enabling background execution, before asserting the initial tutorial stage. Temporary input settings and devices are restored by the existing cleanup path.

## Verification

The complete Ocean Play check passed 142 assertions with the current gameplay settings. It reported open-water speed 2.5 units/second, kelp speed 1.25 units/second, and a successful day-two gas-exhaustion tutorial retry. Rock 12 and Chasm 4 passed the updated collision checks.

This is verification of these scripted routes, not an exhaustive collision audit of every polygon or a standalone player build.

## API References Consulted

- [Unity 6000.6 Rigidbody2D.GetContacts](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rigidbody2d/getcontacts)
- [Unity 6000.6 Collider2D.OverlapPoint](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/collider2d/overlappoint)
- [Unity 6000.6 Collider2D.Distance](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/collider2d/distance)
