"""Small geometry additions; original palette, export and bevel helpers stay intact."""

def expansion_palette():
    PALETTE.update(golden=PALETTE['mango'], toast=PALETTE['peach'],
                   icing='#F1E8CF', berry=PALETTE['deep_jam'], mint='#98AF8C',
                   pink=PALETTE['blush'], blue='#879FA8', custard=PALETTE['mango_flesh'],
                   terracotta='#C48363')


def layout_large_props(objects):
    """Fit real metre-scale props without overlapping neighbouring previews."""
    widths=[o.dimensions.x for o in objects]
    cursor=0
    for obj,width in zip(objects,widths):
        obj.location.x=cursor+width/2;cursor+=width+.075
    span=cursor-.075;center=span/2
    old_center=(len(objects)-1)*ROW_SPACING/2
    height=max(o.dimensions.z for o in objects)
    delta=Vector((center-old_center,0,max(.055,height*.45)-.055))
    for obj in bpy.context.scene.objects:
        if obj.type in {'LIGHT','CAMERA'}: obj.location+=delta
    ground=bpy.data.objects.get('Preview ground')
    if ground: ground.location.x=center
    bpy.context.scene.camera.data.ortho_scale=max(span+.14,height*3.8)


def colored_ball(name, position, size, color, segments=8, rings=4):
    return pigment(ball(name, position, size, 'cream', segments, rings), color)


def path_tube(name, points, radius, color, sides=6):
    """Faceted round piping, following a real spatial path instead of a decal."""
    vertices = []
    for i, p in enumerate(points):
        tangent = Vector(points[min(i+1, len(points)-1)]) - Vector(points[max(0, i-1)])
        tangent.normalize()
        axis = Vector((0, 0, 1)) if abs(tangent.z) < .9 else Vector((1, 0, 0))
        u = tangent.cross(axis).normalized(); v = tangent.cross(u).normalized()
        for j in range(sides):
            vertices.append(tuple(Vector(p) + radius*(u*math.cos(j*math.tau/sides)+v*math.sin(j*math.tau/sides))))
    faces = [tuple(reversed(range(sides)))]
    for i in range(len(points)-1):
        for j in range(sides):
            faces.append((i*sides+j, i*sides+(j+1)%sides, (i+1)*sides+(j+1)%sides, (i+1)*sides+j))
    faces.append(tuple((len(points)-1)*sides+j for j in range(sides)))
    obj = mesh_object(name, vertices, faces, make_material(color), 'modeled_detail')
    add_bevel(obj)
    return obj


def rounded_rib(name, points, radius, color):
    """Six-sided lattice rib with modelled round ends and continuous frames."""
    tangents=[]
    for i in range(len(points)):
        tangents.append((Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])).normalized())
    reference=min((Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1))),
                  key=lambda axis:max(abs(t.dot(axis)) for t in tangents))
    entries=[(Vector(points[0])-tangents[0]*radius*.707,radius*.707,tangents[0])]
    entries += [(Vector(p),radius,t) for p,t in zip(points,tangents)]
    entries += [(Vector(points[-1])+tangents[-1]*radius*.707,radius*.707,tangents[-1])]
    vertices=[]
    for p,r,t in entries:
        u=t.cross(reference).normalized();v=t.cross(u).normalized()
        for j in range(6):vertices.append(tuple(p+r*(u*math.cos(j*math.tau/6)+v*math.sin(j*math.tau/6))))
    vertices += [tuple(Vector(points[0])-tangents[0]*radius),tuple(Vector(points[-1])+tangents[-1]*radius)]
    n=len(entries);faces=[]
    for i in range(n-1):
        for j in range(6):faces.append((i*6+j,i*6+(j+1)%6,(i+1)*6+(j+1)%6,(i+1)*6+j))
    for j in range(6):faces.extend([(n*6,(j+1)%6,j),(n*6+1,(n-1)*6+j,(n-1)*6+(j+1)%6)])
    obj=mesh_object(name,vertices,faces,make_material(color),'rounded_lattice')
    add_bevel(obj)
    return obj


def front_shape(name, contour, y, color):
    # Thin, physically raised icon with softly bevelled silhouette.
    obj = prism(name, contour, -.00035, .00035, make_material('cream'))
    obj.rotation_euler.x = math.pi/2
    obj.location.y = y
    apply_transform(obj)
    return pigment(obj, color)


def leaf_icon(y, z, width=.018, color='leaf', x=0):
    return front_shape('Raised leaf icon', [(x-width*.5,z), (x-width*.2,z+width*.45),
                       (x+width*.45,z+width*.65), (x+width*.5,z+width*.2),
                       (x+width*.2,z-width*.2), (x-width*.2,z-width*.3)], y, color)


def oval_shell(name, radius, top, color='plastic', segments=12, bottom=0):
    # Continuous outside, rounded lip, inner wall and inner floor: true thickness.
    wall=.0012
    if color=='plastic': clear_material()
    obj=hollow_vessel(name,[(bottom,radius*.78),(bottom+.002,radius*.87),
        (top-.001,radius),(top,radius-.0005),(top-.001,radius-wall),
        (bottom+.003,radius*.78)],color,segments)
    if color=='plastic': obj.data.materials[0]=clear_material()
    return obj


def open_profile(name,profile,color,segments=12):
    """Closed wall following a closed radial section; both axial holes stay open."""
    vertices=[(r*math.cos(i*math.tau/segments),r*math.sin(i*math.tau/segments),z)
              for z,r in profile for i in range(segments)]
    faces=[]
    for j in range(len(profile)):
        for i in range(segments):
            faces.append((j*segments+i,j*segments+(i+1)%segments,
                          ((j+1)%len(profile))*segments+(i+1)%segments,
                          ((j+1)%len(profile))*segments+i))
    obj=mesh_object(name,vertices,faces,make_material(color),'through_cavity')
    add_bevel(obj);obj['through_cavity']=True
    return obj
