"""Physical opening / solid checks on the final editable Blender sources."""
from pathlib import Path
import bpy
import bmesh
import json
import struct
import sys
from mathutils import Vector

root=Path(sys.argv[sys.argv.index('--')+1])

def load(folder):
    path=root/'ArtSource'/folder
    bpy.ops.wm.open_mainfile(filepath=str(path/'Models.blend'))
    for collection in bpy.data.collections:collection.hide_viewport=False
    bpy.context.view_layer.update()
    return path

def source(prefix):
    return next(o for o in bpy.data.objects if o.name.startswith(prefix)
                and any(c.name.endswith('_EditableParts') for c in o.users_collection))

def floor(obj):
    hit,p,*_=obj.ray_cast(Vector((0,0,1)),Vector((0,0,-1)),distance=2)
    assert hit and p.z<.020
    return p.z

def solid_volume(obj):
    bm=bmesh.new();bm.from_mesh(obj.data)
    assert all(len(e.link_faces)==2 for e in bm.edges),obj.name+' has open wall edges'
    volume=abs(bm.calc_volume());bm.free()
    assert volume>1e-9,obj.name+' has no wall volume'
    return volume

path=load('Expansion_05_CleaningTools')
bucket=source('Bucket with real inner floor')
report=dict(bucket_inner_floor_z=floor(bucket),bucket_wall_volume_m3=solid_volume(bucket))
(path/'SourceAudit.json').write_text(json.dumps(report,indent=2))
print('PASS bucket real wall and floor',flush=True)

path=load('Expansion_06_BabyCare')
shield=source('Green pacifier shield')
for x in (-.016,.016):
    hit,*_=shield.ray_cast(Vector((x,-1,.001)),Vector((0,1,0)),distance=2)
    assert not hit,'Pacifier ventilation hole is blocked'
hit,*_=shield.ray_cast(Vector((0,-1,.001)),Vector((0,1,0)),distance=2)
assert hit,'Pacifier shield itself is missing'
wipe=source('Wipe coming through lid')
report=dict(real_pacifier_holes=2,wipe_solid_volume_m3=solid_volume(wipe))
(path/'SourceAudit.json').write_text(json.dumps(report,indent=2))
print('PASS pacifier holes and physical wipe thickness',flush=True)

path=load('Expansion_07_GardenSupplies')
body=source('Hollow watering can body');pipe=source('Hollow long spout')
axis=Vector((.096,0,.100)).normalized()
hit,*_=pipe.ray_cast(-axis*.1,axis,distance=1)
assert not hit,'Spout bore is blocked'
rose=source('Perforated rose head');rotation=axis.to_track_quat('Z','Y')
for x,y in ((-.008,-.008),(.008,-.008),(-.008,.008),(.008,.008)):
    hit,*_=rose.ray_cast(rotation@Vector((x,y,-.025)),axis,distance=.1)
    assert not hit,'Rose spray hole is blocked'
hit,*_=rose.ray_cast(-axis*.025,axis,distance=.1)
assert hit,'Rose centre plate is missing'
report=dict(can_inner_floor_z=floor(body),can_wall_volume_m3=solid_volume(body),
            spout_open=True,spout_wall_volume_m3=solid_volume(pipe),real_rose_holes=4)
(path/'SourceAudit.json').write_text(json.dumps(report,indent=2))
print('PASS watering can inner floor, through-spout and 4 rose holes',flush=True)

path=load('Expansion_08_HomeComfort')
shade=source('Thin hollow cream lampshade')
hit,*_=shade.ray_cast(Vector((0,0,1)),Vector((0,0,-1)),distance=2)
assert not hit,'Lampshade axial opening is blocked'
blob=(path/'candle.glb').read_bytes();n=struct.unpack_from('<I',blob,12)[0];doc=json.loads(blob[20:20+n])
assert any(max(m.get('emissiveFactor',[0,0,0]))>0 for m in doc['materials'])
report=dict(lampshade_both_ends_open=True,shade_wall_volume_m3=solid_volume(shade),exported_flame_emission=True)
(path/'SourceAudit.json').write_text(json.dumps(report,indent=2))
print('PASS open thick lampshade and exported candle emission',flush=True)
