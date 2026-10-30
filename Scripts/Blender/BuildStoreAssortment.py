"""Build the approved 48 low-poly products and four EMPTY, stockable fixtures.

Every asset has an editable .blend source, a centred floor pivot, one mesh and
one shared texture/material. Fixture contents are created by Unity stock slots.
"""
import argparse
import hashlib
import json
import math
import random
import sys
from pathlib import Path
import bpy
from mathutils import Vector
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).parent))
from AssortmentDesigns import COLORS,products,FIXTURES

PARTS=[]
MATERIAL=None
GLASS=None

def paint(obj,color):
    obj.data.materials.clear();obj.data.materials.append(MATERIAL)
    uv=obj.data.uv_layers.new(name='Assortment') if not obj.data.uv_layers else obj.data.uv_layers.active
    uv.name='Assortment'
    index=list(COLORS).index(color)
    point=((index%16+.5)/16,1-(index//16+.5)/8)
    for loop in uv.data:loop.uv=point
    PARTS.append(obj)
    return obj

def box(name,center,size,color,bevel=.005,rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=center);o=bpy.context.object;o.name=name
    o.scale=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        m=o.modifiers.new('Soft low-poly edges','BEVEL');m.width=min(bevel,min(size)*.20);m.segments=2
        bpy.ops.object.modifier_apply(modifier=m.name)
    if rotation:o.rotation_euler=rotation
    return paint(o,color)

def ball(name,center,size,color,level=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=level,radius=1,location=center)
    o=bpy.context.object;o.name=name;o.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return paint(o,color)

def profile(name,center,rings,color,segments=12):
    vertices=[];faces=[]
    for z,radius in rings:
        vertices.extend((center[0]+radius*math.cos(i*math.tau/segments),center[1]+radius*math.sin(i*math.tau/segments),center[2]+z) for i in range(segments))
    for ring in range(len(rings)-1):
        for i in range(segments):
            a=ring*segments+i;b=ring*segments+(i+1)%segments
            faces.append((a,b,b+segments,a+segments))
    faces.append(tuple(reversed(range(segments))))
    faces.append(tuple((len(rings)-1)*segments+i for i in range(segments)))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    return paint(obj,color)

def stem(name,start,end,radius,color):
    a=Vector(start);b=Vector(end);vector=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=radius,depth=vector.length,location=(a+b)/2)
    o=bpy.context.object;o.name=name;o.rotation_euler=vector.to_track_quat('Z','Y').to_euler()
    return paint(o,color)

def leaf(name,start,end,width,color):
    a=Vector(start);b=Vector(end);middle=(a+b)*.5
    side=Vector((-(b-a).y,(b-a).x,0)).normalized()*width
    vertices=[a,middle+side,b,middle-side,middle+Vector((0,0,width*.22))]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],[(0,1,4),(1,2,4),(2,3,4),(3,0,4),(3,2,1,0)]);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);return paint(obj,color)

def label(p,width,height,y,z):
    # Label is a texture quad; product names do not add high-poly 3D lettering.
    mesh=bpy.data.meshes.new('Printed label')
    mesh.from_pydata([(-width/2,y,z-height/2),(width/2,y,z-height/2),(width/2,y,z+height/2),(-width/2,y,z+height/2)],[],[(0,1,2,3)])
    mesh.update();obj=bpy.data.objects.new('Printed '+p['name'],mesh);bpy.context.collection.objects.link(obj)
    obj.data.materials.append(MATERIAL);uv=mesh.uv_layers.new(name='Assortment')
    x=p['index']%8*256+7;top=512+p['index']//8*256+7
    left=x/2048;right=(x+242)/2048;bottom=1-(top+242)/2048;upper=1-top/2048
    for loop,point in zip(uv.data,[(left,bottom),(right,bottom),(right,upper),(left,upper)]):loop.uv=point
    PARTS.append(obj)

def glass(name,vertices):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],[tuple(range(len(vertices)))]);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    obj.data.materials.append(GLASS);PARTS.append(obj)

def bag_body(color,soft):
    rings=[(0,.081,.036),(.024,.094,.050),(.13,.105,.058),(.23,.09,.037),(.26,.094,.013)] if soft else [
        (0,.084,.039),(.018,.095,.050),(.23,.095,.050),(.256,.083,.024)]
    vertices=[]
    for z,w,d in rings:
        bevel=.011
        vertices.extend((x,y,z) for x,y in [(-w+bevel,-d),(w-bevel,-d),(w,-d+bevel),(w,d-bevel),
            (w-bevel,d),(-w+bevel,d),(-w,d-bevel),(-w,-d+bevel)])
    faces=[tuple(reversed(range(8)))]
    for ring in range(len(rings)-1):
        for i in range(8):faces.append((ring*8+i,ring*8+(i+1)%8,(ring+1)*8+(i+1)%8,(ring+1)*8+i))
    faces.append(tuple((len(rings)-1)*8+i for i in range(8)))
    mesh=bpy.data.meshes.new('Folded packaging');mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new('Soft filled bag' if soft else 'Folded paper bag',mesh);bpy.context.collection.objects.link(obj)
    return paint(obj,color)

def tray(color='dark',width=.29,depth=.21):
    box('Food tray base',(0,0,.012),(width,depth,.024),color)
    for x in (-width/2,width/2):box('Raised side',(x,0,.033),(.011,depth,.045),color)
    for y in (-depth/2,depth/2):box('Raised lip',(0,y,.033),(width,.011,.045),color)

def package(p):
    shape=p['shape'];color=p['color']
    if shape in ('box','block','pizza_box','chocolate'):
        size=(.18,.08,.27) if shape=='box' else (.21,.12,.09) if shape=='block' else (.30,.08,.30) if shape=='pizza_box' else (.17,.026,.24)
        box('Bevelled packaging',(0,0,size[2]/2),size,color)
        label(p,size[0]*.85,size[2]*.84,-size[1]/2-.001,size[2]/2)
        if shape=='chocolate':
            for x in (-.055,0,.055):
                for z in (.16,.205):box('Chocolate squares',(x,-.022,z),(.048,.012,.036),'chocolate',.003)
        return
    if shape in ('bag','paper_bag','window_bag','pouch','cookies'):
        width=.19;depth=.10;height=.28
        if shape=='pouch':depth=.12;height=.29
        bag_body(color,shape in ('pouch','window_bag','cookies'))
        box('Top folded seam',(0,0,height*.94),(width*.95,.028,.027),'cream' if shape=='paper_bag' else color,.004)
        front=-.059 if shape in ('pouch','window_bag','cookies') else -.051
        label(p,width*.82,height*.65,front-.001,height*.48)
        if shape in ('window_bag','cookies'):
            box('Product window',(0,front-.003,.074),(.135,.006,.072),'clear',.004)
            rng=random.Random(p['index'])
            for i in range(9):
                x=rng.uniform(-.055,.055);z=rng.uniform(.052,.093)
                fill={'pasta':'yellow','nuts':'brown','gummies':['red','sage','yellow'][i%3],
                      'dumplings':'ivory','mixed_vegetables':['leaf','orange','yellow'][i%3],
                      'frozen_berries':['blue_dark','purple','red'][i%3], 'fries':'yellow','cookies':'wood'}.get(p['key'],'yellow')
                if p['key'] in ('pasta','fries'):
                    box('Visible dry goods',(x,front-.008,z),(.009,.006,.034),fill,.002,rotation=(0,rng.uniform(-.6,.6),0))
                else:ball('Visible contents',(x,front-.008,z),(.013,.006,.012),fill)
        return
    if shape in ('bottle','soap','jug'):
        jug=shape=='jug';height=.34 if jug else .31;radius=.087 if jug else .062
        profile('Bottle body',(0,0,0),[(0,radius*.75),(.012,radius),(.20 if jug else .22,radius),(.25 if jug else .255,radius*.45),(.28,radius*.40)],color)
        profile('Screw cap',(0,0,0),[(.28,radius*.44),(height,radius*.44)],'red' if shape=='soap' else 'green' if p['key']=='kefir' else color)
        label(p,.095 if not jug else .12,.14,-radius-.001,.14)
        if jug:
            stem('Handle outer',(.075,0,.25),(.115,0,.19),.012,'blue')
            stem('Handle grip',(.115,0,.19),(.10,0,.11),.012,'blue')
            stem('Handle root',(.10,0,.11),(.07,0,.10),.012,'blue')
        if shape=='soap':profile('Red nozzle',(0,0,height),[(0,.025),(.025,.018)],'red')
        return
    if shape in ('tub','open_tub'):
        profile('Dairy cup',(0,0,0),[(0,.065),(.12,.082),(.132,.085)],'white')
        if shape=='open_tub':
            for i in range(14):
                a=i*2.4;r=.05*math.sqrt(i/14)
                ball('Curd',(math.cos(a)*r,math.sin(a)*r,.133),(.018,.017,.011),'ivory')
        else:profile('Foil lid',(0,0,0),[(.132,.087),(.145,.087)],color)
        label(p,.095,.085,-.083,.073);return
    if shape=='carton':
        box('Cream carton',(0,0,.13),(.14,.11,.26),'white')
        box('Folded blue roof',(0,0,.278),(.14,.11,.055),color,rotation=(0,.0,0))
        box('Fold seam',(0,0,.310),(.14,.014,.018),color,.003)
        profile('Plastic cap',(.037,-.019,.305),[(0,.018),(.014,.018)],'white')
        label(p,.115,.21,-.056,.14);return
    if shape=='can':
        profile('Can',(0,0,0),[(0,.058),(.01,.062),(.23,.062),(.24,.058)],color,16)
        profile('Can top',(0,0,.24),[(0,.060),(.006,.060)],'silver',16)
        profile('Bottom rim',(0,0,.003),[(0,.063),(.006,.063)],'silver',16)
        box('Pull tab',(0,0,.248),(.025,.012,.003),'metal',.001)
        label(p,.083,.17,-.063,.125);return
    if shape=='sponges':
        for i in range(3):
            box('Sponge foam',(0,(i-1)*.054,.055),(.19,.045,.10),'yellow' if i!=2 else 'blue',.007)
            box('Scrubbing side',(0,(i-1)*.054,.108),(.19,.045,.012),'green',.002)
        label(p,.15,.065,-.079,.052);return
    if shape in ('roll','roll_pack'):
        positions=[(0,0)] if shape=='roll' else [(x,y) for x in (-.06,.06) for y in (-.06,.06)]
        for x,y in positions:
            profile('Paper roll',(x,y,0),[(0,.052),(.16 if shape=='roll_pack' else .27,.052)],'white',16)
            profile('Cardboard centre',(x,y,.161 if shape=='roll_pack' else .271),[(0,.017),(.001,.017)],'beige')
        if shape=='roll_pack':box('Paper multipack wrap',(0,-.116,.046),(.23,.01,.078),'blue',.003)
        return
    raise ValueError('Unknown package '+shape)

def produce(p):
    key=p['key'];rng=random.Random(22+p['index'])
    if p['shape']=='berries':
        tray('clear',.25,.19)
        for i in range(18):
            x=(i%6-2.5)*.036;y=(i//6-1)*.047
            ball('Berry',(x,y,.063),(.021,.022,.031 if key=='strawberries' else .021),p['color'],2 if key=='strawberries' else 1)
            if key=='strawberries':
                for j in range(3):leaf('Berry calyx',(x,y,.09),(x+math.cos(j*math.tau/3)*.017,y+math.sin(j*math.tau/3)*.017,.094),.006,'leaf')
        return
    if key=='pineapple':
        body=ball('Pineapple',(0,0,.14),(.095,.088,.14),'yellow',2)
        uv=body.data.uv_layers.active
        for polygon in body.data.polygons:
            if polygon.index%7==0:
                cell=list(COLORS).index('gold')
                for index in polygon.loop_indices:uv.data[index].uv=((cell%16+.5)/16,1-(cell//16+.5)/8)
        for i in range(9):
            a=i*math.tau/9
            leaf('Crown',(0,0,.25),(math.cos(a)*.10,math.sin(a)*.10,.34+(i%3)*.025),.023,'leaf' if i%2 else 'green')
        return
    if key=='watermelon':
        ball('Watermelon',(0,0,.13),(.18,.13,.13),'green',2)
        for i in range(8):
            angle=i*math.tau/8;vertices=[]
            for step in range(13):
                latitude=-math.pi/2+.03+step*(math.pi-.06)/12
                for side in (-1,1):
                    longitude=angle+side*.055
                    vertices.append((.181*math.sin(latitude),.131*math.cos(latitude)*math.cos(longitude),.13+.131*math.cos(latitude)*math.sin(longitude)))
            mesh=bpy.data.meshes.new('Melon stripe');mesh.from_pydata(vertices,[],[(j*2,j*2+1,j*2+3,j*2+2) for j in range(12)]);mesh.update()
            obj=bpy.data.objects.new('Melon stripe',mesh);bpy.context.collection.objects.link(obj);paint(obj,'leaf')
        # Separate red wedge makes the fruit readable from the catalogue angle.
        obj=box('Watermelon wedge',(.10,-.145,.047),(.19,.055,.09),'red',.015)
        box('Rind',(.10,-.145,.008),(.19,.055,.012),'sage')
        for x in (.05,.10,.15):ball('Seeds',(x,-.176,.055),(.003,.002,.006),'dark')
        return
    if key=='cabbage':
        ball('Cabbage core',(0,0,.12),(.13,.13,.12),'sage',2)
        for i in range(8):
            a=i*math.tau/8
            ball('Cabbage leaf',(math.cos(a)*.062,math.sin(a)*.062,.105),(.095,.065,.105),'leaf' if i%3==0 else 'sage')
        return
    if key=='mushrooms':
        tray('wood',.26,.20)
        for i in range(6):
            x=(i%3-1)*.071;y=(i//3-.5)*.08
            profile('Mushroom stem',(x,y,.025),[(0,.015),(.047,.013)],'beige',8)
            profile('Mushroom cap',(x,y,.065),[(0,.037),(.016,.041),(.040,.028),(.045,.007)],'ivory',10)
        return
    raise ValueError('Unknown produce '+key)

def meat(p):
    key=p['key'];tray('beige' if key=='drumsticks' else 'dark',.30,.23)
    if key=='salmon':
        box('Salmon skin',(0,0,.045),(.24,.17,.014),'metal')
        box('Salmon fillet',(0,0,.073),(.235,.17,.047),'salmon',.015)
        for i in range(7):stem('Cream fish grain',(-.09+i*.029,-.07,.099),(-.07+i*.029,.07,.099),.0028,'ivory')
    elif key=='tuna':
        for x in (-.065,.065):
            ball('Tuna steak',(x,0,.058),(.071,.085,.026),'meat',2)
            stem('Steak grain',(x-.025,-.06,.082),(x+.025,.06,.082),.002,'pink')
    elif key=='shrimp':
        for i in range(6):
            cx=(i%3-1)*.082;cy=(i//3-.5)*.09
            for j in range(5):
                a=-.7+j*.75
                ball('Shrimp segment',(cx+math.cos(a)*.023,cy+math.sin(a)*.023,.058),(.017,.014,.015),'salmon')
    elif key=='bacon':
        for i in range(5):
            x=(i-2)*.043
            box('Bacon rasher',(x,0,.05),(.038,.18,.02),'meat',.006)
            for offset in (-.009,.009):box('Bacon fat',(x+offset,0,.062),(.007,.17,.003),'ivory',.001)
    elif key=='ground_beef':
        for row in range(8):
            for i in range(5):
                x=-.10+i*.047;y=-.078+row*.023
                stem('Mince strand',(x,y,.055),(x+.025,y+.006,.065),.008,'meat' if row%2 else 'pink')
    elif key=='drumsticks':
        for x in (-.075,.06):
            for y in (-.05,.055):
                ball('Chicken meat',(x,y,.063),(.039,.038,.028),'peach',2)
                stem('Chicken bone',(x+.02,y,.063),(x+.058,y,.061),.009,'ivory')
    else:raise ValueError(key)

def fixture(p):
    key=p['key'];w=p['width'];d=p['depth'];h=p['height']
    if key in ('household_rack','snack_endcap'):
        wood=key=='snack_endcap';surface='wood' if wood else 'cream'
        for x in (-w/2+.025,w/2-.025):
            for y in (-d/2+.025,d/2-.025):box('Upright',(x,y,h/2),(.035,.035,h),'green',.004)
        box('Rear panel',(0,d/2-.018,h/2),(w-.05,.022,h-.08),'wood_dark' if wood else 'sage',.003)
        tier_spacing=.27 if wood else .31
        for tier in range(3):
            z=.11+tier*tier_spacing
            box('Shelf deck',(0,0,z),(w-.05,d-.03,.027),surface,.005)
            box('Front shelf lip',(0,-d/2+.008,z+.025),(w-.04,.025,.06 if wood else .038),'wood_dark' if wood else 'cream',.004)
            if wood:
                for x in (-w/2+.024,w/2-.024):box('Tray side',(x,0,z+.03),(.025,d,.072),'wood_dark',.004)
        box('Header',(0,d/2-.02,h-.035),(w+.02,.035,.07),'green',.005)
        return
    box('Dark plinth',(0,0,.045),(w,d,.09),'green',.012)
    if key=='chest_freezer':
        box('Insulated base',(0,0,.22),(w,d,.32),'cream',.018)
        for x in (-w/2+.026,w/2-.026):box('Insulated end',(x,0,.50),(.052,d,.32),'cream',.008)
        for y in (-d/2+.024,d/2-.024):box('Insulated side',(0,y,.50),(w-.06,.048,.32),'cream',.008)
        box('Freezer inner floor',(0,0,.375),(w-.10,d-.10,.018),'metal',.004)
        # Wire lid outlines leave an unobstructed view of actual runtime stock.
        for x in (-w/2+.035,0,w/2-.035):box('Sliding lid rail',(x,0,h),(.018,d-.02,.022),'metal',.003)
        for y in (-d/2+.015,d/2-.015):box('Sliding lid trim',(0,y,h),(w,.022,.022),'metal',.004)
        for x in (-w*.23,w*.23):box('Sliding lid handle',(x,-d*.25,h+.012),(.12,.022,.018),'green',.004)
        for left,right in ((-w/2+.045,-.012),(.012,w/2-.045)):
            glass('Sliding glass lid',[(left,-d/2+.03,h-.005),(right,-d/2+.03,h-.005),(right,d/2-.03,h-.005),(left,d/2-.03,h-.005)])
    else:
        box('Counter base',(0,0,.225),(w,d,.36),'cream',.015)
        box('Counter green belt',(0,-d/2-.001,.19),(w,.015,.055),'green',.003)
        box('Refrigerated bed',(0,0,.42),(w-.06,d-.05,.025),'metal',.004)
        for x in (-w/2+.024,w/2-.024):
            box('Side pedestal',(x,0,.46),(.048,d,.10),'cream',.006)
            stem('Slanted glass frame',(x,-d/2,.48),(x,-d*.23,h),.012,'metal')
            stem('Rear upright',(x,d/2,.43),(x,d/2,h),.012,'metal')
        box('Counter top',(0,d*.08,h),(w,.40,.032),'green',.008)
        box('Front glass sill',(0,-d/2,.47),(w,.021,.022),'silver',.004)
        # Thin edge tint suggests glass without making opaque blue walls over goods.
        for x in (-w*.16,w*.16):stem('Glass seam',(x,-d/2,.48),(x,-d*.23,h-.015),.003,'pale_blue')
        glass('Slanted display glass',[(-w/2+.025,-d/2,.48),(w/2-.025,-d/2,.48),(w/2-.025,-d*.23,h-.015),(-w/2+.025,-d*.23,h-.015)])
        for x in (-w/2+.02,w/2-.02):glass('Side glass',[(x,-d/2,.48),(x,-d*.23,h-.015),(x,d/2-.02,h-.015),(x,d/2-.02,.45)])

def finish(design,source,output):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in PARTS:obj.select_set(True)
    bpy.context.view_layer.objects.active=PARTS[0];bpy.ops.object.join();obj=bpy.context.object;obj.name=design['key']
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    # Goods have one submesh; only the two refrigerated fixtures add a glass pass.
    glass_faces=[obj.data.materials[p.material_index].name.startswith('AssortmentGlass') for p in obj.data.polygons]
    obj.data.materials.clear();obj.data.materials.append(MATERIAL)
    if any(glass_faces):obj.data.materials.append(GLASS)
    for polygon,is_glass in zip(obj.data.polygons,glass_faces):polygon.material_index=1 if is_glass else 0
    for vertex in obj.data.vertices:
        if not all(math.isfinite(v) for v in vertex.co):raise ValueError('Nonfinite mesh')
    source.parent.mkdir(parents=True,exist_ok=True);output.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source),check_existing=False)
    bpy.ops.export_scene.fbx(filepath=str(output),use_selection=True,object_types={'MESH'},
        apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False,
        use_mesh_modifiers=True,add_leaf_bones=False,path_mode='STRIP')
    return dict(key=design['key'],vertices=len(obj.data.vertices),triangles=sum(len(p.vertices)-2 for p in obj.data.polygons),
        source=str(source),fbx=str(output),sha256=hashlib.sha256(output.read_bytes()).hexdigest())

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--project',required=True);parser.add_argument('--only')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);root=Path(args.project)
    source=root/'ArtSource/Assortment';target=root/'Library/CityUpdateQA/IsolatedProject/Assets/Art/Assortment'
    if not (source/'AssortmentAtlas.png').exists():raise FileNotFoundError('Run PrepareAssortmentAtlas first')
    result=[]
    for kind,designs in (('Products',products()),('Fixtures',FIXTURES)):
        for design in designs:
            if args.only and args.only!=design['key']:continue
            path=source/kind/design['key']/(design['key']+'.blend')
            if path.exists():raise FileExistsError('Preserve existing artist source before rebuilding: '+str(path))
            bpy.ops.wm.read_factory_settings(use_empty=True);PARTS.clear()
            image=bpy.data.images.load(str(source/'AssortmentAtlas.png'));image.pack()
            global MATERIAL,GLASS
            MATERIAL=bpy.data.materials.new('AssortmentAtlas');MATERIAL.use_nodes=True
            node=MATERIAL.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
            shader=MATERIAL.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.75
            MATERIAL.node_tree.links.new(node.outputs['Color'],shader.inputs['Base Color'])
            GLASS=bpy.data.materials.new('AssortmentGlass');GLASS.diffuse_color=(.67,.83,.78,.12)
            if kind=='Fixtures':fixture(design)
            elif design['storage']=='Produce':produce(design)
            elif design['storage']=='Meat':meat(design)
            else:package(design)
            result.append(finish(design,path,target/'Models'/(design['key']+'.fbx')))
            print('ASSORTMENT '+design['key']+' '+str(result[-1]['triangles'])+' triangles',flush=True)
    manifest_path=source/'Models.json'
    if args.only and manifest_path.exists():
        other=json.loads(manifest_path.read_text(encoding='utf-8'));result=[p for p in other if p['key']!=args.only]+result
    manifest_path.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print('Completed '+str(len(result))+' editable model sources.',flush=True)

if __name__=='__main__':main()
