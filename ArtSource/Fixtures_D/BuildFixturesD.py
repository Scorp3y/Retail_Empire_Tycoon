"""Blender 4.x-compatible ready meals; standalone category 02 helper snapshot."""
import bpy
import bmesh
import math
import os
import json
import struct
from mathutils import Vector, Matrix

# Output is beside the saved .blend, or in the working directory if unsaved.
# Replace this with an absolute directory if preferred.
_base_dir = os.path.dirname(bpy.data.filepath) if bpy.data.filepath else os.getcwd()
_script_dir = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else ""
if os.path.basename(_script_dir) == "Fixtures_D":
    OUTPUT_DIR = _script_dir
elif os.path.basename(_base_dir) == "Fixtures_D":
    OUTPUT_DIR = _base_dir
else:
    OUTPUT_DIR = os.path.join(_base_dir, "Fixtures_D")
EXPORT_Y_UP = False  # False: requested Z-up. True: standard glTF Y-up.
RENDER_PREVIEW = False  # Otherwise the scene is ready for F12.

PALETTE = {
    "kiwi_skin": "#88694E", "kiwi_flesh": "#A0B66C",
    "cream": "#E9DFB9", "peach": "#DEA16F", "blush": "#CD8279",
    "plum": "#806D91", "plum_light": "#9784A3", "leaf": "#839464",
    "mango": "#D6A25F", "mango_flesh": "#EAC781",
    "cherry": "#BE6862", "eggplant": "#7F6B8A",
    "eggplant_light": "#95819D", "plastic": "#DDE9DF",
    "ground": "#F1EADB",
}

# Dimensions in metres. Paired fruit keeps the same individual fruit size.
KIWI_RADIUS = 0.026
PEACH_RADIUS = 0.025
PLUM_RADIUS = 0.023
MANGO_LENGTH = 0.12
EGGPLANT_HEIGHT = 0.22
BOX_SIZE = (0.090, 0.064, 0.029)
CHERRY_RADIUS = 0.009
ROW_SPACING = 0.26
BEVEL_WIDTH = 0.00045
BEVEL_ANGLE = math.radians(65)

MATERIALS = {}
ACTIVE_COLLECTION = None


def srgb_to_linear(value):
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def color_rgba(hex_color, alpha=1.0):
    rgb = [int(hex_color[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple(srgb_to_linear(v) for v in rgb) + (alpha,)


def make_material(key, alpha=1.0):
    if key in MATERIALS:
        return MATERIALS[key]
    mat = bpy.data.materials.new("Produce_" + key)
    mat.use_nodes = True
    rgba = color_rgba(PALETTE[key], alpha)
    mat.diffuse_color = rgba
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = rgba
    shader.inputs["Roughness"].default_value = 0.25 if alpha < 1 else 0.82
    shader.inputs["Metallic"].default_value = 0
    shader.inputs["Alpha"].default_value = alpha
    shader.inputs["IOR"].default_value = 1.45
    if alpha < 1:
        # Blender 4.2 replaced the older blend_method API.
        if hasattr(mat, "surface_render_method"):
            choices = mat.bl_rna.properties["surface_render_method"].enum_items.keys()
            mat.surface_render_method = "BLENDED" if "BLENDED" in choices else "DITHERED"
        elif hasattr(mat, "blend_method"):
            mat.blend_method = "BLEND"
        if hasattr(mat, "use_transparency_overlap"):
            mat.use_transparency_overlap = False
    MATERIALS[key] = mat
    return mat


def register(obj, name, material):
    obj.name = name
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    ACTIVE_COLLECTION.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def apply_transform(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)


def make_sphere(name, position, radii, material, segments=8, rings=6,
                deform=None, flatten=True):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1)
    obj = register(bpy.context.object, name, material)
    for vertex in obj.data.vertices:
        q = vertex.co.copy()
        q.x *= 1 + 0.025 * math.sin(q.z * 4)
        q.y *= 1 + 0.025 * math.cos(q.x * 3 + q.z)
        if deform is not None:
            q = Vector(deform(q))
        if flatten:
            q.z = max(q.z, -0.80)
        vertex.co = (q.x * radii[0], q.y * radii[1], q.z * radii[2])
    obj.location = position
    obj.data.update()
    return obj


def add_bevel(obj, width=BEVEL_WIDTH):
    # Organic surfaces already have gentle facets; bevel their sharp boundaries.
    mod = obj.modifiers.new("Small rounded edges", "BEVEL")
    mod.width = width
    mod.segments = 2
    mod.limit_method = "ANGLE"
    mod.angle_limit = BEVEL_ANGLE
    mod.use_clamp_overlap = True


def apply_modifiers(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    for modifier in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = False


def add_blush(obj, material, side=1):
    index = len(obj.data.materials)
    obj.data.materials.append(material)
    half_width = max(abs(vertex.co.x) for vertex in obj.data.vertices)
    for polygon in obj.data.polygons:
        if polygon.center.x * side > half_width * 0.34:
            polygon.material_index = index


def make_leaf(position, length=0.027, width=0.011, angle=0):
    def curve(q):
        q.z += 1.0 * q.x * q.x
        return q
    leaf = make_sphere("Curved leaf", position, (length / 2, width / 2, 0.0011),
                       make_material("leaf"), rings=4, deform=curve, flatten=False)
    leaf.rotation_euler.z = angle
    apply_transform(leaf)
    add_bevel(leaf, 0.00012)
    return leaf


def make_stem(start, end, radius=0.0007):
    a, b = Vector(start), Vector(end)
    stem = make_sphere("Rounded stem", (a + b) / 2,
                       (radius, radius, (b - a).length / 2),
                       make_material("leaf"), rings=4, flatten=False)
    stem.rotation_euler = (b - a).to_track_quat("Z", "Y").to_euler()
    apply_transform(stem)
    return stem


def cut_upper_half(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                          plane_co=(0, 0, 0), plane_no=(0, 0, 1),
                          clear_outer=True, clear_inner=False, dist=1e-7)
    boundary = [edge for edge in bm.edges if edge.is_boundary]
    if boundary:
        bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def make_disk(name, position, radii, material, vertices=12, angle=0):
    # A tiny flat polygon is real geometry, suitable for cut flesh and seeds.
    bpy.ops.mesh.primitive_circle_add(vertices=vertices, radius=1, fill_type="NGON")
    obj = register(bpy.context.object, name, material)
    obj.scale = (radii[0], radii[1], 1)
    obj.location = position
    obj.rotation_euler.z = angle
    apply_transform(obj)
    return obj



def join_and_seat(name, parts, limit):
    for part in parts:
        apply_modifiers(part)
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    apply_transform(obj)
    # Bake placement into mesh coordinates; the exported node has identity pose.
    coordinates = [obj.matrix_world @ v.co for v in obj.data.vertices]
    low = Vector(tuple(min(v[i] for v in coordinates) for i in range(3)))
    high = Vector(tuple(max(v[i] for v in coordinates) for i in range(3)))
    offset = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
    for vertex, coordinate in zip(obj.data.vertices, coordinates):
        vertex.co = coordinate - offset
    obj.matrix_world = Matrix.Identity(4)
    obj.data.update()
    obj.data.calc_loop_triangles()
    count = len(obj.data.loop_triangles)
    if count >= limit:
        raise RuntimeError(f"{name}: {count} triangles; must be under {limit}.")
    obj["triangles"] = count
    obj["up_axis"] = "Z"
    print(f"{name}: {count} triangles, {len(obj.data.materials)} material slots")
    return obj


def export_glb(obj, directory):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(directory, obj.name + ".glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB",
        use_selection=True, export_yup=EXPORT_Y_UP, export_apply=True,
        export_animations=False, export_cameras=False, export_lights=False,
        export_texcoords=False, export_materials="EXPORT", export_extras=True)
    # Read the actual GLB: no image textures, and precisely one mesh node.
    with open(path, "rb") as stream:
        if stream.read(4) != b"glTF":
            raise RuntimeError("Invalid GLB: " + path)
        stream.seek(12)
        length, chunk_type = struct.unpack("<II", stream.read(8))
        data = json.loads(stream.read(length))
    if data.get("images") or len(data.get("meshes", [])) != 1:
        raise RuntimeError("Expected one texture-free mesh: " + path)
    if sum("mesh" in node for node in data.get("nodes", [])) != 1:
        raise RuntimeError("Expected one mesh node: " + path)
    if obj.name == "Cherries_Box" and not any(
            mat.get("alphaMode") == "BLEND" for mat in data.get("materials", [])):
        raise RuntimeError("Plastic did not export as alpha blend.")


def preview(objects):
    global ACTIVE_COLLECTION
    ACTIVE_COLLECTION = bpy.data.collections.new("Preview_Only")
    bpy.context.scene.collection.children.link(ACTIVE_COLLECTION)
    for i, obj in enumerate(objects):
        obj.location.x = i * ROW_SPACING
    center = (len(objects) - 1) * ROW_SPACING / 2
    bpy.ops.mesh.primitive_plane_add(size=1, location=(center, 0, -0.001))
    ground = register(bpy.context.object, "Preview ground", make_material("ground"))
    ground.scale = (ROW_SPACING * len(objects) * 3, 2.0, 1)
    scene = bpy.context.scene
    scene.world = bpy.data.worlds.new("Soft daylight")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.8, 0.8, 0.75, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.4
    for name, position, power, size in (
        ("Soft key", (center - 0.4, -0.5, 0.9), 18, 1.1),
        ("Soft fill", (center + 0.5, 0.3, 0.7), 8, 1.0)):
        bpy.ops.object.light_add(type="AREA", location=position)
        light = bpy.context.object
        light.name = name
        light.data.energy = power
        light.data.shape = "DISK"
        light.data.size = size
        light.rotation_euler = (Vector((center, 0, 0.04)) - light.location).to_track_quat("-Z", "Y").to_euler()
    bpy.ops.object.camera_add(location=(center + 0.15, -1.10, 0.70))
    camera = bpy.context.object
    camera.name = "Produce preview camera"
    camera.rotation_euler = (Vector((center, 0, 0.055)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = ROW_SPACING * len(objects) + 0.12
    scene.camera = camera
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 650
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "AgX"
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = os.path.join(OUTPUT_DIR, "Preview.png")
    if RENDER_PREVIEW:
        bpy.ops.render.render(write_still=True)



SEGMENTS=10
ITERATION=2
FONT_PATH="C:/Windows/Fonts/arialbd.ttf"
def shade_hex(value, multiplier):
    channels = [int(value[i:i + 2], 16) for i in (1, 3, 5)]
    return "#" + "".join(f"{max(0, min(255, round(c * multiplier))):02X}" for c in channels)

def prepare_palette():
    # Original colours stay byte-for-byte intact; new colours are tonal variants.
    PALETTE.update({
        "dark_brown": shade_hex(PALETTE["kiwi_skin"], 0.58),
        "dark_green": shade_hex(PALETTE["leaf"], 0.62),
        "deep_jam": shade_hex(PALETTE["cherry"], 0.70),
        "metal": "#ADB0A2",
        "pickle_glass": "#D2DDC1",
        "brine": shade_hex(PALETTE["cream"], .96),
        "corn_yellow": shade_hex(PALETTE["mango_flesh"], .90),
        "glass_edge": "#D2DDC1",
    })
    if ITERATION >= 4:
        # A familiar sage tint makes the clear jar readable on cream ground.
        PALETTE["pickle_glass"] = PALETTE["leaf"]
        PALETTE["glass_edge"] = PALETTE["leaf"]

def lathe(name, rings, material, segments=SEGMENTS, bands=None):
    """A low-segment cylinder shaped by a small, explicitly editable profile."""
    vertices = []
    faces = []
    for z, radius in rings:
        for i in range(segments):
            angle = i * math.tau / segments
            vertices.append((radius * math.cos(angle), radius * math.sin(angle), z))
    faces.append(tuple(reversed(range(segments))))
    for ring in range(len(rings) - 1):
        for i in range(segments):
            faces.append((ring * segments + i, ring * segments + (i + 1) % segments,
                          (ring + 1) * segments + (i + 1) % segments, (ring + 1) * segments + i))
    faces.append(tuple((len(rings) - 1) * segments + i for i in range(segments)))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    ACTIVE_COLLECTION.objects.link(obj)
    obj.data.materials.append(material)
    if bands:
        for band_index, band_material in bands.items():
            slot = len(obj.data.materials)
            obj.data.materials.append(band_material)
            for i in range(segments):
                mesh.polygons[1 + band_index * segments + i].material_index = slot
    add_bevel(obj)
    return obj

def soft_cylinder(name, radius, bottom, top, material, segments=SEGMENTS):
    bevel = min(BEVEL_WIDTH, (top - bottom) / 5)
    return lathe(name, [
        (bottom, radius - bevel),
        (bottom + bevel * .40, radius - bevel * .25),
        (bottom + bevel, radius),
        (top - bevel, radius),
        (top - bevel * .40, radius - bevel * .25),
        (top, radius - bevel),
    ], material, segments)

def curved_label(radius, center_z, width, height, material, full_band=False):
    if full_band:
        return soft_cylinder("Separate label band", radius, center_z - height / 2,
                             center_z + height / 2, material, segments=10 if ITERATION >= 3 else 12)
    corner = min(.0035, height / 5)
    columns = (-width / 2, -width / 2 + corner, -width / 4, 0,
               width / 4, width / 2 - corner, width / 2)
    vertices = []
    for index, x in enumerate(columns):
        angle = x / radius
        edge_height = height / 2 - (corner if index in (0, len(columns) - 1) else 0)
        for z in (-edge_height, edge_height):
            vertices.append((radius * math.sin(angle), -radius * math.cos(angle), center_z + z))
    faces = [(i * 2, i * 2 + 2, i * 2 + 3, i * 2 + 1) for i in range(len(columns) - 1)]
    mesh = bpy.data.meshes.new("Rounded front label")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("Wrapped cream label", mesh)
    ACTIVE_COLLECTION.objects.link(obj)
    obj.data.materials.append(material)
    add_bevel(obj)
    return obj

def label_text(body, radius, center_z, max_width, max_height):
    curve = bpy.data.curves.new("Geometric label lettering", "FONT")
    curve.body = body
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    curve.resolution_u = 1
    curve.render_resolution_u = 1
    curve.extrude = 0
    curve.bevel_depth = 0
    curve.space_line = .92
    if os.path.isfile(FONT_PATH):
        curve.font = bpy.data.fonts.load(FONT_PATH, check_existing=True)
    obj = bpy.data.objects.new("LabelText", curve)
    ACTIVE_COLLECTION.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.object
    obj.data.materials.append(make_material("dark_brown"))
    coords = [v.co.copy() for v in obj.data.vertices]
    low_x, high_x = min(v.x for v in coords), max(v.x for v in coords)
    low_y, high_y = min(v.y for v in coords), max(v.y for v in coords)
    scale = min(max_width / max(high_x - low_x, .00001), max_height / max(high_y - low_y, .00001))
    for vertex, point in zip(obj.data.vertices, coords):
        x = (point.x - (low_x + high_x) / 2) * scale
        z = (point.y - (low_y + high_y) / 2) * scale
        vertex.co = (radius * math.sin(x / radius), -radius * math.cos(x / radius), center_z + z)
    obj.data.update()
    obj["label_text"] = body
    return obj

def role(obj, value):
    obj["part_role"] = value
    return obj

def outline(width, depth, corner=.004):
    x, y = width / 2, depth / 2
    r = min(corner, x * .4, y * .4)
    return [(-x+r,-y),(x-r,-y),(x,-y+r),(x,y-r),
            (x-r,y),(-x+r,y),(-x,y-r),(-x,-y+r)]

def mesh_object(name, vertices, faces, material, part_role="container"):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    ACTIVE_COLLECTION.objects.link(obj)
    obj.data.materials.append(material)
    role(obj, part_role)
    return obj

def loft(name, rings, material, cap_start=True, cap_end=True, part_role="container"):
    count = len(rings[0])
    vertices = [point for ring in rings for point in ring]
    faces = []
    if cap_start:
        faces.append(tuple(reversed(range(count))))
    for j in range(len(rings) - 1):
        for i in range(count):
            faces.append((j*count+i,j*count+(i+1)%count,
                          (j+1)*count+(i+1)%count,(j+1)*count+i))
    if cap_end:
        faces.append(tuple((len(rings)-1)*count+i for i in range(count)))
    obj = mesh_object(name, vertices, faces, material, part_role)
    add_bevel(obj)
    return obj

def prism(name, contour, bottom, top, material, part_role="food"):
    # Offset each edge by a physical distance rather than scaling the whole face.
    # Scaling a long, thin label produces steep bevels and unnecessary polygons.
    bevel = min(BEVEL_WIDTH, (top-bottom)/4)
    inset=[]
    for i,point in enumerate(contour):
        previous=Vector(contour[i-1]); current=Vector(point)
        following=Vector(contour[(i+1)%len(contour)])
        incoming=(current-previous).normalized(); outgoing=(following-current).normalized()
        n1=Vector((-incoming.y,incoming.x));n2=Vector((-outgoing.y,outgoing.x))
        offset=(n1+n2)*(bevel*.70/max(.01,1+n1.dot(n2)))
        inset.append(tuple(current+offset))
    rings = []
    for z,shape in ((bottom,inset),(bottom+bevel,contour),(top-bevel,contour),(top,inset)):
        rings.append([(x,y,z) for x,y in shape])
    return loft(name, rings, material, part_role=part_role)

def soft_box(name, position, size, material, part_role="food"):
    obj = prism(name, outline(size[0],size[1],min(size[:2])*.1),
                -size[2]/2, size[2]/2, material, part_role)
    obj.location = position
    return obj

def flat_label(text, y, z, width, height, x=0):
    # Reuse the category 03 lettering and label geometry on a very broad curve.
    radius = 5.0
    label = curved_label(radius, z, width, height, make_material("cream"))
    label.location = (x,y+radius,0)
    lettering = label_text(text, radius+.0002, z, width*.91, height*.76)
    lettering.location = (x,y+radius,0)
    role(label,"label"); role(lettering,"label")
    return [label, lettering]

"""Shared authoring/export workflow for Wave 2 categories 05 onward."""
from pathlib import Path
import argparse
import shutil
import sys

def prepare_asset_palette():
    prepare_palette()
    PALETTE.update(blue="#879FA8", teal="#6A9B91", white=PALETTE["cream"],
                   ink=shade_hex(PALETTE["leaf"],.29), plastic_edge=shade_hex(PALETTE["plastic"],.74))

def ball(name, position, size, color, segments=8, rings=4):
    return make_sphere(name,position,size,make_material(color),segments=segments,rings=rings,flatten=False)

def box(name,position,size,color):
    return soft_box(name,position,size,make_material(color),"component")

def disk_front(name,x,y,z,rx,rz,color,vertices=8):
    obj=make_disk(name,(x,y,z),(rx,rz),make_material(color),vertices=vertices)
    obj.rotation_euler.x=math.pi/2
    apply_transform(obj)
    return obj

def cylinder(name,position,radius,height,color,segments=10):
    obj=soft_cylinder(name,radius,-height/2,height/2,make_material(color),segments)
    obj.location=position
    return obj

def bar_between(name,start,end,width,color,depth=None):
    a,b=Vector(start),Vector(end)
    obj=ball(name,(a+b)/2,(width/2,(depth or width)/2,(b-a).length/2),color,6,4)
    obj.rotation_euler=(b-a).to_track_quat("Z","Y").to_euler()
    apply_transform(obj)
    return obj

def tube_loop(name,center,radii,thickness,color,segments=12,sides=6,plane="XZ"):
    # A genuine through-hole with a rounded tube cross-section, not an inset disk.
    vertices=[]
    for i in range(segments):
        a=i*math.tau/segments
        for j in range(sides):
            t=j*math.tau/sides
            u=(radii[0]+thickness*math.cos(t))*math.cos(a)
            v=(radii[1]+thickness*math.cos(t))*math.sin(a)
            w=thickness*math.sin(t)
            p=(u,w,v) if plane=="XZ" else (u,v,w)
            vertices.append(tuple(Vector(center)+Vector(p)))
    faces=[]
    for i in range(segments):
        for j in range(sides):
            faces.append((i*sides+j,((i+1)%segments)*sides+j,((i+1)%segments)*sides+(j+1)%sides,i*sides+(j+1)%sides))
    obj=mesh_object(name,vertices,faces,make_material(color),"hollow_handle")
    add_bevel(obj)
    return obj

def hollow_vessel(name,profile,color,segments=12,bands=None):
    # The profile runs outside -> lip -> inner wall -> inner floor. Both caps
    # are bottom faces; the upper opening is never filled by a hidden cap.
    obj=lathe(name,profile,make_material(color),segments,bands)
    obj["hollow_opening"]=True
    return obj

def clear_material():
    mat=make_material("plastic",.25)
    mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value=.2
    return mat


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


"""Fixture hierarchy export, surface anchors and real-product preview staging."""
import argparse
import shutil
import hashlib
from pathlib import Path

# Author at 1:10, then bake to metres. Original 0.45 mm / 2-segment helpers
# produce 4.5 mm furniture bevels without changing the approved helper code.
AUTHOR_SCALE=.1
SLOTS=[]
SURFACES=[]


def fbox(name,position,size,color='cream'):
    return soft_box(name,Vector(position)*AUTHOR_SCALE,Vector(size)*AUTHOR_SCALE,make_material(color),'fixture')


def fpanel(name,contour,depth,y,color):
    obj=prism(name,[(x*AUTHOR_SCALE,z*AUTHOR_SCALE) for x,z in contour],
              -depth*AUTHOR_SCALE/2,depth*AUTHOR_SCALE/2,make_material(color),'fixture')
    obj.rotation_euler.x=math.pi/2;obj.location.y=y*AUTHOR_SCALE;apply_transform(obj)
    return obj


def frib(name,points,radius,color='dark_green'):
    return rounded_rib(name,[tuple(Vector(p)*AUTHOR_SCALE) for p in points],radius*AUTHOR_SCALE,color)


def fside(name,contour,thickness,x,color):
    obj=fpanel(name,contour,thickness,0,color)
    obj.rotation_euler.z=math.pi/2;apply_transform(obj);obj.location.x=x*AUTHOR_SCALE
    return obj


def fixture_glass():
    PALETTE['fixture_glass']='#BFD5D7'
    mat=make_material('fixture_glass',.25)
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.2
    mat.node_tree.nodes['Principled BSDF'].inputs['Transmission Weight'].default_value=.75
    mat.node_tree.nodes['Principled BSDF'].inputs['IOR'].default_value=1.05
    return mat


def display_tray(parts,z,width,depth,y,products,count=3,slats=False):
    parts.append(fbox('Level tray floor',(0,y,z-.020),(width,depth,.040),'peach'))
    parts.append(fbox('Low forward tray lip',(0,y-depth/2+.015,z+.025),(width,.030,.050),'peach'))
    if slats:
        for zz in (z+.040,z+.105):parts.append(fbox('Crate back slat',(0,y+depth/2-.015,zz),(width,.025,.045),'peach'))
        for side in (-1,1):
            for zz in (z+.030,z+.095):parts.append(fbox('Crate end slat',(side*(width/2-.015),y,zz),(.030,depth-.06,.044),'peach'))
    else:
        parts.append(fbox('High rear tray lip',(0,y+depth/2-.015,z+.065),(width,.030,.130),'peach'))
        profile=[(y-depth/2,z),(y+depth/2,z),(y+depth/2,z+.13),(y-depth/2,z+.05)]
        for side in (-1,1):parts.append(fside('Angled tray side',profile,.030,side*(width/2-.015),'peach'))
    slots_row(z,width-.08,count,y,depth-.05,.28,products=products)


def slots_row(z,width,count=3,y=0,depth=.30,height=.38,kind='floor',products=None):
    for i in range(count):
        SLOTS.append(dict(position=[-width/2+width*(i+.5)/count,y,z],kind=kind,
            space=[width/count*.92,depth,height],products=products or []))
    if kind=='floor':SURFACES.append(dict(z=z,y=y,width=width,depth=depth,count=count))


def shelf_board(parts,z,width,depth,y=0,color='cream',count=3,lip=True,height=.38,products=None):
    parts.append(fbox('Level shelf board',(0,y,z-.025),(width,depth,.050),color))
    if lip:parts.append(fbox('Small rounded front lip',(0,y-depth/2+.012,z+.012),(width,.024,.024),color))
    slots_row(z,width-.07,count,y,depth-.06,height,products=products)


def frame_posts(parts,width,depth,height,back=True):
    for x in (-width/2+.035,width/2-.035):
        for y in ((depth/2-.035,) if back else (-depth/2+.035,depth/2-.035)):
            parts.append(fbox('Chunky green upright',(x,y,height/2),(.070,.070,height),'dark_green'))


def rounded_sign(parts,width,z_bottom,z_top,depth=.045,y=.12,color='dark_green'):
    radius=min(.12,width*.18)
    contour=[(-width/2,z_bottom),(width/2,z_bottom),(width/2,z_top-radius)]
    for i in range(1,5):
        a=i*math.pi/8;contour.append((width/2-radius+radius*math.cos(a),z_top-radius+radius*math.sin(a)))
    contour.append((-width/2+radius,z_top))
    for i in range(1,5):
        a=math.pi/2+i*math.pi/8;contour.append((-width/2+radius+radius*math.cos(a),z_top-radius+radius*math.sin(a)))
    parts.append(fpanel('Rounded sign board',contour,depth,y,color))


def demo_path(relative):return str(Path(ARTSOURCE)/relative)


def bounds(obj):
    ps=[obj.matrix_world@v.co for v in obj.data.vertices]
    return Vector([min(p[i] for p in ps) for i in range(3)]),Vector([max(p[i] for p in ps) for i in range(3)])


def export_fixture(model,children,directory):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [model]+children:obj.select_set(True)
    bpy.context.view_layer.objects.active=model
    collider=next(o for o in children if o.name=='collider')
    collider.hide_render=False
    path=Path(directory)/(model.name+'.glb')
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
        export_yup=EXPORT_Y_UP,export_apply=True,export_animations=False,export_cameras=False,
        export_lights=False,export_texcoords=False,export_materials='EXPORT',export_extras=True)
    collider.hide_render=True
    data=path.read_bytes();n=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+n])
    assert len(doc['meshes'])==2 and len(doc['nodes'])==len(children)+1
    assert not doc.get('images') and not doc.get('textures')
    root=next(n for n in doc['nodes'] if n['name']==model.name)
    assert not any(k in root for k in ('translation','rotation','scale','matrix'))
    slot_nodes=[n for n in doc['nodes'] if n['name'].startswith('slot_')]
    assert len(slot_nodes)==len(SLOTS)
    collision=next(n for n in doc['nodes'] if n['name']=='collider')
    assert collision['extras']['hidden'] and all('material' not in p for p in doc['meshes'][collision['mesh']]['primitives'])
    root_mesh=doc['meshes'][root['mesh']]
    count=sum(doc['accessors'][p['indices']]['count']//3 for p in root_mesh['primitives'])
    assert count==model['triangles']
    assert 2<=len(doc['materials'])<=4
    return dict(sha256=hashlib.sha256(data).hexdigest(),triangles=count,collider_triangles=12,total_triangles=count+12,
                slots=len(slot_nodes),materials=len(doc['materials']))


def import_demo(relative,collection):
    path=Path(ARTSOURCE)/relative
    if not path.is_file():raise FileNotFoundError('Existing demo product not found: '+str(path))
    before=set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    imported=[o for o in bpy.data.objects if o not in before]
    meshes=[o for o in imported if o.type=='MESH']
    assert len(meshes)==1,(relative,'expected existing single-mesh product')
    obj=meshes[0]
    # Our exported products explicitly use Z-up; glTF import assumes Y-up.
    obj.data.transform(Matrix.Rotation(-math.pi/2,4,'X')@obj.matrix_world)
    obj.matrix_world=Matrix.Identity(4)
    for c in list(obj.users_collection):c.objects.unlink(obj)
    collection.objects.link(obj)
    for other in imported:
        if other!=obj:bpy.data.objects.remove(other,do_unlink=True)
    obj.parent=None;obj.name='DemoTemplate_'+path.stem
    return obj


def previews(models,records,output):
    global ACTIVE_COLLECTION
    preview(models)
    scene=bpy.context.scene;cursor=0
    for obj in models:
        obj.location.x=cursor+obj.dimensions.x/2;cursor+=obj.dimensions.x+.35
    span=cursor-.35;center=span/2;height=max(o.dimensions.z for o in models)
    ground=bpy.data.objects.get('Preview ground');ground.location.x=center;ground.scale=(span*2,span*2,1)
    target=Vector((center,0,height*.44));cam=scene.camera
    cam.location=target+Vector((.15,-1.10,.645)).normalized()*span*1.25
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale=max(span+.3,height*3.2);cam.data.clip_end=100
    for obj in scene.objects:
        if obj.type=='LIGHT':
            side=-1 if 'key' in obj.name.lower() else 1
            obj.location=(center+side*span*.28,side*span*.30,span*.65)
            obj.data.energy=(18 if side==-1 else 8)*(span/1.2)**2
            obj.data.size=span*.65
            obj.rotation_euler=(target-obj.location).to_track_quat('-Z','Y').to_euler()
    scene.render.resolution_x=3200;scene.render.resolution_y=1400;scene.render.resolution_percentage=100
    scene.cycles.samples=32
    scene.render.filepath=str(Path(output)/'Preview_Empty.png');bpy.ops.render.render(write_still=True)
    templates=bpy.data.collections.new('Demo_Templates_Hidden');scene.collection.children.link(templates);templates.hide_render=True
    demos=bpy.data.collections.new('Demo_Products_Preview_Only');scene.collection.children.link(demos)
    cache={};placements=[]
    for model,record in zip(models,records):
        for i,slot in enumerate(record['anchors']):
            options=slot['products'];assert options,'No real product assigned to slot'
            chosen=None
            for k in range(len(options)):
                relative=options[(i+k)%len(options)]
                if relative not in cache:cache[relative]=import_demo(relative,templates)
                template=cache[relative];lo,hi=bounds(template);size=hi-lo
                space=slot['space']
                if size.x<=space[0]+.001 and size.y<=space[1]+.001 and size.z<=space[2]+.001:
                    chosen=(relative,template,lo,hi);break
            assert chosen,(model.name,slot,'No product fits without rescaling')
            relative,template,lo,hi=chosen
            copy=template.copy();demos.objects.link(copy);copy.hide_render=False;copy.name='Demo_'+model.name+'_'+str(i+1)
            position=Vector(slot['position'])+model.location
            if slot['kind']=='hang':
                top=[v.co for v in template.data.vertices if v.co.z>=hi.z-.004]
                mount=sum(top,Vector())/len(top);mount.z=hi.z
            else:mount=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
            copy.location=position-mount
            copy.rotation_euler.z=slot.get('rotation_z',0)
            placements.append(dict(fixture=model.name,slot=i+1,product=relative,scale=1,kind=slot['kind'],mount_local=list(mount)))
    scene.render.filepath=str(Path(output)/'Preview_Stocked.png');bpy.ops.render.render(write_still=True)
    (Path(output)/'DemoPlacements.json').write_text(json.dumps(placements,indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(Path(output)/'Fixtures.blend'))


def run_fixtures():
    global ACTIVE_COLLECTION,OUTPUT_DIR,ITERATION,ARTSOURCE,SLOTS,SURFACES
    parser=argparse.ArgumentParser();parser.add_argument('--output',default=OUTPUT_DIR)
    parser.add_argument('--iteration',type=int,default=2);parser.add_argument('--artsource')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    ITERATION=args.iteration;root=Path(args.output);OUTPUT_DIR=str(root/'Iterations'/f'{ITERATION:02d}');Path(OUTPUT_DIR).mkdir(parents=True,exist_ok=True)
    ARTSOURCE=args.artsource or str(root.parent)
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    for c in list(bpy.data.collections):bpy.data.collections.remove(c)
    MATERIALS.clear();prepare_asset_palette();expansion_palette()
    scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    models=[];records=[]
    for key,builder,budget in JOBS:
        collection=bpy.data.collections.new(key);scene.collection.children.link(collection)
        source=bpy.data.collections.new(key+'_EditableParts');collection.children.link(source)
        ACTIVE_COLLECTION=source;SLOTS=[];SURFACES=[];parts=builder()
        for p in parts:
            apply_modifiers(p)
            p.data.transform(Matrix.Scale(1/AUTHOR_SCALE,4));p.location*=1/AUTHOR_SCALE
        bpy.context.view_layer.update()
        ps=[p.matrix_world@v.co for p in parts for v in p.data.vertices]
        low=Vector([min(p[i] for p in ps) for i in range(3)]);high=Vector([max(p[i] for p in ps) for i in range(3)])
        offset=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
        ACTIVE_COLLECTION=collection;copies=[]
        for p in parts:
            copy=p.copy();copy.data=p.data.copy();collection.objects.link(copy);copies.append(copy)
        model=join_and_seat(key,copies,budget-12);models.append(model)
        for slot in SLOTS:slot['position']=list(Vector(slot['position'])-offset)
        children=[]
        for i,slot in enumerate(SLOTS):
            obj=bpy.data.objects.new(f'slot_{i+1:02d}',None);collection.objects.link(obj);obj.parent=model
            obj.location=slot['position'];obj.empty_display_size=.035;obj['kind']=slot['kind'];obj['front']='-Y';obj['space_m']=slot['space'];children.append(obj)
            obj.rotation_euler.z=slot.get('rotation_z',0)
            if slot.get('rotation_z',0):obj['front']='+Y'
            if slot['kind']=='floor':
                hit,point,*_=model.ray_cast(obj.location+Vector((0,0,.003)),Vector((0,0,-1)),distance=.020)
                assert hit and abs(point.z-obj.location.z)<.0015,(key,obj.name,'Slot not seated on surface',tuple(point))
            else:
                from mathutils.bvhtree import BVHTree
                tree=BVHTree.FromPolygons([v.co for v in model.data.vertices],[list(f.vertices) for f in model.data.polygons])
                nearest=tree.find_nearest(obj.location,.035)
                assert nearest[0] is not None,(key,obj.name,'Hanging anchor misses the hook')
        lo,hi=bounds(model)
        verts=[(x,y,z) for x in (lo.x,hi.x) for y in (lo.y,hi.y) for z in (lo.z,hi.z)]
        faces=[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)]
        mesh=bpy.data.meshes.new('collider_box');mesh.from_pydata(verts,[],faces);mesh.update()
        collision=bpy.data.objects.new('collider',mesh);collection.objects.link(collision);collision.parent=model
        collision['hidden']=True;collision['usage']='collision_only';collision.display_type='WIRE';collision.hide_render=True;children.append(collision)
        record=export_fixture(model,children,OUTPUT_DIR)
        source.hide_render=True;source.hide_viewport=True
        record.update(name=key,budget_exclusive=budget,anchors=SLOTS.copy(),size_m=list(hi-lo),surfaces=SURFACES.copy())
        records.append(record);print('FIXTURE PASS',key,record['triangles'],'triangles',record['slots'],'slots',flush=True)
        # Blender object names are global; retain exact exported child names per file.
        for child in children:child.name=key+'__'+child.name
    (Path(OUTPUT_DIR)/'Validation.json').write_text(json.dumps(dict(group=GROUP,iteration=ITERATION,blender=bpy.app.version_string,fixtures=records),indent=2))
    previews(models,records,OUTPUT_DIR)
    for name in [j[0]+'.glb' for j in JOBS]+['Fixtures.blend','Preview_Empty.png','Preview_Stocked.png','Validation.json','DemoPlacements.json']:
        shutil.copy2(Path(OUTPUT_DIR)/name,root/name)
    script=Path(__file__).resolve()
    if script!=(root/SCRIPT_NAME).resolve():shutil.copy2(script,root/SCRIPT_NAME)
    print('GROUP COMPLETE',GROUP,'iteration',ITERATION,flush=True)


GROUP='D'
SCRIPT_NAME='BuildFixturesD.py'

def toy_cubby_shelf():
    parts=[]
    for side in (-1,1):
        parts.append(fbox('Rounded outer cubby side',(side*.465,0,.50),(.070,.40,1.0),'dark_green'))
        parts.append(fbox('Rounded outer cubby crossbar',(0,0,.50+side*.465),(.86,.40,.070),'dark_green'))
    parts.append(fbox('Cream cubby back',(0,.170,.50),(.86,.035,.86)))
    for x in (-.14,.14):parts.append(fbox('Cream vertical cubby divider',(x,.005,.50),(.035,.30,.86)))
    products=['Wave2_06_ToysGames/teddy_bear.glb','Wave2_06_ToysGames/toy_car.glb','Wave2_06_ToysGames/building_blocks.glb']
    for z in (.115,.535):
        parts.append(fbox('Cream cubby floor',(0,0,z-.0225),(.86,.32,.045)))
        slots_row(z,.84,3,-.015,.27,.35,products=products)
        for slot in SLOTS[-3:]:slot['space'][0]=.23
    if ITERATION>=2:parts.append(fbox('Cream upper inside lining',(0,0,.921),(.86,.32,.025)))
    return parts

def stationery_stand():
    parts=[]
    profile=[(-.25,.035),(.25,.035),(.25,1.20),(.14,1.20),(-.25,.45)]
    for side in (-1,1):parts.append(fside('Slanted green stationery side',profile,.045,side*.425,'dark_green'))
    parts.append(fbox('Low open box floor',(0,0,.075),(.80,.43,.050)))
    for y in (-.20,.20):parts.append(fbox('Low box lip',(0,y,.15),(.80,.030,.10)))
    slots_row(.10,.75,3,0,.30,.27,products=['Wave2_07_Stationery/notebook.glb'])
    products=['Wave2_07_Stationery/pencils.glb','Wave2_07_Stationery/pens.glb','Wave2_07_Stationery/markers.glb']
    for z,y in ((.45,-.17),(.80,-.015),(1.15,.14)):
        shelf_board(parts,z,.81,.18,y,count=3,height=.27,products=products)
    if ITERATION>=2:
        for z,y in ((.45,-.17),(.80,-.015),(1.15,.14)):
            for x in (-.135,.135):parts.append(fbox('Small stationery compartment divider',(x,y,z+.045),(.012,.15,.09)))
    return parts

def homeware_shelf():
    parts=[];frame_posts(parts,1.1,.40,1.50,False)
    products=['Expansion_08_HomeComfort/blanket.glb','Expansion_08_HomeComfort/cushion.glb',
              'Wave2_08_Kitchenware/saucepan.glb','Expansion_08_HomeComfort/table_lamp.glb']
    for z in (.22,.65,1.08):shelf_board(parts,z,1.0,.38,color='peach',count=2,height=.36,lip=False,products=products)
    parts.append(fbox('Open back upper frame brace',(0,.165,1.455),(1.00,.055,.045),'dark_green'))
    if ITERATION>=2:
        for x in (-.48,.48):parts.append(fbox('Rear stability support',(x,.162,.85),(.035,.045,1.15),'dark_green'))
    return parts

def baby_care_shelf():
    parts=[];frame_posts(parts,.90,.40,1.35,False)
    parts += [fbox('Cream baby shelf back',(0,.165,.76),(.79,.035,1.14)),
              fbox('Narrow stable baby shelf base',(0,0,.08),(.84,.36,.16),'dark_green')]
    rounded_sign(parts,.84,1.30,1.60,depth=.040,y=.13)
    rows=[(.24,['Expansion_06_BabyCare/diapers.glb']),
          (.65,['Expansion_06_BabyCare/baby_bottle.glb','Expansion_06_BabyCare/baby_wipes.glb']),
          (1.06,['Expansion_06_BabyCare/baby_food.glb','Expansion_06_BabyCare/baby_cereal.glb'])]
    for z,products in rows:shelf_board(parts,z,.79,.32,y=-.025,count=2,height=.33,products=products)
    if ITERATION>=2:parts.append(fbox('Cream rounded header lower trim',(0,.105,1.33),(.74,.030,.030)))
    return parts

JOBS=[('fixture_13_toy_cubby_shelf',toy_cubby_shelf,2500),('fixture_14_stationery_stand',stationery_stand,2500),
      ('fixture_15_homeware_shelf',homeware_shelf,2500),('fixture_16_baby_care_shelf',baby_care_shelf,2500)]
if __name__=='__main__':run_fixtures()
