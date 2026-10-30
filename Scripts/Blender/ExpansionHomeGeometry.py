CATEGORY_FOLDER='Expansion_08_HomeComfort'
SCRIPT_NAME='BuildExpansionHome.py'
ITERATION=2


def arrange_expansion_preview(objects):layout_large_props(objects)


def candle():
    expansion_palette();PALETTE['candle_glass']=PALETTE['leaf'];PALETTE['flame']=PALETTE['custard']
    glass=make_material('candle_glass',.25);glass.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.2
    shell=hollow_vessel('Green glass candle jar',[(0,.027),(.003,.031),(.058,.031),(.062,.030),(.060,.028),(.005,.026)],'candle_glass',12)
    parts=[shell,cylinder('Cream wax',(0,0,.028),.027,.047,'cream',10)]
    wick=ball('Small candle wick',(0,0,.057),(.0008,.0008,.006),'cream',6,3);parts.append(pigment(wick,'ink'))
    flame=ball('Small soft glowing flame',(0,0,.071),(.004,.004,.012),'cream',8,4)
    for v in flame.data.vertices:
        if v.co.z>0:v.co.x*=.65;v.co.y*=.65
    mat=make_material('flame');shader=mat.node_tree.nodes['Principled BSDF']
    shader.inputs['Emission Color'].default_value=color_rgba(PALETTE['custard'])
    shader.inputs['Emission Strength'].default_value=1.5
    flame.data.materials.clear();flame.data.materials.append(mat);parts.append(flame)
    drop=[(0,.044),(-.006,.035),(-.008,.029),(-.004,.025),(.004,.025),(.008,.029),(.006,.035)]
    parts.append(front_shape('White drop on candle jar',drop,-.032,'icing'))
    if ITERATION>=2:
        # A separate upper wax pool provides a readable inset below the glass lip.
        parts.append(cylinder('Wax pool',(0,0,.052),.026,.002,'cream',10))
    return parts


def table_lamp():
    expansion_palette()
    parts=[cylinder('Round sage lamp base',(0,0,.011),.047,.022,'leaf',12),
           cylinder('Short sage lamp stem',(0,0,.107),.008,.174,'leaf',8)]
    shade=open_profile('Thin hollow cream lampshade',[(.182,.100),(.185,.102),(.337,.059),(.341,.057),
                       (.341,.054),(.337,.056),(.185,.099),(.182,.097)],'cream',12)
    parts.append(shade)
    parts.append(ball('Bulb under the shade',(0,0,.205),(.016,.016,.024),'cream',8,4))
    for side in (-1,1):parts.append(bar_between('Shade support arm',(0,0,.192),(side*.094,0,.188),.003,'leaf'))
    points=[(.032,0,.009),(.057,.003,.004),(.067,.018,.003),(.080,.023,.003)]
    parts.append(rounded_rib('Small electrical cord',points,.0015,'ink'))
    if ITERATION>=2:
        parts.append(box('Cord plug',(.083,.024,.004),(.010,.006,.006),'ink'))
    return parts


def alarm_clock():
    expansion_palette()
    body=cylinder('Blue round clock body',(0,0,0),.045,.034,'blue',12)
    body.rotation_euler.x=math.pi/2;body.location.z=.061;apply_transform(body)
    parts=[body,disk_front('Cream clock face',0,-.0175,.061,.040,.040,'cream',16),
           tube_loop('Rounded clock rim',(0,-.019,.061),(.041,.041),.0015,'blue',16,6,'XZ')]
    for i in range(12):
        a=i*math.tau/12
        tick=ball('Clock tick',(math.sin(a)*.032,-.0205,.061+math.cos(a)*.032),(.001,.0007,.0035),'ink',6,3)
        tick.rotation_euler.y=a;apply_transform(tick);parts.append(tick)
    parts += [rounded_rib('Hour hand',[(0,-.022,.061),(-.014,-.022,.072)],.0015,'ink'),
              rounded_rib('Minute hand',[(0,-.023,.061),(.016,-.023,.084)],.0012,'ink')]
    for side in (-1,1):
        parts.append(ball('Clock foot',(side*.027,0,.011),(.008,.009,.011),'blue',6,3))
        parts.append(bar_between('Bell support',(side*.023,0,.098),(side*.030,0,.109),.003,'ink'))
        parts.append(ball('Blue alarm bell',(side*.030,0,.113),(.015,.013,.010),'blue',8,4))
    parts.append(rounded_rib('Small bell hammer',[(0,0,.108),(0,0,.124)],.0018,'ink'))
    if ITERATION>=2:parts.append(ball('Hand pivot',(0,-.024,.061),(.003,.001,.003),'blue',6,3))
    return parts


def photo_frame():
    expansion_palette();parts=[]
    for side in (-1,1):
        parts.append(box('Thick wood side border',(side*.064,0,.090),(.016,.018,.180),'kiwi_skin'))
        parts.append(box('Thick wood cross border',(0,0,.090+side*.082),(.112,.018,.016),'kiwi_skin'))
    sky=box('Geometric blue sky',(0,-.002,.090),(.114,.003,.145),'cream');parts.append(pigment(sky,'blue'))
    hills=[(-.055,.021),(.055,.021),(.055,.070),(.030,.101),(.002,.064),(-.031,.112),(-.055,.077)]
    parts.append(front_shape('Layered green landscape',hills,-.0045,'leaf'))
    parts.append(colored_ball('Sun in picture',(-.025,-.0057,.137),(.009,.001,.009),'custard',8,4))
    support=box('Small rear frame stand',(0,.031,.065),(.044,.008,.132),'kiwi_skin')
    support.rotation_euler=Vector((0,-.041,.126)).to_track_quat('Z','Y').to_euler();apply_transform(support);parts.append(support)
    if ITERATION>=2:
        foreground=[(-.055,.021),(.055,.021),(.055,.042),(.010,.066),(-.022,.039),(-.055,.052)]
        parts.append(front_shape('Foreground landscape hill',foreground,-.0065,'dark_green'))
    return parts


def cushion():
    expansion_palette()
    def plump(q):
        x=math.copysign(abs(q.x)**.45,q.x);y=math.copysign(abs(q.y)**.45,q.y)
        pinch=1-.45*(abs(x)*abs(y))**5
        center=1-.11*math.exp(-(x*x+y*y)/.10) if ITERATION>=2 else 1
        return (x,y,q.z*pinch*center)
    body=make_sphere('Pillowy pinched cushion',(0,0,.055),(.150,.150,.054),make_material('leaf'),12,8,deform=plump,flatten=False)
    parts=[body,ball('Small centre button',(0,0,.104 if ITERATION>=2 else .109),(.007,.007,.002),'cream',8,4)]
    if ITERATION>=2:
        equator=sorted([v.co.copy() for v in body.data.vertices if abs(v.co.z)<.00001],key=lambda v:math.atan2(v.y,v.x))
        points=[(v.x*1.002,v.y*1.002,.055) for v in equator]
        points.append(points[0])
        parts.append(rounded_rib('Cushion perimeter seam',points,.001,'dark_green'))
    return parts


def blanket():
    expansion_palette()
    parts=[box('Lower blanket fold',(0,.004,.008),(.344,.214,.016),'terracotta'),
           box('Second blanket fold',(.002,.002,.024),(.348,.218,.016),'peach')]
    nx,ny=6,4;vertices=[]
    for layer in (0,1):
        for j in range(ny+1):
            for i in range(nx+1):
                u=i/nx*2-1;v=j/ny*2-1
                x=.175*u*(1-.03*abs(v)**8);y=.110*v*(1-.03*abs(u)**8)
                z=.032 if layer==0 else .048+.006*(1-u*u)*(1-v*v)
                vertices.append((x,y,z))
    stride=nx+1;layer_size=stride*(ny+1);faces=[];colors=[]
    for layer in (0,1):
        for j in range(ny):
            for i in range(nx):
                a=layer*layer_size+j*stride+i;faces.append((a,a+1,a+stride+1,a+stride));colors.append((i+j)%2 if layer else 0)
    perimeter=list(range(nx+1))+[j*stride+nx for j in range(1,ny+1)]+[ny*stride+i for i in range(nx-1,-1,-1)]+[j*stride for j in range(ny-1,0,-1)]
    for a,b in zip(perimeter,perimeter[1:]+perimeter[:1]):faces.append((a,b,b+layer_size,a+layer_size));colors.append(0)
    top=mesh_object('Real geometry checked textile',vertices,faces,make_material('terracotta'))
    top.data.materials.append(make_material('peach'))
    for face,index in zip(top.data.polygons,colors):face.material_index=index
    add_bevel(top);parts.append(top)
    strands=10 if ITERATION==1 else 11
    for i in range(strands):
        x=-.154+i*.308/(strands-1)
        parts.append(rounded_rib('Soft blanket fringe',[(x,-.107,.041),(x+.002,-.119,.035),(x,-.128,.028)],.0017,'cream'))
    return parts


JOBS=[('candle',candle,900),('table_lamp',table_lamp,900),('alarm_clock',alarm_clock,1200),
      ('photo_frame',photo_frame,900),('cushion',cushion,1200),('blanket',blanket,1200)]
if __name__=='__main__':run_category()
