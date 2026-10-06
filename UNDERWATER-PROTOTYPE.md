# Underwater Scene Prototype

This prototype covers the Ocean Floor assignment: a 2D top-down view, a centered player, swimming movement, a simple character/map structure, and square placeholders.

## Requirements

- Unity Editor **6000.6.4f1** (upgraded from 6000.6.2f1).
- Git LFS for the repository's existing image assets.
- The package manifest includes `com.unity.pipeline` **0.8.0-exp.1** for local editor automation. Teammates can play and edit the prototype using Unity without installing the Unity CLI or Codex.

## Play

1. Open `Assets/Scenes/Ocean Floor.unity` in Unity.
2. Press **Play**, then click the **Game** view to give it keyboard focus.
3. Move with **WASD** or the **arrow keys**. The yellow square is the player.
4. Gray-blue blocks are solid rocks and boundaries. Pink squares are non-interactive landmarks.
5. Release the movement keys to slow down and stop. Press Play again to exit Play mode before making persistent scene edits.

Diagonal movement has the same maximum speed as movement along one axis. The camera follows the player without smoothing so the player stays centered. Head-on collisions stop the player; angled contact allows sliding along a wall with no intentional bounce.

## Structure

| Location | Responsibility |
| --- | --- |
| `Assets/Prototype/Character/SwimmerController.cs` | Keyboard input and velocity changes in `FixedUpdate` |
| `Assets/Prototype/Character/CenteredCamera.cs` | Camera follows its assigned target in `LateUpdate` |
| `Assets/Prototype/Character/SwimmerSlide.physicsMaterial2D` | Zero contact friction and zero bounciness |
| `Assets/Prototype/Editor/SwimmingCheck.cs` | Repeatable movement checks in the Unity editor |
| `Map` in the Ocean Floor hierarchy | Static seabed, rocks, boundaries, and landmarks |

The map uses the Square sprite supplied by the installed Unity 2D Sprite package. Static map objects do not need a separate runtime map script yet. The Interior scene is outside this prototype's scope.

## Tune Movement

Select **Player** in the Hierarchy, then edit **Swimmer Controller** in the Inspector while outside Play mode.

| Parameter | Current value | Meaning |
| --- | ---: | --- |
| Max Speed | 5 | Maximum speed in world units per second |
| Acceleration | 8 | Rate of velocity change while a direction is held |
| Deceleration | 7 | Rate of velocity reduction after releasing all directions |

From rest, reaching maximum speed takes about **0.625 seconds**. From maximum speed, releasing input takes about **0.71 seconds** to stop. The measured stopping distance with the current physics timestep is about **1.74 world units**; input processing can add a frame of latency. These are gameplay tuning values, not calibrated real-world diving measurements. Reversing direction retains momentum briefly rather than changing velocity instantly.

The player's Rigidbody2D has zero gravity, frozen rotation, interpolation, and continuous collision detection. Contact friction is separate from the movement controller's release deceleration.

To edit the map, expand **Map**, select a block, and adjust its Transform position or scale. Avoid placing solid blocks at the player spawn `(0, 0)`. Save the scene with **Ctrl+S**.

## Validation

- Compilation completed without errors or warnings on Unity 6000.6.4f1.
- Run **Diving Prototype > Check Swimming** for acceleration, diagonal speed, stopping time/distance, stationary stability, and reversal checks.
- Automated checks in actual Play mode injected keyboard input through the Input System and verified the speed cap, stopping within 0.8 seconds, approximately 1.74 units of travel after release, no subsequent drift, camera centering, and rock collision.
- Head-on stopping, no noticeable rebound, wall sliding, and swimming away from a wall were also checked during collision tuning.
- Subjective movement feel still requires manual playtesting. The automated checks temporarily enabled unfocused input and restored the original settings afterward.

## Scope and Collaboration

Oxygen, mining, device placement, inventory, economy, story, dialogue, and room transitions are not implemented. Coordinate before editing shared scenes, packages, or project settings. Commit Unity asset `.meta` files together with their assets; generated `Library`, `Temp`, logs, and local editor settings are excluded by the existing `.gitignore`.

The previously missing `Assets/Welcome/2d-template.png` was restored locally with a SHA256 matching its existing Git LFS pointer and successfully reimported as a 1400 x 400 texture. The existing LFS object was uploaded to origin and retrieved into a separate local verification cache; the downloaded object matched the expected SHA256.
