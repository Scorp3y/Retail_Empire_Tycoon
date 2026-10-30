"""Check the two corrected physical details in the final editable source scene."""
import bpy
import bmesh
import json
import sys
from pathlib import Path
from mathutils import Vector

folder=Path(sys.argv[sys.argv.index('--')+1])
bpy.ops.wm.open_mainfile(filepath=str(folder/'Models.blend'))
for collection in bpy.data.collections:
    collection.hide_viewport=False
bpy.context.view_layer.update()
cap=next(o for o in bpy.data.objects if o.name.startswith('Separate hollow rounded cap')
         and any(c.name.endswith('_EditableParts') for c in o.users_collection))
hit,top,*_=cap.ray_cast(Vector((0,0,1)),Vector((0,0,-1)),distance=2)
inside,roof,*_=cap.ray_cast(Vector((0,0,.087)),Vector((0,0,1)),distance=1)
assert hit and inside and top.z>.139 and .137<roof.z<.139
assert top.z-roof.z>.001
papers=[]
for obj in bpy.data.objects:
    if not obj.name.startswith('Bent tissue with real thickness') or obj.type!='MESH':continue
    bm=bmesh.new();bm.from_mesh(obj.data)
    assert all(len(e.link_faces)==2 for e in bm.edges),'Open paper edge'
    volume=abs(bm.calc_volume());bm.free()
    assert volume>1e-8,'Paper has no physical thickness'
    papers.append(dict(part=obj.name,volume_m3=volume))
assert len(papers)==3
report=dict(closed_deodorant_roof_z=top.z,inner_roof_z=roof.z,
            cap_roof_thickness_m=top.z-roof.z,closed_tissue_meshes=papers)
(folder/'SourceAudit.json').write_text(json.dumps(report,indent=2))
print('PASS closed cap roof, inner cavity and 3 closed paper solids',flush=True)
