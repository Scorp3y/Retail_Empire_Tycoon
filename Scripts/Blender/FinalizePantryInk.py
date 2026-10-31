"""Normalize the ink material name to the existing Unity import contract.

Only names change. GLB positions, indices, colours and material settings stay
unchanged; no rerender is needed for a semantically invisible name correction.
"""
from pathlib import Path
import json
import struct
import bpy

root=Path(__file__).resolve().parents[2]
old='Pantry solid-colour ink';new='GeometryPigment'
source=root/'Scripts/Blender/FoodPantryGeometry.py'
source.write_text(source.read_text().replace(old,new),encoding='utf-8')
for folder in ('Food_07_PlantBasedSweet','Food_08_CookingEssentials'):
    directory=root/'ArtSource'/folder
    for path in directory.glob('*.glb'):
        blob=path.read_bytes();length,kind=struct.unpack_from('<II',blob,12)
        assert kind==0x4E4F534A
        doc=json.loads(blob[20:20+length]);changed=False
        for material in doc['materials']:
            if material['name']==old:material['name']=new;changed=True
        if not changed:continue
        encoded=json.dumps(doc,separators=(',',':')).encode('utf-8')
        encoded+=b' '*((-len(encoded))%4)
        tail=blob[20+length:]
        output=struct.pack('<III',0x46546C67,2,20+len(encoded)+len(tail))
        output+=struct.pack('<II',len(encoded),kind)+encoded+tail
        path.write_bytes(output)
    for script in directory.glob('Build*.py'):
        script.write_text(script.read_text().replace(old,new),encoding='utf-8')
    bpy.ops.wm.open_mainfile(filepath=str(directory/'Models.blend'))
    material=bpy.data.materials.get(old)
    if material:material.name=new
    bpy.ops.wm.save_as_mainfile(filepath=str(directory/'Models.blend'))
print('FINAL INK COMPATIBILITY PASS',flush=True)
