"""English art brief: small friendly rounded everyday objects for the Pinebrook environment."""
import math,bpy
from mathutils import Vector
import PinebrookGeometry as g

def bicycle_parts():
    parts=[]
    for x in (-.65,.65):
        vertices=[]
        for i in range(12):
            angle=i*math.tau/12
            for j in range(3):
                tube=j*math.tau/3;radius=.275+.045*math.cos(tube)
                vertices.append((x+math.cos(angle)*radius,.045*math.sin(tube),.32+math.sin(angle)*radius))
        faces=[]
        for i in range(12):
            for j in range(3):faces.append((i*3+j,((i+1)%12)*3+j,((i+1)%12)*3+(j+1)%3,i*3+(j+1)%3))
        parts.append(g.mesh('Soft faceted tyre',vertices,faces,2))
    a=(-.65,0,.32);b=(.65,0,.32);c=(.55,0,1.05)
    parts.extend([g.rod('Sage bicycle frame',a,b,.03,5,sides=5),g.rod('Sage bicycle frame',b,c,.03,5,sides=5),g.rod('Sage bicycle frame',c,a,.03,5,sides=5),g.rod('Handlebar',(.55,-.22,1.05),(.55,.22,1.05),.025,3,sides=5),g.rod('Saddle post',(-.22,0,.58),(-.22,0,.9),.022,5,sides=5),g.box('Soft saddle',(-.22,0,.9),(.35,.22,.1),3)])
    return parts

def stroller_parts():
    parts=[g.box('Pram basket',(0,0,.45),(.65,.53,.45),5,.12),g.box('Soft canopy',(.13,0,.79),(.48,.6,.25),9,.11)]
    for x in (-.3,.3):
        for y in (-.28,.28):
            bpy.ops.mesh.primitive_uv_sphere_add(segments=8,ring_count=4,radius=1,location=(x,y,.12))
            wheel=g.register(bpy.context.object,'Rounded wheel',2)
            for vertex in wheel.data.vertices:vertex.co=Vector((vertex.co.x*.12,vertex.co.y*.055,vertex.co.z*.12))
            parts.append(wheel)
    for y in (-.24,.24):parts.append(g.rod('Stroller handle post',(-.3,y,.12),(-.4,y,1.08),.022,3))
    parts.append(g.rod('Handle grip',(-.4,-.24,1.08),(-.4,.24,1.08),.025,3));return parts

def parking_sign_parts():return [g.box('Parking post',(0,0,1.1),(.1,.1,2.2),3),g.box('Parking square',(0,0,1.95),(.7,.14,.7),4)]
