"""Build an editable low-poly regional train and its Unity FBX modules.

Run with Blender --background --factory-startup --python this_file -- --project PATH.
Never connects to, clears, or saves the user's interactive Blender session.
Blender coordinates: X across, +Y towards the cab, Z up. Dimensions are metres.
"""

import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


COLORS = {
    "Cream": (0.85, 0.82, 0.69),
    "Green": (0.09, 0.32, 0.29),
    "Red": (0.65, 0.21, 0.15),
    "Roof": (0.34, 0.38, 0.41),
    "Glass": (0.28, 0.45, 0.54),
    "Dark": (0.13, 0.17, 0.18),
    "Steel": (0.43, 0.49, 0.48),
    "Lamp": (1.0, 0.91, 0.62),
}


def linear(value):
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


class TrainModule:
    """Owns the editable, named mesh parts of one car; no destructive mesh joins."""

    def __init__(self, name, materials):
        self.collection = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(self.collection)
        self.materials = materials
        self.parts = []

    def attach(self, obj, name, color):
        obj.name = name
        for collection in tuple(obj.users_collection):
            collection.objects.unlink(obj)
        self.collection.objects.link(obj)
        obj.data.materials.append(self.materials[color])
        for face in obj.data.polygons:
            face.use_smooth = False
        self.parts.append(obj)
        return obj

    def box(self, name, position, size, color, rotation=None):
        bpy.ops.mesh.primitive_cube_add(size=1, location=position)
        obj = bpy.context.object
        obj.dimensions = size
        if rotation:
            obj.rotation_euler = rotation
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        return self.attach(obj, name, color)

    def prism(self, name, cross_section, y_min, y_max, color):
        # cross_section is a clockwise ring in X/Z, seen from the front.
        vertices = [(x, y, z) for y in (y_min, y_max) for x, z in cross_section]
        count = len(cross_section)
        faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
        faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count)
                  for i in range(count)]
        return self.mesh(name, vertices, faces, color)

    def mesh(self, name, vertices, faces, color):
        data = bpy.data.meshes.new(name)
        data.from_pydata(vertices, [], faces)
        data.update()
        obj = bpy.data.objects.new(name, data)
        self.collection.objects.link(obj)
        self.parts.append(obj)
        obj.data.materials.append(self.materials[color])
        # A single open pane has no enclosed volume: automatic outside detection can
        # reverse its intended face. Keep explicitly authored winding on open panes.
        if len(faces) > 1:
            bpy.ops.object.select_all(action="DESELECT")
            bpy.context.view_layer.objects.active = obj
            obj.select_set(True)
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.select_all(action="SELECT")
            bpy.ops.mesh.normals_make_consistent(inside=False)
            bpy.ops.object.mode_set(mode="OBJECT")
            obj.select_set(False)
        return obj

    def disc(self, name, position, radius, depth, color, axis="X"):
        rotation = (0, math.pi / 2, 0) if axis == "X" else (math.pi / 2, 0, 0)
        bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=radius, depth=depth,
                                           location=position, rotation=rotation)
        return self.attach(bpy.context.object, name, color)

    def window_sides(self, cab):
        for side in (-1, 1):
            for index in range(7):
                self.box(f"Passenger window {side} {index}",
                         (side * 1.356, -3.24 + index * .99, 2.28),
                         (.022, .77, .85), "Glass")
            for y in (-4.5, 4.04):
                self.box(f"Door surround {side} {y}", (side * 1.357, y, 1.96),
                         (.025, .80, 2.05), "Dark")
                self.box(f"Door panel {side} {y}", (side * 1.375, y, 1.96),
                         (.025, .72, 1.98), "Cream")
                self.box(f"Door lower paint {side} {y}", (side * 1.393, y, 1.43),
                         (.014, .72, .91), "Green")
                self.box(f"Door belt {side} {y}", (side * 1.403, y, 1.87),
                         (.012, .72, .11), "Red")
                self.box(f"Door window {side} {y}", (side * 1.396, y, 2.37),
                         (.02, .40, .69), "Glass")
                self.box(f"Boarding step {side} {y}", (side * 1.32, y, .80),
                         (.32, .77, .10), "Steel")
            if cab:
                self.box(f"Cab side window {side}", (side * 1.353, 4.78, 2.48),
                         (.024, .45, .70), "Glass")

    def running_gear(self):
        for y in (-3.45, 3.45):
            self.box(f"Bogie {y}", (0, y, .51), (1.81, 2.24, .35), "Dark")
            for axle in (-.69, .69):
                for side in (-1, 1):
                    # Rail contact is at +/-0.72 m; wider bodies do not imply a wider gauge.
                    self.disc(f"Wheel {side} {y} {axle}",
                              (side * .76, y + axle, .34), .34, .18, "Dark")
                    self.disc(f"Wheel hub {side} {y} {axle}",
                              (side * .86, y + axle, .34), .22, .025, "Steel")
        for y in (-1.1, 1.2):
            self.box(f"Underfloor equipment {y}", (0, y, .57),
                     (1.74, 1.72, .48), "Dark")
        for y in (-5.73, 5.73):
            self.box(f"Coupling beam {y}", (0, y, .80), (.30, .60, .20), "Dark")
            self.box(f"Coupling head {y}", (0, y + (.14 if y > 0 else -.14), .80),
                     (.48, .19, .28), "Steel")

    def body(self, cab):
        y_max = 4.96 if cab else 5.36
        self.prism("Cream car shell", [(-1.35, .92), (1.35, .92), (1.35, 3.01),
                   (1.12, 3.31), (-1.12, 3.31), (-1.35, 3.01)], -5.36, y_max, "Cream")
        self.box("Lower green body", (0, (y_max - 5.36) / 2, 1.40),
                 (2.71, y_max + 5.36, .92), "Green")
        self.box("Continuous red belt", (0, (y_max - 5.36) / 2, 1.87),
                 (2.724, y_max + 5.36, .11), "Red")
        self.prism("Faceted gray roof", [(-1.37, 3.02), (1.37, 3.02),
                   (1.12, 3.36), (-1.12, 3.36)], -5.39, y_max, "Roof")
        for y in (-2.6, 1.6):
            self.box(f"Roof equipment {y}", (0, y, 3.47), (1.05, .96, .22), "Roof")
        for y in (-5.43, 5.43):
            if cab and y > 0:
                continue
            self.box(f"Gangway {y}", (0, y, 2.00), (1.12, .26, 1.83), "Dark")
            for x in (-.55, .55):
                self.box(f"Gangway fold {y} {x}", (x, y, 2.00),
                         (.11, .36, 1.83), "Roof")
        self.window_sides(cab)
        self.running_gear()
        if cab:
            self.cab()

    def cab(self):
        # Four broad height rings define the sloped nose without a rounded high-poly surface.
        rings = [(.92, 5.66, 1.26), (1.80, 5.70, 1.32),
                 (1.93, 5.67, 1.32), (3.02, 4.99, 1.13)]
        colors = ["Green", "Red", "Cream"]
        for index in range(3):
            z0, y0, x0 = rings[index]
            z1, y1, x1 = rings[index + 1]
            self.mesh(f"Cab nose band {index}",
                      [(-x0, 4.94, z0), (x0, 4.94, z0), (x0, y0, z0), (-x0, y0, z0),
                       (-x1, 4.94, z1), (x1, 4.94, z1), (x1, y1, z1), (-x1, y1, z1)],
                      [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
                       (3, 7, 6, 2), (0, 4, 7, 3), (1, 2, 6, 5)], colors[index])
        self.mesh("Cab roof chamfer", [(-1.13, 4.99, 3.02), (1.13, 4.99, 3.02),
                  (1.12, 4.68, 3.36), (-1.12, 4.68, 3.36)], [(3, 2, 1, 0)], "Roof")
        def front_y(z):
            return 5.67 + (z - 1.93) * (4.99 - 5.67) / (3.02 - 1.93) + .018
        for side in (-1, 1):
            x0, x1 = sorted((side * .11, side * 1.03))
            self.mesh(f"Front windshield {side}",
                      [(x0, front_y(2.10), 2.10), (x1, front_y(2.10), 2.10),
                       (x1 * .91, front_y(2.78), 2.78), (x0 * .91, front_y(2.78), 2.78)],
                      [(3, 2, 1, 0)], "Glass")
            self.disc(f"Headlight {side}", (side * .86, 5.716, 1.54), .145, .035, "Lamp", "Y")
            self.disc(f"Marker light {side}", (side * .86, 5.716, 1.22), .075, .037, "Red", "Y")
        self.box("Dark front buffer", (0, 5.67, .92), (2.62, .22, .29), "Dark")
        self.prism("Lower nose skirt", [(-1.23, .79), (1.23, .79),
                   (.98, .45), (-.98, .45)], 5.45, 5.72, "Roof")

    def export(self, path):
        bpy.ops.object.select_all(action="DESELECT")
        for obj in self.parts:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = self.parts[0]
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
                                 object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                                 apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                                 use_mesh_modifiers=True, mesh_smooth_type="FACE",
                                 bake_anim=False, use_custom_props=False, path_mode="AUTO")
        bpy.ops.object.select_all(action="DESELECT")

    def statistics(self):
        return {"parts": len(self.parts), "triangles": sum(
            sum(len(p.vertices) - 2 for p in obj.data.polygons) for obj in self.parts)}


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def build(project):
    source = project / "ArtSource/RegionalTrain"
    models = project / "Assets/Art/RegionalTrain/Models"
    pictures = project / "Docs/TrainBlenderReview"
    for folder in (source, models, pictures):
        folder.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    materials = {}
    for name, rgb in COLORS.items():
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*[linear(c) for c in rgb], 1)
        mat.use_nodes = True
        shader = mat.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = mat.diffuse_color
        shader.inputs["Roughness"].default_value = 1
        materials[name] = mat
    cab = TrainModule("RegionalCab", materials)
    cab.body(True)
    for obj in cab.parts:
        if obj.name.startswith("Front windshield") and obj.data.polygons[0].normal.y <= .5:
            raise RuntimeError("Front windshield faces inward: " + obj.name)
        if obj.name == "Cab roof chamfer" and obj.data.polygons[0].normal.z <= .5:
            raise RuntimeError("Cab roof faces inward")
    coach = TrainModule("RegionalCoach", materials)
    coach.body(False)
    cab.export(models / "RegionalCab.fbx")
    coach.export(models / "RegionalCoach.fbx")
    # A separate presentation collection references the editable source modules.
    presentation = bpy.data.collections.new("Train presentation")
    scene.collection.children.link(presentation)
    for index in range(4):
        instance = bpy.data.objects.new(f"Car {index + 1}", None)
        instance.instance_type = "COLLECTION"
        instance.instance_collection = cab.collection if index in (0, 3) else coach.collection
        instance.location = (0, -12 * index, 0)
        if index == 3:
            instance.rotation_euler.z = math.pi
        presentation.objects.link(instance)
    scene.collection.children.unlink(cab.collection)
    scene.collection.children.unlink(coach.collection)
    scene.name = "Train presentation"
    # Separate source scenes make named mesh parts directly editable without unpacking instances.
    for module in (cab, coach):
        source_scene = bpy.data.scenes.new(module.collection.name + " - editable source")
        source_scene.collection.children.link(module.collection)
        source_scene.unit_settings.system = "METRIC"
        source_scene.unit_settings.scale_length = 1
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, -18, -.025))
    floor = bpy.context.object
    floor.name = "Preview ground - not exported"
    floor_mat = bpy.data.materials.new("Preview ground")
    floor_mat.diffuse_color = (.27, .29, .27, 1)
    floor.data.materials.append(floor_mat)
    camera_data = bpy.data.cameras.new("Review camera")
    camera = bpy.data.objects.new("Review camera", camera_data)
    scene.collection.objects.link(camera)
    camera.location = (25, 28, 17)
    aim(camera, (0, -15, 1.3))
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 40
    scene.camera = camera
    world = bpy.data.worlds.new("Soft neutral daylight")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.65, .70, .78, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .6
    sun_data = bpy.data.lights.new("Daylight", "SUN")
    sun_data.energy = 2
    sun_data.angle = math.radians(18)
    sun = bpy.data.objects.new("Daylight", sun_data)
    scene.collection.objects.link(sun)
    sun.rotation_euler = tuple(math.radians(v) for v in (25, -20, -35))
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1440
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "Standard"
    for area in bpy.context.screen.areas if bpy.context.screen else []:
        if area.type == "VIEW_3D":
            area.spaces.active.region_3d.view_distance = 45
            area.spaces.active.region_3d.view_location = Vector((0, -16, 1.5))
            area.spaces.active.shading.type = "MATERIAL"
    bpy.ops.wm.save_as_mainfile(filepath=str(source / "RegionalTrain.blend"))
    scene.render.filepath = str(pictures / "BlenderTrain.png")
    bpy.ops.render.render(write_still=True)
    camera.location = (10, 14, 6)
    aim(camera, (0, 1, 1.7))
    camera_data.ortho_scale = 15
    scene.render.filepath = str(pictures / "BlenderCab.png")
    bpy.ops.render.render(write_still=True)
    metrics = {"cab": cab.statistics(), "coach": coach.statistics(),
               "palette": COLORS, "units": "metres", "blender": bpy.app.version_string}
    if metrics["cab"]["triangles"] > 2500 or metrics["coach"]["triangles"] > 2500:
        raise RuntimeError("Low-poly module exceeded the 2500-triangle budget")
    (source / "build-report.json").write_text(json.dumps(metrics, indent=2), encoding="utf-8")
    print("TRAIN_BUILD_PASS " + json.dumps(metrics))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", required=True, type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    build(args.project.resolve())
