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

The original Ocean Floor map uses the Square sprite supplied by the installed Unity 2D Sprite package. Its static map objects do not need a separate runtime map script. The Ocean Interactables scene and its Interior transition are described below.

## Tune Movement

Select **Player** in the Hierarchy, then edit **Swimmer Controller** in the Inspector while outside Play mode.

| Parameter | Current value | Meaning |
| --- | ---: | --- |
| Max Speed | 2.5 | Maximum speed in world units per second |
| Acceleration | 1 | Rate of velocity change while a direction is held |
| Deceleration | 1.5 | Rate of velocity reduction after releasing all directions |

From rest, reaching maximum speed takes about **2.5 seconds**. From maximum speed, releasing input takes about **1.67 seconds** to stop. A Windows Play mode check measured about **2.06 world units** of travel after release; input processing can add a frame of latency. These are gameplay tuning values, not calibrated real-world diving measurements. Reversing direction retains momentum briefly rather than changing velocity instantly.

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

The saved scenes currently use a 0.5-unit player light radius. `Player Light Radius` controls it directly; no sprite-coverage minimum is imposed. Changes in Play mode apply on the next frame, and changes made while stopped persist. Both diagonal beam edges are true tangents to that circle, with their perpendicular distance from its center equal to the radius. The short base passes through the tangent points behind the player center: at the current 60° opening and 0.5-unit radius it is 0.25 units behind the center. The circle covers that internal base, giving the visible outline a smooth tangent join. Both outlines share a 0.12-unit outward feather. A single brightness field covers the combined shape: everything inside the circle is fully bright, and the beam begins fading smoothly beyond the circle edge over a 0.5-unit transition. This removes the visible brightness ring at the circle-beam join. After that transition the fade is exponential, with the tunable half-strength distance controlling its decay and no far cutoff. Rocks still block the combined light. Alpha, sprite tint, and sorting are preserved; brightness values are RGB multipliers rather than calibrated display luminance.

Mouse mode aims independently of swimming. Movement Keys mode uses the swimmer's existing normalized input, including diagonals; it faces the held direction rather than the current velocity. Both modes retain the last direction when their input is absent or zero, including a cursor overlapping the player. Initial direction is right. There is no in-game mode switch. Camera following runs before flashlight aiming.

Add a new solid **BoxCollider2D** to **Blockers** while stopped. Offsets, Z rotation, scale (including negative scale), and enabled/active state are respected; geometry refreshes each frame without per-frame managed allocations. Match the visual sprite to the collider footprint. A blocker on the same GameObject as a SpriteRenderer is excluded only when shading that sprite, keeping the rock itself visible while shadowing the ground behind it. Other rocks can shadow its surface. Player, seabed, and landmarks do not block light. The player cannot be registered as a blocker, and lists exceeding 32 report an editor validation error.

The existing Ocean Floor global light is disabled. The flashlight uses a runtime material instance rather than global shader settings; disabling the controller restores the original materials, property blocks, and camera background. No shared renderer settings or Interior assets were changed. Partial light transmission, additional lights, and volumetric effects are not implemented.

## Validation

- Compilation completed without errors or warnings on Unity 6000.6.4f1.
- Run **Diving Prototype > Check Swimming** for acceleration, diagonal speed, stopping time/distance, stationary stability, and reversal checks. These math checks use the original 5 / 8 / 7 tuning rather than reading the current scene parameters.
- An independent Windows Play mode check injected keyboard input through the Input System and verified the current scene's 2.5 speed cap, zero velocity after a 2.1-second release window, approximately 2.06 units of travel after release, no subsequent drift, camera centering, and east-rock collision.
- Head-on stopping, no noticeable rebound, wall sliding, and swimming away from a wall were also checked during collision tuning.
- Run **Diving Prototype > Check Flashlight** outside Play mode for the bright circle and tangent trapezoid, tangent geometry and seamless brightness at the join, shared edge feathering, exponential distance fade, transformed and disabled blockers, saved scene setup, sprite tint/alpha, and actual GPU brightness/shadow checks. It also runs the existing swimming checks.
- Enter Play mode in Ocean Floor and run **Diving Prototype > Check Flashlight In Play Mode** for direct Inspector radius changes (including zero), a bright center with unexposed rear sprite corners at small radii, independence from player sprite scale, all eight WASD aim directions, arrow-key diagonal aiming, mouse projection after camera following, idle/overlap retention, and actual ocean pixels before/behind/around the east rock. It also verifies the illuminated rock surface, self-blocker IDs, tunable ambient, and camera background. The check temporarily pauses and injects synthetic input devices, then restores the original controls, camera, player position/velocity, and pause state, including on failure.
- Both flashlight checks passed on Unity **6000.6.4f1**, URP **17.6.0**, and the macOS Metal renderer. GPU samples in a linear floating-point target matched 50% strength at 10 units and 25% at 20 units within 0.005; unexposed/shadowed samples were zero. Rotated/scaled/offset boxes, disabled blockers, outline feathering, sprite tint/alpha, self-shadow exclusion, and other-rock blocking passed. Play mode scene checks and the existing swimming checks passed; the implementation compiled without script or shader warnings/errors.
- A captured Play mode frame was visually inspected: the bright circle, widening 60° beam, lit east rock, black shadow behind it, and light passing around its corners are visible. Subjective movement and flashlight feel still require manual playtesting. The automated checks temporarily enabled unfocused input and restored the original settings afterward.

## Scope and Collaboration

Oxygen, economy, story, dialogue, and persistent saves are not implemented. Coordinate before editing shared scenes, packages, or project settings. Commit Unity asset `.meta` files together with their assets; generated `Library`, `Temp`, logs, and local editor settings are excluded by the existing `.gitignore`.

## Ocean Interactables

Open **Assets/Scenes/Ocean Interactables.unity** and press Play. The original **Ocean Floor** scene remains the flashlight regression scene. The new scene keeps the teammate's movement tuning (2.5 / 1 / 1.5), mouse aiming, 60-degree beam, half-strength distance 2, and player light radius 0.5.

- **WASD / arrow keys:** swim. Polygon kelp is passable and caps speed at 50%; overlapping kelp does not stack. Polygon rocks and chasms block movement. Rocks and kelp obscure the flashlight; chasms transmit it.
- **SPACE:** interact with the closest unobstructed target within 1.8 units. Pick up placeholder items, enter the central submarine, or retrieve a deployed mining machine and its tellurium. The closest target has a yellow outline.
- **X:** deploy a machine at the closest mining spot. There are three reusable machines and one machine per spot; retrieving returns it. Fixed circular spots have tiers 1–5. Yield is `floor(elapsed seconds * tier / 60)`; retrieving discards an unfinished fractional item.
- **Tap Z:** on release before 0.35 seconds, reveal all on-screen mining spots with cyan outlines that fade over 1.5 seconds. The cooldown is 2 seconds. The scan is independent of flashlight occlusion.
- **Hold Z:** after 0.35 seconds, show direction arrows for the submarine and every uncollected item, including off-screen targets. Arrow placement encodes actual distance: inner <10m, middle 10–20m, outer >=20m; guide circle lines are hidden. Both target types use the same bands; thresholds are adjustable on Ocean HUD. Only the submarine and nearest item get names/distances; collinear labels are separated. Release hides radar without triggering a scan. The prototype treats one Unity unit as one meter.

The reference map informs the placeholder layout; its scale is provisional. The scene has six randomly positioned items, 28 fixed mining spots, six kelp areas, 11 rocks, two chasms and four boundaries. Random pickups avoid obstacles and other targets. The shared local silhouette atlas supplies circular spots, a player ring and arrow graphics. TextMesh Pro essentials supply the existing UI package's font resources.

The existing **Interior** scene loads additively when entering the submarine. **SPACE** returns to the ocean. Its room gameplay is still a placeholder. Items, inventory and deployed mining timers survive this round trip; a new Play session resets them.

Tune interaction distance, hold threshold, cooldown, machine count and random spawn bounds on **Player > Ocean Interactions**. Edit fixed spots and polygon shapes under **Map**. Kelp must be referenced by **Swimmer Controller > Kelp Zones** and **Flashlight Controller > Polygon Blockers**; rocks belong only in Polygon Blockers, and chasms in neither. The flashlight supports 32 box blockers plus 256 polygon edges; this scene uses four boxes and 138 edges.

Verification on Windows / Unity 6000.6.4f1 / D3D12:

- **Diving Prototype > Check Ocean Interactions** checks tier yields, duplicate retrieval, placement access, tap versus hold, cooldown, viewport filtering, fading, obstacle/scene wiring, and polygon GPU shadows/self exclusion/reveal.
- **FlashlightCheck.Run()** still passes the original saved-scene, swimming and GPU regression checks.
- **OceanPlayCheck.Main()** in a fresh Play session passed 35 checks through the real player loop: 2.5 open-water speed, 1.25 kelp speed, rock/chasm contact, keyboard Z/X/SPACE, two-label radar and collinear spacing, actual distance bands with larger/brighter near arrows and hidden guide lines, simultaneous Z+D swimming, pickups, resource accounting and an actual Interior round trip with preserved state. The mining check advances its start timestamp by a minute; it does not wait a full real minute. This check changes session inventory; restart Play afterward.
- To repeat the Play check, enter a fresh, unpaused Play session and choose **Diving Prototype > Check Ocean In Play Mode**. The check temporarily enables background input and selects a synthetic keyboard, then restores the input/background settings and removes its callbacks/device. Avoid manual controls during the check; the Console reports completion or an exception.
- A screen capture including the radar and HUD was visually inspected. Low-end hardware performance, a standalone player build and final map scale/feel have not been tested.

Requirement acceptance and pending machine-tracking, key-comfort and indicator ideas are recorded in [OCEAN-INTERACTABLES-NOTES.md](OCEAN-INTERACTABLES-NOTES.md).

The previously missing `Assets/Welcome/2d-template.png` was restored locally with a SHA256 matching its existing Git LFS pointer and successfully reimported as a 1400 x 400 texture. The existing LFS object was uploaded to origin and retrieved into a separate local verification cache; the downloaded object matched the expected SHA256.
