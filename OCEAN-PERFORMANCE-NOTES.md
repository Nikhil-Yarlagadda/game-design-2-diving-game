# Ocean Performance Notes

Date: 2026-10-17. Scene: Ocean Interactables. These measurements are from the Unity Editor, not a standalone player or a low-end-device acceptance test.

## Changes

- Restrict polygon shadow edges to the rectangle enclosing the ocean camera ground-plane viewport and flashlight origin. An edge is excluded only when its bounding box cannot overlap this rectangle. Crossing edges with off-screen endpoints and boundary-touching edges remain included. If viewport rays cannot intersect the ground plane, filtering is disabled conservatively.
- Preserve original polygon-owner IDs for self-shadow exclusion and preserve the global 256-edge validation limit. Runtime transforms are still evaluated every frame; this change does not assume static obstacles.
- In the fragment shader, skip light/shadow calculations for exactly transparent fragments, fully revealed objects, or fully ambient-lit objects. Blend output is unchanged in these cases.
- Keep camera size, lighting falloff, 15-item count, minimap, texture quality, SMAA and output resolution unchanged.

## Sampling Method

Hardware: NVIDIA GeForce RTX 5070 Ti Laptop GPU. Unity 6000.6.4f1 / D3D12. Actual Game View output: 1920 x 1080. Focused Game View with background operation enabled during measurement. Temporarily disabled VSync and any frame cap; prior values, input devices, render size, background mode and player position were restored afterward.

Two passes at each of three fixed positions. Each sample has 0.9 seconds warm-up followed by 3.2 seconds recording. Upward flashlight aim was injected consistently. FrameTimingManager supplied GPU timings; ProfilerRecorder supplied main-thread and allocation counters. The numbers below average each location's two GPU sample medians. They do not imply the same percentage improvement in overall frame rate.

| Location | Before GPU median (ms) | After GPU median (ms) | Reduction | Active shadow edges |
| --- | --- | --- | --- | --- |
| submarine | 0.821 | 0.267 | 67.5% | 197 -> 23 |
| kelp | 0.593 | 0.302 | 49.0% | 197 -> 43 |
| chasm | 0.747 | 0.271 | 63.8% | 197 -> 14 |

Whole-frame medians were approximately 1.7-2.3 ms before and 1.7-2.0 ms after, with run-to-run overlap and no reliable overall FPS improvement established. CPU/editor overhead now dominates more of the frame. Recorded per-frame allocation medians remained about 9.2 KB and include the Editor and measurement harness; allocation sources were not attributed in this round. The batches recorder returned zero and was not used for conclusions. A separate Pipeline render-stat snapshot reported 23 draw calls and 4 batches before optimization.

## Visual and Functional Validation

Three world-only reference frames were rendered at 1280 x 720 at the exact same positions, upward aim and saved item positions before/after. All three images matched exactly: zero changed pixels, maximum channel difference zero. The first after capture had inconsistent synthetic keyboard aim; that capture was rejected and repeated with explicit upward movement input and an aim assertion. HUD visibility was restored after captures.

The ocean editor/GPU checks and original FlashlightCheck passed. New assertions cover off-screen endpoints crossing the view, boundary touching, fully distant edges and degenerate visible edges. The complete ocean Play regression is recorded in the acceptance notes after its final run.

## Boundaries and Artifacts

This was a short desktop Editor comparison. No standalone build, long-duration thermal test, low-end-device profiling or optimization of texture memory was performed. Terrain atlas size remains about 64 MiB of BC7 data. The filtered shadow set is defined for the gameplay camera; a wider Scene view during Play is not a separate lighting reference.

Raw samples, temporary measuring scripts, saved item positions and reference frames are under ignored Temp/performance/. No profiler or measurement loop is added to runtime gameplay code.

API references: [ProfilerRecorder](https://docs.unity.com/en-us/engine/7000.0/script-reference/unity/profiling/profilerrecorder), [FrameTimingManager](https://docs.unity3d.com/es/current/ScriptReference/FrameTimingManager.html). The available APIs were also verified against this project's installed Unity/Pipeline code.

Final functional result: 122 Play assertions passed, along with OceanInteractionCheck and the original FlashlightCheck. One preceding full Play run failed the synthetic mouse viewport-offset assertion; a fresh session passed with unchanged production input logic. The test now includes direction, pointer coordinates and viewport values in that failure message. The initial failure was not reproduced or assigned a confirmed root cause.
