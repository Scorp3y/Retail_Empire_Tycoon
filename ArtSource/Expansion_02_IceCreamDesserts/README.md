# Expansion_02_IceCreamDesserts

| Model | Triangles | Materials | Metres X / Y / Z |
| --- | ---: | ---: | --- |
| ice_cream_cone.glb | 1148 | 3 | 0.0604 / 0.0604 / 0.1847 |
| ice_cream_tub.glb | 440 | 3 | 0.1040 / 0.0880 / 0.1115 |
| popsicle.glb | 284 | 3 | 0.0540 / 0.0277 / 0.1700 |
| cheesecake.glb | 516 | 4 | 0.0959 / 0.0900 / 0.0670 |
| macarons.glb | 924 | 2 | 0.0500 / 0.0480 / 0.0680 |
| pudding.glb | 612 | 3 | 0.0680 / 0.0680 / 0.0809 |

Each model has one exported mesh node with identity transforms, bottom-centred origin and Z-up geometry. No image textures. Original palette, 19 shared functions and 0.00045 m / 2 segment bevel settings retained.

Models.blend preserves hidden *_EditableParts collections as separate editable meshes. Joined preview copies are arranged in a row; GLBs are exported before that arrangement.

Both iteration renders are retained in Iterations. See Review.md for visual findings and corrections.

Tested in installed Blender 5.2.3 LTS. The script uses the established Blender 4.x-compatible API helpers; a separate 4.x runtime was not available. Import into Unity, shader setup for vertex colours and transparent surface sorting still need engine-side verification. These files have not been added to the purchasing catalogue.
