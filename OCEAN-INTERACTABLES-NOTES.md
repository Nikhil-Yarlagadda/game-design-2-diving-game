# Ocean Scene Interactables: Acceptance and Design Notes

Updated: 2026-10-13. Playable scene: `Assets/Scenes/Ocean Interactables.unity`.

## Requirement Checklist

These features are implemented in the prototype. Radar text follows the agreed rule for avoiding clutter, described in the final checklist item.

- [x] Polygon kelp is passable and halves maximum swimming speed. Play checks measured 2.5 in open water and 1.25 inside kelp. Overlapping kelp does not stack the slowdown.
- [x] Polygon rocks block movement. Play checks verify actual collision contact and prevent the player from entering the rock.
- [x] The closest interactable has a yellow outline. Selection currently considers unobstructed targets within 1.8 units.
- [x] The hydrox tank starts full at 180 units and consumes 1 unit per second. Depletion forces submarine entry from any position; every return to the ocean refills fully. The top-left light blue bar shows remaining capacity and hides inside Interior.
- [x] The central submarine is at `(0, 0)`. Press Space nearby to load the existing Interior scene; press Space inside to return to the ocean.
- [x] Placeholder pickup items receive random positions each new Play session, avoiding obstacles and other targets. Space collects an item and removes it from the scene.
- [x] There are 28 fixed circular tellurium mining spots, with levels 1–5. Each level produces that many items per minute.
- [x] X places a mining machine and starts its timer. Each spot can hold one machine at a time.
- [x] Space near a deployed machine retrieves it and its completed yield: `floor(elapsed seconds * level / 60)`. Retrieving twice cannot duplicate resources.
- [x] A short Z press triggers prospecting on release. All on-screen mining spots are highlighted, the highlight fades over 1.5 seconds, and the cooldown is 2 seconds.
- [x] Holding Z for 0.35 seconds activates radar; releasing hides it. Radar shows directions to the submarine and every uncollected item, including off-screen targets. **Only the submarine and nearest item display distance text. This is the agreed display rule; distances are not shown for all items simultaneously.**

The player ring, directional arrows, and name/distance presentation follow the reference image. Its 100m and 10m values are examples; displayed distances update as the player moves. The prototype treats one Unity unit as one meter.

Verification uses `OceanInteractionCheck`, `FlashlightCheck`, and `OceanPlayCheck` running through the actual player loop. These cover pickups, mining retrieval, submarine round trips with preserved state, distance text, and keyboard behavior. The mining check adjusts its placement timestamp to simulate one minute rather than waiting a full minute.

The latest complete Play check passed 35 assertions, including positions near distance-band boundaries, size/opacity hierarchy, hidden guide lines, and simultaneous Z+D input. Editor and GPU checks also passed. Radar captures temporarily arranged nearby, middle-distance, and distant targets; normal random spawning was restored afterward.

Prototype defaults are not additional requirements: three reusable machines, a 1.8-unit interaction range, a 0.35-second hold threshold, a 1.5-second fade, placeholder artwork, and map scale remain adjustable. Interior room content is a placeholder. Scene transitions are verified; submarine room gameplay is not implemented.

## Agreed Radar Presentation

- Inner band: distance <10m. Middle band: 10m <= distance <20m. Outer band: distance >=20m.
- Submarine and item arrows use the same distance bands, rather than positions based on target-array order.
- Arrow angles follow the actual target direction. Guide circle lines are hidden; distance-based arrow placement remains active.
- Position communicates distance, color distinguishes target type, and size/opacity provide supporting cues. The bands retain wide spacing. Inner arrows are 22x26 reference pixels at 100% opacity, middle arrows 16x20 at 70%, and outer arrows 12x14 at 45%. A 1px dark outline separates arrows from the flashlight and terrain. Submarine arrows/text are light blue; item arrows/text are white. Distance text remains fully readable. Targets in the same band share the same size, so a submarine and item both at 6m belong to the inner band.
- Adjust the 10m/20m thresholds through `Ocean HUD > Near Distance / Far Distance`.
- Bands express approximate distance, not a scaled map. Targets at 20m and beyond share the outer band.
- Checks cover threshold boundaries, arrow updates when crossing bands, hidden guide lines, and the two-label limit.

## Open Decision: Tracking Deployed Machines

This is beyond the current requirement and has not been implemented. Radar currently does not track deployed machines.

- Decide whether retrieving machines should intentionally depend on memory, landmarks, and route planning.
- If memory is part of the challenge, retain the current rule without automatic machine directions. Test whether players can navigate back using the map and visual landmarks, and whether losing a machine has a reasonable cost.
- If continued exploration and efficient retrieval matter more, consider a distinct arrow/icon for each deployed machine, removed upon retrieval. Undeployed mining spots should remain separate from machine tracking and use prospecting highlights.
- If tracking is added, preserve the text limit. One option is to replace the nearest-item label with the nearest recoverable target, either an item or a machine. This changes the agreed display rule and needs a separate design decision.
- The [Miro concept board](https://miro.com/app/board/uXjVHm1YHQ8=/) describes machines mining over time, retrieval being required to collect minerals, and replacement machines in the shop. The related text reviewed does not explicitly define radar tracking. The possibility of losing a machine does not establish a requirement to rely on memory.

## Open Concern: Holding Z While Swimming

- The left hand already operates WASD. Holding Z may compete with movement, especially A/S or diagonal input.
- Swimming and radar can run together; Z+D is tested. Sustained comfort still needs manual playtesting and cannot be established by automated checks.
- Retain the specified tap/hold behavior for now. If controls change, first discuss separating radar and prospecting: hold the right mouse button for radar, and use Z only for prospecting. The current prototype uses mouse movement to aim the flashlight, so this combination can be playtested.
- A separate toggle key is another option, but changes the release-to-deactivate rule and could make radar effectively permanent. Do not substitute it before settling the design.

## Open Ideas: Directional Indicators

Distance bands are implemented. These further ideas remain open:

- Use a submarine icon and a diamond for items. Shape differentiation is not implemented; the agreed light-blue submarine and white item colors are implemented.
- Arrows toward similar directions may overlap. Consider small positional offsets while retaining accurate arrow orientation. If density remains excessive, discuss grouped counts rather than immediately hiding detectable targets.
- Nearest-item text may switch frequently between similarly distant targets. A switching margin could reduce flicker; its value needs playtesting.
- Arrows currently change bands immediately at 10m/20m. If movement around a boundary causes distracting jumps, consider a small hysteresis margin or a short transition.
- Retain submarine plus nearest-item text and the existing label-separation behavior. Readability across resolutions, dense target layouts, and prolonged movement still needs testing.

## Scope and Validation Limits

Chasms were added separately at the user's request: they block movement but transmit the flashlight. Kelp and rock occlusion also follow the concept map. These extend the original checklist.

Machine radar tracking, changed bindings/toggle controls, distinct target shapes, arrow grouping, and target-switch hysteresis are not implemented. Standalone player builds, low-end hardware, final map scale, and prolonged keyboard comfort have not been tested.
