# Ocean Scene Interactables: Acceptance and Design Notes

Updated: 2026-10-17. Playable scene: `Assets/Scenes/Ocean Interactables.unity`.

## Requirement Checklist

These features are implemented in the prototype. Mouse interactions and the always-on top-right minimap replace the original keyboard interactions and player-centered radar.

- [x] Polygon kelp is passable and halves maximum swimming speed. Play checks measured 2.5 in open water and 1.25 inside kelp. Overlapping kelp does not stack the slowdown.
- [x] Polygon rocks block movement. Play checks verify actual collision contact and prevent the player from entering the rock.
- [x] The closest interactable has a yellow outline. Selection currently considers unobstructed targets within 1.8 units.
- [x] The central submarine is at `(0, 0)`. Left-click nearby to load the existing Interior scene; left-click inside to return to the ocean.
- [x] Initially 15 placeholder pickup items receive random positions each new Play session, avoiding obstacles and other targets. Left-click collects the closest highlighted item. Change Player > Ocean Interactions > Item Count before entering Play to adjust quantity.
- [x] There are 37 fixed circular tellurium mining spots matching the reference map, with levels 1–5. Each level produces that many items per minute.
- [x] Left-click an empty mining spot to place a machine and start its timer. Each spot can hold one machine at a time.
- [x] Left-click a deployed machine to retrieve it and its completed yield: `floor(elapsed seconds * level / 60)`. Retrieving twice cannot duplicate resources.
- [x] Right mouse button press immediately triggers prospecting. All on-screen mining spots are highlighted, the highlight fades over 1.5 seconds, and the cooldown is 2 seconds. Holding the button does not automatically repeat scanning. Short Z presses no longer scan; X and Space no longer interact.
- [x] An always-on circular minimap shows the submarine and every uncollected item. It is north-up and player-centered; out-of-range points clamp to the border. Z no longer activates radar or prospecting.

The player ring follows the original reference. The former directional arrows are replaced by minimap points; submarine distance updates as the player moves. The prototype treats one Unity unit as one meter.

Verification uses `OceanInteractionCheck`, `FlashlightCheck`, and `OceanPlayCheck` running through the actual player loop. These cover pickups, mining retrieval, submarine round trips with preserved state, distance text, and keyboard behavior. The mining check adjusts its placement timestamp to simulate one minute rather than waiting a full minute.

The original 35-assertion Play check has been updated for mouse controls, the expanded map, reachable item spawning, and fixed viewport behavior. Validation results for the demo are recorded below. Minimap checks temporarily arrange targets; normal random positions are restored afterward.

Prototype defaults are not additional requirements: three reusable machines, a 1.8-unit interaction range, a 1.5-second fade, placeholder artwork, and map scale remain adjustable. Interior room content is a placeholder. Scene transitions are verified; submarine room gameplay is not implemented.

## Previous Radar Presentation (Replaced)

The earlier hold-Z radar used three distance bands and labels for the submarine and nearest item. The minimap replaces that presentation and hold input. Old arrow/band tuning controls are no longer exposed on Ocean HUD.

## Open Decision: Tracking Deployed Machines

This is beyond the current requirement and has not been implemented. The minimap currently does not track deployed machines.

- Decide whether retrieving machines should intentionally depend on memory, landmarks, and route planning.
- If memory is part of the challenge, retain the current rule without automatic machine directions. Test whether players can navigate back using the map and visual landmarks, and whether losing a machine has a reasonable cost.
- If continued exploration and efficient retrieval matter more, consider a distinct arrow/icon for each deployed machine, removed upon retrieval. Undeployed mining spots should remain separate from machine tracking and use prospecting highlights.
- If tracking is added, preserve the text limit. One option is to replace the nearest-item label with the nearest recoverable target, either an item or a machine. This changes the agreed display rule and needs a separate design decision.
- The [Miro concept board](https://miro.com/app/board/uXjVHm1YHQ8=/) describes machines mining over time, retrieval being required to collect minerals, and replacement machines in the shop. The related text reviewed does not explicitly define radar tracking. The possibility of losing a machine does not establish a requirement to rely on memory.

## Input Comfort

The always-on minimap removes the need to hold Z while swimming. Right click remains a press-triggered prospecting scan; left click remains interaction.

## Scope and Validation Limits

Chasms were added separately at the user's request: they block movement but transmit the flashlight. Kelp and rock occlusion also follow the concept map. These extend the original checklist.

Machine tracking, terrain display and marker grouping are not implemented. Standalone player builds, low-end hardware, final map scale, and prolonged keyboard comfort have not been tested.

## Full Draft Seafloor and Mouse Controls

- The provided designer image is the layout reference. Each reference square now maps to 1.6 Unity units. The initial 4-unit layout was reduced to 2, then to 1.6 for a viewport-coverage playtest. The submarine is the origin. The authored bounds are approximately 81.8 by 79.7 units; the map reduction preserved object sizes, swimming speed and interaction range. The later readability/cross-chasm trial below adjusts object sizes and camera coverage.
- The map contains 7 kelp regions, 14 rocks, 5 chasm/boundary regions, 37 mining spots, two crew-body placeholders, and one photo-quest placeholder. Placeholders do not implement story interactions.
- `DraftSeafloor.json` records calibrated coordinates, simplified polygon paths, tier positions and atlas rectangles. The sprite atlas is rasterized from these same paths; collider geometry matches the visible silhouettes. Rock and kelp use 197 polygon edges, below the shader's 256-edge limit. Chasms remain excluded from light blockers.
- Random placement uses a 1-unit flood-fill grid connected to the player start, with clearance and swept-circle checks against solid obstacles. Candidates must connect to a reachable cell and avoid kelp, solid geometry and other targets. If random attempts fail, placement searches verified reachable cells; an impossible placement disables the pickup and reports an error instead of leaving an inaccessible item.
- The flood-fill is a conservative spawn/route check, not an AI navigation system. It may reject narrow passages that could fit the player; item spawning favors reliably accessible water.
- Left click acts on the nearest unobstructed highlighted target within 1.8 units, rather than requiring a mouse hit on its sprite. All prompts use the new controls.
- Saved map/player sprites use the package's Sprite-Unlit-Default material so terrain colors remain visible while editing. The FlashlightController switches them to its scene-local flashlight material during Play and restores the preview materials when disabled. Preview visibility does not change gameplay illumination.

## Fixed 16:9 View

- Default desktop output is 1920x1080. The HUD uses a 1280x720 reference layout. Both are 16:9; the reference layout is not a restriction on output resolution.
- The ocean camera keeps orthographic size 9: world coverage is 32 by 18 units. Wider or taller windows fit this same view with black bars.
- A background camera clears black bars. The HUD renders through the active gameplay camera and CanvasScaler Expand fits the reference layout inside its viewport. The Interior round trip also uses the fitted viewport.
- Black-bar clicks do not interact or prospect. Mouse aiming retains its previous direction outside the gameplay viewport; aim and minimap placement use the actual camera viewport.
- API references: [Camera viewport](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera-rect.html), [CanvasScaler](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-CanvasScaler.html), and [PolygonCollider2D paths](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PolygonCollider2D.SetPath.html).

## Always-On Minimap: First Playable Version

- North-up and player-centered, anchored at the top right inside the fitted gameplay viewport. Diameter: 180 reference pixels (270 output pixels at 1080p). World radius: 25 units, adjustable on Ocean HUD > Minimap Range before Play.
- Central white player dot with a small facing arrow following flashlight aim without rotating the map. N marks north.
- Cyan diamond for the submarine and white circular item dots. In-range positions scale linearly with relative world coordinates; distant points clamp to the circle border and become slightly dimmer. Marker extents remain inside the circle.
- Only submarine distance is shown below the circle. No per-item labels. Collected items disappear immediately; inactive targets are hidden.
- The submarine marker draws above item dots. Dense border dots may overlap; they are not grouped or displaced, preserving true bearings for this visual trial.
- Mining spots and deployed machines are not tracked. No terrain is drawn. Right-click prospecting remains unchanged.
- The minimap hides inside Interior and returns outside. It uses the existing camera-space Canvas and does not intercept mouse input.
- Replaced the player-centered radar and removed its three-band arrow behavior, hold-Z state and HUD hint. Z has no action.

## Demo Validation

- Previous 2-unit-scale editor-menu Play verification passed **106 assertions** on Unity 6000.6.4f1, including runtime replacement of the unlit editing materials and machine placement/retrieval in the smaller quest alcove. At that scale, editor checks verified 2-unit guide spacing and 0.7-unit mining-center clearance. The native scene/GPU checks pass; the original FlashlightCheck passed before this scene-only scale adjustment. The saved colored preview and restoration after leaving Play were verified. Current Console ground truth reports no errors or warnings; the Pipeline history retains earlier authoring warnings and a tooling timeout.
- The extended Play check covers mouse pickup/placement/retrieval, inactive X/Space, right-button press/cooldown/hold behavior, ignored Z input, resource accounting and state-preserving Interior transitions.
- All 30 randomized pickups and all 37 authored mining locations passed connected-water reachability checks. Physical swimming/contact checks include open water, a remote kelp region, rock and chasm boundaries; they are controlled movement segments rather than a complete human playthrough.
- Actual Game View sizes 1920x1080, 1280x720, 1920x1200 and 2560x1080 were exercised. Checks compare world coverage, viewport ratio and HUD reference dimensions, and verify that black-bar clicks do not interact/scan and that mouse aim accounts for the viewport offset.
- Full-map overview and gameplay captures were visually inspected. The HUD is explicitly sorted above terrain so camera-space UI cannot be obscured by high-order map sprites.
- Before the half-scale adjustment, short local Editor observations on an RTX 5070 Ti Laptop GPU were approximately 101/67/99 frames per second at 720p and 88/63/85 at 1080p, respectively near the submarine, inside remote kelp and in northeast water. Each sample spans about two seconds; Editor cadence, focus and other workloads affect these values. These are not current half-scale measurements, standalone-player performance guarantees or low-end benchmarks.
- Editor scene wiring and polygon GPU checks pass. The original Ocean Floor scene remains unchanged and continues to serve as the movement/flashlight regression scene.
- Standalone build, prolonged manual exploration, final map pacing/item balance and low-end hardware are not validated. No commit or push is included in this implementation step.

## 1.6-unit grid playtest

- Uniformly reduced terrain and map coordinates by 20%. All 37 mining spots retain their reference positions and tiers relative to the terrain. Player, target and machine sizes, movement, camera and interaction range remain unchanged.
- Offline reachable-area sampling estimates 91.4% viewport coverage, or 85.5% with a 1-unit screen-edge inset. These are area fractions, not gameplay-time probabilities.
- At the initial 1.6-unit scale trial, native sprite PPU was 10; polygon paths match the scaled visuals. All 30 authored pickup fallback positions were revalidated.
- Editor mining-center clearance is 0.6 units, exceeding the approximately 0.46-unit enclosing radius of the player. Play checks also exercise placement and retrieval in the narrow quest alcove.
- Placeholder edge-quality improvements remain separate.
- Validation: native editor and polygon GPU checks passed. The final Play run passed 106 assertions, including all 37 mining routes, quest-alcove placement/retrieval, mouse controls, viewport checks and submarine state preservation. Earlier attempts exposed one timing-sensitive synthetic right-click failure and a randomized neighboring-pickup test assumption; the pickup test now selects an isolated target. Scene mining positions and tiers were also compared directly with the reference JSON. No standalone build or full manual route playthrough was performed.

## Smooth silhouettes and wider-view trial

- Kept the 1.6-unit grid map. Increased ocean camera orthographic size to 7.5 (26.67 x 15 units). Screen-space world objects are approximately 13% smaller than at height 13; HUD reference size remains unchanged. No additional target or collision scaling was applied.
- Re-rasterized terrain from the existing collision polygons at 40 PPU with four-subscanline area coverage. The 8192-square atlas uses BC7 on the current desktop target (about 64 MiB of compressed texture data). This increases texture memory relative to the old atlas; low-end and other-platform builds have not been profiled.
- Rebuilt the circle and ring at 256 PPU with fractional alpha edges. Their world-space sizes and asset GUIDs are preserved; other scenes using these shared sprites also receive the clearer graphics.
- Enabled high-quality SMAA on the ocean camera. This complements the source silhouette improvements for rendered edges.
- Rebuild via Diving Prototype > Rebuild Smooth Ocean Silhouettes with the ocean scene open outside Play. This is an editor-only bake and introduces no runtime generation or external package. The JSON atlas coordinates reflect the new pixel density.
- Orthographic camera distance along Z does not change the visible area. Increasing orthographic size widens the view without shortening routes or narrowing geometry; it also makes targets visually smaller and expands screen-based prospecting. The earlier coverage estimate used height 13 and is not a measured probability for this new view.
- Validation for the wider view: editor wiring and polygon GPU checks passed; the final Play run passed 106 assertions across 1920x1080, 1280x720, 1920x1200 and 2560x1080. Source alpha checks found fractional edge coverage in both baked textures. Saved-color previews and a live 1920x1080 prospecting screenshot were inspected. Standalone builds and low-end GPU performance were not tested.

## Smaller objects and cross-chasm visibility trial

- Kept the map at 1.6 units per design square and preserved all fixed coordinates. Set ocean orthographic size to 9: 32 x 18 units visible.
- Player root scale is 0.75, including its collider (0.4875-unit square in world space); its local light radius is 0.375. Pickup scale is 80% and mining spot scale is 85% of the previous version, including child machine/highlight visuals. The submarine retains its landmark size. HUD size, interaction range and swimming speeds are unchanged.
- Relative to the height-15 version, the combined camera and size changes make the player 37.5% smaller on screen, pickups about 33% smaller, and mining spots about 29% smaller. At 1080p the player ring is about 45 pixels across, pickup squares about 31 pixels, and mining spots about 56 pixels.
- Added a real cross-chasm sightline check from approximately (-10.74, -27.09) to (-10.74, -19.15). This opposite-bank point was beyond the height-15 vertical view; the height-18 view includes it. A rendered GPU pixel verifies that flashlight light reaches the opposite bank. This is one representative crossing, not a guarantee that every wide or diagonal chasm fits in a centered view.
- Lighting decision: retain the existing 2-unit half-strength distance, 60-degree cone and zero ambient brightness. Only the small fully lit player circle follows the resized character; distant-light attenuation is unchanged.
- Validation: 109 Play assertions passed, including the real cross-chasm sightline, opposite-bank viewport inclusion and GPU illumination sample, all mining routes, collision/kelp behavior and submarine round trip. Both selected bank points were separately verified outside the chasm polygon. The cross-bank screenshot was inspected: the opposite shore remains dim with the intentionally unchanged light falloff. No standalone build, low-end profiling or exhaustive all-shores visibility check was performed. The final run follows a fixture correction to interact at the submarine center, avoiding randomly nearby pickups.

## Minimap Acceptance

- Final editor-menu Play run: **122 assertions passed** on Unity 6000.6.4f1. Covers always-on visibility, ignored Z input, linear/diagonal positions, exact-range and out-of-range border clamping, north-up behavior while aiming, player-facing indicator, marker colors, pickup removal, non-blocking UI, 720p/1080p/16:10/ultrawide placement, resource accounting, submarine transitions and the cross-chasm regression.
- Editor checks verify the saved 25-unit range, 180-pixel top-right layout and zero/in-range/out-of-range projection. Polygon GPU checks pass. A separate live check moved the player to (3,4) and confirmed the submarine summary updated to 5m.
- The first minimap Play attempt hit a test timing assumption: the shortened display checks reached the Z test before the previous right-click scan cooldown finished. The test now waits for the actual remaining cooldown before checking that Z cannot initiate another scan; the final run passed.
- A fresh 1920x1080 gameplay screenshot was inspected. Sparse interior dots and denser border groups remain readable; no grouping or false-bearing offsets are applied. Screenshots are local review artifacts under ignored Temp/coverage-analysis.
- No standalone build, low-end performance test or extensive human navigation trial was performed. Machine tracking and terrain display remain outside this version.

## Pickup Density Trial

Reduced the default pickup count from 30 to 15. The scene and component default both use 15; the authored pool remains available for adjusting the count. At runtime only the configured count is enabled and tracked, giving 15 item points plus the submarine. The 32 x 18 view and 25-unit minimap radius are unchanged. Earlier 30-item validation records describe the previous density.
- Density verification: editor/GPU checks passed; a fresh Play session produced exactly 15 active pickups and 16 minimap target markers, and all pickup positions passed reachable/clear spawn checks. The camera height remained 18. The entire Play regression suite was not rerun for this count-only adjustment.

## Performance Pass

The flashlight now filters polygon edges against a conservative rectangle enclosing the gameplay viewport and light origin. Shader work is skipped for transparent, fully revealed or fully ambient-lit fragments. All original blocker capacity checks and self-shadow owner IDs remain intact.

At three 1080p desktop Editor sample locations, the active edge count fell from 197 to 14-43 and median GPU frame time fell by approximately 49-68%. Whole-frame medians overlap across runs; no comparable overall FPS gain is claimed. Three deterministic world-only before/after captures matched exactly, with zero changed pixels. See [performance notes](OCEAN-PERFORMANCE-NOTES.md) for method, raw-artifact locations and limits.
- Final performance-pass regression: 122 Play assertions passed, plus ocean editor/GPU and original flashlight checks. A preceding synthetic mouse-offset test failure did not reproduce in a fresh session; diagnostics were expanded without changing production input logic. Full context is recorded in the performance notes.
