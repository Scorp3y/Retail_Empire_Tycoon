# Store Fixtures: final 16 models

All triangle counts include the materialless 12-triangle collision box. All GLBs contain empty fixtures.

| Fixture | Triangles | Slots | Folder |
| --- | ---: | ---: | --- |
| fixture_01_wall_shelf | 852 | 12 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_A |
| fixture_02_double_sided_aisle | 1392 | 24 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_A |
| fixture_03_aisle_endcap | 1252 | 9 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_A |
| fixture_04_promotion_bin | 912 | 6 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_A |
| fixture_05_bakery_rack | 1564 | 9 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_B |
| fixture_06_produce_stand | 1740 | 12 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_B |
| fixture_07_chilled_display | 1232 | 12 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_B |
| fixture_08_glass_top_freezer | 1820 | 6 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_B |
| fixture_09_pegboard_display | 1640 | 11 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_C |
| fixture_10_cleaning_tool_rack | 1152 | 8 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_C |
| fixture_11_pet_supplies_rack | 792 | 9 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_C |
| fixture_12_garden_display | 852 | 18 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_C |
| fixture_13_toy_cubby_shelf | 612 | 6 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_D |
| fixture_14_stationery_stand | 1080 | 12 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_D |
| fixture_15_homeware_shelf | 612 | 6 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_D |
| fixture_16_baby_care_shelf | 916 | 6 | C:\Users\artem\Retail Empire Tycoon\ArtSource\Fixtures_D |

Each folder includes 4 GLBs, a self-contained BuildFixtures*.py, Fixtures.blend with editable source parts, two final previews, iteration renders, slot/demo data and audit files.

Verified: all hierarchy nodes and slot names, unchanged shared helpers/palette/bevel constants, identity root poses, bottom-centred origins, Z-up axes, real-scale demos with no substitutes, slot coordinates, equal working-level spacing, product clearances, collider bounds, no image textures, 2-4 materials and full budgets including collider triangles.

Additional geometry probes verify an open promotion bin, the actual freezer interior, thick glass side walls and six open cubbies. Read the group CavityAudit.json files.

Furniture is authored at 1:10 and baked to metres: the unchanged 0.45 mm / 2-segment helper settings produce 4.5 mm furniture bevels. Working surfaces are horizontal; sloped tray rims give the intended angled look.

Colliders are hidden in Blender, but core glTF does not support universally hidden mesh nodes. Each collider carries extras.hidden=true and usage=collision_only. At engine import disable that child renderer; generic GLB viewers may show the materialless box. Keep the slot Empty children and their rotations.

Tested in installed Blender 5.2.3 LTS; a separate Blender 4.x runtime was unavailable. Unity import, slot-to-game components, collision setup and renderer-specific glass/transmission setup remain untested. These models are not yet registered as purchasable/buildable Unity fixtures. Existing scenes and player saves were not changed. No Git commit/push was made.
