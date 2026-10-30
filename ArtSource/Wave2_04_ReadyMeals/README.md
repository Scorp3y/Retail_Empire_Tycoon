# Wave 2 / 04 Ready Meals

Selected version: iteration 04. Six real 3D assets with separate GLB exports and an editable Blender scene. No image textures are used. Food, lettering, packaging windows and icons are modeled geometry.

| File | Triangles | Limit (exclusive) | Size X/Y/Z, metres |
| --- | ---: | ---: | --- |
| sandwich.glb | 750 | 900 | 0.160 / 0.0813 / 0.1098 |
| wrap.glb | 475 | 900 | 0.082 / 0.0457 / 0.1448 |
| sushi.glb | 992 | 1200 | 0.1819 / 0.1166 / 0.046 |
| ready_salad.glb | 1166 | 1200 | 0.122 / 0.122 / 0.068 |
| lasagna.glb | 812 | 900 | 0.170 / 0.117 / 0.0338 |
| chicken_soup.glb | 541 | 900 | 0.088 / 0.0837 / 0.120 |

## Delivered files

- `BuildReadyMeals.py`: complete standalone Blender generation, export, validation and preview script; no dependency on another category's script at runtime.
- `ReadyMeals.blend`: final meshes, source parts, materials, camera and soft preview lighting.
- `Preview.png`: actual Blender render, 2760 x 1020, all six products in one row.
- `Validation.json`: native/exported triangle counts, dimensions and source-part information.
- `FinalAudit.json`: independent checks against actual GLB binary data and the unchanged shared helpers.
- `SceneAudit.json`: inspection of the saved native scene and geometric ray test through the soup lid's sip hole.
- `Iterations/01` through `04`: exports, native scenes and previews for the four reviewed passes.

## Editing packaging and food

Each product has its own top-level collection and a joined display/export mesh. Its nested `<name>_EditableParts` collection retains food, packaging, lids/windows and label meshes separately with `part_role` properties and separate material assignments.

The source collections are disabled in the viewport and render to avoid displaying duplicate geometry. To edit a product, hide its joined mesh and enable the corresponding source collection. Source parts align with the display mesh. The GLB contains one mesh node with separate material primitives, as requested; editable food/packaging mesh separation is retained in the native `.blend`.

## Preserved style and technical settings

The base palette, 19 shared helper functions, metre scale, row spacing, export conventions and lighting setup from categories 02/03 are unchanged. Added packaging colours are muted kraft and tonal plastic/nori variants. The common bevel modifier uses 0.00045 m width, two segments and a 65-degree threshold; all modifiers are applied before export. Organic food retains gentle flat facets rather than subdivision smoothing. Convex packaging uses shaped/chamfered profiles plus the same bevel helper.

Opaque materials use metallic 0 and roughness 0.82. Clear lids/windows use alpha blending, low alpha and roughness 0.20. The foil tray is the requested exception: metallic 0.30, roughness 0.50. The labels `Sandwich` and `Wrap` remain real mesh lettering; no stripe fallback was needed.

The sushi contains four maki and two salmon nigiri, plus sauce, wasabi and ginger. The salad includes lettuce, tomato wedges, cucumber slices, olives and corn. The soup lid's sip opening is a Boolean cut through actual geometry; the empty material slot introduced by the cutter was removed before export.

## Visual review

1. Initial budget checks exposed excessive geometry on thin bevelled details. Physical edge offsets and existing low-poly organic facets replaced unnecessary hidden surfaces. The first completed render showed a clipped sandwich label and nearly invisible salad/lid silhouettes.
2. Flattened the label curve to keep the full word visible; added clearer plastic edge materials, a lettuce-filled cut surface to each wrap half, cheese blisters and slightly stronger foil crimping. Opened the soup cup so the soup is visible through the lid.
3. Added a subtle bowl wall tint and lower edge band, and defined the soup lid/sip-hole edge. The shape and material checks then found an empty material slot from the lid cutter.
4. Rebuilt the lid's intended material slots and rerendered. All final renders were visually inspected. Labels are readable, food is visible through the lids, and all exports meet their triangle budgets.

## Verified and remaining checks

Verified on the installed **Blender 5.2.3 LTS** at `D:/SteamLibrary/steamapps/common/Blender/blender.exe`, running in separate background processes. The user's open Blender window was not changed. The script uses Blender 4.x-compatible APIs, including a branch for the material transparency API change in 4.2, but no Blender 4.x executable was available for a separate runtime test.

Export checks read the actual binary vertex positions and index counts: one mesh/node per file, no textures, finite coordinates, identity node transforms, XY-centred origins, bottoms at Z=0, appropriate physical dimensions, explicit materials and the requested foil/plastic properties. The saved Blender scene was reopened to inspect applied modifiers, source-part separation, alignment and the real sip opening.

GLBs retain **Z-up** vertex coordinates (`export_yup=False`), following the earlier categories and the request. Standard glTF importers assume Y-up, so the target engine importer may require an axis correction. Alpha blending is simple transparent plastic, without physical refraction. Unity import, transparency sorting and engine lighting have not been tested for this pack. This task did not change Unity catalogs or scenes, and did not create a Git commit or push.
