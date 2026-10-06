# Underwater Scene Prototype

This prototype covers the Ocean Floor assignment: a 2D top-down view, a centered player, swimming movement, a simple character/map structure, square placeholders, and a directional flashlight with a bright player circle, a widening trapezoidal beam, and solid-rock shadows.

## Requirements

- Unity Editor **6000.6.4f1** (upgraded from 6000.6.2f1).
- Git LFS for the repository's existing image assets.
- The package manifest includes `com.unity.pipeline` **0.8.0-exp.1** for local editor automation. Teammates can play and edit the prototype using Unity without installing the Unity CLI or Codex.

## Play

1. Open `Assets/Scenes/Ocean Floor.unity` in Unity.
2. Press **Play**, then click the **Game** view to give it keyboard focus.
3. Move with **WASD** or the **arrow keys**. The yellow square is the player. Aim the flashlight by moving the mouse; a small fully bright circle lights the player’s center, and the world beyond the circle and beam is black by default.
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
| `Assets/Prototype/Lighting/FlashlightController.cs` | Player-centered aiming, scene-local material state, and collider geometry uploads |
| `Assets/Prototype/Lighting/FlashlightMath.cs` | Repeatable cone, distance fade, and transformed-box intersection calculations |
| `Assets/Prototype/Lighting/OceanFlashlight.shader` and `.mat` | Sprite lighting and per-pixel solid-rock shadows |
| `Assets/Prototype/Editor/FlashlightCheck.cs` | Math, saved scene wiring, and GPU pixel checks |
| `Assets/Prototype/Editor/FlashlightPlayCheck.cs` | Play mode controls, camera following, and actual ocean lighting checks |
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

## Tune Flashlight

Select **Player > Flashlight Controller** in the Inspector while outside Play mode. Lighting and shadows update during Play mode; the saved material provides only a fixed right-facing beam and player circle in Edit mode.

| Parameter | Default | Meaning |
| --- | ---: | --- |
| Aim Mode | Mouse | Aim toward the cursor, or select Movement Keys to face the held WASD/arrow direction |
| Ambient Brightness | 0 | Brightness multiplier outside the cone and behind rocks; set 0.05 for a 5% ambient floor |
| Cone Angle | 60° | Angle between the widening trapezoid sides, with a shared 0.12-unit feather outside each edge |
| Player Light Radius | 0.1 | Fully bright circle radius in world units; used directly regardless of sprite size. Zero disables the circle. |
| Half Strength Distance | 10 (scene currently tuned to 2) | World distance at which the flashlight contribution drops to 50% |
| Aim Camera | Main Camera | Camera used to project cursor position onto the player's world plane |
| Sprite Material | OceanFlashlight | Shared source for a runtime material instance local to this scene |
| Map | Map | Existing map sprites that receive flashlight lighting |
| Blockers | 11 entries | The seven solid rocks and four boundaries; maximum 32 |

The player circle currently has a 0.1-unit radius, smaller than the sprite. `Player Light Radius` controls it directly; no sprite-coverage minimum is imposed. Changes in Play mode apply on the next frame, and changes made while stopped persist. Both diagonal beam edges are true tangents to that circle, with their perpendicular distance from its center equal to the radius. The short base passes through the tangent points behind the player center: at the current 60° opening and 0.1-unit radius it is 0.05 units behind the center. The circle covers that internal base, giving the visible outline a smooth tangent join. Both outlines share a 0.12-unit outward feather. A single brightness field covers the combined shape: everything inside the circle is fully bright, and the beam begins fading smoothly beyond the circle edge over a 0.5-unit transition. This removes the visible brightness ring at the circle-beam join. After that transition the fade is exponential, with the tunable half-strength distance controlling its decay and no far cutoff. Rocks still block the combined light. Alpha, sprite tint, and sorting are preserved; brightness values are RGB multipliers rather than calibrated display luminance.

Mouse mode aims independently of swimming. Movement Keys mode uses the swimmer's existing normalized input, including diagonals; it faces the held direction rather than the current velocity. Both modes retain the last direction when their input is absent or zero, including a cursor overlapping the player. Initial direction is right. There is no in-game mode switch. Camera following runs before flashlight aiming.

Add a new solid **BoxCollider2D** to **Blockers** while stopped. Offsets, Z rotation, scale (including negative scale), and enabled/active state are respected; geometry refreshes each frame without per-frame managed allocations. Match the visual sprite to the collider footprint. A blocker on the same GameObject as a SpriteRenderer is excluded only when shading that sprite, keeping the rock itself visible while shadowing the ground behind it. Other rocks can shadow its surface. Player, seabed, and landmarks do not block light. The player cannot be registered as a blocker, and lists exceeding 32 report an editor validation error.

The existing Ocean Floor global light is disabled. The flashlight uses a runtime material instance rather than global shader settings; disabling the controller restores the original materials, property blocks, and camera background. No shared renderer settings or Interior assets were changed. Partial light transmission, additional lights, and volumetric effects are not implemented.

## Validation

- Compilation completed without errors or warnings on Unity 6000.6.4f1.
- Run **Diving Prototype > Check Swimming** for acceleration, diagonal speed, stopping time/distance, stationary stability, and reversal checks.
- Automated checks in actual Play mode injected keyboard input through the Input System and verified the speed cap, stopping within 0.8 seconds, approximately 1.74 units of travel after release, no subsequent drift, camera centering, and rock collision.
- Head-on stopping, no noticeable rebound, wall sliding, and swimming away from a wall were also checked during collision tuning.
- Run **Diving Prototype > Check Flashlight** outside Play mode for the bright circle and tangent trapezoid, tangent geometry and seamless brightness at the join, shared edge feathering, exponential distance fade, transformed and disabled blockers, saved scene setup, sprite tint/alpha, and actual GPU brightness/shadow checks. It also runs the existing swimming checks.
- Enter Play mode in Ocean Floor and run **Diving Prototype > Check Flashlight In Play Mode** for direct Inspector radius changes (including zero), a bright center with unexposed rear sprite corners at small radii, independence from player sprite scale, all eight WASD aim directions, arrow-key diagonal aiming, mouse projection after camera following, idle/overlap retention, and actual ocean pixels before/behind/around the east rock. It also verifies the illuminated rock surface, self-blocker IDs, tunable ambient, and camera background. The check temporarily pauses and injects synthetic input devices, then restores the original controls, camera, player position/velocity, and pause state, including on failure.
- Both flashlight checks passed on Unity **6000.6.4f1**, URP **17.6.0**, and the macOS Metal renderer. GPU samples in a linear floating-point target matched 50% strength at 10 units and 25% at 20 units within 0.005; unexposed/shadowed samples were zero. Rotated/scaled/offset boxes, disabled blockers, outline feathering, sprite tint/alpha, self-shadow exclusion, and other-rock blocking passed. Play mode scene checks and the existing swimming checks passed; the implementation compiled without script or shader warnings/errors.
- A captured Play mode frame was visually inspected: the bright circle, widening 60° beam, lit east rock, black shadow behind it, and light passing around its corners are visible. Subjective movement and flashlight feel still require manual playtesting. The automated checks temporarily enabled unfocused input and restored the original settings afterward.

## Scope and Collaboration

Oxygen, mining, device placement, inventory, economy, story, dialogue, and room transitions are not implemented. Coordinate before editing shared scenes, packages, or project settings. Commit Unity asset `.meta` files together with their assets; generated `Library`, `Temp`, logs, and local editor settings are excluded by the existing `.gitignore`.

The previously missing `Assets/Welcome/2d-template.png` was restored locally with a SHA256 matching its existing Git LFS pointer and successfully reimported as a 1400 x 400 texture. The existing LFS object was uploaded to origin and retrieved into a separate local verification cache; the downloaded object matched the expected SHA256.
