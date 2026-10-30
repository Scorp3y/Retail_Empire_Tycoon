"""Fixture hierarchy export, surface anchors and real-product preview staging."""
import argparse
import shutil
import hashlib
from pathlib import Path

# Author at 1:10, then bake to metres. Original 0.45 mm / 2-segment helpers
# produce 4.5 mm furniture bevels without changing the approved helper code.
AUTHOR_SCALE=.1
SLOTS=[]
SURFACES=[]


def fbox(name,position,size,color='cream'):
    return soft_box(name,Vector(position)*AUTHOR_SCALE,Vector(size)*AUTHOR_SCALE,make_material(color),'fixture')


def fpanel(name,contour,depth,y,color):
    obj=prism(name,[(x*AUTHOR_SCALE,z*AUTHOR_SCALE) for x,z in contour],
              -depth*AUTHOR_SCALE/2,depth*AUTHOR_SCALE/2,make_material(color),'fixture')
    obj.rotation_euler.x=math.pi/2;obj.location.y=y*AUTHOR_SCALE;apply_transform(obj)
    return obj


def frib(name,points,radius,color='dark_green'):
    return rounded_rib(name,[tuple(Vector(p)*AUTHOR_SCALE) for p in points],radius*AUTHOR_SCALE,color)


def fside(name,contour,thickness,x,color):
    obj=fpanel(name,contour,thickness,0,color)
    obj.rotation_euler.z=math.pi/2;apply_transform(obj);obj.location.x=x*AUTHOR_SCALE
    return obj


def fixture_glass():
    PALETTE['fixture_glass']='#BFD5D7'
    mat=make_material('fixture_glass',.25)
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.2
    mat.node_tree.nodes['Principled BSDF'].inputs['Transmission Weight'].default_value=.75
    mat.node_tree.nodes['Principled BSDF'].inputs['IOR'].default_value=1.05
    return mat


def display_tray(parts,z,width,depth,y,products,count=3,slats=False):
    parts.append(fbox('Level tray floor',(0,y,z-.020),(width,depth,.040),'peach'))
    parts.append(fbox('Low forward tray lip',(0,y-depth/2+.015,z+.025),(width,.030,.050),'peach'))
    if slats:
        for zz in (z+.040,z+.105):parts.append(fbox('Crate back slat',(0,y+depth/2-.015,zz),(width,.025,.045),'peach'))
        for side in (-1,1):
            for zz in (z+.030,z+.095):parts.append(fbox('Crate end slat',(side*(width/2-.015),y,zz),(.030,depth-.06,.044),'peach'))
    else:
        parts.append(fbox('High rear tray lip',(0,y+depth/2-.015,z+.065),(width,.030,.130),'peach'))
        profile=[(y-depth/2,z),(y+depth/2,z),(y+depth/2,z+.13),(y-depth/2,z+.05)]
        for side in (-1,1):parts.append(fside('Angled tray side',profile,.030,side*(width/2-.015),'peach'))
    slots_row(z,width-.08,count,y,depth-.05,.28,products=products)


def slots_row(z,width,count=3,y=0,depth=.30,height=.38,kind='floor',products=None):
    for i in range(count):
        SLOTS.append(dict(position=[-width/2+width*(i+.5)/count,y,z],kind=kind,
            space=[width/count*.92,depth,height],products=products or []))
    if kind=='floor':SURFACES.append(dict(z=z,y=y,width=width,depth=depth,count=count))


def shelf_board(parts,z,width,depth,y=0,color='cream',count=3,lip=True,height=.38,products=None):
    parts.append(fbox('Level shelf board',(0,y,z-.025),(width,depth,.050),color))
    if lip:parts.append(fbox('Small rounded front lip',(0,y-depth/2+.012,z+.012),(width,.024,.024),color))
    slots_row(z,width-.07,count,y,depth-.06,height,products=products)


def frame_posts(parts,width,depth,height,back=True):
    for x in (-width/2+.035,width/2-.035):
        for y in ((depth/2-.035,) if back else (-depth/2+.035,depth/2-.035)):
            parts.append(fbox('Chunky green upright',(x,y,height/2),(.070,.070,height),'dark_green'))


def rounded_sign(parts,width,z_bottom,z_top,depth=.045,y=.12,color='dark_green'):
    radius=min(.12,width*.18)
    contour=[(-width/2,z_bottom),(width/2,z_bottom),(width/2,z_top-radius)]
    for i in range(1,5):
        a=i*math.pi/8;contour.append((width/2-radius+radius*math.cos(a),z_top-radius+radius*math.sin(a)))
    contour.append((-width/2+radius,z_top))
    for i in range(1,5):
        a=math.pi/2+i*math.pi/8;contour.append((-width/2+radius+radius*math.cos(a),z_top-radius+radius*math.sin(a)))
    parts.append(fpanel('Rounded sign board',contour,depth,y,color))


def demo_path(relative):return str(Path(ARTSOURCE)/relative)


def bounds(obj):
    ps=[obj.matrix_world@v.co for v in obj.data.vertices]
    return Vector([min(p[i] for p in ps) for i in range(3)]),Vector([max(p[i] for p in ps) for i in range(3)])


def export_fixture(model,children,directory):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [model]+children:obj.select_set(True)
    bpy.context.view_layer.objects.active=model
    collider=next(o for o in children if o.name=='collider')
    collider.hide_render=False
    path=Path(directory)/(model.name+'.glb')
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
        export_yup=EXPORT_Y_UP,export_apply=True,export_animations=False,export_cameras=False,
        export_lights=False,export_texcoords=False,export_materials='EXPORT',export_extras=True)
    collider.hide_render=True
    data=path.read_bytes();n=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+n])
    assert len(doc['meshes'])==2 and len(doc['nodes'])==len(children)+1
    assert not doc.get('images') and not doc.get('textures')
    root=next(n for n in doc['nodes'] if n['name']==model.name)
    assert not any(k in root for k in ('translation','rotation','scale','matrix'))
    slot_nodes=[n for n in doc['nodes'] if n['name'].startswith('slot_')]
    assert len(slot_nodes)==len(SLOTS)
    collision=next(n for n in doc['nodes'] if n['name']=='collider')
    assert collision['extras']['hidden'] and all('material' not in p for p in doc['meshes'][collision['mesh']]['primitives'])
    root_mesh=doc['meshes'][root['mesh']]
    count=sum(doc['accessors'][p['indices']]['count']//3 for p in root_mesh['primitives'])
    assert count==model['triangles']
    assert 2<=len(doc['materials'])<=4
    return dict(sha256=hashlib.sha256(data).hexdigest(),triangles=count,collider_triangles=12,total_triangles=count+12,
                slots=len(slot_nodes),materials=len(doc['materials']))


def import_demo(relative,collection):
    path=Path(ARTSOURCE)/relative
    if not path.is_file():raise FileNotFoundError('Existing demo product not found: '+str(path))
    before=set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    imported=[o for o in bpy.data.objects if o not in before]
    meshes=[o for o in imported if o.type=='MESH']
    assert len(meshes)==1,(relative,'expected existing single-mesh product')
    obj=meshes[0]
    # Our exported products explicitly use Z-up; glTF import assumes Y-up.
    obj.data.transform(Matrix.Rotation(-math.pi/2,4,'X')@obj.matrix_world)
    obj.matrix_world=Matrix.Identity(4)
    for c in list(obj.users_collection):c.objects.unlink(obj)
    collection.objects.link(obj)
    for other in imported:
        if other!=obj:bpy.data.objects.remove(other,do_unlink=True)
    obj.parent=None;obj.name='DemoTemplate_'+path.stem
    return obj


def previews(models,records,output):
    global ACTIVE_COLLECTION
    preview(models)
    scene=bpy.context.scene;cursor=0
    for obj in models:
        obj.location.x=cursor+obj.dimensions.x/2;cursor+=obj.dimensions.x+.35
    span=cursor-.35;center=span/2;height=max(o.dimensions.z for o in models)
    ground=bpy.data.objects.get('Preview ground');ground.location.x=center;ground.scale=(span*2,span*2,1)
    target=Vector((center,0,height*.44));cam=scene.camera
    cam.location=target+Vector((.15,-1.10,.645)).normalized()*span*1.25
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale=max(span+.3,height*3.2);cam.data.clip_end=100
    for obj in scene.objects:
        if obj.type=='LIGHT':
            side=-1 if 'key' in obj.name.lower() else 1
            obj.location=(center+side*span*.28,side*span*.30,span*.65)
            obj.data.energy=(18 if side==-1 else 8)*(span/1.2)**2
            obj.data.size=span*.65
            obj.rotation_euler=(target-obj.location).to_track_quat('-Z','Y').to_euler()
    scene.render.resolution_x=3200;scene.render.resolution_y=1400;scene.render.resolution_percentage=100
    scene.cycles.samples=32
    scene.render.filepath=str(Path(output)/'Preview_Empty.png');bpy.ops.render.render(write_still=True)
    templates=bpy.data.collections.new('Demo_Templates_Hidden');scene.collection.children.link(templates);templates.hide_render=True
    demos=bpy.data.collections.new('Demo_Products_Preview_Only');scene.collection.children.link(demos)
    cache={};placements=[]
    for model,record in zip(models,records):
        for i,slot in enumerate(record['anchors']):
            options=slot['products'];assert options,'No real product assigned to slot'
            chosen=None
            for k in range(len(options)):
                relative=options[(i+k)%len(options)]
                if relative not in cache:cache[relative]=import_demo(relative,templates)
                template=cache[relative];lo,hi=bounds(template);size=hi-lo
                space=slot['space']
                if size.x<=space[0]+.001 and size.y<=space[1]+.001 and size.z<=space[2]+.001:
                    chosen=(relative,template,lo,hi);break
            assert chosen,(model.name,slot,'No product fits without rescaling')
            relative,template,lo,hi=chosen
            copy=template.copy();demos.objects.link(copy);copy.hide_render=False;copy.name='Demo_'+model.name+'_'+str(i+1)
            position=Vector(slot['position'])+model.location
            if slot['kind']=='hang':
                top=[v.co for v in template.data.vertices if v.co.z>=hi.z-.004]
                mount=sum(top,Vector())/len(top);mount.z=hi.z
            else:mount=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
            copy.location=position-mount
            copy.rotation_euler.z=slot.get('rotation_z',0)
            placements.append(dict(fixture=model.name,slot=i+1,product=relative,scale=1,kind=slot['kind'],mount_local=list(mount)))
    scene.render.filepath=str(Path(output)/'Preview_Stocked.png');bpy.ops.render.render(write_still=True)
    (Path(output)/'DemoPlacements.json').write_text(json.dumps(placements,indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(Path(output)/'Fixtures.blend'))


def run_fixtures():
    global ACTIVE_COLLECTION,OUTPUT_DIR,ITERATION,ARTSOURCE,SLOTS,SURFACES
    parser=argparse.ArgumentParser();parser.add_argument('--output',default=OUTPUT_DIR)
    parser.add_argument('--iteration',type=int,default=2);parser.add_argument('--artsource')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    ITERATION=args.iteration;root=Path(args.output);OUTPUT_DIR=str(root/'Iterations'/f'{ITERATION:02d}');Path(OUTPUT_DIR).mkdir(parents=True,exist_ok=True)
    ARTSOURCE=args.artsource or str(root.parent)
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    for c in list(bpy.data.collections):bpy.data.collections.remove(c)
    MATERIALS.clear();prepare_asset_palette();expansion_palette()
    scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    models=[];records=[]
    for key,builder,budget in JOBS:
        collection=bpy.data.collections.new(key);scene.collection.children.link(collection)
        source=bpy.data.collections.new(key+'_EditableParts');collection.children.link(source)
        ACTIVE_COLLECTION=source;SLOTS=[];SURFACES=[];parts=builder()
        for p in parts:
            apply_modifiers(p)
            p.data.transform(Matrix.Scale(1/AUTHOR_SCALE,4));p.location*=1/AUTHOR_SCALE
        bpy.context.view_layer.update()
        ps=[p.matrix_world@v.co for p in parts for v in p.data.vertices]
        low=Vector([min(p[i] for p in ps) for i in range(3)]);high=Vector([max(p[i] for p in ps) for i in range(3)])
        offset=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
        ACTIVE_COLLECTION=collection;copies=[]
        for p in parts:
            copy=p.copy();copy.data=p.data.copy();collection.objects.link(copy);copies.append(copy)
        model=join_and_seat(key,copies,budget-12);models.append(model)
        for slot in SLOTS:slot['position']=list(Vector(slot['position'])-offset)
        children=[]
        for i,slot in enumerate(SLOTS):
            obj=bpy.data.objects.new(f'slot_{i+1:02d}',None);collection.objects.link(obj);obj.parent=model
            obj.location=slot['position'];obj.empty_display_size=.035;obj['kind']=slot['kind'];obj['front']='-Y';obj['space_m']=slot['space'];children.append(obj)
            obj.rotation_euler.z=slot.get('rotation_z',0)
            if slot.get('rotation_z',0):obj['front']='+Y'
            if slot['kind']=='floor':
                hit,point,*_=model.ray_cast(obj.location+Vector((0,0,.003)),Vector((0,0,-1)),distance=.020)
                assert hit and abs(point.z-obj.location.z)<.0015,(key,obj.name,'Slot not seated on surface',tuple(point))
            else:
                from mathutils.bvhtree import BVHTree
                tree=BVHTree.FromPolygons([v.co for v in model.data.vertices],[list(f.vertices) for f in model.data.polygons])
                nearest=tree.find_nearest(obj.location,.035)
                assert nearest[0] is not None,(key,obj.name,'Hanging anchor misses the hook')
        lo,hi=bounds(model)
        verts=[(x,y,z) for x in (lo.x,hi.x) for y in (lo.y,hi.y) for z in (lo.z,hi.z)]
        faces=[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)]
        mesh=bpy.data.meshes.new('collider_box');mesh.from_pydata(verts,[],faces);mesh.update()
        collision=bpy.data.objects.new('collider',mesh);collection.objects.link(collision);collision.parent=model
        collision['hidden']=True;collision['usage']='collision_only';collision.display_type='WIRE';collision.hide_render=True;children.append(collision)
        record=export_fixture(model,children,OUTPUT_DIR)
        source.hide_render=True;source.hide_viewport=True
        record.update(name=key,budget_exclusive=budget,anchors=SLOTS.copy(),size_m=list(hi-lo),surfaces=SURFACES.copy())
        records.append(record);print('FIXTURE PASS',key,record['triangles'],'triangles',record['slots'],'slots',flush=True)
        # Blender object names are global; retain exact exported child names per file.
        for child in children:child.name=key+'__'+child.name
    (Path(OUTPUT_DIR)/'Validation.json').write_text(json.dumps(dict(group=GROUP,iteration=ITERATION,blender=bpy.app.version_string,fixtures=records),indent=2))
    previews(models,records,OUTPUT_DIR)
    for name in [j[0]+'.glb' for j in JOBS]+['Fixtures.blend','Preview_Empty.png','Preview_Stocked.png','Validation.json','DemoPlacements.json']:
        shutil.copy2(Path(OUTPUT_DIR)/name,root/name)
    script=Path(__file__).resolve()
    if script!=(root/SCRIPT_NAME).resolve():shutil.copy2(script,root/SCRIPT_NAME)
    print('GROUP COMPLETE',GROUP,'iteration',ITERATION,flush=True)
