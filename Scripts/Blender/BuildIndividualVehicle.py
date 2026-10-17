"""Build exactly one individually specified car in an isolated Blender process."""
import argparse
from dataclasses import asdict
import hashlib
import json
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent))
import bpy
from BuildSingleHatchback import make_studio
from IndividualVehicleDesigns import BY_ID
from IndividualVehicleCraft import VehicleAssembly,frame_camera

parser=argparse.ArgumentParser()
parser.add_argument('--project',required=True,type=Path)
parser.add_argument('--id',required=True,choices=tuple(BY_ID))
parser.add_argument('--preview-only',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
project=args.project.resolve()
d=BY_ID[args.id]
folder=project/'ArtSource/TrafficVehicles'/d.identity
review=project/'Docs/VehicleStages'/d.identity
destination=folder/(d.identity+'.blend')
if destination.exists() and not args.preview_only:
    raise FileExistsError('Preserve existing artist edits before replacement: '+str(destination))
template=json.loads((project/'Library/IndividualVehiclePipeline/ApprovedWheel.json').read_text(encoding='utf-8'))
hatch=project/'ArtSource/TrafficVehicles/01_Hatchback/01_Hatchback.blend'
if hashlib.sha256(hatch.read_bytes()).hexdigest()!=template['source_sha256']:
    raise RuntimeError('The approved component source changed; extract it again explicitly')
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.name=d.identity+' / individual model'
scene.unit_settings.system='METRIC'
scene.unit_settings.length_unit='METERS'
car=VehicleAssembly(scene,d,template)
car.build()
make_studio(scene,review/'Reference.png')
bpy.data.objects['Approved 01 hatchback reference'].name='Packed reference / '+d.identity
scene.cycles.samples=24
scene.render.resolution_x=900
scene.render.resolution_y=600
frame_camera(scene,car.root)
car.root['design_manifest']=json.dumps(asdict(d))
car.root['reference_sha256']=hashlib.sha256((review/'Reference.png').read_bytes()).hexdigest()
notes=bpy.data.texts.new('READ ME / '+d.identity)
notes.write(d.identity+'\nOne individual editable model. Metres, +Y forward, Z up.\n'
            'Rounded body panels, shaped lenses and the approved wheel geometry.\n'
            'Four packed UV surface maps. Opaque tinted windows; no interior.\n'
            'Reference, studio and model are separate collections.\n'
            'The unseen rear surfaces continue the same style.\n'
            'Unity and traffic have not been modified. No runtime LOD/export yet.\n')
for obj in bpy.context.selected_objects:obj.select_set(False)
car.root.select_set(True)
bpy.context.view_layer.objects.active=car.root
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.overlay.show_overlays=False
            area.spaces.active.shading.type='MATERIAL'
scene.render.filepath=str(review/'Front.png')
if not args.preview_only:
    folder.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(destination))
    print('SAVED '+d.identity,flush=True)
else:
    bpy.ops.render.render(write_still=True)
