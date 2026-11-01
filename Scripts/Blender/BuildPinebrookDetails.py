from pathlib import Path
import sys,json,shutil
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bpy
from mathutils import Vector
import PinebrookGeometry as g
from PinebrookAdditionalProps import bicycle_parts,stroller_parts,parking_sign_parts
out=Path(__file__).resolve().parents[2]/'ArtSource/Pinebrook/DistrictModels'
p=out/'Models.json';data=json.loads(p.read_text());recipes={'Garden_Fence':g.garden_fence,'Bicycle':bicycle_parts,'Stroller':stroller_parts,'Parking_Sign':parking_sign_parts};objects=[]
for name,recipe in recipes.items():
    g.collection(name);obj=g.join(name,recipe());g.bake_vertex_occlusion(obj);obj.data.calc_loop_triangles();n=len(obj.data.loop_triangles);assert n<500,(name,n)
    bpy.ops.export_scene.fbx(filepath=str(out/(name+'_LOD0.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_unit_scale=True,add_leaf_bones=False)
    data['models']=[m for m in data['models'] if m['name']!=name];data['models'].append(dict(name=name,kind='prop',triangles=n,size_m=list(obj.dimensions),width=0,depth=0,floors=0,limit=499,entrance=[0,0,-1.05]));objects.append(obj);print(name,n,flush=True)
kit=Path(__file__).resolve().parents[2]/'ArtSource/Pinebrook/Kit';catalog=json.loads((kit/'Validation.json').read_text())['modules']
for name in ('AC_Unit','Satellite','Roof_Vent'):
    record=next(m for m in catalog if m['name']==name);shutil.copy2(kit/(name+'.fbx'),out/(name+'_LOD0.fbx'));data['models']=[m for m in data['models'] if m['name']!=name];data['models'].append(dict(name=name,kind='prop',triangles=record['triangles'],size_m=record['size_m'],width=0,depth=0,floors=0,limit=499,entrance=[0,0,-1.05]))
p.write_text(json.dumps(data,indent=2))
for i,obj in enumerate(objects):obj.location.x=i*3
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.8,.83,.85,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.75
bpy.ops.object.light_add(type='SUN');bpy.context.object.rotation_euler=(.5,-.5,-.3);bpy.context.object.data.energy=2;bpy.context.object.data.angle=.4
bpy.ops.object.camera_add(location=(10,-14,10));camera=bpy.context.object;camera.rotation_euler=(Vector((4.4,0,.6))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=13;scene.camera=camera
scene.render.resolution_x=1600;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.render.filepath=str(out/'AdditionalProps.png');bpy.ops.wm.save_as_mainfile(filepath=str(out/'PinebrookAdditionalProps.blend'));bpy.ops.render.render(write_still=True)
