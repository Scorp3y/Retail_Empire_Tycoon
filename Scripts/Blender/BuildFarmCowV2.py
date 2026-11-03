"""Approved farm concept, stage 1: cow only. Blender +Y forward, Z up.
Flat normals; small geometric chamfers; material patches are actual face regions.
"""
import argparse,json,math,sys
from pathlib import Path
import bpy,bmesh
from mathutils import Vector

PALETTE={'Cream':'EFE3CC','CreamShade':'E6D9BE','Dark':'5A4E48','Pink':'F4B8B0','PinkShade':'EDA39C','Eyes':'302C29','Horn':'E6D2A9'}

def linear(x):return x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4
def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()

class Cow:
    def __init__(self):
        self.mats={}
        for name,value in PALETTE.items():
            mat=bpy.data.materials.new(name);mat.use_nodes=True
            color=tuple(linear(int(value[i:i+2],16)/255) for i in (0,2,4))
            mat.diffuse_color=(*color,1);bsdf=mat.node_tree.nodes['Principled BSDF'];bsdf.inputs['Base Color'].default_value=(*color,1);bsdf.inputs['Roughness'].default_value=.85
            self.mats[name]=mat
        self.root=self.joint('Cow')
    def joint(self,name,parent=None,position=(0,0,0)):
        obj=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(obj);obj.parent=parent;obj.location=position;return obj
    def finish(self,obj,name,parent,position,material):
        obj.name=name;obj.parent=parent;obj.location=position;obj.data.materials.append(self.mats[material]);return obj
    def box(self,name,parent,position,size,material,bevel,segments=1):
        bpy.ops.mesh.primitive_cube_add(size=1);obj=bpy.context.object;obj.dimensions=size
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        self.finish(obj,name,parent,position,material)
        mod=obj.modifiers.new('Soft geometric edges','BEVEL');mod.width=bevel;mod.segments=segments;bpy.ops.object.modifier_apply(modifier=mod.name)
        for face in obj.data.polygons:face.use_smooth=False
        return obj
    def ico(self,name,parent,position,size,material):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1);obj=bpy.context.object;obj.scale=size
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        return self.finish(obj,name,parent,position,material)
    def mesh(self,name,parent,vertices,faces,material):
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);obj.parent=parent;mesh.materials.append(self.mats[material]);return obj
    def oval(self,name,parent,center,rx,rz,material):
        # A tiny modeled face mark; no image, transparent card or overlapping duplicate face.
        obj=self.mesh(name,parent,[(rx*math.cos(i*math.tau/8),0,rz*math.sin(i*math.tau/8)) for i in range(8)],[tuple(reversed(range(8)))],material)
        obj.location=center;return obj
    def patch(self,obj,axis,side,amount=.55):
        mesh=obj.data;v=[x.co.copy() for x in mesh.vertices];faces=[];slots=[]
        mesh.materials.append(self.mats['Dark']);dark=len(mesh.materials)-1
        for face in mesh.polygons:
            loop=list(face.vertices)
            if len(loop)!=4 or face.normal[axis]*side<.999 or face.area<.04:
                faces.append(loop);slots.append(face.material_index);continue
            center=sum((v[i] for i in loop),Vector())/4
            outline=[]
            for i in range(4):outline.extend([v[loop[i]],(v[loop[i]]+v[loop[(i+1)%4]])*.5])
            inner=[]
            for i,p in enumerate(outline):
                inner.append(len(v));v.append(center+(p-center)*amount*(.73,.84,.88,.88,.71,.86,.78,.91)[i])
            for i in range(4):
                faces.append([loop[i],loop[(i+1)%4],inner[(2*i+2)%8],inner[2*i+1],inner[2*i]]);slots.append(0)
            faces.append(inner);slots.append(dark)
        replacement=bpy.data.meshes.new(mesh.name+'_patch');replacement.from_pydata(v,[],faces);replacement.update()
        for mat in mesh.materials:replacement.materials.append(mat)
        for face,slot in zip(replacement.polygons,slots):face.material_index=slot;face.use_smooth=False
        obj.data=replacement;bpy.data.meshes.remove(mesh)
    def build(self):
        body=self.box('Body',self.root,(0,-.10,1.03),(1.10,1.78,1.02),'Cream',.23,segments=2)
        self.patch(body,0,1,.88);self.patch(body,0,-1,.85);self.patch(body,2,1,.72)
        for face in body.data.polygons:
            if face.normal.z<-.3 and face.material_index==0:
                if len(body.data.materials)<5:body.data.materials.append(self.mats['CreamShade'])
                face.material_index=len(body.data.materials)-1
        head=self.joint('Head',self.root,(0,.65,1.28))
        skull=self.box('Skull',head,(0,.24,.04),(.68,.67,.72),'Cream',.15);self.patch(skull,2,1,.65)
        self.box('Muzzle',head,(0,.60,-.13),(.64,.32,.34),'Pink',.075)
        for side,suffix in ((-1,'L'),(1,'R')):
            eye=self.joint('Eye_'+suffix,head,(side*.20,.577,.14))
            self.oval('EyeSurface_'+suffix,eye,(0,0,0),.026,.050,'Eyes')
            self.oval('Nostril_'+suffix,head,(side*.145,.762,-.13),.026,.040,'Eyes')
            ear=self.joint('Ear_'+suffix,head,(side*.30,.19,.22))
            outer=self.ico('EarShape_'+suffix,ear,(side*.14,0,0),(.23,.075,.13),'Dark')
            inner=self.oval('EarInner_'+suffix,ear,(side*.16,.068,0),.12,.065,'PinkShade')
            bpy.ops.mesh.primitive_cone_add(vertices=6,radius1=.067,radius2=.018,depth=.24)
            horn=self.finish(bpy.context.object,'Horn_'+suffix,head,(side*.22,.17,.43),'Horn');horn.scale.z=.78;horn.rotation_euler.y=side*.28
        for side,suffix in ((-1,'L'),(1,'R')):
            for y,front in ((.53,'F'),(-.69,'B')):
                leg=self.joint('Leg_'+front+suffix,self.root,(side*.34,y,.72))
                self.box('Limb_'+front+suffix,leg,(0,0,-.31),(.25,.28,.60),'Cream',.05)
                self.box('Hoof_'+front+suffix,leg,(0,.025,-.61),(.27,.32,.22),'Dark',.035)
        tail=self.joint('Tail',self.root,(0,-.95,1.30))
        points=[(0,0,0),(0,-.08,-.10),(0,-.12,-.33),(0,-.11,-.61)]
        vertices=[]
        for i,p in enumerate(points):
            for j in range(4):vertices.append((p[0]+.027*math.cos(j*math.tau/4),p[1]+.027*math.sin(j*math.tau/4),p[2]))
        faces=[tuple(reversed(range(4))),tuple(range(12,16))]
        for k in range(3):
            for j in range(4):faces.append((k*4+j,k*4+(j+1)%4,(k+1)*4+(j+1)%4,(k+1)*4+j))
        self.mesh('TailStem',tail,vertices,faces,'CreamShade');self.ico('TailTuft',tail,(0,-.11,-.66),(.075,.075,.13),'Dark')
        for obj in self.root.children_recursive:
            if obj.type=='MESH':
                for face in obj.data.polygons:face.use_smooth=False

def run(project):
    bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.unit_settings.system='METRIC'
    source=project/'ArtSource/FarmAnimalsV2';source.mkdir(parents=True,exist_ok=True)
    export=project/'Library/FarmCowV2/Exports';export.mkdir(parents=True,exist_ok=True)
    docs=project/'Docs/FarmCowV2';docs.mkdir(parents=True,exist_ok=True)
    cow=Cow();cow.build();triangles=sum(len(p.vertices)-2 for obj in cow.root.children_recursive if obj.type=='MESH' for p in obj.data.polygons)
    assert triangles<=800,f'Triangle budget exceeded: {triangles}'
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(export/'Cow.fbx'),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,mesh_smooth_type='FACE')
    cow.root.rotation_euler.z=math.pi
    bpy.ops.export_scene.gltf(filepath=str(source/'Cow.glb'),export_format='GLB',use_selection=True,export_yup=True)
    cow.root.rotation_euler.z=0
    # Preview-only studio, excluded from both exports.
    bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='PreviewGround'
    mat=bpy.data.materials.new('Preview cream');mat.use_nodes=True;mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.88,.83,.72,1);mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.85;floor.data.materials.append(mat)
    world=bpy.data.worlds.new('Daylight');world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Strength'].default_value=.7;scene.world=world
    light=bpy.data.lights.new('Softbox','AREA');light.energy=350;light.size=5
    obj=bpy.data.objects.new('Softbox',light);scene.collection.objects.link(obj);obj.location=(-3,4,6);aim(obj,(0,0,1))
    camera=bpy.data.objects.new('PreviewCamera',bpy.data.cameras.new('PreviewCamera'));scene.collection.objects.link(camera);scene.camera=camera
    camera.data.type='ORTHO';camera.data.ortho_scale=3.75;camera.location=(-3.8,5.1,3.0);aim(camera,(0,.05,.95))
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=32;scene.cycles.use_denoising=False
    scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.resolution_x=1200;scene.render.resolution_y=1000;scene.view_settings.view_transform='Standard'
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source/'Cow.blend'))
    scene.render.filepath=str(docs/'Cow-Blender.png');bpy.ops.render.render(write_still=True)
    camera.location=(-5,0,1.6);aim(camera,(0,.1,.95));scene.render.filepath=str(docs/'Cow-Side.png');bpy.ops.render.render(write_still=True)
    report={'triangles':triangles,'palette':PALETTE,'axis':'Unity +Z forward, Y up','pivot':'ground between feet','stage':'Cow only'}
    (docs/'Model.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--project',type=Path,required=True)
    run(parser.parse_args(sys.argv[sys.argv.index('--')+1:]).project)
