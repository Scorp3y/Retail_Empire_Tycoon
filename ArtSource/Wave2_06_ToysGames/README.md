# Wave2_06_ToysGames

Six real 3D models. Final selected iteration: 3.

| GLB | Triangles | Materials | Metres X / Y / Z |
| --- | ---: | ---: | --- |
| teddy_bear.glb | 968 | 4 | 0.1635 / 0.1097 / 0.2550 |
| toy_car.glb | 896 | 4 | 0.1250 / 0.0760 / 0.0690 |
| building_blocks.glb | 944 | 4 | 0.1090 / 0.0790 / 0.0910 |
| doll.glb | 1036 | 3 | 0.1280 / 0.0856 / 0.2670 |
| football.glb | 120 | 2 | 0.2192 / 0.2156 / 0.2142 |
| puzzle.glb | 596 | 4 | 0.2500 / 0.1710 / 0.0400 |

The folder contains the standalone Build*.py script, Models.blend, Preview.png, Validation.json, FinalAudit.json and the reviewed iteration files.

Each GLB has one mesh node, baked identity transforms, XY-centred origin, bottom at Z=0 and no image textures. The .blend retains hidden *_EditableParts collections at the modelling origin; the joined copies are arranged in the preview row. Solo the source collection to edit a product.

The 19 common functions, base palette and bevel settings from categories 02-04 are retained: 0.00045 m bevel, two segments, gentle flat facets and matte colours. New blue/teal tones satisfy the requested packaging and toy colours. Transparent plastic uses alpha 0.25 and roughness 0.20. Other surfaces use roughness 0.82 and metallic 0. Hollow source parts include actual inner wall and floor geometry and a centre ray test, recorded in Validation.json.

Tested with the installed Blender 5.2.3 LTS. A separate Blender 4.x runtime test was unavailable. GLBs retain requested Z-up coordinates; glTF importers assuming Y-up may require an axis correction. Unity import, alpha sorting and engine lighting remain untested. Existing Unity scenes/catalogs and the user's open Blender window were not changed. No Git commit/push was made.
