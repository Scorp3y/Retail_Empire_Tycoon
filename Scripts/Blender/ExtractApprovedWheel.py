"""Read-only extraction of the approved hatchback's evaluated wheel geometry."""
import argparse
import hashlib
import json
import sys
from pathlib import Path
import bpy

parser=argparse.ArgumentParser()
parser.add_argument('--project',required=True,type=Path)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
source=args.project/'ArtSource/TrafficVehicles/01_Hatchback/01_Hatchback.blend'
if Path(bpy.data.filepath).resolve()!=source.resolve():
    raise RuntimeError('Open the approved hatchback, not an empty scene')
root=bpy.data.objects['Wheel_Front_L']
deps=bpy.context.evaluated_depsgraph_get()
parts=[]
for obj in root.children:
    evaluated=obj.evaluated_get(deps)
    mesh=evaluated.to_mesh()
    transform=root.matrix_world.inverted()@evaluated.matrix_world
    uv=mesh.uv_layers.active
    parts.append({'name':obj.name.removesuffix(' Front_L'),
                  'material':obj.data.materials[0].name,
                  'vertices':[list(transform@v.co) for v in mesh.vertices],
                  'faces':[list(p.vertices) for p in mesh.polygons],
                  'smooth':[p.use_smooth for p in mesh.polygons],
                  'uv':[list(v.uv) for v in uv.data]})
    evaluated.to_mesh_clear()
destination=args.project/'Library/IndividualVehiclePipeline/ApprovedWheel.json'
destination.parent.mkdir(parents=True,exist_ok=True)
destination.write_text(json.dumps({'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
                                  'radius':.315,'parts':parts}),encoding='utf-8')
print('Approved wheel extracted read-only: '+str(len(parts))+' parts',flush=True)
