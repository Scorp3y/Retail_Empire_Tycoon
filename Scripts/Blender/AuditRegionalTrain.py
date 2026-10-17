"""Read-only comparison of saved Blender module geometry/materials with shipped FBX.

Allows UI/scene resaves without regenerating or overwriting the artist's blend file.
Run Blender --background RegionalTrain.blend --python this_file -- --project PATH.
"""
import argparse
from collections import Counter
import hashlib
import json
import sys
from pathlib import Path

import bpy


def file_hash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def signature(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    try:
        matrix = evaluated.matrix_world
        normal_matrix = matrix.inverted().transposed().to_3x3()
        # FBX splits shared vertices at hard-normal edges; unique positions and surface
        # faces compare the same flat-shaded geometry without depending on vertex storage.
        positions = sorted(set(tuple(round(v, 4) for v in matrix @ vertex.co) for vertex in mesh.vertices))
        faces = []
        for face in mesh.polygons:
            normal = tuple(round(v, 3) for v in (normal_matrix @ face.normal).normalized())
            material = evaluated.material_slots[face.material_index].material
            shader = material.node_tree.nodes.get("Principled BSDF")
            color = tuple(round(v, 3) for v in shader.inputs["Base Color"].default_value)
            faces.append((len(face.vertices), normal, color))
        # Numeric tuples also treat +0.0 and -0.0 as equal after axis conversion.
        return tuple(positions), tuple(sorted(faces))
    finally:
        evaluated.to_mesh_clear()


def audit(project):
    report = {"status": "FAILED", "auditor_sha256": file_hash(Path(__file__)),
              "blend_sha256": file_hash(project / "ArtSource/RegionalTrain/RegionalTrain.blend"), "models": {}}
    output = project / "Library/RegionalTrainPipeline/source-audit.json"
    try:
        for name in ("RegionalCab", "RegionalCoach"):
            collection = bpy.data.collections.get(name)
            if collection is None:
                raise RuntimeError("Source collection missing: " + name)
            # Evaluate source objects in their dedicated scene, not a hidden presentation instance.
            source_scene = bpy.data.scenes.get(name + " - editable source")
            if source_scene is None:
                raise RuntimeError("Editable module scene missing: " + name)
            bpy.context.window.scene = source_scene
            expected = Counter(signature(o) for o in collection.all_objects if o.type == "MESH")
            old_objects = set(bpy.data.objects)
            path = project / "Assets/Art/RegionalTrain/Models" / (name + ".fbx")
            bpy.ops.import_scene.fbx(filepath=str(path), use_custom_normals=True)
            imported = [o for o in bpy.data.objects if o not in old_objects and o.type == "MESH"]
            actual = Counter(signature(o) for o in imported)
            if expected != actual:
                raise RuntimeError(f"{name}: saved source differs from FBX in geometry, normals, or colors; {sum((expected-actual).values())} source parts and {sum((actual-expected).values())} imported parts differ")
            report["models"][name] = {"parts": len(imported), "fbx_sha256": file_hash(path)}
        report["status"] = "PASS"
    except Exception as error:
        report["error"] = str(error)
        raise
    finally:
        output.write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(json.dumps(report))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", required=True, type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    audit(args.project.resolve())
