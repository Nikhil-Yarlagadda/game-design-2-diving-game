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

Economy, story, dialogue, and persistent saves are not implemented. Ocean Interactables now includes the Hydrox tank described below. Coordinate before editing shared scenes, packages, or project settings. Commit Unity asset `.meta` files together with their assets; generated `Library`, `Temp`, logs, and local editor settings are excluded by the existing `.gitignore`.

## Ocean Interactables

Open **Assets/Scenes/Ocean Interactables.unity** and press Play. The original **Ocean Floor** scene remains the flashlight regression scene. The new scene keeps the teammate's movement tuning (2.5 / 1 / 1.5), mouse aiming, 60-degree beam and half-strength distance 2. Its current smaller-player trial uses a 0.375-unit local light radius.

- **WASD / arrow keys:** swim. Polygon kelp is passable and caps speed at 50%; overlapping kelp does not stack. Polygon rocks and chasms block movement. Rocks and kelp obscure the flashlight; chasms transmit it.
- **Left mouse click:** interact with the closest unobstructed highlighted target within 1.8 units. Pick up items, enter the submarine, place a machine at an empty mining spot, or retrieve a deployed machine and its tellurium. There are three reusable machines and one machine per spot. Yield is `floor(elapsed seconds * tier / 60)`; retrieving discards an unfinished fractional item.
- **Right mouse click:** immediately reveal all on-screen mining spots with cyan outlines that fade over 1.5 seconds. The cooldown is 2 seconds. Holding the button does not repeatedly scan. The scan is independent of flashlight occlusion.
- **Minimap:** always visible at the top right outside the submarine. North-up, player-centered, 25-unit radius, cyan submarine diamond and white uncollected-item points. Distant points clamp to the circle edge. Only submarine distance appears below the circle; the center arrow shows aim direction.

Z has no action. X and Space no longer interact. The minimap replaces the former hold-Z radar.

The full designer map is traced into draft polygon silhouettes and colliders. Each image square is now 1.6 Unity units; after testing 4 and 2 units, this trial uses bounds of approximately 81.8 by 79.7 units. The current readability trial scales the player to 75%, pickups to 80%, and mining spots (including machine visuals) to 85% of their earlier sizes; movement speed and map coordinates are unchanged. It has 15 randomly positioned items by default, 37 fixed mining spots, seven kelp areas, 14 rocks, five chasm/boundary shapes and four outer boundary colliders. Two crew-body markers and the photo-quest marker are non-interactive placeholders. Random items avoid obstacles/targets and must connect to water reachable from the player start. Map scale and density remain provisional.

Outside Play, sprites use the package's unlit preview material: green kelp, brown rocks and black chasms remain clearly distinguishable. Entering Play switches map/player sprites to the flashlight material; exiting Play restores the editing colors.

The ocean view is fixed at 16:9, with a 32 by 18-unit world view. Desktop defaults are 1920x1080; HUD reference layout is 1280x720. Other window ratios use black bars. HUD, minimap and mouse aim stay within the fitted view; black-bar clicks are ignored. The shared silhouette atlases supply draft terrain, circular spots, the player ring and arrow graphics. TextMesh Pro essentials supply UI fonts.

The existing **Interior** scene loads additively when entering the submarine. **Left click** returns to the ocean. Its room gameplay is still a placeholder. Items, inventory and deployed mining timers survive this round trip; a new Play session resets them.

Tune interaction distance, cooldown, machine count, item count and random spawn bounds on **Player > Ocean Interactions**, before entering Play. Edit fixed spots and polygon shapes under **Map**. Kelp must be referenced by **Swimmer Controller > Kelp Zones** and **Flashlight Controller > Polygon Blockers**; rocks belong only in Polygon Blockers, and chasms in neither. The flashlight supports 32 box blockers plus 256 polygon edges; this scene uses four boxes and 197 edges. If editing silhouettes, update the corresponding atlas graphics together with collider paths.

Verification on Windows / Unity 6000.6.4f1 / D3D12:

- **Diving Prototype > Check Ocean Interactions** checks tier yields, duplicate retrieval, placement access, minimap projection, cooldown, viewport filtering, fading, obstacle/scene wiring, fixed-aspect calculations and polygon GPU shadows/self exclusion/reveal.
- **FlashlightCheck.Run()** still passes the original saved-scene, swimming and GPU regression checks.
- **OceanPlayCheck.Main()** now checks mouse controls, inactive old bindings, connected random spawning, all reference mining routes, always-on minimap projection, border clamping and pickup removal and the Interior round trip. It also switches actual Game View sizes to check 720p, 1080p, 16:10 and ultrawide layouts, black-bar input rejection and offset mouse aim. Final results are recorded in the acceptance notes. The mining check advances its start timestamp by a minute; it does not wait a full real minute. This check changes session inventory; restart Play afterward.
- To repeat the Play check, enter a fresh, unpaused Play session and choose **Diving Prototype > Check Ocean In Play Mode**. The check temporarily enables background input and selects synthetic keyboard/mouse devices, then restores input/background settings and render size and removes its callbacks/devices. Avoid manual controls during the check; the Console reports completion or an exception.
- A screen capture including the minimap and HUD was visually inspected. Low-end hardware performance, a standalone player build and final map scale/feel have not been tested.

Requirement acceptance and pending machine-tracking, key-comfort and indicator ideas are recorded in [OCEAN-INTERACTABLES-NOTES.md](OCEAN-INTERACTABLES-NOTES.md).

The previously missing `Assets/Welcome/2d-template.png` was restored locally with a SHA256 matching its existing Git LFS pointer and successfully reimported as a 1400 x 400 texture. The existing LFS object was uploaded to origin and retrieved into a separate local verification cache; the downloaded object matched the expected SHA256.

The current camera trial uses **Main Camera > Orthographic Size = 9**. Compared with the previous height-15 view, the camera alone makes world objects about 17% smaller. Player and target scaling further improves the relative sizes. The player collider and local light circle follow the smaller player. Terrain silhouettes use 40 PPU and the shared circle/ring use 256 PPU; the ocean camera uses SMAA High. Rebuild the terrain and round markers with **Diving Prototype > Rebuild Smooth Ocean Silhouettes** outside Play after editing the collider outlines. The higher-resolution desktop terrain atlas uses approximately 64 MiB of BC7 texture data; platform-specific memory/performance tuning is pending.

Minimap tuning is on **Ocean HUD > Minimap Range** (default 25). Its reference diameter is 180 pixels. It displays no terrain or machines; crowded border dots remain ungrouped in this first version. It hides inside the submarine and returns outside.

Minimap acceptance: the current editor-menu Play check passed **122 assertions**, including four actual Game View sizes, pickup marker removal, ignored Z input, border clamping, aim-independent north-up coordinates and Interior hide/restore. The first 1080p gameplay view was visually inspected. Dense border points remain ungrouped and require playtesting; no standalone build or low-end profiling was performed.

The flashlight retains the global 256-edge validation limit but submits only edges whose bounds can intersect the current gameplay viewport/origin rectangle. This conservatively reduces shadow work without changing the reference gameplay pixels. Transparent and fully revealed fragments skip redundant lighting work. See [performance notes](OCEAN-PERFORMANCE-NOTES.md) for the short desktop Editor comparison; this is not standalone or low-end performance acceptance.

For the consolidated demo handoff, see [Ocean Scene Demo Prototype](OCEAN-DEMO-PROTOTYPE.md).

## Hydrox Tank

Ocean Interactables includes the Hydrox System from PR #2: 180 capacity, 1 unit consumed per second outside Interior, automatic submarine entry at zero, and full refill before ocean controls resume. Pickups, inventory and deployed machines survive the round trip. Every departure, including after depletion, returns the player to the original map spawn (currently `(0, -1.5)`) with a full tank and zero velocity. Interior remains a demo placeholder for the future submarine gameplay. The top-left light blue bar hides inside Interior. Max Capacity and Consumption Rate are editable on Player > Hydrox Tank.

Hydrox checks are part of **Diving Prototype > Check Ocean Interactions** and **Check Ocean In Play Mode**. There are no separate Hydrox check menus. The current Windows run passed 150 Play assertions, including original-spawn departure after both manual and forced return, plus editor/GPU and flashlight regression checks. The Play check accelerates depletion and intentionally emits one missing-scene error; restart Play afterward. See [the current demo handoff](OCEAN-DEMO-PROTOTYPE.md#hydrox-tank) for complete behavior, configuration and limits.
