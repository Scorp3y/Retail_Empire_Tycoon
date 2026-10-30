"""Bundle expansion geometry with the exact established authoring helpers."""
from pathlib import Path
import ast
import sys

root = Path(__file__).resolve().parents[2]
name, folder = sys.argv[1:3]
previous = (root / 'Scripts/Blender/BuildReadyMeals.py').read_text(encoding='utf-8-sig')
tree = ast.parse(previous)
base = previous[:previous.index('\nSEGMENTS = 10')].replace('Wave2_04_ReadyMeals', folder)
keys = ('shade_hex', 'prepare_palette', 'lathe', 'soft_cylinder', 'curved_label',
        'label_text', 'role', 'outline', 'mesh_object', 'loft', 'prism', 'soft_box', 'flat_label')
shared = '\n\n'.join(ast.get_source_segment(previous, node) for key in keys
                     for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == key)
stationery = ast.parse((root / 'Scripts/Blender/StationeryGeometry.py').read_text())
pigment = next(ast.get_source_segment((root / 'Scripts/Blender/StationeryGeometry.py').read_text(), n)
               for n in stationery.body if isinstance(n, ast.FunctionDef) and n.name == 'pigment')
script = base + '\nSEGMENTS=10\nFONT_PATH="C:/Windows/Fonts/arialbd.ttf"\nITERATION=2\n' + shared
for source in ['Wave2AssetWorkflow.py', 'ExpansionGeometry.py', name + 'Geometry.py']:
    if source == 'ExpansionGeometry.py':
        script += '\n\n' + pigment
    content = (root / 'Scripts/Blender' / source).read_text()
    if source == 'Wave2AssetWorkflow.py':
        # Large tools/textiles need real-scale spacing; the original preview
        # helper remains unchanged and still creates the camera and lights.
        content = content.replace('    preview(objects)\n',
            '    preview(objects)\n    if "arrange_expansion_preview" in globals(): arrange_expansion_preview(objects)\n')
    script += '\n\n' + content
compile(script, name, 'exec')
target = root / 'Scripts/Blender' / ('Build' + name + '.py')
target.write_text(script, encoding='utf-8')
(root / 'ArtSource' / folder).mkdir(parents=True, exist_ok=True)
(root / 'Library/Expansion' / name).mkdir(parents=True, exist_ok=True)
print(target)
