# Next Store Expansion: categories 05-08

24 real 3D models; all categories processed, rendered, reviewed and exported in order.

| Model | Triangles | Materials | Folder |
| --- | ---: | ---: | --- |
| broom.glb | 832 | 4 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_05_CleaningTools |
| mop.glb | 1032 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_05_CleaningTools |
| bucket.glb | 668 | 2 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_05_CleaningTools |
| dustpan.glb | 496 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_05_CleaningTools |
| cleaning_spray.glb | 864 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_05_CleaningTools |
| trash_bags.glb | 872 | 4 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_05_CleaningTools |
| diapers.glb | 608 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_06_BabyCare |
| baby_wipes.glb | 560 | 4 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_06_BabyCare |
| baby_food.glb | 740 | 4 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_06_BabyCare |
| baby_bottle.glb | 824 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_06_BabyCare |
| pacifier.glb | 720 | 2 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_06_BabyCare |
| baby_cereal.glb | 476 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_06_BabyCare |
| potted_cactus.glb | 972 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_07_GardenSupplies |
| seeds.glb | 556 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_07_GardenSupplies |
| watering_can.glb | 820 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_07_GardenSupplies |
| hand_trowel.glb | 396 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_07_GardenSupplies |
| garden_gloves.glb | 1112 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_07_GardenSupplies |
| plant_food.glb | 694 | 4 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_07_GardenSupplies |
| candle.glb | 608 | 4 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_08_HomeComfort |
| table_lamp.glb | 772 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_08_HomeComfort |
| alarm_clock.glb | 1018 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_08_HomeComfort |
| photo_frame.glb | 644 | 2 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_08_HomeComfort |
| cushion.glb | 396 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_08_HomeComfort |
| blanket.glb | 1140 | 3 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Expansion_08_HomeComfort |

Each folder includes six separate GLBs, a standalone Build*.py script, Models.blend with separate editable source parts, both reviewed iteration renders, Validation.json, FinalAudit.json, SourceAudit.json and Review.md.

19 original shared helpers, base palette and bevel constants were compared structurally against the previous categories and are unchanged. All GLBs have exactly one mesh node, identity transforms, bottom-centred origins, Z-up geometry, 2-4 materials and no image textures. Every model is below its specified triangle budget. Larger real-scale props use measured row spacing after the unchanged preview helper creates the lighting/camera.

Physical checks: bucket wall volume and inner floor; two real pacifier holes; closed wipe solid; watering can inner floor, through-spout and four spray holes; open-ended lampshade with nonzero wall volume; exported candle emission. See each SourceAudit.json.

Runtime tested: installed Blender 5.2.3 LTS. Separate Blender 4.x runtime was unavailable. Unity import still needs axis conversion, vertex-colour and transparent-material setup plus an emission appearance check. These new models are not yet registered in the Unity purchasing catalogue.
