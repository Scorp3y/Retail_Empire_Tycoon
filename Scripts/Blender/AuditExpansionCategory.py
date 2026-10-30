"""Audit the published geometry itself, including node axes and original helpers."""
from pathlib import Path
import ast
import hashlib
import json
import math
import struct
import sys

root = Path(__file__).resolve().parents[2]
folder = root / 'ArtSource' / sys.argv[1]
script = next(folder.glob('Build*.py'))
current = ast.parse(script.read_text(encoding='utf-8-sig'))
old = ast.parse((root / 'Scripts/Blender/BuildReadyMeals.py').read_text(encoding='utf-8-sig'))
helpers = ('srgb_to_linear', 'color_rgba', 'make_material', 'register', 'apply_transform',
           'make_sphere', 'add_bevel', 'apply_modifiers', 'make_leaf', 'make_disk',
           'join_and_seat', 'export_glb', 'preview', 'shade_hex', 'prepare_palette',
           'lathe', 'soft_cylinder', 'curved_label', 'label_text')
def functions(tree):
    return {n.name: ast.dump(n, include_attributes=False) for n in tree.body if isinstance(n, ast.FunctionDef)}
a, b = functions(current), functions(old)
assert all(a[n] == b[n] for n in helpers), 'Shared helper was changed'
for key in ('PALETTE', 'BEVEL_WIDTH', 'BEVEL_ANGLE', 'ROW_SPACING', 'EXPORT_Y_UP'):
    def value(tree):
        return next(ast.dump(n.value, include_attributes=False) for n in tree.body if isinstance(n, ast.Assign)
                    and any(isinstance(t, ast.Name) and t.id == key for t in n.targets))
    assert value(current) == value(old), key
report = json.loads((folder / 'Validation.json').read_text())
assert report['iteration'] >= 2
for i in (1, 2):
    assert (folder / 'Iterations' / f'{i:02d}' / 'Preview.png').is_file()
for entry in report['models']:
    data = (folder / (entry['name'] + '.glb')).read_bytes()
    size = struct.unpack_from('<I', data, 12)[0]
    doc = json.loads(data[20:20+size]); start = 28+size
    assert len(doc['meshes']) == len(doc['nodes']) == 1
    assert not doc.get('images') and not doc.get('textures')
    assert 2 <= len(doc['materials']) <= 4
    assert not any(k in doc['nodes'][0] for k in ('translation', 'rotation', 'scale', 'matrix'))
    count = sum(doc['accessors'][p['indices']]['count']//3 for p in doc['meshes'][0]['primitives'])
    assert count == entry['triangles'] and count < entry['budget_exclusive']
    points = []
    for p in doc['meshes'][0]['primitives']:
        if doc['materials'][p['material']]['name'] == 'GeometryPigment':
            assert 'COLOR_0' in p['attributes']
        acc = doc['accessors'][p['attributes']['POSITION']]; view = doc['bufferViews'][acc['bufferView']]
        offset = start+view.get('byteOffset', 0)+acc.get('byteOffset', 0)
        for i in range(acc['count']):
            point = struct.unpack_from('<3f', data, offset+i*view.get('byteStride', 12))
            assert all(math.isfinite(v) for v in point)
            points.append(point)
    low = [min(p[i] for p in points) for i in range(3)]
    high = [max(p[i] for p in points) for i in range(3)]
    assert abs(low[2]) < 1e-7 and abs(low[0]+high[0]) < 1e-7 and abs(low[1]+high[1]) < 1e-7
    entry['sha256'] = hashlib.sha256(data).hexdigest()
    print(entry['name'], count, 'triangles PASS')
report.update(shared_helpers_unchanged=list(helpers), blender_4_runtime_tested=False,
              unity_import_tested=False, geometry_axis='Z-up', exported_objects_per_file=1)
(folder / 'FinalAudit.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
lines = ['# '+folder.name, '', '| Model | Triangles | Materials | Metres X / Y / Z |',
         '| --- | ---: | ---: | --- |']
for m in report['models']:
    lines.append(f'| {m["name"]}.glb | {m["triangles"]} | {m["materials"]} | '+
                 ' / '.join(f'{x:.4f}' for x in m['size_m'])+' |')
lines += ['', 'Each model has one exported mesh node with identity transforms, bottom-centred origin and Z-up geometry. No image textures. Original palette, 19 shared functions and 0.00045 m / 2 segment bevel settings retained.',
          '', 'Models.blend preserves hidden *_EditableParts collections as separate editable meshes. Joined preview copies are arranged in a row; GLBs are exported before that arrangement.',
          '', 'Both iteration renders are retained in Iterations. See Review.md for visual findings and corrections.',
          '', 'Tested in installed Blender '+report['blender']+'. The script uses the established Blender 4.x-compatible API helpers; a separate 4.x runtime was not available. Import into Unity, shader setup for vertex colours and transparent surface sorting still need engine-side verification. These files have not been added to the purchasing catalogue.']
(folder / 'README.md').write_text('\n'.join(lines)+'\n', encoding='utf-8')
