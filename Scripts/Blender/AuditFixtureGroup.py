"""Audit exported hierarchy, collider geometry, materials and slot coordinates."""
from pathlib import Path
import ast
import json
import hashlib
import math
import struct
import sys

root=Path(__file__).resolve().parents[2];folder=root/'ArtSource'/('Fixtures_'+sys.argv[1])
report=json.loads((folder/'Validation.json').read_text());assert report['iteration']>=2
old=ast.parse((root/'Scripts/Blender/BuildReadyMeals.py').read_text(encoding='utf-8-sig'))
new=ast.parse(next(folder.glob('Build*.py')).read_text(encoding='utf-8-sig'))
def functions(tree):return {n.name:ast.dump(n,include_attributes=False) for n in tree.body if isinstance(n,ast.FunctionDef)}
helpers=('srgb_to_linear','color_rgba','make_material','register','apply_transform','add_bevel',
         'apply_modifiers','make_sphere','make_leaf','make_disk','soft_box','lathe',
         'prism','loft','join_and_seat','export_glb','preview','curved_label','label_text',
         'shade_hex','prepare_palette')
a,b=functions(old),functions(new);assert all(a[n]==b[n] for n in helpers)
for key in ('PALETTE','BEVEL_WIDTH','BEVEL_ANGLE','ROW_SPACING','EXPORT_Y_UP'):
    def value(tree):
        return next(ast.dump(n.value,include_attributes=False) for n in tree.body if isinstance(n,ast.Assign)
                    and any(isinstance(t,ast.Name) and t.id==key for t in n.targets))
    assert value(old)==value(new),key
for iteration in (1,2):
    for name in ('Preview_Empty.png','Preview_Stocked.png'):
        assert (folder/'Iterations'/f'{iteration:02d}'/name).is_file()
for fixture in report['fixtures']:
    path=folder/(fixture['name']+'.glb');blob=path.read_bytes();size=struct.unpack_from('<I',blob,12)[0]
    doc=json.loads(blob[20:20+size]);assert not doc.get('images') and not doc.get('textures')
    assert hashlib.sha256(blob).hexdigest()==fixture['sha256']
    nodes=doc['nodes'];model=next(n for n in nodes if n['name']==fixture['name'])
    assert len(doc['meshes'])==2 and len(model['children'])==fixture['slots']+1
    assert not any(k in model for k in ('translation','rotation','scale','matrix'))
    slots=sorted((n for n in nodes if n['name'].startswith('slot_')),key=lambda n:n['name'])
    assert [n['name'] for n in slots]==[f'slot_{i+1:02d}' for i in range(fixture['slots'])]
    for node,anchor in zip(slots,fixture['anchors']):
        assert all(abs(x-y)<1e-5 for x,y in zip(node.get('translation',[0,0,0]),anchor['position']))
        assert node['extras']['kind']==anchor['kind']
        if anchor['kind']=='floor':assert anchor['space'][2]>=.25
    collider=next(n for n in nodes if n['name']=='collider');assert collider['extras']['hidden']
    assert all('material' not in p for p in doc['meshes'][collider['mesh']]['primitives'])
    total=sum(doc['accessors'][p['indices']]['count']//3 for mesh in doc['meshes'] for p in mesh['primitives'])
    assert total==fixture['triangles']+12 and total<fixture['budget_exclusive']
    assert 2<=len(doc['materials'])<=4
    for material in doc['materials']:
        pbr=material['pbrMetallicRoughness'];assert pbr['metallicFactor']==0
        if material.get('alphaMode')=='BLEND':
            assert abs(pbr['baseColorFactor'][3]-.25)<1e-6 and abs(pbr['roughnessFactor']-.2)<1e-6
    def mesh_bounds(mesh):
        accessors=[doc['accessors'][p['attributes']['POSITION']] for p in mesh['primitives']]
        return ([min(a['min'][i] for a in accessors) for i in range(3)],
                [max(a['max'][i] for a in accessors) for i in range(3)])
    lo,hi=mesh_bounds(doc['meshes'][model['mesh']]);clo,chi=mesh_bounds(doc['meshes'][collider['mesh']])
    assert abs(lo[2])<1e-6 and abs(lo[0]+hi[0])<1e-6 and abs(lo[1]+hi[1])<1e-6
    assert all(abs(x-y)<1e-6 for x,y in zip(lo+hi,clo+chi))
    for mesh in doc['meshes']:
        for p in mesh['primitives']:
            acc=doc['accessors'][p['attributes']['POSITION']];view=doc['bufferViews'][acc['bufferView']]
            offset=28+size+view.get('byteOffset',0)+acc.get('byteOffset',0)
            assert all(math.isfinite(v) for i in range(acc['count'])
                       for v in struct.unpack_from('<3f',blob,offset+i*view.get('byteStride',12)))
    fixture['total_triangles']=total
    levels=sorted({round(s['z'],5) for s in fixture['surfaces']})
    if len(levels)>2:
        spacings=[levels[i+1]-levels[i] for i in range(len(levels)-1)]
        assert max(spacings)-min(spacings)<1e-4,'Unequal shelf height intervals'
    print(fixture['name'],total,'triangles incl. collider;',fixture['slots'],'slots PASS')
placements=json.loads((folder/'DemoPlacements.json').read_text())
assert len(placements)==sum(f['slots'] for f in report['fixtures'])
assert all(p['scale']==1 and (root/'ArtSource'/p['product']).is_file() for p in placements)
report.update(shared_helpers_unchanged=list(helpers),demo_placements_verified=len(placements),
              blender_4_runtime_tested=False,unity_import_tested=False,
              collider_visibility_note='GLTF has no standard hidden mesh flag; disable collider renderer by name/extras at import.')
(folder/'FinalAudit.json').write_text(json.dumps(report,indent=2))
lines=['# Fixtures '+sys.argv[1],'','| Fixture | Triangles including collider | Slots |','| --- | ---: | ---: |']
for f in report['fixtures']:lines.append(f'| {f["name"]}.glb | {f["total_triangles"]} | {f["slots"]} |')
lines += ['', 'Each GLB is EMPTY and contains one fixture render mesh, child slot_01... Empty nodes and one materialless 12-triangle collider box. Demo products appear only in Fixtures.blend and Preview_Stocked.png. DemoPlacements.json records real existing product files at their original metre scale.',
          '', 'The fixture front is -Y, geometry is Z-up, local root transform is identity and the origin is bottom-centred. Opposite aisle slots have a 180-degree facing. Floor anchors were ray-tested against the final surface before export. Hanging anchors use kind=hang and their product top attachment is aligned to the hook.',
          '', 'Original authoring helpers, palette and bevel constants are retained. Fixture geometry is authored at 1:10 and baked into metres, giving furniture-sized 4.5 mm, two-segment bevels. Angled tray rims provide the slanted look while usable floors stay level.',
          '', 'IMPORTANT: Blender collider objects are hidden from render. Standard glTF has no universally supported hidden-mesh property: collider nodes carry extras.hidden=true and usage=collision_only; an engine importer must disable their renderer and use the mesh as collision. A generic GLB viewer may display the box.',
          '', 'Tested in installed Blender '+report['blender']+'. A separate Blender 4.x runtime and Unity integration were not tested. At Unity import convert axes, retain slot children, configure glass and disable collider renderers. Source geometry parts are separately editable in hidden *_EditableParts collections. No purchasing/build catalogue or Unity scene was modified.']
(folder/'README.md').write_text('\n'.join(lines)+'\n')
