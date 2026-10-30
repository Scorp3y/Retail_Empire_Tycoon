# Wave2_07_Stationery

Six real 3D models. Final selected iteration: 3.

| GLB | Triangles | Materials | Metres X / Y / Z |
| --- | ---: | ---: | --- |
| notebook.glb | 780 | 3 | 0.1470 / 0.2199 / 0.0300 |
| pencils.glb | 1061 | 3 | 0.0750 / 0.0480 / 0.1570 |
| pens.glb | 714 | 4 | 0.0820 / 0.0260 / 0.1800 |
| markers.glb | 1086 | 4 | 0.1050 / 0.0277 / 0.1500 |
| glue_stick.glb | 613 | 3 | 0.0360 / 0.0342 / 0.1000 |
| scissors.glb | 688 | 2 | 0.0880 / 0.1691 / 0.0096 |

The folder contains the standalone Build*.py script, Models.blend, Preview.png, Validation.json, FinalAudit.json and the reviewed iteration files.

Each GLB has one mesh node, baked identity transforms, XY-centred origin, bottom at Z=0 and no image textures. The .blend retains hidden *_EditableParts collections at the modelling origin; the joined copies are arranged in the preview row. Solo the source collection to edit a product.

The 19 common functions, base palette and bevel settings from categories 02-04 are retained: 0.00045 m bevel, two segments, gentle flat facets and matte colours. New blue/teal tones satisfy the requested packaging and toy colours. Transparent plastic uses alpha 0.25 and roughness 0.20. Other surfaces use roughness 0.82 and metallic 0. Hollow source parts include actual inner wall and floor geometry and a centre ray test, recorded in Validation.json.

Tested with the installed Blender 5.2.3 LTS. A separate Blender 4.x runtime test was unavailable. GLBs retain requested Z-up coordinates; glTF importers assuming Y-up may require an axis correction. Unity import, alpha sorting and engine lighting remain untested. Existing Unity scenes/catalogs and the user's open Blender window were not changed. No Git commit/push was made.
