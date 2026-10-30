"""Convert approved GLB geometry into native Unity source data, omitting collider render meshes."""
from pathlib import Path
import json,struct
root=Path(__file__).resolve().parents[1]
ns={'__file__':str(root/'Scripts/PrepareWave2Unity.py')}
exec((root/'Scripts/PrepareWave2Unity.py').read_text().split('products=[]')[0],ns)
read=ns['read']
families=[('Expansion_01_BreadPastries','Bakery'),('Expansion_02_IceCreamDesserts','Frozen'),('Expansion_03_SaucesCondiments','Preserves')]
families += [(name,'Household') for name in ['Expansion_04_PersonalCare','Expansion_05_CleaningTools','Expansion_06_BabyCare','Expansion_07_GardenSupplies','Expansion_08_HomeComfort']]
for folder,storage in families+[(f'Fixtures_{g}','fixture') for g in 'ABCD']:
 for path in (root/'ArtSource'/folder).glob('*.glb'):
  blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];d=json.loads(blob[20:20+n]);start=28+n
  node=next((x for x in d['nodes'] if x['name']==path.stem),d['nodes'][0]);parts=[]
  for prim in d['meshes'][node.get('mesh',0)]['primitives']:
   pos=read(blob,d,start,prim['attributes']['POSITION']);norm=read(blob,d,start,prim['attributes']['NORMAL']);cs=read(blob,d,start,prim['attributes']['COLOR_0']) if 'COLOR_0' in prim['attributes'] else [[1,1,1,1]]*len(pos);idx=[x[0] for x in read(blob,d,start,prim['indices'])]
   for i in range(0,len(idx),3):idx[i+1],idx[i+2]=idx[i+2],idx[i+1]
   parts.append(dict(positions=[c for v in pos for c in (v[0],v[2],v[1])],normals=[c for v in norm for c in (v[0],v[2],v[1])],colors=[c for v in cs for c in (v+[1])[:4]],indices=idx,material=prim['material']))
  mats=[]
  for m in d['materials']:
   b=m['pbrMetallicRoughness'];mats.append(dict(name=m['name'],color=b.get('baseColorFactor',[1,1,1,1]),metallic=b.get('metallicFactor',0),roughness=b.get('roughnessFactor',.82),vertexColor=m['name']=='GeometryPigment',emission=m.get('emissiveFactor',[0,0,0])))
  doc=dict(key=path.stem,name=path.stem.replace('_',' ').title(),storage=storage,cost=8,sale=16,weight=.4,volume=.004,parts=parts,materials=mats,fixture=storage=='fixture')
  if doc['fixture']:
   record=next(x for x in json.loads((path.parent/'Validation.json').read_text())['fixtures'] if x['name']==path.stem);doc['anchors']=record['anchors'];doc['name']=' '.join(path.stem.split('_')[2:]).title()
  (root/'Assets/Art/Wave2/Source'/(path.stem+'.json')).write_text(json.dumps(doc))
print('Prepared 48 products and 16 fixtures')
