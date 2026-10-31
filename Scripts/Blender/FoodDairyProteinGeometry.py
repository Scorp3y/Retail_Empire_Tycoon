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
