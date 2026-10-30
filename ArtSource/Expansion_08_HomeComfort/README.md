# Expansion_08_HomeComfort

| Model | Triangles | Materials | Metres X / Y / Z |
| --- | ---: | ---: | --- |
| candle.glb | 608 | 4 | 0.0620 / 0.0634 / 0.0830 |
| table_lamp.glb | 772 | 3 | 0.2040 / 0.2040 / 0.3410 |
| alarm_clock.glb | 1018 | 3 | 0.0900 / 0.0419 / 0.1258 |
| photo_frame.glb | 644 | 2 | 0.1440 / 0.0641 / 0.1800 |
| cushion.glb | 396 | 3 | 0.3026 / 0.3060 / 0.1008 |
| blanket.glb | 1140 | 3 | 0.3510 / 0.2407 / 0.0540 |

Each model has one exported mesh node with identity transforms, bottom-centred origin and Z-up geometry. No image textures. Original palette, 19 shared functions and 0.00045 m / 2 segment bevel settings retained.

Models.blend preserves hidden *_EditableParts collections as separate editable meshes. Joined preview copies are arranged in a row; GLBs are exported before that arrangement.

Both iteration renders are retained in Iterations. See Review.md for visual findings and corrections.

Tested in installed Blender 5.2.3 LTS. The script uses the established Blender 4.x-compatible API helpers; a separate 4.x runtime was not available. Import into Unity, shader setup for vertex colours and transparent surface sorting still need engine-side verification. These files have not been added to the purchasing catalogue.
