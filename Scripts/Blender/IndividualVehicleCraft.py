"""Bodywork authoring for separate, individually specified traffic vehicles.

Only geometric operations and surface finish are reused from the approved 01.
No new vehicle inherits a hatchback body or instantiates its assembly. Each file
has its own silhouette, window polygons, axles and functional cargo structures.
"""
import json
import math
from pathlib import Path
import bmesh
import bpy
from mathutils import Vector
from BuildSingleHatchback import Hatchback, VehicleSurfaceTextures, rounded_polygon, rounded_rectangle, aim


class VehicleAssembly:
    # Reuse the tested modelling operations without inheriting the wrong body type.
    attach=Hatchback.attach
    mesh=Hatchback.mesh
    material=Hatchback.material
    box=Hatchback.box
    line=Hatchback.line
    pane=Hatchback.pane
    project_uv=staticmethod(Hatchback.project_uv)
    smooth_hard_surface=staticmethod(Hatchback.smooth_hard_surface)
    rounded_lens_volume=Hatchback.rounded_lens_volume

    def __init__(self,scene,design,wheel_template):
        design.validate()
        self.scene,self.design=scene,design
        self.wheel_template=wheel_template
        self.groups={}
        self.parts={}
        self.materials={}
        for name in ('01_Body','02_Glazing','03_Wheels','04_Details'):
            collection=bpy.data.collections.new(name)
            scene.collection.children.link(collection)
            self.groups[name]=collection
        self.root=bpy.data.objects.new(design.identity,None)
        scene.collection.objects.link(self.root)
        self.root['forward_axis']='+Y'
        self.root['units']='metres'
        self.root['body_family']=design.family
        self.root['reference']='TrafficFleetConcept-v2.png / '+design.identity
        self.root['approved_component_source']=wheel_template['source_sha256']
        self.root['status']='Individual Blender source; Unity not modified'
        self.material('Paint / body colour',design.paint,.42)
        self.materials['Paint / warm yellow']=self.materials['Paint / body colour']
        self.material('Paint / panel gaps',tuple(c*.63 for c in design.paint),.66)
        self.material('Glass / opaque blue graphite',(.155,.215,.30),.45)
        self.material('Glass / rubber seal',(.075,.085,.09),.65)
        self.material('Bumper / charcoal',(.165,.165,.16),.73)
        self.material('Tyre / rubber',(.135,.145,.15),.85)
        self.material('Wheel / hubcap',(.57,.58,.58),.43,.23)
        self.material('Wheel / rim edge',(.69,.70,.70),.36,.30)
        self.material('Lamp / ivory',(.93,.93,.85),.25)
        self.material('Lamp / amber',(.96,.37,.055),.34)
        self.material('Lamp / red',(.70,.07,.04),.35)
        self.material('Grille / recess',(.105,.12,.125),.8)
        self.material('Grille / bars',(.20,.22,.23),.72)
        self.material('Mirror / reflector',(.24,.29,.31),.15,.72)
        self.material('Cargo / pale panels',(.87,.86,.81),.55)
        self.material('Cargo / grey panels',(.64,.66,.67),.55)
        self.material('Cargo / cream panels',(.89,.82,.65),.5)
        self.surfaces=VehicleSurfaceTextures()
        self.surfaces.apply(self.materials)
        for image in self.surfaces.images.values():
            image.name=design.identity[:2]+image.name[2:]
        for mat in set(self.materials.values()):
            for node in mat.node_tree.nodes:
                if node.type=='TEX_IMAGE' and node.image:
                    node.name=node.label=node.image.name
        self.materials['Glass / opaque blue graphite'].node_tree.nodes['Principled BSDF'].inputs['Specular IOR Level'].default_value=.12
        self.half=design.width/2
        self.scale=design.radius/.315
        self.floor=max(.23,design.radius*.80)
        self.cab_only=design.family in ('delivery','flatbed','box_truck','camper')
        self.hull_rear=design.cabin[-1][0]-.055 if self.cab_only else design.rear

    def cabin_x(self,z):
        d=self.design
        return d.width*.489-(d.width*.489-d.roof_half)*(z-d.belt)/(d.height-d.belt)

    def hull_top(self,y):
        d=self.design
        cowl=d.cabin[0][0]
        if y>=cowl:
            return d.belt+(d.nose-d.belt)*(y-cowl)/max(.04,d.front-cowl)
        if 'open_bed' in d.features and y<d.cabin[-1][0]:
            return d.belt-.19
        if 'notch_trunk' in d.features and y<d.cabin[-1][0]:
            return d.belt-.06*(d.cabin[-1][0]-y)/(d.cabin[-1][0]-d.rear)
        return d.belt

    def hull_width(self,y):
        d=self.design
        minimum=.985 if self.cab_only else .96
        t=max(0,min(1,(d.front-y)/.18))
        return self.half*(minimum+(1-minimum)*t)

    def body_x(self,z,y):
        top=self.hull_top(y)
        half=self.hull_width(y)
        profile=((self.floor,half*.90),(self.floor+.065,half*.97),
                 (self.floor+.27,half),(top-.06,half*.995),(top,half*.979))
        for (za,xa),(zb,xb) in zip(profile,profile[1:]):
            if z<=zb:
                return xa+(xb-xa)*max(0,(z-za)/(zb-za))
        return profile[-1][1]

    def hard_bevel(self,obj,width=.035,segments=3,apply=False):
        bpy.context.view_layer.objects.active=obj
        mod=obj.modifiers.new('Formed edge radii','BEVEL')
        mod.width=width
        mod.segments=segments
        mod.limit_method='ANGLE'
        mod.angle_limit=math.radians(18)
        mod.harden_normals=True
        if apply:
            bpy.ops.object.modifier_apply(modifier=mod.name)

    def hull(self):
        d=self.design
        stations={self.hull_rear,self.hull_rear+.14,d.front-.18,d.front,
                  d.cabin[0][0],max(self.hull_rear+.15,d.cabin[-1][0])}
        stations=sorted(y for y in stations if self.hull_rear<=y<=d.front)
        vertices=[]
        for y in stations:
            h=self.hull_width(y)
            z=self.hull_top(y)
            section=((-h*.90,self.floor),(-h*.97,self.floor+.065),(-h,self.floor+.27),
                     (-h*.995,z-.06),(-h*.979,z),(0,z+.006),(h*.979,z),
                     (h*.995,z-.06),(h,self.floor+.27),(h*.97,self.floor+.065),(h*.90,self.floor))
            vertices.extend((x,y,zz) for x,zz in section)
        count=11
        faces=[tuple(reversed(range(count))),tuple(range((len(stations)-1)*count,len(stations)*count))]
        for row in range(len(stations)-1):
            for i in range(count):
                j=(i+1)%count
                faces.append((row*count+i,row*count+j,(row+1)*count+j,(row+1)*count+i))
        obj=self.mesh('Formed body with individual bonnet and deck',vertices,faces,'Paint / body colour','01_Body')
        self.hard_bevel(obj,.035,3,True)
        for y in (d.front_axle,d.rear_axle):
            if y+d.radius+.07<self.hull_rear:
                continue
            for side in (-1,1):
                radius=d.radius+.037
                if 'square_flares' in d.features:
                    yz=[(y-radius*1.12,self.floor-.1),(y-radius*1.12,d.radius+.22),
                        (y-radius*.78,d.radius*2+.10),(y+radius*.78,d.radius*2+.10),
                        (y+radius*1.12,d.radius+.22),(y+radius*1.12,self.floor-.1)]
                    points=[(side*(self.half+depth),yy,z) for depth in (-.40,.35) for yy,z in yz]
                    n=len(yz)
                    fs=[tuple(reversed(range(n))),tuple(range(n,n*2))]
                    fs.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
                    cutter=self.mesh('Temporary squared wheel cut',points,fs,'Tyre / rubber')
                else:
                    bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=radius,depth=.75,
                        location=(side*(self.half-.015),y,d.radius),rotation=(0,math.pi/2,0))
                    cutter=bpy.context.object
                bpy.context.view_layer.objects.active=obj
                mod=obj.modifiers.new('Open wheel well','BOOLEAN')
                mod.operation='DIFFERENCE'
                mod.solver='EXACT'
                mod.object=cutter
                bpy.ops.object.modifier_apply(modifier=mod.name)
                bpy.data.objects.remove(cutter,do_unlink=True)
        self.smooth_hard_surface(obj)
        self.project_uv(obj.data)
        self.box('Chassis and lower suspension',(0,0,self.floor-.015),
                 (d.width*.70,d.length*.88,.11),'Tyre / rubber',.02,'01_Body')
        if d.family in ('delivery','flatbed','box_truck'):
            front=d.cabin[-1][0]+.12
            rear=d.rear+.08
            center=(front+rear)/2
            for side in (-1,1):
                self.box('Cargo chassis rail',(side*d.width*.30,center,d.radius*1.38),
                    (.13,front-rear,.24),'Grille / recess',.016,'01_Body')
                self.box('Cargo floor bearer',(side*d.width*.29,center,.88),
                    (.12,front-rear,.34),'Bumper / charcoal',.012,'01_Body')
                self.box('Fuel tank and battery enclosure',(side*(self.half-.16),d.rear_axle+.90,d.radius+.12),
                    (.31,.69,.34),'Bumper / charcoal',.04)

    def cabin(self):
        d=self.design
        n=len(d.cabin)
        vertices=[(side*self.cabin_x(z),y,z) for side in (-1,1) for y,z in d.cabin]
        faces=[tuple(reversed(range(n))),tuple(range(n,n*2))]
        faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
        obj=self.mesh('Individually shaped roof and pillars',vertices,faces,'Paint / body colour','01_Body')
        self.hard_bevel(obj,.04,4)
        self.smooth_hard_surface(obj)
        for side in (-1,1):
            for index,polygon in enumerate(d.panes):
                self.pane(f'Side window {side} {index+1}',
                          [(side*self.cabin_x(z),y,z) for y,z in polygon],(side,0,.2))
        self.end_glass('Windshield',d.cabin[0],d.cabin[1],1)
        if 'rear_double_doors' not in d.features:
            self.end_glass('Rear window',d.cabin[-1],d.cabin[-2],-1)

    def end_glass(self,name,bottom,top,end):
        y0,z0=bottom;y1,z1=top
        low,high=.095,.90
        za,zb=z0+(z1-z0)*low,z0+(z1-z0)*high
        ya,yb=y0+(y1-y0)*low,y0+(y1-y0)*high
        xa,xb=self.cabin_x(za)-.065,self.cabin_x(zb)-.065
        self.pane(name,[(-xa,ya,za),(xa,ya,za),(xb,yb,zb),(-xb,yb,zb)],(0,end,.5))
        if end>0:
            for x in (-self.design.width*.20,self.design.width*.18):
                self.line('Resting wiper',[(x-.17,ya+.018,za+.01),(x+.17,ya+.018,za+.01)],'Bumper / charcoal',.005)

    def wheels(self):
        d=self.design
        for axle,y in (('Front',d.front_axle),('Rear',d.rear_axle)):
            for side in (-1,1):
                label=axle+('_L' if side<0 else '_R')
                root=bpy.data.objects.new('Wheel_'+label,None)
                self.groups['03_Wheels'].objects.link(root)
                root.parent=self.root
                root.location=(side*(self.half-.11*self.scale),y,d.radius)
                root['radius_m']=d.radius
                root['steering_axle']=axle=='Front'
                for part in self.wheel_template['parts']:
                    points=[(-side*x*self.scale,yy*self.scale,z*self.scale) for x,yy,z in part['vertices']]
                    obj=self.mesh(part['name']+' '+label,points,part['faces'],part['material'],'03_Wheels',root)
                    for face,smooth in zip(obj.data.polygons,part['smooth']):
                        face.use_smooth=smooth
                    uv=obj.data.uv_layers.active
                    for loop,point in zip(uv.data,part['uv']):
                        loop.uv=point
                if not self.cab_only or axle=='Front':
                    self.fender(side,y,label)
                elif d.family in ('delivery','flatbed','box_truck'):
                    self.rear_truck_mudguard(side,y,label)

    def rear_truck_mudguard(self,side,y,label):
        """A rounded, physical guard mounted above the exposed rear tyre."""
        d=self.design
        points=[]
        for x,radius in ((self.half-.27,d.radius+.045),(self.half+.025,d.radius+.045),
                         (self.half+.025,d.radius+.09),(self.half-.27,d.radius+.09)):
            for i in range(29):
                angle=math.pi*i/28
                points.append((side*x,y+radius*math.cos(angle),d.radius+radius*math.sin(angle)))
        faces=[]
        for row in range(4):
            next_row=(row+1)%4
            faces.extend((row*29+i,row*29+i+1,next_row*29+i+1,next_row*29+i) for i in range(28))
        faces.extend(((0,29,58,87),(28,57,86,115)))
        obj=self.mesh('Rear truck mudguard '+label,points,faces,'Bumper / charcoal','01_Body')
        self.smooth_hard_surface(obj)

    def fender(self,side,y,label):
        d=self.design
        r=d.radius+.037
        if 'square_flares' in d.features:
            inner=[(y-r*1.12,self.floor+.02),(y-r*1.12,d.radius+.22),
                   (y-r*.78,d.radius*2+.10),(y+r*.78,d.radius*2+.10),
                   (y+r*1.12,d.radius+.22),(y+r*1.12,self.floor+.02)]
            outer=[(yy+(1 if yy>y else -1)*.075,z+.07) for yy,z in inner]
            points=[(side*(self.body_x(z,yy)+.033),yy,z) for contour in (inner,outer) for yy,z in contour]
            self.mesh('Squared protective arch '+label,points,
                      [(i,i+1,i+7,i+6) for i in range(5)],'Bumper / charcoal','01_Body')
        else:
            black='round_flares' in d.features
            points=[]
            for radial,offset in ((r,.002),(r+.008,.015),(r+(.055 if black else .027),.008)):
                for i in range(25):
                    angle=math.pi*i/24
                    yy=y+radial*math.cos(angle);z=d.radius+radial*math.sin(angle)
                    points.append((side*(self.body_x(z,yy)+offset),yy,z))
            obj=self.mesh('Rolled arch '+label,points,
                [(row*25+i,row*25+i+1,(row+1)*25+i+1,(row+1)*25+i) for row in range(2) for i in range(24)],
                'Bumper / charcoal' if black else 'Paint / body colour','01_Body')
            for face in obj.data.polygons:face.use_smooth=True
        # Dark inner closure is in front of the boolean's back wall.
        xx=side*(self.half-.34)
        points=[(xx,y+(r-.002)*math.cos(math.pi*i/24),d.radius+(r-.002)*math.sin(math.pi*i/24)) for i in range(25)]
        self.mesh('Inner arch back panel '+label,points,[tuple(range(25))],'Tyre / rubber','03_Wheels')

    def lens(self,name,x,y,z,width,height,material='Lamp / ivory',rear=False,sloped=False):
        obj=self.rounded_lens_volume(name,(x,y,z),
             ((width,height,.014,0),(width*.985,height*.98,.016,.006),
              (width*.91,height*.88,.020,.011)),material,.035)
        if rear:
            for vertex in obj.data.vertices:vertex.co.y=2*y-vertex.co.y
        if sloped:
            for vertex in obj.data.vertices:vertex.co.z+=(vertex.co.x-x)*(.10 if x>0 else -.10)
        bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
        return obj

    def fascia(self):
        d=self.design
        tall=d.family in ('flatbed','box_truck','utility')
        modern=d.family in ('fastback','crossover')
        slim='slim_lamps' in d.features
        height=.135 if slim else .21 if tall else .175
        width=d.width*(.205 if modern else .17 if slim else .145)
        z=d.nose-height*.64-.015
        bumper=max(d.radius*1.40,z-.27)
        self.box('Front wraparound bumper',(0,d.front+.035,bumper),
                 (d.width*1.025,.18,.23 if tall else .185),
                 'Paint / body colour' if modern else 'Bumper / charcoal',.068)
        self.box('Rear wraparound bumper',(0,d.rear-.03,bumper-.02),
                 (d.width*1.02,.17,.19),'Paint / body colour' if modern else 'Bumper / charcoal',.06)
        if modern:
            self.box('Lower integrated radiator intake',(0,d.front+.128,bumper-.004),
                (d.width*.65,.016,.096),'Grille / recess',.022)
            self.box('Rear lower protective insert',(0,d.rear-.119,bumper-.045),
                (d.width*.83,.019,.073),'Bumper / charcoal',.027)
            if d.family=='crossover':
                self.box('Front silver skid insert',(0,d.front+.14,bumper-.057),
                    (d.width*.37,.018,.052),'Wheel / hubcap',.015)
        self.box('Inset front grille',(0,d.front+.009,z),(d.width*.40,.025,height*1.04),'Grille / recess',.008)
        for index in range(4):
            zz=z-height*.37+index*height*.245
            self.box('Fine grille slat',(0,d.front+.027,zz),(d.width*.37,.013,.010),'Grille / bars',.003)
        self.box('Grille central divider',(0,d.front+.033,z),(.018,.014,height*.86),'Bumper / charcoal',.003)
        for side in (-1,1):
            x=side*d.width*.333
            self.box('Headlamp seal '+str(side),(x,d.front+.012,z),(width+.019,.020,height+.018),'Glass / rubber seal',.008)
            self.lens('Convex headlamp '+str(side),x,d.front+.024,z,width,height,sloped=slim)
            self.lens('Amber corner indicator '+str(side),side*d.width*.455,d.front+.016,z,
                      d.width*.044,height,'Lamp / amber')
        self.rear_lamps()
        plate_z=d.radius*1.65 if self.cab_only else bumper+.20
        self.box('Rear plate surround',(0,d.rear-.009,plate_z),(.34,.022,.105),'Bumper / charcoal',.007)
        self.box('Rear plain plate',(0,d.rear-.024,plate_z),(.308,.008,.078),'Wheel / hubcap',.002)
        if not self.cab_only:
            self.box('Boot release',(0,d.rear-.008,d.belt-.14),(.13,.03,.035),'Bumper / charcoal',.008)

    def rear_lamps(self):
        d=self.design
        vertical=('hatch' in d.features or d.family in ('panel_van','people_van')) and d.family!='fastback'
        z=d.radius*1.65 if self.cab_only else d.belt-.15
        if self.cab_only:
            self.box('Rear lamp mounting crossmember',(0,d.rear+.055,z),
                (d.width*.99,.11,.20),'Bumper / charcoal',.025)
        for side in (-1,1):
            w,h=(.115,.30) if vertical else (.32,.155)
            self.lens('Rear red lens '+str(side),side*(self.half-w*.50-.005),d.rear-.002,z,w,h,'Lamp / red',True)
            # The thin side return touches the rear lens around the body corner.
            self.box('Rear side lamp return '+str(side),(side*(self.half-.005),d.rear+.035,z),
                     (.024,.11,h),'Lamp / red',.009)

    def surface_point(self,side,y,z,offset=.004):
        x=self.cabin_x(z) if z>self.design.belt else self.body_x(z,y)
        return side*(x+offset),y,z

    def door(self,side,front,rear,index):
        d=self.design
        low=self.floor+.065
        points=[(rear,d.belt-.012),(rear,d.belt-.22),(rear,low),(front,low),(front,d.belt-.20),(front,d.belt-.012)]
        rounded=rounded_polygon([(0,y,z) for y,z in points],.035,3)
        path=[]
        for a,b in zip(rounded,rounded[1:]+rounded[:1]):
            steps=max(2,math.ceil((b-a).length/.035))
            for i in range(steps):
                point=a.lerp(b,i/steps);y,z=point.y,point.z
                for axle in (d.front_axle,d.rear_axle):
                    radius=d.radius+.05
                    if abs(z-d.radius)<radius and abs(y-axle)<radius:
                        delta=math.sqrt(radius*radius-(z-d.radius)**2)+.012
                        y=axle-delta if axle>0 else axle+delta
                path.append(self.surface_point(side,y,z))
        self.line(f'Door shut line {side} {index}',path+[path[0]],'Paint / panel gaps',.0016)
        y=rear+.14
        self.box('Door handle '+str(side)+' '+str(index),self.surface_point(side,y,d.belt-.12,.014),
                 (.026,.135,.039),'Bumper / charcoal',.009)

    def details(self):
        d=self.design
        for side in (-1,1):
            for index,(front,rear) in enumerate(d.doors):self.door(side,front,rear,index)
            y=d.doors[0][0]-.09
            z=d.belt+(.32 if 'truck_mirrors' in d.features else .15)
            xx=self.cabin_x(z)+.13
            self.box('Mirror support '+str(side),(side*(xx-.07),y,z-.025),(.18,.045,.042),'Bumper / charcoal',.012)
            height=.28 if 'truck_mirrors' in d.features else .135
            self.box('Rounded mirror housing '+str(side),(side*xx,y,z),(.145,.17,height),'Bumper / charcoal',.035)
            self.box('Mirror reflector '+str(side),(side*xx,y-.088,z),(.100,.006,height*.72),'Mirror / reflector',.002)
            if d.front-d.cabin[0][0]>.30:
                points=[]
                for step in range(6):
                    yy=d.cabin[0][0]+.06+(d.front-d.cabin[0][0]-.20)*step/5
                    points.append((side*self.hull_width(yy)*.91,yy,self.hull_top(yy)+.007))
                self.line('Bonnet edge '+str(side),points,'Paint / panel gaps',.0012)
        if 'roof_rack' in d.features:self.roof_rack()
        if 'spare' in d.features:self.spare()
        if 'taxi_sign' in d.features:self.taxi_sign()
        if 'cab_steps' in d.features:
            for side in (-1,1):
                step_y=d.front_axle-d.radius-.32
                for i in range(2):self.box('Cab step',(side*(self.half+.01),step_y,self.floor-.035+i*.12),
                    (.20,.46,.065),'Grille / bars',.02)
        if 'sliding_door' in d.features or 'sliding_cargo' in d.features:self.sliding_panel()
        if 'rear_double_doors' in d.features:
            def rear_surface(z):
                bottom,top=d.cabin[-1],d.cabin[-2]
                if z<=bottom[1]:return d.rear-.014
                return bottom[0]+(top[0]-bottom[0])*(z-bottom[1])/(top[1]-bottom[1])-.014
            self.line('Rear double door split',[(0,rear_surface(z),z) for z in
                (d.belt-.25,d.belt,d.height-.12)],'Paint / panel gaps',.0023)
            for x in (-.10,.10):self.box('Rear cargo latch',(x,rear_surface(d.belt+.05)-.01,d.belt+.05),
                (.055,.025,.16),'Bumper / charcoal',.007)

    def roof_rack(self):
        d=self.design
        front=d.cabin[1][0]-.15
        rear=d.cabin[-2][0]+.18
        for side in (-1,1):
            x=side*d.roof_half*.83
            for y in (front,rear):self.box('Roof rail foot',(x,y,d.height+.023),(.08,.13,.06),'Bumper / charcoal',.018)
            self.box('Roof side rail',(x,(front+rear)/2,d.height+.077),(.065,front-rear+.15,.058),'Bumper / charcoal',.025)
        for y in (front-.06,rear+.08):
            self.box('Roof rack crossbar',(0,y,d.height+.105),(d.roof_half*1.85,.065,.045),'Grille / bars',.018)

    def spare(self):
        d=self.design
        for name,radius,depth,material,offset in (
            ('Rear spare tyre cover',d.radius*.99,.22,'Tyre / rubber',.11),
            ('Spare cover face',d.radius*.83,.035,'Bumper / charcoal',.23)):
            bpy.ops.mesh.primitive_cylinder_add(vertices=40,radius=radius,depth=depth,
                location=(0,d.rear-offset,d.belt-.06),rotation=(math.pi/2,0,0))
            obj=bpy.context.object
            self.attach(obj,name,material,'04_Details')
            self.project_uv(obj.data)
            self.hard_bevel(obj,min(.025,depth*.42),3)
            self.smooth_hard_surface(obj)

    def taxi_sign(self):
        d=self.design
        self.box('Taxi roof sign',(0,-.10,d.height+.13),(.68,.22,.19),'Paint / body colour',.045)
        curve=bpy.data.curves.new('TAXI lettering','FONT')
        curve.body='TAXI';curve.align_x='CENTER';curve.size=.145;curve.extrude=.0007
        obj=bpy.data.objects.new('TAXI',curve)
        self.groups['04_Details'].objects.link(obj)
        obj.parent=self.root
        obj.location=(0,.017,d.height+.075)
        obj.rotation_euler=(math.pi/2,0,math.pi)
        curve.materials.append(self.materials['Bumper / charcoal'])

    def sliding_panel(self):
        d=self.design
        rear=d.rear+.15
        front=d.doors[0][1]-.10
        top=d.height-.15
        if 'sliding_cargo' in d.features:
            for side in (-1,1):
                path=[]
                for point in rounded_polygon([(0,front,d.belt+.03),(0,front,top),(0,rear,top),(0,rear,d.belt+.03)],.08,4):
                    path.append(self.surface_point(side,point.y,point.z,.007))
                self.line('Pressed cargo panel '+str(side),path+[path[0]],'Paint / panel gaps',.0018)
                self.box('Sliding cargo handle '+str(side),self.surface_point(side,front-.15,d.belt-.10,.015),
                         (.03,.15,.04),'Bumper / charcoal',.01)
        for side in (-1,1):
            self.line('Sliding door runner '+str(side),[self.surface_point(side,front-.06,d.belt-.22,.009),
                self.surface_point(side,rear,d.belt-.22,.009)],'Paint / panel gaps',.003)

    def cargo(self):
        d=self.design
        if 'open_bed' in d.features:
            front=d.cabin[-1][0]-.055
            rear=d.rear+.025
            base=d.belt-.16
            self.box('Open pickup bed floor',(0,(front+rear)/2,base),
                     (d.width*.88,front-rear,.07),'Paint / body colour',.025,'01_Body')
            for side in (-1,1):
                self.box('Pickup bed raised side',(side*(self.half-.045),(front+rear)/2,base+.14),
                         (.09,front-rear,.29),'Paint / body colour',.03,'01_Body')
                self.box('Rolled pickup bed rail',(side*(self.half-.045),(front+rear)/2,base+.295),
                         (.115,front-rear,.033),'Paint / body colour',.013)
            for y in (front,rear):self.box('Pickup bed end wall',(0,y,base+.14),(d.width*.98,.09,.29),'Paint / body colour',.025,'01_Body')
            for i in range(6):self.box('Pressed pickup bed rib',((i-2.5)*d.width*.13,(front+rear)/2,base+.041),
                (.025,front-rear-.15,.014),'Paint / body colour',.005)
        if 'open_flatbed' in d.features:
            front=d.cabin[-1][0]-.12;rear=d.rear+.035
            base=1.12
            self.box('Flatbed deck',(0,(front+rear)/2,base),(d.width,front-rear,.14),'Paint / body colour',.025,'01_Body')
            for side in (-1,1):
                for row in range(3):self.box('Flatbed horizontal board',(side*(self.half-.045),(front+rear)/2,base+.16+row*.16),
                    (.085,front-rear,.145),'Paint / body colour',.012,'01_Body')
                for i in range(4):self.box('Flatbed vertical stake',(side*(self.half+.005),rear+(front-rear)*i/3,base+.32),
                    (.052,.065,.64),'Paint / panel gaps',.01)
            for row in range(3):self.box('Flatbed tailboard',(0,rear,base+.16+row*.16),(d.width,.085,.145),'Paint / body colour',.012,'01_Body')
        if 'cargo_box' in d.features:
            front=d.cabin[-1][0]+.08
            bottom=1.05 if d.family=='delivery' else 1.10
            top=2.94 if d.family=='delivery' else 3.42
            width=d.width*(1.015 if d.family=='delivery' else 1.03)
            material='Cargo / pale panels' if d.family=='delivery' else 'Cargo / grey panels'
            self.box('Rigid enclosed cargo box',(0,(front+d.rear)/2,(top+bottom)/2),
                     (width,front-d.rear,top-bottom),material,.045,'01_Body')
            for side in (-1,1):
                self.box('Cargo box lower rail',(side*(width/2+.006),(front+d.rear)/2,bottom+.045),
                         (.028,front-d.rear,.075),'Wheel / rim edge',.008)
            y=d.rear-.032
            self.box('Cargo rear double door frame',(0,y,(top+bottom)/2),(width*.94,.025,top-bottom-.14),'Wheel / rim edge',.018)
            self.box('Cargo rear door panels',(0,y-.02,(top+bottom)/2),(width*.91,.024,top-bottom-.20),material,.015)
            self.line('Cargo box rear door split',[(0,y-.035,bottom+.12),(0,y-.035,top-.12)],'Grille / bars',.004)
            for x in (-.13,.13):self.box('Cargo door latch',(x,y-.06,bottom+.42),(.045,.035,.25),'Grille / bars',.008)
        if 'overcab_bunk' in d.features:
            self.camper_body()

    def camper_body(self):
        d=self.design
        front=.52;rear=d.rear+.035
        habitation=self.box('Camper habitation body',(0,(front+rear)/2,1.60),(d.width*1.01,front-rear,1.86),
                 'Cargo / cream panels',.075,'01_Body')
        self.cut_camper_wheel_wells(habitation)
        # An actual over-cab sleeping section, including the slanted front face.
        outline=((rear,2.50),(rear,2.96),(1.93,2.96),(2.26,2.67),(2.16,2.50))
        n=len(outline)
        points=[(side*d.width*.510,y,z) for side in (-1,1) for y,z in outline]
        faces=[tuple(reversed(range(n))),tuple(range(n,n*2))]
        faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
        obj=self.mesh('Over-cab sleeping loft',points,faces,'Cargo / cream panels','01_Body')
        self.hard_bevel(obj,.055,3);self.smooth_hard_surface(obj)
        for side in (-1,1):
            self.box('Camper terracotta side inset',(side*(d.width*.506+.003),(front+rear)/2,1.82),
                     (.018,front-rear-.16,1.12),'Paint / body colour',.007)
            lower=self.box('Camper terracotta lower skirt',(side*(d.width*.506+.003),(front+rear)/2,.915),
                     (.018,front-rear-.16,.35),'Paint / body colour',.007)
            self.cut_camper_wheel_wells(lower)
            for y,width in ((-.20,.58),(-1.82,.42)):
                self.pane('Camper living window '+str(side)+str(y),[(side*(d.width*.506+.017),y-width/2,1.55),
                    (side*(d.width*.506+.017),y+width/2,1.55),(side*(d.width*.506+.017),y+width/2,2.17),
                    (side*(d.width*.506+.017),y-width/2,2.17)],(side,0,0))
            self.pane('Overcab side window '+str(side),[(side*(d.width*.506+.012),1.05,2.64),
                (side*(d.width*.506+.012),1.48,2.64),(side*(d.width*.506+.012),1.48,2.85),
                (side*(d.width*.506+.012),1.05,2.85)],(side,0,0))
            y=-1.12
            self.line('Camper habitation door '+str(side),[(side*(d.width*.506+.024),y-.29,.78),
                (side*(d.width*.506+.024),y+.29,.78),(side*(d.width*.506+.024),y+.29,2.38),
                (side*(d.width*.506+.024),y-.29,2.38),(side*(d.width*.506+.024),y-.29,.78)],'Paint / panel gaps',.003)
        self.pane('Overcab front window',[(-.66,2.23,2.71),(.66,2.23,2.71),(.65,2.025,2.89),(-.65,2.025,2.89)],(0,1,.5))

    def cut_camper_wheel_wells(self,obj):
        """Keep the rear tyres clear of the low living-body skirt."""
        bpy.context.view_layer.objects.active=obj
        for modifier in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        d=self.design
        bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=d.radius+.055,depth=d.width*1.3,
            location=(0,d.rear_axle,d.radius),rotation=(0,math.pi/2,0))
        cutter=bpy.context.object
        bpy.context.view_layer.objects.active=obj
        modifier=obj.modifiers.new('Rear wheel clearance','BOOLEAN')
        modifier.operation='DIFFERENCE';modifier.solver='EXACT';modifier.object=cutter
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        bpy.data.objects.remove(cutter,do_unlink=True)
        self.project_uv(obj.data)
        self.smooth_hard_surface(obj)

    def build(self):
        self.hull()
        self.cabin()
        self.wheels()
        self.cargo()
        self.fascia()
        self.details()


def frame_camera(scene,root,position_direction=(-.64,.77,.17)):
    """Frame the actual model bounds; trucks keep their full roofs and mirrors."""
    from bpy_extras.object_utils import world_to_camera_view
    bpy.context.view_layer.update()
    deps=bpy.context.evaluated_depsgraph_get()
    points=[]
    for obj in root.children_recursive:
        if obj.type not in ('MESH','CURVE','FONT'):continue
        evaluated=obj.evaluated_get(deps)
        mesh=evaluated.to_mesh()
        world=[evaluated.matrix_world@vertex.co for vertex in mesh.vertices]
        if world:
            low=[min(point[i] for point in world) for i in range(3)]
            high=[max(point[i] for point in world) for i in range(3)]
            points.extend(Vector((x,y,z)) for x in (low[0],high[0])
                for y in (low[1],high[1]) for z in (low[2],high[2]))
        evaluated.to_mesh_clear()
    minimum=Vector(tuple(min(point[i] for point in points) for i in range(3)))
    maximum=Vector(tuple(max(point[i] for point in points) for i in range(3)))
    center=(minimum+maximum)/2
    span=max(maximum.x-minimum.x,maximum.y-minimum.y,(maximum.z-minimum.z)*1.5)
    camera=scene.camera
    camera.data.type='PERSP';camera.data.lens=73
    camera.data.shift_x=0;camera.data.shift_y=0
    camera.location=center+Vector(position_direction)*(span*2.25)
    aim(camera,center)
    for _ in range(3):
        projected=[world_to_camera_view(scene,camera,p) for p in points]
        left,right=min(p.x for p in projected),max(p.x for p in projected)
        bottom,top=min(p.y for p in projected),max(p.y for p in projected)
        camera.data.lens*=min(.88/(right-left),.82/(top-bottom))
        bpy.context.view_layer.update()
        projected=[world_to_camera_view(scene,camera,p) for p in points]
        camera.data.shift_x+=(min(p.x for p in projected)+max(p.x for p in projected))/2-.5
        camera.data.shift_y+=((min(p.y for p in projected)+max(p.y for p in projected))/2-.49)*scene.render.resolution_y/scene.render.resolution_x
    return [list(minimum),list(maximum)]
