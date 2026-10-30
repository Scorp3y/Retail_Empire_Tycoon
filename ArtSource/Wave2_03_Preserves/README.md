# Wave 2 / 03 Preserves & Spreads

Final selection: iteration 04. Six native 3D models, six separate GLBs, one mesh per file. All label lettering and icons are geometry; there are no image textures.

| File | Triangles | Height (m) |
| --- | ---: | ---: |
| honey_jar.glb | 538 | 0.102 |
| jam_jar.glb | 664 | 0.103 |
| peanut_butter_jar.glb | 692 | 0.105 |
| pickles_jar.glb | 772 | 0.119 |
| canned_corn.glb | 728 | 0.100 |
| canned_peas.glb | 596 | 0.100 |

## Contents

- `BuildPreserves.py`: complete standalone generation/export/render script.
- `Preserves.blend`: editable meshes, one named collection per product, plus the preview setup.
- `Preview.png`: actual Blender render of the final meshes, 2520 x 920.
- `Validation.json`: measurements and exported polygon counts.
- `FinalAudit.json`: independent checks of the GLB binary positions, counts, materials and unchanged category 02 helpers.
- `SceneAudit.json`: checks of the saved native Blender scene.
- `Iterations/01` through `04`: earlier exports and rendered previews retained for comparison.

## Style and verification

Category 02's base palette, 13 shared functions, metre scale and bevel settings are unchanged. Bevel width is 0.00045 m with two segments; flat facets remain visible. New lid/content colours are tonal variants of that palette. Opaque materials use roughness 0.82 and metallic 0; glass uses alpha blending and roughness 0.20. The faint pickle brine uses roughness 0.25.

All four rendered iterations were visually inspected:

1. First pass: correct broad silhouettes; pickle glass too faint, corn icon too pointed, lettering small.
2. Filled jar shoulders, larger lettering, rounder pickles and a broader corn icon; mismatched can segment counts caused metal stripes through the labels.
3. Matched label/body segments to remove those intersections; added readable pickle glass edge bands.
4. Used the original sage colour for the pickle glass tint so its silhouette is visible against the light background.

Honey, peanut butter and pickles retain real mesh lettering. No stripe fallback was needed. The jam, corn and peas have primitive geometry icons. Every exported mesh is below 800 triangles, with transforms baked, XY centred and its bottom at Z=0.

## Runtime notes

Generated and rendered headlessly using the installed Blender **5.2.3 LTS** at `D:/SteamLibrary/steamapps/common/Blender/blender.exe`. The script uses Blender 4.x-compatible APIs and handles the transparency API change in 4.2; a Blender 4.x executable was not available for a separate runtime test.

The GLB vertex coordinates are **Z-up**, as requested (`export_yup=False`). Standard glTF importers assume Y-up, so the target engine's importer may require an axis correction. Materials use simple alpha transparency rather than physical glass refraction. Unity import, glass sorting and engine lighting have not been tested for these assets. No Unity catalog or scene was modified by this category task.

The user's open Blender scene was not changed. No Git commit or push was made.
