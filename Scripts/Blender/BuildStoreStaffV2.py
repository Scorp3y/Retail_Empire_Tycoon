"""Staff uniforms on the CURRENT Kenney-based shopper meshes, faces, shading and skeleton.

Each source is saved separately. The superseded handmade V1 sources are preserved.
"""
import argparse
import copy
import hashlib
import json
import math
import sys
from collections import Counter
from pathlib import Path

import bpy
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).parent))
import BuildShopperFleet as fleet
import BuildShopperStyleStudy as study
import BuildShoppers as primitives
import BuildStoreStaff as previous

# Keep the saved role ordering: cashier, security, stocker, cleaner, driver.
STAFF = (
    ('Cashier', 2, 'fringe bun', 3, 'peach', 'auburn'),
    ('Guard', 1, 'cap', 11, 'light', 'dark'),
    ('Stocker', 1, 'quiff', 4, 'tan', 'brown'),
    ('Cleaner', 2, 'high bun', 2, 'light', 'silver'),
    ('Driver', 1, 'moustache', 11, 'light', 'brown'),
)
CELLS = dict(ivory=(8,0), cream=(0,1), forest=(3,1), apron_blue=(13,1),
    charcoal=(1,0), black=(0,0), metal=(7,0), mustard=(4,1), yellow=(5,1),
    teal=(3,1), mint=(2,1), tan=(1,1), gray=(2,0), dark_brown=(12,0),
    work_blue=(11,1), work_ochre=(4,1))

class StaffParts(primitives.CharacterMesh):
    def panel(self, name, center, size, color, bone, radius):
        """Round the visible XZ outline independently of the thin panel's depth."""
        width,depth,height=size;radius=min(radius,width*.45,height*.45)
        outline=[]
        for x,z,start in ((width/2-radius,height/2-radius,0),(-width/2+radius,height/2-radius,90),
                          (-width/2+radius,-height/2+radius,180),(width/2-radius,-height/2+radius,270)):
            for step in range(4):
                angle=math.radians(start+step*30);outline.append((x+math.cos(angle)*radius,z+math.sin(angle)*radius))
        offset=len(self.vertices);count=len(outline);cx,cy,cz=center
        for y in (-depth/2,depth/2):
            for x,z in outline:self.vertices.append((cx+x,cy+y,cz+z));self.bones.append(bone)
        self.faces.extend([tuple(offset+i for i in range(count)),tuple(offset+count+i for i in reversed(range(count)))])
        self.colors.extend([color,color])
        for i in range(count):
            j=(i+1)%count;self.faces.append((offset+i,offset+count+i,offset+count+j,offset+j));self.colors.append(color)

    def finish(self, material):
        # Existing shoppers use sixteen columns and FOUR gradient rows, unlike the rejected V1 atlas.
        mesh=bpy.data.meshes.new('Staff work accessories');mesh.from_pydata(self.vertices,[],self.faces);mesh.materials.append(material);mesh.update()
        uv=mesh.uv_layers.new(name='PaletteUV')
        for face,color in zip(mesh.polygons,self.colors):
            x,y=CELLS[color]
            for loop in face.loop_indices:uv.data[loop].uv=((x+.5)/16,(y+.65)/4)
        obj=bpy.data.objects.new('WorkTools',mesh);bpy.context.collection.objects.link(obj)
        for name in set(self.bones):obj.vertex_groups.new(name=name).add([i for i,key in enumerate(self.bones) if key==name],1,'REPLACE')
        return obj

def design_for(role,template,head,top,skin,hair,index):
    design=copy.deepcopy(study.STUDIES[template-1]);model,parts=fleet.HEADS[head]
    design.update(number=index+1,template_number=template,id=role,name=role,top=top,bottom=4,shoe=5,
        skin=skin,hair=hair,skin_cell=fleet.SKIN[skin],hair_cell=fleet.HAIR[hair],
        head_design={'model':'character-'+model,'parts':parts})
    return design

def append_parts(body, addition):
    # Join preserves the shopper's authored split normals/UV seams, weights and unmodified face geometry.
    bpy.ops.object.select_all(action='DESELECT');body.select_set(True);addition.select_set(True)
    bpy.context.view_layer.objects.active=body;bpy.ops.object.join();body.name='Body'

def cap(project,body,material):
    source=project/'Assets/Models/mini characters/Models/OBJ format/character-male-c.obj'
    positions,uv,normals,faces=study.read_obj(source);keys=study.component_keys(positions,faces)
    vertices=[];polygons=[];coordinates=[];normal_values=[]
    for (group,corners),part in zip(faces,keys):
        if group!='head-mesh' or part!=514:continue
        indices=[]
        for vi,ti,ni in reversed(corners):
            indices.append(len(vertices));vertices.append(study.converted(positions[vi],True))
            coordinates.append((6.5/16,.25+uv[ti][1]%.25))
            n=normals[ni];normal_values.append(Vector((n.x,n.z,n.y)).normalized())
        polygons.append(indices)
    mesh=bpy.data.meshes.new('Original mini-character cap');mesh.from_pydata(vertices,[],polygons);mesh.materials.append(material);mesh.update()
    layer=mesh.uv_layers.new(name='PaletteUV')
    for item,value in zip(layer.data,coordinates):item.uv=value
    for face in mesh.polygons:face.use_smooth=True
    mesh.normals_split_custom_set(normal_values)
    obj=bpy.data.objects.new('Original cap',mesh);bpy.context.collection.objects.link(obj)
    obj.vertex_groups.new(name='Head').add(list(range(len(vertices))),1,'REPLACE');append_parts(body,obj)

def uniform(body,role,material,rounded=False):
    group=body.vertex_groups.get('Spine')
    points=[v.co for v in body.data.vertices if any(g.group==group.index for g in v.groups)]
    front=max(v.y for v in points)+.012
    detail=StaffParts(None)
    def panel(name,center,size,color,bone,radius):
        if rounded:detail.panel(name,center,size,color,bone,radius)
        else:detail.box(name,center,size,color,bone,min(.004,radius))
    if role in ('Cashier','Cleaner'):
        color='forest' if role=='Cashier' else 'apron_blue'
        panel('Apron bib',(0,front,.39),(.29,.018,.18),color,'Spine',.022)
        panel('Apron hem',(0,front,.278),(.37,.023,.10),color,'Hips',.021)
        for x in (-.115,.115):panel('Apron strap',(x,front,.477),(.031,.018,.085),color,'Spine',.012)
        panel('Apron pocket',(0,front+.011,.35),(.13,.01,.053),color,'Spine',.009)
    if role=='Guard':
        detail.polygon([(-.12,front,.45),(-.06,front,.45),(-.06,front,.413),(-.09,front,.393),(-.12,front,.413)],'cream','Spine')
        detail.box('Belt',(0,-.04,.285),(.39,.29,.027),'black','Hips',.004)
        detail.box('Belt buckle',(0,front,.285),(.045,.012,.029),'metal','Hips',.002)
    if role in ('Driver','Stocker'):
        pocket_color=('work_blue' if role=='Driver' else 'work_ochre') if rounded else ('apron_blue' if role=='Driver' else 'tan')
        for x in (-.10,.10):panel('Work shirt pocket',(x,front,.39),(.075,.012,.055),pocket_color,'Spine',.012)
    panel('Employee badge',(.09,front+.012,.461),(.06,.012,.023),'mustard' if role=='Cashier' else 'cream','Spine',.006)
    append_parts(body,detail.finish(material))

def soften_arms(body,design,material):
    """Replace only disconnected arm shells with closed rounded pieces on the original bone pivots."""
    names={g.index:g.name for g in body.vertex_groups};mesh=body.data
    arm_groups={i for i,name in names.items() if name.startswith(('UpperArm.','Forearm.'))}
    arm_vertices={v.index for v in mesh.vertices if any(g.group in arm_groups and g.weight>.5 for g in v.groups)}
    if not arm_vertices:raise ValueError('Current NPC has no weighted arm geometry')
    arm=StaffParts(None);CELLS['sleeve']=(design['top'],1);CELLS['bare_arm']=tuple(design['skin_cell'])
    for sign,side in ((1,'L'),(-1,'R')):
        group=body.vertex_groups['Forearm.'+side].index
        hands=[v for v in mesh.vertices if any(g.group==group and g.weight>.5 for g in v.groups)]
        lower=Vector(tuple(min(v.co[i] for v in hands) for i in range(3)));upper=Vector(tuple(max(v.co[i] for v in hands) for i in range(3)))
        cells=Counter()
        hand_indices={v.index for v in hands}
        for face in mesh.polygons:
            if all(i in hand_indices for i in face.vertices):
                for loop in face.loop_indices:
                    uv=mesh.uv_layers.active.data[loop].uv;cells[(min(15,int(uv.x*16)),min(3,int(uv.y*4)))]+=1
        CELLS['hand_'+side]=cells.most_common(1)[0][0]
        shoulder=Vector((sign*.205,-.025,.455));elbow=Vector((sign*.355,0,.315));wrist=Vector((sign*.432,.065,.285))
        def beam(a,b,width,depth,color,bone):
            arm.box('Rounded arm segment',(a+b)*.5,(width,depth,(b-a).length+.045),color,bone,min(width,depth)*.2,(b-a).to_track_quat('Z','Y'),segments=3)
        if design['template_number']==2:
            cuff=shoulder.lerp(elbow,.52)
            beam(shoulder,cuff,.18,.195,'sleeve','UpperArm.'+side)
            beam(cuff,elbow,.145,.155,'bare_arm','UpperArm.'+side)
            forearm_color='bare_arm'
        else:
            beam(shoulder,elbow,.18,.20,'sleeve','UpperArm.'+side);forearm_color='sleeve'
        arm.box('Soft shoulder',shoulder,(.175,.19,.16),'sleeve','UpperArm.'+side,.034,segments=3)
        arm.ball(elbow,(.079,.083,.079),forearm_color,'Forearm.'+side,2)
        beam(elbow,wrist,.145,.16,forearm_color,'Forearm.'+side)
        arm.box('Rounded mitten',(lower+upper)*.5,tuple(upper-lower),'hand_'+side,'Forearm.'+side,.031,segments=3)

    # Keep untouched face/hair/torso UVs and split normals, including their original bone weights.
    vertices=[];faces=[];uvs=[];normals=[];weights=[]
    for face in mesh.polygons:
        if any(i in arm_vertices for i in face.vertices):continue
        indices=[]
        for loop in face.loop_indices:
            vertex=mesh.vertices[mesh.loops[loop].vertex_index];indices.append(len(vertices));vertices.append(tuple(vertex.co))
            uvs.append(tuple(mesh.uv_layers.active.data[loop].uv));normals.append(tuple(mesh.corner_normals[loop].vector))
            weights.append({names[g.group]:g.weight for g in vertex.groups})
        faces.append(indices)
    replacement=bpy.data.meshes.new('Body / original head and torso');replacement.from_pydata(vertices,[],faces);replacement.materials.append(material);replacement.update()
    layer=replacement.uv_layers.new(name='PaletteUV')
    for item,value in zip(layer.data,uvs):item.uv=value
    for face in replacement.polygons:face.use_smooth=True
    replacement.normals_split_custom_set(normals);body.data=replacement
    # Blender may clear the object's group definitions when assigning a new mesh datablock.
    body.vertex_groups.clear()
    for name in names.values():
        group=body.vertex_groups.new(name=name)
        for index,entry in enumerate(weights):
            if group.name in entry:group.add([index],entry[group.name],'REPLACE')
    rounded=arm.finish(material)
    for face in rounded.data.polygons:face.use_smooth=True
    bpy.context.view_layer.objects.active=rounded;rounded.select_set(True)
    modifier=rounded.modifiers.new('Weighted rounded arm highlights','WEIGHTED_NORMAL');modifier.keep_sharp=True;modifier.weight=40
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    append_parts(body,rounded)
    if not {'Head','Spine','Hips','UpperArm.L','UpperArm.R','Forearm.L','Forearm.R'}.issubset({g.name for g in body.vertex_groups}):
        raise ValueError('Rounded body lost a required skeleton group')
    if any(abs(sum(g.weight for g in v.groups)-1)>.001 for v in body.data.vertices):
        raise ValueError('Rounded body has unweighted or multiply weighted vertices')
    return len(arm_vertices)

def skin_hands(body,design):
    """Recolour the original hand surfaces only; sleeve UVs, normals, geometry and weights stay intact."""
    groups={g.index for g in body.vertex_groups if g.name.startswith('Forearm.')}
    hands={v.index for v in body.data.vertices if any(g.group in groups and g.weight>.5 for g in v.groups)}
    count=0;column,row=design['skin_cell'];layer=body.data.uv_layers.active.data
    for face in body.data.polygons:
        if not any(index in hands for index in face.vertices):continue
        # A wrist triangle can contain both upper-arm and hand weights. Recolour its whole
        # surface: mixing atlas columns at its corners would interpolate rainbow stripes.
        for loop in face.loop_indices:
            layer[loop].uv=((column+.5)/16,(row+.5)/4);count+=1
    if not count:raise ValueError('No original hand UVs were recoloured')
    return count

def build(project, only_role=None, soft_edges=False, natural_hands=False):
    revision='StaffV4' if natural_hands else 'StaffV3' if soft_edges else 'StaffV2'
    work='StaffHands' if natural_hands else 'StaffSoftening' if soft_edges else 'StaffStyle'
    folder=project/'ArtSource'/revision;exports=project/'Library'/work/'Exports';previews=project/'Library'/work/'Previews'
    exports.mkdir(parents=True,exist_ok=True);previews.mkdir(parents=True,exist_ok=True)
    fleet.configure_palette();reports=[]
    for index,spec in enumerate(STAFF):
        if only_role and spec[0]!=only_role:continue
        role=spec[0];design=design_for(*spec,index)
        source=folder/role/(role+'.blend')
        if source.exists():raise FileExistsError('Preserve existing artist source: '+str(source))
        fleet.verify_head_parts(project,design);bpy.ops.wm.read_factory_settings(use_empty=True)
        material=study.palette(exports)
        base=project/'Assets/Models/mini characters/Models/OBJ format'/(design['model']+'.obj')
        body=study.body_mesh(base,design,material)
        original_vertices=len(body.data.vertices)
        # Exact current customer geometry/UVs, before adding profession accessories.
        original_hash=hashlib.sha256(b''.join(float(c).hex().encode()+b';' for v in body.data.vertices for c in v.co)).hexdigest()
        recolored=skin_hands(body,design) if natural_hands and role in ('Guard','Stocker','Driver') else 0
        recolored_geometry=hashlib.sha256(b''.join(float(c).hex().encode()+b';' for v in body.data.vertices for c in v.co)).hexdigest()
        if natural_hands and recolored_geometry!=original_hash:raise ValueError('Hand recolouring changed the original geometry')
        arm_vertices=soften_arms(body,design,material) if soft_edges else 0
        uniform(body,role,material,soft_edges or natural_hands)
        if role=='Driver':cap(project,body,material)
        rig,marker=study.rig(body);study.animations(rig);previous.work_animation(rig,role)
        parts=StaffParts(None);previous.tools(parts,role)
        # Current shoppers have the pack's wide, outward relaxed hands, not V1's narrow arms.
        for i,(point,bone) in enumerate(zip(parts.vertices,parts.bones)):
            if bone.startswith('Forearm.'):
                point=Vector(point);point.x+=.18 if bone.endswith('.L') else -.18
                if role!='Cleaner':point.z-=.025
                parts.vertices[i]=tuple(point)
        props=parts.finish(material);props.parent=rig;props.modifiers.new('Shared mini-character rig','ARMATURE').object=rig
        rig['source_style']='Same Kenney mini-character geometry and palette as the live Shoppers roster'
        rig['role']=role;rig['design']=json.dumps(design)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in (rig,body,props,marker):obj.select_set(True)
        bpy.context.view_layer.objects.active=rig
        fbx=exports/(role+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
        study.studio(bpy.context.scene,previews/(role+'.png'));bpy.context.scene.cycles.samples=16
        source.parent.mkdir(parents=True,exist_ok=True);bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source))
        rig.animation_data.action=bpy.data.actions['Browse'];bpy.context.scene.frame_set(1);bpy.ops.render.render(write_still=True)
        reports.append(dict(role=role,source=str(source.relative_to(project)),head=design['head_design']['model'],template=design['model'],base_vertices=original_vertices,base_sha256=original_hash,
            source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),fbx_sha256=hashlib.sha256(fbx.read_bytes()).hexdigest(),bones=len(rig.data.bones),softened_arm_vertices=arm_vertices,
            arm_bevel_segments=3 if soft_edges else 0,recolored_hand_loops=recolored,hands_skin_cell=design['skin_cell'],original_arm_geometry_preserved=natural_hands,
            triangles=sum(len(face.vertices)-2 for mesh in (body,props) for face in mesh.data.polygons)))
    folder.mkdir(parents=True,exist_ok=True)
    if only_role and (folder/'Manifest.json').exists():
        reports=[entry for entry in json.loads((folder/'Manifest.json').read_text(encoding='utf-8')) if entry['role']!=only_role]+reports
        reports.sort(key=lambda entry:next(i for i,spec in enumerate(STAFF) if spec[0]==entry['role']))
    (folder/'Manifest.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--project',type=Path,required=True);parser.add_argument('--only-role',choices=[spec[0] for spec in STAFF])
    variant=parser.add_mutually_exclusive_group();variant.add_argument('--soft-edges',action='store_true');variant.add_argument('--skin-hands',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);build(args.project,args.only_role,args.soft_edges,args.skin_hands)
