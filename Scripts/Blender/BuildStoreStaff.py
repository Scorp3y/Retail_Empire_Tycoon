"""Five individually editable employee sources, shared rigid-weighted NPC rig and palette.

Run in a separate background Blender. Never reads or changes an artist's open scene.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).parent))
import BuildShoppers as npc
from ShopperDesigns import ShopperDesign

DESIGNS = (
    ('Cashier', ShopperDesign(1,'Cashier','skin_light','copper','bob','polo','ivory','charcoal','dark_brown')),
    ('Guard', ShopperDesign(2,'Security','skin_light','brown','crop','shirt','navy','charcoal','charcoal',hat='cap:navy')),
    ('Stocker', ShopperDesign(3,'Stocker','skin_tan','brown','quiff','vest','mustard','teal','tan')),
    ('Cleaner', ShopperDesign(4,'Cleaner','skin_peach','ash','bun','polo','sage','sage','dark_brown')),
    ('Driver', ShopperDesign(5,'Driver','skin_light','brown','crop','jacket','blue','charcoal','tan',hat='cap:rust',beard='short')),
)

def uniform(mesh, role):
    if role in ('Cashier','Cleaner'):
        color='forest' if role=='Cashier' else 'apron_blue'
        mesh.box('Apron bib',(0,.113,.425),(.255,.025,.185),color,'Spine',.012)
        mesh.box('Apron skirt',(0,.115,.277),(.345,.035,.14),color,'Hips',.018)
        for x in (-.105,.105):mesh.box('Apron strap',(x,.114,.505),(.035,.02,.075),color,'Spine',.005)
        mesh.box('Apron pocket',(0,.133,.355),(.14,.014,.06),color,'Spine',.007)
    if role=='Guard':
        mesh.box('Belt',(0,.006,.294),(.31,.213,.04),'black','Hips',.007)
        mesh.box('Buckle',(0,.118,.295),(.05,.015,.033),'metal','Hips',.004)
        mesh.box('Radio',(.155,.119,.32),(.055,.045,.075),'black','Spine',.007)
        mesh.box('Antenna',(.158,.119,.373),(.009,.009,.055),'black','Spine',.002)
        mesh.polygon([(-.11,.124,.473),(-.06,.124,.473),(-.06,.124,.435),(-.085,.124,.418),(-.11,.124,.435)],'cream','Spine')
        mesh.box('Cap badge',(0,.242,1.01),(.064,.018,.073),'cream','Head',.012)
    mesh.box('Name badge',(-.09,.136,.472),(.073,.014,.028),'mustard' if role=='Cashier' else 'cream','Spine',.004)

def tools(mesh,role):
    if role=='Cashier':
        mesh.box('Scanner grip',(-.28,.045,.31),(.045,.05,.11),'charcoal','Forearm.R',.009)
        mesh.box('Scanner',(-.28,.09,.385),(.11,.10,.075),'charcoal','Forearm.R',.014,segments=2)
        mesh.box('Scanner glass',(-.28,.145,.385),(.073,.008,.038),'mint','Forearm.R',.005)
    elif role=='Cleaner':
        mesh.beam((-.31,.10,.06),(-.31,.10,.88),.022,.025,'tan','Forearm.R')
        mesh.box('Mop base',(-.31,.12,.035),(.26,.15,.055),'teal','Forearm.R',.012)
        for x in range(6):mesh.box('Mop fringe',(-.415+x*.042,.14,.014),(.039,.18,.025),'cream','Forearm.R',.006)
        for x in (-.28,.28):mesh.box('Glove',(x,.012,.285),(.115,.12,.115),'yellow','Forearm.R' if x<0 else 'Forearm.L',.027,segments=2)
    elif role=='Stocker':
        mesh.box('Delivery carton',(0,.255,.37),(.40,.24,.27),'tan','Spine',.012)
        mesh.box('Carton tape',(0,.255,.508),(.055,.24,.007),'cream','Spine',.001)
        mesh.box('Carton label',(.10,.378,.38),(.12,.008,.07),'ivory','Spine',.003)
    elif role=='Driver':
        mesh.box('Clipboard',(.33,.06,.35),(.17,.04,.235),'dark_brown','Forearm.L',.008)
        mesh.box('Delivery paper',(.33,.084,.35),(.14,.006,.19),'ivory','Forearm.L',.003)
        for z in (.30,.34,.38):mesh.box('Paper line',(.33,.089,z),(.09,.003,.007),'gray','Forearm.L',0)
        mesh.box('Clipboard clip',(.33,.087,.469),(.07,.02,.025),'charcoal','Forearm.L',.004)
        mesh.ball((-.285,.09,.26),(.035,.014,.035),'black','Forearm.R',2)
        mesh.box('Key',(-.285,.095,.203),(.018,.018,.077),'metal','Forearm.R',.003)
        mesh.box('Key teeth',(-.27,.095,.178),(.03,.018,.015),'metal','Forearm.R',.002)
    else:
        mesh.box('Handheld radio',(-.28,.06,.32),(.07,.06,.13),'charcoal','Forearm.R',.01)
        mesh.box('Radio aerial',(-.30,.06,.405),(.009,.009,.06),'black','Forearm.R',.002)

def work_animation(rig,role):
    # New actions work with both legacy and layered Blender Action storage.
    rig.animation_data.action=None
    bpy.data.actions.remove(bpy.data.actions['Browse'])
    rig.animation_data.action=bpy.data.actions.new('Browse')
    rig.animation_data.action.use_fake_user=True
    for frame in range(0,91,3):
        p=frame/90*math.tau;bpy.context.scene.frame_set(frame+1)
        for bone in rig.pose.bones:bone.rotation_euler=(0,0,0);bone.location=(0,0,0)
        bones=rig.pose.bones
        bones['Spine'].rotation_euler.x=.02*math.sin(p)
        bones['Head'].rotation_euler.y=.06*math.sin(p)
        if role=='Cleaner':
            bones['Forearm.R'].rotation_euler.x=.10*math.sin(p)
            bones['Spine'].rotation_euler.z=.045*math.sin(p)
        elif role=='Stocker':
            for side in ('L','R'):
                bones['UpperArm.'+side].rotation_euler.x=.5
                bones['Forearm.'+side].rotation_euler.x=.55+.06*math.sin(p)
        else:
            bones['UpperArm.R'].rotation_euler.x=.48+.08*math.sin(p)
            bones['Forearm.R'].rotation_euler.x=.5+.10*math.cos(p)
        for bone in bones:
            bone.keyframe_insert(data_path='rotation_euler',frame=frame+1,group=bone.name)
            bone.keyframe_insert(data_path='location',frame=frame+1,group=bone.name)
    rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)

def build(project):
    exports=project/'Library/StaffBuild/Exports';previews=project/'Library/StaffBuild/Previews'
    exports.mkdir(parents=True,exist_ok=True);previews.mkdir(parents=True,exist_ok=True)
    npc.COLORS.update(forest='365C49',apron_blue='9AB5BC')
    npc.PALETTE[:]=list(npc.COLORS)
    report=[]
    for role,design in DESIGNS:
        folder=project/'ArtSource/Staff'/role;folder.mkdir(parents=True,exist_ok=True)
        source=folder/(role+'.blend')
        if source.exists():raise FileExistsError('Preserve existing source: '+str(source))
        bpy.ops.wm.read_factory_settings(use_empty=True)
        material=npc.material_and_atlas(exports)
        bodymesh=npc.CharacterMesh(design);bodymesh.outfit();bodymesh.face();bodymesh.hair();uniform(bodymesh,role)
        body=bodymesh.finish(material);rig,marker=npc.make_rig(body)
        toolmesh=npc.CharacterMesh(design);tools(toolmesh,role);prop=toolmesh.finish(material);prop.name='WorkTools'
        prop.parent=rig;modifier=prop.modifiers.new('Common NPC skeleton','ARMATURE');modifier.object=rig
        npc.animations(rig);work_animation(rig,role)
        rig['role']=role;rig['concept']='../StaffConcept-v1.png'
        bpy.ops.object.select_all(action='DESELECT')
        for obj in (rig,body,prop,marker):obj.select_set(True)
        bpy.context.view_layer.objects.active=rig
        bpy.ops.export_scene.fbx(filepath=str(exports/(role+'.fbx')),use_selection=True,
            object_types={'ARMATURE','MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,bake_anim=True,
            bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
            path_mode='COPY',embed_textures=False)
        npc.studio(bpy.context.scene,previews/(role+'.png'))
        bpy.context.scene.render.engine='CYCLES';bpy.context.scene.cycles.samples=16
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(source))
        # The review pose shows each accessory and its profession.
        rig.animation_data.action=bpy.data.actions['Browse'];bpy.context.scene.frame_set(1)
        bpy.ops.render.render(write_still=True)
        triangles=sum(len(p.vertices)-2 for obj in (body,prop) for p in obj.data.polygons)
        report.append(dict(role=role,source=str(source.relative_to(project)),triangles=triangles,bones=len(rig.data.bones)))
    (exports/'manifest.json').write_text(json.dumps(report,indent=2),encoding='utf-8')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--project',required=True)
    build(Path(parser.parse_args(sys.argv[sys.argv.index('--')+1:]).project))
