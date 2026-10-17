"""Read-only conversion of the approved individual blends into compact traffic LODs.

Five meshes per LOD share one UV atlas. Body panels and four wheel pivots remain
separate; studio/reference objects are never exported. Editable sources are not saved.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

COMMON=('Glass / opaque blue graphite','Glass / rubber seal','Bumper / charcoal',
        'Tyre / rubber','Wheel / hubcap','Wheel / rim edge','Lamp / ivory','Lamp / amber',
        'Lamp / red','Grille / recess','Grille / bars','Mirror / reflector',
        'Cargo / pale panels','Cargo / grey panels','Cargo / cream panels')
WHEELS={'Wheel_Front_L':'FL','Wheel_Front_R':'FR','Wheel_Rear_L':'RL','Wheel_Rear_R':'RR'}
CELL=256
GUTTER=8


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def slot(name,identity):
    if name in ('Paint / warm yellow','Paint / body colour'):
        return int(identity[:2])-1
    if name=='Paint / panel gaps':
        return 20+int(identity[:2])-1
    if name not in COMMON:
        raise ValueError('Unmapped source material: '+name)
    return 40+COMMON.index(name)


class TrafficExport:
    def __init__(self,project,identity):
        self.project,self.identity=project,identity
        self.folder=project/'Library/IndividualTrafficImport/Exports'
        self.folder.mkdir(parents=True,exist_ok=True)
        self.source=project/'ArtSource/TrafficVehicles'/identity/(identity+'.blend')
        if Path(bpy.data.filepath).resolve()!=self.source.resolve():
            raise RuntimeError('Open the individual source blend first')
        self.before=digest(self.source)
        self.root=bpy.data.objects[identity]
        self.deps=bpy.context.evaluated_depsgraph_get()
        self.definitions={}
        self.objects=[]
        self.atlas=bpy.data.materials.new('IndividualFleetAtlas')
        self.atlas.diffuse_color=(1,1,1,1)

    def material_slot(self,material):
        tile=slot(material.name,self.identity)
        shader=material.node_tree.nodes.get('Principled BSDF')
        self.definitions[tile]={'tile':tile,'name':material.name,
            'color':list(shader.inputs['Base Color'].default_value),
            'roughness':shader.inputs['Roughness'].default_value,
            'metallic':shader.inputs['Metallic'].default_value}
        return tile

    def combine(self,name,sources,pivot):
        vertices,faces,uvs,normals=[],[],[],[]
        for obj in sources:
            evaluated=obj.evaluated_get(self.deps)
            mesh=evaluated.to_mesh()
            matrix=self.root.matrix_world.inverted()@evaluated.matrix_world
            normal_matrix=matrix.to_3x3().inverted().transposed()
            base=len(vertices)
            vertices.extend(tuple(matrix@v.co-pivot) for v in mesh.vertices)
            uv=mesh.uv_layers.active
            corner_normals=mesh.corner_normals
            for face in mesh.polygons:
                material=obj.data.materials[face.material_index]
                tile=self.material_slot(material)
                polygon=tuple(base+i for i in face.vertices)
                faces.append(polygon)
                face_uvs,face_normals=[],[]
                source_uv=[tuple(uv.data[i].uv) if uv else (0.5,0.5) for i in face.loop_indices]
                offset=[math.floor(min(point[axis] for point in source_uv)+1e-5) for axis in (0,1)]
                for loop_index,point in zip(face.loop_indices,source_uv):
                    local=[max(0,min(1,point[axis]-offset[axis])) for axis in (0,1)]
                    face_uvs.append(((tile%8*CELL+GUTTER+local[0]*(CELL-2*GUTTER))/(CELL*8),
                                     (tile//8*CELL+GUTTER+local[1]*(CELL-2*GUTTER))/(CELL*8)))
                    face_normals.append(tuple((normal_matrix@corner_normals[loop_index].vector).normalized()))
                uvs.extend(face_uvs);normals.extend(face_normals)
                if tile in (40,41):
                    # Blender's planar glazing is two-sided; reproduce that without
                    # disabling backface culling on the entire opaque vehicle atlas.
                    faces.append(tuple(reversed(polygon)))
                    uvs.extend(reversed(face_uvs))
                    normals.extend(tuple(-component for component in normal) for normal in reversed(face_normals))
            evaluated.to_mesh_clear()
        if not faces:
            raise RuntimeError('Empty export part: '+name)
        mesh=bpy.data.meshes.new(name)
        mesh.from_pydata(vertices,[],faces)
        mesh.update()
        texture_uv=mesh.uv_layers.new(name='FleetAtlasUV')
        for loop,point in zip(texture_uv.data,uvs):
            loop.uv=point
        for face in mesh.polygons:
            face.use_smooth=True
        mesh.normals_split_custom_set(normals)
        mesh.materials.append(self.atlas)
        obj=bpy.data.objects.new('LOD0_'+name,mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.location=pivot
        self.objects.append(obj)
        return obj

    def glazing_subset(self,source,keep_glass):
        """Keep thin window overlays intact; collapse decimation can delete them."""
        mesh=source.data
        uv=mesh.uv_layers.active
        selected=[]
        for face in mesh.polygons:
            point=uv.data[face.loop_start].uv
            tile=math.floor(point.y*8)*8+math.floor(point.x*8)
            if (tile in (40,41))==keep_glass:selected.append(face)
        used=sorted({index for face in selected for index in face.vertices})
        remap={index:i for i,index in enumerate(used)}
        result=bpy.data.meshes.new('Protected glass' if keep_glass else 'Collapsible body')
        result.from_pydata([mesh.vertices[i].co for i in used],[],
            [tuple(remap[i] for i in face.vertices) for face in selected])
        result.update()
        target_uv=result.uv_layers.new(name='FleetAtlasUV')
        source_loops=[i for face in selected for i in face.loop_indices]
        for destination,index in zip(target_uv.data,source_loops):destination.uv=uv.data[index].uv
        for face in result.polygons:face.use_smooth=True
        result.normals_split_custom_set([mesh.corner_normals[i].vector for i in source_loops])
        result.materials.append(self.atlas)
        return result

    def export(self):
        bpy.context.view_layer.update()
        wheel_roots={name:bpy.data.objects[name] for name in WHEELS}
        wheel_parts={child for wheel in wheel_roots.values() for child in wheel.children_recursive}
        renderable=[obj for obj in self.root.children_recursive if obj.type in ('MESH','CURVE','FONT')]
        parts=[self.combine('Body',[obj for obj in renderable if obj not in wheel_parts],Vector())]
        wheel_data=[]
        for name,label in WHEELS.items():
            root=wheel_roots[name]
            pivot=self.root.matrix_world.inverted()@root.matrix_world.translation
            parts.append(self.combine('Wheel'+label,list(root.children_recursive),pivot))
            wheel_data.append({'name':label,'position':[pivot.x,pivot.z,pivot.y],
                'front':root['steering_axle']})
        lod_triangles=[]
        for level,ratio in enumerate((1,.38,.12)):
            count=0
            for source in parts:
                obj=source
                if level:
                    body=source.name.endswith('_Body')
                    data=self.glazing_subset(source,False) if body else source.data.copy()
                    obj=bpy.data.objects.new('LOD'+str(level)+'_'+source.name.split('_',1)[1],data)
                    bpy.context.scene.collection.objects.link(obj)
                    obj.location=source.location
                    modifier=obj.modifiers.new('Distance simplification','DECIMATE')
                    modifier.ratio=ratio
                    modifier.use_collapse_triangulate=True
                    if body:
                        bpy.context.view_layer.objects.active=obj
                        bpy.ops.object.modifier_apply(modifier=modifier.name)
                        glass=bpy.data.objects.new('Protected glazing',self.glazing_subset(source,True))
                        bpy.context.scene.collection.objects.link(glass)
                        glass.location=source.location
                        bpy.ops.object.select_all(action='DESELECT')
                        obj.select_set(True);glass.select_set(True)
                        bpy.context.view_layer.objects.active=obj
                        bpy.ops.object.join()
                    self.objects.append(obj)
                bpy.context.view_layer.update()
                evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
                mesh=evaluated.to_mesh();mesh.calc_loop_triangles()
                count+=len(mesh.loop_triangles)
                evaluated.to_mesh_clear()
            lod_triangles.append(count)
        points=[Vector(v.co)+obj.location for obj in parts for v in obj.data.vertices]
        low=[min(point[i] for point in points) for i in range(3)]
        high=[max(point[i] for point in points) for i in range(3)]
        if self.identity=='01_Hatchback':
            body_width=1.72
            radius=.315
        else:
            design=json.loads(self.root['design_manifest'])
            body_width=design['width']*1.03
            radius=design['radius']
        bpy.ops.object.select_all(action='DESELECT')
        for obj in self.objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active=parts[0]
        destination=self.folder/(self.identity+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(destination),use_selection=True,object_types={'MESH'},
            axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,
            add_leaf_bones=False,use_mesh_modifiers=True,use_custom_props=False,path_mode='STRIP')
        # Source-local +Y maps to Unity-local +Z (verified again after FBX import).
        report={'id':self.identity,'source':str(self.source.relative_to(self.project)).replace('\\','/'),
            'source_sha256':self.before,'fbx_sha256':digest(destination),'materials':list(self.definitions.values()),
            'width':body_width,'length':high[1]-low[1],'height':high[2],
            'centerZ':(high[1]+low[1])/2,'wheelbase':wheel_data[0]['position'][2]-wheel_data[2]['position'][2],
            'radius':radius,'wheels':wheel_data,'lodTriangles':lod_triangles}
        (self.folder/(self.identity+'.json')).write_text(json.dumps(report,indent=2),encoding='utf-8')
        if digest(self.source)!=self.before:
            raise RuntimeError('Editable source unexpectedly changed')
        print('EXPORTED '+self.identity+' LOD triangles '+str(lod_triangles),flush=True)


def srgb(values):
    return np.where(values<=.0031308,values*12.92,1.055*np.maximum(0,values)**(1/2.4)-.055)


def write_image(path,pixels):
    image=bpy.data.images.new(path.stem,width=pixels.shape[1],height=pixels.shape[0],alpha=True)
    image.colorspace_settings.name='Non-Color'
    image.pixels.foreach_set(pixels.astype(np.float32).ravel())
    image.filepath_raw=str(path);image.file_format='PNG';image.save()


def make_atlas(project):
    folder=project/'Library/IndividualTrafficImport/Exports'
    reports=[json.loads(path.read_text(encoding='utf-8')) for path in sorted(folder.glob('*.json'))]
    reports=[report for report in reports if 'id' in report]
    if len(reports)!=20:
        raise RuntimeError('Export all twenty models before building the atlas')
    definitions={}
    for report in reports:
        for definition in report['materials']:
            definitions.setdefault(definition['tile'],definition)
    bpy.ops.wm.open_mainfile(filepath=str(project/'ArtSource/TrafficVehicles/01_Hatchback/01_Hatchback.blend'))
    images={}
    for suffix in ('Paint roughness','Rubber micrograin','Lamp lens fluting','Tyre tread relief'):
        image=bpy.data.images['01 '+suffix]
        pixels=np.array(image.pixels[:],dtype=np.float32).reshape(512,512,4)[:,:,0]
        indices=(np.arange(CELL-2*GUTTER)*512/(CELL-2*GUTTER)).astype(int)
        images[suffix]=pixels[np.ix_(indices,indices)]
    size=CELL*8
    base=np.ones((size,size,4),dtype=np.float32)
    mask=np.zeros_like(base);mask[:,:,3]=.35
    normal=np.ones_like(base);normal[:,:,0:2]=.5
    for tile,definition in definitions.items():
        name=definition['name'];width=CELL-2*GUTTER
        color=np.array(definition['color'][:3])
        rgba=np.ones((width,width,4),dtype=np.float32);rgba[:,:,:3]=srgb(color)
        rough=np.full((width,width),definition['roughness'])
        height=np.zeros_like(rough)
        if name in ('Paint / warm yellow','Paint / body colour'):
            rough=images['Paint roughness'];height=rough*.000012
        elif name in ('Tyre / rubber','Glass / rubber seal','Bumper / charcoal'):
            height=images['Rubber micrograin']*.00007
            if name=='Tyre / rubber':height=height+images['Tyre tread relief']*.000408
        elif name.startswith('Lamp /'):
            height=images['Lamp lens fluting']*.00028
            if name=='Lamp / ivory':rgba[:,:,:3]*=.97+.03*images['Lamp lens fluting'][:,:,None]
        gy,gx=np.gradient(height,width**-1)
        vectors=np.dstack((-gx,-gy,np.ones_like(gx)))
        vectors/=np.linalg.norm(vectors,axis=2)[:,:,None]
        normals=np.ones_like(rgba);normals[:,:,:3]=vectors*.5+.5
        masks=np.zeros_like(rgba);masks[:,:,0]=definition['metallic'];masks[:,:,3]=1-rough
        y,x=tile//8*CELL,tile%8*CELL
        for destination,source in ((base,rgba),(mask,masks),(normal,normals)):
            destination[y:y+CELL,x:x+CELL,:]=np.pad(source,((GUTTER,GUTTER),(GUTTER,GUTTER),(0,0)),mode='edge')
    for name,pixels in (('FleetBase',base),('FleetMetalSmooth',mask),('FleetNormal',normal)):
        write_image(folder/(name+'.png'),pixels)
    (folder/'fleet.json').write_text(json.dumps({'vehicles':reports},indent=2),encoding='utf-8')
    print('ATLAS COMPLETE: 20 sources, '+str(len(definitions))+' occupied tiles',flush=True)


parser=argparse.ArgumentParser()
parser.add_argument('--project',type=Path,required=True)
parser.add_argument('--id')
parser.add_argument('--atlas',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
if args.atlas:
    make_atlas(args.project.resolve())
else:
    TrafficExport(args.project.resolve(),args.id).export()
