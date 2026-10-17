"""Author the approved 20-car low-poly fleet without touching interactive Blender.

Blender: X across, +Y forward, Z up, metres. Named separate wheel parts survive
FBX export; Unity bakes the static shell and four independently animated wheels.
Run: blender --background --factory-startup --python THIS -- --project PROJECT.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


PALETTE = {
    "Yellow": (.93, .69, .12), "Brick": (.68, .24, .16),
    "Green": (.30, .43, .24), "Blue": (.25, .43, .57),
    "Beige": (.77, .70, .52), "Orange": (.88, .38, .12),
    "Cream": (.88, .83, .67), "Teal": (.16, .53, .52),
    "White": (.88, .88, .84), "Olive": (.43, .46, .24),
    "Red": (.72, .24, .16), "Cargo": (.64, .66, .65),
    "Sky": (.43, .59, .70), "Burgundy": (.39, .18, .21),
    "Mint": (.53, .71, .59), "Navy": (.19, .29, .43),
    "Terracotta": (.69, .34, .21), "Silver": (.61, .65, .67),
    "Glass": (.055, .085, .115), "Rubber": (.085, .09, .09),
    "Trim": (.17, .20, .20), "Rim": (.43, .47, .46),
    "Lamp": (.96, .92, .71), "Tail": (.65, .08, .05),
    "Indicator": (.95, .49, .08),
}

# ID, body family, paint, width, length, roof height, wheelbase, radius.
DESIGNS = [
    ("01_Hatchback", "hatch", "Yellow", 1.66, 3.65, 1.49, 2.30, .30),
    ("02_ClassicSedan", "sedan", "Brick", 1.74, 4.40, 1.45, 2.58, .32),
    ("03_StationWagon", "wagon", "Green", 1.78, 4.55, 1.58, 2.62, .32),
    ("04_Coupe", "coupe", "Blue", 1.77, 4.32, 1.32, 2.53, .31),
    ("05_BoxSUV", "suv", "Beige", 1.86, 4.12, 1.96, 2.48, .39),
    ("06_Pickup", "pickup", "Orange", 1.84, 4.75, 1.66, 2.85, .35),
    ("07_Minivan", "minivan", "Cream", 1.85, 4.65, 1.93, 2.78, .33),
    ("08_PanelVan", "van", "Teal", 1.94, 4.95, 2.16, 2.97, .35),
    ("09_DeliveryVan", "delivery", "White", 2.06, 5.50, 2.82, 3.24, .37),
    ("10_Taxi", "taxi", "Yellow", 1.83, 4.85, 1.55, 2.91, .33),
    ("11_Minibus", "minibus", "Yellow", 2.03, 5.70, 2.49, 3.45, .37),
    ("12_FlatbedTruck", "flatbed", "Olive", 2.22, 6.10, 2.57, 3.60, .43),
    ("13_BoxTruck", "boxtruck", "Red", 2.27, 6.55, 3.22, 3.91, .44),
    ("14_Crossover", "crossover", "Sky", 1.86, 4.43, 1.77, 2.67, .36),
    ("15_ExecutiveSedan", "executive", "Burgundy", 1.89, 5.04, 1.48, 3.00, .34),
    ("16_Microcar", "micro", "Mint", 1.53, 2.96, 1.59, 1.91, .28),
    ("17_CompactCargoVan", "compactvan", "Cream", 1.73, 4.03, 1.94, 2.46, .31),
    ("18_DoubleCabPickup", "doublepickup", "Navy", 1.94, 5.24, 1.82, 3.16, .37),
    ("19_SmallCamper", "camper", "Terracotta", 2.11, 5.73, 2.95, 3.38, .38),
    ("20_Fastback", "fastback", "Silver", 1.83, 4.67, 1.49, 2.77, .33),
]


def linear(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4


class Vehicle:
    def __init__(self, design, materials):
        self.id, self.kind, self.paint, self.width, self.length, self.height, self.wheelbase, self.radius = design
        self.materials = materials
        self.collection = bpy.data.collections.new(self.id)
        bpy.context.scene.collection.children.link(self.collection)
        self.parts = []
        self.wheels = []

    def attach(self, obj, name, color):
        obj.name = name
        for old in tuple(obj.users_collection):
            old.objects.unlink(obj)
        self.collection.objects.link(obj)
        obj.data.materials.append(self.materials[color])
        for polygon in obj.data.polygons:
            polygon.use_smooth = False
        self.parts.append(obj)
        return obj

    def box(self, name, pos, size, color, bevel=0):
        bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
        obj = bpy.context.object
        obj.dimensions = size
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        if bevel:
            modifier = obj.modifiers.new("Single low-poly chamfer", "BEVEL")
            modifier.width = bevel
            modifier.segments = 1
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        return self.attach(obj, name, color)

    def mesh(self, name, points, faces, color):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(points, [], faces)
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        self.attach(obj, name, color)
        if len(faces) > 1:
            bpy.ops.object.select_all(action="DESELECT")
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.select_all(action="SELECT")
            bpy.ops.mesh.normals_make_consistent(inside=False)
            bpy.ops.object.mode_set(mode="OBJECT")
        return obj

    def pane(self, name, points):
        # Closed glass with a 12 mm back face prevents accidental inward pane winding.
        center = sum((Vector(p) for p in points), Vector()) / len(points)
        normal = (Vector(points[1])-Vector(points[0])).cross(Vector(points[2])-Vector(points[0])).normalized()
        if normal.dot(center - Vector((0, 0, self.height*.55))) < 0:
            points = list(reversed(points))
            normal = -normal
        inside = [Vector(p)-normal*.012 for p in points]
        vertices = list(points) + inside
        n = len(points)
        faces = [tuple(range(n)), tuple(reversed(range(n, n*2)))]
        faces += [(i, (i+1)%n, (i+1)%n+n, i+n) for i in range(n)]
        self.mesh(name, vertices, faces, "Glass")

    def cabin(self, rear, front, roof_rear, roof_front, bottom, top, windows=2, cargo=False, roof_width=.87):
        half = self.width/2
        upper = half*roof_width
        points = [(-half,rear,bottom),(half,rear,bottom),(half,front,bottom),(-half,front,bottom),
                  (-upper,roof_rear,top),(upper,roof_rear,top),(upper,roof_front,top),(-upper,roof_front,top)]
        self.mesh("Faceted cabin shell", points, [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)], self.paint)
        z0 = bottom + .12
        z1 = top - .12
        def interpolate(a,b,z):
            return a+(b-a)*(z-bottom)/(top-bottom)
        y0,y1 = interpolate(front,roof_front,z0)+.015,interpolate(front,roof_front,z1)+.015
        x0,x1 = interpolate(half,upper,z0)-.10,interpolate(half,upper,z1)-.10
        self.pane("Opaque windshield", [(-x0,y0,z0),(x0,y0,z0),(x1,y1,z1),(-x1,y1,z1)])
        if not cargo:
            y0,y1 = interpolate(rear,roof_rear,z0)-.015,interpolate(rear,roof_rear,z1)-.015
            self.pane("Opaque rear glass", [(x0,y0,z0),(-x0,y0,z0),(-x1,y1,z1),(x1,y1,z1)])
        for side in (-1,1):
            # Window corners lie on the sloping side wall, not axis-aligned over it.
            for i in range(windows):
                a,b = i/windows, (i+1)/windows
                r0 = interpolate(rear,roof_rear,z0)+.12
                f0 = interpolate(front,roof_front,z0)-.12
                r1 = interpolate(rear,roof_rear,z1)+.10
                f1 = interpolate(front,roof_front,z1)-.10
                if cargo and i == 0 and windows > 1:
                    continue
                xx0,xx1 = side*(interpolate(half,upper,z0)+.015),side*(interpolate(half,upper,z1)+.015)
                self.pane(f"Opaque side glass {side} {i}", [(xx0,r0+(f0-r0)*a+.035,z0),(xx0,r0+(f0-r0)*b-.035,z0),
                          (xx1,r1+(f1-r1)*b-.035,z1),(xx1,r1+(f1-r1)*a+.035,z1)])
            for i in range(windows):
                if cargo and i == 0:
                    continue
                y = rear+(front-rear)*(i+.45)/windows
                self.box(f"Door handle {side} {i}", (side*(half+.015),y,bottom-.10),(.035,.17,.045),"Trim")
                self.box(f"Door seam {side} {i}",(side*(half+.012),rear+(front-rear)*i/windows,bottom-.24),(.014,.015,.43),"Trim")

    def running_gear(self):
        for front, y in ((True,self.wheelbase/2),(False,-self.wheelbase/2)):
            for side in (-1,1):
                label = ("F" if front else "R")+("L" if side == -1 else "R")
                x = side*(self.width/2-.045)
                self.wheels.append({"name":label,"position":[x,self.radius,y],"radius":self.radius,"front":front})
                for suffix, radius, depth, color, dx in (("Tire",self.radius,.21,"Rubber",0),("Rim",self.radius*.62,.025,"Rim",side*.115),
                                                          ("Hub",self.radius*.23,.032,"Trim",side*.134)):
                    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=radius, depth=depth,
                        location=(x+dx,y,self.radius), rotation=(0,math.pi/2,0))
                    self.attach(bpy.context.object,"Wheel"+label+"_"+suffix,color)
                # A thin arch trim follows the upper tire without covering it with a solid square.
                points=[]
                for radius in (self.radius+ .035,self.radius+.085):
                    for step in range(7):
                        angle=math.pi*step/6
                        points.append((side*(self.width/2+.012),y+math.cos(angle)*radius,self.radius+math.sin(angle)*radius))
                faces=[(i,i+1,i+8,i+7) for i in range(6)]
                # Duplicate back faces because these trim ribbons have no enclosed volume.
                faces += [tuple(reversed(face)) for face in faces[:]]
                self.mesh("Wheel arch "+label,points,faces,"Trim")

    def build(self):
        w,l,h,r = self.width,self.length,self.height,self.radius
        bottom = r+.17
        belt = max(.93,r*2+.24)
        self.box("Underbody",(0,0,r*.95),(w*.72,l*.85,.20),"Trim",.035)
        self.box("Chamfered body",(0,0,(bottom+belt)/2),(w,l,belt-bottom),self.paint,.055)
        kind=self.kind
        car = kind in ("hatch","sedan","wagon","coupe","taxi","executive","micro","crossover","fastback","suv")
        if car:
            rear = -l*.40 if kind in ("hatch","micro","wagon","suv","crossover") else -l*.27
            front=l*.25
            roof_rear=rear+.17 if kind in ("wagon","suv") else rear+.40
            roof_front=front-.46 if kind not in ("micro","suv") else front-.20
            if kind == "fastback": rear=-l*.44;roof_rear=-l*.15
            self.cabin(rear,front,roof_rear,roof_front,belt,h,1 if kind in ("coupe","micro","hatch") else 3 if kind=="wagon" else 2)
            self.box("Sloped hood",(0,(front+l/2)/2,belt+.015),(w*.96,l/2-front,.09),self.paint,.035)
            if kind in ("sedan","executive","coupe","taxi"):
                self.box("Trunk deck",(0,(-l/2+rear)/2,belt+.015),(w*.97,rear+l/2,.08),self.paint,.025)
            if kind in ("wagon","crossover"):
                for side in (-1,1):self.box("Roof rail",(side*w*.33,-.20,h+.075),(.055,l*.45,.07),"Trim")
                for y in (-l*.19,l*.09):self.box("Roof crossbar",(0,y,h+.11),(w*.74,.06,.055),"Trim")
            if kind=="taxi":
                self.box("Taxi roof sign",(0,-.10,h+.15),(.57,.25,.23),"Yellow",.03)
                # Mesh lettering remains editable in the saved file and exports without a font dependency.
                bpy.ops.object.text_add(location=(0,.034,h+.09),rotation=(math.pi/2,0,math.pi))
                text=bpy.context.object;text.data.body="TAXI";text.data.align_x="CENTER";text.data.size=.13
                bpy.ops.object.convert(target="MESH");self.attach(bpy.context.object,"Taxi lettering","Trim")
            if kind=="suv":
                bpy.ops.mesh.primitive_cylinder_add(vertices=10,radius=.36,depth=.22,location=(.35,-l/2-.12,1.13),rotation=(math.pi/2,0,0))
                self.attach(bpy.context.object,"Rear mounted spare tire","Rubber")
        elif kind in ("pickup","doublepickup"):
            back=-.12 if kind=="pickup" else -.76
            self.cabin(back,l*.30,back+.10,l*.21,belt,h,1 if kind=="pickup" else 2)
            bedfront=back-.08;bedrear=-l/2+.10;bedlength=bedfront-bedrear
            self.box("Open bed floor",(0,(bedfront+bedrear)/2,belt-.06),(w*.90,bedlength,.09),"Trim")
            for side in (-1,1):self.box("Bed side",(side*(w/2-.06),(bedfront+bedrear)/2,belt+.19),(.12,bedlength,.46),self.paint,.025)
            self.box("Tailgate",(0,bedrear,belt+.19),(w,.13,.46),self.paint,.025)
        elif kind in ("van","minivan","minibus","compactvan"):
            iscompact=kind=="compactvan"
            self.cabin(-l*.46,l*(.18 if iscompact else .43),-l*.43,l*(.07 if iscompact else .30),belt,h,
                       2 if iscompact else 4 if kind=="minibus" else 3,cargo=kind in ("van","compactvan"),roof_width=.94)
            if kind=="van":
                # Rear cargo section is solid paint; side panes are only the two cab doors.
                for obj in list(self.parts):
                    if obj.name.startswith("Opaque side glass") and not obj.name.endswith(" 2"):
                        self.parts.remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
            for side in (-1,1):self.box("Sliding door track",(side*(w/2+.02),-.30,belt-.11),(.025,l*.40,.035),"Trim")
        else:
            cabrear=l*.10
            cabtop=min(h,2.25 if kind=="camper" else 2.40)
            self.cabin(cabrear,l*.47,cabrear+.03,l*.34,belt,cabtop,1,roof_width=.94)
            cargo_end=cabrear-.08;cargo_start=-l/2+.08;cargo_length=cargo_end-cargo_start
            if kind=="flatbed":
                self.box("Cargo bed",(0,(cargo_end+cargo_start)/2,1.0),(w,cargo_length,.16),"Olive")
                for side in (-1,1):
                    for level in range(3):self.box("Bed plank",(side*(w/2-.06),(cargo_end+cargo_start)/2,1.23+level*.19),(.10,cargo_length,.16),"Olive")
                    for y in (cargo_start+.08,(cargo_end+cargo_start)/2,cargo_end-.08):
                        self.box("Bed stake",(side*w/2,y,1.43),(.10,.09,.81),"Rim")
                self.box("Bed tailboard",(0,cargo_start,1.43),(w,.10,.73),"Olive")
            else:
                color="Cream" if kind=="camper" else "White" if kind=="delivery" else "Cargo"
                floor=1.03
                self.box("Cargo accommodation shell",(0,(cargo_end+cargo_start)/2,(h+floor)/2),(w,cargo_length,h-floor),color,.055)
                if kind=="camper":
                    self.box("Overcab sleeping pod",(0,l*.20,h-.28),(w,l*.53,.56),"Cream",.07)
                    self.pane("Overcab front glass",[(-.58,l*.465+.01,h-.39),(.58,l*.465+.01,h-.39),(.54,l*.465+.01,h-.16),(-.54,l*.465+.01,h-.16)])
                    for side in (-1,1):
                        self.box("Camper painted stripe",(side*(w/2+.01),(cargo_end+cargo_start)/2,1.82),(.02,cargo_length-.10,.70),"Terracotta")
                        for y in (-1.72,-.57):self.box("Opaque camper glass",(side*(w/2+.025),y,2.14),(.025,.61,.56),"Glass")
                else:
                    self.box("Rear cargo doors",(0,cargo_start-.015,(floor+h)/2),(w-.18,.025,h-floor-.12),color)
                    self.box("Rear door split",(0,cargo_start-.035,(floor+h)/2),(.025,.025,h-floor-.16),"Trim")
                    for side in (-1,1):self.box("Rear cargo lock",(side*.28,cargo_start-.052,1.50),(.035,.03,.48),"Rim")
        for end in (-1,1):
            y=end*(l/2+.025)
            self.box("Bumper",(0,y,bottom+.08),(w+.05,.16,.18),"Trim",.025)
            for side in (-1,1):
                self.box("Headlamp" if end==1 else "Tail lamp",(side*w*.32,y+.018*end,belt-.13),(w*.22,.055,.18),"Lamp" if end==1 else "Tail")
                self.box("Turn indicator",(side*w*.45,y+.02*end,belt-.13),(.10,.06,.18),"Indicator")
            self.box("License plate",(0,y+.09*end,bottom+.09),(.35,.018,.10),"Rim")
        self.box("Front grille",(0,l/2+.052,belt-.15),(w*.36,.04,.18),"Trim")
        for side in (-1,1):
            self.box("Mirror arm",(side*(w/2+.08),l*.23,belt+.12),(.18,.055,.055),"Trim")
            self.box("Mirror",(side*(w/2+.17),l*.23,belt+.18),(.11,.18,.20),"Trim",.025)
        self.running_gear()

    def export(self, directory):
        bpy.ops.object.select_all(action="DESELECT")
        for obj in self.parts:obj.select_set(True)
        bpy.context.view_layer.objects.active=self.parts[0]
        bpy.ops.export_scene.fbx(filepath=str(directory/(self.id+".fbx")),use_selection=True,
            object_types={"MESH"},axis_forward="-Z",axis_up="Y",apply_unit_scale=True,
            bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True)
        tris=sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in self.parts)
        assert tris < 4000,(self.id,tris)
        return {"id":self.id,"kind":self.kind,"paint":self.paint,"width":self.width,"length":self.length,
                "height":self.height,"wheelbase":self.wheelbase,"radius":self.radius,"triangles":tris,"wheels":self.wheels}


def main():
    parser=argparse.ArgumentParser();parser.add_argument("--project",required=True)
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    project=Path(args.project);source=project/"ArtSource/TrafficFleet";export=project/"Assets/Art/TrafficFleet/Models"
    source.mkdir(parents=True,exist_ok=True);export.mkdir(parents=True,exist_ok=True)
    destination=source/"TrafficFleet.blend"
    if destination.exists():raise RuntimeError("Existing artist source preserved; choose a new source folder before regenerating.")
    bpy.ops.object.select_all(action="SELECT");bpy.ops.object.delete(use_global=False)
    scene=bpy.context.scene;scene.name="Traffic fleet - editable source";scene.unit_settings.system="METRIC"
    materials={}
    for name,color in PALETTE.items():
        material=bpy.data.materials.new(name);material.diffuse_color=(*[linear(c) for c in color],1)
        material.use_nodes=True;bsdf=material.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value=material.diffuse_color
        bsdf.inputs["Roughness"].default_value=.72 if name=="Glass" else .85
        bsdf.inputs["Metallic"].default_value=0
        materials[name]=material
    vehicles=[];reports=[]
    for design in DESIGNS:
        vehicle=Vehicle(design,materials);vehicle.build();reports.append(vehicle.export(export));vehicles.append(vehicle)
    # Collections are arranged as an editable showroom only after local-space FBX export.
    for index,vehicle in enumerate(vehicles):
        offset=Vector(((index%5)*7.5,-(index//5)*9,0))
        for obj in vehicle.parts:obj.location+=offset
    scene.world.color=(.3,.3,.3)
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            area.spaces.active.region_3d.view_distance=42
            area.spaces.active.region_3d.view_location=(15,-13,0)
            area.spaces.active.shading.color_type="MATERIAL"
    bpy.ops.wm.save_as_mainfile(filepath=str(destination))
    (source/"fleet-report.json").write_text(json.dumps({"palette":PALETTE,"vehicles":reports},indent=2),encoding="utf-8")
    print("FLEET PASS:",len(reports),"vehicles;",sum(r["triangles"] for r in reports),"triangles; opaque glass alpha=1")


if __name__=="__main__":main()
