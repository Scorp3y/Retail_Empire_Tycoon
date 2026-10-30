"""Editable, flat-shaded bakery meshes based on the approved six-object preview.

Small geometric bevels soften silhouettes. There are no photographic textures,
surface-noise shaders, floating product labels, rigs or extra display furniture.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path
import bpy
from mathutils import Vector
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).parent))
from BakeryDesigns import PALETTE,PRODUCTS


class BakeryModel:
    def __init__(self,material):
        self.material=material
        self.parts=[]

    def paint(self,obj,color):
        obj.data.materials.clear();obj.data.materials.append(self.material)
        uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='BakeryPalette')
        uv.name='BakeryPalette';index=list(PALETTE).index(color)
        point=((index%4+.5)/4,1-(index//4+.5)/4)
        for loop in uv.data:loop.uv=point
        for polygon in obj.data.polygons:polygon.use_smooth=False
        self.parts.append(obj);return obj

    def mesh(self,name,vertices,faces,color,bevel=0,closed=True):
        if closed:
            volume=0
            for face in faces:
                a=Vector(vertices[face[0]])
                for index in range(1,len(face)-1):volume+=a.dot(Vector(vertices[face[index]]).cross(Vector(vertices[face[index+1]])))/6
            if volume<=1e-12:raise ValueError('Closed part has inward or degenerate faces: '+name)
        data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update()
        obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
        if bevel:
            bpy.context.view_layer.objects.active=obj;obj.select_set(True)
            modifier=obj.modifiers.new('Small silhouette bevel','BEVEL');modifier.width=bevel;modifier.segments=2
            modifier.limit_method='ANGLE';modifier.angle_limit=math.radians(50)
            bpy.ops.object.modifier_apply(modifier=modifier.name);obj.select_set(False)
        return self.paint(obj,color)

    def box(self,name,center,size,color,rotation=(0,0,0),bevel=.0015):
        bpy.ops.mesh.primitive_cube_add(size=1,location=center);obj=bpy.context.object;obj.name=name;obj.scale=size
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        if bevel:
            modifier=obj.modifiers.new('Soft corners','BEVEL');modifier.width=min(bevel,min(size)*.2);modifier.segments=2
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.rotation_euler=rotation;return self.paint(obj,color)

    def ball(self,name,center,size,color,subdivisions=1):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions,radius=1,location=center)
        obj=bpy.context.object;obj.name=name;obj.scale=size
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        return self.paint(obj,color)

    def profile(self,name,rings,color,segments=12,fold=0):
        vertices=[];faces=[]
        for height,radius in rings:
            for i in range(segments):
                r=radius+(fold if i%2 else -fold);angle=i*math.tau/segments
                vertices.append((r*math.cos(angle),r*math.sin(angle),height))
        faces.append(tuple(reversed(range(segments))))
        for ring in range(len(rings)-1):
            for i in range(segments):faces.append((ring*segments+i,ring*segments+(i+1)%segments,(ring+1)*segments+(i+1)%segments,(ring+1)*segments+i))
        faces.append(tuple((len(rings)-1)*segments+i for i in range(segments)))
        return self.mesh(name,vertices,faces,color,bevel=.0007)

    def tube(self,name,points,radii,color,segments=8,vertical=1):
        """Flat-shaded pastry along a path; cap bevels preserve broad side facets."""
        points=[Vector(p) for p in points];vertices=[];faces=[]
        for i,point in enumerate(points):
            tangent=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized()
            side=tangent.cross(Vector((0,0,1))).normalized()
            if side.length<.5:side=Vector((1,0,0))
            up=side.cross(tangent).normalized()
            for j in range(segments):
                angle=j*math.tau/segments
                vertices.append(point+side*(math.cos(angle)*radii[i])+up*(math.sin(angle)*radii[i]*vertical))
        faces.append(tuple(reversed(range(segments))))
        for ring in range(len(points)-1):
            for j in range(segments):faces.append((ring*segments+j,ring*segments+(j+1)%segments,(ring+1)*segments+(j+1)%segments,(ring+1)*segments+j))
        faces.append(tuple((len(points)-1)*segments+j for j in range(segments)))
        # Side/up framing is left-handed relative to the path tangent.
        faces=[tuple(reversed(face)) for face in faces]
        return self.mesh(name,vertices,faces,color,bevel=.0008)

    def croissant(self):
        limits=(0,.12,.26,.42,.58,.74,.88,1)
        for band,(start,end) in enumerate(zip(limits,limits[1:])):
            points=[];radii=[]
            for i in range(5):
                t=start+(end-start)*i/4;angle=math.pi*t
                points.append((-.16*math.cos(angle),.078*math.sin(angle),.035+.037*math.sin(angle)))
                fullness=.90+.13*math.sin(i/4*math.pi)
                radii.append((.006+.048*math.sin(angle)**.8)*fullness)
            self.tube('Broad dough band '+str(band+1),points,radii,'dough' if band%2 else 'light_dough',8,vertical=.92)

    def doughnut(self):
        ring_segments=20;cross=8;major=.079;minor=.038;center=.034
        points=[];faces=[]
        for i in range(ring_segments):
            angle=i*math.tau/ring_segments
            for j in range(cross):
                around=j*math.tau/cross;r=major+math.cos(around)*minor
                points.append((math.cos(angle)*r,math.sin(angle)*r,center+math.sin(around)*minor*.80))
        for i in range(ring_segments):
            for j in range(cross):faces.append((i*cross+j,((i+1)%ring_segments)*cross+j,((i+1)%ring_segments)*cross+(j+1)%cross,i*cross+(j+1)%cross))
        self.mesh('Faceted dough ring',points,faces,'dough')
        vertices=[];faces=[]
        for i in range(ring_segments):
            angle=i*math.tau/ring_segments
            for j in range(6):
                around=-.12+(math.pi+.24)*j/5
                if j in (0,5):around+=.055*math.sin(i*2.5)
                r=major+math.cos(around)*(minor+.0018)
                vertices.append((math.cos(angle)*r,math.sin(angle)*r,center+math.sin(around)*minor*.80+.003))
        for i in range(ring_segments):
            for j in range(5):faces.append((i*6+j,((i+1)%ring_segments)*6+j,((i+1)%ring_segments)*6+j+1,i*6+j+1))
        self.mesh('Simple pink icing cap',vertices,faces,'icing',closed=False)
        for i,color in enumerate(('yellow','blue','mint','rose','ivory','blue','yellow','mint')):
            angle=i*math.tau/8+.2;r=.074 if i%2 else .086
            z=center+math.sqrt(1-((r-major)/minor)**2)*minor*.80+.006
            self.box('Large sprinkle '+str(i),(math.cos(angle)*r,math.sin(angle)*r,z),(.016,.006,.004),color,rotation=(0,0,angle+.5),bevel=.001)

    def muffin(self):
        self.profile('Eight folded paper panels',[(.002,.041),(.006,.043),(.079,.058),(.084,.057)],'paper',16,fold=.0025)
        self.profile('Plump faceted muffin top',[(.077,.056),(.090,.068),(.119,.067),(.148,.052),(.165,.027),(.168,.007)],'light_dough',10)
        for i,(x,y,z) in enumerate(((-.035,-.026,.145),(.032,-.009,.153),(-.008,.029,.163))):
            self.box('Chocolate chunk '+str(i),(x,y,z),(.024,.022,.021),'cocoa',rotation=(.1,-.14,i*.8),bevel=.0025)

    @staticmethod
    def curve(points,steps=3):
        """Catmull-Rom knots give a few broad, even pastry segments."""
        points=[Vector(p) for p in points];result=[]
        for i in range(len(points)-1):
            p0=points[max(0,i-1)];p1=points[i];p2=points[i+1];p3=points[min(i+2,len(points)-1)]
            for step in range(steps):
                t=step/steps
                result.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t))
        return result+[points[-1]]

    def pretzel(self):
        outer=[(0,-.105,.030),(-.085,-.095,.030),(-.125,-.035,.030),(-.115,.050,.030),(-.075,.085,.030),
            (-.035,.068,.030),(0,.022,.030),(.035,.068,.030),(.075,.085,.030),(.115,.050,.030),
            (.125,-.035,.030),(.085,-.095,.030),(0,-.105,.030)]
        path=self.curve(outer,2);self.tube('Outer pretzel loop',path,[.023]*len(path),'dough',8,vertical=.87)
        for side,height in ((1,.053),(-1,.032)):
            path=self.curve([(-side*.075,-.084,.035),(-side*.030,-.025,height),(side*.035,.045,.042),(side*.067,.078,.033)],3)
            self.tube('Crossed dough strand',path,[.022]*len(path),'dough',8,vertical=.84)
        for i,(x,y) in enumerate(((-.10,.025),(-.075,.072),(.078,.073),(.11,.027),(-.09,-.073),(.09,-.072),(-.035,-.10),(.035,-.10))):
            self.box('Broad salt piece '+str(i),(x,y,.054),(.008,.007,.005),'ivory',rotation=(.1,0,i*.7),bevel=.0015)

    def cupcake(self):
        self.profile('Pink folded paper cup',[(.001,.039),(.006,.041),(.074,.055),(.079,.054)],'rose',16,fold=.0025)
        self.profile('Cupcake sponge',[(.070,.052),(.086,.056),(.100,.048),(.105,.031)],'light_dough',12)
        for i,(base,radius,height) in enumerate(((.095,.049,.028),(.120,.038,.027),(.145,.025,.025))):
            self.profile('Broad cream frosting tier '+str(i+1),[(base,radius*.82),(base+.008,radius),(base+height-.006,radius*.9),(base+height,radius*.51)],'cream',12)
        self.ball('Cherry',(0,0,.181),(.019,.019,.018),'berry',2)
        self.tube('Cherry stem',[(0,0,.195),(.003,.001,.211),(.014,.003,.220)],[.0027,.0025,.0023],'leaf',5)

    def cake_slice(self):
        triangle=[(-.112,-.070),(.119,-.070),(-.071,.112)]
        for name,bottom,top,color,bevel in (
            ('Lower sponge layer',0,.044,'light_dough',.0018),
            ('Berry filling',.044,.064,'filling',.0008),
            ('Upper sponge layer',.064,.111,'light_dough',.0018),
            ('Cream top',.111,.125,'cream',.003)):
            vertices=[(x,y,z) for z in (bottom,top) for x,y in triangle]
            self.mesh(name,vertices,[(2,1,0),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],color,bevel)
        self.ball('Cream dollop',(-.042,.010,.130),(.025,.019,.012),'cream')
        fruit=self.profile('Faceted strawberry',[(.135,.006),(.142,.015),(.160,.018),(.174,.012),(.179,.004)],'berry',9)
        fruit.location=(-.042,.010,0)
        for i in range(4):
            angle=i*math.tau/4
            self.tube('Strawberry leaf',[(-.042,.010,.174),(-.042+math.cos(angle)*.016,.010+math.sin(angle)*.016,.180)],[.004,.0009],'leaf',4)

    def export(self,definition,source,fbx):
        bpy.ops.object.select_all(action='DESELECT')
        for obj in self.parts:obj.select_set(True)
        bpy.context.view_layer.objects.active=self.parts[0];bpy.ops.object.join();obj=bpy.context.object;obj.name=definition['key']
        bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
        # Origin is centred on the floor, shared by stock slots and catalogue previews.
        world=[obj.matrix_world@vertex.co for vertex in obj.data.vertices]
        low=Vector(tuple(min(v[i] for v in world) for i in range(3)));high=Vector(tuple(max(v[i] for v in world) for i in range(3)))
        offset=Vector(((low.x+high.x)*.5,(low.y+high.y)*.5,low.z))
        for vertex in obj.data.vertices:vertex.co=obj.matrix_world@vertex.co-offset
        obj.location=(0,0,0)
        for polygon in obj.data.polygons:polygon.material_index=0
        obj.data.materials.clear();obj.data.materials.append(self.material)
        obj.data.update();bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        triangles=sum(len(p.vertices)-2 for p in obj.data.polygons)
        if not 100<triangles<3000:raise ValueError('Unexpected modelling complexity: '+str(triangles))
        source.parent.mkdir(parents=True,exist_ok=True);fbx.parent.mkdir(parents=True,exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(source),check_existing=False)
        bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},
            axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='STRIP',add_leaf_bones=False)
        return dict(key=definition['key'],triangles=triangles,vertices=len(obj.data.vertices),
            size=list(high-low),source=str(source),sha256=hashlib.sha256(fbx.read_bytes()).hexdigest())


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--project',required=True);parser.add_argument('--only')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);root=Path(args.project)
    source=root/'ArtSource/Bakery';target=root/'Library/CityUpdateQA/IsolatedProject/Assets/Art/Bakery'
    result=[]
    for definition in PRODUCTS:
        if args.only and definition['key']!=args.only:continue
        path=source/definition['key']/(definition['key']+'.blend')
        if path.exists():raise FileExistsError('Preserve artist edits before regenerating: '+str(path))
        bpy.ops.wm.read_factory_settings(use_empty=True)
        image=bpy.data.images.load(str(source/'BakeryPalette.png'));image.pack()
        material=bpy.data.materials.new('BakeryPalette');material.use_nodes=True
        shader=material.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.85
        node=material.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
        material.node_tree.links.new(node.outputs['Color'],shader.inputs['Base Color'])
        model=BakeryModel(material);getattr(model,definition['key'])()
        result.append(model.export(definition,path,target/'Models'/(definition['key']+'.fbx')))
        print('BAKERY '+definition['key']+' '+str(result[-1]['triangles'])+' triangles',flush=True)
    manifest=source/'Models.json'
    if args.only and manifest.exists():result=[p for p in json.loads(manifest.read_text(encoding='utf-8')) if p['key']!=args.only]+result
    manifest.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')

if __name__=='__main__':main()
