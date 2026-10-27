"""Approved V3 shoppers: authored Kenney forms, cohesive outfits, shared animation rig.

Uses the project's existing mini-character assets; does not claim original authorship.
Run in a separate background Blender. Never edits an open artist scene.
"""
import argparse
import hashlib
import json
import sys
import copy
from pathlib import Path

import bpy

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).parent))
import BuildShopperStyleStudy as study

# Explicit authored component maps. IDs are connected OBJ parts, never face-number ranges.
HEADS = {
    'crop': ('male-a', {'hair':(366,), 'skin':(390,396,439), 'face':(305,311,319)}),
    'quiff': ('male-e', {'hair':(406,422), 'skin':(323,329,372), 'face':(283,289,297)}),
    'swept': ('male-d', {'hair':(451,), 'skin':(344,350,393), 'face':(402,408,416)}),
    'moustache': ('male-b', {'hair':(398,), 'skin':(330,353,359), 'face':(273,279,287)}),
    'cap': ('male-c', {'hair':(374,390), 'hat':(514,), 'skin':(459,465,508), 'face':(401,407,415)}),
    'fringe bun': ('female-a', {'hair':(387,448,471), 'skin':(488,483,551), 'face':(396,402,410)}),
    'double bun': ('female-b', {'hair':(321,338,352), 'skin':(434,440,483), 'face':(378,384,392)}),
    'high bun': ('female-d', {'hair':(414,373), 'skin':(423,418,486), 'face':(312,318,326)}),
    'ponytail': ('female-e', {'hair':(297,346,366), 'skin':(415,421,464), 'face':(375,381,389)}),
    'pigtails': ('female-f', {'hair':(439,446,467,487), 'skin':(355,361,404), 'face':(497,503,511,518,522)}),
}

SKIN = {'light':(2,2),'peach':(3,2),'warm':(4,2),'tan':(5,2),'brown':(6,2),'deep':(7,2)}
HAIR = {'charcoal':(1,0),'chestnut':(12,0),'brown':(13,0),'ginger':(10,0),
        'blond':(8,2),'silver':(9,2),'auburn':(10,2),'dark':(11,2)}
# Five coordinated wardrobes rather than random RGB colours. Each row is ten distinct people.
# head, body template, top swatch, trousers, skin, hair, shoes
WARDROBES = (
    ('Blue / sage / terracotta', (
        ('crop',1,11,4,'light','charcoal',5), ('pigtails',2,3,4,'peach','brown',9),
        ('quiff',3,8,4,'light','dark',5), ('high bun',2,4,4,'light','blond',9),
        ('swept',1,10,5,'peach','chestnut',5), ('ponytail',2,7,4,'brown','dark',9),
        ('moustache',3,2,3,'light','silver',5), ('double bun',2,14,4,'deep','charcoal',9),
        ('cap',1,3,5,'warm','chestnut',5), ('fringe bun',2,0,4,'peach','auburn',9))),
    ('Warm everyday', (
        ('quiff',1,4,5,'light','ginger',9), ('ponytail',2,10,4,'peach','chestnut',9),
        ('crop',3,2,3,'light','charcoal',5), ('double bun',2,6,4,'peach','brown',9),
        ('cap',3,11,5,'peach','dark',5), ('pigtails',2,14,4,'warm','auburn',9),
        ('swept',1,0,4,'light','chestnut',5), ('high bun',2,8,4,'deep','dark',9),
        ('moustache',1,10,5,'tan','silver',5), ('fringe bun',2,3,4,'light','brown',9))),
    ('Garden and city', (
        ('moustache',3,8,4,'light','silver',5), ('fringe bun',2,4,4,'brown','dark',9),
        ('swept',1,3,5,'peach','auburn',9), ('pigtails',2,10,4,'deep','charcoal',9),
        ('quiff',3,14,3,'light','brown',5), ('double bun',2,0,4,'light','ginger',9),
        ('crop',1,6,5,'warm','chestnut',5), ('ponytail',2,2,4,'peach','blond',9),
        ('cap',1,4,4,'light','dark',5), ('high bun',2,15,4,'peach','silver',9))),
    ('Soft weekend', (
        ('cap',3,8,5,'light','brown',5), ('high bun',2,3,4,'peach','chestnut',9),
        ('crop',1,14,4,'peach','ginger',9), ('ponytail',2,4,4,'deep','charcoal',9),
        ('moustache',3,11,3,'light','silver',5), ('fringe bun',2,10,4,'light','blond',9),
        ('quiff',1,2,5,'brown','dark',5), ('pigtails',2,7,4,'light','auburn',9),
        ('swept',3,6,4,'tan','silver',5), ('double bun',2,3,4,'peach','chestnut',9))),
    ('Neighbourhood', (
        ('swept',1,4,4,'light','brown',5), ('double bun',2,10,4,'peach','auburn',9),
        ('moustache',3,0,5,'deep','silver',5), ('fringe bun',2,8,4,'peach','charcoal',9),
        ('crop',3,3,3,'light','dark',5), ('high bun',2,14,4,'light','blond',9),
        ('cap',1,2,4,'tan','ginger',5), ('ponytail',2,0,4,'light','silver',9),
        ('quiff',3,10,5,'peach','brown',5), ('pigtails',2,4,4,'brown','chestnut',9))),
)


def designs():
    result = []
    for wardrobe, people in WARDROBES:
        for head, template, top, bottom, skin, hair, shoe in people:
            design = copy.deepcopy(study.STUDIES[template-1])
            number = len(result)+1
            model, parts = HEADS[head]
            design.update(number=number, id=f'Shopper_{number:02}', template_number=template,
                          name=f'{head.title()} / {wardrobe}', top=top, bottom=bottom, shoe=shoe,
                          skin=skin, hair=hair, hairstyle=head, skin_cell=SKIN[skin], hair_cell=HAIR[hair],
                          head_design={'model':'character-'+model, 'parts':parts})
            result.append(design)
    if len(result) != 50 or len({(d['hairstyle'], d['template_number'], d['top'], d['skin'], d['hair']) for d in result}) != 50:
        raise ValueError('The collection must contain exactly fifty distinct designs.')
    if sum(d['skin'] in ('light','peach') for d in result) != 35:
        raise ValueError('The approved collection requires 35 light-skinned appearances out of 50.')
    return result


def configure_palette():
    rows = list(study.SWATCHES)
    rows[2] = ('BF95AD','B19BBD','F2D1B3','EBC2AA','C38A63','AE7955','906044','754E39',
               'D5B175','B5B2AA','B57C4B','624938','C39794','8DA99A','829BB5','C4B2A2')
    study.SWATCHES = tuple(rows)


def verify_head_parts(project, design):
    head = design['head_design']
    path = project/'Assets/Models/mini characters/Models/OBJ format'/(head['model']+'.obj')
    positions, uv, normals, faces = study.read_obj(path)
    actual = {key for (group, corners), key in zip(faces, study.component_keys(positions, faces)) if group == 'head-mesh'}
    requested = {key for values in head['parts'].values() for key in values}
    if not requested.issubset(actual):
        raise ValueError(f'Authored head parts changed: {path} / {requested-actual}')


def build(project, design, replace, render):
    destination = project/'ArtSource/Shoppers'/design['id']/(design['id']+'.blend')
    if destination.exists() and not replace:
        raise FileExistsError('Use --replace-verified after preserving current sources: '+str(destination))
    verify_head_parts(project, design)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    exports = project/'Library/ShopperV3/Exports'
    material = study.palette(exports)
    source = project/'Assets/Models/mini characters/Models/OBJ format'/(design['model']+'.obj')
    body = study.body_mesh(source, design, material)
    skeleton, marker = study.rig(body)
    study.animations(skeleton)
    skeleton['design'] = json.dumps(design)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in (body, skeleton, marker):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = skeleton
    fbx = exports/(design['id']+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={'ARMATURE','MESH','EMPTY'},
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0)
    study.studio(scene, project/'Docs/Shoppers'/('V3_'+design['id']+'.png'))
    scene.camera.data.ortho_scale = 1.65
    readme = bpy.data.texts.new('README / source attribution and editing')
    readme.write('Base geometry: mini characters by Kenney (www.kenney.nl), already included in this project.\n'
                 'V3 adaptation: friendly authored faces, ten hair silhouettes, coordinated palette, removed outdoor gear,\n'
                 'relaxed arms, closed waist, editable fourteen-bone generic rig and Idle/Walk/Browse actions.\n'
                 'The studio camera and lights are not exported to Unity. All palette pixels are packed into this file.\n'
                 'Design: '+json.dumps(design, indent=2))
    destination.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(destination))
    if render:
        Path(scene.render.filepath).parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.render.render(write_still=True)
    result = design | {'source':str(destination.relative_to(project)),
        'source_sha256':hashlib.sha256(destination.read_bytes()).hexdigest(),
        'fbx_sha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),
        'triangles':sum(len(f.vertices)-2 for f in body.data.polygons), 'bones':len(skeleton.data.bones)}
    print('BUILT '+design['id']+' / '+design['name'], flush=True)
    return result


def inspect_heads(project):
    folder = project / 'Assets/Models/mini characters/Models/OBJ format'
    for path in sorted(folder.glob('character-*.obj')):
        positions, uv, normals, faces = study.read_obj(path)
        keys = study.component_keys(positions, faces)
        parts = {}
        for (group, corners), key in zip(faces, keys):
            if group == 'head-mesh':
                parts.setdefault(key, []).extend(corners)
        print('\n' + path.stem, flush=True)
        for key, corners in parts.items():
            bounds = [(round(min(positions[c[0]][axis] for c in corners), 3),
                       round(max(positions[c[0]][axis] for c in corners), 3)) for axis in range(3)]
            cells = sorted({(int(uv[c[1]][0]*16), int(uv[c[1]][1]*4)) for c in corners})
            print(key, bounds, cells, flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--inspect-heads', action='store_true')
    parser.add_argument('--replace-verified', action='store_true')
    parser.add_argument('--render', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    if args.inspect_heads:
        inspect_heads(args.project.resolve())
        return
    project = args.project.resolve()
    configure_palette()
    previous_path = project/'ArtSource/Shoppers/Manifest.json'
    previous = json.loads(previous_path.read_text(encoding='utf-8'))
    if args.replace_verified:
        # An artist's manual changes must never be silently overwritten by regeneration.
        for entry in previous['characters']:
            source = project/entry['source']
            if hashlib.sha256(source.read_bytes()).hexdigest() != entry['source_sha256']:
                raise ValueError('Artist-edited Blender file: '+str(source))
        if not (project/'Library/ShopperV3/Before_20261020/ArtSource/Shoppers/Manifest.json').exists():
            raise ValueError('Preserve the previous collection before replacing it.')
    # Publish one complete collection manifest. Partial replacement would invalidate the
    # source hashes of the other entries and could mix rigs/palettes from different revisions.
    results = [build(project, design, args.replace_verified, args.render) for design in designs()]
    palette = project/'Library/ShopperV3/Exports/StudyPalette.png'
    manifest = {'visual_revision':3, 'palette_columns':16, 'palette_rows':4,
                'light_skin_count':35, 'skin_tone_distribution':{tone:sum(d['skin']==tone for d in results) for tone in SKIN},
                'palette_sha256':hashlib.sha256(palette.read_bytes()).hexdigest(),
                'base_geometry':'Kenney mini characters, existing project assets', 'characters':results}
    previous_path.write_text(json.dumps(manifest, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
