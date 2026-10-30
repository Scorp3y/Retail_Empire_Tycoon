# Expansion_04_PersonalCare

| Model | Triangles | Materials | Metres X / Y / Z |
| --- | ---: | ---: | --- |
| toothpaste.glb | 848 | 3 | 0.0430 / 0.0285 / 0.1580 |
| toothbrush.glb | 636 | 4 | 0.0640 / 0.0245 / 0.2200 |
| hand_soap.glb | 796 | 3 | 0.0730 / 0.0564 / 0.2000 |
| shower_gel.glb | 452 | 3 | 0.0700 / 0.0454 / 0.2000 |
| deodorant.glb | 608 | 3 | 0.0540 / 0.0404 / 0.1400 |
| tissues.glb | 1088 | 3 | 0.1200 / 0.0854 / 0.1161 |

Each model has one exported mesh node with identity transforms, bottom-centred origin and Z-up geometry. No image textures. Original palette, 19 shared functions and 0.00045 m / 2 segment bevel settings retained.

Models.blend preserves hidden *_EditableParts collections as separate editable meshes. Joined preview copies are arranged in a row; GLBs are exported before that arrangement.

Both iteration renders are retained in Iterations. See Review.md for visual findings and corrections.

Tested in installed Blender 5.2.3 LTS. The script uses the established Blender 4.x-compatible API helpers; a separate 4.x runtime was not available. Import into Unity, shader setup for vertex colours and transparent surface sorting still need engine-side verification. These files have not been added to the purchasing catalogue.
