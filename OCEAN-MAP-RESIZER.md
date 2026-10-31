# Ocean map resizer

Open `Ocean Interactables` outside Play mode and choose **Diving Prototype → Ocean Map Resizer**.

1. Choose **Capture Baseline** to establish the current layout as scale **1** for this editor session.
2. Enter a positive **Target scale** and choose **Apply Scale**. **2** doubles map distances and terrain dimensions; **0.5** halves them. Reapplying the same target does not compound it.
3. Use **Edit → Undo** to revert a resize in one step. Redo reapplies it. **Reset to 1** restores the captured layout as another undoable scene edit.

The submarine's captured center is the anchor. Rocks, kelp, chasms, seabed, grid and outer boundaries resize together. Grid line thickness stays fixed. Mining spots, quest markers, the submarine, and their child artwork retain their sizes. Player, camera, HUD, gameplay settings and runtime spawn ranges are unchanged. Very small maps may overlap fixed-size objects; the tool does not adjust the design to avoid those overlaps.

The baseline remains authoritative: applying a scale restores the captured object transforms before resizing. After intentional manual layout edits, use **Recapture Baseline** to make the current layout the new **1**. Added, removed, renamed or reparented map objects and component changes block resizing until recapture. Moving the Map root also requires recapture. Include inactive map objects when editing; they are captured and resized too.

The tool modifies scene transforms, collider paths and renderer dimensions. It does not create baseline assets, sprite copies, textures or other resize data on disk, and it never saves the scene automatically. Shared art assets remain unchanged. Saving the scene yourself retains its ordinary scene edits, but the captured baseline is temporary editor memory and is discarded when the scene closes. On reopening, capture the current layout as a new scale 1.

Undo resizing before using **Rebuild Smooth Ocean Silhouettes**, which writes shared art assets. Resizing itself needs no silhouette bake: renderer dimensions keep the existing artwork aligned with the resized collider paths.
