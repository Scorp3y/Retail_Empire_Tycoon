CATEGORY_FOLDER="Wave2_06_ToysGames"
SCRIPT_NAME="BuildToysGames.py"
ITERATION=3

def teddy_bear():
    parts=[ball("Bear body",(0,0,.093),(.054,.040,.066),"kiwi_skin",10,6),
           ball("Large round bear head",(0,-.002,.189),(.059,.045,.056),"kiwi_skin",10,6),
           ball("Cream muzzle",(0,-.043,.172),(.027,.010,.018),"cream",8,4)]
    for side in (-1,1):
        parts.append(ball("Round ear",(side*.047,0,.232),(.021,.014,.023),"kiwi_skin",8,4))
        parts.append(ball("Inner ear",(side*.047,-.013,.232),(.012,.002,.013),"cream",6,4))
        parts.append(ball("Seated foot",(side*.037,-.026,.027),(.029,.034,.027),"kiwi_skin",8,4))
        parts.append(ball("Foot pad",(side*.037,-.0625 if ITERATION>=2 else -.054,.027),(.018,.003,.016),"cream",6,4))
        arm=ball("Bear arm",(side*.058,-.006,.100),(.021,.025,.040),"kiwi_skin",8,4)
        arm.rotation_euler.y=side*.35;apply_transform(arm);parts.append(arm)
        parts.append(ball("Bear eye",(side*.020,-.044,.196),(.0039,.0025,.0047),"ink",6,3))
    parts.append(ball("Bear nose",(0,-.053,.180),(.008,.003,.005),"ink",6,3))
    for side in (-1,1):
        bow=ball("Blue bow wing",(side*.013,-.042,.135),(.014,.006,.009),"blue",6,4)
        bow.rotation_euler.y=side*.25;apply_transform(bow);parts.append(bow)
    parts.append(ball("Bow knot",(0,-.047,.135),(.006,.004,.006),"blue",6,3))
    if ITERATION>=2:
        parts.append(ball("Cream belly patch",(0,-.037,.085),(.032,.006,.037),"cream",8,4))
    if ITERATION>=3:
        parts.append(bar_between("Stitched smile left",(-.011,-.053,.168),(0,-.055,.164),.0017,"ink"))
        parts.append(bar_between("Stitched smile right",(0,-.055,.164),(.011,-.053,.168),.0017,"ink"))
    return parts

def toy_car():
    parts=[box("Rounded red car body",(0,0,.030),(.12,.058,.025),"cherry"),
           box("Car cabin",(-.004,0,.053),(.064,.049,.032),"cherry")]
    for side in (-1,1):
        shape=outline(.052,.021,.003)
        parts.append(mesh_object("Light blue side window",[(x-.004,side*.025,z+.055) for x,z in shape],
                                 [tuple(range(8))],make_material("blue")))
        for x in (-.039,.038):
            wheel=cylinder("Chunky dark wheel",(x,side*.031,.018),.018,.012,"ink",8)
            wheel.rotation_euler.x=math.pi/2;apply_transform(wheel);parts.append(wheel)
            hub=disk_front("Grey hubcap",x,side*.038,.018,.009,.009,"metal",8)
            if side>0:hub.rotation_euler.z=math.pi;apply_transform(hub)
            parts.append(hub)
    for x in (-.037,.030):
        window=mesh_object("Front / rear glass",[(x,y,z+.055) for y,z in outline(.041,.021,.003)],
                           [tuple(range(8))],make_material("blue"))
        parts.append(window)
    for y in (-.018,.018):
        parts.append(box("Small headlight",(-.061,y,.032),(.0015,.010,.007),"metal"))
    if ITERATION>=2:
        parts.append(box("Front bumper",(-.061,0,.022),(.003,.047,.006),"metal"))
        parts.append(box("Rear bumper",(.061,0,.022),(.003,.047,.006),"metal"))
    if ITERATION>=3:
        # Visible central pillar separates the two side windows as real bodywork.
        for side in (-1,1):parts.append(box("Red window pillar",(-.002,side*.027,.055),(.003,.002,.023),"cherry"))
    return parts

def building_blocks():
    parts=[]
    for i,(x,y,z,color) in enumerate(((-.024,-.018,.012,"blue"),(.025,.016,.012,"cherry"),
                                     (-.008,.008,.041,"leaf"),(.013,.008,.071,"mango_flesh"))):
        parts.append(box("Toy brick body",(x,y,z),(.060,.043,.023),color))
        for dx in (-.016,.016):
            for dy in (-.010,.010):
                # Six radial sides plus bevel-shaped profile keep studs rounded.
                stud=lathe("Raised brick stud",[(0,.0054),(.001,.006),(.008,.006),(.009,.0054)],make_material(color),6)
                stud.location=(x+dx,y+dy,z+.0115);parts.append(stud)
    if ITERATION>=2:
        for p in parts[10:]:p.location.x-=.006
    if ITERATION>=3:
        # Four lower studs remain exposed by a small, deliberate stack offset.
        for p in parts[15:]:p.location.y+=.010
    return parts

def doll():
    parts=[ball("Doll big head",(0,-.002,.213),(.044,.035,.045),"cream",10,6),
           ball("Brown hair cap",(0,.005,.231),(.047,.035,.036),"kiwi_skin",10,6)]
    # Small hair cap leaves the front face uncovered.
    for v in parts[-1].data.vertices:
        if v.co.y<0:v.co.y*=.25
    parts.append(role(lathe("Teal flared dress",[(.058,.041),(.061,.045),(.085,.043),(.143,.025),(.151,.023)],make_material("teal"),10),"clothing"))
    for side in (-1,1):
        parts.append(ball("Pigtail",(side*.045,.004,.222),(.019,.019,.030),"kiwi_skin",8,4))
        parts.append(ball("Doll shoe",(side*.017,-.008,.014),(.016,.026,.014),"kiwi_skin",8,4))
        parts.append(ball("Chunky doll leg",(side*.017,0,.048),(.010,.011,.025),"cream",8,4))
        arm=ball("Doll arm",(side*.036,0,.129),(.012,.013,.032),"cream",8,4)
        arm.rotation_euler.y=side*.35;apply_transform(arm);parts.append(arm)
        parts.append(disk_front("Doll eye",side*.014,-.036,.216,.003,.0035,"kiwi_skin",6))
    parts.append(bar_between("Doll smile",(-.008,-.037,.200),(.008,-.037,.200),.002,"kiwi_skin"))
    # A readable 5-petal flower uses geometry and the same cream material.
    for i in range(5):
        a=i*math.tau/5
        parts.append(disk_front("Dress flower petal",.006*math.cos(a),-.0355 if ITERATION>=2 else -.030,.118+.006*math.sin(a),.004,.004,"cream",6))
    if ITERATION>=2:
        parts.append(box("Dress waist ribbon",(0,-.026,.140),(.045,.003,.006),"cream"))
    if ITERATION>=3:
        for side in (-1,1):parts.append(ball("Pigtail tie",(side*.044,-.011,.231),(.009,.004,.005),"teal",6,3))
        for x in (-.022,0,.022):parts.append(ball("Soft hair fringe",(x,-.023,.244),(.017,.014,.013),"kiwi_skin",8,4))
    return parts

def football():
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1)
    ico=bpy.context.object
    points=[v.co.copy() for v in ico.data.vertices]
    triangles=[tuple(f.vertices) for f in ico.data.polygons]
    bpy.data.objects.remove(ico,do_unlink=True)
    directed={}
    vertices=[]
    for a,b,c in triangles:
        for i,j in ((a,b),(b,a),(b,c),(c,b),(c,a),(a,c)):
            if (i,j) not in directed:
                directed[i,j]=len(vertices)
                vertices.append(tuple(((points[i]*2+points[j])/3).normalized()*.11))
    faces=[];black=[]
    for a,b,c in triangles:faces.append(tuple(directed[p] for p in ((a,b),(b,a),(b,c),(c,b),(c,a),(a,c))))
    for i,p in enumerate(points):
        neighbours=[j for a,j in directed if a==i]
        normal=p.normalized();u=normal.cross(Vector((0,0,1)))
        if u.length<.01:u=normal.cross(Vector((0,1,0)))
        u.normalize();v=normal.cross(u)
        neighbours.sort(key=lambda j:math.atan2((points[j]-p).dot(v),(points[j]-p).dot(u)))
        black.append(len(faces));faces.append(tuple(directed[i,j] for j in neighbours))
    obj=mesh_object("Truncated icosahedron soccer ball",vertices,faces,make_material("cream"))
    obj.data.materials.append(make_material("ink"))
    for index in black:obj.data.polygons[index].material_index=1
    add_bevel(obj)
    if ITERATION>=2:obj.rotation_euler=(.10,.08,.18);apply_transform(obj)
    if ITERATION>=3:
        # Real low-relief valve, sized to remain a tiny secondary detail.
        valve=make_disk("Ball valve",(0,-.1095,.003),(.002,.002),make_material("ink"),vertices=6)
        valve.rotation_euler.x=math.pi/2;apply_transform(valve)
        return [obj,valve]
    return [obj]

def puzzle():
    parts=[box("Puzzle box",(0,0,.014),(.25,.171,.028),"mango_flesh"),
           box("Lid landscape sky",(0,0,.030),(.238,.159,.005),"blue")]
    hill=prism("Landscape hill",[(-.115,-.073),(.115,-.073),(.115,.006),(.068,.025),(.025,.002),(-.025,.032),(-.080,.011),(-.115,.022)],.0325,.0345,make_material("leaf"))
    parts.append(hill)
    sun=make_disk("Landscape sun",(.070,.045,.033),(.014,.014),make_material("mango_flesh"),vertices=10);parts.append(sun)
    parts.append(box("Tree trunk",(-.062,.026,.034),(.006,.030,.003),"dark_green"))
    crown=ball("Little tree crown",(-.062,.052 if ITERATION>=2 else .039,.037),(.024,.018,.003),"dark_green",8,4);parts.append(crown)
    if ITERATION>=2:
        for y in (-.083,.083):parts.append(box("Raised box lid rim",(0,y,.031),(.247,.004,.005),"mango_flesh"))
    if ITERATION>=3:
        for x in (-.123,.123):parts.append(box("Side lid rim",(x,0,.031),(.004,.166,.005),"mango_flesh"))
    return parts

JOBS=[("teddy_bear",teddy_bear,1200),("toy_car",toy_car,900),("building_blocks",building_blocks,1200),
      ("doll",doll,1200),("football",football,1200),("puzzle",puzzle,900)]
if __name__=="__main__":run_category()
