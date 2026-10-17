"""Author vehicle 01 only, as a standalone, reference-led Blender asset.

Run with Blender 5.2 --background --python <this file> -- --project <root>.
This script never reads or writes Unity assets. +Y is forward, Z is up; metres.
The supplied three-quarter concept defines the visible surfaces. Unseen rear
details are a restrained continuation of that design, not claimed as measured.
"""

import argparse
import hashlib
import json
import math
import shutil
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Vector


def linear(value):
    return value / 12.92 if value <= .04045 else ((value + .055) / 1.055) ** 2.4


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def rounded_rectangle(width, height, radius, segments=4):
    """Counter-clockwise X/Z outline with true quarter-circle corners."""
    radius=min(radius,width*.45,height*.45)
    points=[]
    for cx,cz,start in ((width/2-radius,height/2-radius,0),
                        (-width/2+radius,height/2-radius,90),
                        (-width/2+radius,-height/2+radius,180),
                        (width/2-radius,-height/2+radius,270)):
        for index in range(segments+1):
            angle=math.radians(start+90*index/segments)
            points.append((cx+radius*math.cos(angle),cz+radius*math.sin(angle)))
    return points


def rounded_polygon(points, radius=.018, segments=4):
    """Round a planar contour without changing its measured straight edges."""
    points=[Vector(point) for point in points]
    result=[]
    for index,point in enumerate(points):
        before=points[index-1]-point
        after=points[(index+1)%len(points)]-point
        distance=min(radius,before.length*.22,after.length*.22)
        start=point+before.normalized()*distance
        end=point+after.normalized()*distance
        for step in range(segments+1):
            t=step/segments
            result.append(start*(1-t)**2+point*(2*t*(1-t))+end*t*t)
    return result


class VehicleSurfaceTextures:
    """Small tileable material maps, generated deterministically and packed.

    These are surface maps, not a painted screenshot wrapped around the car.
    Ordinary UVs and packed images keep the editable blend self-contained.
    """
    def __init__(self):
        self.images={}
        rng=np.random.default_rng(1031)
        size=512
        v,u=np.mgrid[0:size,0:size].astype(np.float32)/size
        grain=rng.uniform(-1,1,(size,size)).astype(np.float32)
        self.image('01 Paint roughness',.40+.024*grain)
        self.image('01 Rubber micrograin',.50+.12*grain)
        ribs=.5+.20*np.cos(u*math.tau*24)+.025*np.cos(v*math.tau*9)
        edge=np.clip(np.minimum.reduce((u,1-u,v,1-v))*15,0,1)
        self.image('01 Lamp lens fluting',.5+(ribs-.5)*edge)
        diagonal=np.mod(u*42+np.abs(v-.5)*5,1)
        tread=np.where(diagonal<.10,.29,.55)
        mask=np.clip((.25-np.abs(v-.5))*36,0,1)
        self.image('01 Tyre tread relief',.5+(tread-.5)*mask)

    def image(self,name,values):
        values=np.asarray(values,dtype=np.float32)
        pixels=np.ones((*values.shape,4),dtype=np.float32)
        pixels[:,:,:3]=np.clip(values[:,:,None],0,1)
        image=bpy.data.images.new(name,width=values.shape[1],height=values.shape[0],alpha=False)
        image.colorspace_settings.name='Non-Color'
        image.pixels.foreach_set(pixels.ravel())
        image.update()
        image.pack()
        self.images[name]=image

    def texture(self,material,name):
        node=material.node_tree.nodes.new('ShaderNodeTexImage')
        node.name=name
        node.label=name
        node.image=self.images[name]
        node.extension='REPEAT'
        return node

    def bump(self,material,image_name,strength,distance,previous=None):
        nodes=material.node_tree.nodes
        links=material.node_tree.links
        image=self.texture(material,image_name)
        bump=nodes.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value=strength
        bump.inputs['Distance'].default_value=distance
        links.new(image.outputs['Color'],bump.inputs['Height'])
        if previous:
            links.new(previous.outputs['Normal'],bump.inputs['Normal'])
        links.new(bump.outputs['Normal'],nodes['Principled BSDF'].inputs['Normal'])
        return bump

    def apply(self,materials):
        paint=materials['Paint / warm yellow']
        shader=paint.node_tree.nodes['Principled BSDF']
        roughness=self.texture(paint,'01 Paint roughness')
        paint.node_tree.links.new(roughness.outputs['Color'],shader.inputs['Roughness'])
        shader.inputs['Coat Weight'].default_value=.19
        shader.inputs['Coat Roughness'].default_value=.30
        self.bump(paint,'01 Paint roughness',.10,.00012)
        for name in ('Tyre / rubber','Bumper / charcoal','Glass / rubber seal'):
            self.bump(materials[name],'01 Rubber micrograin',.20,.00035)
        self.bump(materials['Tyre / rubber'],'01 Tyre tread relief',.34,.0012,
                  materials['Tyre / rubber'].node_tree.nodes.get('Bump'))
        for name in ('Lamp / ivory','Lamp / amber','Lamp / red'):
            mat=materials[name]
            shader=mat.node_tree.nodes['Principled BSDF']
            shader.inputs['Coat Weight'].default_value=.32
            shader.inputs['Coat Roughness'].default_value=.16
            self.bump(mat,'01 Lamp lens fluting',.28,.0010)
        ivory=materials['Lamp / ivory']
        lens=self.texture(ivory,'01 Lamp lens fluting')
        tint=ivory.node_tree.nodes.new('ShaderNodeValToRGB')
        tint.label='Very subtle ribbed reflector tint'
        tint.color_ramp.elements[0].position=.20
        tint.color_ramp.elements[0].color=(*[linear(c) for c in (.89,.90,.84)],1)
        tint.color_ramp.elements[1].position=.80
        tint.color_ramp.elements[1].color=(*[linear(c) for c in (.96,.96,.89)],1)
        ivory.node_tree.links.new(lens.outputs['Color'],tint.inputs['Fac'])
        ivory.node_tree.links.new(tint.outputs['Color'],ivory.node_tree.nodes['Principled BSDF'].inputs['Base Color'])


class Hatchback:
    """The three-door hatchback's panels, running gear and opaque glazing."""

    FRONT = 1.82
    REAR = -1.80
    AXLES = {'Front': 1.3543, 'Rear': -1.23}
    RADIUS = .315
    BODY_STATIONS = ((-1.80,1,1.0263),(-1.70,1,1.0463),(-1.25,1,1.0463),
                     (-.72,1,1.0463),(1.0474,1,1.0463),(1.10,1,1.032),
                     (1.70,.985,.869),(1.82,.945,.835))

    def __init__(self, scene):
        self.scene = scene
        self.parts = {}
        self.materials = {}
        self.groups = {}
        for name in ('01_Body', '02_Glazing', '03_Wheels', '04_Details'):
            collection = bpy.data.collections.new(name)
            scene.collection.children.link(collection)
            self.groups[name] = collection
        self.root = bpy.data.objects.new('01_Hatchback', None)
        scene.collection.objects.link(self.root)
        self.root['forward_axis'] = '+Y'
        self.root['units'] = 'metres'
        self.root['reference'] = 'TrafficFleetConcept-v2.png / 01 HATCHBACK'
        self.root['status'] = 'Individual artist review; not installed in Unity'
        self.root['revision'] = 'Rounded body, shaped lenses, profiled wheels, packed surface maps'
        self.material('Paint / warm yellow', (.985, .725, .14), .42)
        self.material('Paint / panel gaps', (.63, .43, .07), .66)
        glass=self.material('Glass / opaque blue graphite', (.155, .215, .30), .45)
        glass.node_tree.nodes['Principled BSDF'].inputs['Specular IOR Level'].default_value=.12
        self.material('Glass / rubber seal', (.075, .085, .09), .65)
        self.material('Bumper / charcoal', (.165, .165, .16), .73)
        self.material('Tyre / rubber', (.135, .145, .15), .85)
        self.material('Wheel / hubcap', (.57, .58, .58), .43, .23)
        self.material('Wheel / rim edge', (.69, .70, .70), .36, .30)
        self.material('Lamp / ivory', (.93, .93, .85), .25)
        self.material('Lamp / amber', (.96, .37, .055), .34)
        self.material('Lamp / red', (.70, .07, .04), .35)
        self.material('Grille / recess', (.105, .12, .125), .8)
        self.material('Grille / bars', (.20, .22, .23), .72)
        self.material('Mirror / reflector', (.24,.29,.31), .15, .72)
        self.surfaces=VehicleSurfaceTextures()
        self.surfaces.apply(self.materials)

    def material(self, name, rgb, roughness, metallic=0):
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*[linear(c) for c in rgb], 1)
        mat.use_nodes = True
        shader = mat.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value = mat.diffuse_color
        shader.inputs['Roughness'].default_value = roughness
        shader.inputs['Metallic'].default_value = metallic
        shader.inputs['Transmission Weight'].default_value = 0
        shader.inputs['Specular IOR Level'].default_value = .26
        self.materials[name] = mat
        return mat

    def attach(self, obj, name, material, group, parent=None):
        obj.name = name
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        self.groups[group].objects.link(obj)
        obj.parent = parent or self.root
        obj.data.materials.append(self.materials[material])
        self.parts[name] = obj
        return obj

    def mesh(self, name, vertices, faces, material, group='04_Details', parent=None):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(vertices, [], faces)
        mesh.update()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        self.project_uv(mesh)
        obj = bpy.data.objects.new(name, mesh)
        self.scene.collection.objects.link(obj)
        return self.attach(obj, name, material, group, parent)

    @staticmethod
    def project_uv(mesh,plane=None):
        uv=mesh.uv_layers.get('SurfaceUV') or mesh.uv_layers.new(name='SurfaceUV')
        points=[vertex.co for vertex in mesh.vertices]
        minimum=[min(point[i] for point in points) for i in range(3)]
        maximum=[max(point[i] for point in points) for i in range(3)]
        for face in mesh.polygons:
            axis=max(range(3),key=lambda i:abs(face.normal[i]))
            axes=plane or ((1,2) if axis==0 else (0,2) if axis==1 else (0,1))
            for loop_index in face.loop_indices:
                point=mesh.vertices[mesh.loops[loop_index].vertex_index].co
                uv.data[loop_index].uv=tuple((point[i]-minimum[i])/max(.001,maximum[i]-minimum[i]) for i in axes)
        mesh.uv_layers.active=uv

    @staticmethod
    def smooth_hard_surface(obj):
        for face in obj.data.polygons:
            face.use_smooth=True
        normal=obj.modifiers.new('Area-weighted panel normals','WEIGHTED_NORMAL')
        normal.keep_sharp=True
        normal.weight=50
        normal.mode='FACE_AREA_WITH_ANGLE'

    def box(self, name, center, size, material, bevel=0, group='04_Details', parent=None):
        bpy.ops.mesh.primitive_cube_add(size=1, location=center)
        obj = bpy.context.object
        obj.dimensions = size
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        self.attach(obj, name, material, group, parent)
        if bevel:
            mod = obj.modifiers.new('Small manufactured edge radius', 'BEVEL')
            mod.width = min(bevel,min(size)*.42)
            mod.segments = 3
            mod.affect = 'EDGES'
            mod.harden_normals = True
            self.smooth_hard_surface(obj)
        return obj

    def line(self, name, points, material, radius=.0025, group='04_Details'):
        curve = bpy.data.curves.new(name, 'CURVE')
        curve.dimensions = '3D'
        curve.resolution_u = 1
        curve.bevel_depth = radius
        curve.bevel_resolution = 1
        path = curve.splines.new('POLY')
        path.points.add(len(points)-1)
        for destination, point in zip(path.points, points):
            destination.co = (*point, 1)
        obj = bpy.data.objects.new(name, curve)
        self.scene.collection.objects.link(obj)
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        self.groups[group].objects.link(obj)
        obj.parent = self.root
        curve.materials.append(self.materials[material])
        self.parts[name] = obj
        return obj

    @classmethod
    def body_section(cls,y):
        if y<=cls.BODY_STATIONS[0][0]:
            return cls.BODY_STATIONS[0][1:]
        for (ya,wa,za),(yb,wb,zb) in zip(cls.BODY_STATIONS,cls.BODY_STATIONS[1:]):
            if y<=yb:
                t=(y-ya)/(yb-ya)
                return wa+(wb-wa)*t,za+(zb-za)*t
        return cls.BODY_STATIONS[-1][1:]

    @classmethod
    def body_x(cls,z,y=0):
        width,top=cls.body_section(y)
        profile = ((.255, .748*width), (.33, .805*width), (.57, .832*width),
                   (top-.065, .826*width), (top, .819*width))
        if z <= profile[0][0]:
            return profile[0][1]
        for (za, xa), (zb, xb) in zip(profile, profile[1:]):
            if z <= zb:
                return xa + (xb-xa)*(z-za)/(zb-za)
        return profile[-1][1]

    @staticmethod
    def cabin_x(z):
        return .819 - .3403857 * (z-1.0313178)

    @staticmethod
    def windshield_y(z):
        return 1.0416 + (.4741-1.0416)*(z-1.0455)/(1.6025-1.0455)

    def make_body(self):
        # These longitudinal stations belong to this one car, not a fleet profile.
        # The short front overhang and rising hood follow the reference silhouette.
        stations = self.BODY_STATIONS
        vertices = []
        for y, width, top in stations:
            section = [(-.748*width, y, .255), (-.805*width, y, .33),
                       (-.832*width, y, .57), (-.826*width, y, top-.065),
                       (-.819*width, y, top), (0, y, top+.009),
                       (.819*width, y, top), (.826*width, y, top-.065),
                       (.832*width, y, .57), (.805*width, y, .33),
                       (.748*width, y, .255)]
            vertices.extend(section)
        ring = 11
        faces = [tuple(reversed(range(ring))),
                 tuple(range((len(stations)-1)*ring, len(stations)*ring))]
        for i in range(len(stations)-1):
            for j in range(ring):
                faces.append((i*ring+j, i*ring+(j+1)%ring,
                              (i+1)*ring+(j+1)%ring, (i+1)*ring+j))
        body = self.mesh('Sculpted lower body and sloping hood', vertices, faces,
                         'Paint / warm yellow', '01_Body')
        # Form the generous outer corner radii before the wheel cuts. Tiny
        # boolean edges would otherwise clamp the entire bevel to almost zero.
        bpy.context.view_layer.objects.active=body
        outer=body.modifiers.new('Formed body corner radii','BEVEL')
        outer.width=.040
        outer.segments=4
        outer.limit_method='ANGLE'
        outer.angle_limit=math.radians(18)
        bpy.ops.object.modifier_apply(modifier=outer.name)
        for side in (-1, 1):
            for axle, y in self.AXLES.items():
                bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=.352, depth=.65,
                    location=(side*.91, y, self.RADIUS), rotation=(0, math.pi/2, 0))
                cutter = bpy.context.object
                bpy.context.view_layer.objects.active = body
                mod = body.modifiers.new(f'{axle} actual open wheel well {side}', 'BOOLEAN')
                mod.operation = 'DIFFERENCE'
                mod.solver = 'EXACT'
                mod.object = cutter
                bpy.ops.object.modifier_apply(modifier=mod.name)
                bpy.data.objects.remove(cutter, do_unlink=True)
        bevel = body.modifiers.new('Soft sheet-metal corners', 'BEVEL')
        bevel.width = .006
        bevel.segments = 2
        bevel.limit_method = 'ANGLE'
        bevel.angle_limit = math.radians(18)
        bevel.harden_normals=True
        self.smooth_hard_surface(body)

        # Side panels and roof chamfer share vertices, avoiding a stacked box cabin.
        vertices = [(-.819, -1.80, 1.0313), (.819, -1.80, 1.0313),
                    (.819, 1.0474, 1.0313), (-.819, 1.0474, 1.0313),
                    (-.5879, -1.1886, 1.7104), (.5879, -1.1886, 1.7104),
                    (.6246, .4741, 1.6025), (-.6246, .4741, 1.6025),
                    (-.5479, -1.1536, 1.7454), (.5479, -1.1536, 1.7454),
                    (.5846, .4391, 1.6375), (-.5846, .4391, 1.6375),
                    (0, -1.1536, 1.7654), (0, .4391, 1.6575)]
        faces = [(3,2,1,0)]
        for j in range(4):
            faces.append((j,(j+1)%4,(j+1)%4+4,j+4))
            faces.append((j+4,(j+1)%4+4,(j+1)%4+8,j+8))
        # The crown also closes against the front/rear roof chamfers.
        # A straight chamfer edge here would leave a thin triangular opening.
        faces[2]=(4,5,9,12,8)
        faces[6]=(6,7,11,13,10)
        faces.extend(((8,12,13,11), (12,9,10,13)))
        cabin=self.mesh('Three-door cabin and gently crowned roof', vertices, faces,
                        'Paint / warm yellow', '01_Body')
        bevel=cabin.modifiers.new('Small roof and pillar edge radii','BEVEL')
        bevel.width=.052
        bevel.segments=4
        bevel.limit_method='ANGLE'
        bevel.angle_limit=math.radians(20)
        bevel.harden_normals=True
        self.smooth_hard_surface(cabin)

    def pane(self, name, points, outward):
        points = rounded_polygon(points,.022,4)
        center = sum(points, Vector()) / len(points)
        normal = Vector(outward).normalized()
        if (points[1]-points[0]).cross(points[2]-points[0]).dot(normal)<0:
            points.reverse()
        seal = [p + normal*.004 for p in points]
        glass = [center + (p-center)*.958 + normal*.006 for p in points]
        self.mesh(name+' rubber gasket', seal, [tuple(range(len(seal)))],
                  'Glass / rubber seal', '02_Glazing')
        self.mesh(name+' opaque glass', glass, [tuple(range(len(glass)))],
                  'Glass / opaque blue graphite', '02_Glazing')

    def make_glazing(self):
        for side in (-1, 1):
            for name, shape in (
                ('Large front door window', [(.9119,1.0662),(-.2380,1.0674),(-.3209,1.6064),(.4289,1.5404)]),
                ('Rear quarter window', [(-.4080,1.0838),(-1.3626,1.1338),(-.9536,1.633),(-.4643,1.5955)])):
                points = [(side*self.cabin_x(z), y, z) for y,z in shape]
                self.pane(f'{name} {side}', points, (side,0,.205))
        z0,z1 = 1.083,1.552
        x0,x1 = self.cabin_x(z0)-.048, self.cabin_x(z1)-.047
        self.pane('Windshield', [(-x0,self.windshield_y(z0),z0),
                  (x0,self.windshield_y(z0),z0), (x1,self.windshield_y(z1),z1),
                  (-x1,self.windshield_y(z1),z1)], (0,.658,.753))
        def hatch_y(z):
            return -1.80+.6114*(z-1.0313)/.6791
        rz0,rz1=1.13,1.65
        self.pane('Rear hatch window', [(self.cabin_x(rz0)-.055,hatch_y(rz0),rz0),
                  (-self.cabin_x(rz0)+.055,hatch_y(rz0),rz0),
                  (-self.cabin_x(rz1)+.055,hatch_y(rz1),rz1),
                  (self.cabin_x(rz1)-.055,hatch_y(rz1),rz1)], (0,-.71,.704))
        for x in (-.37, .29):
            z = 1.096
            self.line('Resting windshield wiper', [(x-.19,self.windshield_y(z)+.015,z),
                      (x+.19,self.windshield_y(z+.006)+.015,z+.006)],
                      'Bumper / charcoal', .006)
        self.line('Hood rear shut line', [(-.812,1.054,1.047),(0,1.054,1.056),(.812,1.054,1.047)],
                  'Paint / panel gaps', .0024)

    def revolution(self, name, rings, count, material, root, smooth=False, closed=False):
        # Local axle is X. Separate wheel roots retain correct rotation pivots.
        vertices,rows = [],[]
        for x,radius in rings:
            row=[]
            if radius==0:
                row.append(len(vertices))
                vertices.append((x,0,0))
                rows.append(row)
                continue
            for i in range(count):
                angle=2*math.pi*i/count
                row.append(len(vertices))
                vertices.append((x, math.sin(angle)*radius, math.cos(angle)*radius))
            rows.append(row)
        faces=[]
        pairs=list(zip(rows,rows[1:]))
        if closed:
            pairs.append((rows[-1],rows[0]))
        for first,second in pairs:
            for i in range(count):
                j=(i+1)%count
                if len(first)==1:
                    faces.append((first[0],second[j],second[i]))
                elif len(second)==1:
                    faces.append((first[i],first[j],second[0]))
                else:
                    faces.append((first[i],first[j],second[j],second[i]))
        obj=self.mesh(name,vertices,faces,material,'03_Wheels',root)
        uv=obj.data.uv_layers.active
        for face in obj.data.polygons:
            values=[]
            for loop_index in face.loop_indices:
                point=obj.data.vertices[obj.data.loops[loop_index].vertex_index].co
                values.append((loop_index,(math.atan2(point.y,point.z)/math.tau)%1,
                               (point.x+.12)/.24))
            crossing=max(value[1] for value in values)-min(value[1] for value in values)>.5
            for loop_index,u,v in values:
                uv.data[loop_index].uv=(u+1 if crossing and u<.5 else u,v)
        for face in obj.data.polygons:
            face.use_smooth=smooth
        return obj

    def make_wheels(self):
        for axle,y in self.AXLES.items():
            for side in (-1,1):
                label=f'{axle}_{"L" if side<0 else "R"}'
                root=bpy.data.objects.new('Wheel_'+label,None)
                self.groups['03_Wheels'].objects.link(root)
                root.parent=self.root
                root.location=(side*.730,y,self.RADIUS)
                root['radius_m']=self.RADIUS
                root['steering_axle']=axle=='Front'
                profile=[(-.106,.176),(-.116,.196),(-.120,.223),(-.118,.253),
                         (-.109,.277),(-.094,.298),(-.075,.311),(-.057,.315),
                         (-.040,.315),(-.037,.312),(-.033,.312),(-.030,.315),
                         (-.004,.315),(-.002,.312),(.002,.312),(.004,.315),
                         (.030,.315),(.033,.312),(.037,.312),(.040,.315),
                         (.057,.315),(.075,.311),(.094,.298),(.109,.277),
                         (.118,.253),(.120,.223),(.116,.196),(.106,.176)]
                self.revolution('Profiled tyre '+label,profile,40,'Tyre / rubber',root,True,True)
                cap=[(.147*side,0),(.147*side,.030),(.145*side,.038),
                     (.137*side,.044),(.128*side,.046),(.128*side,.054),
                     (.136*side,.061),(.133*side,.105),(.124*side,.139),
                     (.116*side,.151),(.116*side,.161),(.124*side,.166),
                     (.112*side,.173),(.105*side,.173),(.100*side,0)]
                self.revolution('Plain dished hubcap '+label,cap,40,'Wheel / hubcap',root,True)
                lip=[(.112*side,.170),(.117*side,.177),(.112*side,.183),
                     (.106*side,.185),(.099*side,.181),(.099*side,.173)]
                self.revolution('Narrow steel rim lip '+label,lip,40,'Wheel / rim edge',root,True,True)
                self.box('Tyre valve '+label,(side*.127,.123,.123),(.021,.009,.009),
                         'Tyre / rubber',.002,'03_Wheels',root)
                fastener_vertices,fastener_faces=[],[]
                for bolt in range(4):
                    angle=math.tau*bolt/4+math.pi/4
                    cy,cz=.073*math.sin(angle),.073*math.cos(angle)
                    start=len(fastener_vertices)
                    for xx,rad in ((.137,.0065),(.142,.0065),(.143,.0050)):
                        for i in range(6):
                            phase=math.tau*i/6
                            fastener_vertices.append((side*xx,cy+rad*math.sin(phase),cz+rad*math.cos(phase)))
                    fastener_faces.extend((tuple(reversed(range(start,start+6))),tuple(range(start+12,start+18))))
                    for row in range(2):
                        for i in range(6):
                            j=(i+1)%6
                            fastener_faces.append((start+row*6+i,start+row*6+j,
                                                   start+(row+1)*6+j,start+(row+1)*6+i))
                self.mesh('Wheel fasteners '+label,fastener_vertices,fastener_faces,
                          'Wheel / rim edge','03_Wheels',root)
                # Inner liner is behind the tyre, rather than a black painted arch.
                liner=[]
                lip_points=[]
                for radius,projection in ((.350,.002),(.356,.012),(.368,.010),(.378,.001)):
                    for i in range(25):
                        angle=math.pi*i/24
                        z=self.RADIUS+math.sin(angle)*radius
                        yy=y+math.cos(angle)*radius
                        lip_points.append((side*(self.body_x(z,yy)+projection),yy,z))
                lip_faces=[(row*25+i,row*25+i+1,(row+1)*25+i+1,(row+1)*25+i)
                           for row in range(3) for i in range(24)]
                lip_obj=self.mesh('Rolled painted fender lip '+label,lip_points,lip_faces,
                                  'Paint / warm yellow','01_Body')
                for face in lip_obj.data.polygons:
                    face.use_smooth=True
                for outer in (False,True):
                    for i in range(25):
                        angle=math.pi*i/24
                        yy=y+math.cos(angle)*.351
                        zz=self.RADIUS+math.sin(angle)*.351
                        depth=self.body_x(zz,yy)-.018 if outer else .602
                        liner.append((side*depth,yy,zz))
                self.mesh('Recessed wheel-well liner '+label,liner,
                          [(i,i+1,i+26,i+25) for i in range(24)],
                          'Tyre / rubber','03_Wheels')
                backwall=[(side*.600,y+math.cos(math.pi*i/24)*.349,
                           self.RADIUS+math.sin(math.pi*i/24)*.349) for i in range(25)]
                self.mesh('Dark inner wheel-well back panel '+label,backwall,
                          [tuple(range(len(backwall)))],'Tyre / rubber','03_Wheels')

    def rounded_lens_volume(self,name,center,profiles,material,rake=.055):
        """Manufactured lens/socket: soft outline, raked face and convex depth."""
        vertices=[]
        for width,height,radius,depth in profiles:
            for x,z in rounded_rectangle(width,height,radius,5):
                vertices.append((center[0]+x,center[1]+depth-rake*z,center[2]+z))
        count=len(vertices)//len(profiles)
        faces=[tuple(reversed(range(count))),
               tuple(range((len(profiles)-1)*count,len(profiles)*count))]
        for row in range(len(profiles)-1):
            for i in range(count):
                j=(i+1)%count
                faces.append((row*count+i,row*count+j,(row+1)*count+j,(row+1)*count+i))
        obj=self.mesh(name,vertices,faces,material)
        self.project_uv(obj.data,(0,2))
        for face in obj.data.polygons:
            face.use_smooth=True
        return obj

    def make_tail_wrap(self,side):
        # A single bent lens follows the rounded rear body corner. Separate
        # rear/side plaques would leave an unnatural split visible from behind.
        def path(inset):
            result=[(.830,-1.716-inset,1,0)]
            for index in range(9):
                angle=math.pi/2*index/8
                result.append((.783+.047*math.cos(angle),-1.759-.047*math.sin(angle),
                               math.cos(angle),-math.sin(angle)))
            result.append((.642+inset,-1.806,0,-1))
            return result
        for name,height,depth,offset,inset,material in (
            ('Tail wraparound gasket ',.250,.016,.005,0,'Glass / rubber seal'),
            ('Tail wraparound lens ',.220,.014,.017,.008,'Lamp / red')):
            section=rounded_rectangle(depth,height,min(.005,depth*.3),3)
            route=path(inset)
            vertices=[]
            for x,y,nx,ny in route:
                for distance,z in section:
                    distance+=offset
                    vertices.append((side*(x+nx*distance),y+ny*distance,.898+z))
            count=len(section)
            faces=[tuple(reversed(range(count))),
                   tuple(range((len(route)-1)*count,len(route)*count))]
            for row in range(len(route)-1):
                for i in range(count):
                    j=(i+1)%count
                    faces.append((row*count+i,row*count+j,(row+1)*count+j,(row+1)*count+i))
            obj=self.mesh(name+str(side),vertices,faces,material)
            for face in obj.data.polygons:
                face.use_smooth=True
        self.box('Rear amber lamp segment '+str(side),(side*.718,-1.840,.831),(.117,.009,.045),
                 'Lamp / amber',.003)

    def make_fascia(self):
        self.box('Deep front wraparound bumper',(0,1.856,.467),(1.75,.177,.197),
                 'Bumper / charcoal',.070)
        self.box('Rear wraparound bumper',(0,-1.818,.445),(1.70,.18,.179),
                 'Bumper / charcoal',.065)
        self.box('Recessed rectangular radiator grille',(0,1.828,.714),(.735,.027,.189),
                 'Grille / recess',.011)
        for z in (.649,.691,.733,.775):
            self.box('Horizontal grille slat',(0,1.843,z),(.696,.016,.011),
                     'Grille / bars',.003)
        self.box('Grille central divider',(0,1.848,.714),(.018,.012,.160),
                 'Bumper / charcoal',.002)
        for side in (-1,1):
            self.rounded_lens_volume('Soft headlamp socket '+str(side),(side*.535,1.826,.721),
                ((.291,.196,.016,-.009),(.289,.194,.017,.005)), 'Glass / rubber seal')
            self.rounded_lens_volume('Convex ivory headlamp '+str(side),(side*.535,1.834,.721),
                ((.274,.182,.015,0),(.270,.178,.017,.004),
                 (.250,.160,.022,.008),(.180,.107,.025,.010)), 'Lamp / ivory')
            self.rounded_lens_volume('Rounded amber corner lens '+str(side),(side*.735,1.822,.717),
                ((.093,.201,.014,0),(.090,.197,.015,.017),(.067,.168,.018,.023)), 'Lamp / amber')
            self.make_tail_wrap(side)
            self.box('Lower valance cooling slot '+str(side),(side*.485,1.823,.309),(.153,.010,.027),
                     'Paint / panel gaps',.003)
        self.box('Rear licence plate recess',(0,-1.811,.647),(.342,.022,.104),
                 'Bumper / charcoal',.006)
        self.box('Plain rear licence plate',(0,-1.826,.647),(.308,.009,.078),
                 'Wheel / hubcap',.002)
        self.box('Hatch handle and release button',(0,-1.821,.795),(.139,.035,.041),
                 'Bumper / charcoal',.005)

    def make_panel_details(self):
        for side in (-1,1):
            def on_side(y,z,extra=.002):
                return (side*(self.body_x(z,y)+extra),y,z)
            path=[(-.27,1.042),(-.33,.70),(-.39,.40),(-.44,.350),
                  (-.39,.319),(.89,.319),(.956,.344),(.98,.43),(.98,.70),(1.035,1.042)]
            rounded=rounded_polygon([(0,y,z) for y,z in path],.028,4)
            path=[(point.y,point.z) for point in rounded]
            path.append(path[0])
            # Follow each change of panel slope; a single long straight line
            # would disappear into the sheet metal between its endpoints.
            samples=[]
            for (ya,za),(yb,zb) in zip(path,path[1:]):
                count=max(2,math.ceil(math.hypot(yb-ya,zb-za)/.02))
                for index in range(count):
                    t=index/count
                    samples.append(on_side(ya+(yb-ya)*t,za+(zb-za)*t,.003))
            samples.append(on_side(*path[-1],.003))
            self.line('Single-door perimeter seam '+str(side),samples,
                      'Paint / panel gaps',.0018)
            self.box('Inset door handle surround '+str(side),on_side(-.14,.925,.007),
                     (.014,.155,.052),'Paint / panel gaps',.004)
            self.box('Small black door handle '+str(side),on_side(-.14,.925,.017),
                     (.022,.134,.038),'Bumper / charcoal',.004)
            bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=.0065,depth=.004,
                location=on_side(-.237,.925,.019),rotation=(0,math.pi/2,0))
            self.attach(bpy.context.object,'Door lock cylinder '+str(side),
                        'Wheel / rim edge','04_Details')
            self.project_uv(bpy.context.object.data)
            self.line('Lower pressed sill crease '+str(side),
                      [on_side(y,.298) for y in (.96,.45,-.50,-.82)],
                      'Paint / panel gaps',.002)
            self.box('Door mirror mounting stem '+str(side),(side*.824,.895,1.100),
                     (.145,.052,.04),'Bumper / charcoal',.006)
            self.box('Dark angular door mirror '+str(side),(side*.910,.910,1.139),
                     (.155,.190,.132),'Bumper / charcoal',.030)
            self.box('Inset rear-facing mirror reflector '+str(side),(side*.910,.811,1.139),
                     (.108,.007,.078),'Mirror / reflector',.0025)
            points=[(side*(self.cabin_x(z)+.010),y,z)
                    for y,z in ((.905,1.074),(.972,1.074),(.895,1.159))]
            self.mesh('Triangular mirror sail mount '+str(side),points,[(0,1,2)],'Bumper / charcoal')
            hood=[]
            for y in (1.12,1.24,1.46,1.65,1.74):
                width,top=self.body_section(y)
                hood.append((side*.756*width,y,top+.003))
            self.line('Fine bonnet edge seam '+str(side),hood,'Paint / panel gaps',.0013)
        fuel=[]
        for y,z in rounded_rectangle(.172,.143,.023,4):
            yy,zz=-.77+y,.868+z
            fuel.append((self.body_x(zz,yy)+.003,yy,zz))
        self.line('Right rear fuel filler flap',fuel+[fuel[0]],'Paint / panel gaps',.0016)
        self.box('Underbody behind suspension',(0,-.04,.254),(1.17,2.53,.12),
                 'Tyre / rubber',.018)
        self.line('Rear hatch lower shut line',[(-.717,-1.803,1.039),(0,-1.807,1.036),(.717,-1.803,1.039)],
                  'Paint / panel gaps',.0024)

    def build(self):
        self.make_body()
        self.make_glazing()
        self.make_wheels()
        self.make_fascia()
        self.make_panel_details()


def make_studio(scene, reference_path):
    studio=bpy.data.collections.new('90_Studio / not part of car')
    scene.collection.children.link(studio)
    def put(obj):
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        studio.objects.link(obj)
    # Curved cyclorama: no visible horizon across the top of the reference view.
    sections=[(-50,-.006),(12,-.006)]
    for index in range(1,25):
        angle=math.pi/2*index/24
        sections.append((12+4*math.sin(angle),4-4*math.cos(angle)-.006))
    sections.append((16,40))
    right=Vector((-.77056,-.63738,0))
    back=Vector((.63738,-.77056,0))
    vertices=[]
    for depth,z in sections:
        for side in (-1,1):
            vertices.append(tuple(back*depth+right*(side*50)+Vector((0,0,z))))
    mesh=bpy.data.meshes.new('Continuous studio backdrop')
    mesh.from_pydata(vertices,[],[(i*2,i*2+1,i*2+3,i*2+2) for i in range(len(sections)-1)])
    mesh.update()
    ground=bpy.data.objects.new('Studio cyclorama / render only',mesh)
    scene.collection.objects.link(ground)
    for face in mesh.polygons:
        face.use_smooth=True
    ground.hide_select=True
    put(ground)
    mat=bpy.data.materials.new('Studio / warm grey')
    mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=(*[linear(c) for c in (.86,.85,.84)],1)
    shader.inputs['Roughness'].default_value=.85
    ground.data.materials.append(mat)
    world=bpy.data.worlds.new('Soft studio environment')
    world.use_nodes=True
    world.node_tree.nodes['Background'].inputs['Color'].default_value=(.75,.75,.75,1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value=.75
    scene.world=world
    for name,position,power,size in (
        ('Large key softbox',(1,4,7),350,5),
        ('Front fill',(4,5,3),80,5),
        ('Roof rim',(-1,-4,6),100,4)):
        data=bpy.data.lights.new(name,'AREA')
        data.energy=power
        data.shape='DISK'
        data.size=size
        obj=bpy.data.objects.new(name,data)
        studio.objects.link(obj)
        obj.location=position
        aim(obj,(0,0,.65))
    camera_data=bpy.data.cameras.new('Reference perspective 73mm')
    camera=bpy.data.objects.new('Reference camera / front left',camera_data)
    studio.objects.link(camera)
    camera.location=(-5.52174,6.67556,1.88210)
    aim(camera,(0,0,.83027))
    camera_data.type='PERSP'
    camera_data.lens=73
    camera_data.shift_x=-.0284449
    scene.camera=camera
    # Embedded reference remains available even when the blend is moved elsewhere.
    reference=bpy.data.images.load(str(reference_path),check_existing=True)
    reference.pack()
    ref_collection=bpy.data.collections.new('00_Reference / packed source')
    scene.collection.children.link(ref_collection)
    image_empty=bpy.data.objects.new('Approved 01 hatchback reference',None)
    ref_collection.objects.link(image_empty)
    image_empty.empty_display_type='IMAGE'
    image_empty.data=reference
    image_empty.empty_display_size=4
    image_empty.location=(5,0,1.5)
    image_empty.hide_render=True
    image_empty.hide_viewport=True
    scene.render.engine='CYCLES'
    scene.cycles.samples=32
    scene.cycles.use_denoising=True
    scene.render.resolution_x=1140
    scene.render.resolution_y=720
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='Standard'
    scene.view_settings.look='None'
    scene.view_settings.exposure=0
    scene.view_settings.gamma=1
    return studio


def audit(car):
    depsgraph=bpy.context.evaluated_depsgraph_get()
    triangles=0
    for group in car.groups.values():
        for obj in group.objects:
            if obj.type not in ('MESH','CURVE'):
                continue
            evaluated=obj.evaluated_get(depsgraph)
            mesh=evaluated.to_mesh()
            mesh.calc_loop_triangles()
            triangles+=len(mesh.loop_triangles)
            evaluated.to_mesh_clear()
    return {'vehicle':'01_Hatchback','triangles':triangles,
            'wheel_count':len([o for o in bpy.data.objects if o.name.startswith('Wheel_')]),
            'opaque_glass':all(m.node_tree.nodes['Principled BSDF'].inputs['Transmission Weight'].default_value==0
                               for m in car.materials.values()),
            'packed_surface_textures':len(car.surfaces.images),
            'source_reference_packed':True,'unity_modified':False}


def render_detail_views(scene,review):
    camera=scene.camera
    camera.data.shift_x=0
    for name,position,target,lens in (
        ('04-Headlamp-detail.png',(-1.5,3.4,1.12),(-.40,1.84,.72),65),
        ('05-Wheel-detail.png',(-2.6,2.2,.65),(-.78,1.3543,.33),60)):
        camera.location=position
        aim(camera,target)
        camera.data.lens=lens
        scene.render.filepath=str(review/name)
        bpy.ops.render.render(write_still=True)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--project',type=Path,required=True)
    parser.add_argument('--preview-only',action='store_true')
    parser.add_argument('--detail-previews',action='store_true')
    parser.add_argument('--replace-sha256',help='Allow replacement only of this exact backed-up version')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    project=args.project.resolve()
    output=project/'ArtSource'/'TrafficVehicles'/'01_Hatchback'
    review=project/'Docs'/'VehicleStages'/'01_Hatchback'
    output.mkdir(parents=True,exist_ok=True)
    review.mkdir(parents=True,exist_ok=True)
    destination=output/'01_Hatchback.blend'
    previous_hash=None
    if destination.exists() and not args.preview_only:
        previous_hash=hashlib.sha256(destination.read_bytes()).hexdigest()
        if args.replace_sha256!=previous_hash:
            raise FileExistsError(f'Expected SHA256 is required to preserve artist edits before replacing {destination}')
        backup=project/'Library'/'SingleHatchback'/'Iterations'/f'Before-{previous_hash[:16]}.blend'
        backup.parent.mkdir(parents=True,exist_ok=True)
        if backup.exists():
            if hashlib.sha256(backup.read_bytes()).hexdigest()!=previous_hash:
                raise RuntimeError('The existing model backup does not match the original')
        else:
            shutil.copy2(destination,backup)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.name='01 Hatchback / reference-led model'
    scene.unit_settings.system='METRIC'
    scene.unit_settings.length_unit='METERS'
    car=Hatchback(scene)
    car.build()
    make_studio(scene,review/'Reference-closeup.png')
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    car.root.select_set(True)
    bpy.context.view_layer.objects.active=car.root
    # Open directly at the composed camera view; studio is clearly separated.
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.region_3d.view_perspective='CAMERA'
                area.spaces.active.overlay.show_overlays=False
                area.spaces.active.shading.type='MATERIAL'
    report=audit(car)
    print(json.dumps(report),flush=True)
    (review/'model-audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    scene.render.filepath=str(review/'01-Hatchback-render.png')
    notes=bpy.data.texts.new('READ ME / 01 Hatchback')
    notes.write('Individual vehicle 01, yellow three-door hatchback.\n'
                'Approved reference crop is packed into this file.\n'
                'One car only. Studio and reference are separate collections.\n'
                'Body and four independent wheel roots are under 01_Hatchback.\n'
                'Metres; +Y forward, Z up. No transparent glass or interior.\n'
                'Surface revision: rounded panel edges, convex fluted lenses,\n'
                'profiled tyres and dished hubcaps. Four UV surface maps are\n'
                'packed in this file; no external texture folder is needed.\n'
                'The visible three-quarter view is reference-led. Hidden rear\n'
                'surfaces are an interpretation, not a second supplied view.\n'
                'This is an artist review asset; Unity has not been changed.\n')
    if not args.preview_only:
        if previous_hash and hashlib.sha256(destination.read_bytes()).hexdigest()!=previous_hash:
            raise RuntimeError('The artist edited the model during generation; refusing to replace it')
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(destination))
    bpy.ops.render.render(write_still=True)
    if args.detail_previews:
        render_detail_views(scene,review)


if __name__=='__main__':
    main()
