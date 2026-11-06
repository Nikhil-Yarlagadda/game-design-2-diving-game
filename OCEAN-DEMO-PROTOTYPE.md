# Ocean Scene Demo Prototype

This update continues `prototype/ocean-interactables` and completes the Ocean Scene Demo Prototype task: the always-on minimap, mouse controls, and draft seafloor map. It builds on the existing movement, flashlight, mining, and submarine prototype.

## Run the Demo

Open `Assets/Scenes/Ocean Interactables.unity` in Unity 6000.6.4f1 and press Play. Use the **Game** tab to play. Fit the Game view to the editor window using its Scale slider; an enlarged preview can crop the HUD and minimap without changing the actual game camera.

The original `Ocean Floor` scene remains available for flashlight regression checks. `Interior` is the existing submarine destination.

## Controls

- **WASD / arrow keys:** swim. Mouse position controls flashlight aim.
- **Left click:** interact with the nearest unobstructed highlighted target within 1.8 units. Pick up an item, deploy a machine at an empty mining spot, retrieve a deployed machine and its completed yield, or enter the submarine. Left click inside the submarine returns to the ocean.
- **Right click:** scan all mining spots currently on screen. Highlights fade over 1.5 seconds, with a 2-second cooldown. Holding the button does not repeat scanning.
- **Z, X and Space:** the former radar/interaction bindings have been removed.

## Hydrox Tank

The player starts with a full **180-unit Hydrox tank**, consuming **1 unit per second** during ocean gameplay (a three-minute default dive). At zero, the player automatically enters the submarine from any ocean position. Manual and forced entry use the same guarded additive transition. Returning to the ocean refills the tank before swimming resumes. Consumption stops inside Interior, during transitions, and while game time is paused.

Inventory, randomized pickup positions and deployed machines survive both kinds of round trip. Every departure from the submarine starts a new dive at the player's original map spawn (currently `(0, -1.5)`), with a full tank and zero velocity. This also applies after depletion: continuing past the Interior placeholder does not resume at the depletion location. There is no death penalty, refill cost, upgrade system or save persistence. Tune **Max Capacity** and **Consumption Rate** on **Player > Hydrox Tank**; dive duration is capacity divided by consumption rate. Invalid settings clamp positive or restore finite defaults.

A 240 x 20 reference-pixel light blue bar shows remaining Hydrox at the top left, inset 20 pixels. Status text sits below it. The bar hides inside Interior and ignores pointer input. The current minimap, mouse controls, view, map and flashlight optimizations are retained when integrating [Hydrox System PR #2](https://github.com/Nikhil-Yarlagadda/game-design-2-diving-game/pull/2).

Interior must remain enabled in the build scene list. If unavailable, entry reports one configuration error. A depleted player stays stopped with a persistent message rather than repeatedly requesting loads.

## Minimap

The circular minimap is always visible at the top right while outside the submarine. It is north-up and player-centered, with a 25-unit radius and a diameter of 180 reference pixels. Its center arrow follows flashlight aim without rotating the map.

The submarine is a cyan diamond; uncollected items are white points. In-range positions reflect relative world coordinates. Out-of-range points stay on the circle border and are slightly dimmer. Only submarine distance is shown below the circle. Picked-up items disappear, and the minimap hides inside the submarine and returns on re-entry to the ocean.

The old player-centered arrows and hold-Z radar are replaced. Terrain and mining machines are not tracked. Border points can overlap; this first version preserves true bearings without grouping or displacing them.

## Draft Seafloor and Current Defaults

- Full draft layout based on the designer map, exceeding the task's minimum area near the submarine.
- 1.6 Unity units per reference grid square; bounds approximately 81.8 x 79.7 units, with the submarine at the origin.
- Seven passable kelp regions that halve speed and block light; 14 solid rocks that block light; five solid chasm/boundary shapes that do not block light; four outer boundary colliders.
- 37 fixed circular mining spots with tiers 1-5. Mining produces `tier` tellurium per minute. Three reusable machines are available; retrieval awards completed whole items and returns the machine. Duplicate retrieval cannot award extra resources.
- 15 randomly positioned pickup items per new Play session. Spawns avoid obstacles and targets and must connect to reachable water; verified fallback positions are available.
- Two crew-body markers and one photo-quest marker are visual placeholders only.

Movement remains tuned to a maximum of 2.5 units/second in open water and 1.25 in kelp. Items, inventory and deployed mining state survive the submarine round trip; they reset in a new Play session.

The current size trial uses 75% player scale, 80% pickup scale and 85% mining-spot scale relative to the earlier prototype. The player collider and local light circle follow the smaller character. Flashlight falloff remains at a 2-unit half-strength distance with a 60-degree cone.

## View and Visual Quality

The game view is fixed at 16:9. The ocean camera has Orthographic Size 9, showing 32 x 18 world units. Desktop defaults are 1920x1080; the HUD uses a 1280x720 reference layout. Other aspect ratios have black bars. HUD placement, mouse aim and input checks use the fitted game viewport; black-bar clicks are ignored.

Terrain silhouettes were rebuilt at 40 PPU from the same polygon paths as their colliders. Shared circle/ring sprites use 256 PPU, and the ocean camera uses SMAA High. Editing previews retain distinct terrain colors; entering Play enables flashlight lighting.

The editor-only menu **Diving Prototype > Rebuild Smooth Ocean Silhouettes** regenerates the silhouettes from the open ocean scene. The desktop terrain atlas uses approximately 64 MiB of BC7 texture data.

## Performance and Verification

The flashlight excludes shadow edges that cannot intersect any ray from the player to the visible gameplay area. Transparent and fully revealed fragments skip unnecessary lighting work. Self-shadow exclusion and the 256-edge capacity validation are preserved.

On an RTX 5070 Ti Laptop GPU, short 1080p Editor samples at three locations reduced active edges from 197 to 14-43 and median GPU frame time by approximately 49-68%. Overall frame-time samples overlap, so this is not a claim of the same improvement in FPS. Three fixed world-only before/after renderings matched exactly, with zero changed pixels.

The final performance-pass Play run passed 122 assertions. It covers input, minimap behavior, all mining routes, pickup/mining accounting, submarine state preservation, a real cross-chasm sightline, and 720p/1080p/16:10/ultrawide layouts. Ocean editor/polygon GPU checks and the original flashlight regression also passed. A preceding synthetic mouse-offset assertion failed once and did not reproduce in the fresh final run; its diagnostics were expanded without changing production input logic.

The current Hydrox/departure checks passed **150 Play assertions** on Windows / Unity 6000.6.4f1 / D3D12, including paused consumption, live bar fill at all four window sizes, manual and forced submarine round trips, departure at the original map spawn with zero velocity, refill, preserved inventory/mining/pickup positions, duplicate-entry prevention and missing-scene handling. Ocean editor checks (tank timing, configurable/invalid settings, saved HUD layout and polygon GPU rendering) and the original flashlight regression also passed. Depletion is accelerated with simulated elapsed time; this is not a full three-minute endurance run. The unavailable-scene test intentionally logs exactly one configuration error.

Repeat the checks outside Play with **Diving Prototype > Check Ocean Interactions**. For the full Play check, start a fresh, unpaused ocean session and choose **Diving Prototype > Check Ocean In Play Mode**. The Play check changes session inventory; restart Play afterward for manual testing.

## Remaining Limits

This is a playable draft with placeholder art, provisional scale and provisional item density. Story tasks, machine tracking, persistent saves, and terrain on the minimap are not implemented. Some wide chasms do not fit both shores in one centered view, and distant shores remain dim with the retained light falloff.

Standalone builds, low-end devices, long-duration thermal behavior and exhaustive human route playtests have not been verified. Editor measurements include editor overhead and do not establish performance on other hardware. Temporary screenshots and raw sampling files remain local, outside the committed source.

Further details: [acceptance and design notes](OCEAN-INTERACTABLES-NOTES.md), [performance comparison](OCEAN-PERFORMANCE-NOTES.md), and [prototype setup](UNDERWATER-PROTOTYPE.md).

## Updated mining demo controls (2026-10-19)

Stop moving and hold **E for three seconds** to prospect the current screen. Release early to cancel; completed scans have a five-second cooldown. Spots stay marked after discovery, but their displayed tiers and rate estimates become outdated as machines mine them. Re-prospect frequently to discover depletion. Tier rates are now 2 / 4 / 6 / 12 / 24 tellurium per minute, with finite reserves and automatic downgrades; see the current acceptance notes for tick intervals and capacities.

Move near a highlighted spot and left-click to deploy a machine. Left-click a highlighted deployed machine to retrieve both it and its stored ore. The mining tutorial appears on the first excursion, day 2. Later excursions receive a one-time delivery tutorial when the first shop purchases arrive. Its overnight warning is preparatory: overnight breakage is not yet implemented, and submarine visits preserve machines and mining progress. The player now uses the supplied transparent diver illustration, rotated to face the camera aim.

## Day flow and deliveries

New games start indoors on day 1 with **100 credits**. Bed departures start excursion days 2, 4, 6, and so on; leaving Results starts the next interior day. Purchased mining machines arrive as collectable packages within the submarine's configurable delivery radius (default 10 world units). All authored test pickups were removed. The first delivery tutorial uses radar guidance and remains visible until all delivered packages are collected. Uncollected deliveries expire on manual return or gas depletion, and later orders do not repeat the tutorial. Starting a new game resets tutorial eligibility and credits.
