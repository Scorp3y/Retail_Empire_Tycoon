"""Build individually editable, rigid-weighted low-poly shoppers in a separate Blender process.

Outputs are sources under ArtSource, FBX exports and QA renders under Library. Unity
installation is a separate guarded step. Never opens or overwrites the user's Blender scene.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector

sys.path.insert(0,str(Path(__file__).parent))
from ShopperDesigns import COLORS, BY_NUMBER

PALETTE = list(COLORS)
PALETTE_COLUMNS = 16
PALETTE_SIZE = PALETTE_COLUMNS * 16
assert len(PALETTE) <= PALETTE_COLUMNS ** 2

def material_and_atlas(folder):
    folder.mkdir(parents=True,exist_ok=True)
    image=bpy.data.images.new('ShopperPalette',width=PALETTE_SIZE,height=PALETTE_SIZE,alpha=True)
    pixels=[]
    for y in range(PALETTE_SIZE):
        for x in range(PALETTE_SIZE):
            slot=(y//16)*PALETTE_COLUMNS+x//16
            value=COLORS[PALETTE[slot]] if slot<len(PALETTE) else COLORS['ivory']
            pixels.extend([int(value[i:i+2],16)/255 for i in (0,2,4)]+[1])
    image.pixels.foreach_set(pixels)
    image.filepath_raw=str(folder/'ShopperPalette.png');image.file_format='PNG';image.save();image.pack()
    material=bpy.data.materials.new('Shoppers / shared palette');material.use_nodes=True
    shader=material.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.82
    texture=material.node_tree.nodes.new('ShaderNodeTexImage');texture.image=image;texture.interpolation='Closest'
    material.node_tree.links.new(texture.outputs['Color'],shader.inputs['Base Color'])
    return material

class CharacterMesh:
    def __init__(self,design):
        self.design=design;self.vertices=[];self.faces=[];self.colors=[];self.bones=[]
    def add_bmesh(self,bm,color,bone,transform):
        bm.verts.ensure_lookup_table();bm.verts.index_update()
        offset=len(self.vertices)
        for vertex in bm.verts:
            self.vertices.append(tuple(transform@vertex.co));self.bones.append(bone)
        for face in bm.faces:
            self.faces.append(tuple(offset+v.index for v in face.verts));self.colors.append(color)
        bm.free()
    def box(self,name,center,size,color,bone='Spine',bevel=.015,rotation=None,segments=1,bottom_scale=1):
        bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
        bmesh.ops.scale(bm,vec=Vector(size),verts=list(bm.verts))
        for vertex in bm.verts:
            if vertex.co.z < 0: vertex.co.x *= bottom_scale
        bm.normal_update()
        if bevel>0:
            # Explicit convex fillet: the default BMesh profile can otherwise produce recessed corner strips.
            bmesh.ops.bevel(bm,geom=list(bm.edges),offset=min(bevel,min(size)*.3),segments=segments,profile=.5,affect='EDGES')
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        transform=Matrix.Translation(Vector(center))
        if rotation is not None:transform=transform@rotation.to_matrix().to_4x4()
        self.add_bmesh(bm,color,bone,transform)
    def ball(self,center,scale,color,bone='Head',subdivisions=1):
        bm=bmesh.new();bmesh.ops.create_icosphere(bm,subdivisions=subdivisions,radius=1)
        self.add_bmesh(bm,color,bone,Matrix.Translation(Vector(center))@Matrix.Diagonal(Vector((*scale,1))))
    def beam(self,start,end,width,depth,color,bone):
        a,b=Vector(start),Vector(end)
        self.box('Beam',(a+b)*.5,(width,depth,(b-a).length),color,bone,.018,(b-a).to_track_quat('Z','Y'),segments=2)
    def polygon(self,points,color,bone):
        # These are front-facing facial/clothing details. Mirroring a lapel must not turn it invisible in Unity's back-face culling.
        points=list(points)
        if (Vector(points[1])-Vector(points[0])).cross(Vector(points[2])-Vector(points[0])).y < 0:
            points.reverse()
        offset=len(self.vertices)
        self.vertices.extend(points);self.bones.extend([bone]*len(points))
        self.faces.append(tuple(offset+i for i in range(len(points))));self.colors.append(color)
    def disc(self,x,y,z,radius,color='black',sides=12,aspect=1):
        self.polygon([(x+math.cos(i*math.tau/sides)*radius,y,z+math.sin(i*math.tau/sides)*radius*aspect) for i in reversed(range(sides))],color,'Head')
    def face_line(self,a,b,width,color='black',bone='Head',y=.221):
        x,z=a;xx,zz=b;length=math.hypot(xx-x,zz-z);nx=-(zz-z)/length*width*.5;nz=(xx-x)/length*width*.5
        self.polygon([(x+nx,y,z+nz),(xx+nx,y,zz+nz),(xx-nx,y,zz-nz),(x-nx,y,z-nz)],color,bone)
    def face(self):
        d=self.design
        self.box('Head',(0,0,.755),(.50,.43,.445),d.skin,'Head',.086,segments=3)
        for x in (-.254,.254):self.box('Ear',(x,-.006,.735),(.065,.10,.095),d.skin,'Head',.027,segments=2)
        for x in (-.098,.098):
            self.disc(x,.220,.771,.021,aspect=1.10)
            self.disc(x-.005,.222,.779,.0045,'ivory',sides=8)
        for x in (-.151,.151):self.disc(x,.2157,.723,.030,d.skin+'_cheek',aspect=.42)
        self.box('Nose',(0,.224,.730),(.028,.027,.031),d.skin,'Head',.009,segments=2)
        for i in range(10):
            a=math.pi+i*math.pi/10;b=math.pi+(i+1)*math.pi/10
            self.face_line((math.cos(a)*.052,.698+math.sin(a)*.026),(math.cos(b)*.052,.698+math.sin(b)*.026),.009,y=.222)
        if d.beard=='short':
            for x in (-.135,.135):self.box('Beard side',(x,.214,.676),(.09,.018,.085),d.hair,'Head',.012)
            self.box('Beard chin',(0,.213,.641),(.20,.018,.046),d.hair,'Head',.012)
        elif d.beard=='mustache':
            for x in (-.042,.042):self.box('Mustache',(x,.231,.709),(.09,.026,.027),d.hair,'Head',.008)
        if d.glasses:
            for center in (-.105,.105):
                sides=16 if d.glasses=='round' else 4
                points=[(center+math.cos(math.tau*i/sides+math.pi/4)*.064,.771+math.sin(math.tau*i/sides+math.pi/4)*.054) for i in range(sides)]
                for i,a in enumerate(points):self.face_line(a,points[(i+1)%sides],.012,y=.228)
            self.face_line((-.045,.776),(.045,.776),.012,y=.228)
    def hair(self):
        d=self.design;style=d.hairstyle
        if style=='shaved':return
        if style=='balding':
            for x in (-.235,.235):self.box('Side hair',(x,-.025,.86),(.055,.34,.16),d.hair,'Head',.016,segments=2)
            self.hat()
            return
        self.box('Hair cap',(0,-.012,.952),(.518,.448,.134),d.hair,'Head',.044,segments=2)
        if style in ('bob','long','wavy','ponytail','bun'):
            bottom=.586 if style=='long' else .665
            for x in (-.242,.242):self.box('Side hair',(x,-.012,(.965+bottom)/2),(.09,.36,.965-bottom),d.hair,'Head',.030,segments=2)
            self.box('Back hair',(0,-.204,(.97+bottom)/2),(.465,.075,.97-bottom),d.hair,'Head',.028,segments=2)
        if style in ('quiff','swept','messy','sidepart'):
            # A single swept volume avoids coplanar, overlapping fringe boxes and saw-tooth hairlines.
            tilt=.11 if style in ('quiff','messy') else -.055
            quat=Matrix.Rotation(tilt,4,'Y').to_quaternion()
            self.box('Swept forelock',(-.015,.074,.979),(.465,.285,.126),d.hair,'Head',.037,quat,segments=2)
            if style=='messy':self.box('Loose tuft',(.12,-.02,1.016),(.19,.20,.07),d.hair,'Head',.021,segments=2)
        elif style=='bob':
            self.box('Fringe',(-.018,.185,.913),(.45,.085,.105),d.hair,'Head',.027,segments=2)
        elif style=='long':
            self.box('Side fringe',(-.08,.197,.915),(.31,.07,.12),d.hair,'Head',.025,segments=2)
        elif style=='wavy':
            for i in range(4):self.ball((-.18+i*.12,.16,.954),(.105,.10,.105),d.hair,subdivisions=2)
        elif style in ('curly','afro'):
            for row in range(2):
                for i in range(3):self.ball((-.17+i*.17,-.09+row*.19,.987+(i%2)*.015),(.145,.145,.124 if style=='curly' else .15),d.hair,subdivisions=2)
            if style=='afro':
                for x in (-.245,.245):self.ball((x,.005,.9),(.11,.16,.14),d.hair,subdivisions=2)
        if style=='ponytail':
            self.ball((0,-.29,.91),(.115,.14,.13),d.hair,subdivisions=2)
            self.box('Ponytail',(0,-.315,.77),(.135,.14,.23),d.hair,'Head',.035,segments=2)
            self.box('Hair tie',(0,-.266,.87),(.15,.045,.045),d.top,'Head',.01)
        if style=='bun':self.ball((-.035,-.14,1.045),(.13,.14,.11),d.hair,subdivisions=2)
        self.hat()
    def hat(self):
        d=self.design
        if d.hat:
            kind,color=d.hat.split(':')
            if kind=='cap':
                self.box('Cap',(0,-.002,1.007),(.56,.48,.17),color,'Head',.055,segments=2)
                self.box('Cap brim',(0,.275,.951),(.43,.205,.035),color,'Head',.012,segments=2)
            else:
                self.box('Beanie',(0,0,1.017),(.55,.46,.20),color,'Head',.065,segments=2)
                self.box('Beanie cuff',(0,.004,.947),(.57,.475,.065),color,'Head',.017)
    def outfit(self):
        d=self.design;kind=d.outfit
        skirt=kind in ('skirt','skirt_cardigan','dress')
        self.box('Pelvis',(0,-.01,.283),(.295,.19,.10),d.top if kind=='dress' else d.bottom,'Hips',.02)
        self.box('Torso',(0,-.012,.405),(.335,.213,.25),d.top,'Spine',.032,segments=2,bottom_scale=.92)
        self.box('Neck',(0,0,.535),(.13,.13,.08),d.skin,'Head',.018)
        for sign,side in ((1,'L'),(-1,'R')):
            shoulder=Vector((sign*.185,0,.484));elbow=Vector((sign*.26,.002,.35));hand=Vector((sign*.282,.012,.285))
            sleeve_color=d.inner if kind in ('vest','puffer','overalls') else d.top
            short=kind in ('shorts','striped_shorts','skirt','polo','shirt')
            sleeve_end=shoulder.lerp(elbow,.62) if short else elbow
            self.beam(shoulder,sleeve_end,.115,.135,sleeve_color,'UpperArm.'+side)
            if short:self.beam(sleeve_end,elbow,.09,.10,d.skin,'UpperArm.'+side)
            self.beam(elbow,hand,.092,.105,d.skin if short or kind=='dress' else sleeve_color,'Forearm.'+side)
            self.box('Hand',hand,(.108,.115,.11),d.skin,'Forearm.'+side,.032,segments=2)
            trim=d.top+'_trim' if d.top+'_trim' in COLORS else d.top
            if not short and kind not in ('vest','overalls','puffer'):
                self.beam(elbow.lerp(hand,.68),elbow.lerp(hand,.94),.100,.112,trim,'Forearm.'+side)
            thigh=(sign*.088,0,.235);shin=(sign*.088,0,.133)
            leg_color=d.skin if skirt or kind in ('shorts','striped_shorts') else d.bottom
            self.box('Thigh',thigh,(.14,.16,.135),d.bottom if not skirt else d.skin,'Thigh.'+side,.015)
            self.box('Shin',shin,(.13,.15,.115),leg_color,'Shin.'+side,.015)
            boot=skirt and d.shoes=='charcoal' and d.number==29
            if boot:self.box('Boot cuff',(sign*.088,0,.13),(.14,.165,.11),d.shoes,'Shin.'+side,.015)
            self.box('Shoe',(sign*.093,.035,.049),(.165,.235,.096),d.shoes,'Foot.'+side,.029,segments=2)
            self.box('Sole',(sign*.093,.035,.015),(.17,.24,.03),'sole' if d.shoes in ('ivory','cream') else 'charcoal','Foot.'+side,.008,segments=2)
            if d.shoes in ('ivory','cream'):
                self.box('Shoe tongue',(sign*.093,.027,.097),(.086,.07,.012),'sole','Foot.'+side,.004,segments=2)
        if skirt:
            width=.41 if kind=='dress' else .39
            self.box('Skirt',(0,-.01,.245),(width,.245,.18),d.top if kind=='dress' else d.bottom,'Hips',.026)
        if kind in ('jacket','cardigan','skirt_cardigan','suit','coat'):
            self.box('Shirt',(0,.101,.414),(.15,.012,.22),d.inner,'Spine',.002)
            for sign in (-1,1):
                self.polygon([(sign*.065,.112,.515),(sign*.13,.112,.515),(sign*.078,.118,.407),(sign*.027,.118,.456)],d.top,'Spine')
                self.box('Pocket',(sign*.111,.105,.361),(.068,.017,.047),d.top,'Spine',.004)
            if kind in ('coat','cardigan','skirt_cardigan'):
                self.box('Button',(0.085,.119,.38),(.015,.012,.015),'metal','Spine',.004)
            if kind=='coat':
                for sign in (-1,1):self.box('Coat hem',(sign*.105,-.015,.292),(.15,.22,.155),d.top,'Hips',.014)
            if kind=='suit':
                self.polygon([(-.027,.12,.497),(0,.124,.515),(.027,.12,.497),(0,.123,.474)],'red' if d.number==1 else 'charcoal','Spine')
                self.polygon([(0,.123,.475),(-.024,.123,.424),(0,.123,.405),(.024,.123,.424)],'red' if d.number==1 else 'charcoal','Spine')
        if kind in ('shirt','polo','collared','vest','puffer','overalls'):
            for sign in (-1,1):self.polygon([(sign*.012,.118,.509),(sign*.09,.116,.517),(sign*.07,.12,.468)],d.inner,'Spine')
        if kind=='shirt':
            self.box('Shirt placket',(0,.104,.411),(.012,.01,.18),d.top,'Spine',0)
            for z in (.38,.43,.48):self.box('Shirt button',(0,.114,z),(.012,.01,.012),'metal','Spine',.002)
        if kind=='hoodie':
            self.box('Hood',(0,-.10,.499),(.27,.17,.115),d.top,'Spine',.025)
            for x in (-.048,.048):self.box('Drawstring',(x,.11,.461),(.009,.012,.072),'ivory','Spine',.002)
            self.box('Kangaroo pocket',(0,.107,.342),(.22,.018,.07),d.top,'Spine',.01)
        if kind in ('stripe','striped_shorts'):
            self.box('Broad stripe',(0,.101,.42),(.325,.016,.065),d.inner,'Spine',.004)
            if kind=='striped_shorts':
                for z in (.355,.49):self.box('Stripe',(0,.104,z),(.31,.013,.027),'ivory','Spine',.003)
        if kind=='puffer':
            for z in (.34,.395,.45):self.box('Puffer panel',(0,.103,z),(.325,.025,.042),d.top,'Spine',.012)
        if kind=='overalls':
            self.box('Overall bib',(0,.112,.41),(.23,.021,.16),d.top,'Spine',.01)
            for x in (-.093,.093):
                self.box('Overall strap',(x,.112,.49),(.04,.022,.095),d.top,'Spine',.004)
                self.box('Overall button',(x,.128,.451),(.018,.012,.018),'metal','Spine',.005)
    def finish(self,material):
        mesh=bpy.data.meshes.new('Body');mesh.from_pydata(self.vertices,[],self.faces);mesh.materials.append(material);mesh.update()
        uv=mesh.uv_layers.new(name='PaletteUV')
        for face,color in zip(mesh.polygons,self.colors):
            slot=PALETTE.index(color);coord=((slot%PALETTE_COLUMNS+.5)/PALETTE_COLUMNS,(slot//PALETTE_COLUMNS+.5)/PALETTE_COLUMNS)
            for loop in face.loop_indices:uv.data[loop].uv=coord
        body=bpy.data.objects.new('Body',mesh);bpy.context.collection.objects.link(body)
        for bone in sorted(set(self.bones)):
            group=body.vertex_groups.new(name=bone);group.add([i for i,key in enumerate(self.bones) if key==bone],1,'REPLACE')
        return body

def make_rig(body):
    rig=bpy.data.objects.new('Rig',bpy.data.armatures.new('ShopperSkeleton'));bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    specs=[('Root',(0,0,0),(0,0,.10),None),('Hips',(0,0,.28),(0,0,.36),'Root'),
           ('Spine',(0,0,.36),(0,0,.52),'Hips'),('Head',(0,0,.53),(0,0,.94),'Spine')]
    for sign,side in ((1,'L'),(-1,'R')):
        specs.extend([(f'UpperArm.{side}',(sign*.185,0,.484),(sign*.26,.002,.35),'Spine'),
                      (f'Forearm.{side}',(sign*.26,.002,.35),(sign*.282,.012,.25),f'UpperArm.{side}'),
                      (f'Thigh.{side}',(sign*.088,0,.28),(sign*.088,0,.17),'Hips'),
                      (f'Shin.{side}',(sign*.088,0,.17),(sign*.093,0,.07),f'Thigh.{side}'),
                      (f'Foot.{side}',(sign*.093,0,.07),(sign*.093,.14,.045),f'Shin.{side}')])
    for name,head,tail,parent in specs:
        bone=rig.data.edit_bones.new(name);bone.head=head;bone.tail=tail
        if parent:bone.parent=rig.data.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT');body.parent=rig
    modifier=body.modifiers.new('Rigid weighted skeleton','ARMATURE');modifier.object=rig
    rig.show_in_front=True
    marker=bpy.data.objects.new('FacingMarker',None);bpy.context.collection.objects.link(marker);marker.parent=rig;marker.location=(0,.35,.75)
    return rig,marker

def animations(rig):
    scene=bpy.context.scene;scene.render.fps=30;rig.animation_data_create()
    for name,duration in (('Idle',60),('Walk',24),('Browse',90)):
        action=bpy.data.actions.new(name);rig.animation_data.action=action
        for frame in sorted(set(range(0,duration+1,3))|{duration}):
            p=frame/duration*math.tau;scene.frame_set(frame+1)
            for bone in rig.pose.bones:bone.rotation_mode='XYZ';bone.rotation_euler=(0,0,0);bone.location=(0,0,0)
            bones=rig.pose.bones
            if name=='Idle':
                bones['Spine'].rotation_euler.x=.015*math.sin(p)
                bones['Head'].rotation_euler.y=.045*math.sin(p)
                bones['Head'].rotation_euler.x=.015*math.cos(p)
                for sign,side in ((1,'L'),(-1,'R')):bones['UpperArm.'+side].rotation_euler.x=sign*.025*math.sin(p)
            elif name=='Walk':
                bones['Hips'].location.y=.012*(1-math.cos(p*2))*.5
                bones['Spine'].rotation_euler.y=.035*math.sin(p)
                for sign,side in ((1,'L'),(-1,'R')):
                    stride=sign*.64*math.sin(p)
                    knee=max(0,sign*math.sin(p))*.36
                    bones['Thigh.'+side].rotation_euler.x=stride
                    bones['Shin.'+side].rotation_euler.x=-knee
                    bones['Foot.'+side].rotation_euler.x=-stride*.6+knee*.6
                    bones['UpperArm.'+side].rotation_euler.x=-stride*.65
                    bones['Forearm.'+side].rotation_euler.x=-.10-.06*math.cos(p)
            else:
                bones['Head'].rotation_euler.y=.12*math.sin(p)
                bones['Head'].rotation_euler.x=.04+.025*math.cos(p)
                bones['Spine'].rotation_euler.x=-.035
                bones['UpperArm.R'].rotation_euler.x=.65+.18*math.sin(p)
                bones['Forearm.R'].rotation_euler.x=.6-.12*math.sin(p)
            for bone in bones:
                bone.keyframe_insert(data_path='rotation_euler',frame=frame+1,group=bone.name)
                bone.keyframe_insert(data_path='location',frame=frame+1,group=bone.name)
        action.use_fake_user=True
    rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_start=1;scene.frame_end=61;scene.frame_set(1)

def studio(scene,destination):
    scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.render.resolution_x=700;scene.render.resolution_y=700;scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('Soft studio');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.68,.71,.69,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.55
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.004));ground=bpy.context.object;ground.name='STUDIO / ground'
    material=bpy.data.materials.new('STUDIO / cream');material.diffuse_color=(.63,.63,.58,1);ground.data.materials.append(material)
    for name,position,energy,size in [('Key',(-3,4,6),380,4),('Fill',(4,1,4),180,4)]:
        light=bpy.data.lights.new(name,'AREA');light.energy=energy;light.shape='DISK';light.size=size
        obj=bpy.data.objects.new('STUDIO / '+name,light);scene.collection.objects.link(obj);obj.location=position;obj.rotation_euler=(Vector((0,0,.5))-obj.location).to_track_quat('-Z','Y').to_euler()
    camera=bpy.data.objects.new('STUDIO / camera',bpy.data.cameras.new('Studio camera'));scene.collection.objects.link(camera)
    camera.location=(1.7,3.7,1.9);camera.rotation_euler=(Vector((0,0,.54))-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO';camera.data.ortho_scale=1.45;scene.camera=camera
    scene.view_settings.view_transform='Standard';scene.render.image_settings.file_format='PNG';scene.render.filepath=str(destination)

def build(project,number,render,replace):
    design=BY_NUMBER[number];folder=project/'ArtSource/Shoppers'/design.identity;destination=folder/(design.identity+'.blend')
    if destination.exists() and not replace:raise FileExistsError('Source exists; preserve artist edits: '+str(destination))
    bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.name=design.identity
    scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    material=material_and_atlas(project/'Library/ShopperBuild/Exports')
    builder=CharacterMesh(design);builder.outfit();builder.face();builder.hair();body=builder.finish(material)
    rig,marker=make_rig(body);animations(rig)
    rig['design']=json.dumps(design.data());rig['reference']='../Concepts/ShopperLineup-v1.png'
    folder.mkdir(parents=True,exist_ok=True)
    export=project/'Library/ShopperBuild/Exports'/(design.identity+'.fbx')
    bpy.ops.object.select_all(action='DESELECT')
    for obj in (rig,body,marker):obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(export),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,
        bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0,path_mode='COPY',embed_textures=False)
    studio(scene,project/'Library/ShopperBuild/Previews'/(design.identity+'.png'))
    notes=bpy.data.texts.new('README / '+design.identity)
    notes.write(json.dumps(design.data(),indent=2)+'\n14-bone rigid-weighted low-poly rig. +Y front, Z up.\n'
        'Idle, Walk and Browse are editable Actions. Body has one shared UV palette material.\n'
        'STUDIO objects are not part of the export. Model origin is at the feet.\n')
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(destination))
    if render:
        Path(scene.render.filepath).parent.mkdir(parents=True,exist_ok=True);bpy.ops.render.render(write_still=True)
    triangles=sum(len(face.vertices)-2 for face in body.data.polygons)
    return design.data()|{'source':str(destination.relative_to(project)),'source_sha256':hashlib.sha256(destination.read_bytes()).hexdigest(),
        'fbx_sha256':hashlib.sha256(export.read_bytes()).hexdigest(),'vertices':len(body.data.vertices),'triangles':triangles,'bones':len(rig.data.bones)}

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--project',required=True,type=Path)
    parser.add_argument('--ids',default=','.join(str(i) for i in range(1,51)))
    parser.add_argument('--render',action='store_true');parser.add_argument('--replace',action='store_true')
    parser.add_argument('--export-only',action='store_true',help='Re-export existing sources without rebuilding or modifying them.')
    parser.add_argument('--refresh-animations',action='store_true',help='With export-only, explicitly rebuild and save the authored animation actions.')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);project=args.project.resolve()
    if args.refresh_animations and not args.export_only:
        parser.error('--refresh-animations requires --export-only')
    results=[]
    if args.export_only:
        manifest_path=project/'ArtSource/Shoppers/Manifest.json'
        manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
        for number in map(int,args.ids.split(',')):
            design=BY_NUMBER[number]
            source=project/'ArtSource/Shoppers'/design.identity/(design.identity+'.blend')
            bpy.ops.wm.open_mainfile(filepath=str(source))
            if args.refresh_animations:
                rig=bpy.data.objects['Rig']
                rig.animation_data_clear()
                for action in list(bpy.data.actions):bpy.data.actions.remove(action)
                animations(rig)
                bpy.context.preferences.filepaths.save_version=0
                bpy.ops.wm.save_as_mainfile(filepath=str(source))
            bpy.ops.object.select_all(action='DESELECT')
            for name in ('Rig','Body','FacingMarker'):bpy.data.objects[name].select_set(True)
            bpy.context.view_layer.objects.active=bpy.data.objects['Rig']
            export=project/'Library/ShopperBuild/Exports'/(design.identity+'.fbx')
            bpy.ops.export_scene.fbx(filepath=str(export),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},
                axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,
                bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
                path_mode='COPY',embed_textures=False)
            entry=next(entry for entry in manifest['characters'] if entry['number']==number)
            entry['source_sha256']=hashlib.sha256(source.read_bytes()).hexdigest()
            entry['fbx_sha256']=hashlib.sha256(export.read_bytes()).hexdigest()
            mesh=bpy.data.objects['Body'].data
            entry['vertices']=len(mesh.vertices)
            entry['triangles']=sum(len(face.vertices)-2 for face in mesh.polygons)
            entry['bones']=len(bpy.data.objects['Rig'].data.bones)
        manifest_path.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
        return
    for number in map(int,args.ids.split(',')):
        results.append(build(project,number,args.render,args.replace));print('SHOPPER_COMPLETE '+results[-1]['id'],flush=True)
    receipt=project/'Library/ShopperBuild'/('batch-'+args.ids.replace(',','-')+'.json')
    manifest={'visual_revision':2,'palette_columns':PALETTE_COLUMNS,
        'palette_sha256':hashlib.sha256((project/'Library/ShopperBuild/Exports/ShopperPalette.png').read_bytes()).hexdigest(),
        'characters':results}
    receipt.parent.mkdir(parents=True,exist_ok=True);receipt.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    if len(results)==50:
        (project/'ArtSource/Shoppers/Manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

if __name__=='__main__':main()
