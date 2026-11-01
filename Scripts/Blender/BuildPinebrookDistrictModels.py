"""English art brief: extend the approved cream/timber/teal low-poly Pinebrook kit.
All dimensions are metres. Buildings reuse the approved wall, balcony, roof and entrance functions.
"""
import json, math, shutil
from pathlib import Path
import bpy
from mathutils import Vector
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import PinebrookGeometry as g
from PinebrookAdditionalProps import bicycle_parts,stroller_parts,parking_sign_parts
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtSource/Pinebrook/DistrictModels';OUT.mkdir(parents=True,exist_ok=True)
g.OUT=OUT;g.ATLAS.filepath_raw=str(OUT/'PinebrookPalette.png');g.ATLAS.save()
records=[];models=[]
resume_path=OUT/'RevisionResume.json'
resume=json.loads(resume_path.read_text()) if '--resume-pinebrook' in sys.argv and resume_path.exists() else {}

def chamfer_slab(name,loc,size,index=8):
    x,y,z=size;radius=min(.07,x*.1,y*.1)
    ring=[(-x/2+radius,-y/2),(x/2-radius,-y/2),(x/2,-y/2+radius),(x/2,y/2-radius),(x/2-radius,y/2),(-x/2+radius,y/2),(-x/2,y/2-radius),(-x/2,-y/2+radius)]
    vertices=[(a+loc[0],b+loc[1],height+loc[2]) for height in (-z/2,z/2) for a,b in ring]
    faces=[tuple(reversed(range(8))),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    return g.mesh(name,vertices,faces,index)

def window(width=4.4,wood=False):
    parts=g.wall(True,wood,width)
    sill=next(o for o in parts if o.name.startswith('Window sill'))
    parts.remove(sill);bpy.data.objects.remove(sill,do_unlink=True)
    parts.append(chamfer_slab('Soft chamfered sill',(0,-.17,.59),(2.02,.36,.12)))
    # Real raised joinery survives the street camera; thin painted quads did not.
    for x in (-.97,.97):parts.append(g.box('Raised sage window jamb',(x,-.16,1.65),(.09,.11,2.16),5,.014))
    parts.append(g.box('Raised window head',(0,-.16,2.70),(2.03,.11,.10),5,.014))
    parts.append(g.box('Raised centre mullion',(0,-.10,1.65),(.055,.09,2),5,.01))
    if wood:
        for x in (-2.05,-1.81,-1.57,-1.33,1.33,1.57,1.81,2.05):
            if abs(x)<width/2-.05:parts.append(g.box('Individual timber batten',(x,-.16,1.6),(.10,.07,3.12),6,.014))
    else:
        for z in (.35,1.05,1.75,2.45,3.12):
            for side in (-1,1):
                length=max(.08,width/2-1.06)
                parts.append(g.box('Shallow masonry course',(side*(1.06+length/2),-.132,z),(length,.018,.022),1,0))
    return parts

def recolor(obj,index):
    uv=obj.data.uv_layers.active
    for value in uv.data:
        if abs(value.uv.x-.125)<.001 and abs(value.uv.y-.125)<.001:value.uv=((index%4+.5)/4,(index//4+.5)/4)

def export(name,parts,width,depth,floors,limit=6000,kind='building',tint=0):
    obj=g.join(name,parts);recolor(obj,tint);g.bake_vertex_occlusion(obj);obj.data.calc_loop_triangles();count=len(obj.data.loop_triangles)
    if count>limit:raise RuntimeError(f'{name}: {count} > {limit} triangles')
    record=dict(name=name,kind=kind,triangles=count,size_m=list(obj.dimensions),width=width,depth=depth,floors=floors,entrance=[0,0,-depth/2-1.05],limit=limit)
    for suffix in ('LOD0','LOD1') if kind=='building' else ('LOD0',):
        target=obj
        if suffix=='LOD1':
            target=obj.copy();target.data=obj.data.copy();g.active.objects.link(target);target.name=name+'_LOD1';mod=target.modifiers.new('Half density distant silhouette','DECIMATE');mod.ratio=.5;g.bake(target);target.data.calc_loop_triangles();record['lod1_triangles']=len(target.data.loop_triangles)
        bpy.ops.object.select_all(action='DESELECT');target.select_set(True);bpy.context.view_layer.objects.active=target
        bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'_'+suffix+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_unit_scale=True,add_leaf_bones=False)
        if suffix=='LOD1':target.hide_render=True;target.hide_viewport=True
    obj.name=name;models.append(obj);records.append(record);print(name,count,flush=True)
    return obj

def apartment(name,bays=4,floors=3,depth=12,tint=0,style='compact',tenant=None):
    g.collection(name);width=bays*4.4;height=floors*g.FLOOR;parts=[]
    if name in resume:
        bpy.ops.import_scene.fbx(filepath=str(OUT/(name+'_LOD0.fbx')))
        parts=[o for o in bpy.context.selected_objects if o.type=='MESH']
        for o in parts:
            for collection in list(o.users_collection):collection.objects.unlink(o)
            g.active.objects.link(o);o.data.materials.clear();o.data.materials.append(g.mat)
        return export(name,parts,width,depth,floors,70000,tint=tint)
    for floor in range(floors):
        for side in (-1,1):
            for bay in range(bays):
                if floor==0 and side==-1 and tenant:panel=g.shopfront()
                elif style=='loggia' and floor>0 and side==-1 and bay==1:panel=g.loggia()
                else:panel=window(wood=bay in (0,bays-1))
                parts+=g.transform(panel,(bay-(bays-1)/2)*4.4,side*depth/2,floor*g.FLOOR,0 if side==-1 else math.pi)
                balcony_here=floor>0 and (bay%2==0 or bay==bays-1)
                if style=='hero':balcony_here=floor>0 and bay in (0,2,4)
                if not(style=='loggia' and bay==1) and balcony_here:
                    parts+=g.transform(g.balcony(),(bay-(bays-1)/2)*4.4,side*(depth/2+.15),floor*g.FLOOR+.58,0 if side==-1 else math.pi)
        for side in (-1,1):
            for bay in range(3):parts+=g.transform(window(width=depth/3),side*width/2,(bay-1)*depth/3,floor*g.FLOOR,math.pi/2 if side==1 else -math.pi/2)
        for side in (-1,1):
            parts.append(g.box('Continuous floor cornice',(0,side*(depth/2+.16),(floor+1)*g.FLOOR-.10),(width+.18,.32,.17),8,.025))
    parts += [g.box('Warm enclosed shell',(0,0,height/2),(width-.4,depth-2.4,height),0),g.box('Foundation',(0,0,.12),(width+.25,depth+.25,.24),8)]
    parts+=g.transform(g.entrance(),0,-depth/2-.05,0)
    parts+=g.transform(g.roof(width+.8,depth+.8),0,0,height-.03)
    parts += [g.box('Chimney',(-width*.25,0,height+depth*.19-.1),(.8,.7,1.5),1),g.box('Chimney cap',(-width*.25,0,height+depth*.19+.69),(1,.9,.12),2)]
    for side in (-1,1):parts.append(g.rod('Rainwater pipe',(side*(width/2-.15),-depth/2-.2,0),(side*(width/2-.15),-depth/2-.2,height),.045))
    if tenant:parts.append(g.text('Tenant',tenant.upper(),(0,-depth/2-.30,2.97),width*.65))
    if style=='corner':parts+=g.transform(g.entrance(),width/2+.05,depth*.2,0,math.pi/2)
    return export(name,parts,width,depth,floors,70000,tint=tint)

apartment('Compact_Cream_3F',4,3,tint=0)
apartment('Compact_Sand_3F',4,3,tint=1)
apartment('Compact_Grey_3F',4,3,tint=13)
apartment('Long_Cream_4F',6,4,tint=0,style='long')
apartment('Long_Grey_4F',6,4,tint=13,style='long')
apartment('Corner_Sand_3F',5,3,tint=1,style='corner')
apartment('Loggia_Cream_3F',4,3,tint=0,style='loggia')
apartment('Loggia_Grey_3F',4,3,tint=13,style='loggia')
apartment('Mixed_Pharmacy_3F',5,3,style='mixed',tenant='Green Leaf Pharmacy')
apartment('Mixed_LaundryPost_3F',5,3,tint=13,style='mixed',tenant='Fresh Fold Laundry - Post & Parcel')
apartment('Mixed_Flowers_3F',5,3,tint=1,style='mixed',tenant='Petal & Stem Flowers')
# The approved three-balcony hero composition, with the roof seated into the wall cap.
apartment('Mixed_Bakery_3F',5,3,style='hero',tenant='Golden Crust Bakery')

def townhouse(name,tint):
    g.collection(name);parts=[];width=23.1;depth=9.5
    for floor in range(2):
        for side in (-1,1):
            for i in range(6):parts+=g.transform(window(width=3.85,wood=i in (0,5)),(i-2.5)*3.85,side*depth/2,floor*3.2,0 if side==-1 else math.pi)
        for side in (-1,1):
            for i in (-1,1):parts+=g.transform(window(width=depth/2),side*width/2,i*depth/4,floor*3.2,math.pi/2 if side==1 else -math.pi/2)
    parts+=[g.box('Townhouse shell',(0,0,3.2),(width-.4,depth-.4,6.4),0),g.box('Stone base',(0,0,.12),(width+.25,depth+.25,.24),8)]
    for i in (-1,0,1):parts+=g.transform(g.entrance(),i*7.7,-depth/2-.05,0)
    parts+=g.transform(g.roof(width+.8,depth+.8),0,0,6.37)
    return export(name,parts,width,depth,2,18000,tint=tint)
townhouse('Townhouse_Cream_2F',0);townhouse('Townhouse_Sand_2F',1)
apartment('CommunityCenter_2F',6,2,14,tint=0,tenant='Pinebrook Community Center')
apartment('Kindergarten_1F',6,1,12,tint=0,tenant='Little Pines Kindergarten')

# New low-poly storytelling props; modest bevels retain a soft silhouette.
def prop(name,fn):
    g.collection(name);parts=fn();obj=export(name,parts,0,0,0,499,kind='prop');return obj
prop('Planter_Long',lambda:[g.box('Planter',(0,0,.35),(2.2,.8,.7),6),g.box('Soil',(0,0,.71),(2,.66,.08),7)]+[g.box('Soft shrub',(x,0,.97),(.6,.6,.45),10,.12) for x in (-.7,0,.7)])
prop('Picnic_Table',lambda:[g.box('Table',(0,0,.8),(2,1,.16),6)]+[g.box('Leg',(x,y,.38),(.14,.14,.76),3) for x in (-.7,.7) for y in (-.35,.35)]+[g.box('Bench',(0,y,.45),(2,.3,.13),6) for y in (-.8,.8)])
prop('Table_Tennis',lambda:[g.box('Table',(0,0,.8),(2.7,1.5,.14),5),g.box('Centre net',(0,0,1.02),(.035,1.5,.3),9)]+[g.box('Support',(x,0,.37),(.12,1,.74),3) for x in (-.9,.9)])
prop('Workout_Bars',lambda:[g.rod('Post',(x,y,0),(x,y,2.1),.07,3) for x in (-1,1) for y in (-.5,.5)]+[g.rod('Bar',(-1,0,2.1),(1,0,2.1),.07,6)])
prop('Spring_Rider',lambda:[g.box('Rounded toy',(0,0,.9),(.9,.38,.5),6,.12),g.box('Head',(.4,0,1.15),(.4,.38,.45),6,.1),g.rod('Spring',(0,0,.12),(0,0,.65),.12,3),g.box('Seat',(0,0,1.19),(.5,.4,.1),5),g.box('Base',(0,0,.06),(.7,.7,.12),8)])
prop('Raised_Garden',lambda:[g.box('Garden bed',(0,0,.3),(2.8,1.2,.6),6),g.box('Soil',(0,0,.62),(2.6,1,.08),7)]+[g.box('Lettuce',(x,0,.81),(.6,.7,.32),10,.12) for x in (-.8,0,.8)])
prop('Notice_Board',lambda:[g.box('Notice case',(0,0,1.5),(1.7,.2,1.2),6),g.box('Cream notice',(0,-.12,1.5),(1.5,.03,1),9)]+[g.box('Post',(x,0,.9),(.12,.14,1.8),3) for x in (-.65,.65)])
prop('Dog_Walk_Sign',lambda:[g.box('Post',(0,0,.9),(.12,.12,1.8),3),g.box('Sign',(0,0,1.65),(.9,.14,.65),5),g.box('Dog body',(-.05,-.09,1.65),(.4,.04,.18),9),g.box('Dog head',(.21,-.09,1.77),(.2,.04,.2),9)])
prop('Fountain',lambda:[g.box('Octagonal basin',(0,0,.25),(2.8,2.8,.5),8,.24),g.box('Water',(0,0,.51),(2.4,2.4,.03),4,.2),g.box('Pedestal',(0,0,.8),(.5,.5,.6),8),g.box('Crown',(0,0,1.15),(1.2,1.2,.16),8,.15)])
prop('Sculpture',lambda:[g.box('Plinth',(0,0,.35),(1.4,1.4,.7),8),g.box('Art block',(-.25,0,1.4),(.5,.55,1.6),6,.1),g.box('Art block',(.3,0,1.7),(.6,.55,1.3),5,.1)])
prop('Kiosk',lambda:[g.box('Kiosk',(0,0,1.3),(2.6,2,2.6),0),g.box('Window',(0,-1.04,1.45),(2,.06,1.2),4),g.box('Counter',(0,-1.2,.82),(2.2,.5,.14),6),g.box('Roof',(0,0,2.7),(3,2.4,.18),2),g.box('Sage awning',(0,-1.3,2.3),(3,.9,.15),5)])
prop('Entrance_Sign',lambda:[g.box('Sign panel',(0,0,2.8),(4.8,.28,1.2),3),g.box('Raised frame',(0,.02,2.8),(5,.2,1.4),6)]+[g.box('Post',(x,0,1.55),(.18,.2,3.1),3) for x in (-2,2)])
prop('Street_Name_Sign',lambda:[g.box('Post',(0,0,1.35),(.12,.12,2.7),3),g.box('Sign',(0,0,2.4),(2.7,.14,.48),3)])
prop('Footbridge',lambda:[g.box('Bridge deck',(0,0,.3),(2.7,5.5,.25),6),g.box('Ramp',(0,-3,.12),(2.7,.7,.24),6),g.box('Ramp',(0,3,.12),(2.7,.7,.24),6)]+[g.rod('Railing',(x,-2.7,1.1),(x,2.7,1.1),.045,3) for x in (-1.3,1.3)]+[g.box('Rail post',(x,y,.7),(.09,.09,1.4),3) for x in (-1.3,1.3) for y in (-2.5,2.5)])
prop('Garden_Fence',g.garden_fence)
prop('Bicycle',bicycle_parts);prop('Stroller',stroller_parts);prop('Parking_Sign',parking_sign_parts)
kit=ROOT/'ArtSource/Pinebrook/Kit';kit_catalog=json.loads((kit/'Validation.json').read_text())['modules']
for name in ('AC_Unit','Satellite','Roof_Vent'):
 record=next(m for m in kit_catalog if m['name']==name);shutil.copy2(kit/(name+'.fbx'),OUT/(name+'_LOD0.fbx'));records.append(dict(name=name,kind='prop',triangles=record['triangles'],size_m=record['size_m'],width=0,depth=0,floors=0,limit=499,entrance=[0,0,-1.05]))
# Actual model inspection sheet.
for i,obj in enumerate(models):obj.location=(i%5*33,(i//5)*25,0)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.78,.81,.84,1);scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.7
bpy.ops.object.light_add(type='SUN');sun=bpy.context.object;sun.rotation_euler=(.45,-.5,-.3);sun.data.energy=2;sun.data.angle=.4
bpy.ops.object.camera_add(location=(150,-125,145));camera=bpy.context.object;camera.rotation_euler=(Vector((65,42,0))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=185;scene.camera=camera
scene.render.resolution_x=2400;scene.render.resolution_y=1600;scene.render.resolution_percentage=100;scene.render.filepath=str(OUT/'Models.png')
scene.view_settings.view_transform='AgX';bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'PinebrookDistrictModels.blend'));bpy.ops.render.render(write_still=True)
(OUT/'Models.json').write_text(json.dumps({'models':records},indent=2));print('PINEBROOK MODELS COMPLETE',flush=True)

