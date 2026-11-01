"""Pinebrook checkpoint one: reusable low-poly architecture, metres, shared palette."""
from pathlib import Path
import bpy,bmesh,math,json,sys
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtSource/Pinebrook/Kit';OUT.mkdir(parents=True,exist_ok=True)
PALETTE=['#DED6BE','#BEB297','#363D41','#303E39','#4B6E77','#80957D','#B18653','#734D2C','#C5C1B1','#EDE6D4','#64725B','#AFB7A7','#353831','#A09785','#6D7B7B','#7E91A0']
FLOOR=3.2;BAY=4.4;DEPTH=12
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):bpy.data.collections.remove(c)
ATLAS=bpy.data.images.new('PinebrookSharedPalette',width=1024,height=1024)
colors=[tuple(int(h[i:i+2],16)/255 for i in (1,3,5)) for h in PALETTE]
pixels=[]
for y in range(1024):
 for x in range(1024):pixels.extend((*colors[(y//256)*4+x//256],1))
ATLAS.pixels.foreach_set(pixels);ATLAS.filepath_raw=str(OUT/'PinebrookPalette.png');ATLAS.file_format='PNG';ATLAS.save()
mat=bpy.data.materials.new('PinebrookShared');mat.use_nodes=True
nodes=mat.node_tree.nodes;bsdf=nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.85
tex=nodes.new('ShaderNodeTexImage');tex.image=ATLAS;tex.interpolation='Closest';mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
colornode=nodes.new('ShaderNodeVertexColor');colornode.layer_name='Color'
multiply=nodes.new('ShaderNodeMixRGB');multiply.blend_type='MULTIPLY';multiply.inputs[0].default_value=1
mat.node_tree.links.new(tex.outputs['Color'],multiply.inputs[1]);mat.node_tree.links.new(colornode.outputs['Color'],multiply.inputs[2]);mat.node_tree.links.new(multiply.outputs[0],bsdf.inputs['Base Color'])
# Authoring vertex tint is retained for future building colour variants.
active=None

def register(obj,name,index):
 obj.name=name
 for c in list(obj.users_collection):c.objects.unlink(obj)
 active.objects.link(obj);obj.data.materials.clear();obj.data.materials.append(mat)
 for layer in list(obj.data.uv_layers):obj.data.uv_layers.remove(layer)
 uv=obj.data.uv_layers.new(name='Palette')
 for value in uv.data:value.uv=((index%4+.5)/4,(index//4+.5)/4)
 color=obj.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
 for value in color.data:value.color=(1,1,1,1)
 return obj

def bake(obj):
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 for mod in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
 for poly in obj.data.polygons:poly.use_smooth=False
 return obj

def bevel(obj,width=.025):
 m=obj.modifiers.new('Soft two-segment bevel','BEVEL');m.width=width;m.segments=2;m.limit_method='ANGLE';m.angle_limit=.65
 return obj

def box(name,loc,size,index=0,rounding=.02):
 points=[(x*size[0]/2,y*size[1]/2,z*size[2]/2) for z in (-1,1) for y in (-1,1) for x in (-1,1)]
 o=mesh(name,points,[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],index);o.location=loc
 if rounding:
  bevel(o,min(rounding,min(size)*.23))
  if min(size)<.5:o.modifiers[-1].segments=1
 return o

def mesh(name,verts,faces,index=0,rounded=0):
 data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
 bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
 obj=bpy.data.objects.new(name,data);active.objects.link(obj);register(obj,name,index)
 if rounded:bevel(obj,rounded)
 return obj

def rod(name,a,b,r=.025,index=3,sides=6):
 a,b=Vector(a),Vector(b)
 points=[(r*math.cos(i*math.tau/sides),r*math.sin(i*math.tau/sides),z*(b-a).length/2) for z in (-1,1) for i in range(sides)]
 faces=[tuple(reversed(range(sides))),tuple(range(sides,2*sides))]+[(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)]
 o=mesh(name,points,faces,index);o.location=(a+b)/2;o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
 bevel(o,min(.006,r*.2));o.modifiers[-1].segments=1;return o

def text(name,word,loc,width,index=9):
 data=bpy.data.curves.new(name,'FONT');data.body=word;data.align_x='CENTER';data.align_y='CENTER';data.resolution_u=3;data.extrude=0
 o=bpy.data.objects.new(name,data);active.objects.link(o);bpy.ops.object.select_all(action='DESELECT');bpy.context.view_layer.objects.active=o;o.select_set(True)
 bpy.ops.object.convert(target='MESH');register(o,name,index)
 xs=[v.co.x for v in o.data.vertices];ys=[v.co.y for v in o.data.vertices]
 scale=min(width/(max(xs)-min(xs)),.30/(max(ys)-min(ys)))
 for v in o.data.vertices:v.co*=scale
 o.rotation_euler.x=math.pi/2;o.location=loc
 return o

def wall(window=True,wood=False,width=BAY):
 if not window:return [box('Plain wall',(0,0,FLOOR/2),(width,.24,FLOOR),0)]
 w,h=1.8,2.0;bottom=.65;top=bottom+h
 outside=[(-width/2,0),(width/2,0),(width/2,FLOOR),(-width/2,FLOOR)]
 inside=[(-w/2,bottom),(w/2,bottom),(w/2,top),(-w/2,top)]
 verts=[(x,-.12,z) for x,z in outside+inside]+[(x,0,z) for x,z in inside]
 faces=[]
 for i in range(4):j=(i+1)%4;faces.extend([(i,j,j+4,i+4),(i+4,j+4,j+8,i+8)])
 p=[mesh('Wall with real 12cm window reveal',verts,faces,6 if wood else 0),
    mesh('Inset teal pane',[(-w/2,.012,bottom),(w/2,.012,bottom),(w/2,.012,top),(-w/2,.012,top)],[(0,1,2,3)],4),
    box('Window sill',(0,-.17,bottom-.06),(w+.22,.36,.12),8),
    mesh('Window lintel',[(-w/2-.08,-.14,top),(w/2+.08,-.14,top),(w/2+.08,-.14,top+.1),(-w/2-.08,-.14,top+.1)],[(0,1,2,3)],8),
    mesh('Window mullion',[(-.027,-.025,bottom),(.027,-.025,bottom),(.027,-.025,top),(-.027,-.025,top)],[(0,1,2,3)],5)]
 # Thin frame geometry adds depth contrast without duplicating bevelled boxes.
 outer=[(-w/2-.075,bottom-.075),(w/2+.075,bottom-.075),(w/2+.075,top+.075),(-w/2-.075,top+.075)]
 p.append(mesh('Dark window surround',[(x,-.135,z) for x,z in outer+inside],[(i,(i+1)%4,(i+1)%4+4,i+4) for i in range(4)],3))
 if wood:
  for x in (-1.85,-1.55,-1.25,1.25,1.55,1.85):p.append(mesh('Timber slat',[(x-.025,-.145,.05),(x+.025,-.145,.05),(x+.025,-.145,FLOOR-.05),(x-.025,-.145,FLOOR-.05)],[(0,1,2,3)],7))
 return p

def balcony():
 p=[box('Balcony rounded slab',(0,-.6,.06),(2.5,1.2,.12),8)]
 for a,b in [((-1.2,-1.16,1.1),(1.2,-1.16,1.1)),((-1.2,0,1.1),(-1.2,-1.16,1.1)),((1.2,0,1.1),(1.2,-1.16,1.1))]:p.append(rod('Thin black handrail',a,b))
 for x in (-1.2,-.9,-.6,-.3,0,.3,.6,.9,1.2):p.append(rod('Balcony baluster',(x,-1.16,.12),(x,-1.16,1.1),.018))
 for x in (-1.2,1.2):
  for y in (-.38,-.76):p.append(rod('Side balcony baluster',(x,y,.12),(x,y,1.1),.018))
 p.append(box('Planter',(.65,-1.02,.28),(.6,.25,.27),6));p.append(box('Planter foliage',(.65,-1.02,.46),(.64,.28,.12),10))
 return p

def entrance():
 p=[box('Door frame',(0,0,1.1),(1.42,.20,2.2),3),box('Teal entrance door',(0,-.12,1.05),(1.2,.06,2.1),4),box('Door handle',(.45,-.18,1),(.04,.045,.32),8),box('Entrance canopy',(0,-.7,2.45),(2.6,1.6,.14),2)]
 for i in range(2):p.append(box('Entrance step',(0,-.45-i*.4,.08*(2-i)),(2,.45*(i+1),.08*(2-i)),8))
 return p

def roof(width=5,depth=5):
 rise=depth*.19
 v=[(-width/2,-depth/2,0),(width/2,-depth/2,0),(width/2,0,rise),(-width/2,0,rise),(-width/2,depth/2,0),(width/2,depth/2,0)]
 p=[mesh('Charcoal gable roof',v,[(0,1,2,3),(3,2,5,4),(0,3,4),(1,5,2)],2,rounded=.025)]
 # A few real raised seams, never photo textures.
 for i in range(1,int(width/.7)):
  x=-width/2+i*.7
  for side in (-1,1):
   p.append(mesh('Roof raised seam',[(x-.035,side*depth/2,.055),(x+.035,side*depth/2,.055),(x+.035,0,rise+.055),(x-.035,0,rise+.055)],[(0,1,2,3)],14))
 return p

def shopfront(word=None):
 p=[box('Storefront frame',(0,0,1.45),(BAY,.16,2.9),3),box('Shop glazing',(0,-.10,1.45),(BAY-.18,.04,2.65),4),box('Sign band',(0,-.16,2.97),(BAY,.16,.42),3),box('Sage awning',(0,-.7,2.65),(BAY,1.4,.13),5)]
 p.append(box('Shop mullion',(0,-.16,1.45),(.07,.05,2.6),5))
 if word:p.append(text('Tenant lettering',word,(0,-.26,2.96),BAY-.25))
 return p

def loggia():
 p=[box('Loggia rear wall',(0,.8,FLOOR/2),(BAY,.16,FLOOR),0),box('Loggia floor',(0,.3,.06),(BAY,1.2,.12),8),box('Loggia head',(0,.3,FLOOR-.08),(BAY,1.2,.16),0)]
 for x in (-BAY/2+.13,BAY/2-.13):p.append(box('Loggia cheek',(x,.3,FLOOR/2),(.26,1.2,FLOOR),0))
 for x in (-1.8,-1.2,-.6,0,.6,1.2,1.8):p.append(rod('Loggia baluster',(x,-.25,.12),(x,-.25,1.1),.018))
 p.append(rod('Loggia rail',(-2,-.25,1.1),(2,-.25,1.1)));return p

def collection(name):
 global active
 active=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(active);return active

def join(name,parts):
 # Evaluate the entire recipe once. Applying hundreds of modifiers through UI
 # operators separately repeatedly rebuilt the whole dependency graph.
 bpy.context.view_layer.update();graph=bpy.context.evaluated_depsgraph_get()
 vertices=[];faces=[];uvs=[];tints=[]
 for source in parts:
  evaluated=source.evaluated_get(graph);data=evaluated.to_mesh(preserve_all_data_layers=True,depsgraph=graph);offset=len(vertices)
  vertices.extend(tuple(source.matrix_world@v.co) for v in data.vertices)
  faces.extend(tuple(offset+i for i in p.vertices) for p in data.polygons)
  layer=data.uv_layers.active;color=data.color_attributes.get('Color')
  uvs.extend(tuple(v.uv) for v in layer.data)
  tints.extend(tuple(v.color) for v in color.data)
  evaluated.to_mesh_clear()
 data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update()
 uv=data.uv_layers.new(name='Palette');color=data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
 for value,coord in zip(uv.data,uvs):value.uv=coord
 for value,tint in zip(color.data,tints):value.color=tint
 data.materials.append(mat);obj=bpy.data.objects.new(name,data);active.objects.link(obj)
 for source in parts:bpy.data.objects.remove(source,do_unlink=True)
 data.calc_loop_triangles();return obj

def transform(parts,x,y,z,angle=0):
 bpy.context.view_layer.update()
 m=Matrix.Translation(Vector((x,y,z)))@Matrix.Rotation(angle,4,'Z')
 for o in parts:o.matrix_world=m@o.matrix_world
 return parts

records=[];modules=[]
def module(name,fn):
 collection(name);obj=join(name,fn());records.append({'name':name,'triangles':len(obj.data.loop_triangles),'size_m':list(obj.dimensions)});modules.append(obj)
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
 bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_unit_scale=True,add_leaf_bones=False)
 return obj


def garden_fence():
 """Three-metre child-safe timber fence section, soft edges, below 500 triangles."""
 return [box('Fence post',(x,0,.6),(.12,.12,1.2),6) for x in (-1.45,1.45)]+[rod('Fence rail',(-1.45,0,z),(1.45,0,z),.04,6) for z in (.35,.95)]+[box('Rounded picket',(x,0,.65),(.09,.09,.78),6) for x in (-.9,-.45,0,.45,.9)]

def bake_vertex_occlusion(obj):
 """Bake gentle local ambient occlusion into the existing colour attribute, never into a noisy image texture."""
 from mathutils.bvhtree import BVHTree
 tree=BVHTree.FromPolygons([v.co for v in obj.data.vertices],[list(p.vertices) for p in obj.data.polygons])
 cache={};attribute=obj.data.color_attributes.get('Color')
 if attribute is None:return
 for poly in obj.data.polygons:
  normal=poly.normal.normalized();axis=Vector((0,0,1)) if abs(normal.z)<.9 else Vector((1,0,0))
  tangent=normal.cross(axis).normalized();bitangent=normal.cross(tangent).normalized()
  for loop in poly.loop_indices:
   position=obj.data.vertices[obj.data.loops[loop].vertex_index].co
   key=tuple(round(v,4) for v in (*position,*normal))
   if key not in cache:
    blocked=0
    for sample in range(8):
     angle=sample*2.39996323;z=.22+.7*(sample+.5)/8;r=math.sqrt(1-z*z)
     direction=normal*z+tangent*(math.cos(angle)*r)+bitangent*(math.sin(angle)*r)
     hit=tree.ray_cast(position+normal*.018,direction,1.25)
     if hit[0] is not None:blocked+=1
    cache[key]=1-.20*blocked/8
   shade=cache[key];attribute.data[loop].color=(shade,shade,shade,1)
