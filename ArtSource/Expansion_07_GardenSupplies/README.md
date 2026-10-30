# Expansion_07_GardenSupplies

| Model | Triangles | Materials | Metres X / Y / Z |
| --- | ---: | ---: | --- |
| potted_cactus.glb | 972 | 3 | 0.0960 / 0.0830 / 0.1710 |
| seeds.glb | 556 | 3 | 0.0850 / 0.0161 / 0.1450 |
| watering_can.glb | 820 | 3 | 0.2507 / 0.0989 / 0.1690 |
| hand_trowel.glb | 396 | 3 | 0.0700 / 0.2790 / 0.0223 |
| garden_gloves.glb | 1112 | 3 | 0.2586 / 0.2600 / 0.0245 |
| plant_food.glb | 694 | 4 | 0.1300 / 0.0631 / 0.2500 |

Each model has one exported mesh node with identity transforms, bottom-centred origin and Z-up geometry. No image textures. Original palette, 19 shared functions and 0.00045 m / 2 segment bevel settings retained.

Models.blend preserves hidden *_EditableParts collections as separate editable meshes. Joined preview copies are arranged in a row; GLBs are exported before that arrangement.

Both iteration renders are retained in Iterations. See Review.md for visual findings and corrections.

Tested in installed Blender 5.2.3 LTS. The script uses the established Blender 4.x-compatible API helpers; a separate 4.x runtime was not available. Import into Unity, shader setup for vertex colours and transparent surface sorting still need engine-side verification. These files have not been added to the purchasing catalogue.
