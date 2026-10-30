CATEGORY_FOLDER='Expansion_01_BreadPastries'
SCRIPT_NAME='BuildExpansionBread.py'
ITERATION=2


def bagel():
    expansion_palette()
    body=tube_loop('Real open bagel ring',(0,0,.019),(.035,.035),.019,'golden',18,8,'XY')
    body.data.materials.append(make_material('cream'))
    for face in body.data.polygons:
        if face.center.z<.011: face.material_index=1
    parts=[body]
    for i in range(16 if ITERATION>=2 else 12):
        angle=i*2.399;rad=.033+.009*math.sin(i*3)
        seed=ball('Sesame seed',(rad*math.cos(angle),rad*math.sin(angle),.035),(.003,.0012,.00065),'cream',6,3)
        seed.rotation_euler.z=angle+.4;apply_transform(seed);parts.append(seed)
    return parts


def baguette():
    expansion_palette()
    # Cross-section rings form genuine recessed diagonal scores in the bread.
    sections=[(-.2,.05),(-.188,.65),(-.17,.94)]
    for y in (-.128,-.064,0,.064,.128):sections.extend([(y-.008,1),(y-.003,.86),(y+.003,.86),(y+.008,1)])
    sections.extend([(.17,.94),(.188,.65),(.2,.05)])
    sections.sort();rings=[]
    for y,depth in sections:
        points=[]
        for i in range(10):
            a=i*math.tau/10;x=.031*math.cos(a);upper=max(0,math.sin(a))
            endscale=min(1,(max(0,.201-abs(y))/.030)**.5)
            depression=(1-depth)*.044*upper**3 if abs(y)<.16 else 0
            points.append((x*endscale,y+(x*.45 if abs(y)<.16 else 0),.032+ .029*math.sin(a)*endscale-depression))
        rings.append(points)
    obj=loft('Curved loaf with recessed scoring',rings,make_material('golden'))
    obj.data.materials.append(make_material('cream'))
    for face in obj.data.polygons:
        # Pale crumb is exposed only along the five recessed top cuts.
        if face.center.z>.047 and any(abs(face.center.y-y)<.011 for y in (-.128,-.064,0,.064,.128)):
            face.material_index=1
    return [obj]


def cinnamon_roll():
    expansion_palette()
    parts=[lathe('Plump baked roll',[(0,.037),(.003,.047),(.013,.050),(.034,.049),(.043,.041),(.045,.012)],make_material('golden'),12)]
    pts=[]
    for i in range(37):
        t=i/36;r=.004+.037*t;a=t*math.tau*2.7
        pts.append((r*math.cos(a),r*math.sin(a),.045+.002*math.cos(t*math.pi)))
    parts.append(path_tube('Dark cinnamon spiral',pts,.0023,'dark_brown'))
    icing=[]
    for i in range(9):
        x=-.035+i*.070/8;icing.append((x,.006*math.sin(i*.8),.048))
    parts.append(path_tube('Thick cream icing drizzle',icing,.0017 if ITERATION==1 else .0023,'icing'))
    return parts


def gingerbread():
    expansion_palette()
    # One continuous cookie contour gives attached soft limbs, not stacked cubes.
    contour=[(-.014,.046),(-.019,.057),(-.016,.070),(-.008,.076),(.008,.076),(.016,.070),(.019,.057),(.014,.046),
       (.020,.040),(.043,.030),(.048,.022),(.043,.016),(.034,.015),(.020,.022),(.020,.003),
       (.031,-.025),(.029,-.035),(.018,-.039),(.009,-.034),(0,-.014),(-.009,-.034),(-.018,-.039),(-.029,-.035),(-.031,-.025),(-.020,.003),
       (-.020,.022),(-.034,.015),(-.043,.016),(-.048,.022),(-.043,.030),(-.020,.040)]
    parts=[prism('Thick gingerbread cookie',contour,0,.014,make_material('toast'))]
    for x in (-.006,.006):parts.append(ball('Icing eye',(x,.060,.015),(.002,.002,.001),'icing',6,3))
    smile=[(.008*math.cos(math.pi+i*math.pi/6),.054+.004*math.sin(math.pi+i*math.pi/6),.015) for i in range(7)]
    parts.append(path_tube('Icing smile',smile,.0011,'icing'))
    for y,color in ((.032,'leaf'),(.020,'cherry')):parts.append(colored_ball('Candy button',(0,y,.016),(.0035,.0035,.0018),color,6,3))
    if ITERATION>=2:
        for side in (-1,1):parts.append(path_tube('Icing cuff',[(side*.034,.019,.015),(side*.037,.023,.015),(side*.038,.029,.015)],.001,'icing'))
    return parts


def fruit_tart():
    expansion_palette()
    # Outer flutes and inner cavity belong to a continuous pastry shell.
    obj=hollow_vessel('Fluted pastry crust',[(0,.034),(.003,.042),(.023,.050),(.027,.048),(.024,.044),(.006,.034)],'golden',20)
    for v in obj.data.vertices:
        a=math.atan2(v.co.y,v.co.x);v.co.x*=1+.028*math.cos(a*10);v.co.y*=1+.028*math.cos(a*10)
    parts=[obj,cylinder('Cream filling',(0,0,.018),.041,.012,'cream',12)]
    for x,y in [(-.025,-.008),(-.011,-.024),(-.024,.016)]:parts.append(colored_ball('Blueberry',(x,y,.033),(.010,.010,.009),'plum',8,4))
    strawberry=colored_ball('Strawberry slice',(.002,.011,.037),(.012,.017,.011),'cherry',8,4);parts.append(strawberry)
    for i in range(4):parts.append(colored_ball('Strawberry seed',(.001+(i%2)*.005,.006+(i//2)*.009,.047),(.0012,.0012,.0006),'cream',6,3))
    kiwi=make_disk('Kiwi slice',(.021,-.008,.035),(.017,.017),make_material('leaf'),vertices=10);parts.append(kiwi)
    parts.append(colored_ball('Kiwi pale core',(.021,-.008,.036),(.006,.006,.001),'cream',6,3))
    if ITERATION>=2:
        for i in range(8):
            a=i*math.tau/8;parts.append(colored_ball('Kiwi seed',(.021+.009*math.cos(a),-.008+.009*math.sin(a),.036),(.0008,.0012,.0004),'dark_brown',4,3))
    return parts


def eclair():
    expansion_palette()
    body=ball('Long baked eclair',(0,0,.022),(.068,.025,.022),'toast',12,6)
    glaze=ball('Rounded chocolate glaze',(0,0,.035),(.064,.023,.009),'dark_brown',12,4)
    glaze.data.materials[0].node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.82
    parts=[body,glaze]
    for x in (-.057,.057):parts.append(ball('Visible cream at pastry end',(x,0,.023),(.010,.019,.007),'cream',8,4))
    if ITERATION>=2:
        # A narrow cream seam helps the filling read from the game camera.
        parts.append(path_tube('Cream filling seam',[(-.050,-.020,.023),(0,-.024,.022),(.050,-.020,.023)],.0014,'cream'))
    return parts


JOBS=[('bagel',bagel,900),('baguette',baguette,900),('cinnamon_roll',cinnamon_roll,900),
      ('gingerbread',gingerbread,1200),('fruit_tart',fruit_tart,1200),('eclair',eclair,900)]
if __name__=='__main__':run_category()
