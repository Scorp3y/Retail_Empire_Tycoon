"""Read original GLBs losslessly; convert Z-up to Unity Y-up, retaining colours."""
from pathlib import Path
import json,struct
root=Path(__file__).resolve().parents[1]
groups=[('Wave2_03_Preserves','Preserves'),('Wave2_04_ReadyMeals','ReadyMeals'),('Wave2_05_PetSupplies','PetSupplies'),('Wave2_06_ToysGames','Toys'),('Wave2_07_Stationery','Stationery'),('Wave2_08_Kitchenware','Kitchenware')]
def read(blob,doc,start,index):
 a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']];n={'SCALAR':1,'VEC3':3,'VEC4':4}[a['type']];fmt={5126:'f',5125:'I',5123:'H',5121:'B'}[a['componentType']];size=struct.calcsize(fmt)*n
 values=[list(struct.unpack_from('<'+fmt*n,blob,start+v.get('byteOffset',0)+a.get('byteOffset',0)+i*v.get('byteStride',size))) for i in range(a['count'])]
 if a.get('normalized'):values=[[c/(255 if fmt=='B' else 65535) for c in p] for p in values]
 return values
products=[]
for folder,storage in groups:
 for p in sorted((root/'ArtSource'/folder).glob('*.glb')):
  blob=p.read_bytes();n=struct.unpack_from('<I',blob,12)[0];d=json.loads(blob[20:20+n]);start=28+n;parts=[]
  for prim in d['meshes'][0]['primitives']:
   pos=read(blob,d,start,prim['attributes']['POSITION']);norm=read(blob,d,start,prim['attributes']['NORMAL']);colors=read(blob,d,start,prim['attributes']['COLOR_0']) if 'COLOR_0' in prim['attributes'] else [[1,1,1,1]]*len(pos)
   indices=[v[0] for v in read(blob,d,start,prim['indices'])]
   for i in range(0,len(indices),3):indices[i+1],indices[i+2]=indices[i+2],indices[i+1]
   parts.append(dict(positions=[c for v in pos for c in (v[0],v[2],v[1])],normals=[c for v in norm for c in (v[0],v[2],v[1])],colors=[c for v in colors for c in (v+[1])[:4]],indices=indices,material=prim['material']))
  mats=[]
  for m in d['materials']:
   b=m['pbrMetallicRoughness'];mats.append(dict(name=m['name'],color=b.get('baseColorFactor',[1,1,1,1]),metallic=b.get('metallicFactor',0),roughness=b.get('roughnessFactor',.82),vertexColor=m['name']=='GeometryPigment'))
  key=p.stem;name=key.replace('_jar','').replace('_',' ').title();cost=5 if storage in ('Preserves','ReadyMeals') else 9
  doc=dict(key=key,name=name,storage=storage,cost=cost,sale=cost*2,weight=.3,volume=.002,parts=parts,materials=mats)
  (root/'Assets/Art/Wave2/Source'/(key+'.json')).write_text(json.dumps(doc))
  products.append(key)
assert len(products)==36
print('Prepared 36 original meshes with native colour/material data')
