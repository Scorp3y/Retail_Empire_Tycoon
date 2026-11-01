"""Rebuild the approved Pinebrook style checkpoint using the shared modelling library."""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import PinebrookGeometry as geometry
from PinebrookGeometry import *
module('Wall_Plain',lambda:wall(False));module('Wall_Window',wall);module('Wall_Timber',lambda:wall(True,True))
module('Wall_Balcony',lambda:wall()+transform(balcony(),0,-.15,.58));module('Wall_Loggia',loggia)
module('Corner',lambda:[box('Corner pier',(0,0,FLOOR/2),(.4,.4,FLOOR),0)])
module('Entrance',entrance);module('StairCore',lambda:wall()+[box('Staircore vertical strip',(-1.3,-.15,FLOOR/2),(.6,.1,FLOOR),6)])
module('Plinth',lambda:[box('Stone base',(0,0,.18),(BAY,.5,.36),8)])
module('Roof_Gable',roof)
module('Roof_Hip',lambda:[mesh('Hip roof',[(-2.5,-2.5,0),(2.5,-2.5,0),(2.5,2.5,0),(-2.5,2.5,0),(-1,0,1.1),(1,0,1.1)],[(0,1,5,4),(1,2,5),(2,3,4,5),(3,0,4)],2,rounded=.025)])
module('Roof_Corner',lambda:roof(4.4,4.4)+transform(roof(4.4,4.4),2.2,2.2,0,math.pi/2))
module('Roof_Dormer',lambda:[box('Dormer body',(0,0,.5),(1.4,1.2,1),0),box('Dormer pane',(0,-.62,.5),(1,.04,.7),4)]+transform(roof(1.7,1.6),0,0,1))
module('Chimney',lambda:[box('Chimney',(0,0,.9),(.8,.65,1.8),1),box('Chimney cap',(0,0,1.83),(1,.85,.12),2)])
module('Roof_Vent',lambda:[box('Vent',(0,0,.2),(.5,.5,.4),3),box('Vent cap',(0,0,.44),(.7,.7,.12),2)])
for name in ['Golden Crust Bakery','Green Leaf Pharmacy','Fresh Fold Laundry','Post & Parcel','Petal & Stem Flowers']:module('Shopfront_'+name.split()[0],lambda word=name:shopfront(word))
module('Downpipe',lambda:[rod('Downpipe',(0,0,0),(0,0,FLOOR),.045,3)])
module('AC_Unit',lambda:[box('AC body',(0,0,.3),(.8,.32,.6),11),box('AC grille',(0,-.18,.3),(.55,.03,.4),3)])
def satellite():
 p=[rod('Satellite support',(0,0,0),(0,0,.6),.03)]
 bpy.ops.mesh.primitive_uv_sphere_add(segments=8,ring_count=4,radius=1,location=(0,-.1,.65));dish=register(bpy.context.object,'Satellite curved dish',11)
 for v in dish.data.vertices:v.co=Vector((v.co.x*.32,v.co.y*.065,v.co.z*.28))
 p.append(dish);return p
module('Satellite',satellite)

collection('Hero_Editable')
parts=[]
for floor in range(3):
 for side in (-1,1):
  for bay in range(5):
   x=(bay-2)*BAY
   panel=shopfront() if floor==0 and side==-1 else wall(True,bay in (0,4))
   parts+=transform(panel,x,side*DEPTH/2,floor*FLOOR,0 if side==-1 else math.pi)
   if floor>0 and side==-1 and bay in (1,3) and not(floor==2 and bay==3):parts+=transform(balcony(),x,side*(DEPTH/2+.15),floor*FLOOR+.58)
 for side in (-1,1):
  for bay in range(3):parts+=transform(wall(True,False,width=4),side*11,(bay-1)*4,floor*FLOOR,math.pi/2 if side==1 else -math.pi/2)
parts+=[box('Building core',(0,0,4.8),(21.6,11.6,9.6),0),box('Whole plinth',(0,0,.12),(22.25,12.25,.24),8)]
parts+=transform(entrance(),0,-6.05,0)
parts+=transform(roof(22.8,12.8),0,0,9.7)
parts+=[box('Chimney',(-6,0,12.3),(.8,.8,2),1),box('Chimney cap',(-6,0,13.35),(1,1,.15),2)]
for x in (-10.9,10.9):parts.append(rod('Full-height downpipe',(x,-6.28,.15),(x,-6.28,9.6),.045,3))
parts.append(text('Golden Crust Bakery sign','GOLDEN CRUST BAKERY',(0,-6.30,2.97),14))
hero=join('Pinebrook_Hero_LOD0',parts);hero.data.calc_loop_triangles();count=len(hero.data.loop_triangles)
records.append({'name':hero.name,'triangles':count,'size_m':list(hero.dimensions)})
print('HERO TRIANGLES',count,flush=True)
print('HERO BOUNDS', min(v.co.z for v in hero.data.vertices),max(v.co.z for v in hero.data.vertices),flush=True)
if count>8000:raise RuntimeError('Hero exceeds 8000-triangle hard limit')
bpy.ops.export_scene.fbx(filepath=str(OUT/'Pinebrook_Hero_LOD0.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_unit_scale=True,add_leaf_bones=False)
lod=hero.copy();lod.data=hero.data.copy();geometry.active.objects.link(lod);lod.name='Pinebrook_Hero_LOD1';mod=lod.modifiers.new('LOD1 half budget','DECIMATE');mod.ratio=.5;bake(lod);lod.data.calc_loop_triangles();records.append({'name':lod.name,'triangles':len(lod.data.loop_triangles),'size_m':list(lod.dimensions)})
bpy.ops.export_scene.fbx(filepath=str(OUT/'Pinebrook_Hero_LOD1.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_unit_scale=True,add_leaf_bones=False)
lod.hide_render=True;lod.hide_viewport=True
# This is the actual existing cafe mesh from Unity, not a reconstruction.
collection('Existing_Cafe_Reference')
ref=json.loads((ROOT/'ArtSource/Pinebrook/Audit/CafeReference.json').read_text())
for i,surface in enumerate(ref['surfaces']):
 vertices=[(v['x'],v['y'],v['z']) for v in surface['vertices']];indices=surface['triangles'];data=bpy.data.meshes.new('Existing cafe');data.from_pydata(vertices,[],[indices[k:k+3] for k in range(0,len(indices),3)]);data.update()
 o=bpy.data.objects.new('Existing Cafe Reference '+str(i),data);geometry.active.objects.link(o)
 rm=bpy.data.materials.new('Existing material '+str(i));rm.use_nodes=True;shader=rm.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.85
 node=rm.node_tree.nodes.new('ShaderNodeVertexColor');node.layer_name='ReferenceColor';rm.node_tree.links.new(node.outputs['Color'],shader.inputs['Base Color']);o.data.materials.append(rm)
 attr=o.data.color_attributes.new(name='ReferenceColor',type='FLOAT_COLOR',domain='CORNER')
 for poly in data.polygons:
  for loop in poly.loop_indices:
   c=surface['colors'][data.loops[loop].vertex_index];attr.data[loop].color_srgb=(c['r'],c['g'],c['b'],1)
 o.location.x=-16
hero.location.x=16
# Separate kit presentation remains editable in its own collections.
for i,o in enumerate(modules):o.location=(i%6*6-16,(i//6)*7+23,0);o.hide_render=True
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32
scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.78,.81,.84,1)
scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.7
bpy.ops.object.light_add(type='SUN',location=(0,0,35));sun=bpy.context.object;sun.rotation_euler=(.45,-.5,-.3);sun.data.energy=2;sun.data.angle=.4
collection('Preview')
bpy.ops.mesh.primitive_plane_add(size=220);ground=bpy.context.object;ground.name='Preview ground';gm=bpy.data.materials.new('Warm preview ground');gm.diffuse_color=(.72,.74,.66,1);ground.data.materials.append(gm);ground.location.z=-.04
bpy.ops.object.light_add(type='AREA',location=(-20,-25,42));light=bpy.context.object;light.data.energy=6500;light.data.shape='DISK';light.data.size=35
bpy.ops.object.camera_add(location=(30,-56,28));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,5))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=65;scene.camera=camera
scene.render.resolution_x=2200;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.filepath=str(OUT/'Comparison.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'PinebrookKit.blend'));bpy.ops.render.render(write_still=True)
hero.hide_render=True
for o in bpy.data.collections['Existing_Cafe_Reference'].objects:o.hide_render=True
for o in modules:o.hide_render=False
camera.location=(16,-10,43);target=Vector((0,34,0));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=59
scene.render.filepath=str(OUT/'Kit.png');bpy.ops.render.render(write_still=True)
(OUT/'Validation.json').write_text(json.dumps({'blender':bpy.app.version_string,'floorHeight':FLOOR,'sharedMaterial':'PinebrookShared','palette':'PinebrookPalette.png','modules':records},indent=2))
print('PINEBROOK CHECKPOINT COMPLETE',flush=True)
