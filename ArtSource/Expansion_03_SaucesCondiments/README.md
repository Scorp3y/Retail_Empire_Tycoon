# Expansion_03_SaucesCondiments

| Model | Triangles | Materials | Metres X / Y / Z |
| --- | ---: | ---: | --- |
| ketchup.glb | 840 | 3 | 0.0520 / 0.0534 / 0.2000 |
| mustard.glb | 840 | 3 | 0.0520 / 0.0536 / 0.2000 |
| mayonnaise.glb | 860 | 4 | 0.0750 / 0.0755 / 0.1190 |
| soy_sauce.glb | 692 | 4 | 0.0500 / 0.0511 / 0.1995 |
| hot_sauce.glb | 880 | 4 | 0.0420 / 0.0433 / 0.2000 |
| bbq_sauce.glb | 840 | 4 | 0.0680 / 0.0687 / 0.2000 |

Each model has one exported mesh node with identity transforms, bottom-centred origin and Z-up geometry. No image textures. Original palette, 19 shared functions and 0.00045 m / 2 segment bevel settings retained.

Models.blend preserves hidden *_EditableParts collections as separate editable meshes. Joined preview copies are arranged in a row; GLBs are exported before that arrangement.

Both iteration renders are retained in Iterations. See Review.md for visual findings and corrections.

Tested in installed Blender 5.2.3 LTS. The script uses the established Blender 4.x-compatible API helpers; a separate 4.x runtime was not available. Import into Unity, shader setup for vertex colours and transparent surface sorting still need engine-side verification. These files have not been added to the purchasing catalogue.
