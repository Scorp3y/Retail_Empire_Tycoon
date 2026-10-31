"""Reusable puffy packaging and whole-food pantry geometry, in metres.

Built on the previous Wave2 palette, bevel, primitive and export functions.
Opaque packaging has a genuine front opening: no paper hides the clear window.
"""
def pantry_palette():
    expansion_palette()
    PALETTE.update(kraft='#B7956F', ivory='#E9DFC5', amber='#C29055',
                   grain='#D6BE8D', olive='#819061', bean_red='#9D5650',
                   bean_black='#4D4543', red='#B96A5F', ochre='#C69B50',
                   sage='#778D69', blue='#839DA9', terracotta='#B88068',
                   charcoal='#484B43', purple='#857087', white='#E7E1CA')

def rounded_food(name,position,size,color,segments=8,rings=4):
    pantry_palette()
    obj=ball(name,position,size,color,segments,rings)
    add_bevel(obj,.0002)
    return obj

def sprig(y,z,color,kind='wheat',scale=1):
    """Raised primitives, never texture labels or expensive font geometry."""
    s=scale;parts=[]
    if kind=='coconut':
        # Whole brown coconut: the project deliberately excludes sliced icons.
        parts.append(rounded_food('Whole coconut icon',(0,y,z),(.012*s,.002*s,.015*s),color))
        for x,dz in ((-.004,.004),(.003,.006),(0,-.002)):
            parts.append(disk_front('Coconut eye',x*s,y-.0025*s,z+dz*s,.001*s,.001*s,'ivory',6))
        return parts
    if kind in ('apple','tomato','almond','corn'):
        size=(.013,.002,.012) if kind in ('apple','tomato') else (.008,.002,.016)
        body=disk_front('Whole '+kind+' icon',0,y,z,size[0]*s,size[2]*s,color,12)
        parts.append(body)
        parts.append(disk_front('Whole icon leaf',.005*s,y-.001*s,z+.014*s,.006*s,.003*s,color,8))
        return parts
    if kind=='maple':
        contour=[(-.012,z-.009),(-.010,z),(-.018,z+.008),(-.008,z+.007),(-.009,z+.019),
                 (-.003,z+.014),(0,z+.027),(.004,z+.014),(.011,z+.019),(.009,z+.007),
                 (.018,z+.008),(.010,z),(.012,z-.009),(0,z-.006)]
        return [front_shape('Maple leaf silhouette',[(x*s,z+(zz-z)*s) for x,zz in contour],y,color)]
    if kind=='grapes':
        for i in range(6):
            row=0 if i<3 else 1 if i<5 else 2
            x=((i if row==0 else i-3 if row==1 else 0)-(1 if row==0 else .5 if row==1 else 0))*.009*s
            parts.append(ball('Grape icon berry',(x,y,z-row*.008*s),(.005*s,.0018*s,.005*s),color,6,4))
        return parts
    if kind=='sesame':
        for x,dz in ((-.007,-.004),(0,.005),(.007,-.004)):
            parts.append(rounded_food('Sesame seed icon',(x*s,y,z+dz*s),(.003*s,.0015*s,.0045*s),color,6,3))
        return parts
    if kind=='leaf':
        return [disk_front('Whole basil leaf',0,y,z,.011*s,.017*s,color,12),
                bar_between('Leaf midrib',(0,y-.0004,z-.012*s),(0,y-.0004,z+.012*s),.0012*s,color)]
    parts.append(bar_between('Icon stalk',(0,y,z-.013*s),(0,y,z+.020*s),.002*s,color))
    count=3 if ITERATION>=2 else 2
    for i in range(count):
        for side in (-1,1):
            obj=disk_front('Thin rounded ear or leaf',side*.006*s,y,z+i*.009*s,.006*s,.003*s,color,8)
            parts.append(obj)
    return parts

def window_bag(body_color,band_color,content_color,kind,pouch=False):
    """One shared silhouette per packaging family, with a clear front panel.

    The front face is split at window height. Window polygons are removed from
    the paper shell and exported as a separate thin plastic component.
    """
    pantry_palette();clear_material()
    width=.12;depth=.064;shape=outline(width,depth,.009)
    profile=[(0,.80,.72),(.008,.95,.94),(.030,1,1.04),(.105,1.03,1.08),
             (.145,1,1),(.182,.88,.45 if not pouch else .72),(.195,.86,.22 if not pouch else .50)]
    rings=[[(x*sx,y*sy,z) for x,y in shape] for z,sx,sy in profile]
    vertices=[p for ring in rings for p in ring];faces=[tuple(reversed(range(8)))];window=[]
    for j in range(len(rings)-1):
        for i in range(8):
            face=(j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i)
            if i==0 and j==2:window.append(face)
            else:faces.append(face)
    faces.append(tuple((len(rings)-1)*8+i for i in range(8)))
    shell=mesh_object('Puffy pouch shell' if pouch else 'Pinched paper bag shell',vertices,faces,make_material(body_color))
    mod=shell.modifiers.new('Real paper thickness','SOLIDIFY');mod.thickness=.0006
    add_bevel(shell)
    glass=mesh_object('Separate see-through window',vertices,window,clear_material())
    mod=glass.modifiers.new('Thin plastic window','SOLIDIFY');mod.thickness=.0003
    parts=[shell,glass]
    front=-.036
    # Paper edge strips frame the actual opening without covering its centre.
    for x in (-.047,.047):
        border=mesh_object('Flush paper window edge',[(x-.0045,front,.030),(x+.0045,front,.030),
                   (x+.0045,front,.105),(x-.0045,front,.105)],[(0,1,2,3)],make_material(body_color))
        solid=border.modifiers.new('Paper edge thickness','SOLIDIFY');solid.thickness=.0006
        parts.append(border)
    band=box('Colour band above window',(0,front-.0005,.153 if pouch and ITERATION>=2 else .119),
             (.105,.002,.043 if pouch and ITERATION>=2 else .026),band_color)
    if pouch and ITERATION>=2:
        for v in band.data.vertices:
            z=v.co.z+band.location.z
            sy=next((sy0+(sy1-sy0)*(z-z0)/(z1-z0) for (z0,_,sy0),(z1,_,sy1) in zip(profile,profile[1:]) if z0<=z<=z1),.5)
            v.co.y=-depth*.5*sy-.001-band.location.y+v.co.y
    parts.append(band)
    parts.append(box('Folded zip seam' if pouch else 'Folded paper top',(0,0,.198),(.105,.016 if pouch else .011,.006),band_color if pouch else body_color))
    # Nine chunky mounds, in three staggered rows, rather than loose grains.
    for i in range(9):
        x=(i%3-1)*.030;y=-.020;z=.042+(i//3)*.025
        radii=(.015,.012,.016) if ITERATION>=2 else (.011,.010,.011)
        if kind in ('lentil','semolina'):radii=(.011,.010,.005 if pouch else .009)
        if kind in ('kidney','whitebean','blackbean'):radii=(.010,.009,.014)
        obj=rounded_food('Visible chunky grain mound',(x,y,z),radii,content_color,6,4 if kind=='lentil' else 3)
        if kind=='kidney':
            for v in obj.data.vertices:v.co.x+=.25*v.co.z*math.sin(v.co.z*120)
        if ITERATION>=3:
            # Offset the rows to break a mechanical egg-carton arrangement.
            obj.location.x+=.002*math.sin(i*2.4);obj.location.z+=.002*math.cos(i*1.7)
        parts.append(obj)
    if pouch:
        icon_y=-.030 if ITERATION>=2 else front-.002
        for x in (-.018,0,.018):parts.append(ball('Legume identity icon',(x,icon_y,.157),(.006,.002,.007),content_color,6,3))
        if ITERATION>=2:
            bulk=mesh_object('Hidden bulk behind visible legumes',[(0,-.006,.026),(0,-.006,.108),
                 (-.050,-.006,.067),(.050,-.006,.067),(0,-.027,.067),(0,.015,.067)],
                 [(0,2,4),(0,4,3),(0,3,5),(0,5,2),(1,4,2),(1,3,4),(1,5,3),(1,2,5)],make_material(content_color))
            parts.append(bulk)
            shader=clear_material().node_tree.nodes.get('Principled BSDF')
            shader.inputs['Specular IOR Level'].default_value=0
    else:
        icons=sprig(front+.001,.158,band_color,'corn' if kind=='polenta' else 'wheat',1 if ITERATION>=2 else .65)
        if ITERATION>=2:
            for part in icons:
                for v in part.data.vertices:
                    z=v.co.z+part.location.z
                    sy=next((sy0+(sy1-sy0)*(z-z0)/(z1-z0) for (z0,_,sy0),(z1,_,sy1) in zip(profile,profile[1:]) if z0<=z<=z1),.22)
                    v.co.y=-depth*.5*sy-.001-part.location.y
        parts+=icons
    return parts

GRAIN_SPEC=[('quinoa','ivory','red','grain'),('bulgur','kraft','terracotta','amber'),
            ('millet','ivory','ochre','ochre'),('pearl_barley','ivory','sage','grain'),
            ('semolina','ivory','red','grain'),('polenta','kraft','ochre','ochre')]
LEGUME_SPEC=[('chickpeas','ivory','ivory','amber'),('green_lentils','ivory','sage','olive'),
             ('red_kidney_beans','ivory','red','bean_red'),('white_beans','ivory','ivory','white'),
             ('black_beans','ivory','charcoal','bean_black'),('dried_peas','ivory','sage','sage')]
def grain(index):
    name,body,band,food=GRAIN_SPEC[index]
    return window_bag(body,band,food,name)
def legume(index):
    name,body,band,food=LEGUME_SPEC[index]
    kind=['pea','lentil','kidney','whitebean','blackbean','pea'][index]
    return window_bag(body,band,food,kind,True)

def closed_shell(name,profile,color='plastic',segments=10):
    pantry_palette()
    material=clear_material() if color=='plastic' else make_material(color,.25)
    if color!='plastic':material.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.2
    inner=[(max(.003,z),max(.003,r-.0012)) for z,r in profile]
    obj=lathe(name,profile+list(reversed(inner)),material,segments)
    obj['hollow_opening']=True
    return obj

def jar_food(name,height,radius,content,lid_color,icon,icon_color):
    pantry_palette()
    shell=closed_shell('True hollow '+name,[(0,radius*.90),(.003,radius),(height*.76,radius),
                        (height*.86,radius*.92),(height*.90,radius*.88)])
    filling=lathe('Separate visible contents',[(.003,radius*.85),(.005,radius-.002),
                   (height*.74,radius-.002),(height*.83,radius*.86)],make_material(content),8)
    cap=cylinder('Rounded lid',(0,0,height*.95),radius*.94,height*.10,lid_color,10)
    # Label shares the filling colour where possible to respect four materials.
    parts=[shell,filling,cap,curved_label(radius+.0005,height*.45,radius*1.55,height*.32,make_material('ivory'))]
    parts+=sprig(-radius-.001,height*.45,icon_color,icon,.9 if ITERATION>=3 else .65)
    return parts

def can_food(height,radius,label,icon,icon_color,lower=None):
    pantry_palette()
    # Rim colour doubles as cream label ink; no separate texture or hidden fill.
    profile=[(0,radius*.94),(.001,radius),(.004,radius),(.005,radius*.95),
        (height-.005,radius*.95),(height-.004,radius),(height-.001,radius),(height,radius*.94)]
    if ITERATION>=2:profile += [(height-.001,radius*.90),(.004,radius*.90),(.003,radius*.86)]
    body=lathe('Rounded ribbed can',profile,make_material('ivory'),12)
    if ITERATION>=2:body['hollow_opening']=True
    band=soft_cylinder('Continuous label wrap',radius*.98,height*.105,height*.895,make_material(label),12)
    parts=[body,band]
    # Full band above is a separate cylinder and intentionally follows the can.
    if lower:parts.append(soft_cylinder('Lower blue band',radius*.985,.006,.022,make_material(lower),12))
    if ITERATION>=2:
        parts.append(soft_cylinder('Separate sealed tin lid',radius*.94,height-.001,height+.001,make_material('ivory'),10))
    if icon=='tomato' and ITERATION>=2:
        parts.append(disk_front('Cream tomato medallion',0,-radius-.0007,height*.54,.017,.017,'ivory',16))
        parts+=sprig(-radius-.0012,height*.54,'red',icon,.85)
    else:parts+=sprig(-radius-.0005,height*.54,icon_color,icon,.85 if ITERATION>=2 else .65)
    return parts

def carton(kind):
    pantry_palette();color='sage' if kind=='oat' else 'ivory';accent='ochre' if kind=='oat' else 'terracotta'
    shape=outline(.075,.063,.005);rings=[]
    for z,sx,sy in [(0,.9,.9),(.003,1,1),(.151,1,1),(.183,.96,.18),(.194,.96,.15)]:
        rings.append([(x*sx,y*sy,z) for x,y in shape])
    parts=[loft('Gable-top carton',rings,make_material(color)),
           box('Folded top seam',(0,0,.195),(.074,.009,.006),color)]
    if kind=='almond':parts.append(box('Terracotta lower panel',(0,-.032,.028),(.065,.0015,.047),accent))
    cap=cylinder('Small screw cap',(.019,-.014,.177),.009,.008,'ivory',10)
    cap.rotation_euler.x=.4;apply_transform(cap);parts.append(cap)
    if ITERATION>=3:
        collar=cylinder('Screw-cap seating ring',(.019,-.014,.173),.0105,.002,'ivory',10)
        collar.rotation_euler.x=.4;apply_transform(collar);parts.append(collar)
    parts+=sprig(-.033,.097,accent,'almond' if kind=='almond' else 'wheat',1.0)
    return parts

def bottle(kind):
    pantry_palette();maple=kind=='maple';height=.18 if kind in ('maple','molasses') else .20
    radius=.033 if maple else .025
    liquid='amber' if kind in ('maple','apple') else 'dark_brown'
    lid='charcoal' if maple else 'dark_brown'
    icon={'maple':'maple','molasses':'leaf','apple':'apple','balsamic':'grapes'}[kind]
    ink='red' if kind in ('maple','apple') else 'purple' if kind=='balsamic' else 'dark_brown'
    profile=[(0,radius*.85),(.003,radius),(height*.65,radius),(height*.81,.012),(height*.91,.011)]
    glass='plastic'
    if ITERATION>=2:
        glass=liquid+'_glass';PALETTE[glass]=PALETTE[liquid]
    parts=[closed_shell('Hollow bottle',profile,glass),
           lathe('Visible syrup or vinegar',[(.003,radius*.82),(.005,radius-.002),(height*.64,radius-.002),
                 (height*(.88 if ITERATION>=2 else .79),.010)],make_material(liquid),10),
           cylinder('Sealed screw cap',(0,0,height*.955),.013,height*.09,lid,10),
           curved_label(radius+.0006,height*.42,radius*1.55,height*.30,make_material('ivory'))]
    parts+=sprig(-radius-.001,height*.42,ink,icon,.75)
    if maple:
        handle=tube_loop('Real through handle',(.030 if ITERATION>=3 else .038,0,.132),(.012,.017),.003,glass,10,6,'XZ')
        handle.data.materials[0]=parts[0].data.materials[0];parts.append(handle)
    # A single matte vertex-colour ink material preserves the black cap and red
    # or purple print while keeping the bottle at four materials, without textures.
    print_material=bpy.data.materials.get('GeometryPigment')
    if print_material is None:
        print_material=bpy.data.materials.new('GeometryPigment');print_material.use_nodes=True
        shader=print_material.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Roughness'].default_value=.82;shader.inputs['Metallic'].default_value=0
        vertex=print_material.node_tree.nodes.new('ShaderNodeVertexColor');vertex.layer_name='Color'
        print_material.node_tree.links.new(vertex.outputs['Color'],shader.inputs['Base Color'])
    for part in parts:
        key=lid if part.name.startswith('Sealed screw cap') else ink if part in parts[4:-1 if maple else None] else None
        if key is None:continue
        rgba=color_rgba(PALETTE[key]);attribute=part.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
        for item in attribute.data:item.color=rgba
        part.data.materials.clear();part.data.materials.append(print_material)
    return parts

def hummus():
    pantry_palette();parts=[closed_shell('Hollow hummus tub',[(0,.046),(.003,.051),(.040,.060),(.044,.060)],'plastic',12),
        lathe('Separate hummus',[(.003,.044),(.006,.049),(.037,.056),(.040,.054)],make_material('ivory'),12)]
    # Intact food: only the packaging opens, as specifically requested here.
    lid=cylinder('Green removable lid',(.117,.025,.015) if ITERATION>=2 else (.055,.025,.060),.061,.005,'sage',12)
    lid.rotation_euler.x=.20 if ITERATION>=2 else .65;apply_transform(lid);parts.append(lid)
    points=[]
    for i in range(19):
        t=i/18;a=t*math.tau*1.7;r=.004+.027*t;points.append((r*math.cos(a),r*math.sin(a),.041))
    topping='grain' if ITERATION>=3 else 'amber'
    parts.append(path_tube('Hummus swirl',points,.0014,topping,6))
    for x,y in ((-.023,-.012),(.017,.022),(.022,-.017)):
        parts.append(rounded_food('Whole chickpea topping',(x,y,.042),(.005,.005,.004),topping,6,3))
    parts.append(curved_label(.057,.023,.049,.021,make_material('ivory')))
    parts+=sprig(-.058,.023,topping,'sesame',.85 if ITERATION>=3 else .65)
    return parts

def arrange_expansion_preview(objects):
    layout_large_props(objects)

"""Food Expansion part three: shared packaging and edible silhouettes, metres."""
def part3_palette():
    pantry_palette()
    PALETTE.update(milk='#ECE6D0', wax='#B45E57', cheese='#DDC88C',
        silver='#B4BDB5', fish_blue='#718D9C', fish_dark='#526C77',
        fish_pink='#BE8D87', poultry='#D6B8A1', duck='#DBC6A7',
        squid_pink='#D5B7AA', spice_gold='#C79D55', spice_red='#B46B54')

def geometry_pigment(parts):
    """Reuse the project's vertex-colour shader contract, without image textures."""
    mat=bpy.data.materials.get('GeometryPigment')
    if mat is None:
        mat=bpy.data.materials.new('GeometryPigment');mat.use_nodes=True
        shader=mat.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Roughness'].default_value=.82;shader.inputs['Metallic'].default_value=0
        vertex=mat.node_tree.nodes.new('ShaderNodeVertexColor');vertex.layer_name='Color'
        mat.node_tree.links.new(vertex.outputs['Color'],shader.inputs['Base Color'])
    for obj in parts:
        attribute=obj.data.color_attributes.get('Color') or obj.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
        colors=[m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value[:] for m in obj.data.materials]
        for face in obj.data.polygons:
            for loop in face.loop_indices:attribute.data[loop].color=colors[face.material_index]
            face.material_index=0
        obj.data.materials.clear();obj.data.materials.append(mat)
    return parts

def readable_label(word,radius,z,width,height,parts,fallback='leaf',color='sage',flat=False,max_triangles=180):
    """Use actual font meshes only when their triangle cost stays modest."""
    text=label_text(word,radius,z,width,height)
    text.data.calc_loop_triangles()
    if len(text.data.loop_triangles)<=max_triangles:
        if flat:
            for v in text.data.vertices:v.co.y=-radius-.001
        return [text]
    bpy.data.objects.remove(text,do_unlink=True)
    return sprig(-radius-.001,z,color,fallback,.7)

def dairy_tub(kind):
    part3_palette();radius=.048;height=.070
    accent='blue' if kind=='ricotta' else 'sage'
    body=hollow_vessel('Thick hollow dairy tub',[(0,.041),(.003,.044),(.059,radius),(.064,radius),
         (.067,.046),(.065,.044),(.007,.039)],'ivory',10)
    band=lathe('Coloured rounded lid band',[(.059,.048),(.0595,.0485),(.0645,.0485),(.065,.048)],make_material(accent),10)
    parts=[body,band]
    filling=lathe('Separate creamy filling',[(.006,.038),(.009,.040),(.061,.044),(.064,.037)],make_material('milk'),10)
    parts.append(filling)
    # Closed clear lid lets the swirl show while keeping the food intact.
    lid=lathe('Clear intact dairy lid',[(.066,.0445),(.0663,.045),(.0677,.045),(.068,.0445)],clear_material(),10)
    parts.append(lid)
    points=[]
    for i in range(13):
        t=i/12;a=t*math.tau*1.5;r=.003+.024*t
        points.append((r*math.cos(a),r*math.sin(a),.064))
    parts.append(path_tube('Soft ricotta or mascarpone swirl',points,.0016,'milk',6))
    parts.append(curved_label(.046,.034,.067,.025,make_material('ivory')))
    ink=readable_label(kind.title(),.047,.034,.055,.014,parts,'leaf',accent)
    # Milk and ivory are intentionally one warm family; pigment keeps lid band,
    # lettering and cream variations inside the four-material budget.
    opaque=[p for p in parts if p is not lid]+ink
    geometry_pigment(opaque)
    return parts+ink

def mozzarella():
    part3_palette()
    shape=outline(.090,.065,.010);rings=[]
    for z,sx,sy in [(0,.75,.75),(.005,.95,.95),(.045,1.04,1.08),(.096,.96,1),(.122,.25,.24),(.135,.40,.35)]:
        rings.append([(x*sx,y*sy,z) for x,y in shape])
    film=clear_material()
    if ITERATION>=2:
        PALETTE['mozzarella_film']=PALETTE['plastic'];film=make_material('mozzarella_film',.25)
        shader=film.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.2
        shader.inputs['Specular IOR Level'].default_value=0
    bag=loft('Puffy clear mozzarella bag',rings,film)
    solid=bag.modifiers.new('Thin bag wall','SOLIDIFY');solid.thickness=.00035
    parts=[bag]
    for i in range(6):
        x=(i%2-.5)*.032;y=(-.012 if i<4 else .013);z=.025+(i//2)*.028
        parts.append(rounded_food('Whole mozzarella ball',(x,y,z),(.018,.017,.017),'milk',8,4))
    parts.append(tube_loop('Sage bag tie',(0,0,.121),(.014,.009),.0015,'sage',8,6,'XY'))
    if ITERATION>=3:
        for side in (-1,1):
            parts.append(ball('Soft tied ribbon loop',(side*.010,-.008,.123),(.009,.004,.002),'sage',8,4))
    parts.append(box('Cream mozzarella label',(0,-.038,.068),(.072,.001,.027),'ivory'))
    text=readable_label('Mozzarella',.039,.068,.062,.012,parts,'leaf','sage',flat=ITERATION>=2)
    geometry_pigment(parts[1:]+text)
    return parts+text

def feta():
    part3_palette()
    rings=[[(x,y,z) for x,y in outline(w,d,.008)] for z,w,d in
           [(0,.100,.082),(.004,.109,.091),(.058,.12,.10),(.063,.12,.10),(.062,.116,.096),(.006,.096,.078)]]
    tub=loft('True hollow square feta tub',rings,make_material('ivory'));tub['hollow_opening']=True
    parts=[tub,box('Green feta lid',(0,0,.066),(.123,.103,.007),'sage'),
           box('Whole feta block inside',(0,0,.025),(.085,.067,.035),'milk'),
           box('Cream front label',(0,-.050,.034),(.080,.001,.029),'ivory'),
           box('Green cube icon',(-.027,-.052,.035),(.012,.001,.012),'sage')]
    ink=readable_label('Feta',.051,.034,.042,.014,parts,flat=ITERATION>=2)
    for p in ink:
        for v in p.data.vertices:v.co.x+=.010
    return parts+ink

def cheese_wheel(kind):
    part3_palette();red=kind=='gouda';color='wax' if red else 'ivory';h=.038 if red else .029
    wheel=lathe('Whole uncut '+kind+' wheel',[(0,.061),(.002,.067),(.007,.070),
              (h-.006,.070),(h-.002,.067),(h,.061)],make_material(color),16)
    if ITERATION>=3:
        PALETTE['wax_light']='#BF7569'
        wheel.data.materials.append(make_material('wax_light' if red else 'milk'))
        for face in wheel.data.polygons:
            if face.center.z>h-.004:face.material_index=1
    parts=[wheel,cylinder('Round cheese label',(0,0,h+.0005),.035,.0007,'cheese' if red else 'ivory',12)]
    label=label_text(kind.title(),5,0,.054,.014)
    # Turn the existing curved-text helper into a flat top label.
    for v in label.data.vertices:
        x,z=v.co.x,v.co.z;v.co=(x,z,h+.001)
    label.data.calc_loop_triangles()
    if len(label.data.loop_triangles)>220:
        bpy.data.objects.remove(label,do_unlink=True)
        icon=ball('Sage leaf on whole cheese',(0,0,h+.001),(.012,.020,.0007),'sage',8,4);parts.append(icon)
    else:parts.append(label)
    return parts

def meat_tray():
    """One real hollow tray and one clear tall lid shared by all six products."""
    part3_palette()
    rings=[]
    for z,w,d in [(0,.198,.130),(.003,.21,.14),(.025,.22,.15),(.028,.219,.149),
                  (.026,.214,.144),(.005,.194,.126)]:
        rings.append([(x,y,z) for x,y in outline(w,d,.009)])
    tray=loft('Shared thick hollow protein tray',rings,make_material('charcoal'));tray['hollow_opening']=True
    glass=clear_material();glass.node_tree.nodes.get('Principled BSDF').inputs['Specular IOR Level'].default_value=0
    lid_rings=[[(x,y,z) for x,y in outline(w,d,.009)] for z,w,d in
              [(.026,.220,.150),(.061 if ITERATION>=2 else .070,.214,.144),(.068 if ITERATION>=2 else .077,.195,.125)]]
    lid=loft('Closed transparent tray lid',lid_rings,glass,cap_start=False)
    if ITERATION<2:
        solid=lid.modifiers.new('Real film thickness','SOLIDIFY');solid.thickness=.0003
    card=box('Cream label card',(0,-.075,.018),(.065,.001,.020),'ivory')
    return [tray,lid,card]

def bird_food(kind):
    part3_palette();color='poultry' if kind=='turkey' else 'duck'
    size=(.064,.032,.025) if kind=='duck' and ITERATION>=2 else (.058,.040,.029)
    parts=[rounded_food('Whole plump bird',(0,0,.035),size,color,12,6)]
    if ITERATION>=3:
        PALETTE['poultry_blush']='#CBA99B'
        parts[0].data.materials.append(make_material('poultry_blush'))
        for face in parts[0].data.polygons:
            if face.center.z>.012 and face.center.x<0:face.material_index=1
    for side in (-1,1):
        thigh=rounded_food('Whole drumstick',(side*.048,-.025,.026),(.023,.017,.017),color,8,4)
        thigh.rotation_euler.z=side*.45;apply_transform(thigh);parts.append(thigh)
        parts.append(rounded_food('Rounded drumstick tip',(side*.067,-.039,.024),(.009,.008,.009),'ivory',6,4))
        wing=rounded_food('Folded whole wing',(side*.045,.022,.034),(.018,.012,.015),color,8,4)
        wing.rotation_euler.z=side*.40;apply_transform(wing);parts.append(wing)
    parts.append(rounded_food('Tucked neck',(-.063,.007,.030),(.014,.014,.016),color,8,4))
    return parts

def fish_food(kind):
    part3_palette();parts=[]
    body=rounded_food('Whole fish body',(0,0,.028),(.071,.020,.020),'silver',12,6)
    body.data.materials.append(make_material('fish_blue'))
    body.data.materials.append(make_material('fish_pink' if kind=='trout' else 'fish_dark'))
    for face in body.data.polygons:
        if face.center.z>.004:face.material_index=1
        elif abs(face.center.z)<.004 and kind=='trout':face.material_index=2
    parts.append(body)
    parts.append(rounded_food('Fish head',(-.072,0,.028),(.018,.018,.018),'fish_blue',8,4))
    for side in (-1,1):
        tail=rounded_food('Rounded tail fin',(.079,side*.014,.028),(.018,.014,.003),'fish_dark',8,4)
        tail.rotation_euler.z=side*.5;apply_transform(tail);parts.append(tail)
        parts.append(disk_front('Fish eye',-.080,side*.017,.034,.0023,.0023,'charcoal',8))
    parts.append(rounded_food('Dorsal fin',(.005,0,.046),(.020,.004,.009),'fish_dark',8,4))
    if kind=='mackerel':
        for x in (-.044,-.025,-.006,.013,.032):
            vertices=[];f=math.sqrt(1-(x/.071)**2)
            for angle in (-1.1,-.55,0,.55,1.1):
                for dx in (-.0012,.0012):
                    vertices.append((x+dx,.020*f*math.sin(angle),.028+.020*f*math.cos(angle)+.0005))
            stripe=mesh_object('Flush blue-green back stripe',vertices,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(4)],make_material('fish_dark'))
            solid=stripe.modifiers.new('Thin real stripe thickness','SOLIDIFY');solid.thickness=.0002
            parts.append(stripe)
    else:
        for i in range(7):
            x=-.042+i*.012
            parts.append(ball('Trout back spot',(x,-.011,.042),(.002,.0018,.001),'fish_dark',6,3))
        if ITERATION>=3:
            vertices=[]
            for x in (-.052,-.025,.025,.052):
                y=-.020*math.sqrt(1-(x/.071)**2)-.0005
                vertices.extend([(x,y,.026),(x,y,.030)])
            parts.append(mesh_object('Real pink trout side stripe',vertices,[(i*2,i*2+2,i*2+3,i*2+1) for i in range(3)],make_material('fish_pink')))
    return parts

def mussels_food():
    part3_palette();parts=[]
    for i in range(8):
        x=(i%4-1.5)*.043;y=(i//4-.5)*.045
        shell=rounded_food('Whole closed mussel',(x,y,.022),(.025,.016,.014),'fish_dark',8,4)
        shell.data.materials.append(make_material('fish_blue'))
        for face in shell.data.polygons:
            if abs(face.center.y)<.004:face.material_index=1
        if ITERATION>=3:
            for v in shell.data.vertices:
                q=v.co.x/.025;v.co.y*=.80-.25*q;v.co.z*=.90-.10*q
        shell.rotation_euler.z=(i%3-1)*.18;apply_transform(shell);parts.append(shell)
    return parts

def squid_food():
    part3_palette();parts=[rounded_food('Whole squid mantle',(.030,0,.030),(.048,.024,.022),'squid_pink',10,6),
                          rounded_food('Squid head',(-.020,0,.027),(.020,.019,.017),'squid_pink',8,4)]
    for side in (-1,1):
        fin=rounded_food('Squid fin',(.044,side*.025,.029),(.028,.013,.003),'squid_pink',8,4)
        fin.rotation_euler.z=side*.4;apply_transform(fin);parts.append(fin)
        parts.append(disk_front('Squid eye',-.025,side*.018,.031,.0025,.0025,'charcoal',8))
    for i in range(8):
        y=(i-3.5)*.004;length=.061 if i in (0,7) else .044
        end=(-.025-length,y*2+.012,.019)
        arm=path_tube('Chunky curved squid arm',[(-.030,y,.023),(-.049,y*1.5,.016),
                     (-.030-length,y*2+.006*math.sin(i),.013),end],.002,'squid_pink',6)
        # The hexagonal organic tube already has gentle 60-degree facets. A
        # real small ball rounds its exposed tip instead of beveling every bend.
        arm.modifiers.clear();parts.append(arm)
        parts.append(ball('Rounded squid arm tip',end,(.002,.002,.002),'squid_pink',6,3))
    return parts

def protein_icon(kind):
    y=-.077;z=.018;color='fish_dark'
    if kind in ('turkey','duck'):
        parts=[disk_front('Bird body icon',-.003,y,z,.010,.005,color,12),
               disk_front('Bird head icon',.010,y,z+.005,.004,.004,color,8)]
        tail=.007 if kind=='turkey' else .003
        parts.append(mesh_object('Bird beak and tail',[(.013,y,z+.005),(.019,y,z+.004),(.013,y,z+.002),
                      (-.011,y,z),(-.019,y,z+tail),(-.018,y,z-tail)],[(0,1,2),(3,4,5)],make_material(color)))
        return parts
    if kind in ('trout','mackerel'):
        parts=[disk_front('Whole fish icon',-.003,y,z,.012,.0045,color,12)]
        parts.append(mesh_object('Fish tail icon',[(.007,y,z),(.018,y,z+.006),(.018,y,z-.006)],[(0,1,2)],make_material(color)))
        return parts
    if kind=='mussels':return [disk_front('Whole shell icon',0,y,z,.013,.006,color,12)]
    parts=[disk_front('Whole squid icon',.006,y,z,.010,.005,color,8)]
    vertices=[];faces=[]
    for i in range(8):
        zz=z+(i-3.5)*.0012;n=len(vertices)
        vertices.extend([(-.002,y,zz),(-.017,y,zz+(i-3.5)*.001),(-.016,y,zz+(i-3.5)*.001+.0007),(-.002,y,zz+.0007)])
        faces.append((n,n+1,n+2,n+3))
    parts.append(mesh_object('Eight squid arms icon',vertices,faces,make_material(color)))
    return parts

def protein(kind):
    parts=meat_tray()
    food=bird_food(kind) if kind in ('turkey','duck') else fish_food(kind) if kind in ('trout','mackerel') else mussels_food() if kind=='mussels' else squid_food()
    # Food, spots, fins and label ink share the existing vertex-colour material.
    icon=protein_icon(kind) if ITERATION>=2 else [disk_front('Product identity silhouette',0,-.077,.019,.015,.006,'fish_dark',10)]
    if kind=='mackerel':icon=readable_label('Mackerel',.077,.018,.055,.012,food,'leaf','fish_dark',flat=ITERATION>=2)
    geometry_pigment(food+icon)
    return parts+food+icon

def drink_bottle(kind):
    part3_palette();liquid={'lemonade':'cheese','ginger_ale':'amber','kombucha':'dark_brown'}[kind]
    h=.22;r=.028;glass=liquid+'_drink_glass';PALETTE[glass]=PALETTE[liquid]
    parts=[closed_shell('Shared hollow soda bottle',[(0,r*.88),(.004,r),(.150,r),(.181,.012),(.200,.011)],glass),
           lathe('Separate drink',[(.004,.023),(.006,.026),(.148,.026),(.178,.010),(.197,.009)],make_material(liquid),10),
           cylinder('Sealed drink cap',(0,0,.210),.013,.020,'charcoal' if kind=='kombucha' else 'sage',10),
           curved_label(.029,.090,.046,.048,make_material('ivory'))]
    icon=sprig(-.030,.092,'sage','leaf',.9) if kind=='kombucha' else sprig(-.030,.092,'cheese' if kind=='lemonade' else 'amber','almond',.9)
    if kind=='ginger_ale':
        icon=[ball('Whole ginger root icon',(x,-.030,.092+z),(.008,.0015,.006),'amber',6,4)
              for x,z in [(-.009,0),(0,.005),(.009,0),(.003,-.007)]]
    if kind=='lemonade':icon+=sprig(-.030,.071,'sage','leaf',.4)
    if kind=='lemonade' and ITERATION>=2:
        for p in icon:bpy.data.objects.remove(p,do_unlink=True)
        icon=[disk_front('Whole plump lemon icon',0,-.030,.101,.013,.009,'cheese',12),
              disk_front('Whole lemon leaf',.009,-.031,.111,.006,.003,'sage',8)]
        icon+=readable_label('Lemonade',.030,.076,.042,.010,parts,max_triangles=190)
    caps=[parts[2]]
    if ITERATION>=3:
        collar=lathe('Rounded bottle neck ring',[(.198,.0133),(.1986,.0139),(.2014,.0139),(.202,.0133)],
                     make_material('charcoal' if kind=='kombucha' else 'sage'),10)
        parts.append(collar);caps.append(collar)
    geometry_pigment(caps+icon)
    return parts+icon

def juice_carton(kind):
    part3_palette();color={'coconut_water':'ivory','grape_juice':'purple','tomato_juice':'red'}[kind]
    shape=outline(.075,.063,.005)
    rings=[[(x*sx,y*sy,z) for x,y in shape] for z,sx,sy in
           [(0,.92,.92),(.003,1,1),(.151,1,1),(.184,.96,.18),(.194,.96,.15)]]
    parts=[loft('Shared gable-top juice carton',rings,make_material(color)),
           box('Folded sealed ridge',(0,0,.195),(.074,.009,.006),color)]
    cap=cylinder('Green screw cap',(.019,-.014,.177),.009,.009,'sage',10)
    cap.rotation_euler.x=.4;apply_transform(cap);parts.append(cap)
    if ITERATION>=3:
        collar=lathe('Rounded carton cap seat',[(-.0015,.0095),(-.0009,.0101),(.0009,.0101),(.0015,.0095)],make_material('sage'),10)
        collar.location=(.019,-.014,.173);collar.rotation_euler.x=.4;apply_transform(collar);parts.append(collar)
    icon=sprig(-.033,.092,'sage' if kind=='coconut_water' else 'purple' if kind=='grape_juice' else 'red',
               'leaf' if kind=='coconut_water' else 'grapes' if kind=='grape_juice' else 'tomato',1.2)
    parts.append(box('Cream juice label',(0,-.0325,.09),(.057,.001,.074 if ITERATION>=2 else .055),'ivory'))
    if ITERATION>=2:
        if kind=='coconut_water':
            for p in icon:bpy.data.objects.remove(p,do_unlink=True)
            icon=sprig(-.0345,.092,'dark_brown','coconut',.55)
            icon.append(bar_between('Palm trunk',(0,-.0345,.090),(0,-.0345,.120),.0018,'sage'))
            for i in range(5):
                angle=(i-2)*.65
                frond=disk_front('Palm frond',.012*math.sin(angle),-.0345-(i*.00015 if ITERATION>=3 else 0),
                                 (.112 if ITERATION>=3 else .119)+.005*math.cos(angle),.010,.0025,'sage',8)
                if ITERATION>=3:frond.rotation_euler.y=angle-math.pi/2;apply_transform(frond)
                icon.append(frond)
        else:
            for p in icon:
                p.location.y-=.0015
                if kind=='grape_juice' and ITERATION>=3:p.location.z+=.008
        word={'coconut_water':'Coconut\nwater','grape_juice':'Grape juice','tomato_juice':'Tomato juice'}[kind]
        icon+=readable_label(word,.0335,.065,.049,.014,parts,flat=True,max_triangles=280)
    geometry_pigment(icon)
    return parts+icon

def spice_jar(kind):
    part3_palette();h=.12;r=.025
    lid_color={'cinnamon_sticks':'sage','vanilla_pods':'ivory','turmeric':'spice_gold','paprika':'spice_red','black_pepper':'sage'}[kind]
    parts=[closed_shell('Shared hollow spice jar',[(0,.022),(.003,r),(.102,r),(.109,.023)]),
           cylinder('Spice screw lid',(0,0,.115),.026,.010,lid_color,8),
           curved_label(.0255,.055,.040,.040 if ITERATION>=2 else .032,make_material('ivory'))]
    if ITERATION>=3:
        parts.append(lathe('Rounded spice neck seat',[(.1065,.0245),(.1071,.0251),(.1094,.0251),(.1100,.0245)],make_material(lid_color),8))
    contents=[]
    if kind=='cinnamon_sticks':
        for i in range(5 if ITERATION>=2 else 6):
            a=i*2.399;x=.013*math.cos(a);y=.013*math.sin(a)
            if ITERATION>=2:
                obj=open_profile('True hollow rolled cinnamon',[(.005,.0034),(.0056,.004),(.0994,.004),(.100,.0034),
                     (.100,.0026),(.0994,.002),(.0056,.002),(.005,.0026)],'amber',6)
            else:obj=lathe('Rolled cinnamon stick',[(.004,.003),(.006,.004),(.098,.004),(.101,.003)],make_material('amber'),6)
            obj.location.x=x;obj.location.y=y;contents.append(obj)
    elif kind=='vanilla_pods':
        for i in range(5):
            a=i*2.399;x=.012*math.cos(a);y=.012*math.sin(a)
            if ITERATION>=2:
                obj=make_sphere('Soft curved whole vanilla pod',(x,y,.052),(.002,.002,.048),make_material('dark_brown'),
                     segments=6,rings=6,flatten=False,deform=lambda q:(q.x+2.5*q.z*q.z,q.y,q.z))
                add_bevel(obj,.0001);contents.append(obj)
            else:contents.append(path_tube('Curved whole vanilla pod',[(x,y,.004),(x+.004,y,.032),(x,y,.070),(x-.003,y,.101)],.0018,'dark_brown',6))
    elif kind=='black_pepper':
        for i in range(20 if ITERATION>=2 else 14):
            a=i*2.399;rad=.013*math.sqrt((i%5)/4)
            if ITERATION>=3:
                rad=.010*math.sqrt((i%4)/3)
                contents.append(ball('Grouped whole peppercorn',(rad*math.cos(a),rad*math.sin(a),.013+(i//4)*.0165),(.009,.009,.009),'charcoal',6,3))
            elif ITERATION>=2:contents.append(ball('Whole round peppercorn',(rad*math.cos(a),rad*math.sin(a),.012+(i//5)*.021),(.007,.007,.007),'charcoal',6,3))
            else:contents.append(ball('Chunky peppercorn mound',(rad*math.cos(a),rad*math.sin(a),.012+(i//4)*.023),(.010,.010,.012),'charcoal',6,4))
    else:
        contents.append(lathe('Separate ground spice mound',[(.004,.020),(.007,.023),(.089,.023),(.099,.016),(.101,.004)],
                         make_material('spice_gold' if kind=='turmeric' else 'spice_red'),10))
    icon=sprig(-.026,.055,'amber' if kind in ('cinnamon_sticks','turmeric') else 'sage' if kind=='vanilla_pods' else 'spice_red' if kind=='paprika' else 'charcoal',
                'wheat' if kind=='cinnamon_sticks' else 'leaf' if kind=='vanilla_pods' else 'almond' if kind=='turmeric' else 'tomato' if kind=='paprika' else 'sesame',.6)
    if ITERATION>=2:
        for p in icon:bpy.data.objects.remove(p,do_unlink=True)
        icon=[]
        if kind in ('cinnamon_sticks','black_pepper'):
            word='Cinnamon\nsticks' if kind=='cinnamon_sticks' else 'Black\npepper'
            icon=readable_label(word,.0265,.055,.038,.018,parts,max_triangles=300)
        elif kind=='vanilla_pods':
            for i in range(5):
                a=i*math.tau/5
                icon.append(disk_front('Vanilla flower petal',.007*math.cos(a),-.0265,.062+.007*math.sin(a),.004,.003,'milk' if ITERATION>=3 else 'ivory',8))
            icon.append(disk_front('Vanilla flower centre',0,-.027,.062,.0025,.0025,'cheese',8))
            if ITERATION>=3:
                text=label_text('Vanilla',.0265,.043,.038,.008);text.data.calc_loop_triangles()
                if len(text.data.loop_triangles)<=120:icon.append(text)
                else:bpy.data.objects.remove(text,do_unlink=True)
        elif kind=='turmeric':
            for x,z in ((-.007,.059),(0,.064),(.007,.059)):
                icon.append(ball('Whole turmeric root icon',(x,-.027,z),(.006,.0015,.004),'spice_gold',6,4))
            icon+=readable_label('Turmeric',.0265,.044,.038,.008,parts,max_triangles=200)
        else:
            contour=[(-.007,.067),(.001,.069),(.006,.065),(.006,.058),(.002,.051),(-.010,.048),(-.004,.055),(0,.061),(-.002,.065)]
            icon.append(mesh_object('Whole curved chili icon',[(x,-.027,z) for x,z in contour],[tuple(range(len(contour)))],make_material('spice_red')))
            icon+=readable_label('Paprika',.0265,.042,.038,.008,parts,max_triangles=200)
    geometry_pigment(contents+icon)
    return parts+contents+icon

def bay_leaves():
    part3_palette()
    parts=window_bag('sage','ivory','sage','lentil',True)
    # Reuse the sealed puffy pouch, but fill it with visible whole leaves.
    for obj in list(parts):
        if obj.name.startswith(('Visible chunky grain','Hidden bulk','Legume identity')):
            parts.remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
    for i in range(4 if ITERATION>=2 else 6):
        leaf=ball('Whole dried bay leaf',((i%2-.5)*.03,-.020,.045+(i//2)*.023),(.014,.002,.025),'sage',6 if ITERATION>=2 else 8,4)
        leaf.rotation_euler.y=(i%2-.5)*.5+(.10*math.sin(i*2.4) if ITERATION>=3 else 0)
        if ITERATION>=3:
            leaf.location.x+=.002*math.sin(i*1.7);leaf.location.z+=.002*math.cos(i*2.4)
        apply_transform(leaf);parts.append(leaf)
    for obj in parts:
        obj.location*=.70
        for v in obj.data.vertices:v.co*=.70
    if ITERATION>=2:parts+=readable_label('Bay leaves',.0225,.107,.062,.014,parts,flat=True,max_triangles=200)
    return parts

def arrange_expansion_preview(objects):layout_large_props(objects)

"""Refinements for the revised part-three brief; original builders stay reusable."""

def top_leaf_sprig(z, x=0, y=-.014):
    """Small raised, rounded primitives on a cheese-wheel label."""
    parts=[bar_between('Label sprig stem',(x-.010,y-.003,z),(x+.010,y+.003,z),.0012,'sage')]
    for dx,dy,angle in [(-.006,.003,-.4),(.003,-.003,-.4),(.009,.004,-.4)]:
        leaf=ball('Label sprig leaf',(x+dx,y+dy,z),(.005,.0025,.0006),'sage',6,3)
        leaf.rotation_euler.z=angle;apply_transform(leaf);parts.append(leaf)
    return parts


_previous_dairy_tub=dairy_tub
def dairy_tub(kind):
    parts=_previous_dairy_tub(kind)
    if ITERATION>=4:
        if kind=='mascarpone':
            ring=open_profile('Cream lid with central swirl window',
                [(.066,.045),(.0665,.046),(.068,.046),(.0685,.045),
                 (.0685,.037),(.068,.0365),(.0665,.0365),(.066,.037)],'ivory',8)
            geometry_pigment([ring]);parts.append(ring)
        # Tubs are intentionally squat, with enough height to read as dairy packs.
        for obj in parts:
            # Bake angle-limited bevels before changing proportions; scaling an
            # unbaked swirl would create extra bevels on its hexagonal sections.
            apply_modifiers(obj)
            if ITERATION>=5 and obj.name.startswith('Soft ricotta or mascarpone swirl'):
                for vertex in obj.data.vertices:
                    vertex.co.z+=.0015*max(0,1-math.hypot(vertex.co.x,vertex.co.y)/.030)
            obj.location.z*=1.20
            for vertex in obj.data.vertices:vertex.co.z*=1.20
    return parts


_previous_mozzarella=mozzarella
def mozzarella():
    parts=_previous_mozzarella()
    if ITERATION>=4:
        leaf=disk_front('Mozzarella small leaf icon',0,-.040,.053,.006,.003,'sage',8)
        if ITERATION>=5:
            for vertex in leaf.data.vertices:
                vertex.co.x=vertex.co.x*.7+.027
                vertex.co.z=(vertex.co.z-.053)*.7+.077
            if ITERATION>=6:
                # disk_front keeps its centre in object.location, not vertices.
                for vertex in leaf.data.vertices:
                    vertex.co.x-=.027
                    vertex.co.z-=.077-.053*.7
                leaf.location=(.027,-.040,.077)
            for obj in parts:
                if obj.get('label_text'):
                    for vertex in obj.data.vertices:vertex.co.x=vertex.co.x*.90-.003
        geometry_pigment([leaf]);parts.append(leaf)
    return parts


_previous_cheese_wheel=cheese_wheel
def cheese_wheel(kind):
    parts=_previous_cheese_wheel(kind)
    if ITERATION>=4:
        red=kind=='gouda';h=.038 if red else .029
        # Move lettering toward the back to leave room for a distinct raised motif.
        for obj in parts:
            if obj.get('label_text'):obj.location.y+=.008
        if red:
            motif=[]
            for i in range(5):
                angle=i*math.tau/5
                motif.append(ball('Red gouda flower petal',(.005*math.cos(angle),-.014+.005*math.sin(angle),h+.0015),
                                  (.0035,.0035,.0006),'wax',6,3))
            motif.append(ball('Gouda flower centre',(0,-.014,h+.0017),(.002,.002,.0007),'cheese',6,3))
        else:motif=top_leaf_sprig(h+.0015)
        geometry_pigment(parts[:1]+parts[2:]+motif);parts+=motif
        if ITERATION>=6:
            wheel=parts[0];apply_modifiers(wheel)
            for vertex in wheel.data.vertices:
                angle=math.atan2(vertex.co.y,vertex.co.x)
                ripple=.006*math.sin(angle*3)+.003*math.cos(angle*5)
                vertex.co.x*=1+ripple;vertex.co.y*=1+ripple
                vertex.co.z+=.00035*math.sin(angle*3)*max(0,vertex.co.z/h)
    return parts


def opened_mussel(position, angle):
    """A natural open shell with wall thickness, not a cut sample."""
    rings=[]
    for z,size in [(0,.10),(.002,.65),(.009,1),(.012,1),(.012,.90),(.004,.60),(.004,.10)]:
        rings.append([(.025*size*math.cos(a),.016*size*math.sin(a),z)
                      for a in [i*math.tau/8 for i in range(8)]])
    shell=loft('Naturally opened mussel shell',rings,make_material('fish_dark'))
    # The radial profile already rounds the shell lip; avoid subdividing every
    # inner/outer ring again. Keep a small two-segment bevel for sharp boundaries.
    for modifier in shell.modifiers:
        if modifier.type=='BEVEL':modifier.angle_limit=math.radians(120)
    shell.location=position;shell.rotation_euler.z=angle;apply_transform(shell)
    meat=ball('Whole mussel flesh in shell',(position[0],position[1],position[2]+.007),
              (.017,.009,.004),'poultry',6,3)
    meat.rotation_euler.z=angle;apply_transform(meat)
    return [shell,meat]


_previous_mussels_food=mussels_food
def mussels_food():
    parts=_previous_mussels_food()
    if ITERATION>=4:
        for index in (1,4,6):
            obj=parts[index];position=obj.location.copy();angle=obj.rotation_euler.z
            # Original rotation is baked, so retain the designed arrangement.
            angle=(index%3-1)*.18
            bpy.data.objects.remove(obj,do_unlink=True)
            replacement=opened_mussel((position.x,position.y,.011),angle)
            parts[index]=replacement[0];parts.append(replacement[1])
    return parts


def tilt_protein_card(objects):
    pivot=Vector((0,-.075,.018));rotation=Matrix.Rotation(-.48,4,'X')
    for obj in objects:
        apply_modifiers(obj)
        world=obj.matrix_world.copy()
        for vertex in obj.data.vertices:
            offset=Vector((0,-.009 if ITERATION>=5 else 0,.005))
            vertex.co=pivot+rotation.to_3x3()@(world@vertex.co-pivot)+offset
        obj.matrix_world=Matrix.Identity(4)


_previous_protein=protein
def protein(kind):
    parts=_previous_protein(kind)
    if ITERATION>=4:
        names=('Cream label card','Bird body icon','Bird head icon','Bird beak and tail',
               'Whole fish icon','Fish tail icon','Whole shell icon','Whole squid icon','Eight squid arms icon')
        card_parts=[obj for obj in parts if obj.name.startswith(names) or obj.get('label_text')]
        if kind=='duck':
            for obj in card_parts:
                colors=obj.data.color_attributes.get('Color')
                if colors:
                    for entry in colors.data:entry.color=color_rgba(PALETTE['sage'])
        if kind in ('turkey','trout','mussels'):
            # These short words fit alongside the icon without consuming the
            # geometry reserve needed by squid arms and mackerel stripes.
            text=label_text(kind.title(),5,.018,.040,.010)
            for vertex in text.data.vertices:
                vertex.co.y=-.077;vertex.co.x+=.010
            text.data.calc_loop_triangles()
            if len(text.data.loop_triangles)<180:
                for obj in card_parts:
                    if not obj.name.startswith('Cream label card'):
                        for vertex in obj.data.vertices:vertex.co.x=vertex.co.x*.42-.020
                        obj.location.x*=.42
                geometry_pigment([text]);parts.append(text);card_parts.append(text)
            else:bpy.data.objects.remove(text,do_unlink=True)
        if ITERATION>=6 and kind=='turkey':
            for obj in list(card_parts):
                if obj.name.startswith(('Bird body icon','Bird head icon','Bird beak and tail')):
                    card_parts.remove(obj);parts.remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
            fan=[]
            for i in range(5):
                angle=math.pi*(.15+i*.175)
                fan.append(disk_front('Turkey fan feather',-.024+.005*math.cos(angle),-.077,
                    .018+.005*math.sin(angle),.0023,.003,'fish_dark',6))
            fan+=[disk_front('Turkey icon body',-.022,-.0775,.018,.0035,.0035,'fish_dark',8),
                  disk_front('Turkey icon head',-.0165,-.0775,.022,.002,.002,'fish_dark',6)]
            geometry_pigment(fan);parts+=fan;card_parts+=fan
        tilt_protein_card(card_parts)
        if ITERATION>=5:
            PALETTE['protein_clear']='#A7B5AF'
            film=make_material('protein_clear',.25)
            shader=film.node_tree.nodes.get('Principled BSDF')
            shader.inputs['Roughness'].default_value=.2
            shader.inputs['Specular IOR Level'].default_value=0
            parts[1].data.materials.clear();parts[1].data.materials.append(film)
            if kind=='duck':
                for obj in parts:
                    if obj.name.startswith('Whole plump bird'):
                        apply_modifiers(obj)
                        for vertex in obj.data.vertices:
                            vertex.co.x*=1.07;vertex.co.y*=.94
        if ITERATION>=6 and kind=='duck':
            head=[ball('Duck tucked whole head',(-.075,.009,.035),(.013,.011,.012),'duck',8,4),
                  ball('Duck rounded bill',(-.087,.008,.031),(.012,.007,.004),'poultry',8,4)]
            geometry_pigment(head);parts+=head
        if ITERATION>=6 and kind=='squid':
            for obj in parts:
                if obj.name.startswith('Chunky curved squid arm'):
                    for start in range(0,len(obj.data.vertices),6):
                        ring=list(obj.data.vertices)[start:start+6]
                        centre=sum((v.co for v in ring),Vector())/len(ring)
                        for vertex in ring:vertex.co=centre+(vertex.co-centre)*1.2
                elif obj.name.startswith('Rounded squid arm tip'):
                    for vertex in obj.data.vertices:vertex.co*=1.2
    return parts


def fit_product_word(parts, word, radius, z, width, height, budget):
    """Use real bold lettering if the remaining geometry budget permits it."""
    text=label_text(word,radius,z,width,height)
    for obj in parts+[text]:apply_modifiers(obj);obj.data.calc_loop_triangles()
    if sum(len(obj.data.loop_triangles) for obj in parts+[text])>=budget:
        bpy.data.objects.remove(text,do_unlink=True)
        return
    geometry_pigment([text]);parts.append(text)


_previous_drink_bottle=drink_bottle
def drink_bottle(kind):
    parts=_previous_drink_bottle(kind)
    if ITERATION>=4:
        color='charcoal' if kind=='kombucha' else 'sage'
        # All three bottles retain one shared shape, including identical lids.
        for obj in list(parts):
            if obj.name.startswith(('Sealed drink cap','Rounded bottle neck ring')):
                parts.remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
        cap=cylinder('Shared rounded drink cap',(0,0,.210),.013,.020,color,8)
        if ITERATION>=6:
            apply_modifiers(cap)
            for vertex in cap.data.vertices:
                taper=.98-.04*(vertex.co.z/.020+.5)
                vertex.co.x*=taper;vertex.co.y*=taper
        collar=lathe('Shared rounded cap seat',[(.198,.0133),(.1986,.0139),(.2014,.0139),(.202,.0133)],make_material(color),8)
        geometry_pigment([cap,collar]);parts+=[cap,collar]
        if kind=='ginger_ale':
            for obj in list(parts):
                if obj.name.startswith('Whole ginger root icon'):
                    parts.remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
            positions=[(-.008,-.002),(0,.003),(.008,-.002)] if ITERATION>=5 else [(-.010,-.002),(0,.003),(.010,-.002)]
            icon=[ball('Chunky ginger root label',(x,-.030,.103+z),(.008,.0016,.0055),'amber',6,4 if ITERATION>=5 else 3)
                  for x,z in positions]
            if ITERATION>=6:
                for obj,angle in zip(icon,(-.30,.18,.40)):
                    obj.rotation_euler.y=angle;apply_transform(obj)
            geometry_pigment(icon);parts+=icon
        elif kind=='kombucha':
            for obj in parts:
                if obj.name.startswith(('Whole basil leaf','Leaf midrib')):obj.location.z+=.009
        if kind in ('ginger_ale','kombucha'):
            fit_product_word(parts,'Ginger ale' if kind=='ginger_ale' else 'Kombucha',.030,.076,.042,.009,900)
    return parts


_previous_juice_carton=juice_carton
def juice_carton(kind):
    parts=_previous_juice_carton(kind)
    if ITERATION>=4 and kind=='tomato_juice':
        for obj in list(parts):
            if obj.name.startswith('Whole icon leaf'):
                parts.remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
        contour=[]
        for i in range(10):
            angle=i*math.tau/10;r=.008 if i%2==0 else .003
            contour.append((r*math.cos(angle),.108+r*math.sin(angle)))
        calyx=front_shape('Tomato green star calyx',contour,-.0355,'sage')
        geometry_pigment([calyx])
        if ITERATION>=5:
            for value in calyx.data.color_attributes['Color'].data:value.color=color_rgba(PALETTE['sage'])
        parts.append(calyx)
    if ITERATION>=6 and kind=='grape_juice':
        leaf=disk_front('Small grape sprig leaf',.013,-.036,.109,.006,.003,'sage',6)
        geometry_pigment([leaf]);parts.append(leaf)
    return parts


_previous_spice_jar=spice_jar
def spice_jar(kind):
    parts=_previous_spice_jar(kind)
    if ITERATION>=4:
        PALETTE['spice_clear']='#A7B5AF'
        film=make_material('spice_clear',.25)
        shader=film.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Roughness'].default_value=.2
        shader.inputs['Specular IOR Level'].default_value=0
        parts[0].data.materials.clear();parts[0].data.materials.append(film)
        if ITERATION>=5:
            tints={'cinnamon_sticks':'#B0A28B','vanilla_pods':'#A7B0A1',
                   'turmeric':'#C5AF7B','paprika':'#B99A89','black_pepper':'#A3ADA5'}
            key=kind+'_clear_glass';PALETTE[key]=tints[kind]
            glass=make_material(key,.25);bsdf=glass.node_tree.nodes.get('Principled BSDF')
            bsdf.inputs['Roughness'].default_value=.2;bsdf.inputs['Specular IOR Level'].default_value=0
            parts[0].data.materials.clear();parts[0].data.materials.append(glass)
            if kind=='cinnamon_sticks':
                for obj in parts:
                    if obj.name.startswith('True hollow rolled cinnamon'):
                        for value in obj.data.color_attributes['Color'].data:value.color=color_rgba('#986D48')
        if kind=='black_pepper':
            for obj in list(parts):
                if obj.name.startswith('Grouped whole peppercorn'):
                    parts.remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
            pepper=[]
            for i in range(14):
                angle=i*2.399;radius=.010*math.sqrt((i%3)/2)
                if ITERATION>=5:radius=.014*math.sqrt((i%4+.5)/4)
                pepper.append(ball('Rounded clustered whole peppercorn',
                    (radius*math.cos(angle),radius*math.sin(angle),.014+(i//(4 if ITERATION>=5 else 3))*.019),
                    (.008,.008,.008),'charcoal',6,4))
            geometry_pigment(pepper);parts+=pepper
        if kind=='vanilla_pods':
            pods=[obj for obj in parts if obj.name.startswith('Soft curved whole vanilla pod')]
            parts.remove(pods[-1]);bpy.data.objects.remove(pods[-1],do_unlink=True)
            old=[obj for obj in parts if obj.get('label_text')]
            for obj in old:parts.remove(obj)
            before=len(parts)
            fit_product_word(parts,'Vanilla\npods',.0265,.043,.037,.017,900)
            if len(parts)>before:
                for obj in old:bpy.data.objects.remove(obj,do_unlink=True)
            else:parts+=old
        if ITERATION>=6:
            for obj in parts:
                if obj.name.startswith('Spice screw lid'):
                    apply_modifiers(obj)
                    for vertex in obj.data.vertices:
                        taper=.99-.04*(vertex.co.z/.010+.5)
                        vertex.co.x*=taper;vertex.co.y*=taper
    return parts


_previous_bay_leaves=bay_leaves
def bay_leaves():
    parts=_previous_bay_leaves()
    if ITERATION>=4:
        leaf=disk_front('Bay leaves small label icon',.029,-.024,.118,.004,.0025,'sage',8)
        parts.append(leaf)
        # Reuse the pouch's existing sage material rather than adding a fifth
        # material for this small icon. Window remains an independent surface.
        if ITERATION>=6:
            windows=[obj for obj in parts if obj.name.startswith('Separate see-through window')]
            opaque=[obj for obj in parts if obj not in windows]
            geometry_pigment(opaque)
            for index,obj in enumerate(parts):
                if obj.name.startswith('Whole dried bay leaf'):
                    shade='#8B9B68' if index%2 else '#7D8C59'
                    for value in obj.data.color_attributes['Color'].data:value.color=color_rgba(shade)
            PALETTE['bay_clear']='#9EAD91'
            film=make_material('bay_clear',.25);shader=film.node_tree.nodes.get('Principled BSDF')
            shader.inputs['Roughness'].default_value=.2;shader.inputs['Specular IOR Level'].default_value=0
            for obj in windows:obj.data.materials.clear();obj.data.materials.append(film)
    return parts

CATEGORY_FOLDER='Food_12_Spices'
SCRIPT_NAME='BuildFoodSpices.py'
ITERATION=6
JOBS=[('cinnamon_sticks',lambda:spice_jar('cinnamon_sticks'),1200),('vanilla_pods',lambda:spice_jar('vanilla_pods'),900),('turmeric',lambda:spice_jar('turmeric'),900),('paprika',lambda:spice_jar('paprika'),900),('black_pepper',lambda:spice_jar('black_pepper'),1200),('bay_leaves',bay_leaves,900)]
if __name__=='__main__':run_category()
