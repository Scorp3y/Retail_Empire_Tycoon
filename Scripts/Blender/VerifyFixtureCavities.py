"""Probe empty render meshes, ignoring the deliberately solid collider."""
import bpy
import bmesh
import json
import sys
from pathlib import Path
from mathutils import Vector

root=Path(sys.argv[sys.argv.index('--')+1])/'ArtSource'
def load(group):
    folder=root/('Fixtures_'+group)
    bpy.ops.wm.open_mainfile(filepath=str(folder/'Fixtures.blend'))
    for c in bpy.data.collections:c.hide_viewport=False
    bpy.context.view_layer.update()
    return folder,json.loads((folder/'Validation.json').read_text())

folder,data=load('A');record=data['fixtures'][3];model=bpy.data.objects[record['name']]
centre=sum((Vector(a['position']) for a in record['anchors']),Vector())/len(record['anchors'])
hit,p,*_=model.ray_cast(centre+Vector((0,0,1)),Vector((0,0,-1)),distance=2)
assert hit and abs(p.z-centre.z)<.0015,'Promotion bin top is blocked'
(folder/'CavityAudit.json').write_text(json.dumps(dict(bin_open_top=True,bin_floor_z=p.z),indent=2))
print('PASS open bin and real floor',flush=True)

folder,data=load('B');record=data['fixtures'][3];model=bpy.data.objects[record['name']]
centre=sum((Vector(a['position']) for a in record['anchors']),Vector())/len(record['anchors']);inside=centre+Vector((0,0,.10))
down,p,*_=model.ray_cast(inside,Vector((0,0,-1)),distance=1)
up,top,*_=model.ray_cast(inside,Vector((0,0,1)),distance=1)
assert down and up and abs(p.z-centre.z)<.0015 and top.z-p.z>.25
glass=[]
for obj in bpy.data.objects:
    if obj.name.startswith('Curved pale glass side') and any(c.name.endswith('_EditableParts') for c in obj.users_collection):
        bm=bmesh.new();bm.from_mesh(obj.data);assert all(len(e.link_faces)==2 for e in bm.edges)
        volume=abs(bm.calc_volume());bm.free();assert volume>1e-5;glass.append(volume)
assert len(glass)==2
(folder/'CavityAudit.json').write_text(json.dumps(dict(freezer_actual_interior_height=top.z-p.z,freezer_floor_z=p.z,
                        freezer_glass_underside_z=top.z,closed_thick_side_glass_volumes=glass),indent=2))
print('PASS real freezer interior and solid glass side walls',flush=True)

folder,data=load('D');record=data['fixtures'][0];model=bpy.data.objects[record['name']];results=[]
for anchor in record['anchors']:
    point=Vector(anchor['position'])+Vector((0,-.20,.18))
    hit,p,*_=model.ray_cast(point,Vector((0,1,0)),distance=1)
    assert hit and p.y-point.y>.25,'Cubby front is blocked'
    results.append(p.y-point.y)
(folder/'CavityAudit.json').write_text(json.dumps(dict(open_cubby_fronts=len(results),depth_probes=results),indent=2))
print('PASS six open-front cubbies',flush=True)
