import bpy
import bmesh
import math
import os
import json
import struct
from mathutils import Vector, Matrix

# Output is beside the saved .blend, or in the working directory if unsaved.
# Replace this with an absolute directory if preferred.
OUTPUT_DIR = os.path.join(
    os.path.dirname(bpy.data.filepath) if bpy.data.filepath else os.getcwd(),
    "FreshProduceGLB",
)
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


def build_kiwi():
    r = KIWI_RADIUS
    skin = make_sphere("Kiwi skin", (0, 0, r * 0.80), (r, r * 0.92, r),
                       make_material("kiwi_skin"), segments=12)
    cut_upper_half(skin)
    add_bevel(skin, 0.0003)
    z = skin.location.z + 0.00025
    parts = [skin, make_disk("Green cut flesh", (0, 0, z),
             (r * 0.92, r * 0.84), make_material("kiwi_flesh"))]
    parts.append(make_disk("Cream core", (0, 0, z + 0.00008),
                 (r * 0.23, r * 0.19), make_material("cream")))
    for i in range(10):
        angle = i * math.tau / 10
        parts.append(make_disk("Kiwi seed", (r * 0.57 * math.cos(angle),
                     r * 0.52 * math.sin(angle), z + 0.00010),
                     (0.00065, 0.00125), make_material("kiwi_skin"),
                     vertices=8, angle=angle))
    return parts


def build_pair(kind, radius, body_key, accent_key):
    parts = []
    for i, (x, y) in enumerate(((-radius * 0.77, -0.004), (radius * 0.77, 0.007))):
        def shape(q):
            # A shallow seam and top indentation, not separate detailed grooves.
            q.x *= 1 - 0.045 * math.exp(-((q.y / 0.23) ** 2))
            q.z -= 0.07 * max(0, q.z) ** 6
            return q
        fruit = make_sphere(kind, (x, y, radius * 0.80),
                            (radius, radius * 0.95, radius * 1.03),
                            make_material(body_key), deform=shape)
        add_blush(fruit, make_material(accent_key), 1 if i == 0 else -1)
        add_bevel(fruit)
        parts.append(fruit)
    parts.append(make_leaf((radius * 0.65, 0.007, radius * 1.75), angle=-0.5))
    return parts


def build_mango():
    length = MANGO_LENGTH
    def shape(q):
        q.y *= 0.88 + 0.16 * q.x
        q.z += 0.08 * q.x
        return q
    whole = make_sphere("Whole mango", (-0.040, 0.006, 0.027),
                        (length / 2, 0.033, 0.031), make_material("mango"),
                        segments=12, deform=shape)
    add_blush(whole, make_material("blush"))
    add_bevel(whole)
    half = make_sphere("Mango half skin", (0.045, -0.046, 0.020),
                       (length * 0.43, 0.030, 0.026), make_material("mango"),
                       segments=12)
    cut_upper_half(half)
    add_bevel(half, 0.0003)
    flesh = make_disk("Mango cut flesh", (0.045, -0.046, 0.02025),
                      (length * 0.40, 0.027), make_material("mango_flesh"))
    stone = make_sphere("Simple mango stone", (0.045, -0.046, 0.0205),
                        (0.018, 0.008, 0.0018), make_material("mango_flesh"),
                        rings=4, flatten=False)
    return [whole, half, flesh, stone]


def build_cherries():
    w, d, h = BOX_SIZE
    bpy.ops.mesh.primitive_cube_add(size=1)
    box = register(bpy.context.object, "Clear open cherry box", make_material("plastic", 0.14))
    box.scale = (w, d, h)
    apply_transform(box)
    # Open top, real wall thickness, and bevelled plastic edges.
    bm = bmesh.new()
    bm.from_mesh(box.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.z > 0.5], context="FACES")
    bm.to_mesh(box.data)
    bm.free()
    box.location.z = h / 2
    solidify = box.modifiers.new("Plastic wall thickness", "SOLIDIFY")
    solidify.thickness = 0.00055
    solidify.offset = -1
    add_bevel(box, 0.00035)
    parts = [box]
    for i in range(6):
        x = (i % 3 - 1) * 0.023
        y = (i // 3 - 0.5) * 0.024
        z = 0.012 + (i % 2) * 0.001
        fruit = make_sphere("Cherry", (x, y, z),
                            (CHERRY_RADIUS, CHERRY_RADIUS, CHERRY_RADIUS * 0.94),
                            make_material("cherry"), rings=4)
        add_bevel(fruit, 0.00018)
        parts += [fruit, make_stem((x, y, z + 0.007),
                   ((i % 3 - 1) * 0.003, 0.004, 0.045 + (i % 2) * 0.003))]
    return parts


def build_eggplant():
    height = EGGPLANT_HEIGHT
    def shape(q):
        q.x *= 0.83 - 0.20 * q.z
        q.y *= 0.90 - 0.12 * q.z
        q.x += 0.14 * q.z * q.z
        return q
    fruit = make_sphere("Plump eggplant", (0, 0, height * 0.36),
                        (0.038, 0.033, height * 0.44), make_material("eggplant"),
                        segments=12, deform=shape)
    add_blush(fruit, make_material("eggplant_light"))
    add_bevel(fruit)
    cap_z = height * 0.75
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.020, radius2=0.006,
                                  depth=0.018, location=(0.004, 0, cap_z))
    cap = register(bpy.context.object, "Star shaped calyx", make_material("leaf"))
    for vertex in cap.data.vertices:
        if vertex.co.z < 0:
            angle = math.atan2(vertex.co.y, vertex.co.x)
            if round(angle / (math.pi / 4)) % 2:
                vertex.co.x *= 0.55
                vertex.co.y *= 0.55
                vertex.co.z += 0.006
    add_bevel(cap, 0.0003)
    stem = make_stem((0.004, 0, cap_z + 0.005),
                     (0.015, 0.002, height), radius=0.003)
    return [fruit, cap, stem]


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


def main():
    global ACTIVE_COLLECTION
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for obj in list(bpy.context.scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    MATERIALS.clear()
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    try:
        bpy.ops.export_scene.gltf.get_rna_type()
    except (AttributeError, RuntimeError):
        bpy.ops.preferences.addon_enable(module="io_scene_gltf2")
    jobs = [
        ("Kiwi_Half", build_kiwi, 600),
        ("Peaches_Pair", lambda: build_pair("Peach", PEACH_RADIUS, "peach", "blush"), 600),
        ("Plums_Pair", lambda: build_pair("Plum", PLUM_RADIUS, "plum", "plum_light"), 600),
        ("Mango_Whole_and_Half", build_mango, 600),
        ("Cherries_Box", build_cherries, 1000),
        ("Eggplant", build_eggplant, 600),
    ]
    objects = []
    for name, builder, limit in jobs:
        ACTIVE_COLLECTION = bpy.data.collections.new(name)
        scene.collection.children.link(ACTIVE_COLLECTION)
        obj = join_and_seat(name, builder(), limit)
        export_glb(obj, OUTPUT_DIR)
        objects.append(obj)
    preview(objects)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    print("Exported six real 3D GLBs to:", OUTPUT_DIR)


if __name__ == "__main__":
    main()
