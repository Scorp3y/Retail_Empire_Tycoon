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
    print_material=bpy.data.materials.get('Pantry solid-colour ink')
    if print_material is None:
        print_material=bpy.data.materials.new('Pantry solid-colour ink');print_material.use_nodes=True
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

CATEGORY_FOLDER='Food_06_DriedLegumes'
SCRIPT_NAME='BuildPantryLegumes.py'
JOBS=[('chickpeas',lambda:legume(0),1200),('green_lentils',lambda:legume(1),1200),('red_kidney_beans',lambda:legume(2),1200),('white_beans',lambda:legume(3),1200),('black_beans',lambda:legume(4),1200),('dried_peas',lambda:legume(5),1200)]
if __name__=='__main__':run_category()
