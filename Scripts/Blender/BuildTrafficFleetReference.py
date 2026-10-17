"""Second fleet: individually measured silhouettes from the approved 5x4 reference.

The first source remains untouched. Generates ArtSource/TrafficFleet-v2, exports
there first, then the revision pipeline imports into the disposable Unity project.
X across, +Y forward, Z up; metres. No connection to an interactive Blender scene.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
from BuildTrafficFleet import Vehicle, DESIGNS, PALETTE, linear


# Belt, front/rear cabin bases, front/rear roof ends (fractions of length),
# window divisions from rear to front, actual door count per side.
PROFILES = {
    "hatch": (.86, .26, -.44, .10, -.29, (0,.40,1), 1),
    "sedan": (.88, .24, -.32, .105, -.22, (0,.46,1), 2),
    "wagon": (.92, .25, -.47, .11, -.41, (0,.29,.62,1), 2),
    "coupe": (.83, .23, -.29, .09, -.15, (0,.31,1), 1),
    "suv": (1.16, .25, -.45, .15, -.42, (0,.39,1), 2),
    "pickup": (1.00, .25, -.035, .16, -.015, (0,1), 1),
    "doublepickup": (1.07, .26, -.18, .15, -.15, (0,.45,1), 2),
    "taxi": (.92, .24, -.33, .105, -.24, (0,.48,1), 2),
    "executive": (.91, .23, -.33, .10, -.24, (0,.49,1), 2),
    "micro": (.91, .31, -.44, .14, -.29, (0,.35,1), 1),
    "crossover": (1.05, .26, -.46, .115, -.35, (0,.28,.61,1), 2),
    "fastback": (.93, .26, -.43, .11, -.16, (0,.21,.56,1), 2),
    "minivan": (1.02, .40, -.47, .22, -.44, (0,.30,.62,1), 3),
    "van": (1.15, .39, -.48, .27, -.45, (0,.73,1), 1),
    "minibus": (1.25, .42, -.48, .30, -.45, (0,.24,.49,.73,1), 3),
    "compactvan": (1.04, .29, -.47, .09, -.44, (0,.68,1), 1),
}


def mix(a, b, t):
    return a + (b-a)*t


class ReferenceVehicle(Vehicle):
    """Continuous sculpted panels and real wheel cut-outs, not stacked body boxes."""
    def mesh(self,name,points,faces,color):
        data=bpy.data.meshes.new(name);data.from_pydata(points,[],faces);data.update()
        # Recompute outside normals directly, without repeated global Edit Mode switches.
        bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
        obj=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(obj)
        return self.attach(obj,name,color)

    def panel(self, name, corners, color="Glass", chamfer=.035):
        corners = [Vector(p) for p in corners]
        polygon=[]
        for i, p in enumerate(corners):
            polygon.extend((p.lerp(corners[i-1],chamfer),p.lerp(corners[(i+1)%len(corners)],chamfer)))
        normal=(corners[1]-corners[0]).cross(corners[2]-corners[0]).normalized()
        n=len(polygon)
        points=polygon+[p-normal*.007 for p in polygon]
        faces=[tuple(range(n)),tuple(reversed(range(n,n*2)))]
        faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
        return self.mesh(name,points,faces,color)

    def body_hull(self, rear, front, belt, cabin_front=None):
        r=self.radius
        sill=.30 if r<.35 else .38 if r<.4 else .47
        stations={rear, rear+.10, front-.12, front}
        for axle in (-self.wheelbase/2,self.wheelbase/2):
            for i in range(11):
                angle=math.pi*i/10
                y=axle-math.cos(angle)*(r+.065)
                if rear+.12<y<front-.13:stations.add(y)
        if cabin_front is not None:stations.add(cabin_front)
        stations=sorted(stations)
        vertices=[]
        for y in stations:
            z=sill
            for axle in (-self.wheelbase/2,self.wheelbase/2):
                dy=y-axle
                if abs(dy)<r+.065:z=max(z,r+math.sqrt(max(0,(r+.065)**2-dy*dy)))
            top=belt
            if cabin_front is not None and y>cabin_front:
                top-=.115*(y-cabin_front)/(front-cabin_front)
            end=min(1,(y-rear)/.18,(front-y)/.20)
            half=self.width*.5*(.955+.045*max(0,end))
            shoulder=max(z+.025,top-.09)
            # The underside also follows the arch: no hidden full-width slab fills the wheel well.
            vertices.extend([(-half*.93,y,z),(half*.93,y,z),(half,y,shoulder),
                             (half*.97,y,top),(-half*.97,y,top),(-half,y,shoulder)])
        faces=[tuple(reversed(range(6))),tuple(range((len(stations)-1)*6,len(stations)*6))]
        for i in range(len(stations)-1):
            for j in range(6):faces.append((i*6+j,i*6+(j+1)%6,(i+1)*6+(j+1)%6,(i+1)*6+j))
        self.mesh("Continuous body with open wheel arches",vertices,faces,self.paint)

    def cabin_shape(self, rear, front, roof_rear, roof_front, belt, roof, divisions, doors,
                    cargo=False, wide=False, rear_glass=True):
        half=self.width*.485
        roof_half=self.width*(.455 if wide else .424)
        high=roof-.065
        # Lower belt, broad sloping panes, a narrow roof chamfer, then a slightly crowned roof.
        rings=[(belt,half,rear,front),(high,roof_half,roof_rear,roof_front),
               (roof,roof_half-.045,roof_rear+.055,roof_front-.055)]
        vertices=[]
        for z,x,back,nose in rings:vertices.extend([(-x,back,z),(x,back,z),(x,nose,z),(-x,nose,z)])
        faces=[(3,2,1,0),(8,9,10,11)]
        for i in range(2):
            for j in range(4):faces.append((i*4+j,i*4+(j+1)%4,(i+1)*4+(j+1)%4,(i+1)*4+j))
        self.mesh("Raked cabin with chamfered roof",vertices,faces,self.paint)
        def at(z):
            t=(z-belt)/(high-belt)
            return mix(half,roof_half,t),mix(rear,roof_rear,t),mix(front,roof_front,t)
        z0,z1=belt+.040,high-.035
        x0,back0,nose0=at(z0);x1,back1,nose1=at(z1)
        self.panel("Framed opaque windshield",[(-x0+.085,nose0+.010,z0),(x0-.085,nose0+.010,z0),
                                                    (x1-.085,nose1+.010,z1),(-x1+.085,nose1+.010,z1)])
        if rear_glass and not cargo:
            self.panel("Framed rear windshield",[(x0-.09,back0-.010,z0),(-x0+.09,back0-.010,z0),
                                                 (-x1+.09,back1-.010,z1),(x1-.09,back1-.010,z1)])
        for side in (-1,1):
            for i in range(len(divisions)-1):
                if cargo and i<len(divisions)-2:continue
                lo,hi=divisions[i],divisions[i+1]
                q=[(side*(x0+.010),mix(back0,nose0,lo)+.065,z0),
                   (side*(x0+.010),mix(back0,nose0,hi)-.065,z0),
                   (side*(x1+.010),mix(back1,nose1,hi)-.060,z1),
                   (side*(x1+.010),mix(back1,nose1,lo)+.060,z1)]
                self.panel(f"Side glass {side} pane {i}",q)
            # Thin seams stay below the belt, with painted pillars separating the glass above.
            if doors==1:
                door_break=divisions[-2] if len(divisions)>2 else 0
                bounds=[door_break,1]
            else:
                bounds=list(divisions[-(doors+1):])
            for i,t in enumerate(bounds):
                y=mix(rear,front,t)
                if abs(y-self.wheelbase/2)<self.radius+.10 or abs(y+self.wheelbase/2)<self.radius+.10:
                    continue
                self.box(f"Fine door shut line {side} {i}",(side*(self.width/2+.002),y,(belt+.38)/2),(.007,.009,belt-.38),"Trim")
            for i in range(len(bounds)-1):
                lo,hi=bounds[i],bounds[i+1]
                y=mix(rear,front,mix(lo,hi,.18))
                self.box(f"Door handle {side} {i}",(side*(self.width/2+.014),y,belt-.085),(.026,.145,.035),"Trim",.008)
        self.mirrors(front-.12,belt+.13)

    def mirrors(self,y,z):
        for side in (-1,1):
            self.box("Mirror stem",(side*(self.width/2+.035),y,z-.025),(.13,.035,.035),"Trim",.01)
            self.box("Beveled mirror",(side*(self.width/2+.105),y,z+.015),(.105,.155,.125),"Trim",.027)

    def wheel_mesh(self,label,side,y):
        r=self.radius;x=side*(self.width/2-.090)
        self.wheels.append({"name":label,"position":[x,r,y],"radius":r,"front":label.startswith("F")})
        count=16;vertices=[]
        for offset,rad in ((-.12,r*.86),(-.085,r),(.085,r),(.12,r*.86)):
            for j in range(count):
                a=2*math.pi*j/count
                vertices.append((x+offset,y+math.sin(a)*rad,r+math.cos(a)*rad))
        faces=[tuple(reversed(range(count))),tuple(range(count*3,count*4))]
        for ring in range(3):
            for j in range(count):faces.append((ring*count+j,ring*count+(j+1)%count,(ring+1)*count+(j+1)%count,(ring+1)*count+j))
        self.mesh("Wheel"+label+"_ShapedSidewall",vertices,faces,"Rubber")
        # Inset rim, raised rim lip and a modest hub: the tyre is no longer a flat black coin.
        for name,rad,depth,color,dx in (("Rim",r*.67,.021,"Rim",.121),("Hub",r*.28,.027,"Rim",.138)):
            bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=rad,depth=depth,
                location=(x+side*dx,y,r),rotation=(0,math.pi/2,0))
            if name=="Rim":
                modifier=bpy.context.object.modifiers.new("Rim edge chamfer","BEVEL");modifier.width=.009;modifier.segments=1
                bpy.ops.object.modifier_apply(modifier=modifier.name)
            self.attach(bpy.context.object,"Wheel"+label+"_"+name,color)
        # A dark inset inside the wheel well gives depth without drawing an oversized black arch outline.
        points=[]
        for radial in (r+.035,r+.065):
            for i in range(9):
                a=math.pi*i/8
                points.append((side*(self.width/2-.055),y+math.cos(a)*radial,r+math.sin(a)*radial))
        self.mesh("Wheel well inner edge "+label,points,[(i,i+1,i+10,i+9) for i in range(8)],"Rubber")
        if self.kind in ("suv","crossover","doublepickup"):
            flare=[]
            for radial in (r+.065,r+.12):
                for i in range(9):
                    a=math.pi*i/8
                    flare.append((side*(self.width/2+.015),y+math.cos(a)*radial,r+math.sin(a)*radial))
            self.mesh("Protective wheel flare "+label,flare,[(i,i+1,i+10,i+9) for i in range(8)],"Trim")

    def running_gear(self):
        for front,y in ((True,self.wheelbase/2),(False,-self.wheelbase/2)):
            for side in (-1,1):self.wheel_mesh(("F" if front else "R")+("L" if side<0 else "R"),side,y)

    def face_details(self,belt,front=None,rear=None,cabover=False):
        w,l=self.width,self.length
        front=l/2 if front is None else front
        rear=-l/2 if rear is None else rear
        bumper_z=.46 if self.radius<.35 else .52 if self.radius<.40 else .58
        for end,y in ((1,front),(-1,rear)):
            self.box("Moulded bumper",(0,y+end*.035,bumper_z),(w*1.015,.15,.155),"Trim",.045)
            lamp_z=bumper_z+.22 if not cabover or end<0 else bumper_z+.15
            if end>0 and not cabover:lamp_z=min(lamp_z,belt-.205)
            lamp_width=w*.235;lamp_height=.145
            if self.kind=="suv":lamp_width=w*.145;lamp_height=.205;lamp_z=.82
            if cabover:lamp_width=w*.175;lamp_height=.20
            for side in (-1,1):
                xx=side*w*.325
                self.box("Lamp recess",(xx,y+end*.022,lamp_z),(lamp_width+.035,.035,lamp_height+.035),"Trim",.012)
                if end>0 and self.kind in ("fastback","crossover","coupe"):
                    x0,x1=xx-lamp_width/2,xx+lamp_width/2
                    self.panel("Swept front headlamp",[(x0,y+.047,lamp_z-lamp_height/2),(x1,y+.047,lamp_z-lamp_height/2+.025),
                        (x1,y+.047,lamp_z+lamp_height/2),(x0+.055,y+.047,lamp_z+lamp_height/2)],"Lamp",.025)
                else:self.box("Headlamp" if end>0 else "Tail lamp",(xx,y+end*.046,lamp_z),(lamp_width,.024,lamp_height),"Lamp" if end>0 else "Tail",.012)
                self.box("Corner turn signal",(side*w*.455,y+end*.040,lamp_z),(.075,.029,lamp_height),"Indicator",.009)
            self.box("Number plate",(0,y+end*.117,bumper_z+.005),(.31,.012,.085),"Rim",.008)
        grille_z=min(bumper_z+.22,belt-.205) if not cabover else bumper_z+.43
        if self.kind=="suv":grille_z=.82
        self.box("Inset radiator grille",(0,front+.033,grille_z),(w*.39,.035,.175 if not cabover else .21),"Trim",.015)
        if self.kind in ("fastback","crossover"):
            self.box("Body colour bumper chin",(0,front+.012,bumper_z-.065),(w*.94,.13,.17),self.paint,.055)
            self.box("Lower sculpted air intake",(0,front+.085,bumper_z-.045),(w*.57,.022,.10),"Trim",.025)
        if self.kind in ("minivan","van","compactvan","minibus"):
            self.box("Lower air intake",(0,front+.052,bumper_z+.055),(w*.41,.024,.075),"Rubber",.012)

    def rack(self):
        w,l,h=self.width,self.length,self.height
        for side in (-1,1):
            for y in (-l*.31,l*.06):self.box("Roof rail foot",(side*w*.33,y,h+.035),(.075,.10,.07),"Trim",.012)
            self.box("Roof rail",(side*w*.33,-l*.12,h+.09),(.06,l*.48,.055),"Trim",.022)
        for y in (-l*.28,l*.035):self.box("Roof crossbar",(0,y,h+.12),(w*.72,.06,.048),"Trim",.015)

    def pickup_bed(self,rear_cabin,belt):
        w,l=self.width,self.length
        back=-l/2+.035;front=rear_cabin-.035;length=front-back
        self.body_hull(back,front,belt-.12)
        self.box("Recessed cargo bed floor",(0,(back+front)/2,belt-.10),(w*.86,length-.08,.07),self.paint,.02)
        for side in (-1,1):
            self.box("Cargo bed side",(side*(w/2-.055),(back+front)/2,belt+.085),(.11,length,.43),self.paint,.04)
            self.box("Cargo bed rim",(side*(w/2-.055),(back+front)/2,belt+.31),(.13,length,.035),self.paint,.012)
        self.box("Tailgate",(0,back,belt+.085),(w,.10,.43),self.paint,.04)
        self.box("Bed forward wall",(0,front,belt+.085),(w,.07,.43),self.paint,.02)
        self.box("Tailgate handle",(0,back-.056,belt+.20),(.21,.018,.038),"Trim",.006)

    def cargo_panel(self,rear,front,belt,roof):
        for side in (-1,1):
            self.box("Recessed solid cargo panel",(side*(self.width*.475),mix(rear,front,.37),mix(belt,roof,.51)),
                     (.025,(front-rear)*.59,(roof-belt)*.70),self.paint,.05)
            self.box("Cargo door track",(side*(self.width/2+.006),mix(rear,front,.36),belt-.045),(.016,(front-rear)*.58,.012),"Trim")

    def truck(self):
        w,l,h=self.width,self.length,self.height
        cab_rear=l*.10
        belt=1.33 if self.kind=="delivery" else 1.45
        cab_height=2.18 if self.kind=="delivery" else 2.44 if self.kind=="boxtruck" else h
        cargo_width=w
        self.box("Truck chassis",(0,0,.64),(w*.61,l*.93,.22),"Trim",.035)
        self.width=w*.89
        self.body_hull(cab_rear,l/2,belt,l*.47)
        self.cabin_shape(cab_rear,l*.484,cab_rear+.09,l*.434,belt,cab_height,(0,1),1,wide=True,rear_glass=False)
        self.face_details(belt,cabover=True)
        self.width=w
        back=-l/2+.035;front=cab_rear-.10;length=front-back
        if self.kind=="flatbed":
            self.box("Cargo bed platform",(0,(front+back)/2,1.09),(cargo_width,length,.14),"Olive",.03)
            for side in (-1,1):
                for z in (1.29,1.53,1.77):self.box("Open bed horizontal side panel",(side*(w/2-.04),(front+back)/2,z),(.08,length,.205),"Olive",.015)
                for y in (back+.08,mix(back,front,.34),mix(back,front,.67),front-.08):
                    self.box("Bed side stake",(side*(w/2+.005),y,1.53),(.08,.07,.88),"Olive",.01)
            self.box("Rear cargo tailboard",(0,back,1.54),(w,.09,.85),"Olive",.025)
        else:
            color="White" if self.kind=="delivery" else "Cargo"
            self.box("Box body with edge chamfer",(0,(back+front)/2,(1.02+h)/2),(w,length,h-1.02),color,.035)
            for side in (-1,1):self.box("Cargo lower frame",(side*(w/2-.025),(back+front)/2,1.03),(.055,length,.075),"Rim")
            self.box("Rear door central seam",(0,back-.041,(h+1.02)/2),(.014,.018,h-1.14),"Trim")
            for side in (-1,1):self.box("Cargo door locking bar",(side*w*.22,back-.06,(h+1.08)/2),(.027,.025,h-1.28),"Rim")
        self.running_gear()

    def camper(self):
        w,l,h=self.width,self.length,self.height
        belt=1.02;rear=.15*l;front=.43*l
        self.box("Camper chassis",(0,0,.55),(w*.63,l*.94,.20),"Trim",.03)
        self.width=w*.90
        self.body_hull(rear,l/2,belt,front)
        self.cabin_shape(rear,front,rear+.03,.30*l,belt,2.05,(0,1),1,wide=True,rear_glass=False)
        self.face_details(belt)
        self.width=w
        back=-l/2+.03;end=rear-.04;length=end-back
        original_paint=self.paint;self.paint="Cream"
        self.body_hull(back,end,1.10)
        self.paint=original_paint
        self.box("Cream camper accommodation",(0,(back+end)/2,1.92),(w,length,1.98),"Cream",.065)
        self.box("Camper overcab sleeping extension",(0,.18*l,h-.28),(w,l*.57,.56),"Cream",.10)
        pod_front=.18*l+l*.285
        self.panel("Overcab front window",[(-.59,pod_front+.005,h-.43),(.59,pod_front+.005,h-.43),(.54,pod_front+.005,h-.16),(-.54,pod_front+.005,h-.16)])
        for side in (-1,1):
            self.box("Terracotta camper side inset",(side*(w/2+.006),(back+end)/2,1.78),(.018,length-.15,1.33),"Terracotta",.04)
            for y,width in ((-1.68,.57),(-.45,.67)):
                x=side*(w/2+.023)
                self.panel("Camper side opaque window",[(x,y-width/2,1.87),(x,y+width/2,1.87),(x,y+width/2,2.36),(x,y-width/2,2.36)],chamfer=.025)
            self.box("Camper side door seam",(side*(w/2+.019),-.08,1.45),(.009,.010,.94),"Trim")
            self.box("Camper door handle",(side*(w/2+.029),-.15,1.59),(.025,.10,.033),"Trim")
        self.box("Camper roof hatch",(0,-1.1,h+.025),(.60,.73,.055),"Cream",.025)
        self.running_gear()

    def build(self):
        w,l,h=self.width,self.length,self.height
        if self.kind in ("flatbed","boxtruck","delivery"):
            self.truck();return
        if self.kind=="camper":
            self.camper();return
        belt,front,rear,roof_front,roof_rear,divisions,doors=PROFILES[self.kind]
        front*=l;rear*=l;roof_front*=l;roof_rear*=l
        cargo=self.kind in ("van","compactvan")
        wide=self.kind in ("minivan","minibus","van","compactvan","suv")
        self.box("Recessed underbody",(0,0,.37 if self.radius<.35 else .46),(w*.63,l*.91,.16),"Trim",.025)
        if self.kind in ("pickup","doublepickup"):
            # Cabin and load bed are independent volumes, not a flat slab through the open bed.
            self.body_hull(rear,l/2,belt,front)
            self.pickup_bed(rear,belt)
        else:self.body_hull(-l/2,l/2,belt,front)
        self.cabin_shape(rear,front,roof_rear,roof_front,belt,h,divisions,doors,cargo,wide)
        self.face_details(belt)
        if self.kind in ("wagon","crossover"):self.rack()
        if self.kind=="suv":
            bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=.37,depth=.24,location=(-.32,-l/2-.18,1.17),rotation=(math.pi/2,0,0))
            self.attach(bpy.context.object,"Rear mounted spare tyre","Rubber")
            bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=.24,depth=.025,location=(-.32,-l/2-.31,1.17),rotation=(math.pi/2,0,0))
            self.attach(bpy.context.object,"Rear spare wheel centre","Rim")
        if cargo:self.cargo_panel(rear,front,belt,h)
        if self.kind=="taxi":
            self.box("TAXI roof light",(0,-.08,h+.105),(.48,.24,.17),"Yellow",.035)
            bpy.ops.object.text_add(location=(0,.046,h+.05),rotation=(math.pi/2,0,math.pi))
            obj=bpy.context.object;obj.data.body="TAXI";obj.data.align_x="CENTER";obj.data.size=.115
            bpy.ops.object.convert(target="MESH");self.attach(bpy.context.object,"TAXI lettering","Trim")
        self.running_gear()


def main():
    parser=argparse.ArgumentParser();parser.add_argument("--project",required=True)
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:]);project=Path(args.project)
    source=project/"ArtSource/TrafficFleet-v2";export=source/"Exports"
    source.mkdir(parents=True,exist_ok=True);export.mkdir(exist_ok=True)
    destination=source/"TrafficFleet-v2.blend"
    if destination.exists():raise RuntimeError("Refusing to overwrite editable revision; preserve it before another generation.")
    bpy.ops.object.select_all(action="SELECT");bpy.ops.object.delete(use_global=False)
    scene=bpy.context.scene;scene.name="Reference fleet v2 - editable models";scene.unit_settings.system="METRIC"
    materials={};palette=dict(PALETTE);palette["Glass"]=(.15,.205,.26);palette["Rubber"]=(.14,.15,.16);palette["Rim"]=(.54,.57,.58)
    for name,color in palette.items():
        material=bpy.data.materials.new(name);material.diffuse_color=(*[linear(v) for v in color],1)
        material.use_nodes=True;bsdf=material.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value=material.diffuse_color
        bsdf.inputs["Roughness"].default_value=.43 if name=="Glass" else .78
        materials[name]=material
    vehicles=[];report=[]
    for design in DESIGNS:
        car=ReferenceVehicle(design,materials);car.build();report.append(car.export(export));vehicles.append(car)
    for i,car in enumerate(vehicles):
        offset=Vector(((i%5)*7.5,-(i//5)*9,0))
        for obj in car.parts:obj.location+=offset
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            area.spaces.active.shading.color_type="MATERIAL";area.spaces.active.region_3d.view_distance=42
            area.spaces.active.region_3d.view_location=(15,-13,0)
    bpy.ops.wm.save_as_mainfile(filepath=str(destination))
    (source/"fleet-report.json").write_text(json.dumps({"revision":2,"palette":palette,"vehicles":report},indent=2),encoding="utf-8")
    print("REFERENCE FLEET PASS:",len(report),"vehicles;",sum(c["triangles"] for c in report),"triangles")


if __name__=="__main__":main()
