"""Bundle native modelling sources into one self-contained deliverable script."""
from pathlib import Path
import ast
import sys

root=Path(__file__).resolve().parents[2]
category=sys.argv[1]
folder=sys.argv[2]
previous=(root/"Scripts/Blender/BuildReadyMeals.py").read_text(encoding="utf-8-sig")
tree=ast.parse(previous)
base=previous[:previous.index("\nSEGMENTS = 10")].replace("Wave2_04_ReadyMeals",folder)
keys=("shade_hex","prepare_palette","lathe","soft_cylinder","curved_label","label_text",
      "role","outline","mesh_object","loft","prism","soft_box","flat_label")
shared="\n\n".join(ast.get_source_segment(previous,node) for name in keys for node in tree.body
                       if isinstance(node,ast.FunctionDef) and node.name==name)
script=base+'\nSEGMENTS=10\nFONT_PATH="C:/Windows/Fonts/arialbd.ttf"\nITERATION=3\n\n'+shared+"\n\n"
script+=(root/"Scripts/Blender/Wave2AssetWorkflow.py").read_text()+"\n\n"
script+=(root/"Scripts/Blender"/(category+"Geometry.py")).read_text()
compile(script,category,"exec")
target=root/"Scripts/Blender"/("Build"+category+".py")
target.write_text(script,encoding="utf-8")
(root/"ArtSource"/folder).mkdir(parents=True,exist_ok=True)
(root/"Library/Wave2"/category).mkdir(parents=True,exist_ok=True)
print(target)
