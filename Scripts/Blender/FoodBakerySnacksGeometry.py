"""Final Food Expansion: reusable packaging and whole bakery/snack products."""
def final_palette():
    part3_palette()
    PALETTE.update(pastry='#D6A164', pastry_light='#E3BD82', fried='#BF925A',
                   falafel='#9C7853', mint='#A6B79A', marshmallow_pink='#CFABA8',
                   fruit_red='#B96D65', candy_yellow='#DBBF76', cocoa='#725645')

def pack_tray(clear=False,round_tray=False):
    """Shared 20 cm tray; dark food trays and clear bakery trays use one profile."""
    final_palette();mat=clear_material() if clear else make_material('charcoal')
    if round_tray:
        tray=hollow_vessel('Real hollow round food tray',[(0,.086),(.003,.094),(.026,.10),(.028,.099),
             (.026,.096),(.006,.083)],'plastic' if clear else 'ivory',12)
        if clear:tray.data.materials[0]=mat
        lid=lathe('Clear round intact lid',[(.027,.10),(.063,.097),(.067,.086)],clear_material(),12)
    else:
        rings=[[(x,y,z) for x,y in outline(w,d,.008)] for z,w,d in
               [(0,.18,.115),(.003,.19,.125),(.026,.20,.135),(.028,.199,.134),(.026,.194,.129),(.005,.176,.111)]]
        tray=loft('Shared hollow food tray',rings,mat);tray['hollow_opening']=True
        low=clear and ITERATION>=2
        rings=[[(x,y,z) for x,y in outline(w,d,.008)] for z,w,d in
               [(.027,.20,.135),(.042 if low else .061,.196,.131),(.047 if low else .066,.180,.115)]]
        lid=loft('Clear intact food lid',rings,clear_material(),cap_start=False)
    return [tray,lid]

def sleeve(width=.045,depth=.137,height=.068):
    # A real open-ended paper loop around the package, with rounded corners.
    section=outline(depth,height,.004)
    rings=[[(x,y,z+height/2-.001) for y,z in section] for x in (-width/2,width/2)]
    obj=loft('Cream paper sleeve around tray',rings,make_material('ivory'),cap_start=False,cap_end=False)
    wall=obj.modifiers.new('Paper sleeve thickness','SOLIDIFY');wall.thickness=.0004
    return obj

def sleeve_icon(kind='leaf',color='sage',z=.068):
    icons=sprig(-.074,.023,color,kind,.65)
    return top_icon_parts(icons,z)

def top_icon_parts(icons,z=.068):
    for obj in icons:
        for v in obj.data.vertices:
            p=obj.matrix_world@v.co;v.co=(p.x,p.z-.023,z+.0008-(p.y+.074))
        obj.matrix_world=Matrix.Identity(4)
    return icons

def puff_pastry(obj,rise):
    """Raise the real cap centre into a gentle low-poly pastry dome."""
    bm=bmesh.new();bm.from_mesh(obj.data);bm.normal_update()
    faces=[f for f in bm.faces if f.normal.z>.9]
    face=max(faces,key=lambda f:f.calc_center_median().z)
    bmesh.ops.poke(bm,faces=[face],offset=rise,center_mode='MEAN_WEIGHTED')
    bm.to_mesh(obj.data);bm.free();obj.data.update()

def ready_icon(kind):
    if kind in ('burrito','spring_rolls'):return sleeve_icon('leaf')
    icons=[]
    if kind=='calzone':
        vertices=[];faces=[]
        for x in (-.010,0,.010):
            n=len(vertices)
            for dx,dz in ((-.002,-.010),(.002,0),(-.002,.010)):
                vertices.extend([(x+dx-.0008,-.074,.023+dz),(x+dx+.0008,-.074,.023+dz)])
            faces.extend([(n,n+1,n+3,n+2),(n+2,n+3,n+5,n+4)])
        icons=[mesh_object('Three steam lines',vertices,faces,make_material('sage'))]
    elif kind=='falafel':
        icons=[disk_front('Two-dot icon',x,-.074,.023,.004,.004,'sage',10) for x in (-.006,.006)]
    else:
        shape=[(-.012,.017),(-.010,.015),(.010,.015),(.012,.017),(.002,.035),(-.002,.035)]
        icons=[mesh_object('Rounded triangle icon',[(x,-.074,z) for x,z in shape],[tuple(range(6))],make_material('sage'))]
    return top_icon_parts(icons)

def finish_pack(parts,food,icons):
    geometry_pigment(food+icons)
    return parts+food+icons

def tied_bag(height=.18,width=.112,depth=.075):
    """One puffy tied-film base reused for meringues and colourful sweets."""
    final_palette()
    shoulder=.80 if height>=.20 and ITERATION>=3 else .73
    rings=[[(x*sx,y*sy,z*height) for x,y in outline(width,depth,.009)] for z,sx,sy in
           [(0,.75,.75),(.03,.95,.95),(.35,1.04,1.08),(shoulder,.96,1),(.91,.25,.24),(1,.40,.35)]]
    bag=loft('Soft intact tied clear bag',rings,clear_material())
    tie=tube_loop('Green twist tie',(0,0,height*.91),(width*.16,depth*.15),.0015,'sage',8,6,'XY')
    label=box('Cream candy label',(0,-depth*.56,height*.47),(width*.65,.001,height*.16),'ivory')
    return [bag,tie,label]

def snack_pouch(body='sage',band='ivory',height=.18,window=True):
    final_palette();parts=window_bag(body,band,body,'pea',True)
    for obj in list(parts):
        if obj.name.startswith(('Visible chunky grain','Hidden bulk','Legume identity')):
            parts.remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
    for obj in parts:
        obj.location.z*=height/.2
        for v in obj.data.vertices:v.co.z*=height/.2
        if not window and obj.name.startswith('Separate see-through'):obj.data.materials[0]=make_material(body)
    return parts

def waffle_body(x):
    # Three-by-three real square recesses with floors and sloping walls.
    n=9;w=.080;vertices=[]
    for j in range(n+1):
        for i in range(n+1):
            xx=(i/n-.5)*w;yy=(j/n-.5)*w
            if i in (0,n) and j in (0,n):xx*=.97;yy*=.97
            dent=i%3 in (1,2) and j%3 in (1,2)
            vertices.append((xx,yy,.018 if dent else .022))
    faces=[(j*(n+1)+i,j*(n+1)+i+1,(j+1)*(n+1)+i+1,(j+1)*(n+1)+i) for j in range(n) for i in range(n)]
    boundary=list(range(n+1))+[j*(n+1)+n for j in range(1,n+1)]+[n*(n+1)+i for i in range(n-1,-1,-1)]+[j*(n+1) for j in range(n-1,0,-1)]
    original=[vertices[i] for i in boundary]
    for i in boundary:vertices[i]=(vertices[i][0]*.98,vertices[i][1]*.98,vertices[i][2])
    previous=boundary
    # Two explicitly modelled bevel segments on the outer top edge avoid
    # applying a dense modifier to every grid recess. The floors stay real.
    for scale,z in ((.995,.0217),(1,.0212),(.98,.008)):
        start=len(vertices);vertices += [(x*scale,y*scale,z) for x,y,_ in original]
        current=list(range(start,start+len(boundary)))
        for k in range(len(boundary)):faces.append((previous[k],previous[(k+1)%len(boundary)],current[(k+1)%len(boundary)],current[k]))
        previous=current
    faces.append(tuple(reversed(previous)))
    obj=mesh_object('Whole waffle with recessed grid',vertices,faces,make_material('pastry'))
    if ITERATION>=2:
        obj.data.materials.append(make_material('fried'))
        for face in obj.data.polygons:
            if .017<face.center.z<.019 and face.normal.z>.8:face.material_index=1
    obj.location.x=x
    if ITERATION>=3:obj.rotation_euler.z=.03 if x>0 else -.03;obj.location.z=-.002
    return obj

def waffles():
    final_palette();parts=pack_tray(True);height=.049 if ITERATION>=2 else .068
    parts.append(sleeve(.038,height=height));food=[waffle_body(-.046),waffle_body(.046)]
    return finish_pack(parts,food,sleeve_icon('wheat',z=height))

def pancakes():
    final_palette();parts=pack_tray(True,True);food=[]
    for i in range(4):
        z=.007+i*.008
        food.append(lathe('Whole plump pancake',[(z,.060),(z+.002,.064),(z+.006,.064),(z+.008,.060)],make_material('pastry'),12))
    food.append(box('Whole butter square',(.007,0,.045),(.021,.021,.007),'cheese'))
    food.append(lathe('Syrup puddle',[(.040,.025),(.041,.029),(.042,.025)],make_material('amber'),12))
    food.append(path_tube('Syrup drip',[(-.024,-.018,.041),(-.045,-.030,.040),(-.061,-.026,.034),(-.061,-.024,.027)],.002,'amber',6))
    parts.append(curved_label(.097,.020,.054,.022,make_material('ivory')))
    icons=[disk_front('Pancake-stack icon',0,-.098-(i*.0001 if ITERATION>=3 else 0),.016+i*.005 if ITERATION>=3 else .020,
                      .014,.002 if ITERATION>=3 else .005,'pastry',12) for i in range(3 if ITERATION>=3 else 1)]
    return finish_pack(parts,food,icons)

def apple_pie():
    final_palette()
    tray=hollow_vessel('Hollow pale foil pie tray',[(0,.084),(.003,.091),(.025,.10),(.028,.099),(.026,.096),(.006,.082)],'ivory',12)
    crust=lathe('Whole uncut golden pie',[(.005,.080),(.008,.090),(.030,.094),(.039,.088),(.040,.076)],make_material('pastry'),16)
    food=[crust]
    # Four strips each way. Hidden strip end caps sit inside the rounded crust.
    for axis in range(2):
        for i in range(4):
            cross=(i-1.5)*.032;extent=math.sqrt(.083**2-cross**2);rings=[]
            steps=[-extent]+[(j-1.5)*.032 for j in range(4)]+[extent]
            for k,t in enumerate(steps):
                z=.043+(.0014 if (k+i+axis)%2 else -.0014)
                section=[(-.004,0),(-.0035,.001),(.0035,.001),(.004,0),(.0035,-.001),(-.0035,-.001)]
                rings.append([(t,cross+u,z+v) if axis==0 else (cross+u,t,z+v) for u,v in section])
            strip=loft('Woven real lattice strip',rings,make_material('pastry_light'));strip.modifiers.clear();food.append(strip)
    label=cylinder('Cream round pie label',(0,0,.047),.025,.0008,'ivory',10)
    icons=sprig(-.074,.023,'red','apple',.8)
    for obj in icons:
        for v in obj.data.vertices:
            p=obj.matrix_world@v.co;v.co=(p.x,p.z-.023,.049-(p.y+.074))
        obj.matrix_world=Matrix.Identity(4)
    return finish_pack([tray,label],food,icons)

def madeleines():
    final_palette();parts=pack_tray(True);height=.049 if ITERATION>=2 else .068;parts.append(sleeve(.038,height=height));food=[]
    for x in (-.060,0,.060):
        obj=make_sphere('Whole ridged shell madeleine',(x,0,.018),(.024,.044,.014),make_material('pastry'),segments=12,rings=5,flatten=False,
             deform=lambda q:(q.x*(.85+.15*q.y),q.y,q.z*(1+.18*math.cos(6*math.atan2(q.x,q.y+1.2)))))
        if ITERATION>=3:
            obj.data.materials.append(make_material('pastry_light'))
            for face in obj.data.polygons:
                p=face.center
                if p.z>.003 and math.cos(6*math.atan2(p.x/.024,p.y/.044+1.2))>.45:face.material_index=1
        add_bevel(obj,.0002);food.append(obj)
    return finish_pack(parts,food,sleeve_icon('wheat',z=height))

def meringues():
    final_palette();parts=tied_bag(.16);food=[]
    for i in range(6):
        x=(i%2-.5)*.034;y=(-.014 if i<4 else .014);z=.006+(i//2)*.037
        rings=[]
        for dz,r in [(0,.016),(.004,.018),(.022,.010),(.034,.0015)]:
            ripple=.08 if ITERATION>=2 else .14;twist=20 if ITERATION>=2 else 42
            rings.append([(x+r*(1+ripple*math.cos(j*math.tau/8*4))*math.cos(j*math.tau/8+dz*twist),
                           y+r*(1+ripple*math.cos(j*math.tau/8*4))*math.sin(j*math.tau/8+dz*twist),z+dz) for j in range(8)])
        food.append(loft('Whole swirled meringue kiss',rings,make_material('milk')))
    icons=[disk_front('Cloud icon',x,-.044,.075+dz,.008,.005,'milk',8) for x,dz in [(-.010,0),(0,.004),(.010,0)]]
    if ITERATION>=2:
        for i,obj in enumerate(icons):obj.location.y-=i*.0002
    return finish_pack(parts,food,icons)

def cannoli():
    final_palette();parts=pack_tray(True);height=.049 if ITERATION>=2 else .068;parts.append(sleeve(.035,height=height));food=[]
    for x in (-.057,0,.057):
        tube=open_profile('Whole crisp cannoli tube',[(-.038,.012),(-.037,.013),(.037,.013),(.038,.012),
             (.038,.010),(.037,.009),(-.037,.009),(-.038,.010)],'pastry',6)
        tube.rotation_euler.x=math.pi/2;tube.location=(x,0,.021);apply_transform(tube);food.append(tube)
        for side in (-1,1):
            food.append(ball('Cream at natural pastry opening',(x,side*.038,.021),(.009,.003,.009),'milk',6,4))
            for dx in (-.0035,.0035):food.append(ball('Small chocolate chip',(x+dx,side*.041,.024),(.0018,.0015,.0018),'cocoa',6,3))
    return finish_pack(parts,food,sleeve_icon('almond','pastry',z=height))

def sweets(kind):
    final_palette()
    if kind in ('popcorn','licorice','caramels'):
        parts=snack_pouch('ivory' if kind=='caramels' else 'dark_green','ivory',.22 if kind=='popcorn' else .18,kind!='popcorn')
    else:parts=tied_bag(.20 if kind=='lollipops' else .18,width=.13 if kind=='lollipops' and ITERATION>=3 else .112)
    food=[];icons=[]
    if kind=='popcorn':
        if ITERATION>=2:
            for obj in list(parts):
                if obj.name.startswith('Flush paper window edge'):parts.remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
        # The popcorn is a modelled relief on a closed paper bag.
        bowl=ball('Popcorn bowl icon',(0,-.038,.130),(.026,.0015,.012),'sage' if ITERATION>=2 else 'ivory',8,4);food.append(bowl)
        for i in range(7):food.append(ball('Popcorn puff icon',((i%4-1.5)*.011,-.040,.145+(i//4)*.010),(.007,.003,.007),'cheese' if i%3==0 else 'milk',6,4))
        if ITERATION>=2:
            icons=[disk_front('Cloud-shaped cream label',x,-.038-i*.0002,.080+dz,.012,.006,'ivory',8)
                   for i,(x,dz) in enumerate([(-.012,0),(0,.004),(.012,0)])]
        if ITERATION>=3:
            for obj in food:obj.location.z+=.030
    elif kind=='licorice':
        if ITERATION>=2:
            PALETTE['licorice_window']=PALETTE['charcoal']
            film=make_material('licorice_window',.25);film.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.2
            for obj in parts:
                if obj.name.startswith('Separate see-through'):obj.data.materials[0]=film
        for i in range(5):
            obj=lathe('Whole licorice stick',[(.024,.003),(.025,.004),(.097,.004),(.098,.003)],make_material('charcoal'),6)
            obj.location=( (i-2)*.015,-.017,0);food.append(obj)
        icons=sprig(-.032,.140,'sage','leaf',.7)
    elif kind=='caramels':
        for i in range(5):
            x=(i%3-1)*.029;z=.034+(i//3)*.038
            food.append(ball('Whole wrapped caramel',(x,-.017,z),(.010,.009,.011),'amber',6,4))
            for side in (-1,1):
                verts=[(x+side*.010,-.017,z),(x+side*.017,-.022,z-.007),(x+side*.017,-.012,z-.007),
                       (x+side*.017,-.012,z+.007),(x+side*.017,-.022,z+.007)]
                food.append(mesh_object('Twisted candy wrapper end',verts,[(0,1,2),(0,2,3),(0,3,4),(0,4,1),(1,4,3,2)],make_material('amber')))
        icons=[disk_front('Wrapped candy icon',0,-.033,.140,.010,.006,'amber',8)]
        if ITERATION>=2:
            icons.append(mesh_object('Wrapper bow-tie icon',[(-.010,-.034,.140),(-.021,-.034,.149),(-.021,-.034,.131),
                         (.010,-.034,.140),(.021,-.034,.149),(.021,-.034,.131)],[(0,1,2),(3,4,5)],make_material('amber')))
    elif kind=='marshmallows':
        for i in range(15 if ITERATION>=2 else 9):
            x=(i%3-1)*.027;z=.012+(i//3)*(.025 if ITERATION>=2 else .035)
            color=['milk','marshmallow_pink','mint'][(i+i//3)%3 if ITERATION>=2 else i%3]
            if ITERATION>=2:
                obj=lathe('Whole soft pastel marshmallow',[(z,.0108),(z+.0012,.012),(z+.0228,.012),(z+.024,.0108)],make_material(color),6)
                obj.location=(x,-.008,0)
                if ITERATION>=3:
                    center=z+.012
                    for v in obj.data.vertices:v.co.z-=center
                    obj.location.z=center-.004;obj.rotation_euler.y=.06*math.sin(i*2.4);apply_transform(obj)
                food.append(obj)
            else:food.append(cylinder('Whole pastel marshmallow',(x,-.008,z+.012),.012,.024,color,6))
        icons=[disk_front('Cloud label icon',x,-.044,.086+dz,.008,.005,'sage',8) for x,dz in [(-.010,0),(0,.004),(.010,0)]]
        if ITERATION>=2:
            for i,obj in enumerate(icons):obj.location.y-=i*.0002
    elif kind=='jelly_beans':
        for i in range(16 if ITERATION>=2 else 12):
            x=(i%4-1.5)*.023 if ITERATION>=2 else (i%3-1)*.027
            z=.020+(i//4)*.028 if ITERATION>=2 else .018+(i//3)*.028
            bean=ball('Whole colourful jelly bean',(x,-.008,z),(.012,.011,.014 if ITERATION>=2 else .009),['fruit_red','candy_yellow','purple','mint'][i%4],6,4)
            for v in bean.data.vertices:v.co.x+=.002*math.sin(v.co.z*170)
            if ITERATION>=3:
                bean.location.y+=.003*math.sin(i*2.4);bean.rotation_euler.z=.10*math.cos(i*1.7);apply_transform(bean)
            food.append(bean)
        icons=[disk_front('Whole bean icon',0,-.044,.085,.013,.006,'fruit_red',10)]
    else:
        for i,color in enumerate(('fruit_red','candy_yellow','purple')):
            x=(i-1)*.033
            stick=lathe('Whole lollipop stick',[(.002,.0011),(.003,.0015),(.142,.0015),(.144,.0011)],make_material('milk'),6)
            stick.location.x=x;food.append(stick)
            food.append(ball('Whole round lollipop',(x,0,.153),(.019,.007,.019),color,10,6))
        icons=sprig(-.044,.094,'sage','leaf',.6)
    # Closed paper popcorn has no transparent material: keep at least two
    # materials by retaining the package body and label separately.
    return finish_pack(parts,food,icons)

def ready_food(kind):
    final_palette();parts=pack_tray(False);parts.append(sleeve(.043));food=[]
    if kind=='burrito':
        for y in (-.027,.027):food.append(ball('Whole folded closed burrito',(0,y,.028),(.078,.021,.019),'ivory',10,6))
        if ITERATION>=2:
            for y in (-.027,.027):
                for side in (-1,1):
                    verts=[]
                    for x,dy in ((side*.057,-.010),(side*.069,-.003),(side*.069,.003),(side*.057,.010)):
                        z=.028+.019*math.sqrt(max(.1,1-(x/.078)**2-(dy/.021)**2))+.0003
                        verts.append((x,y+dy,z))
                    food.append(mesh_object('Closed tortilla fold',verts,[(0,1,2,3)],make_material('grain')))
    elif kind=='calzone':
        contour=[(.079*math.cos(i*math.pi/10),.052*math.sin(i*math.pi/10)-.018) for i in range(11)]
        obj=prism('Whole crescent calzone',contour,.008,.035,make_material('pastry'))
        if ITERATION>=2:puff_pastry(obj,.008)
        food.append(obj)
        for i in range(5):
            x=-.052+i*.026
            food.append(ball('Soft folded pastry seam',(x,-.018,.031),(.009,.004,.003),'pastry_light',6,4))
    elif kind=='samosas':
        for x,a in [(-.046,-.12),(.046,.12)]:
            obj=prism('Whole triangular samosa',[(-.039,-.031),(.039,-.031),(0,.041)],.008,.035,make_material('pastry'))
            if ITERATION>=2:puff_pastry(obj,.009)
            obj.location.x=x;obj.rotation_euler.z=a;apply_transform(obj);food.append(obj)
    elif kind=='spring_rolls':
        for y in (-.034,0,.034):
            roll=lathe('Whole crisp closed spring roll',[(-.070,.009),(-.067,.013),(.067,.013),(.070,.009)],make_material('pastry'),8)
            roll.rotation_euler.y=math.pi/2;roll.location=(0,y,.024);apply_transform(roll);food.append(roll)
            if ITERATION>=3:
                verts=[]
                for x,dy in ((.030,-.008),(.034,-.008),(.042,.008),(.038,.008)):
                    verts.append((x,y+dy,.024+.013*math.sqrt(1-(dy/.013)**2)+.0003))
                food.append(mesh_object('Closed crisp roll seam',verts,[(0,1,2,3)],make_material('pastry_light')))
    elif kind=='falafel':
        for x,y in [(-.045,-.027),(.045,-.027),(-.045,.027),(.045,.027)]:
            obj=make_sphere('Whole rounded falafel',(x,y,.029),(.025,.025,.023),make_material('falafel'),segments=10,rings=6,flatten=False,
                           deform=lambda q:tuple(v*(1+.06*math.sin(q.x*16+q.z*13)) for v in q))
            add_bevel(obj,.0003);food.append(obj)
    else:
        for x in (-.046,.046):
            rice=prism('Whole rounded onigiri',[(-.036,-.023),(.036,-.023),(0,.037)],.009,.039,make_material('milk'))
            if ITERATION>=3:puff_pastry(rice,.004)
            rice.location.x=x;food.append(rice)
            if ITERATION>=3:
                vertices=[]
                for yy in (-.0245,-.005,.0145):
                    for xx in (-.011,0,.011):
                        surface=.039+.004*max(0,1-max(abs(xx)/.036,abs((yy+.003)/.04)))
                        vertices.append((x+xx,yy,surface+.0007))
                nori=mesh_object('Soft nori following rice surface',vertices,[(j*3+i,j*3+i+1,(j+1)*3+i+1,(j+1)*3+i) for j in range(2) for i in range(2)],make_material('dark_green'))
                wall=nori.modifiers.new('Natural nori thickness','SOLIDIFY');wall.thickness=.0006;add_bevel(nori,.00012);food.append(nori)
            else:food.append(box('Nori strip on rice',(x,-.005,.040),(.022,.039,.0015),'dark_green'))
    icon='leaf' if kind in ('burrito','spring_rolls') else 'wheat' if kind=='calzone' else 'sesame' if kind=='falafel' else 'corn'
    return finish_pack(parts,food,ready_icon(kind) if ITERATION>=2 else sleeve_icon(icon))

def spread_jar(kind):
    final_palette();color={'chocolate_spread':'cocoa','almond_butter':'amber','cashew_butter':'grain','sunflower_seed_butter':'spice_gold'}[kind]
    glass='plastic'
    if ITERATION>=2:glass=color+'_spread_glass';PALETTE[glass]=PALETTE[color]
    shell=closed_shell('Shared hollow spread jar',[(0,.033),(.003,.036),(.078,.036),(.086,.033),(.091,.0315)],glass)
    filling=lathe('Separate visible spread',[(.003,.031),(.005,.034),(.077,.034),(.086,.030)],make_material(color),8)
    lid=cylinder('Dark green spread lid',(0,0,.096),.034,.010,'dark_green',10)
    if ITERATION>=3:
        apply_modifiers(lid)
        for v in lid.data.vertices:
            t=max(0,min(1,(v.co.z+.005)/.010));factor=1.02-.035*t
            v.co.x*=factor;v.co.y*=factor
    label=curved_label(.0365,.050,.056,.037,make_material('ivory'));icons=[]
    if kind=='chocolate_spread':
        for x in (-.006,.006):
            for z in (.045,.055):icons.append(box('Chocolate-bar moulded square',(x,-.038,z),(.010,.0015,.008),'cocoa'))
    elif kind=='almond_butter':icons=sprig(-.038,.050,'amber','almond',.9)
    elif kind=='cashew_butter':
        points=[(.010*math.cos(.7+i*4.8/6),-.038,.050+.013*math.sin(.7+i*4.8/6)) for i in range(7)]
        icons=[path_tube('Whole curved cashew icon',points,.003,'amber',6)]
        if ITERATION>=3:
            apply_modifiers(icons[0])
            for v in icons[0].data.vertices:
                if v.co.x<-.004:
                    angle=math.atan2((v.co.z-.050)/.013,v.co.x/.010)
                    cx=.010*math.cos(angle);cz=.050+.013*math.sin(angle)
                    v.co.x=cx+(v.co.x-cx)*1.25;v.co.z=cz+(v.co.z-cz)*1.10
                    v.co.y=-.038+(v.co.y+.038)*1.10
        if ITERATION>=2:
            for point in (points[0],points[-1]):icons.append(ball('Rounded whole cashew tip',point,(.003,.003,.003),'amber',6,3))
    else:
        for i in range(8):
            a=i*math.tau/8
            petal=disk_front('Sunflower petal',.010*math.cos(a),-.038-(i*.0001 if ITERATION>=2 else 0),.050+.010*math.sin(a),.005,.003,'cheese',8)
            if ITERATION>=2:petal.rotation_euler.y=-a;apply_transform(petal)
            icons.append(petal)
        icons.append(disk_front('Sunflower centre',0,-.039,.050,.006,.006,'cocoa',10))
    geometry_pigment([filling,lid]+icons)
    return [shell,filling,lid,label]+icons

def halva():
    final_palette();body=box('Whole sealed halva bar',(0,0,.015),(.12,.06,.026),'ivory');parts=[body];food=[]
    for side in (-1,1):
        rings=[]
        for x,depth,height in [(side*.058,.060,.022),(side*.066,.049,.009),(side*.075,.055,.006)]:
            if ITERATION>=2:
                top=[((j/5-.5)*depth,.015+height/2+(.0007*(j%2) if abs(x)>.065 else 0)) for j in range(6)]
                bottom=[((j/5-.5)*depth,.015-height/2-(.0007*(j%2) if abs(x)>.065 else 0)) for j in reversed(range(6))]
                rings.append([(x,y,z) for y,z in top+bottom])
            else:rings.append([(x+.0015*math.sin(i*math.pi/2),y,.015+z) for i,(y,z) in enumerate(outline(depth,height,.002))])
        parts.append(loft('Real crimped wrapper end',rings,make_material('ivory')))
    band=box('Green halva band',(0,0,.0285),(.055,.052,.001),'sage');parts.append(band)
    food.append(box('Whole pale block icon',(-.012,0,.030),(.018,.018,.0015),'grain'))
    icons=sleeve_icon('wheat','ivory',.030)
    for obj in icons:obj.location.x+=.014
    geometry_pigment(food+icons)
    return parts+food+icons

def fruit_leather():
    final_palette();parts=snack_pouch('ivory','sage',.20,True)
    for obj in parts:
        # Bake the shared rounded paper profile before flattening it into a
        # sachet; this preserves its two-segment bevel instead of rebeveling folds.
        apply_modifiers(obj)
        obj.location.x*=.75;obj.location.y*=.35;obj.location.z*=.75
        for v in obj.data.vertices:v.co.x*=.75;v.co.y*=.35;v.co.z*=.75
        if ITERATION>=2 and obj.name.startswith('Separate see-through'):
            PALETTE['fruit_window']=PALETTE['fruit_red'];film=make_material('fruit_window',.25)
            film.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.2;obj.data.materials[0]=film
    food=[]
    for x in (-.021,0,.021):
        top=.074 if ITERATION>=3 else .084
        roll=open_profile('Whole rolled fruit leather',[(.028,.008),(.029,.009),(top-.001,.009),(top,.008),
              (top,.005),(top-.001,.004),(.029,.004),(.028,.005)],'fruit_red',6)
        roll.location=(x,0,0);food.append(roll)
    icons=sprig(-.013,.114,'red','apple',.65)
    return finish_pack(parts,food,icons)

def arrange_expansion_preview(objects):layout_large_props(objects)
