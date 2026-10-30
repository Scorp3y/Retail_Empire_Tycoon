CATEGORY_FOLDER='Expansion_05_CleaningTools'
SCRIPT_NAME='BuildExpansionCleaning.py'
ITERATION=2


def arrange_expansion_preview(objects):
    layout_large_props(objects)


def broom():
    expansion_palette();parts=[]
    handle=cylinder('Wooden broom handle',(0,0,0),.0055,.336,'kiwi_skin',8)
    handle.location=(.020,0,.229);handle.rotation_euler.y=.10;apply_transform(handle);parts.append(handle)
    parts.append(box('Dark bristle binding',(0,0,.055),(.105,.030,.016),'dark_brown'))
    for i in range(5):
        x=(i-2)*.021
        shape=outline(.024,.029,.004)
        tuft=loft('Chunky angled bristle tuft', [[(x+u*scale+(i-2)*.002*(1-z/.05),v,z) for u,v in shape]
            for z,scale in ((.003,1.15),(.006,1.17),(.047,.85),(.051,.76))],make_material('custard'))
        parts.append(tuft)
    if ITERATION>=2:
        parts.append(box('Warm head inset',(0,-.016,.057),(.091,.0018,.007),'golden'))
    return parts


def mop():
    expansion_palette()
    parts=[cylinder('Blue mop handle',(0,0,.230),.0055,.335,'blue',8),
           box('Grey mop rope holder',(0,0,.060),(.057,.032,.018),'metal')]
    for i in range(6 if ITERATION==1 else 7):
        a=i*math.tau/(6 if ITERATION==1 else 7);c,s=math.cos(a),math.sin(a)
        points=[(.012*c,.012*s,.060),(.025*c,.025*s,.041),(.037*c,.035*s,.013),
                (.043*c,.041*s,.006),(.049*c,.046*s,.010)]
        parts.append(rounded_rib('Chunky cotton mop strand',points,.005,'icing'))
    return parts


def bucket():
    expansion_palette()
    body=hollow_vessel('Bucket with real inner floor',[(0,.085),(.004,.092),(.175,.121),(.184,.120),(.181,.115),(.006,.085)],'leaf',12)
    parts=[body,tube_loop('Thick rounded bucket lip',(0,0,.181),(.119,.119),.004,'leaf',12,6,'XY')]
    points=[(.120*math.cos(i*math.pi/12),.040*math.sin(i*math.pi/12),.166+.080*math.sin(i*math.pi/12)) for i in range(13)]
    parts.append(rounded_rib('Black wire bucket handle',points,.0035,'ink'))
    for x in (-.119,.119):parts.append(ball('Handle pivot',(x,0,.166),(.006,.005,.006),'ink',6,3))
    if ITERATION>=2:
        parts.append(rounded_rib('Rounded central handle grip',[(-.025,.040,.245),(0,.040,.247),(.025,.040,.245)],.005,'ink'))
    return parts


def dustpan():
    expansion_palette();shape=outline(.150,.120,.010)
    rings=[]
    for z,scale in ((0,.96),(.003,1),(.030,1),(.030,.95),(.005,.95)):
        rings.append([(x*scale,y*scale,(.008 if y<-.045 else z) if z==.030 else z) for x,y in shape])
    pan=loft('Hollow dustpan with low front lip',rings,make_material('terracotta'));pan['hollow_opening']=True
    parts=[pan,box('Short dustpan handle',(0,.102,.027),(.025,.096,.014),'terracotta'),
           tube_loop('Real hanging hole',(0,.169,.025),(.012,.021),.005,'terracotta',12,6,'XY')]
    parts.append(box('Pale sweep lip',(0,-.059,.006),(.138,.003,.003),'cream'))
    if ITERATION>=2:
        parts.append(box('Raised grip inset',(0,.105,.0348),(.012,.049,.001),'golden'))
    return parts


def cleaning_spray():
    expansion_palette()
    shell=hollow_vessel('Hollow cleaning bottle',[(0,.024),(.005,.037),(.150,.037),(.177,.013),(.199,.013),(.199,.010),(.175,.010),(.149,.034),(.007,.033)],'cream',10)
    parts=[shell,cylinder('Blue sprayer collar',(0,0,.201),.017,.012,'blue',8),
           box('Blue trigger head',(-.005,0,.230),(.056,.024,.025),'blue'),
           box('Blue spray nozzle',(-.034,0,.229),(.012,.026,.018),'blue'),
           rounded_rib('Curved trigger lever',[(-.010,0,.220),(-.012,0,.209),(-.022,0,.194)],.003,'blue'),
           curved_label(.0377,.096,.048,.058,make_material('blue'))]
    star=[(0,.118),(.004,.103),(.015,.097),(.004,.092),(0,.077),(-.004,.092),(-.015,.097),(-.004,.103)]
    parts.append(front_shape('White sparkle icon',star,-.039,'icing'))
    if ITERATION>=2:parts.append(colored_ball('Spray nozzle dark opening',(-.040,0,.229),(.0008,.005,.005),'dark_green',6,3))
    return parts


def trash_bags():
    expansion_palette();parts=[]
    for i in range(2):
        x=(i-.5)*.057;h=.100 if i==0 else .088
        roll=lathe('Rolled bag with hollow core',[(0,.024),(.002,.026),(h-.002,.026),(h,.024),
                   (h,.0035),(.002,.0035)],make_material('dark_green'),10)
        roll.location.x=x;parts.append(roll)
        points=[]
        for j in range(15):
            t=j/14;r=.004+.019*t;a=t*math.tau*2.0
            points.append((x+r*math.cos(a),r*math.sin(a),h+.0007))
        parts.append(rounded_rib('Raised spiral bag end',points,.0009,'leaf'))
        label=curved_label(.0265,h*.5,.031,.024,make_material('cream'));label.location.x=x;parts.append(label)
        if ITERATION>=2:
            parts.append(colored_ball('Green label mark',(x,-.027,h*.5),(.010,.0008,.003),'leaf',6,3))
    return parts


JOBS=[('broom',broom,1200),('mop',mop,1200),('bucket',bucket,900),('dustpan',dustpan,900),
      ('cleaning_spray',cleaning_spray,900),('trash_bags',trash_bags,900)]
if __name__=='__main__':run_category()
