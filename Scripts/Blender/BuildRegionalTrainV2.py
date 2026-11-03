"""Regional Train V2: three-car reference, flat normals, solid materials, independent wheels.
Blender X=across, +Y=forward, Z=up; FBX exports Unity +Z forward, Y up.
"""
import argparse,json,math,sys
from pathlib import Path
import bpy,bmesh
from mathutils import Vector

PALETTE={'Cream':'F0DDBC','Green':'2F5D46','Stripe':'C4573A','Roof':'6E6E6E','Glass':'4A6275',
         'Frame':'F7E7CA','Dark':'252D30','Steel':'666E70','Lamp':'FFDF91','Marker':'B93329','Rubber':'353B3C'}
LENGTH=15.10
SPACING=16.0
WHEEL_RADIUS=.4
BOGIE_OFFSET=5.65


def linear(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4

def materials():
    result={}
    for name,value in PALETTE.items():
        rgb=tuple(linear(int(value[i:i+2],16)/255) for i in (0,2,4))
        mat=bpy.data.materials.new(name);mat.diffuse_color=(*rgb,1);mat.use_nodes=True
        bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(*rgb,1);bsdf.inputs['Roughness'].default_value=.85
        result[name]=mat
    return result

class Car:
    def __init__(self,name,mats):
        self.root=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(self.root);self.mats=mats
    def empty(self,name,position=(0,0,0),parent=None):
        obj=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(obj);obj.parent=parent or self.root;obj.location=position;return obj
    def mesh(self,name,vertices,faces,mat,parent=None):
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);obj.parent=parent or self.root;mesh.materials.append(self.mats[mat])
        for p in mesh.polygons:p.use_smooth=False
        return obj
    def bevel(self,obj,width=.025):
        bpy.context.view_layer.objects.active=obj;obj.select_set(True)
        mod=obj.modifiers.new('Small geometric edge bevel','BEVEL');mod.width=width;mod.segments=1;mod.affect='EDGES'
        bpy.ops.object.modifier_apply(modifier=mod.name);obj.select_set(False)
        for p in obj.data.polygons:p.use_smooth=False
        return obj
    def box(self,name,pos,size,mat,parent=None,bevel=.025):
        bpy.ops.mesh.primitive_cube_add(size=1);obj=bpy.context.object;obj.name=name;obj.dimensions=size
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);obj.parent=parent or self.root;obj.location=pos;obj.data.materials.append(self.mats[mat])
        self.bevel(obj,min(bevel,min(size)*.24));return obj
    def cylinder(self,name,pos,radius,depth,mat,parent,axis='X'):
        bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=radius,depth=depth);obj=bpy.context.object;obj.name=name
        obj.parent=parent;obj.location=pos;obj.rotation_euler=(0,math.pi/2,0) if axis=='X' else ((math.pi/2,0,0) if axis=='Y' else (0,0,0))
        obj.data.materials.append(self.mats[mat]);self.bevel(obj,.012);return obj
    def hull(self,name,z0,z1,w0,w1,front0,front1,mat,cab):
        def ring(z,w,front):
            if cab:return [(-w,-7.55,z),(w,-7.55,z),(w,front-.27,z),(w-.19,front,z),(-w+.19,front,z),(-w,front-.27,z)]
            return [(-w,-7.55,z),(w,-7.55,z),(w,7.55,z),(-w,7.55,z)]
        a=ring(z0,w0,front0);b=ring(z1,w1,front1);n=len(a)
        faces=[tuple(reversed(range(n))),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        return self.bevel(self.mesh(name,a+b,faces,mat),.018)
    def side_panel(self,name,side,y,z,width,height,mat,radius=.055,offset=0):
        # Chamfer the visible outline independently of panel thickness.
        a,b=width/2,height/2;r=min(radius,a*.3,b*.3)
        outline=[(-a+r,-b),(a-r,-b),(a,-b+r),(a,b-r),(a-r,b),(-a+r,b),(-a,b-r),(-a,-b+r)]
        vertices=[(side*(1.39+offset+d),y+u,z+v) for d in (-.0175,.0175) for u,v in outline]
        faces=[tuple(range(8)),tuple(reversed(range(8,16)))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
        return self.bevel(self.mesh(name,vertices,faces,mat),.003)
    def window(self,side,y,index):
        root=self.empty('Window_'+str(side)+'_'+str(index))
        # Frame and glass use separate depths so no coplanar texture flicker is possible.
        frame=self.side_panel('WindowFrame_'+str(side)+'_'+str(index),side,y,2.40,1.58,1.02,'Frame',offset=.008)
        pane=self.side_panel('WindowGlass_'+str(side)+'_'+str(index),side,y,2.40,1.44,.88,'Glass',offset=.030)
        for obj in (frame,pane):obj.parent=root
    def doors(self,side,y):
        root=self.empty('Door_'+str(side))
        surround=self.side_panel('DoorSurround_'+str(side),side,y,1.90,1.38,2.18,'Dark',offset=.010);surround.parent=root
        for index,sign in enumerate((-1,1)):
            leaf=self.empty('DoorLeaf_'+str(side)+'_'+str(index),parent=root)
            panel=self.side_panel('DoorPanel_'+str(side)+'_'+str(index),side,y+sign*.327,1.90,.625,2.08,'Cream',offset=.034);panel.parent=leaf
            glass=self.side_panel('DoorGlass_'+str(side)+'_'+str(index),side,y+sign*.327,2.44,.21,.72,'Glass',offset=.057);glass.parent=leaf
        step=self.box('BoardingStep_'+str(side),(side*1.35,y,.77),(.26,1.38,.13),'Steel');step.parent=root
    def front_window(self,side):
        def pane(name,x0,x1,z0,z1,mat,offset):
            # Cab face is a sloped plane; both frame and glass follow the same plane.
            def fy(z):return 7.70+(z-1.90)*(6.88-7.70)/(2.98-1.90)+offset
            cut=.045
            outline=[(x0+cut,z0),(x1-cut,z0),(x1,z0+cut),(x1,z1-cut),(x1-cut,z1),(x0+cut,z1),(x0,z1-cut),(x0,z0+cut)]
            front=[(x,fy(z),z) for x,z in outline];back=[(x,y-.018,z) for x,y,z in front];n=len(front)
            return self.mesh(name,front+back,[tuple(range(n)),tuple(reversed(range(n,n*2)))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat)
        x0,x1=(-1.07,-.085) if side<0 else (.085,1.07)
        pane('WindscreenFrame_'+str(side),x0,x1,2.04,2.88,'Frame',.027)
        pane('WindscreenGlass_'+str(side),x0+.042,x1-.042,2.085,2.835,'Glass',.049)
    def bogie(self,sign):
        root=self.empty('Bogie_Front' if sign>0 else 'Bogie_Rear',(0,sign*BOGIE_OFFSET,WHEEL_RADIUS))
        self.box('BogieCrossFrame',(0,0,.17),(1.90,2.28,.26),'Dark',root)
        for side in (-1,1):
            self.box('BogieSideFrame_'+str(side),(side*.98,0,.08),(.18,2.38,.25),'Dark',root)
            for axle in (-1,1):
                pivot=self.empty('WheelPivot_'+str(sign)+'_'+str(side)+'_'+str(axle),(side*.76,axle*.72,0),root)
                self.cylinder('Wheel_'+str(sign)+'_'+str(side)+'_'+str(axle),(0,0,0),WHEEL_RADIUS,.18,'Dark',pivot)
                self.cylinder('WheelHub_'+str(sign)+'_'+str(side)+'_'+str(axle),(side*.105,0,0),.255,.035,'Steel',pivot)
            for level in range(3):self.cylinder('Spring_'+str(side)+'_'+str(level),(side*.98,0,.24+level*.065),.16,.05,'Steel',root,'Z')
        return root
    def build(self,cab):
        self.box('Chassis',(0,0,.88),(2.66,14.85,.24),'Dark')
        self.hull('LowerBody',.96,1.79,1.38,1.38,7.65,7.70,'Green',cab)
        self.hull('WaistStripe',1.79,1.90,1.383,1.383,7.70,7.70,'Stripe',cab)
        self.hull('UpperBody',1.90,2.98,1.38,1.38,7.70,6.88,'Cream',cab)
        self.hull('Roof',2.98,3.25,1.405,1.18,6.88,6.60,'Roof',cab)
        self.box('RoofHVAC',(0,-.2,3.40),(1.12,1.60,.36),'Roof',bevel=.07)
        for side in (-1,1):
            for i in range(3):self.box('HVAC_Vent_'+str(side)+'_'+str(i),(side*.557,-.2,3.32+i*.075),(.012,1.04,.024),'Dark',bevel=.004)
            for index,y in enumerate((-5.25,-2.95,-.65,1.65) if cab else (-5.5,-3.3,-1.1,1.1,3.3)):self.window(side,y,index)
            self.doors(side,4.45 if cab else 5.75)
            self.box('UnderfloorEquipment_'+str(side),(side*.66,-.5,.58),(1.0,3.8,.52),'Steel',bevel=.045)
            self.box('EquipmentLid_'+str(side),(side*1.17,-.5,.58),(.018,3.5,.35),'Roof',bevel=.004)
        self.bogie(1);self.bogie(-1)
        self.empty('RearGangwaySocket',(0,-7.55,1.95))
        if cab:
            for side in (-1,1):self.front_window(side)
            self.box('FrontSkirt',(0,7.57,.83),(2.64,.42,.38),'Dark',bevel=.09)
            self.box('CouplerBeam',(0,7.75,.68),(.25,.45,.18),'Dark')
            self.box('Coupler',(0,7.87,.68),(.46,.32,.26),'Steel',bevel=.025)
            lamps=self.empty('Headlights');markers=self.empty('MarkerLights')
            for side in (-1,1):
                self.cylinder('HeadlightHousing_'+str(side),(side*.91,7.73,1.55),.18,.08,'Steel',lamps,'Y')
                self.cylinder('HeadlightLens_'+str(side),(side*.91,7.78,1.55),.139,.035,'Lamp',lamps,'Y')
                self.cylinder('Marker_'+str(side),(side*.91,7.74,1.20),.069,.037,'Marker',markers,'Y')
        else:self.empty('FrontGangwaySocket',(0,7.55,1.95))
    def export(self,path):
        bpy.ops.object.select_all(action='DESELECT');self.root.select_set(True)
        for obj in self.root.children_recursive:obj.select_set(True)
        bpy.context.view_layer.objects.active=self.root
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=False,mesh_smooth_type='FACE',bake_anim=False)
    def triangles(self):return sum(len(p.vertices)-2 for obj in self.root.children_recursive if obj.type=='MESH' for p in obj.data.polygons)


def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()

def build(project):
    bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.unit_settings.system='METRIC'
    out=project/'Library/RegionalTrainV2/Exports';out.mkdir(parents=True,exist_ok=True)
    docs=project/'Docs/RegionalTrainV2';docs.mkdir(parents=True,exist_ok=True)
    source=project/'ArtSource/RegionalTrainV2';source.mkdir(parents=True,exist_ok=True)
    mats=materials();cab=Car('Cab',mats);cab.build(True);cab.export(out/'Cab.fbx')
    coach=Car('Coach',mats);coach.build(False);coach.export(out/'Coach.fbx')
    cab.root.location.y=16
    tail=bpy.data.objects.new('Car3',None);bpy.context.collection.objects.link(tail);tail.location.y=-16
    # Editable linked copies keep the three-car assembly in one .blend without destructive joins.
    def duplicate_tree(old,parent):
        new=old.copy();bpy.context.collection.objects.link(new);new.parent=parent
        for child in old.children:duplicate_tree(child,new)
        return new
    tail_model=duplicate_tree(cab.root,tail);tail_model.location=(0,0,0);tail_model.rotation_euler.z=math.pi
    cab.root.name='Car1';coach.root.name='Car2'
    for center in (-8,8):
        tunnel=Car('GangwayTunnel_'+str(center),mats)
        tunnel.box('DarkFlexibleTunnel',(0,center,1.95),(1.65,.90,2.02),'Dark',bevel=.02)
        for index in range(5):
            rib=Car('Gangway_'+str(center)+'_'+str(index),mats)
            rib.box('RubberFold',(0,center+(index-2)*.18,1.95),(1.84,.11,2.20),'Rubber',bevel=.055)
    assembly=bpy.data.objects.new('RegionalTrain',None);bpy.context.collection.objects.link(assembly)
    for obj in list(bpy.context.scene.objects):
        if obj!=assembly and obj.parent is None:obj.parent=assembly
    # glTF maps Blender +Y to -Z. Rotate the export only so the supplied GLB faces +Z.
    assembly.rotation_euler.z=math.pi
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.gltf(filepath=str(source/'RegionalTrainV2.glb'),export_format='GLB',use_selection=True,export_yup=True)
    assembly.rotation_euler.z=0
    # The track is only a render aid; the game retains its existing railway.
    preview=Car('Preview track — not exported',mats)
    for x in (-.72,.72):preview.box('Preview rail',(x,0,-.07),(.075,54,.14),'Steel',bevel=.006)
    for y in range(-27,28):preview.box('Preview sleeper',(0,y,-.20),(2.2,.22,.15),'Dark',bevel=.01)
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.28));floor=bpy.context.object
    mat=bpy.data.materials.new('Preview cream');mat.diffuse_color=(.72,.68,.58,1);floor.data.materials.append(mat)
    world=bpy.data.worlds.new('Soft daylight');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Strength'].default_value=.7
    light=bpy.data.lights.new('Key','AREA');light.energy=4500;light.shape='DISK';light.size=35
    obj=bpy.data.objects.new('Key',light);scene.collection.objects.link(obj);obj.location=(12,10,30);aim(obj,(0,0,1))
    sun=bpy.data.lights.new('Daylight','SUN');sun.energy=1.5;sun.angle=.20;obj=bpy.data.objects.new('Daylight',sun);scene.collection.objects.link(obj);obj.rotation_euler=(.4,-.3,-.6)
    camera=bpy.data.objects.new('Preview camera',bpy.data.cameras.new('Preview camera'));scene.collection.objects.link(camera);scene.camera=camera
    camera.location=(38,45,22);aim(camera,(0,0,1.4));camera.data.type='ORTHO';camera.data.ortho_scale=57
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=12;scene.cycles.use_denoising=False
    scene.render.threads_mode='FIXED';scene.render.threads=2
    scene.render.resolution_x=1400;scene.render.resolution_y=780;scene.view_settings.view_transform='Standard'
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source/'RegionalTrainV2.blend'))
    scene.render.filepath=str(docs/'BlenderTrain.png');bpy.ops.render.render(write_still=True)
    camera.location=(8,29,5.7);aim(camera,(0,20.4,1.75));camera.data.ortho_scale=12
    scene.render.filepath=str(docs/'BlenderCab.png');bpy.ops.render.render(write_still=True)
    report=dict(cab_triangles=cab.triangles(),coach_triangles=coach.triangles(),palette=PALETTE,spacing=SPACING,bogie_offset=BOGIE_OFFSET,wheel_radius=WHEEL_RADIUS,axis='Blender +Y forward, Unity +Z forward',blender=bpy.app.version_string)
    (docs/'Model.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--project',type=Path,required=True);build(p.parse_args(sys.argv[sys.argv.index('--')+1:]).project)
