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
if os.path.basename(_script_dir) == "Food_06_DriedLegumes":
    OUTPUT_DIR = _script_dir
elif os.path.basename(_base_dir) == "Food_06_DriedLegumes":
    OUTPUT_DIR = _base_dir
else:
    OUTPUT_DIR = os.path.join(_base_dir, "Food_06_DriedLegumes")
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
            parts.append(rounded_food('Grape icon berry',(x,y,z-row*.008*s),(.005*s,.0018*s,.005*s),color,6,3))
        return parts
    if kind=='sesame':
        for x,dz in ((-.007,-.004),(0,.005),(.007,-.004)):
            parts.append(rounded_food('Sesame seed icon',(x*s,y,z+dz*s),(.003*s,.0015*s,.0045*s),color,6,3))
        return parts
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
    parts+=sprig(-radius-.001,height*.45,icon_color,icon,.65)
    return parts

def can_food(height,radius,label,icon,icon_color,lower=None):
    pantry_palette()
    # Rim colour doubles as cream label ink; no separate texture or hidden fill.
    body=lathe('Rounded ribbed can',[(0,radius*.94),(.001,radius),(.004,radius),(.005,radius*.95),
        (height-.005,radius*.95),(height-.004,radius),(height-.001,radius),(height,radius*.94)],make_material('ivory'),12)
    band=curved_label(radius*.955,height*.50,math.tau*radius*.955,height*.79,make_material(label),True)
    parts=[body,band]
    # Full band above is a separate cylinder and intentionally follows the can.
    if lower:parts.append(soft_cylinder('Lower blue band',radius*.959,.006,.022,make_material(lower),10))
    parts+=sprig(-radius-.0005,height*.54,icon_color,icon,.65)
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
    parts+=sprig(-.033,.097,accent,'almond' if kind=='almond' else 'wheat',1.0)
    return parts

def bottle(kind):
    pantry_palette();maple=kind=='maple';height=.18 if kind in ('maple','molasses') else .20
    radius=.033 if maple else .025
    liquid='amber' if kind in ('maple','apple') else 'dark_brown'
    lid='charcoal' if maple else 'dark_brown'
    icon={'maple':'maple','molasses':'wheat','apple':'apple','balsamic':'grapes'}[kind]
    ink='red' if kind in ('maple','apple') else 'purple' if kind=='balsamic' else 'dark_brown'
    profile=[(0,radius*.85),(.003,radius),(height*.65,radius),(height*.81,.012),(height*.91,.011)]
    parts=[closed_shell('Hollow bottle',profile),
           lathe('Visible syrup or vinegar',[(.003,radius*.82),(.005,radius-.002),(height*.64,radius-.002),
                 (height*.79,.010)],make_material(liquid),10),
           cylinder('Sealed screw cap',(0,0,height*.955),.013,height*.09,lid,10),
           curved_label(radius+.0006,height*.42,radius*1.55,height*.30,make_material('ivory'))]
    parts+=sprig(-radius-.001,height*.42,ink,icon,.75)
    if maple:parts.append(tube_loop('Real through handle',(.038,0,.132),(.012,.017),.003,'plastic',10,6,'XZ'))
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
    lid=cylinder('Green removable lid',(.055,.025,.060),.061,.005,'sage',12)
    lid.rotation_euler.x=.65;apply_transform(lid);parts.append(lid)
    points=[]
    for i in range(19):
        t=i/18;a=t*math.tau*1.7;r=.004+.027*t;points.append((r*math.cos(a),r*math.sin(a),.041))
    parts.append(path_tube('Hummus swirl',points,.0014,'amber',6))
    for x,y in ((-.023,-.012),(.017,.022),(.022,-.017)):
        parts.append(rounded_food('Whole chickpea topping',(x,y,.042),(.005,.005,.004),'amber',6,3))
    parts.append(curved_label(.057,.023,.049,.021,make_material('ivory')))
    parts+=sprig(-.058,.023,'amber','sesame',.65)
    return parts

def arrange_expansion_preview(objects):
    layout_large_props(objects)

CATEGORY_FOLDER='Food_06_DriedLegumes'
SCRIPT_NAME='BuildPantryLegumes.py'
JOBS=[('chickpeas',lambda:legume(0),1200),('green_lentils',lambda:legume(1),1200),('red_kidney_beans',lambda:legume(2),1200),('white_beans',lambda:legume(3),1200),('black_beans',lambda:legume(4),1200),('dried_peas',lambda:legume(5),1200)]
if __name__=='__main__':run_category()
