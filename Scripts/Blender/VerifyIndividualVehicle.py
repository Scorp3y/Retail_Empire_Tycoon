"""Validate a reopened individual blend and render its front, rear and profile."""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent))
import bmesh
import bpy
from mathutils import Vector
from IndividualVehicleCraft import frame_camera

def require(condition,message):
    if not condition:raise AssertionError(message)

parser=argparse.ArgumentParser()
parser.add_argument('--project',required=True,type=Path)
parser.add_argument('--id',required=True)
parser.add_argument('--primary-only',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
project=args.project.resolve()
source=project/'ArtSource/TrafficVehicles'/args.id/(args.id+'.blend')
review=project/'Docs/VehicleStages'/args.id
require(Path(bpy.data.filepath).resolve()==source,'Wrong model opened')
root=bpy.data.objects.get(args.id)
require(root is not None and root.type=='EMPTY','One named vehicle root is required')
require(len(bpy.data.scenes)==1,'Only one scene/car per file')
design=json.loads(root['design_manifest'])
require(design['identity']==args.id,'Wrong design manifest')
require(bpy.context.scene.unit_settings.system=='METRIC','Metre units required')
cab_only=design['family'] in ('delivery','flatbed','box_truck','camper')
if cab_only:
    require(bpy.data.objects.get('Rear lamp mounting crossmember') is not None,
        'Cargo-vehicle tail lamps need a physical mounting crossmember')
    for step in (obj for obj in root.children if obj.name.startswith('Cab step')):
        front=step.location.y+max(vertex.co.y for vertex in step.data.vertices)
        require(front<design['front_axle']-design['radius']-.025,
            'Cab step intersects the front tyre: '+step.name)
wheel_roots=[obj for obj in root.children if obj.name.startswith('Wheel_')]
require(len(wheel_roots)==4,'Exactly four independent running wheel pivots required')
for obj in wheel_roots:
    require(len(obj.children)==5,'Approved tyre, rim, hubcap, fasteners and valve required')
    require(abs(obj.location.z-design['radius'])<1e-5,'Wheel pivot must be at axle height')
    for part in obj.children:
        bm=bmesh.new();bm.from_mesh(part.data)
        require(all(edge.is_manifold for edge in bm.edges),'Open wheel mesh: '+part.name)
        require(bm.calc_volume()>1e-9,'Invalid wheel normals: '+part.name)
        bm.free()
glass=bpy.data.materials['Glass / opaque blue graphite'].node_tree.nodes['Principled BSDF']
require(glass.inputs['Alpha'].default_value==1 and glass.inputs['Transmission Weight'].default_value==0,'Windows must remain opaque')
for suffix in ('Paint roughness','Rubber micrograin','Lamp lens fluting','Tyre tread relief'):
    image=bpy.data.images.get(args.id[:2]+' '+suffix)
    require(image is not None and image.packed_file is not None,'Missing packed map '+suffix)
require(any(image.packed_file and image.name.startswith('Reference') for image in bpy.data.images),'Reference must be embedded')
bpy.context.view_layer.update()
deps=bpy.context.evaluated_depsgraph_get()
triangles=0
bad=[]
geometry_bounds=[]
below_ground=[]
for obj in root.children_recursive:
    if obj.type not in ('MESH','CURVE','FONT'):continue
    if obj.type=='MESH':require(obj.data.uv_layers.active is not None,'Missing UVs: '+obj.name)
    evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh();mesh.calc_loop_triangles()
    triangles+=len(mesh.loop_triangles)
    points=[evaluated.matrix_world@vertex.co for vertex in mesh.vertices]
    geometry_bounds.extend(points)
    if any(point.z<-.003 for point in points):below_ground.append(obj.name)
    if any(t.area<1e-11 for t in mesh.loop_triangles):bad.append(obj.name)
    evaluated.to_mesh_clear()
require(not bad,'Degenerate mesh triangles: '+', '.join(bad))
scene=bpy.context.scene
bounds=frame_camera(scene,root)
bounds=[[min(point[i] for point in geometry_bounds) for i in range(3)],
        [max(point[i] for point in geometry_bounds) for i in range(3)]]
require(not below_ground,'Geometry extends below ground: '+', '.join(below_ground)+' '+str(bounds[0][2]))
report={'identity':args.id,'reopened':True,'model_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
        'independent_wheels':4,'closed_approved_wheel_parts':True,'opaque_windows':True,
        'packed_surface_maps':4,'zero_area_triangles':0,'triangles':triangles,'bounds':bounds,
        'family':design['family'],'unity_modified':False,'views_complete':False}
report['cab_step_clearance']=True if cab_only else None
report['mounted_rear_lights']=True if cab_only else None
scene.render.filepath=str(review/'Front.png')
bpy.ops.render.render(write_still=True)
if not args.primary_only:
    frame_camera(scene,root,(-.65,-.76,.34))
    scene.render.filepath=str(review/'Rear.png')
    bpy.ops.render.render(write_still=True)
    frame_camera(scene,root,(-1,0,.04))
    scene.camera.data.type='ORTHO'
    scene.camera.data.ortho_scale=(bounds[1][1]-bounds[0][1])*1.18
    scene.camera.data.shift_x=0;scene.camera.data.shift_y=0
    scene.render.filepath=str(review/'Side.png')
    bpy.ops.render.render(write_still=True)
    report['views_complete']=True
(review/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('VERIFIED '+args.id+' '+str(triangles)+' triangles',flush=True)
