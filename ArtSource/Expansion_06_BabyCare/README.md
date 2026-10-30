# Expansion_06_BabyCare

| Model | Triangles | Materials | Metres X / Y / Z |
| --- | ---: | ---: | --- |
| diapers.glb | 608 | 3 | 0.2500 / 0.1205 / 0.2110 |
| baby_wipes.glb | 560 | 4 | 0.1800 / 0.1014 / 0.1032 |
| baby_food.glb | 740 | 4 | 0.0600 / 0.0610 / 0.0800 |
| baby_bottle.glb | 824 | 3 | 0.0620 / 0.0621 / 0.2000 |
| pacifier.glb | 720 | 2 | 0.0539 / 0.0644 / 0.0380 |
| baby_cereal.glb | 476 | 3 | 0.0970 / 0.0590 / 0.2015 |

Each model has one exported mesh node with identity transforms, bottom-centred origin and Z-up geometry. No image textures. Original palette, 19 shared functions and 0.00045 m / 2 segment bevel settings retained.

Models.blend preserves hidden *_EditableParts collections as separate editable meshes. Joined preview copies are arranged in a row; GLBs are exported before that arrangement.

Both iteration renders are retained in Iterations. See Review.md for visual findings and corrections.

Tested in installed Blender 5.2.3 LTS. The script uses the established Blender 4.x-compatible API helpers; a separate 4.x runtime was not available. Import into Unity, shader setup for vertex colours and transparent surface sorting still need engine-side verification. These files have not been added to the purchasing catalogue.
