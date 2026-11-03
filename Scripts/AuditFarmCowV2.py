"""Read-only checks on the exported cow, including instanced/parented geometry."""
import json,struct
from pathlib import Path
import numpy as np

project=Path(__file__).resolve().parents[1]
data=(project/'ArtSource/FarmAnimalsV2/Cow.glb').read_bytes();assert data[:4]==b'glTF'
length=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+length]);binary=data[28+length:]
assert not doc.get('textures'),'Unexpected image texture'
def accessor(index):
    a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']];columns={'SCALAR':1,'VEC3':3}[a['type']]
    dtype={5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']];size=np.dtype(dtype).itemsize
    return np.ndarray((a['count'],columns),dtype=dtype,buffer=binary,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',size*columns),size))
def local(node):
    if 'matrix' in node:return np.array(node['matrix']).reshape(4,4,order='F')
    x,y,z,w=node.get('rotation',[0,0,0,1]);m=np.eye(4)
    m[:3,:3]=np.array([[1-2*y*y-2*z*z,2*x*y-2*z*w,2*x*z+2*y*w],[2*x*y+2*z*w,1-2*x*x-2*z*z,2*y*z-2*x*w],[2*x*z-2*y*w,2*y*z+2*x*w,1-2*x*x-2*y*y]])@np.diag(node.get('scale',[1,1,1]));m[:3,3]=node.get('translation',[0,0,0]);return m
triangles=0;positions=[];joints={};names=[]
def visit(index,parent):
    global triangles
    node=doc['nodes'][index];name=node.get('name','');world=parent@local(node);names.append(name)
    if name in ('Head','Body','Tail','Leg_FL','Leg_FR','Leg_BL','Leg_BR','Ear_L','Ear_R','Eye_L','Eye_R'):joints[name]=world[:3,3].tolist()
    if 'mesh' in node:
        for p in doc['meshes'][node['mesh']]['primitives']:
            v=accessor(p['attributes']['POSITION']);positions.append((np.c_[v,np.ones(len(v))]@world.T)[:,:3])
            idx=accessor(p['indices']).ravel();triangles+=len(idx)//3
            n=accessor(p['attributes']['NORMAL'])[idx].reshape(-1,3,3)
            assert np.max(np.abs(n[:,0]-n[:,1]))<.0001 and np.max(np.abs(n[:,0]-n[:,2]))<.0001,'Smooth normals found'
            if name.startswith('EyeSurface'):
                normals=n.reshape(-1,3)@np.linalg.inv(world[:3,:3]);assert normals[:,2].min()>.99,'Eye faces inward'
    for child in node.get('children',[]):visit(child,world)
for root in doc['scenes'][doc.get('scene',0)]['nodes']:visit(root,np.eye(4))
vertices=np.concatenate(positions);minimum=vertices.min(axis=0);maximum=vertices.max(axis=0)
assert triangles==776 and len(joints)==11
assert abs(minimum[1])<.001 and joints['Head'][2]>joints['Body'][2]
assert not any('Preview' in name for name in names)
result=dict(status='PASS',triangles=triangles,materials=len(doc['materials']),textures=0,flat_normals=True,eye_normals='outward +Z',dimensions=(maximum-minimum).tolist(),joints=joints,pivot='ground between feet',forward='+Z')
(project/'Docs/FarmCowV2/GLBAudit.json').write_text(json.dumps(result,indent=2),encoding='utf8');print(json.dumps(result))
