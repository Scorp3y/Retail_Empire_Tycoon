# Expansion_05_CleaningTools

| Model | Triangles | Materials | Metres X / Y / Z |
| --- | ---: | ---: | --- |
| broom.glb | 832 | 4 | 0.1191 / 0.0319 / 0.3937 |
| mop.glb | 1032 | 3 | 0.1029 / 0.0995 / 0.3964 |
| bucket.glb | 668 | 2 | 0.2472 / 0.2460 / 0.2520 |
| dustpan.glb | 496 | 3 | 0.1500 / 0.2555 / 0.0353 |
| cleaning_spray.glb | 864 | 3 | 0.0776 / 0.0745 / 0.2425 |
| trash_bags.glb | 872 | 4 | 0.1090 / 0.0524 / 0.1015 |

Each model has one exported mesh node with identity transforms, bottom-centred origin and Z-up geometry. No image textures. Original palette, 19 shared functions and 0.00045 m / 2 segment bevel settings retained.

Models.blend preserves hidden *_EditableParts collections as separate editable meshes. Joined preview copies are arranged in a row; GLBs are exported before that arrangement.

Both iteration renders are retained in Iterations. See Review.md for visual findings and corrections.

Tested in installed Blender 5.2.3 LTS. The script uses the established Blender 4.x-compatible API helpers; a separate 4.x runtime was not available. Import into Unity, shader setup for vertex colours and transparent surface sorting still need engine-side verification. These files have not been added to the purchasing catalogue.
