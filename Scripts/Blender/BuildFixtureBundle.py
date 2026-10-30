"""Create a standalone fixture script using the unchanged product authoring helpers."""
from pathlib import Path
import ast
import sys

root=Path(__file__).resolve().parents[2]
group=sys.argv[1]
old=(root/'Scripts/Blender/BuildReadyMeals.py').read_text(encoding='utf-8-sig')
tree=ast.parse(old)
base=old[:old.index('\nSEGMENTS = 10')].replace('Wave2_04_ReadyMeals','Fixtures_'+group)
keys=('shade_hex','prepare_palette','lathe','soft_cylinder','curved_label','label_text',
      'role','outline','mesh_object','loft','prism','soft_box','flat_label')
script=base+'\nSEGMENTS=10\nITERATION=2\nFONT_PATH="C:/Windows/Fonts/arialbd.ttf"\n'
script+='\n\n'.join(ast.get_source_segment(old,n) for key in keys for n in tree.body
                      if isinstance(n,ast.FunctionDef) and n.name==key)
workflow=(root/'Scripts/Blender/Wave2AssetWorkflow.py').read_text().split('\ndef run_category():')[0]
script+='\n\n'+workflow
script+='\n\n'+(root/'Scripts/Blender/ExpansionGeometry.py').read_text()
script+='\n\n'+(root/'Scripts/Blender/FixtureWorkflow.py').read_text()
script+='\n\n'+(root/'Scripts/Blender'/('Fixture'+group+'Geometry.py')).read_text()
compile(script,'Fixtures_'+group,'exec')
target=root/'Scripts/Blender'/('BuildFixtures'+group+'.py');target.write_text(script,encoding='utf-8')
(root/'ArtSource'/('Fixtures_'+group)).mkdir(exist_ok=True)
(root/'Library/Fixtures'/group).mkdir(parents=True,exist_ok=True)
print(target)
