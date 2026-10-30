CATEGORY_FOLDER='Expansion_06_BabyCare'
SCRIPT_NAME='BuildExpansionBaby.py'
ITERATION=2


def arrange_expansion_preview(objects):layout_large_props(objects)


def puffy_pack(name,width,depth,height,color):
    shape=outline(width,depth,min(width,depth)*.19)
    return loft(name,[[(x*s,y*s,z) for x,y in shape] for z,s in
        ((0,.87),(.012,1),(height-.012,1),(height,.86))],make_material(color))


def paper_fold(name,points,width,color):
    # Closed solid paper with a physical half-millimetre thickness.
    vertices=[];count=len(points)
    for thickness in (-.00025,.00025):
        for i,p in enumerate(points):
            tangent=Vector(points[min(i+1,count-1)])-Vector(points[max(i-1,0)])
            normal=Vector((0,-tangent.z,tangent.y)).normalized()
            for side in (-1,1):vertices.append(tuple(Vector(p)+normal*thickness+Vector((side*width/2,0,0))))
    faces=[]
    for layer in range(2):
        for i in range(count-1):faces.append((layer*count*2+i*2,layer*count*2+i*2+1,layer*count*2+i*2+3,layer*count*2+i*2+2))
    for side in (0,1):
        for i in range(count-1):faces.append((i*2+side,(i+1)*2+side,count*2+(i+1)*2+side,count*2+i*2+side))
    faces.extend([(0,1,count*2+1,count*2),(count*2-2,count*2-1,count*4-1,count*4-2)])
    obj=mesh_object(name,vertices,faces,make_material(color));add_bevel(obj);return obj


def diapers():
    expansion_palette()
    parts=[puffy_pack('Soft diaper pack',.250,.115,.190,'cream'),
           box('Blue lower wrap',(0,-.0585,.032),(.228,.002,.039),'blue'),
           tube_loop('Small real carry handle',(0,0,.196),(.028,.012),.003,'cream',12,6,'XZ')]
    parts.append(colored_ball('Friendly baby face',(0,-.059,.113),(.029,.002,.028),'peach',10,5))
    for x in (-.031,.031):parts.append(colored_ball('Baby ear',(x,-.059,.113),(.005,.0015,.007),'peach',6,3))
    for x in (-.009,.009):parts.append(colored_ball('Baby eye',(x,-.062,.120),(.003,.0008,.003),'dark_brown',6,3))
    smile=[(.010*math.cos(math.pi+i*math.pi/5),-.062,.109+.005*math.sin(math.pi+i*math.pi/5)) for i in range(6)]
    parts.append(pigment(rounded_rib('Small baby smile',smile,.0012,'cream'),'dark_brown'))
    if ITERATION>=2:
        curl=[(-.010,-.062,.141),(-.005,-.062,.148),(.003,-.062,.147),(.006,-.062,.142)]
        parts.append(pigment(rounded_rib('Baby hair curl',curl,.0012,'cream'),'dark_brown'))
    return parts


def baby_wipes():
    expansion_palette()
    parts=[puffy_pack('Soft pink wipes packet',.180,.100,.062,'pink'),
           box('Cream flip lid',(0,0,.066),(.069,.044,.008),'cream'),
           paper_fold('Wipe coming through lid',[(0,0,.071),(0,-.003,.088),(0,.006,.103),(0,.011,.099)],.029,'icing')]
    heart=[(0,.020),(-.013,.031),(-.014,.039),(-.009,.044),(-.003,.044),(0,.040),(.003,.044),(.009,.044),(.014,.039),(.013,.031)]
    parts.append(front_shape('White heart icon',heart,-.051,'icing'))
    if ITERATION>=2:
        hinge=box('Flip lid hinge',(0,.023,.068),(.038,.004,.006),'cream');parts.append(hinge)
    return parts


def baby_food():
    expansion_palette();clear_material()
    shell=oval_shell('Hollow clear baby food jar',.029,.065)
    contents=lathe('Orange puree contents',[(.003,.022),(.005,.026),(.053,.027),(.058,.024)],make_material('cream'),10)
    parts=[shell,pigment(contents,'peach'),cylinder('Orange baby food lid',(0,0,.072),.030,.016,'golden',10),
           curved_label(.0297,.034,.041,.032,make_material('cream'))]
    parts.append(front_shape('Carrot icon',[(-.008,.044),(.008,.044),(.007,.038),(-.002,.021),(-.006,.034)],-.0305,'peach'))
    parts.append(leaf_icon(-.031,.046,.009))
    if ITERATION>=2:
        parts.append(colored_ball('Carrot inset mark',(.002,-.0315,.035),(.004,.0006,.001),'golden',6,3))
    return parts


def baby_bottle():
    expansion_palette();clear_material()
    shell=hollow_vessel('Transparent milk bottle',[(0,.025),(.004,.031),(.124,.031),(.143,.025),(.148,.024),
          (.148,.021),(.141,.022),(.123,.028),(.006,.026)],'plastic',10)
    shell.data.materials[0]=clear_material()
    parts=[shell,lathe('Separate milk filling',[(.004,.024),(.006,.027),(.111,.027),(.114,.026)],make_material('cream'),10),
           cylinder('Blue screw ring',(0,0,.153),.029,.018,'blue',10),
           lathe('Cream silicone nipple',[(.161,.025),(.166,.023),(.174,.011),(.191,.009),(.200,.005)],make_material('cream'),10)]
    for i in range(4 if ITERATION==1 else 5):
        z=.032+i*.017
        parts.append(ball('Rounded blue measurement mark',(-.006,-.032,z),
                          (.007 if i%2==0 else .0045,.0007,.001),'blue',6,3))
    return parts


def pacifier():
    expansion_palette()
    shield=ball('Green pacifier shield',(0,0,.022),(.027,.005,.019),'leaf',12,6)
    for x in (-.016,.016):
        bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=.004,depth=.040,location=(x,0,.023),rotation=(math.pi/2,0,0))
        cutter=bpy.context.object
        mod=shield.modifiers.new('Real ventilation hole','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
        apply_modifiers(shield);bpy.data.objects.remove(cutter,do_unlink=True)
    add_bevel(shield)
    parts=[shield,ball('Yellow soft nipple',(0,.015,.022),(.010,.017,.010),'custard',8,4),
           ball('Rounded nipple tip',(0,.033,.022),(.012,.014,.011),'custard',8,4),
           tube_loop('Chunky open ring handle',(0,-.014,.021),(.015,.012),.0035,'leaf',12,6,'XZ')]
    if ITERATION>=2:
        parts.append(ball('Handle hinge',(0,-.007,.024),(.008,.005,.007),'leaf',6,3))
    return parts


def baby_cereal():
    expansion_palette()
    parts=[box('Blue cereal carton',(0,0,.100),(.096,.056,.200),'blue'),
           box('Cream side panel',(.0485,0,.102),(.001,.049,.184),'cream')]
    bowl=[(-.024,.073),(.024,.073),(.019,.053),(.011,.046),(-.011,.046),(-.019,.053)]
    parts.append(front_shape('Bowl icon',bowl,-.029,'cream'))
    stem=[(0,-.030,.080),(0,-.030,.105),(.005,-.030,.132)]
    parts.append(pigment(rounded_rib('Wheat stem',stem,.0012,'cream'),'golden'))
    for i in range(4):
        x=(-.006 if i%2 else .006);z=.097+i*.009
        grain=colored_ball('Wheat grain',(x,-.030,z),(.005,.001,.008),'golden',6,3);grain.rotation_euler.y=(-.5 if i%2 else .5);apply_transform(grain);parts.append(grain)
    if ITERATION>=2:parts.append(box('Cream top carton fold',(0,0,.200),(.087,.002,.003),'cream'))
    return parts


JOBS=[('diapers',diapers,900),('baby_wipes',baby_wipes,900),('baby_food',baby_food,900),
      ('baby_bottle',baby_bottle,900),('pacifier',pacifier,900),('baby_cereal',baby_cereal,900)]
if __name__=='__main__':run_category()
