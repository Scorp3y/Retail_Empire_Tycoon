"""Render the actual exported facade at pedestrian distance, independently of the kit sheet."""
from pathlib import Path
import sys,bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
import PinebrookGeometry as g
bpy.ops.import_scene.fbx(filepath=str(ROOT/'ArtSource/Pinebrook/DistrictModels/Long_Cream_4F_LOD0.fbx'))
for obj in bpy.context.selected_objects:
    if obj.type=='MESH':obj.data.materials.clear();obj.data.materials.append(g.mat)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.75,.81,.85,1);scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.8
bpy.ops.object.light_add(type='SUN');bpy.context.object.rotation_euler=(.5,-.45,-.5);bpy.context.object.data.energy=2
bpy.ops.object.camera_add(location=(34,-40,22));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,6))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=39;scene.camera=camera
scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
scene.render.filepath=str(ROOT/'ArtSource/Pinebrook/DistrictModels/FacadeDetailReview.png');bpy.ops.render.render(write_still=True)
