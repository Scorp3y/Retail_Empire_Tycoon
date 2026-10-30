CATEGORY_FOLDER='Expansion_02_IceCreamDesserts'
SCRIPT_NAME='BuildExpansionDesserts.py'
ITERATION=2


def ice_cream_cone():
    expansion_palette()
    cone=hollow_vessel('Waffle cone with thick open rim',[(0,.002),(.003,.004),(.091,.028),(.095,.028),(.094,.025),(.008,.002)],'golden',8 if ITERATION>=2 else 10)
    parts=[cone]
    # Two crossing families of helical ribs form an actual diamond lattice.
    for direction in (-1,1):
        for i in range(4):
            points=[]
            steps=6 if ITERATION>=2 else 5
            for j in range(steps):
                t=.12+j*.86/(steps-1);a=i*math.tau/4+direction*t*(3.3 if ITERATION>=2 else 2.1);r=.002+.027*t
                points.append((r*math.cos(a),r*math.sin(a),t*.094))
            parts.append(rounded_rib('Raised waffle lattice',points,.0009 if ITERATION==1 else .0012,'cream'))
    for z,color,radius in ((.114,'mint',.031),(.158,'pink',.029)):
        parts.append(colored_ball('Faceted ice cream scoop',(0,0,z),(radius,radius,radius*.92),color,8 if ITERATION>=2 else 10,5))
        for i in range(2 if ITERATION>=2 else 3):
            a=i*math.tau/(2 if ITERATION>=2 else 3)+.4
            parts.append(colored_ball('Soft drippy scoop rim',(.022*math.cos(a),.022*math.sin(a),z-.019),(.010,.009,.008),color,6,3))
    return parts


def ice_cream_tub():
    expansion_palette();shape=outline(.096,.079,.012)
    rings=[]
    for z,scale in ((0,.80),(.003,.86),(.096,1),(.100,.99),(.098,.96),(.005,.79)):
        rings.append([(x*scale,y*scale,z) for x,y in shape])
    body=loft('Hollow cream tub with inner floor',rings,make_material('cream'));body['hollow_opening']=True
    parts=[body,box('Blue removable lid',(0,0,.105),(.103,.087,.013),'blue')]
    # The scoop icon is raised geometry, with a miniature wafer below it.
    parts.append(front_shape('Ice cream icon wafer',[(-.007,.054),(.007,.054),(0,.036)],-.041,'golden'))
    parts.append(colored_ball('Pink scoop icon',(0,-.041,.065),(.012,.0014,.012),'pink',8,4))
    if ITERATION>=2:
        parts.append(box('Blue lid lip',(0,0,.099),(.104,.088,.003),'blue'))
    return parts


def popsicle():
    expansion_palette()
    # The concave outline is a real bite cut through the ice, not a painted mark.
    contour=[(-.022,0),(.022,0),(.027,.006),(.027,.088),(.021,.090),(.014,.096),(.012,.104),
             (.015,.111),(.022,.115),(.018,.123),(.008,.129),(-.008,.129),(-.021,.122),(-.027,.111),(-.027,.006)]
    ice=prism('Rounded ice pop with missing bite',contour,-.013,.013,make_material('pink'))
    ice.rotation_euler.x=math.pi/2;ice.location.z=.041;apply_transform(ice)
    parts=[ice,box('Wooden ice pop stick',(0,0,.028),(.012,.008,.056),'cream')]
    if ITERATION>=2:
        parts.append(path_tube('Shallow raised ice groove',[(-.010,-.0135,.064),(-.010,-.0135,.101),(-.007,-.0135,.134)],.0012,'blush'))
    return parts


def cheesecake():
    expansion_palette();shape=[(-.048,.035),(.048,.035),(0,-.055)]
    parts=[prism('Biscuit base',shape,0,.008,make_material('kiwi_skin')),
           prism('Thick cream cheesecake layer',shape,.008,.047,make_material('cream')),
           prism('Thin pale top',shape,.047,.050,make_material('icing'))]
    for x,y,z,col,r in [(-.012,.018,.058,'cherry',.009),(.008,.021,.058,'cherry',.009),(.020,.005,.056,'plum',.007)]:
        parts.append(colored_ball('Berry topping',(x,y,z),(r,r,r),col,8,4))
    if ITERATION>=2:
        parts.append(colored_ball('Small berry at tip',(-.002,-.003,.056),(.006,.006,.006),'cherry',6,3))
    return parts


def macarons():
    expansion_palette();parts=[]
    for i,color in enumerate(('custard','mint','pink')):
        z=i*.023;dx=(.002 if i==1 else 0)
        for upper in (False,True):
            profile=[(z+.001,.018),(z+.004,.023),(z+.008,.024),(z+.010,.022)] if not upper else [(z+.013,.022),(z+.015,.024),(z+.020,.021),(z+.023,.015)]
            shell=lathe('Soft macaron shell',profile,make_material('cream'),8 if ITERATION>=2 else 12)
            shell.location.x=dx;pigment(shell,color);parts.append(shell)
        filling=lathe('Cream macaron filling',[(z+.009,.021),(z+.0095,.022),(z+.0135,.022),(z+.014,.021)],make_material('cream'),8 if ITERATION>=2 else 12)
        filling.location.x=dx;parts.append(filling)
        # A crinkled foot rim is real radial geometry, sharing shell colour.
        if ITERATION>=2:
            rim=tube_loop('Ruffled macaron foot',(dx,0,z+.013),(.022,.022),.0012,'cream',8,6,'XY')
            for v in rim.data.vertices:
                a=math.atan2(v.co.y,v.co.x-dx);v.co.z+=.0006*math.cos(a*12)
            parts.append(pigment(rim,color))
    return parts


def pudding():
    expansion_palette()
    parts=[oval_shell('Clear pudding cup',.034,.080),
           lathe('Yellow custard',[(.003,.026),(.006,.028),(.068,.031),(.070,.030)],make_material('custard'),12),
           cylinder('Caramel top layer',(0,0,.0715),.030,.003,'kiwi_skin',12)]
    if ITERATION>=2:
        parts.append(tube_loop('Thick clear drinking rim',(0,0,.080),(.033,.033),.001,'plastic',12,6,'XY'))
    return parts


JOBS=[('ice_cream_cone',ice_cream_cone,1200),('ice_cream_tub',ice_cream_tub,900),
      ('popsicle',popsicle,900),('cheesecake',cheesecake,900),('macarons',macarons,1200),('pudding',pudding,900)]
if __name__=='__main__':run_category()
