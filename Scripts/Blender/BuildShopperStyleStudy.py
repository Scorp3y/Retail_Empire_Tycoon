"""Three isolated shopper art studies based on the project's existing Kenney mini-character geometry.

Preserves the original authored faces, hair and UV shading. Adds a shared game palette,
an editable 14-bone rig and three animation actions. Does not replace the live shopper roster.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).parent))
from BuildShoppers import animations, studio

SWATCHES = (
    ('333B42','424D57','8594A3','697A8A','4E6070','465767','A4B3C2','B8C7D0',
     'F5EEDD','DEDACD','B87848','885B3E','6C4D39','9D7052','F0C6A0','D7A07A'),
    ('EDE0C7','D6BC92','87A47F','579887','D7B364','D9B36C','C88F66','CE936B',
     'B77D64','CA8B78','6B93AA','4F718B','CCDCE1','A0BECF','A898B9','847599'),
    ('BF95AD','B19BBD') + ('343B42',) * 14,
    ('343B42',) * 16,
)
STUDIES = (
    {'number':1, 'id':'Study_01', 'name':'Blue casual shirt', 'model':'character-male-a', 'top':11,
     'remove':(225,455,449,460,469,480), 'shirt':(108,263,252,281,287), 'arms':(108,263,252,281),
     'pants':(86,137,161,173,189,212), 'shoes':(48,62)},
    {'number':2, 'id':'Study_02', 'name':'Sage casual', 'model':'character-female-f', 'top':3,
     'remove':(150,158,168,186,198,216,332), 'shirt':(265,284,292,310,315),
     'arms':(237,257,265,284,292,310), 'pants':(), 'shoes':(78,101)},
    {'number':3, 'id':'Study_03', 'name':'Warm overshirt', 'model':'character-male-f', 'top':8,
     'remove':(146,154,162,254,310,322,371,383,387), 'shirt':(78,95,271,282,286,292,307),
     'arms':(78,95,186,204,282,286,292,307), 'pants':(14,26,46,50), 'shoes':(110,133)},
)

def palette(folder):
    """Analytic swatch gradients retain the pack's vertex-UV shading; no image filters or global lighting changes."""
    folder.mkdir(parents=True, exist_ok=True)
    texture = bpy.data.images.new('Study palette', width=256, height=256, alpha=True)
    pixels = []
    for y in range(256):
        row = y // 64
        shade = .84 + .16 * ((y % 64) / 63)
        for x in range(256):
            color = SWATCHES[row][x // 16]
            pixels.extend([int(color[i:i+2], 16) / 255 * shade for i in (0,2,4)] + [1])
    texture.pixels.foreach_set(pixels)
    texture.filepath_raw = str(folder / 'StudyPalette.png')
    texture.file_format = 'PNG'
    texture.save()
    texture.pack()
    material = bpy.data.materials.new('Study / shared matte palette')
    material.use_nodes = True
    shader = material.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value = .9
    image = material.node_tree.nodes.new('ShaderNodeTexImage')
    image.image = texture
    image.interpolation = 'Linear'
    material.node_tree.links.new(image.outputs['Color'], shader.inputs['Base Color'])
    return material

def read_obj(path):
    positions, coordinates, normals, faces = [], [], [], []
    group = ''
    for line in path.read_text(encoding='utf-8').splitlines():
        parts = line.split()
        if not parts:
            continue
        if parts[0] == 'g':
            group = parts[1]
        elif parts[0] == 'v':
            positions.append(Vector(tuple(map(float, parts[1:4]))))
        elif parts[0] == 'vt':
            coordinates.append(tuple(map(float, parts[1:3])))
        elif parts[0] == 'vn':
            normals.append(Vector(tuple(map(float, parts[1:4]))))
        elif parts[0] == 'f':
            faces.append((group, [tuple(int(value)-1 for value in corner.split('/')) for corner in parts[1:]]))
    return positions, coordinates, normals, faces

def converted(point, head=False):
    # OBJ is Y-up / Z-front. Swapping Y and Z also mirrors winding (handled on faces below).
    return Vector((point.x * (1.40 if head else 1.5), point.z * 1.5, point.y * 1.5))

def inspect(project):
    for design in STUDIES:
        path=project/'Assets/Models/mini characters/Models/OBJ format'/(design['model']+'.obj')
        positions,uv,normals,faces=read_obj(path)
        parent=list(range(len(positions)))
        def root(i):
            while parent[i]!=i:
                parent[i]=parent[parent[i]];i=parent[i]
            return i
        for group,corners in faces:
            if group!='head-mesh':continue
            first=root(corners[0][0])
            for corner in corners[1:]:parent[root(corner[0])]=first
        parts={}
        for group,corners in faces:
            if group!='head-mesh':continue
            key=root(corners[0][0]);parts.setdefault(key,[]).append(corners)
        print(design['model'],flush=True)
        for key,part in parts.items():
            indices={c[0] for face in part for c in face}
            bounds=[(round(min(positions[i][a] for i in indices),3),round(max(positions[i][a] for i in indices),3)) for a in range(3)]
            cells=sorted({(int(uv[c[1]][0]*16),int(uv[c[1]][1]*4)) for face in part for c in face})
            print(key,len(part),bounds,cells,flush=True)

def bone_for(point, group):
    if group != 'body-mesh':
        return 'Head'
    side = 'L' if point.x >= 0 else 'R'
    x = abs(point.x)
    if point.y < .09:
        return 'Foot.' + side
    if point.y < .175 and x < .16:
        return ('Shin.' if point.y < .125 else 'Thigh.') + side
    if x > .175:
        return ('UpperArm.' if x < .275 else 'Forearm.') + side
    return 'Hips' if point.y < .19 else 'Spine'

def component_keys(positions, faces):
    """Use authored disconnected mesh parts, not fragile face-number ranges, to identify accessories."""
    parent=list(range(len(positions)))
    def root(i):
        while parent[i]!=i:
            parent[i]=parent[parent[i]];i=parent[i]
        return i
    for group,corners in faces:
        first=root(corners[0][0])
        for corner in corners[1:]:parent[root(corner[0])]=first
    return [root(corners[0][0]) for group,corners in faces]

def relaxed_arm(point, direction=False):
    sign=1 if point.x>=0 else -1
    angle=-sign*math.radians(35)
    pivot=Vector() if direction else Vector((sign*.145,.30,0))
    relative=point-pivot
    return pivot+Vector((relative.x*math.cos(angle)-relative.y*math.sin(angle),
                         relative.x*math.sin(angle)+relative.y*math.cos(angle),relative.z))

def body_mesh(path, design, material):
    positions, uv, normals, source_faces = read_obj(path)
    template = design.get('template_number', design['number'])
    head_design = design.get('head_design')
    if head_design:
        source_faces = [(group, corners) for group, corners in source_faces if group == 'body-mesh']
        hp, hu, hn, hf = read_obj(path.with_name(head_design['model']+'.obj'))
        keys = component_keys(hp, hf)
        offsets = (len(positions), len(uv), len(normals))
        positions.extend(hp); uv.extend(hu); normals.extend(hn)
        for (group, corners), key in zip(hf, keys):
            role = next((role for role, parts in head_design['parts'].items() if key in parts), None)
            if group == 'head-mesh' and role:
                source_faces.append(('head-'+role, [tuple(value+offset for value, offset in zip(corner, offsets)) for corner in corners]))
    elif template==3:
        # The outdoor pack includes an angry face. Use its friendly expression for a regular shopper instead.
        fp,fu,fn,ff=read_obj(path.with_name('character-male-a.obj'))
        keys=component_keys(fp,ff)
        offsets=(len(positions),len(uv),len(normals))
        positions.extend(fp);uv.extend(fu);normals.extend(fn)
        for (group,corners),key in zip(ff,keys):
            if group=='head-mesh' and key in (305,311,319):
                source_faces.append(('face-study',[tuple(value+offset for value,offset in zip(corner,offsets)) for corner in corners]))
    vertices, faces, loop_uv, loop_normals, weights = [], [], [], [], []
    components=component_keys(positions,source_faces)
    # Separate vertices at authored normal/UV boundaries. Original facial expressions remain intact.
    for (group, corners),part in zip(source_faces,components):
        if part in design['remove']:
            continue
        indices = []
        centre = sum((positions[c[0]] for c in corners), Vector()) / len(corners)
        for vi, ti, ni in reversed(corners):
            point = positions[vi]
            indices.append(len(vertices))
            arm=part in design['arms']
            vertices.append(tuple(converted(relaxed_arm(point) if arm else point, group != 'body-mesh')))
            if arm:
                weights.append(('UpperArm.' if abs(point.x)<.275 else 'Forearm.')+('L' if point.x>=0 else 'R'))
            else:
                weights.append(bone_for(point, group))
            u, v = uv[ti]
            # Skin, hair and clothes are assigned by semantic part, not a global palette swap:
            # the source atlas sometimes uses the same cell for hair, skin and trouser fabric.
            skin_cell = design.get('skin_cell')
            if skin_cell and ((group == 'body-mesh' and int(u*16) in (11,13,15) and v < .25)
                              or group == 'head-skin'):
                u, v = ((skin_cell[0]+.5)/16, skin_cell[1]*.25+v%.25)
            if group == 'head-hair' and 'hair_cell' in design:
                cell = design['hair_cell']; u,v=((cell[0]+.5)/16,cell[1]*.25+v%.25)
            if group == 'head-face':
                u,v=(1.5/16,v%.25)
            if group == 'head-hat':
                u,v=((design['top']+.5)/16,.25+v%.25)
            if part in design['shirt'] and (.25 <= v < .75 or not arm or abs(point.x)<.245):
                u = (design['top'] + .5) / 16
                v = .25 + v % .25
            if part in design['pants']:
                u,v=((design.get('bottom',4)+.5)/16,v% .25)
            if template==3 and part in (78,95):
                u,v=((design['top']+.5)/16,.25+v% .25)
            if part in design['shoes']:
                u,v=((design.get('shoe',9 if template==2 else 5)+.5)/16,v% .25)
            if not head_design and template==2 and part in (518,522):
                u,v=(1.5/16,v% .25)
            loop_uv.append((u,v))
            n = normals[ni]
            if arm:
                # Normals must follow the same handed rotation as the limb they belong to.
                sign=1 if point.x>=0 else -1
                angle=-sign*math.radians(35)
                n=Vector((n.x*math.cos(angle)-n.y*math.sin(angle),n.x*math.sin(angle)+n.y*math.cos(angle),n.z))
            loop_normals.append(tuple(Vector((n.x,n.z,n.y)).normalized()))
        faces.append(indices)
    # The original belt also closed the torso/leg junction. Replace it with a plain fitted waist,
    # rather than leaving an open gap after removing the outdoor accessories.
    waist=bmesh.new()
    bmesh.ops.create_cube(waist,size=1)
    bmesh.ops.scale(waist,vec=Vector((.38,.285,.057)),verts=list(waist.verts))
    waist.normal_update()
    bmesh.ops.bevel(waist,geom=list(waist.edges),offset=.01,segments=1,profile=.5,affect='EDGES')
    bmesh.ops.recalc_face_normals(waist,faces=list(waist.faces))
    for face in waist.faces:
        indices=[]
        for vertex in face.verts:
            indices.append(len(vertices))
            vertices.append(tuple(vertex.co+Vector((0,-.04,.285))))
            weights.append('Hips');loop_uv.append(((design.get('bottom',4)+.5)/16,.125));loop_normals.append(tuple(face.normal))
        faces.append(indices)
    waist.free()
    mesh = bpy.data.meshes.new(design['id'] + ' / authored forms')
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    layer = mesh.uv_layers.new(name='PaletteUV')
    for loop, coord in zip(layer.data, loop_uv):
        loop.uv = coord
    for face in mesh.polygons:
        face.use_smooth = True
    mesh.normals_split_custom_set(loop_normals)
    body = bpy.data.objects.new('Body', mesh)
    bpy.context.collection.objects.link(body)
    for name in set(weights):
        group = body.vertex_groups.new(name=name)
        group.add([i for i,key in enumerate(weights) if key == name], 1, 'REPLACE')
    return body

def rig(body):
    skeleton = bpy.data.objects.new('Rig', bpy.data.armatures.new('Study skeleton'))
    bpy.context.collection.objects.link(skeleton)
    bpy.context.view_layer.objects.active = skeleton
    skeleton.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    specs = [('Root',(0,0,0),(0,0,.10),None), ('Hips',(0,0,.24),(0,0,.30),'Root'),
             ('Spine',(0,0,.30),(0,0,.48),'Hips'), ('Head',(0,0,.51),(0,0,.90),'Spine')]
    for sign, side in ((1,'L'),(-1,'R')):
        shoulder=converted(Vector((sign*.16,.29,0)))
        elbow=converted(relaxed_arm(Vector((sign*.27,.28,0))))
        hand=converted(relaxed_arm(Vector((sign*.36,.28,0))))
        specs.extend([
            ('UpperArm.'+side,shoulder,elbow,'Spine'),
            ('Forearm.'+side,elbow,hand,'UpperArm.'+side),
            ('Thigh.'+side,(sign*.125,0,.27),(sign*.125,0,.17),'Hips'),
            ('Shin.'+side,(sign*.125,0,.17),(sign*.125,0,.07),'Thigh.'+side),
            ('Foot.'+side,(sign*.125,0,.07),(sign*.125,.13,.04),'Shin.'+side),
        ])
    for name, head, tail, parent in specs:
        bone = skeleton.data.edit_bones.new(name)
        bone.head, bone.tail = head, tail
        if parent:
            bone.parent = skeleton.data.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    body.parent = skeleton
    modifier = body.modifiers.new('Study skin', 'ARMATURE')
    modifier.object = skeleton
    marker = bpy.data.objects.new('FacingMarker', None)
    bpy.context.collection.objects.link(marker)
    marker.parent = skeleton
    marker.location = (0,.35,.7)
    return skeleton, marker

def build(project, design, replace=False):
    output = project / 'ArtSource/Shoppers/StyleStudies/V3'
    destination = output / design['id'] / (design['id'] + '.blend')
    if destination.exists() and not replace:
        raise FileExistsError('Study already exists; preserve it: ' + str(destination))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    material = palette(output / 'Exports')
    source = project / 'Assets/Models/mini characters/Models/OBJ format' / (design['model'] + '.obj')
    body = body_mesh(source, design, material)
    skeleton, marker = rig(body)
    animations(skeleton)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in (body,skeleton,marker):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = skeleton
    fbx = output / 'Exports' / (design['id'] + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,
        bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
    studio(scene, project / 'Docs/ShopperStyleStudy' / (design['id'] + '.png'))
    scene.camera.data.ortho_scale = 1.58
    text = bpy.data.texts.new('README / source attribution')
    text.write('Art direction study, not installed in the live shopper roster.\n'
        'Base geometry: existing project asset mini characters, created by Kenney (www.kenney.nl).\n'
        'Preserved authored faces, hair, UV gradients and normals; recoloured outfits and added a generic rig.\n'
        'Original source: ' + str(source.relative_to(project)) + '\n')
    destination.parent.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(destination))
    Path(scene.render.filepath).parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.render.render(write_still=True)
    return design | {'triangles':sum(len(f.vertices)-2 for f in body.data.polygons),
        'bones':len(skeleton.data.bones), 'source':str(destination.relative_to(project)),
        'source_sha256':hashlib.sha256(destination.read_bytes()).hexdigest(),
        'fbx_sha256':hashlib.sha256(fbx.read_bytes()).hexdigest()}

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--project',type=Path,required=True)
    parser.add_argument('--inspect',action='store_true')
    parser.add_argument('--replace-studies',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    project=args.project.resolve()
    if args.inspect:
        inspect(project)
        return
    results=[build(project,design,args.replace_studies) for design in STUDIES]
    (project/'ArtSource/Shoppers/StyleStudies/V3/Manifest.json').write_text(json.dumps({'studies':results},indent=2),encoding='utf-8')

if __name__ == '__main__':
    main()
