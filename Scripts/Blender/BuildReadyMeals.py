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
if os.path.basename(_script_dir) == "Wave2_04_ReadyMeals":
    OUTPUT_DIR = _script_dir
elif os.path.basename(_base_dir) == "Wave2_04_ReadyMeals":
    OUTPUT_DIR = _base_dir
else:
    OUTPUT_DIR = os.path.join(_base_dir, "Wave2_04_ReadyMeals")
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



SEGMENTS = 10
FONT_PATH = "C:/Windows/Fonts/arialbd.ttf"
ITERATION = 4

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

"""Category 04 geometry; bundled with the unchanged category 02/03 helpers."""
from pathlib import Path
import argparse
import shutil
import sys

SEGMENTS = 10
FONT_PATH = "C:/Windows/Fonts/arialbd.ttf"
ITERATION = 4
MODEL_BUDGETS = {"sandwich": 900, "wrap": 900, "sushi": 1200,
                 "ready_salad": 1200, "lasagna": 900, "chicken_soup": 900}
# Metres, matching the previous categories' real-world size convention.
SANDWICH_WIDTH = .16
WRAP_HEIGHT = .14
SUSHI_WIDTH = .18
SALAD_DIAMETER = .12
LASAGNA_WIDTH = .17
SOUP_HEIGHT = .12


def prepare_meal_palette():
    prepare_palette()
    PALETTE.update({
        "kraft": "#BEAA82", "nori": shade_hex(PALETTE["leaf"], .39),
        "tray": shade_hex(PALETTE["leaf"], .29),
        "plastic_edge": shade_hex(PALETTE["plastic"], .74), "foil": PALETTE["metal"],
        "bowl_wall": shade_hex(PALETTE["plastic"], .84),
    })


def role(obj, value):
    obj["part_role"] = value
    return obj


def clear_plastic(edge=False):
    mat = make_material("plastic_edge" if edge else "plastic", .36 if edge else .13)
    mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .2
    return mat


def foil_material():
    mat = make_material("foil")
    shader = mat.node_tree.nodes["Principled BSDF"]
    shader.inputs["Metallic"].default_value = .3
    shader.inputs["Roughness"].default_value = .5
    return mat


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


def food_sphere(name, position, radii, color, segments=6, rings=4):
    obj = make_sphere(name, position, radii, make_material(color),
                      segments=segments, rings=rings, flatten=False)
    # Same gentle facets as category 02; no subdivision or smooth shading.
    return role(obj,"food")


def flat_label(text, y, z, width, height, x=0):
    # Reuse the category 03 lettering and label geometry on a very broad curve.
    radius = 5.0
    label = curved_label(radius, z, width, height, make_material("cream"))
    label.location = (x,y+radius,0)
    lettering = label_text(text, radius+.0002, z, width*.91, height*.76)
    lettering.location = (x,y+radius,0)
    role(label,"label"); role(lettering,"label")
    return [label, lettering]


def triangle_slice(name, y, thickness, mat, width=.143, height=.092):
    obj = prism(name,[(-width/2,0),(width/2,0),(width/2,height)],
                -thickness/2,thickness/2,make_material(mat))
    obj.rotation_euler.x = math.pi/2
    obj.location = (0,y,.009)
    apply_transform(obj)
    return obj


def build_sandwich():
    parts = [soft_box("Cream wedge pack base",(0,0,.004),(.16,.078,.008),make_material("cream"),"container")]
    back = triangle_slice("Cardboard triangular back",.035,.002,"cream",.155,.101)
    role(back,"container"); parts.append(back)
    for y,mat,thick in ((-.022,"mango",.012),(-.010,"leaf",.007),
                        (-.002,"blush",.007),(.006,"mango_flesh",.006),(.018,"mango",.012)):
        slice_obj=triangle_slice("Sandwich bread / filling " + mat,y,thick,mat)
        if mat=="mango":
            slice_obj.data.materials.append(make_material("cream"))
            for face in slice_obj.data.polygons:
                if abs(face.normal.y)>.9: face.material_index=1
        parts.append(slice_obj)
    # A clear triangular front and a sloped top are distinct from the paper frame.
    parts.append(mesh_object("Clear triangular window",[(-.076,-.039,.009),(.076,-.039,.009),(.076,-.039,.108)],
                             [(0,1,2)],clear_plastic(),"window"))
    parts.append(mesh_object("Clear sloping window",[(-.076,-.039,.009),(.076,-.039,.108),
                             (.076,.034,.108),(-.076,.034,.009)],[(0,1,2,3)],clear_plastic(),"window"))
    parts.append(soft_box("Lower paper window frame",(0,-.040,.015),(.157,.003,.018),make_material("cream"),"container"))
    parts.append(soft_box("Green pack stripe",(0,-.0418,.008),(.153,.001,.004),make_material("leaf"),"label"))
    parts += flat_label("Sandwich",-.042,.016,.108,.013)
    return parts


def build_wrap():
    parts=[]
    for index,(cx,top) in enumerate(((-.019,WRAP_HEIGHT),(.019,.121))):
        contour=[(cx+.022*math.cos(i*math.tau/8),.017*math.sin(i*math.tau/8)) for i in range(8)]
        rings=[]
        for z,scale in ((.032,.82),(.034,.88),(top-.001,1),(top, .96)):
            rings.append([(cx+(x-cx)*scale,y*scale,z+.30*y if z>.04 else z) for x,y in contour])
        tortilla=loft("Tortilla half " + str(index+1),rings,make_material("cream"),part_role="food")
        parts.append(tortilla)
        filling=make_disk("Lettuce-filled cut end",(cx,0,top+.0002),(.0175,.0125),make_material("leaf"),vertices=8)
        for vertex in filling.data.vertices: vertex.co.z=.30*vertex.co.y
        role(filling,"food");parts.append(filling)
        # The food is above the cut face; the sleeve never substitutes for filling.
        for dx,dy,rx,ry,key in ((-.007,-.005,.008,.006,"leaf"),(.007,.004,.008,.006,"kiwi_flesh"),
                                (.004,-.007,.006,.005,"cherry"),(-.004,.006,.004,.007,"peach")):
            parts.append(food_sphere("Chunky visible wrap filling",(cx+dx,dy,top+.30*dy+.0005),
                                     (rx,ry,.0035),key,segments=6,rings=3))
    contour=outline(.079,.044,.007)
    rings=[[(x*.89,y*.89,.001) for x,y in contour],[(x,y,.006) for x,y in contour],
           [(x,y,.072 + .08*y) for x,y in contour]]
    parts.append(loft("Open kraft paper sleeve",rings,make_material("kraft"),cap_end=False))
    parts += flat_label("Wrap",-.0235,.041,.042,.024)
    return parts


def rectangular_tray(name,width,depth,top,material,crimp=False):
    shape=outline(width,depth,.009)
    if crimp:
        dense=[]
        for i,p in enumerate(shape):
            q=shape[(i+1)%len(shape)]
            for t in (0, .333, .667):
                dense.append((p[0]*(1-t)+q[0]*t,p[1]*(1-t)+q[1]*t))
        shape=dense
    rings=[]
    for z,scale in ((0,.89),(.002,.94),(top-.002,1),(top,1),(top-.002,.94),(.005,.85)):
        rings.append([(x*scale,y*scale,z+(.0011 if crimp and i%2 and z>=top-.002 else 0)) for i,(x,y) in enumerate(shape)])
    return loft(name,rings,material)


def rectangular_lid(width,depth,bottom,top):
    shape=outline(width,depth,.008)
    rings=[[(x*scale,y*scale,z) for x,y in shape] for z,scale in ((bottom,1),(bottom+.001,1.005),(top-.002,1),(top,.96))]
    lid=loft("Clear raised tray lid",rings,clear_plastic(),cap_start=False,part_role="lid")
    lid.data.materials.append(clear_plastic(edge=True))
    for face in lid.data.polygons:
        if face.index<8 or 16<=face.index<24: face.material_index=1
    return lid


def build_sushi():
    parts=[rectangular_tray("Black plastic sushi tray",SUSHI_WIDTH,.115,.014,make_material("tray")),
           rectangular_lid(.181,.116,.015,.046)]
    for index,(x,y) in enumerate(((-.054,-.026),(0,-.026),(-.054,.026),(0,.026))):
        parts.append(role(soft_cylinder("Dark nori maki band",.017,.006,.031,make_material("nori"),segments=8),"food"))
        parts[-1].location=(x,y,0)
        rice=make_disk("White rice cut face",(x,y,.0313),(.0149,.0149),make_material("cream"),vertices=8)
        role(rice,"food");parts.append(rice)
        for dx,key in ((-.004,"peach"),(.004,"kiwi_flesh")):
            bit=make_disk("Maki salmon / avocado centre",(x+dx,y,.0316),(.0055,.0065),make_material(key),vertices=6)
            role(bit,"food");parts.append(bit)
    for y in (-.026,.026):
        parts.append(food_sphere("Plump nigiri rice",(.054,y,.016),(.020,.013,.011),"cream",segments=8,rings=4))
        parts.append(soft_box("Soft salmon nigiri topping",(.054,y,.028),(.040,.024,.008),make_material("peach")))
        strap=soft_box("Nigiri nori strap",(.054,y,.0322),(.006,.024,.0012),make_material("nori"))
        parts.append(strap)
    parts.append(food_sphere("Wasabi blob",(.065,.047,.008),(.006,.005,.004),"kiwi_flesh",segments=6,rings=3))
    parts.append(food_sphere("Ginger blob",(.046,.047,.008),(.008,.004,.003),"blush",segments=6,rings=3))
    dot=make_disk("Small sauce dot",(-.077,.043,.0058),(.005,.005),make_material("dark_brown"),vertices=8)
    role(dot,"food"); parts.append(dot)
    return parts


def build_salad():
    radius=SALAD_DIAMETER/2
    def circular_rings(profile):
        return [[(r*math.cos(i*math.tau/12),r*math.sin(i*math.tau/12),z) for i in range(12)] for z,r in profile]
    parts=[loft("Clear salad bowl",circular_rings([(0,.038),(.001,.039),(.003,.040),(.049,radius),(.052,radius)]),
                clear_plastic(),cap_end=False)]
    parts.append(loft("Salad clear domed lid",circular_rings([(.052,.061),(.054,.061),(.060,.056),(.066,.044),(.068,.012)]),
                      clear_plastic(),cap_start=False,part_role="lid"))
    wall=make_material("bowl_wall",.17)
    wall.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value=.2
    parts[0].data.materials[0]=wall
    for obj,start in ((parts[0],37),(parts[1],0)):
        obj.data.materials.append(clear_plastic(edge=True))
        for face in obj.data.polygons:
            if start<=face.index<start+12 or (obj==parts[0] and 1<=face.index<13): face.material_index=1
    for i in range(8):
        angle=i*math.tau/8
        p=(.028*math.cos(angle),.028*math.sin(angle),.032+(i%3)*.004)
        leaf=food_sphere("Chunky lettuce leaf",p,(.023,.013,.009),"leaf" if i%2 else "kiwi_flesh")
        leaf.rotation_euler.z=angle+.3
        apply_transform(leaf); parts.append(leaf)
    for i,angle in enumerate((-.8,1.2,3.2)):
        x,y=.025*math.cos(angle),.025*math.sin(angle)
        wedge=prism("Tomato wedge",[(x-.011,y-.006),(x+.010,y-.006),(x+.003,y+.010)],.043,.051,make_material("cherry"))
        parts.append(wedge)
    for x,y in ((-.020,-.014),(.020,.012),(0,.026)):
        cucumber=soft_cylinder("Cucumber green skin",.010,.046,.050,make_material("leaf"),segments=8)
        cucumber.location=(x,y,0); role(cucumber,"food");parts.append(cucumber)
        face=make_disk("Cucumber pale centre",(x,y,.0502),(.008,.008),make_material("kiwi_flesh"),vertices=8)
        role(face,"food");parts.append(face)
    for x,y in ((-.009,.005),(.025,-.010),(-.027,.018)):
        parts.append(food_sphere("Olive",(x,y,.050),(.004,.005,.0035),"nori",segments=6,rings=3))
    for x,y in ((0,-.013),(.010,.004),(-.015,.022),(.027,.020)):
        parts.append(food_sphere("Corn kernel",(x,y,.051),(.003,.0035,.0025),"mango_flesh",segments=6,rings=3))
    return parts


def build_lasagna():
    parts=[rectangular_tray("Crimped silver foil tray",LASAGNA_WIDTH,.117,.027,foil_material(),crimp=True)]
    for z,thick,key in ((.009,.009,"cherry"),(.017,.006,"cream"),(.023,.006,"cherry"),(.029,.006,"mango_flesh")):
        parts.append(soft_box("Visible lasagna layer " + key,(0,0,z),(.144,.090,thick),make_material(key)))
    for x,y,r,key in ((-.040,-.020,.010,"mango"),(.029,.016,.012,"mango"),
                       (.047,-.025,.007,"cherry"),(-.014,.026,.007,"cherry")):
        patch=make_disk("Baked cheese / sauce patch",(x,y,.0323),(r,r*.7),make_material(key),vertices=8)
        role(patch,"food");parts.append(patch)
    for x,y in ((-.026,.006),(.018,-.022),(.047,.023)):
        parts.append(food_sphere("Soft baked cheese blister",(x,y,.032),(.008,.006,.0018),"mango_flesh",segments=6,rings=3))
    return parts


def chicken_icon(front,center):
    parts=[food_sphere("Chicken icon body",(-.003,front,center),(.013,.002,.009),"mango_flesh",segments=8,rings=4),
           food_sphere("Chicken icon head",(.010,front,center+.009),(.006,.002,.006),"mango_flesh",segments=6,rings=3)]
    for x in (.006,.010,.014):
        parts.append(food_sphere("Red chicken comb",(x,front,center+.015),(.0024,.0015,.0026),"cherry",segments=6,rings=3))
    beak=mesh_object("Orange chicken beak",[(.014,front-.0008,center+.009),(.021,front-.0008,center+.007),
                                          (.014,front-.0008,center+.005)],[(0,1,2)],make_material("peach"),"label")
    parts.append(beak)
    eye=make_disk("Chicken eye",(.011,front-.0022,center+.010),(.001,.001),make_material("dark_brown"),vertices=6)
    eye.rotation_euler.x=math.pi/2;apply_transform(eye);role(eye,"label");parts.append(eye)
    for obj in parts: role(obj,"label")
    return parts


def build_soup():
    def cup_rings(profile):
        return [[(r*math.cos(i*math.tau/10),r*math.sin(i*math.tau/10),z) for i in range(10)] for z,r in profile]
    parts=[loft("Open cream tapered paper soup cup",cup_rings([(0,.031),(.001,.032),(.004,.033),(.103,.042),(.106,.042),(.108,.041)]),
                make_material("cream"),cap_end=False)]
    band=loft("Green soup label band",cup_rings([(.031,.0359),(.032,.0361),(.078,.0401),(.079,.0401)]),
              make_material("leaf"),cap_start=False,cap_end=False)
    role(band,"label");parts.append(band)
    lid=lathe("Clear plastic soup lid with sip hole",[(.106,.042),(.107,.044),(.110,.044),(.112,.042),(.118,.040),(.120,.038)],
               clear_plastic())
    # A real hole rather than a painted black circle. Apply before the common bevel.
    for modifier in list(lid.modifiers): lid.modifiers.remove(modifier)
    bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=.0045,depth=.05,location=(0,-.025,.120))
    cutter=bpy.context.object
    cut=lid.modifiers.new("Real sip opening","BOOLEAN")
    cut.operation="DIFFERENCE";cut.solver="EXACT";cut.object=cutter
    apply_modifiers(lid)
    bpy.data.objects.remove(cutter,do_unlink=True)
    # Boolean cutters without a material can introduce an empty material slot.
    # Rebuild the two intentional lid slots before assigning its edge faces.
    lid.data.materials.clear()
    lid.data.materials.append(clear_plastic())
    lid.data.materials.append(clear_plastic(edge=True))
    for face in lid.data.polygons:
        face.material_index=0
        near_hole=math.hypot(face.center.x,face.center.y+.025)<.006
        outer_rim=face.center.z>.117 and math.hypot(face.center.x,face.center.y)>.034
        if near_hole or outer_rim: face.material_index=1
    add_bevel(lid)
    role(lid,"lid");parts.append(lid)
    soup=make_disk("Soup visible below sip opening",(0,0,.105),(.039,.039),make_material("mango_flesh"),vertices=10)
    role(soup,"food");parts.append(soup)
    parts+=chicken_icon(-.0393,.056)
    return parts


def build_all():
    global ACTIVE_COLLECTION
    objects=[];records=[]
    for name,builder in (("sandwich",build_sandwich),("wrap",build_wrap),("sushi",build_sushi),
                         ("ready_salad",build_salad),("lasagna",build_lasagna),("chicken_soup",build_soup)):
        product_collection=bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(product_collection)
        source=bpy.data.collections.new(name+"_EditableParts")
        product_collection.children.link(source)
        ACTIVE_COLLECTION=source
        parts=builder()
        for part in parts: apply_modifiers(part)
        coordinates=[part.matrix_world@v.co for part in parts for v in part.data.vertices]
        low=Vector(tuple(min(p[i] for p in coordinates) for i in range(3)))
        high=Vector(tuple(max(p[i] for p in coordinates) for i in range(3)))
        centre=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
        ACTIVE_COLLECTION=product_collection
        copies=[]
        for part in parts:
            duplicate=part.copy();duplicate.data=part.data.copy()
            product_collection.objects.link(duplicate);copies.append(duplicate)
        obj=join_and_seat(name,copies,MODEL_BUDGETS[name])
        export_glb(obj,OUTPUT_DIR)
        for part in parts: part.location-=centre
        source.hide_render=True;source.hide_viewport=True
        records.append(dict(name=name,triangles=obj["triangles"],budget_exclusive=MODEL_BUDGETS[name],
                            source_parts=len(parts),source_roles=sorted(set(p.get("part_role","container") for p in parts))))
        objects.append(obj)
    return objects,records


def validate_exports(records):
    for record in records:
        blob=(Path(OUTPUT_DIR)/(record["name"]+".glb")).read_bytes()
        length=struct.unpack_from("<I",blob,12)[0]
        doc=json.loads(blob[20:20+length])
        assert len(doc["meshes"])==1 and len(doc["nodes"])==1
        assert not doc.get("images") and not doc.get("textures")
        node=doc["nodes"][0]
        assert not any(k in node for k in ("translation","rotation","scale","matrix"))
        primitives=doc["meshes"][0]["primitives"]
        count=sum(doc["accessors"][p["indices"]]["count"]//3 for p in primitives)
        assert count==record["triangles"] and count<record["budget_exclusive"]
        positions=[doc["accessors"][p["attributes"]["POSITION"]] for p in primitives]
        low=[min(p["min"][i] for p in positions) for i in range(3)]
        high=[max(p["max"][i] for p in positions) for i in range(3)]
        assert abs(low[2])<1e-7 and abs(low[0]+high[0])<1e-7 and abs(low[1]+high[1])<1e-7
        record["size_m"]=[b-a for a,b in zip(low,high)]
        record["materials"]=len(doc["materials"]);record["textures"]=0
    report=dict(iteration=ITERATION,blender=bpy.app.version_string,models=records)
    (Path(OUTPUT_DIR)/"Validation.json").write_text(json.dumps(report,indent=2),encoding="utf-8")


def main():
    global OUTPUT_DIR,ITERATION
    parser=argparse.ArgumentParser()
    parser.add_argument("--output",default=OUTPUT_DIR)
    parser.add_argument("--iteration",type=int,default=ITERATION)
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    ITERATION=args.iteration
    OUTPUT_DIR=os.path.join(args.output,"Iterations",f"{ITERATION:02d}")
    os.makedirs(OUTPUT_DIR,exist_ok=True)
    if bpy.context.object and bpy.context.object.mode!="OBJECT": bpy.ops.object.mode_set(mode="OBJECT")
    for obj in list(bpy.context.scene.objects): bpy.data.objects.remove(obj,do_unlink=True)
    for collection in list(bpy.data.collections): bpy.data.collections.remove(collection)
    MATERIALS.clear();prepare_meal_palette()
    scene=bpy.context.scene
    scene.unit_settings.system="METRIC";scene.unit_settings.scale_length=1
    objects,records=build_all()
    validate_exports(records)
    preview(objects)
    # Unhiding editable parts places them exactly over their joined preview copy.
    for index,obj in enumerate(objects):
        for part in bpy.data.collections[obj.name+"_EditableParts"].objects:
            part.location.x+=index*ROW_SPACING
    scene.render.resolution_x=2760;scene.render.resolution_y=1020
    scene.cycles.samples=48
    scene.render.filepath=os.path.join(OUTPUT_DIR,"Preview.png")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUTPUT_DIR,"ReadyMeals.blend"))
    bpy.ops.render.render(write_still=True)
    root=Path(args.output)
    for filename in [name+".glb" for name in MODEL_BUDGETS]+["ReadyMeals.blend","Preview.png","Validation.json"]:
        shutil.copy2(Path(OUTPUT_DIR)/filename,root/filename)
    if "__file__" in globals():
        script=Path(__file__).resolve()
        if script.is_file() and script!=(root/"BuildReadyMeals.py").resolve(): shutil.copy2(script,root/"BuildReadyMeals.py")
    print("READY MEALS iteration",ITERATION,"complete:",OUTPUT_DIR,flush=True)


if __name__=="__main__":
    main()
