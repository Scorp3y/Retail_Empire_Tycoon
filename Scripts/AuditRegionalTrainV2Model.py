"""Checks the deliverable GLB, including instanced geometry, axes, pivots and flat normals."""
import json,struct
from pathlib import Path
import numpy as np

project=Path(__file__).resolve().parents[1]
data=(project/'ArtSource/RegionalTrainV2/RegionalTrainV2.glb').read_bytes()
assert data[:4]==b'glTF'
length=struct.unpack_from('<I',data,12)[0]
document=json.loads(data[20:20+length])
binary=data[28+length:]
assert not document.get('textures'),'Model unexpectedly uses image textures'

def accessor(index):
    a=document['accessors'][index];view=document['bufferViews'][a['bufferView']]
    dtype={5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]
    columns={'SCALAR':1,'VEC3':3}[a['type']]
    return np.ndarray((a['count'],columns),dtype=dtype,buffer=binary,
        offset=view.get('byteOffset',0)+a.get('byteOffset',0),
        strides=(view.get('byteStride',np.dtype(dtype).itemsize*columns),np.dtype(dtype).itemsize))

def local(node):
    if 'matrix' in node:return np.array(node['matrix']).reshape(4,4,order='F')
    x,y,z,w=node.get('rotation',[0,0,0,1])
    result=np.eye(4)
    result[:3,:3]=np.array([[1-2*y*y-2*z*z,2*x*y-2*z*w,2*x*z+2*y*w],
        [2*x*y+2*z*w,1-2*x*x-2*z*z,2*y*z-2*x*w],
        [2*x*z-2*y*w,2*y*z+2*x*w,1-2*x*x-2*y*y]])@np.diag(node.get('scale',[1,1,1]))
    result[:3,3]=node.get('translation',[0,0,0]);return result

positions=[];triangles=0;wheel_pivots=0;car_centers={};headlights=[]
def visit(index,parent):
    global triangles,wheel_pivots
    node=document['nodes'][index];world=parent@local(node);name=node.get('name','')
    assert 'Preview' not in name,'Preview track exported into train GLB'
    if name in ('Car1','Car2','Car3'):car_centers[name]=world[:3,3].tolist()
    if name.startswith('WheelPivot_'):wheel_pivots+=1
    if name.startswith('HeadlightLens'):headlights.append(world[:3,3])
    if 'mesh' in node:
        for primitive in document['meshes'][node['mesh']]['primitives']:
            vertices=accessor(primitive['attributes']['POSITION'])
            positions.append(np.c_[vertices,np.ones(len(vertices))]@world.T)
            indices=accessor(primitive['indices']).ravel();triangles+=len(indices)//3
            normals=accessor(primitive['attributes']['NORMAL'])[indices].reshape(-1,3,3)
            assert np.max(np.abs(normals[:,0]-normals[:,1]))<.0001 and np.max(np.abs(normals[:,0]-normals[:,2]))<.0001,'Smooth normals found'
    for child in node.get('children',[]):visit(child,world)

for root in document['scenes'][document.get('scene',0)]['nodes']:visit(root,np.eye(4))
vertices=np.concatenate(positions)[:,:3];minimum=vertices.min(axis=0);maximum=vertices.max(axis=0)
assert triangles==20736 and wheel_pivots==24
assert np.max(np.abs((maximum-minimum)-[2.96,3.58,48.06]))<.005
assert abs(minimum[1])<.001 and abs((minimum[2]+maximum[2])*.5)<.001
assert abs(car_centers['Car1'][2]-16)<.001 and abs(car_centers['Car3'][2]+16)<.001
assert max(p[2] for p in headlights)>23.7,'Leading headlights do not face +Z'
result=dict(status='PASS',triangles=triangles,wheel_pivots=wheel_pivots,
    dimensions= (maximum-minimum).tolist(),car_centers=car_centers,materials=len(document['materials']),
    textures=0,normals='Flat on every triangle',forward='+Z',up='+Y',pivot='Bottom center at rail contact')
(project/'Docs/RegionalTrainV2/GLBAudit.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print(json.dumps(result))
