CATEGORY_FOLDER='Expansion_04_PersonalCare'
SCRIPT_NAME='BuildExpansionCare.py'
ITERATION=2


def shaped_shell(name,width,depth,profile,color):
    shape=outline(width,depth,min(width,depth)*.18)
    rings=[[(x*sx,y*sy,z) for x,y in shape] for z,sx,sy in profile]
    obj=loft(name,rings,make_material(color))
    obj['hollow_opening']=True
    return obj


def toothpaste():
    expansion_palette()
    shell=shaped_shell('Hollow tapered toothpaste tube',.043,.025,
        [(.018,.60,.60),(.027,1,1),(.127,.95,.8),(.152,.93,.15),
         (.152,.86,.07),(.126,.88,.69),(.028,.92,.9),(.021,.52,.52)],'cream')
    parts=[shell,cylinder('Green standing cap',(0,0,.010),.015,.020,'leaf',10),
           box('Flattened real top crimp',(0,0,.155),(.042,.004,.006),'cream'),
           leaf_icon(-.013,.082,.026)]
    if ITERATION>=2:
        for x in (-.014,-.007,0,.007,.014):
            parts.append(box('Top crimp fold',(x,-.0026,.155),(.001,.001,.004),'cream'))
    return parts


def toothbrush():
    expansion_palette();clear_material()
    parts=[box('Blue backing card',(0,.002,.110),(.064,.005,.220),'blue'),
           box('Cream toothbrush handle',(0,-.009,.082),(.011,.008,.121),'cream'),
           box('Rounded toothbrush head',(0,-.009,.163),(.018,.008,.039),'cream'),
           box('Cream bristle block',(0,-.015,.165),(.017,.006,.031),'cream')]
    # The blister has a front wall, inner front wall, and an actual open back.
    blister=shaped_shell('Formed clear blister',.054,.199,
        [(.013,.86,.96),(.010,1,1),(0,1,1),(0,.96,.99),(.009,.94,.98),(.011,.85,.96)],'plastic')
    blister.data.materials[0]=clear_material()
    blister.rotation_euler.x=math.pi/2;blister.location=(0,-.004,.110)
    apply_transform(blister);blister['hollow_opening']=False
    parts.append(blister)
    stripe=box('Sage brush grip',(0,-.0135,.078),(.005,.0015,.074),'cream');parts.append(pigment(stripe,'leaf'))
    if ITERATION>=2:
        for z in (.154,.165,.176):
            rib=box('Readable bristle row',(0,-.019,z),(.015,.002,.002),'cream');parts.append(rib)
    return parts


def hand_soap():
    expansion_palette()
    body=shaped_shell('Hollow pink soap bottle',.073,.054,
        [(0,.83,.83),(.005,1,1),(.117,1,1),(.137,.65,.65),(.146,.3,.3),
         (.146,.25,.25),(.136,.59,.59),(.116,.95,.94),(.006,.94,.93),(.003,.78,.77)],'pink')
    parts=[body,cylinder('Pump collar',(0,0,.151),.013,.014,'cream',10),
           cylinder('Pump stem',(0,0,.175),.004,.030,'cream',8),
           box('Pump head',(0,0,.195),(.047,.017,.010),'pink'),
           box('Pump nozzle pointing down',(-.021,0,.189),(.010,.017,.014),'pink'),
           box('Cream soap label',(0,-.0275,.078),(.044,.001,.046),'cream'),
           leaf_icon(-.029,.077,.024)]
    if ITERATION>=2:
        parts.append(front_shape('Nozzle opening',[(-.024,.181),(-.018,.181),(-.018,.184),(-.024,.184)],-.009,'dark_green'))
    return parts


def shower_gel():
    expansion_palette()
    body=shaped_shell('Hollow soft blue shower bottle',.070,.044,
       [(0,.80,.8),(.005,1,1),(.142,1,1),(.166,.75,.75),(.174,.40,.40),
        (.174,.34,.34),(.165,.70,.68),(.141,.95,.93),(.006,.94,.92),(.003,.76,.74)],'blue')
    parts=[body,box('Blue flip cap',(0,0,.185),(.037,.028,.030),'blue')]
    drop=[(0,.124),(-.008,.111),(-.014,.098),(-.012,.089),(-.005,.085),(.005,.085),(.012,.089),(.014,.098),(.008,.111)]
    parts.append(front_shape('White raised water drop',drop,-.023,'icing'))
    if ITERATION>=2:
        parts.append(box('Flip cap seam',(0,-.0145,.186),(.029,.001,.002),'cream'))
        parts.append(box('Rounded flip hinge',(0,.014,.187),(.015,.004,.009),'blue'))
    return parts


def deodorant():
    expansion_palette()
    body=shaped_shell('Hollow sage deodorant case',.051,.037,
       [(0,.78,.78),(.003,1,1),(.084,1,1),(.087,.96,.96),
        (.087,.87,.85),(.005,.87,.85),(.003,.73,.73)],'leaf')
    # The cap is domed using successive rounded rectangular sections.
    cap=shaped_shell('Separate hollow rounded cap',.053,.039,
       [(.140,.40,.45),(.132,.80,.85),(.115,1,1),(.088,1,1),
        (.088,.91,.88),(.114,.91,.88),(.130,.73,.77),(.138,.36,.40)],'leaf')
    # This cavity opens downwards; its roof must remain closed from above.
    cap['hollow_opening']=False
    parts=[body,cap,box('Cream deodorant label',(0,-.019,.051),(.034,.001,.040),'cream'),leaf_icon(-.020,.052,.018)]
    if ITERATION>=2:
        parts.append(box('Cream twist wheel',(0,0,.005),(.054,.040,.009),'cream'))
    return parts


def tissue_box():
    expansion_palette()
    # Rounded perimeter and separate inner surfaces leave a genuine dispensing slit.
    shape=outline(.120,.083,.009)
    rings=[[(x*s,y*s,z) for x,y in shape] for z,s in ((0,.94),(.003,1),(.060,1),(.063,.96),(.060,.93),(.004,.93))]
    shell=loft('Real hollow tissue box',rings,make_material('cream'));shell['hollow_opening']=True
    parts=[shell]
    # Four panels close most of the top, leaving an open 46 x 7 mm central slot.
    for side in (-1,1):
        parts.append(box('Upper folded cardboard panel',(0,side*.022,.062),(.115,.036,.002),'cream'))
        parts.append(box('Side of dispensing slit',(side*.040,0,.062),(.034,.008,.002),'cream'))
    for i in range(2 if ITERATION==1 else 3):
        # A paper sheet with physical thickness follows a soft, bent cross-section.
        path=[(0,.063),(-.003,.080),(.002,.103),(.006,.113),(-.002,.108)]
        points=[]
        for y,z in path:points.append(((i-1)*.012,y+(i-1)*.002,z+(.003 if i==1 else 0)))
        verts=[]
        for thickness in (-.00025,.00025):
            for j,(x,y,z) in enumerate(points):
                direction=Vector(points[min(j+1,len(points)-1)])-Vector(points[max(0,j-1)])
                normal=Vector((0,-direction.z,direction.y)).normalized()
                width=.016 if ITERATION==1 else (.018,.029,.045,.040,.030)[j]
                verts.extend([(x-width/2,y+normal.y*thickness,z+normal.z*thickness),
                              (x+width/2,y+normal.y*thickness,z+normal.z*thickness)])
        count=len(points);faces=[]
        for layer in range(2):
            for j in range(count-1):faces.append((layer*count*2+j*2,layer*count*2+j*2+1,layer*count*2+j*2+3,layer*count*2+j*2+2))
        for edge in (0,1):
            for j in range(count-1):faces.append((j*2+edge,(j+1)*2+edge,count*2+(j+1)*2+edge,count*2+j*2+edge))
        faces += [(0,1,count*2+1,count*2),(count*2-2,count*2-1,2*count*2-1,2*count*2-2)]
        sheet=mesh_object('Bent tissue with real thickness',verts,faces,make_material('icing'));add_bevel(sheet);parts.append(sheet)
    parts.append(leaf_icon(-.042,.030,.022))
    if ITERATION>=2:
        small_leaf=colored_ball('Second sprout leaf',(-.012,-.043,.022),(.006,.001,.003),'leaf',6,3)
        small_leaf.rotation_euler.y=-.4;apply_transform(small_leaf);parts.append(small_leaf)
    return parts


JOBS=[('toothpaste',toothpaste,900),('toothbrush',toothbrush,900),('hand_soap',hand_soap,900),
      ('shower_gel',shower_gel,900),('deodorant',deodorant,900),('tissues',tissue_box,1200)]
if __name__=='__main__':run_category()
