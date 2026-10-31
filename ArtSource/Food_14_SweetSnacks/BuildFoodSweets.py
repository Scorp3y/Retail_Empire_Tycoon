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
if os.path.basename(_script_dir) == "Food_14_SweetSnacks":
    OUTPUT_DIR = _script_dir
elif os.path.basename(_base_dir) == "Food_14_SweetSnacks":
    OUTPUT_DIR = _base_dir
else:
    OUTPUT_DIR = os.path.join(_base_dir, "Food_14_SweetSnacks")
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
FONT_PATH="C:/Windows/Fonts/arialbd.ttf"
ITERATION=3
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

def run_category():
    global ACTIVE_COLLECTION,OUTPUT_DIR,ITERATION
    parser=argparse.ArgumentParser()
    parser.add_argument("--output",default=OUTPUT_DIR)
    parser.add_argument("--iteration",type=int,default=ITERATION)
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    ITERATION=args.iteration
    OUTPUT_DIR=os.path.join(args.output,"Iterations",f"{ITERATION:02d}")
    os.makedirs(OUTPUT_DIR,exist_ok=True)
    if bpy.context.object and bpy.context.object.mode!="OBJECT": bpy.ops.object.mode_set(mode="OBJECT")
    for obj in list(bpy.context.scene.objects): bpy.data.objects.remove(obj,do_unlink=True)
    for col in list(bpy.data.collections): bpy.data.collections.remove(col)
    MATERIALS.clear();prepare_asset_palette()
    scene=bpy.context.scene
    scene.unit_settings.system="METRIC";scene.unit_settings.scale_length=1
    objects=[];records=[]
    for name,builder,budget in JOBS:
        collection=bpy.data.collections.new(name);scene.collection.children.link(collection)
        source=bpy.data.collections.new(name+"_EditableParts");collection.children.link(source)
        ACTIVE_COLLECTION=source
        parts=builder()
        for p in parts:
            apply_modifiers(p)
            p.data.calc_loop_triangles()
        measured=sum(len(p.data.loop_triangles) for p in parts)
        print(name,measured,[(p.name,len(p.data.loop_triangles)) for p in parts],flush=True)
        ACTIVE_COLLECTION=collection
        copies=[]
        for p in parts:
            copy=p.copy();copy.data=p.data.copy();collection.objects.link(copy);copies.append(copy)
        obj=join_and_seat(name,copies,budget)
        export_glb(obj,OUTPUT_DIR)
        source.hide_render=True;source.hide_viewport=True
        blob=(Path(OUTPUT_DIR)/(name+".glb")).read_bytes()
        n=struct.unpack_from("<I",blob,12)[0];doc=json.loads(blob[20:20+n])
        primitives=doc["meshes"][0]["primitives"]
        triangles=sum(doc["accessors"][p["indices"]]["count"]//3 for p in primitives)
        assert triangles==obj["triangles"] and triangles<budget
        assert len(doc["nodes"])==1 and len(doc["meshes"])==1
        assert not any(k in doc["nodes"][0] for k in ("translation","rotation","scale","matrix"))
        assert not doc.get("images") and not doc.get("textures")
        assert 2<=len(doc["materials"])<=4,(name,"material count",len(doc["materials"]))
        assert all("material" in p for p in primitives)
        accessors=[doc["accessors"][p["attributes"]["POSITION"]] for p in primitives]
        low=[min(a["min"][i] for a in accessors) for i in range(3)]
        high=[max(a["max"][i] for a in accessors) for i in range(3)]
        assert abs(low[2])<1e-7 and abs(low[0]+high[0])<1e-7 and abs(low[1]+high[1])<1e-7
        for material in doc["materials"]:
            pbr=material["pbrMetallicRoughness"]
            assert pbr["metallicFactor"]==0
            target=.2 if material.get("alphaMode")=="BLEND" else .82
            assert abs(pbr["roughnessFactor"]-target)<1e-6
        # Probe the real cavity floor rather than trusting the silhouette.
        cavities=[]
        source.hide_viewport=False
        bpy.context.view_layer.update()
        for p in parts:
            if p.get("hollow_opening"):
                hit,point,*_=p.ray_cast(Vector((0,0,1)),Vector((0,0,-1)),distance=2)
                assert hit and point.z<max(v.co.z for v in p.data.vertices)-.003
                cavities.append(dict(part=p.name,inner_floor_z=point.z))
        source.hide_viewport=True
        records.append(dict(name=name,triangles=triangles,budget_exclusive=budget,materials=len(doc["materials"]),
                            size_m=[b-a for a,b in zip(low,high)],cavities=cavities,texts=[p["label_text"] for p in parts if p.get("label_text")]))
        objects.append(obj)
    report=dict(category=CATEGORY_FOLDER,iteration=ITERATION,blender=bpy.app.version_string,models=records)
    (Path(OUTPUT_DIR)/"Validation.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    preview(objects)
    if "arrange_expansion_preview" in globals(): arrange_expansion_preview(objects)
    scene.render.resolution_x=2880;scene.render.resolution_y=1120;scene.cycles.samples=40
    scene.render.filepath=os.path.join(OUTPUT_DIR,"Preview.png")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUTPUT_DIR,"Models.blend"))
    bpy.ops.render.render(write_still=True)
    root=Path(args.output)
    for filename in [name+".glb" for name,_,_ in JOBS]+["Models.blend","Preview.png","Validation.json"]:
        shutil.copy2(Path(OUTPUT_DIR)/filename,root/filename)
    if "__file__" in globals():
        script=Path(__file__).resolve()
        if script.is_file() and script!=(root/SCRIPT_NAME).resolve(): shutil.copy2(script,root/SCRIPT_NAME)
    print("CATEGORY COMPLETE",CATEGORY_FOLDER,"iteration",ITERATION,flush=True)


def pigment(obj,color):
    # Several discrete pencil colours share one matte material through vertex
    # colours. This is glTF COLOR_0 geometry data, never an image texture.
    key="GeometryPigment"
    if key not in MATERIALS:
        mat=bpy.data.materials.new(key);mat.use_nodes=True
        shader=mat.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value=(1,1,1,1)
        shader.inputs["Metallic"].default_value=0;shader.inputs["Roughness"].default_value=.82
        attr=mat.node_tree.nodes.new("ShaderNodeVertexColor");attr.layer_name="Color"
        mat.node_tree.links.new(attr.outputs["Color"],shader.inputs["Base Color"])
        MATERIALS[key]=mat
    obj.data.materials.clear();obj.data.materials.append(MATERIALS[key])
    for face in obj.data.polygons:face.material_index=0
    layer=obj.data.color_attributes.new(name="Color",type="FLOAT_COLOR",domain="CORNER")
    rgba=color_rgba(PALETTE[color])
    for value in layer.data:value.color=rgba
    return obj

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
    for obj in icons:
        for v in obj.data.vertices:
            p=obj.matrix_world@v.co;v.co=(p.x,p.z-.023,z+.0008-(p.y+.074))
        obj.matrix_world=Matrix.Identity(4)
    return icons

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
    elif kind=='calzone':
        contour=[(.079*math.cos(i*math.pi/10),.052*math.sin(i*math.pi/10)-.018) for i in range(11)]
        obj=prism('Whole crescent calzone',contour,.008,.035,make_material('pastry'));food.append(obj)
        for i in range(5):
            x=-.052+i*.026
            food.append(ball('Soft folded pastry seam',(x,-.018,.031),(.009,.004,.003),'pastry_light',6,4))
    elif kind=='samosas':
        for x,a in [(-.046,-.12),(.046,.12)]:
            obj=prism('Whole triangular samosa',[(-.039,-.031),(.039,-.031),(0,.041)],.008,.035,make_material('pastry'))
            obj.location.x=x;obj.rotation_euler.z=a;apply_transform(obj);food.append(obj)
    elif kind=='spring_rolls':
        for y in (-.034,0,.034):
            roll=lathe('Whole crisp closed spring roll',[(-.070,.009),(-.067,.013),(.067,.013),(.070,.009)],make_material('pastry'),8)
            roll.rotation_euler.y=math.pi/2;roll.location=(0,y,.024);apply_transform(roll);food.append(roll)
    elif kind=='falafel':
        for x,y in [(-.045,-.027),(.045,-.027),(-.045,.027),(.045,.027)]:
            obj=make_sphere('Whole rounded falafel',(x,y,.029),(.025,.025,.023),make_material('falafel'),segments=10,rings=6,flatten=False,
                           deform=lambda q:tuple(v*(1+.06*math.sin(q.x*16+q.z*13)) for v in q))
            add_bevel(obj,.0003);food.append(obj)
    else:
        for x in (-.046,.046):
            rice=prism('Whole rounded onigiri',[(-.036,-.023),(.036,-.023),(0,.037)],.009,.039,make_material('milk'))
            rice.location.x=x;food.append(rice)
            food.append(box('Nori strip on rice',(x,-.005,.040),(.022,.039,.0015),'dark_green'))
    icon='leaf' if kind in ('burrito','spring_rolls') else 'wheat' if kind=='calzone' else 'sesame' if kind=='falafel' else 'corn'
    return finish_pack(parts,food,sleeve_icon(icon))

def spread_jar(kind):
    final_palette();color={'chocolate_spread':'cocoa','almond_butter':'amber','cashew_butter':'grain','sunflower_seed_butter':'spice_gold'}[kind]
    shell=closed_shell('Shared hollow spread jar',[(0,.033),(.003,.036),(.078,.036),(.086,.033),(.091,.0315)])
    filling=lathe('Separate visible spread',[(.003,.031),(.005,.034),(.077,.034),(.086,.030)],make_material(color),8)
    lid=cylinder('Dark green spread lid',(0,0,.096),.034,.010,'dark_green',10)
    label=curved_label(.0365,.050,.056,.037,make_material('ivory'));icons=[]
    if kind=='chocolate_spread':
        for x in (-.006,.006):
            for z in (.045,.055):icons.append(box('Chocolate-bar moulded square',(x,-.038,z),(.010,.0015,.008),'cocoa'))
    elif kind=='almond_butter':icons=sprig(-.038,.050,'amber','almond',.9)
    elif kind=='cashew_butter':
        points=[(.010*math.cos(.7+i*4.8/6),-.038,.050+.013*math.sin(.7+i*4.8/6)) for i in range(7)]
        icons=[path_tube('Whole curved cashew icon',points,.003,'amber',6)]
    else:
        for i in range(8):
            a=i*math.tau/8
            icons.append(disk_front('Sunflower petal',.010*math.cos(a),-.038,.050+.010*math.sin(a),.005,.003,'cheese',8))
        icons.append(disk_front('Sunflower centre',0,-.039,.050,.006,.006,'cocoa',10))
    geometry_pigment([filling,lid]+icons)
    return [shell,filling,lid,label]+icons

def halva():
    final_palette();body=box('Whole sealed halva bar',(0,0,.015),(.12,.06,.026),'ivory');parts=[body];food=[]
    for side in (-1,1):
        rings=[]
        for x,depth,height in [(side*.058,.060,.022),(side*.066,.049,.009),(side*.075,.055,.006)]:
            rings.append([(x+.0015*math.sin(i*math.pi/2),y,.015+z) for i,(y,z) in enumerate(outline(depth,height,.002))])
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
        obj.location.x*=.75;obj.location.y*=.35;obj.location.z*=.75
        for v in obj.data.vertices:v.co.x*=.75;v.co.y*=.35;v.co.z*=.75
    food=[]
    for x in (-.021,0,.021):
        roll=open_profile('Whole rolled fruit leather',[(.028,.008),(.029,.009),(.083,.009),(.084,.008),
              (.084,.005),(.083,.004),(.029,.004),(.028,.005)],'fruit_red',6)
        roll.location=(x,0,0);food.append(roll)
    icons=sprig(-.013,.114,'red','apple',.65)
    return finish_pack(parts,food,icons)

def arrange_expansion_preview(objects):layout_large_props(objects)

CATEGORY_FOLDER='Food_14_SweetSnacks'
SCRIPT_NAME='BuildFoodSweets.py'
JOBS=[('popcorn',lambda:sweets('popcorn'),900),('marshmallows',lambda:sweets('marshmallows'),1200),('jelly_beans',lambda:sweets('jelly_beans'),1200),('licorice',lambda:sweets('licorice'),900),('caramels',lambda:sweets('caramels'),900),('lollipops',lambda:sweets('lollipops'),1200)]
if __name__=='__main__':run_category()
