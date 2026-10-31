"""Whole-food geometry, using the established Wave2 modelling/export helpers.

All dimensions are metres. Each builder returns editable separate components;
the shared exporter joins only the export copy, preserving material boundaries.
"""
def food_palette():
    PALETTE.update(orange='#D99852', russet='#AD735F', ivory='#E6DDC0',
                   purple='#986D8B', green='#8DA66B', darkleaf='#5F7952',
                   yellow='#D8BD67', red='#B65857', brown='#906249',
                   kraft='#BEA17D', pink='#C47E9B', russet_light='#BD8B75', green_light='#9FAE78')

def organic(name,pos,size,color,segments=10,rings=6,deform=None):
    food_palette()
    obj=make_sphere(name,pos,size,make_material(color),segments,rings,
                    deform=deform,flatten=False)
    add_bevel(obj)
    return obj

def tinted(obj,color,test):
    obj.data.materials.append(make_material(color))
    for face in obj.data.polygons:
        if test(face.center):face.material_index=len(obj.data.materials)-1
    return obj

def stem(a,b,r=.002,color='darkleaf'):
    if ITERATION<2:return bar_between('Soft organic stalk',a,b,r*2,color)
    food_palette();a,b=Vector(a),Vector(b);length=(b-a).length;edge=min(r*.4,length*.1)
    obj=lathe('Rounded constant-width stalk',[(0,r*.6),(edge,r),(length-edge,r),(length,r*.6)],make_material(color),6)
    obj.location=a;obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();apply_transform(obj)
    return obj

def leaf(pos,length=.035,width=.016,angle=0,color='green',segments=8):
    # A closed thin leaf with a raised midrib and curved silhouette.
    obj=organic('Curved plump leaf',pos,(width/2,length/2,.0018),color,segments,4,
                 lambda q:(q.x,q.y,q.z+.7*q.y*q.y))
    # Thin organic leaves have an elliptical, already rounded section. A tiny
    # boundary bevel avoids inflating fronds at this centimetre-scale thickness.
    obj.modifiers[-1].width=.00012
    obj.rotation_euler.z=angle;apply_transform(obj)
    return obj

def leaves(parts,z,count=5,length=.04):
    for i in range(count):
        a=i*math.tau/count
        top=(math.cos(a)*length*.65,math.sin(a)*length*.65,z+length*.65)
        parts.append(stem((0,0,z-.015),top,.002))
        blade=leaf(top,length,.018,a-math.pi/2)
        blade.rotation_euler.x=.35;apply_transform(blade);parts.append(blade)

def pumpkin():
    # Twenty radial samples preserve ten ribs rather than aliasing them away.
    def rib(q):
        a=math.atan2(q.y,q.x);r=1+.10*math.cos(10*a)
        return (q.x*r,q.y*r,q.z*.84-.055*max(q.z,0)**4)
    body=organic('Ten rounded pumpkin ribs',(0,0,.085),(.098,.093,.1),'orange',20,6,rib)
    tinted(body,'russet',lambda p:p.x>.075 and p.y>0)
    parts=[body,stem((0,0,.164),(.006,0,.194),.006)]
    if ITERATION>=2:
        parts.append(path_tube('Stem curl',[(.006,0,.190),(.017,0,.194),(.023,0,.186),(.019,0,.180)],.002,'darkleaf'))
    return parts

def sweet_potato():
    body=organic('Curved tapered sweet potato',(0,0,.028),(.078,.028,.03),'russet',12,8,
                  lambda q:(q.x,q.y*(.8+.2*(1-q.x*q.x)),q.z+.22*q.x*q.x))
    tinted(body,'russet_light',lambda p:p.z>.016 and p.x>.025)
    return [body]

def turnip():
    body=organic('Cream purple turnip',(0,0,.045),(.048,.047,.045),'ivory')
    tinted(body,'purple',lambda p:p.z>.005)
    parts=[body,path_tube('Curved root',[(0,0,.009),(.005,0,-.008),(.013,0,-.015)],.002,'ivory')]
    leaves(parts,.087,4,.025)
    return parts

def parsnip():
    parts=[]
    for x,a in [(-.022,-.18),(.023,.20)]:
        obj=organic('Tapered curved parsnip',(x,0,.022),(.019,.1,.021),'ivory',10,8,
            lambda q:(q.x*(.25+.75*(q.y+1)/2),q.y,q.z*(.25+.75*(q.y+1)/2)+.12*q.y*q.y))
        obj.rotation_euler.z=a;apply_transform(obj);parts.append(obj)
        parts.append(stem((x,.087,.02),(x,.098,.023),.003))
        if ITERATION>=2:
            parts.append(stem((x,-.08,.012),(x+.008,-.096,.012),.0007,'ivory'))
    return parts

def kohlrabi():
    parts=[organic('Kohlrabi bulb',(0,0,.040),(.048,.046,.04),'green')]
    leaves(parts,.071,6,.045)
    return parts

def fennel():
    parts=[]
    for i in range(5):
        x=(i-2)*.009
        parts.append(organic('Layered fennel bulb',(x,0,.034),(.019,.025,.035),'ivory',8,4))
    for i in range(4):
        a=i*math.tau/4;x=.022*math.cos(a);y=.015*math.sin(a)
        parts.append(stem((x*.3,y*.3,.05),(x,y,.130),.0025,'green'))
        for j in range(2):
            parts.append(leaf((x+(j-1)*.009,y,.13+j*.008),.033,.008,a,'darkleaf'))
    return parts

def leek():
    parts=[]
    for x in (-.017,.017):
        base=organic('White leek base',(x,0,.07),(.011,.012,.065),'ivory',8,4)
        tinted(base,'green',lambda p:p.z>.025);parts.append(base)
        for i in range(2):
            obj=leaf((x+(i-1)*.012,0,.19+i*.009),.15,.021,(i-1)*.14,'darkleaf')
            obj.rotation_euler.x=1.25;apply_transform(obj);parts.append(obj)
        parts.append(stem((x,0,.012),(x+.003,0,0),.0007,'ivory'))
    parts.append(tube_loop('Loose cream tie',(0,0,.115),(.031,.014),.0015,'ivory',8,6,'XY'))
    return parts

def celery():
    parts=[organic('Pale celery base',(0,0,.018),(.025,.02,.018),'ivory',8,4)]
    for i in range(7):
        a=i*math.tau/7;x=.026*math.cos(a);y=.018*math.sin(a)
        parts.append(stem((x*.25,y*.25,.02),(x,y,.215+(i%3)*.015),.005,'green'))
        if i%2==0:parts.append(leaf((x,y,.215+(i%3)*.015),.065,.03,a))
    return parts

def asparagus():
    parts=[]
    for i in range(8):
        a=i*2.399;r=.014*math.sqrt(i/7);x=r*math.cos(a);y=r*math.sin(a);h=.205+(i%3)*.006
        parts.append(stem((x,y,.005),(x,y,h-.014),.004,'green'))
        tip=organic('Overlapping purple spear tip',(x,y,h-.006),(.005,.005,.015),'darkleaf',8,4)
        tinted(tip,'purple',lambda p:p.z>.006);parts.append(tip)
    parts.append(tube_loop('Cream asparagus band',(0,0,.08),(.024,.024),.002,'ivory',8,6,'XY'))
    return parts

def artichoke():
    parts=[stem((0,0,.002),(0,0,.025),.007)]
    for j in range(2):
        for i in range(6):
            a=(i+j*.5)*math.tau/6;r=.027-j*.007
            obj=organic('Layered artichoke scale',(r*math.cos(a),r*math.sin(a),.04+j*.016),(.016,.013,.022),'green',6,4,
                lambda q:(q.x*(1-.25*max(q.z,0)),q.y,q.z))
            tinted(obj,'purple',lambda p:p.z>.014);parts.append(obj)
    return parts

def produce_tray(clear=False):
    # Rounded closed section: outer floor -> wall -> lip -> inner wall -> inner floor.
    color='plastic' if clear else 'kraft'
    if clear:clear_material()
    rings=[]
    for z,w,d in [(0,.12,.085),(.003,.14,.10),(.030,.14,.10),(.032,.138,.098),(.030,.134,.094),(.004,.115,.080)]:
        rings.append([(x,y,z) for x,y in outline(w,d,.006)])
    obj=loft('Hollow thick-wall punnet' if clear else 'Hollow kraft tray',rings,make_material(color))
    obj['hollow_opening']=True
    return obj

def sprouts():
    parts=[produce_tray()]
    for i in range(8):
        x=(i%4-1.5)*.030;y=(i//4-.5)*.034
        body=organic('Layered Brussels sprout',(x,y,.023),(.015,.015,.017),'green',8,4)
        tinted(body,'darkleaf',lambda p:p.x>0 and p.z<.005);parts.append(body)
        if ITERATION>=2 and i<7:parts.append(leaf((x-.008,y,.030),.024,.012,.6,'darkleaf'))
    return parts

def romaine():
    parts=[organic('Pale lettuce base',(0,0,.015),(.018,.02,.015),'ivory',8,4)]
    for i in range(7):
        a=i*2.399;r=.025*(1-i/12);h=.14+(i%3)*.02
        obj=organic('Wavy layered lettuce leaf',(r*math.cos(a),r*math.sin(a),h/2),(.027,.013,h/2),'green',8,4,
            lambda q:(q.x*(1+.10*math.sin(q.z*12)),q.y,q.z))
        if i<3:tinted(obj,'darkleaf',lambda p:p.z>.01)
        parts.append(obj)
    return parts

def fruit_group(color,radii,positions,shape=None,segments=10,rings=6):
    parts=[]
    for i,(x,y) in enumerate(positions):
        body=organic('Whole intact fruit',(x,y,radii[2]),radii,color,segments,rings,shape)
        tint='pink' if color=='purple' else 'green_light' if color=='green' else 'yellow'
        tinted(body,tint,lambda p:p.x>radii[0]*.65)
        a=(x,y,radii[2]*1.92);b=(x+.002,y,radii[2]*2+.004)
        twig=bar_between('Small rounded fruit twig',a,b,.003,'darkleaf') if min(radii)<.02 else stem(a,b,.0015)
        parts.extend([body,twig])
    return parts

def passion():return fruit_group('purple',(.030,.029,.03),[(-.025,-.014),(.026,-.008),(0,.035)])

def dragon():
    parts=[organic('Whole pink dragon fruit',(0,0,.042),(.045,.064,.042),'pink',12,8)]
    for i in range(12):
        a=i*2.399;z=.018+(i%4)*.016;r=.035*math.sqrt(max(.2,1-((z-.042)/.05)**2))
        q=math.sqrt(max(.1,1-((z-.042)/.042)**2))
        xradius=.044*q if ITERATION>=2 else r
        yradius=.061*q if ITERATION>=2 else .045
        obj=leaf((xradius*math.cos(a),yradius*math.sin(a),z),.028,.014,a-math.pi/2,'green')
        obj.rotation_euler.y=.7*math.cos(a);apply_transform(obj);parts.append(obj)
    return parts

def lychee():
    parts=[]
    for i in range(5):
        a=i*math.tau/5;x=.024*math.cos(a);y=.022*math.sin(a)
        body=organic('Whole bumpy unpeeled lychee',(x,y,.018),(.018,.017,.018),'red',10,6,
            lambda q:tuple(v*(1+.045*math.sin(q.x*24+q.z*17)) for v in q))
        tinted(body,'pink',lambda p:p.x>.012 and p.z>.005);parts.extend([body,stem((x,y,.034),(x+.002,y,.040),.001)])
        if ITERATION>=2:
            for j in range(3):
                b=a+j*2.1
                parts.append(ball('Small rounded lychee skin bump',(x+.014*math.cos(b),y+.014*math.sin(b),.027),(.003,.003,.003),'red',6,3))
    return parts

def guava():return fruit_group('green',(.040,.038,.042),[(0,0)],lambda q:(q.x*(1-.1*q.z),q.y,q.z))

def starfruit():
    food_palette();rings=[]
    # A five-ridged closed fruit, with bevelled ridges sampled at shoulder positions.
    for y,scale in [(-.05,.10),(-.043,.65),(-.025,1),(.025,1),(.043,.65),(.05,.10)]:
        ring=[]
        for i in range(20):
            a=i*math.tau/20;r=.025*(1 if i%4==0 else .64 if i%2 else .55)*scale
            ring.append((r*math.sin(a),y,.028+r*math.cos(a)))
        rings.append(ring)
    body=loft('Five rounded whole starfruit ridges',rings,make_material('yellow'))
    tinted(body,'green',lambda p:p.z>.047)
    return [body,stem((0,.046,.028),(0,.053,.029),.0015)]

def pomelo():return fruit_group('green',(.085,.082,.09),[(0,0)],lambda q:(q.x*(1-.12*q.z),q.y*(1-.12*q.z),q.z))

def figs():return fruit_group('purple',(.022,.022,.03),[(-.022,-.015),(.025,-.01),(0,.026)],lambda q:(q.x*(1-.28*q.z),q.y*(1-.28*q.z),q.z))

def dates():
    parts=[produce_tray()]
    for i in range(9):
        body=organic('Whole softly wrinkled date',((i%3-1)*.038,(i//3-1)*.026,.020),(.019,.011,.014),'brown',10,6,
                     lambda q:(q.x,q.y*(1+.07*math.sin(q.x*16)),q.z))
        tinted(body,'russet',lambda p:p.x>.012 and p.z>0);parts.append(body)
    return parts

def kumquats():
    parts=fruit_group('orange',(.015,.021,.015),[(-.029,-.02),(0,-.025),(.03,-.02),(-.014,.018),(.02,.022)],segments=8,rings=5)
    parts.append(leaf((-.014,.018,.030),.027,.013,.8,segments=6))
    return parts

def berries(kind):
    color={'gooseberries':'green','cranberries':'red','redcurrants':'red'}[kind]
    parts=[produce_tray(True)]
    for i in range(18 if kind=='redcurrants' else 15):
        x=(i%5-2)*.023;y=(i//5-1)*.027;z=.014+(i%2)*.004
        if ITERATION>=2:
            x+=.002*math.sin(i*2.4);y+=.003*math.cos(i*1.7)
            if kind=='redcurrants':
                a=(i%6)*math.tau/6
                x=(i//6-1)*.037+.017*math.cos(a);y=.021*math.sin(a)
                z=.016+.003*(i%2)
        body=organic('Whole berry',(x,y,z),(.010,.010,.011),color,6,4)
        tinted(body,'yellow' if kind=='gooseberries' else 'pink',lambda p:p.x>.007)
        parts.append(body)
        if kind=='redcurrants' and i%3==0:parts.append(stem((x,y,z+.010),(x+.012,y+.016,z+.018),.0007))
        if kind=='gooseberries' and ITERATION>=2:
            tinted(body,'darkleaf',lambda p:abs(p.x)<.002 and p.z>0)
    return parts

def arrange_expansion_preview(objects):layout_large_props(objects)

CATEGORY_FOLDER='Food_01C_TropicalFruit'
SCRIPT_NAME='BuildFoodTropical.py'
JOBS=[('passion_fruit',passion,1200),('dragon_fruit',dragon,1200),('lychee',lychee,1200),('guava',guava,700),('starfruit',starfruit,700),('pomelo',pomelo,700)]
if __name__=='__main__':run_category()
