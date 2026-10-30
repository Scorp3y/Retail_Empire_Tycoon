# Expansion_01_BreadPastries

| Model | Triangles | Materials | Metres X / Y / Z |
| --- | ---: | ---: | --- |
| bagel.glb | 672 | 2 | 0.1080 / 0.1064 / 0.0380 |
| baguette.glb | 516 | 2 | 0.0620 / 0.4000 / 0.0552 |
| cinnamon_roll.glb | 780 | 3 | 0.1000 / 0.1000 / 0.0500 |
| gingerbread.glb | 996 | 3 | 0.0966 / 0.1152 / 0.0178 |
| fruit_tart.glb | 904 | 4 | 0.1028 / 0.0978 / 0.0480 |
| eclair.glb | 368 | 3 | 0.1360 / 0.0512 / 0.0440 |

Each model has one exported mesh node with identity transforms, bottom-centred origin and Z-up geometry. No image textures. Original palette, 19 shared functions and 0.00045 m / 2 segment bevel settings retained.

Models.blend preserves hidden *_EditableParts collections as separate editable meshes. Joined preview copies are arranged in a row; GLBs are exported before that arrangement.

Both iteration renders are retained in Iterations. See Review.md for visual findings and corrections.

Tested in installed Blender 5.2.3 LTS. The script uses the established Blender 4.x-compatible API helpers; a separate 4.x runtime was not available. Import into Unity, shader setup for vertex colours and transparent surface sorting still need engine-side verification. These files have not been added to the purchasing catalogue.
