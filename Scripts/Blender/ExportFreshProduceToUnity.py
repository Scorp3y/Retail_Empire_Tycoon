"""Export the already modelled produce, retaining its three simple materials.

The standalone GLBs remain untouched. Unity uses FBX, matching this project's
native model pipeline. Every source .blend contains one centred product mesh.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path
import bpy
from mathutils import Matrix,Vector

DESIGNS=(
 ('kiwi','Kiwi','Kiwi_Half',4,7,.07,.00015,600),
 ('peaches','Peaches','Peaches_Pair',4,7,.30,.00035,600),
 ('plums','Plums','Plums_Pair',3,6,.18,.00030,600),
 ('mango','Mango','Mango_Whole_and_Half',6,10,.45,.00090,600),
 ('cherries','Cherries','Cherries_Box',6,10,.25,.00040,1000),
 ('eggplant','Eggplant','Eggplant',3,5,.30,.00120,600),
)

def srgb(value):
    return value*12.92 if value<=.0031308 else 1.055*value**(1/2.4)-.055

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--project',required=True)
    root=Path(parser.parse_args(sys.argv[sys.argv.index('--')+1:]).project)
    preview=root/'ArtSource/FreshProduceGLB/Preview.blend'
    source=root/'ArtSource/FreshProduce';art=root/'Library/CityUpdateQA/IsolatedProject/Assets/Art/FreshProduce'
    (art/'Models').mkdir(parents=True,exist_ok=True);source.mkdir(parents=True,exist_ok=True)
    products=[];materials={}
    for key,name,object_name,cost,sale,weight,volume,limit in DESIGNS:
        path=source/key/(key+'.blend')
        if path.exists():raise FileExistsError('Preserve existing artist source: '+str(path))
        bpy.ops.wm.open_mainfile(filepath=str(preview))
        obj=bpy.data.objects[object_name]
        for other in list(bpy.data.objects):
            if other!=obj:bpy.data.objects.remove(other,do_unlink=True)
        obj.name=key;obj.matrix_world=Matrix.Identity(4)
        for collection in list(obj.users_collection):collection.objects.unlink(obj)
        collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection);collection.objects.link(obj)
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        obj.data.calc_loop_triangles();triangles=len(obj.data.loop_triangles)
        if triangles>=limit or obj.modifiers:raise ValueError('Unexpected triangle budget or unapplied modifiers: '+key)
        if len(obj.data.materials)!=3:raise ValueError('Expected three simple materials: '+key)
        for material in obj.data.materials:
            shader=material.node_tree.nodes.get('Principled BSDF')
            rgba=list(shader.inputs['Base Color'].default_value)
            materials[material.name]=dict(name=material.name,color=[srgb(v) for v in rgba[:3]]+[rgba[3]],
                roughness=shader.inputs['Roughness'].default_value,alpha=shader.inputs['Alpha'].default_value)
        bounds=[obj.matrix_world@Vector(point) for point in obj.bound_box]
        size=[max(v[i] for v in bounds)-min(v[i] for v in bounds) for i in range(3)]
        if abs(min(v.z for v in bounds))>1e-7:raise ValueError('Product not seated at zero: '+key)
        for screen in bpy.data.screens:
            for area in screen.areas:
                if area.type=='VIEW_3D':
                    area.spaces.active.region_3d.view_location=(0,0,size[2]/2)
                    area.spaces.active.region_3d.view_distance=max(size)*2.4
        path.parent.mkdir(parents=True,exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(path),check_existing=False)
        target=art/'Models'/(key+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'MESH'},apply_unit_scale=True,
            axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
        products.append(dict(key=key,name=name,storage='Produce',cost=cost,sale=sale,weight=weight,volume=volume,
            triangles=triangles,limit=limit,size=size,source=str(path),sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
        print('PRODUCE '+key+': '+str(triangles)+' triangles, three materials',flush=True)
    document=dict(products=products,materials=list(materials.values()))
    text=json.dumps(document,indent=2)+'\n'
    (source/'Definitions.json').write_text(text,encoding='utf-8');(art/'Definitions.json').write_text(text,encoding='utf-8')

if __name__=='__main__':main()
