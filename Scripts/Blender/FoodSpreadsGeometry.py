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

CATEGORY_FOLDER='Food_16_SpreadsTreats'
SCRIPT_NAME='BuildFoodSpreads.py'
JOBS=[('chocolate_spread',lambda:spread_jar('chocolate_spread'),900),('almond_butter',lambda:spread_jar('almond_butter'),900),('cashew_butter',lambda:spread_jar('cashew_butter'),900),('sunflower_seed_butter',lambda:spread_jar('sunflower_seed_butter'),900),('halva',halva,900),('fruit_leather',fruit_leather,900)]
if __name__=='__main__':run_category()
