"""Robber concept on the existing mini-character body, palette UVs and 14-bone rig."""
import argparse
import json
import math
import sys
from pathlib import Path
import bpy
from mathutils import Vector
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).parent))
import BuildShopperFleet as fleet
import BuildShopperStyleStudy as study
import BuildStoreStaffV2 as staff


def build(project):
    out=project/'Library/Robber/Exports';out.mkdir(parents=True,exist_ok=True)
    docs=project/'Docs/Robber';docs.mkdir(parents=True,exist_ok=True)
    source=project/'ArtSource/Characters/Robber/Robber.blend';source.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    fleet.configure_palette()
    rows=[list(row) for row in study.SWATCHES]
    rows[1][3]='526352';rows[0][1]='383C40';rows[0][0]='252E31'
    study.SWATCHES=tuple(tuple(row) for row in rows)
    design=staff.design_for('Robber',1,'crop',3,'light','brown',0)
    design['shoe']=1
    material=study.palette(out)
    body=study.body_mesh(project/'Assets/Models/mini characters/Models/OBJ format/character-male-a.obj',design,material)
    staff.skin_hands(body,design)
    parts=staff.StaffParts(None)
    head=body.vertex_groups['Head'].index
    points=[v.co for v in body.data.vertices if any(g.group==head and g.weight>.5 for g in v.groups)]
    low=Vector(tuple(min(v[i] for v in points) for i in range(3)));high=Vector(tuple(max(v[i] for v in points) for i in range(3)))
    width,depth,height=high-low;cx=(high.x+low.x)/2;cy=(high.y+low.y)/2
    cuff_z=low.z+height*.83
    parts.box('Beanie cuff',(cx,cy,cuff_z),(width*1.07,depth*1.05,.105),'charcoal','Head',.035,segments=2)
    rings=[]
    for rise,factor in ((.025,1),(.09,.99),(.17,.87),(.225,.63),(.25,.20)):
        ring=[]
        for i in range(16):
            angle=i*math.tau/16
            x=math.copysign(abs(math.cos(angle))**.55,math.cos(angle))*width*.49*factor
            y=math.copysign(abs(math.sin(angle))**.55,math.sin(angle))*depth*.50*factor
            ring.append(len(parts.vertices));parts.vertices.append((cx+x,cy+y,cuff_z+rise));parts.bones.append('Head')
        rings.append(ring)
    for a,b in zip(rings,rings[1:]):
        for i in range(16):parts.faces.append((a[i],a[(i+1)%16],b[(i+1)%16],b[i]));parts.colors.append('charcoal')
    parts.faces.append(tuple(rings[-1]));parts.colors.append('charcoal')
    # Eye openings follow the actual two authored eye components, preserving the original face.
    pos,uv,normals,faces=study.read_obj(project/'Assets/Models/mini characters/Models/OBJ format/character-male-a.obj')
    keys=study.component_keys(pos,faces)
    facial=[]
    for key in design['head_design']['parts']['face']:
        vertices=[study.converted(pos[corner[0]],True) for (group,corners),part in zip(faces,keys) if group=='head-mesh' and part==key for corner in corners]
        facial.append(vertices)
    eyes=sorted(facial,key=lambda p:sum(v.z for v in p)/len(p),reverse=True)[:2]
    mask_y=high.y+.012
    eye_centers=[]
    # A single soft eye-mask panel with true openings, rather than two spectacle rings.
    mask=bpy.data.curves.new('Eye mask with openings','CURVE');mask.dimensions='2D';mask.resolution_u=1;mask.fill_mode='BOTH';mask.extrude=.003;mask.bevel_depth=.002;mask.bevel_resolution=1
    def loop(points):
        spline=mask.splines.new('POLY');spline.points.add(len(points)-1)
        for item,(x,z) in zip(spline.points,points):item.co=(x,z,0,1)
        spline.use_cyclic_u=True
    eye_z=sum(sum(v.z for v in points)/len(points) for points in eyes)/2
    loop([(-.21,eye_z-.039),(-.19,eye_z-.065),(.19,eye_z-.065),(.21,eye_z-.039),(.21,eye_z+.037),(.185,eye_z+.061),(-.185,eye_z+.061),(-.21,eye_z+.037)])
    for points in eyes:
        x=sum(v.x for v in points)/len(points);z=sum(v.z for v in points)/len(points);eye_centers.append((x,z))
        loop([(x+math.cos(-i*math.tau/8)*.045,z+math.sin(-i*math.tau/8)*.033) for i in range(8)])
    mask_obj=bpy.data.objects.new('Eye mask',mask);bpy.context.collection.objects.link(mask_obj)
    mask_obj.rotation_euler.x=math.pi/2;mask_obj.location.y=mask_y
    bpy.ops.object.select_all(action='DESELECT');mask_obj.select_set(True);bpy.context.view_layer.objects.active=mask_obj
    bpy.ops.object.convert(target='MESH');bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    mask_obj.data.materials.append(material);layer=mask_obj.data.uv_layers.new(name='PaletteUV')
    for item in layer.data:item.uv=(.5/16,.14)
    mask_obj.vertex_groups.new(name='Head').add(list(range(len(mask_obj.data.vertices))),1,'REPLACE')
    staff.append_parts(body,mask_obj)
    parts.ball((0,-.142,.49),(.235,.10,.085),'forest','Spine',2)
    spine=body.vertex_groups['Spine'].index
    torso=[v.co for v in body.data.vertices if any(g.group==spine and g.weight>.5 for g in v.groups)]
    front=max(v.y for v in torso)+.015
    for x in (-.09,.09):
        parts.panel('Hoodie pocket',(x,front,.355),(.11,.018,.065),'forest','Spine',.012)
        parts.box('Drawstring',(x,front,.459),(.014,.012,.066),'gray','Spine',.006,segments=2)
    a=Vector((-.17,front+.018,.49));b=Vector((.19,front+.018,.295))
    parts.box('Canvas strap',(a+b)/2,(.046,.017,(b-a).length),'dark_brown','Spine',.007,(b-a).to_track_quat('Z','Y'),segments=2)
    parts.box('Canvas bag',(.28,.045,.25),(.20,.16,.22),'dark_brown','Hips',.035,segments=2)
    parts.box('Bag flap',(.28,.133,.285),(.19,.025,.10),'dark_brown','Hips',.023,segments=2)
    addition=parts.finish(material)
    for face in addition.data.polygons:face.use_smooth=True
    bpy.context.view_layer.objects.active=addition;addition.select_set(True)
    mod=addition.modifiers.new('Soft bevel normals','WEIGHTED_NORMAL');mod.keep_sharp=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    staff.append_parts(body,addition)
    rig,marker=study.rig(body);study.animations(rig)
    rig['character_role']='Robber';rig['base']='Existing mini-character shopper geometry and rig'
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.ops.object.select_all(action='DESELECT')
    for obj in (rig,body,marker):obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(out/'Robber.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
    study.studio(bpy.context.scene,docs/'BlenderPreview.png');bpy.context.scene.cycles.samples=24
    rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source))
    bpy.ops.render.render(write_still=True)
    manifest=dict(triangles=sum(len(p.vertices)-2 for p in body.data.polygons),bones=len(rig.data.bones),actions=[a.name for a in bpy.data.actions],source=str(source),export=str(out/'Robber.fbx'),eyes=eye_centers,head_bounds=[list(low),list(high)])
    (docs/'Model.json').write_text(json.dumps(manifest,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--project',type=Path,required=True)
    build(parser.parse_args(sys.argv[sys.argv.index('--')+1:]).project)
