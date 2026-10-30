# Fixtures A

| Fixture | Triangles including collider | Slots |
| --- | ---: | ---: |
| fixture_01_wall_shelf.glb | 852 | 12 |
| fixture_02_double_sided_aisle.glb | 1392 | 24 |
| fixture_03_aisle_endcap.glb | 1252 | 9 |
| fixture_04_promotion_bin.glb | 912 | 6 |

Each GLB is EMPTY and contains one fixture render mesh, child slot_01... Empty nodes and one materialless 12-triangle collider box. Demo products appear only in Fixtures.blend and Preview_Stocked.png. DemoPlacements.json records real existing product files at their original metre scale.

The fixture front is -Y, geometry is Z-up, local root transform is identity and the origin is bottom-centred. Opposite aisle slots have a 180-degree facing. Floor anchors were ray-tested against the final surface before export. Hanging anchors use kind=hang and their product top attachment is aligned to the hook.

Original authoring helpers, palette and bevel constants are retained. Fixture geometry is authored at 1:10 and baked into metres, giving furniture-sized 4.5 mm, two-segment bevels. Angled tray rims provide the slanted look while usable floors stay level.

IMPORTANT: Blender collider objects are hidden from render. Standard glTF has no universally supported hidden-mesh property: collider nodes carry extras.hidden=true and usage=collision_only; an engine importer must disable their renderer and use the mesh as collision. A generic GLB viewer may display the box.

Tested in installed Blender 5.2.3 LTS. A separate Blender 4.x runtime and Unity integration were not tested. At Unity import convert axes, retain slot children, configure glass and disable collider renderers. Source geometry parts are separately editable in hidden *_EditableParts collections. No purchasing/build catalogue or Unity scene was modified.
