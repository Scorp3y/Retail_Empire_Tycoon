"""Audit published GLBs, reused helper ASTs and store a compact delivery record."""
from pathlib import Path
import ast
import hashlib
import json
import struct
import sys
import math

ROOT=Path(__file__).resolve().parents[2]
folder=ROOT/"ArtSource"/sys.argv[1]
script=next(folder.glob("Build*.py"))
tree=ast.parse(script.read_text(encoding="utf-8-sig"))
old=ast.parse((ROOT/"Scripts/Blender/BuildReadyMeals.py").read_text(encoding="utf-8-sig"))
def functions(t):return {n.name:ast.dump(n,include_attributes=False) for n in t.body if isinstance(n,ast.FunctionDef)}
names=("srgb_to_linear","color_rgba","make_material","register","apply_transform","make_sphere","add_bevel","apply_modifiers","make_leaf","make_disk","join_and_seat","export_glb","preview","shade_hex","prepare_palette","lathe","soft_cylinder","curved_label","label_text")
a,b=functions(tree),functions(old)
assert all(a[n]==b[n] for n in names)
for key in ("PALETTE","BEVEL_WIDTH","BEVEL_ANGLE","ROW_SPACING","EXPORT_Y_UP"):
    def value(t):
        return next(ast.dump(n.value,include_attributes=False) for n in t.body if isinstance(n,ast.Assign)
                    and any(isinstance(target,ast.Name) and target.id==key for target in n.targets))
    assert value(tree)==value(old),key
report=json.loads((folder/"Validation.json").read_text())
assert report["iteration"]>=3
for i in (1,2,3):assert (folder/"Iterations"/f"{i:02d}"/"Preview.png").is_file()
for entry in report["models"]:
    path=folder/(entry["name"]+".glb")
    blob=path.read_bytes();n=struct.unpack_from("<I",blob,12)[0];doc=json.loads(blob[20:20+n])
    assert len(doc["meshes"])==len(doc["nodes"])==1
    assert not doc.get("images") and not doc.get("textures")
    assert 2<=len(doc["materials"])<=4
    count=sum(doc["accessors"][p["indices"]]["count"]//3 for p in doc["meshes"][0]["primitives"])
    assert count==entry["triangles"] and count<entry["budget_exclusive"]
    points=[]
    for primitive in doc["meshes"][0]["primitives"]:
        if doc["materials"][primitive["material"]]["name"]=="GeometryPigment":
            assert "COLOR_0" in primitive["attributes"], "Vertex palette missing"
        accessor=doc["accessors"][primitive["attributes"]["POSITION"]]
        view=doc["bufferViews"][accessor["bufferView"]]
        start=20+n+8+view.get("byteOffset",0)+accessor.get("byteOffset",0)
        for i in range(accessor["count"]):
            point=struct.unpack_from("<3f",blob,start+i*view.get("byteStride",12))
            assert all(math.isfinite(v) for v in point)
            points.append(point)
    low=[min(p[i] for p in points) for i in range(3)]
    high=[max(p[i] for p in points) for i in range(3)]
    assert abs(low[2])<1e-7 and abs(low[0]+high[0])<1e-7 and abs(low[1]+high[1])<1e-7
    assert not any(k in doc["nodes"][0] for k in ("translation","rotation","scale","matrix"))
    entry["sha256"]=hashlib.sha256(blob).hexdigest()
    print(entry["name"],count,"triangles,",len(doc["materials"]),"materials PASS")
report.update(shared_helpers_unchanged=list(names),blender_4_runtime_tested=False,unity_import_tested=False)
(folder/"FinalAudit.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
lines=["# "+folder.name,"", "Six real 3D models. Final selected iteration: "+str(report["iteration"])+".","", "| GLB | Triangles | Materials | Metres X / Y / Z |","| --- | ---: | ---: | --- |"]
for m in report["models"]:lines.append(f'| {m["name"]}.glb | {m["triangles"]} | {m["materials"]} | '+" / ".join(f"{x:.4f}" for x in m["size_m"])+" |")
lines += ["","The folder contains the standalone Build*.py script, Models.blend, Preview.png, Validation.json, FinalAudit.json and the reviewed iteration files.","", "Each GLB has one mesh node, baked identity transforms, XY-centred origin, bottom at Z=0 and no image textures. The .blend retains hidden *_EditableParts collections at the modelling origin; the joined copies are arranged in the preview row. Solo the source collection to edit a product.","", "The 19 common functions, base palette and bevel settings from categories 02-04 are retained: 0.00045 m bevel, two segments, gentle flat facets and matte colours. New blue/teal tones satisfy the requested packaging and toy colours. Transparent plastic uses alpha 0.25 and roughness 0.20. Other surfaces use roughness 0.82 and metallic 0. Hollow source parts include actual inner wall and floor geometry and a centre ray test, recorded in Validation.json.","", "Tested with the installed Blender 5.2.3 LTS. A separate Blender 4.x runtime test was unavailable. GLBs retain requested Z-up coordinates; glTF importers assuming Y-up may require an axis correction. Unity import, alpha sorting and engine lighting remain untested. Existing Unity scenes/catalogs and the user's open Blender window were not changed. No Git commit/push was made."]
(folder/"README.md").write_text("\n".join(lines)+"\n",encoding="utf-8")
