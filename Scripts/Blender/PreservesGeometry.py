"""Preserves geometry and preview orchestration.

This source is bundled with the unchanged helper functions from category 02
into the standalone BuildPreserves.py delivered beside the selected GLBs.
"""
from pathlib import Path
import argparse
import sys
import shutil

SEGMENTS = 10
FONT_PATH = "C:/Windows/Fonts/arialbd.ttf"
ITERATION = 4
MODEL_NAMES = ("honey_jar", "jam_jar", "peanut_butter_jar", "pickles_jar", "canned_corn", "canned_peas")
TEXT_REPORT = {}


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


def glass_material(key="plastic"):
    alpha = 0.08 if key == "plastic" else (0.16 if ITERATION >= 2 else 0.10)
    material = make_material(key, alpha)
    material.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.20
    return material


def jar_shell(radius, height, key="plastic"):
    bands = None
    if key == "pickle_glass" and ITERATION >= 3:
        edge = make_material("glass_edge", .36)
        edge.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .20
        bands = {0: edge, 1: edge, 4: edge, 5: edge}
    return lathe("Rounded glass body", [
        (0, radius - .001), (.0004, radius - .0003), (.0012, radius),
        (height * .79, radius), (height * .87, radius * .97),
        (height * .94, radius * .87), (height, radius * .86),
    ], glass_material(key), bands=bands)


def lid(radius, base, material_key, light_band=False):
    rings = [
        (base, radius - .0006),
        (base + .0004, radius + .0001),
        (base + .0012, radius + .0006),
        (base + .0023, radius + .0006),
        (base + .0030, radius),
        (base + .0103, radius),
        (base + .0114, radius - .0007),
        (base + .0120, radius - .0014),
    ]
    bands = {2: make_material("blush")} if light_band else None
    return lathe("Lid and thin raised rim", rings, make_material(material_key), bands=bands)


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


def stripe_label(radius, center_z, width):
    return curved_label(radius, center_z, width, .0020, make_material("dark_brown"))


def jar(name, radius, height, content_key, lid_key, text=None, light_band=False, icon=None):
    shell = jar_shell(radius, height, "pickle_glass" if name == "pickles_jar" else "plastic")
    cap = lid(radius * .92, height - .001, lid_key, light_band)
    parts = [shell, cap]
    if name == "pickles_jar":
        for i, (x, y) in enumerate(((-.010, -.002), (.009, -.005), (0, .012))):
            pickle = make_sphere("Pickle inside jar", (x, y, .052 if ITERATION >= 3 else .046),
                                 (.009, .009, .037 if ITERATION >= 3 else .030),
                                 make_material("leaf"), rings=6 if ITERATION >= 2 else 4, flatten=False)
            pickle.rotation_euler.y = (i - 1) * .12
            apply_transform(pickle)
            add_bevel(pickle, .0002)
            parts.append(pickle)
        if ITERATION >= 2:
            parts.append(soft_cylinder("Faint inner brine volume", radius - .0024, .002,
                                       height * .80, make_material("brine", .06), segments=8))
    else:
        if ITERATION >= 2:
            parts.append(lathe("Inner contents following rounded shoulder", [
                (.0020, radius - .0033), (.0025, radius - .0026), (.0032, radius - .0024),
                (height * .78, radius - .0024), (height * .86, radius * .92 - .0014),
                (height * .92, radius * .84 - .0014),
            ], make_material(content_key)))
        else:
            parts.append(soft_cylinder("Inner visible contents", radius - .0024, .0020,
                                       height * .82, make_material(content_key)))
    label_height = .031 if name != "peanut_butter_jar" else .036
    label_width = radius * 1.43
    label_z = height * .46
    parts.append(curved_label(radius + .00045, label_z, label_width, label_height, make_material("cream")))
    if text:
        font_width, font_height = (.95, .72) if ITERATION >= 2 else (.87, .63)
        parts.append(label_text(text, radius + .00075, label_z, label_width * font_width, label_height * font_height))
    if icon == "strawberry":
        parts += strawberry_icon(radius + .0014, label_z)
    return parts


def strawberry_icon(radius, center_z):
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=.0013, radius2=.0064,
                                  depth=.014, location=(0, -radius, center_z - .001))
    fruit = register(bpy.context.object, "Strawberry icon", make_material("cherry"))
    fruit.scale.y = .24
    apply_transform(fruit)
    add_bevel(fruit, .0003)
    leaf = make_leaf((0, -radius - .0002, center_z + .007), .014, .006, .1)
    leaf.rotation_euler.x = math.pi / 2
    apply_transform(leaf)
    return [fruit, leaf]


def can(kind):
    radius = .0375
    rings = [
        (0, radius - .0007), (.0004, radius - .0002), (.001, radius),
        (.0027, radius), (.0034, radius - .0007), (.0042, radius - .0007),
        (.0052, radius), (.0062, radius), (.0072, radius - .0007),
        (.0928, radius - .0007), (.0938, radius), (.0948, radius),
        (.0958, radius - .0007), (.0966, radius - .0007),
        (.0973, radius), (.0993, radius), (.1000, radius - .0007),
    ]
    parts = [lathe("Tin body with raised rims and ribs", rings, make_material("metal"), segments=10 if ITERATION >= 2 else 12)]
    label_color = "mango_flesh" if kind == "corn" else "leaf"
    parts.append(curved_label(radius - .00015, .050, .06, .069, make_material(label_color), full_band=True))
    front = radius + .0014
    if kind == "corn":
        corn = make_sphere("Corn icon", (0, -front, .050), (.009 if ITERATION >= 2 else .0074, .0020, .019),
                           make_material("corn_yellow" if ITERATION >= 2 else "mango"), rings=6 if ITERATION >= 2 else 4, flatten=False)
        parts.append(corn)
        if ITERATION >= 2:
            for x in (-.0025, .0025):
                for z in (.043, .050, .057):
                    kernel = make_disk("Broad geometric corn kernel", (x, -front - .0021, z),
                                       (.00125, .0020), make_material("mango_flesh"), vertices=8)
                    kernel.rotation_euler.x = math.pi / 2
                    apply_transform(kernel)
                    parts.append(kernel)
        for sign in (-1, 1):
            leaf = make_leaf((sign * .0060, -front - .0002, .043), .026, .008, sign * .60)
            leaf.rotation_euler.x = math.pi / 2
            apply_transform(leaf)
            parts.append(leaf)
    else:
        for x, z in ((-.009, .046), (.009, .046), (0, .058)):
            parts.append(make_sphere("Pea icon", (x, -front, z), (.0066, .0021, .0066),
                                     make_material("kiwi_flesh"), rings=4, flatten=False))
    return parts


def build_models():
    global ACTIVE_COLLECTION
    scene = bpy.context.scene
    jobs = [
        ("honey_jar", lambda: jar("honey_jar", .0355, .091, "mango", "mango_flesh", "Honey")),
        ("jam_jar", lambda: jar("jam_jar", .0345, .092, "deep_jam", "cherry", light_band=True, icon="strawberry")),
        ("peanut_butter_jar", lambda: jar("peanut_butter_jar", .041, .094, "peach", "dark_brown", "Peanut\nbutter")),
        ("pickles_jar", lambda: jar("pickles_jar", .034, .108, "leaf", "dark_green", "Pickles")),
        ("canned_corn", lambda: can("corn")),
        ("canned_peas", lambda: can("peas")),
    ]
    objects = []
    for name, builder in jobs:
        ACTIVE_COLLECTION = bpy.data.collections.new(name)
        scene.collection.children.link(ACTIVE_COLLECTION)
        parts = builder()
        for part in parts:
            apply_modifiers(part)
        total = 0
        for part in parts:
            part.data.calc_loop_triangles()
            total += len(part.data.loop_triangles)
        text_parts = [part for part in parts if part.get("label_text")]
        if total >= 800 and text_parts:
            # User-approved geometric fallback; report it rather than pretending text exists.
            for part in text_parts:
                original = part["label_text"]
                radius = .041 if name == "peanut_butter_jar" else .034 if name == "pickles_jar" else .0355
                z = .094 * .46 if name == "peanut_butter_jar" else .108 * .46 if name == "pickles_jar" else .091 * .46
                parts.remove(part);bpy.data.objects.remove(part, do_unlink=True)
                stripe = stripe_label(radius + .0008, z, radius * .9)
                apply_modifiers(stripe);parts.append(stripe)
                TEXT_REPORT[name] = "Stripe fallback for " + original
        else:
            TEXT_REPORT[name] = "Mesh text" if text_parts else "Primitive icon"
        obj = join_and_seat(name, parts, 800)
        export_glb(obj, OUTPUT_DIR)
        objects.append(obj)
    return objects


def read_export(path):
    data = Path(path).read_bytes()
    length = struct.unpack_from("<I", data, 12)[0]
    return json.loads(data[20:20 + length])


def report_exports(objects):
    records = []
    for obj in objects:
        path = Path(OUTPUT_DIR) / (obj.name + ".glb")
        document = read_export(path)
        mesh = document["meshes"][0]
        triangles = sum(document["accessors"][p["indices"]]["count"] // 3 for p in mesh["primitives"])
        assert triangles == obj["triangles"] and triangles < 800
        assert not document.get("images") and not document.get("textures")
        nodes = [node for node in document["nodes"] if "mesh" in node]
        assert len(nodes) == 1 and not any(key in nodes[0] for key in ("translation", "rotation", "scale", "matrix"))
        if obj.name.endswith("_jar"):
            assert any(mat.get("alphaMode") == "BLEND" for mat in document["materials"])
        low = [min(document["accessors"][p["attributes"]["POSITION"]]["min"][i] for p in mesh["primitives"]) for i in range(3)]
        high = [max(document["accessors"][p["attributes"]["POSITION"]]["max"][i] for p in mesh["primitives"]) for i in range(3)]
        assert abs(low[2]) < 1e-7 and abs(low[0] + high[0]) < 1e-7 and abs(low[1] + high[1]) < 1e-7
        records.append(dict(name=obj.name,triangles=triangles,size_m=[b-a for a,b in zip(low,high)],
                            materials=len(document["materials"]),textures=0,text=TEXT_REPORT[obj.name]))
    (Path(OUTPUT_DIR) / "Validation.json").write_text(json.dumps(dict(iteration=ITERATION,blender=bpy.app.version_string,models=records),indent=2),encoding="utf-8")


def main():
    global OUTPUT_DIR, ITERATION
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default=OUTPUT_DIR)
    parser.add_argument("--iteration", default=ITERATION, type=int)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    ITERATION = args.iteration
    OUTPUT_DIR = os.path.join(args.output, "Iterations", f"{ITERATION:02d}")
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for obj in list(bpy.context.scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    MATERIALS.clear()
    prepare_palette()
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    objects = build_models()
    report_exports(objects)
    preview(objects)
    # Same lighting/scale as category 02; more pixels make geometric labels inspectable.
    scene.render.resolution_x = 2520
    scene.render.resolution_y = 920
    scene.render.filepath = os.path.join(OUTPUT_DIR, "Preview.png")
    scene.cycles.samples = 48
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUTPUT_DIR, "Preserves.blend"))
    bpy.ops.render.render(write_still=True)
    root_output = Path(args.output)
    for name in [key + ".glb" for key in MODEL_NAMES] + ["Preview.png", "Preserves.blend", "Validation.json"]:
        shutil.copy2(Path(OUTPUT_DIR) / name, root_output / name)
    # The Scripting tab does not always define __file__; exports still succeed.
    if "__file__" in globals():
        script = Path(__file__).resolve()
        if script != (root_output / "BuildPreserves.py").resolve():
            shutil.copy2(script, root_output / "BuildPreserves.py")
    print("PRESERVES iteration", ITERATION, "complete:", OUTPUT_DIR, flush=True)


if __name__ == "__main__":
    main()
