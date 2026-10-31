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
