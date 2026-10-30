# Wave2_05_PetSupplies

Six real 3D models. Final selected iteration: 3.

| GLB | Triangles | Materials | Metres X / Y / Z |
| --- | ---: | ---: | --- |
| dog_food.glb | 492 | 3 | 0.1373 / 0.0892 / 0.2500 |
| cat_food.glb | 612 | 3 | 0.1175 / 0.0799 / 0.2000 |
| pet_treats.glb | 689 | 3 | 0.0936 / 0.0673 / 0.1500 |
| cat_litter.glb | 565 | 3 | 0.1570 / 0.1023 / 0.2800 |
| pet_bowl.glb | 258 | 2 | 0.1800 / 0.1802 / 0.0800 |
| chew_toy.glb | 288 | 2 | 0.1597 / 0.0329 / 0.0578 |

The folder contains the standalone Build*.py script, Models.blend, Preview.png, Validation.json, FinalAudit.json and the reviewed iteration files.

Each GLB has one mesh node, baked identity transforms, XY-centred origin, bottom at Z=0 and no image textures. The .blend retains hidden *_EditableParts collections at the modelling origin; the joined copies are arranged in the preview row. Solo the source collection to edit a product.

The 19 common functions, base palette and bevel settings from categories 02-04 are retained: 0.00045 m bevel, two segments, gentle flat facets and matte colours. New blue/teal tones satisfy the requested packaging and toy colours. Transparent plastic uses alpha 0.25 and roughness 0.20. Other surfaces use roughness 0.82 and metallic 0. Hollow source parts include actual inner wall and floor geometry and a centre ray test, recorded in Validation.json.

Tested with the installed Blender 5.2.3 LTS. A separate Blender 4.x runtime test was unavailable. GLBs retain requested Z-up coordinates; glTF importers assuming Y-up may require an axis correction. Unity import, alpha sorting and engine lighting remain untested. Existing Unity scenes/catalogs and the user's open Blender window were not changed. No Git commit/push was made.
