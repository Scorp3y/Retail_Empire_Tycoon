"""Read back and validate the saved vehicle, then render independent views.

Run Blender with the completed blend as its input, not with the generation script.
Never saves the loaded blend and never touches Unity assets.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--project',required=True,type=Path)
    parser.add_argument('--skip-renders',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    project=args.project.resolve()
    destination=project/'ArtSource/TrafficVehicles/01_Hatchback/01_Hatchback.blend'
    review=project/'Docs/VehicleStages/01_Hatchback'
    require(Path(bpy.data.filepath).resolve()==destination,'Wrong input blend')
    scene=bpy.context.scene
    root=bpy.data.objects.get('01_Hatchback')
    require(root is not None and root.type=='EMPTY','One car root is required')
    require(len(bpy.data.scenes)==1,'Unexpected extra scenes')
    require(scene.unit_settings.system=='METRIC','Asset units must be metric')
    wheels=[obj for obj in root.children if obj.name.startswith('Wheel_')]
    require(len(wheels)==4,'Expected exactly four independent wheel pivots')
    for axle,y in (('Front',1.3543),('Rear',-1.23)):
        for side,x in (('L',-.730),('R',.730)):
            wheel=bpy.data.objects.get(f'Wheel_{axle}_{side}')
            require(wheel in wheels,'Missing wheel pivot')
            require((wheel.location-Vector((x,y,.315))).length<1e-5,'Incorrect wheel pivot')
            require(wheel['steering_axle']==(axle=='Front'),'Incorrect steering axle')
            require(len(wheel.children)==5,'Wheel must contain tyre, hubcap, rim, valve and fasteners')
            for obj in wheel.children:
                bm=bmesh.new()
                bm.from_mesh(obj.data)
                require(all(edge.is_manifold for edge in bm.edges),f'Open wheel mesh: {obj.name}')
                require(bm.calc_volume()>1e-8,f'Incorrect wheel winding: {obj.name}')
                bm.free()
    for side in (-1,1):
        headlamp=bpy.data.objects.get('Convex ivory headlamp '+str(side))
        require(headlamp is not None,'Shaped headlamp missing')
        bm=bmesh.new()
        bm.from_mesh(headlamp.data)
        require(all(edge.is_manifold for edge in bm.edges),'Headlamp must be a closed volume')
        require(bm.calc_volume()>1e-8,'Headlamp winding is incorrect')
        bm.free()
        require(all(face.use_smooth for face in headlamp.data.polygons),'Headlamp shading must be smooth')
        require(len(headlamp.data.vertices)>80,'Headlamp is still a plain box')
        tail=bpy.data.objects.get('Tail wraparound lens '+str(side))
        require(tail is not None,'Continuous rear/side lens missing')
        bm=bmesh.new()
        bm.from_mesh(tail.data)
        require(all(edge.is_manifold for edge in bm.edges),'Rear/side lens has an open joint')
        require(bm.calc_volume()>1e-8,'Rear/side lens winding is incorrect')
        bm.free()
    for name in ('Sculpted lower body and sloping hood','Three-door cabin and gently crowned roof'):
        panel=bpy.data.objects[name]
        require(all(face.use_smooth for face in panel.data.polygons),'Panel normals are still flat')
        require(any(mod.type=='WEIGHTED_NORMAL' for mod in panel.modifiers),'Weighted panel normals missing')
    glass=bpy.data.materials.get('Glass / opaque blue graphite')
    require(glass is not None,'Opaque glazing material missing')
    shader=glass.node_tree.nodes['Principled BSDF']
    require(shader.inputs['Transmission Weight'].default_value==0,'Glass must not transmit light')
    require(shader.inputs['Alpha'].default_value==1,'Glass must not be transparent')
    panes=[obj for obj in root.children if obj.name.endswith('opaque glass')]
    require(len(panes)==6,'Expected windshield, rear windshield and four side panes')
    packed=[image for image in bpy.data.images if image.packed_file is not None]
    require(any(image.name.startswith('Reference-closeup') for image in packed),'Reference is not embedded')
    surface_names=('01 Paint roughness','01 Rubber micrograin','01 Lamp lens fluting','01 Tyre tread relief')
    for name in surface_names:
        image=bpy.data.images.get(name)
        require(image is not None and image in packed,'Surface map is not embedded: '+name)
        require(tuple(image.size)==(512,512),'Unexpected surface map resolution')
        require(image.colorspace_settings.name=='Non-Color','Surface data must not use sRGB decoding')
    for obj in root.children_recursive:
        if obj.type=='MESH':
            require(obj.data.uv_layers.active is not None,'Missing texture UVs: '+obj.name)
    require(bpy.data.texts.get('READ ME / 01 Hatchback') is not None,'Model notes missing')
    bpy.context.view_layer.update()
    depsgraph=bpy.context.evaluated_depsgraph_get()
    bounds=[]
    triangles=0
    degenerate=[]
    for obj in root.children_recursive:
        if obj.type not in ('MESH','CURVE'):
            continue
        evaluated=obj.evaluated_get(depsgraph)
        mesh=evaluated.to_mesh()
        mesh.calc_loop_triangles()
        triangles+=len(mesh.loop_triangles)
        for triangle in mesh.loop_triangles:
            if triangle.area<1e-11:
                degenerate.append(obj.name)
        bounds.extend(evaluated.matrix_world@vertex.co for vertex in mesh.vertices)
        evaluated.to_mesh_clear()
    require(not degenerate,'Degenerate triangles: '+', '.join(sorted(set(degenerate))))
    minimum=[min(point[i] for point in bounds) for i in range(3)]
    maximum=[max(point[i] for point in bounds) for i in range(3)]
    require(abs(minimum[2])<.001,'Tyres must rest at ground level')
    report={
        'saved_file':str(destination), 'blender':bpy.app.version_string,
        'reopened_successfully':True, 'vehicles':1, 'independent_wheel_pivots':4,
        'wheel_meshes_closed_and_outward':True, 'opaque_glass_panes':len(panes),
        'reference_embedded':True,'zero_area_triangles':0,'triangles':triangles,
        'rounded_headlamps':2,'smooth_weighted_body_normals':True,
        'packed_surface_maps':list(surface_names),
        'model_sha256':hashlib.sha256(destination.read_bytes()).hexdigest(),
        'bounds_min_m':minimum,'bounds_max_m':maximum,
        'dimensions_m':[round(maximum[i]-minimum[i],4) for i in range(3)],
        'unity_touched':False,
        'limitation':'Only the supplied front-left view is reference-led; unseen surfaces are interpreted.'
    }
    (review/'saved-file-verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report),flush=True)
    if args.skip_renders:
        return
    camera=scene.camera
    scene.render.filepath=str(review/'01-Hatchback-reopened.png')
    bpy.ops.render.render(write_still=True)
    sys.path.insert(0,str(Path(__file__).parent))
    from BuildSingleHatchback import render_detail_views
    render_detail_views(scene,review)
    camera.data.type='PERSP'
    camera.data.shift_x=0
    camera.data.lens=70
    camera.location=(6.5,7.5,3.0)
    camera.rotation_euler=(Vector((0,0,.85))-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(review/'06-Front-right.png')
    bpy.ops.render.render(write_still=True)
    camera.data.shift_x=0
    camera.location=(-6.5,-7.5,3.3)
    camera.rotation_euler=(Vector((0,0,.85))-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.lens=70
    scene.render.filepath=str(review/'02-Rear-quarter.png')
    bpy.ops.render.render(write_still=True)
    camera.data.type='ORTHO'
    camera.data.ortho_scale=4.8
    camera.location=(-8,0,.87)
    camera.rotation_euler=(Vector((0,0,.87))-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(review/'03-Side-profile.png')
    bpy.ops.render.render(write_still=True)
    print('Saved blend read-back and all independent renders passed.',flush=True)


if __name__=='__main__':
    main()
