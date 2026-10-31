"""Convert the 96 approved Z-up GLBs using the established Unity mesh contract."""
from pathlib import Path
import hashlib
import json
import struct
from PrepareWave2Unity import read

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'Assets/Art/FoodExpansion'
GROUPS=[
 ('Food_01A_RootsSquash','Produce',5,.7),('Food_01B_GreensVegetables','Produce',4,.4),
 ('Food_01C_TropicalFruit','Produce',6,.4),('Food_01D_SmallFruitBerries','Produce',5,.2),
 ('Food_05_Grains','Groceries',5,.5),('Food_06_DriedLegumes','Groceries',5,.5),
 ('Food_07_PlantBasedSweet','Preserves',6,.4),('Food_08_CookingEssentials','Preserves',6,.3),
 ('Food_09_CheeseDairy','Dairy',7,.25),('Food_10_MeatSeafood','Meat',12,.7),
 ('Food_11_Drinks','Drinks',5,.5),('Food_12_Spices','Groceries',4,.08),
 ('Food_13_Bakery','Bakery',6,.3),('Food_14_SweetSnacks','Snacks',4,.15),
 ('Food_15_ReadyFood','ReadyMeals',8,.4),('Food_16_SpreadsTreats','Preserves',7,.3)]
STORAGE={'coconut_milk':'Groceries','oat_milk':'Drinks','almond_milk':'Drinks',
 'hummus':'ReadyMeals','coconut_oil':'Oil','apple_cider_vinegar':'Groceries',
 'balsamic_vinegar':'Groceries','halva':'Snacks','fruit_leather':'Snacks'}

def convert(path,storage,cost,weight):
    blob=path.read_bytes();size=struct.unpack_from('<I',blob,12)[0]
    doc=json.loads(blob[20:20+size]);start=28+size;parts=[]
    assert len(doc['nodes'])==1 and len(doc['meshes'])==1,path
    for primitive in doc['meshes'][0]['primitives']:
        attributes=primitive['attributes']
        positions=read(blob,doc,start,attributes['POSITION'])
        normals=read(blob,doc,start,attributes['NORMAL'])
        colors=read(blob,doc,start,attributes['COLOR_0']) if 'COLOR_0' in attributes else [[1,1,1,1]]*len(positions)
        indices=[value[0] for value in read(blob,doc,start,primitive['indices'])]
        # Axis swap reverses handedness, so reverse winding as well as normals.
        for index in range(0,len(indices),3):indices[index+1],indices[index+2]=indices[index+2],indices[index+1]
        parts.append(dict(positions=[c for p in positions for c in (p[0],p[2],p[1])],
            normals=[c for n in normals for c in (n[0],n[2],n[1])],
            colors=[c for color in colors for c in (color+[1])[:4]],indices=indices,material=primitive['material']))
    materials=[]
    for material in doc['materials']:
        pbr=material['pbrMetallicRoughness']
        materials.append(dict(name=material['name'],color=pbr.get('baseColorFactor',[1,1,1,1]),
            metallic=pbr.get('metallicFactor',0),roughness=pbr.get('roughnessFactor',.82),
            vertexColor=material['name']=='GeometryPigment'))
    bounds=[[min(part['positions'][axis::3]) for axis in range(3)] for part in parts]
    assert abs(min(bound[1] for bound in bounds))<1e-6,path
    source=dict(key=path.stem,name=path.stem.replace('_',' ').title(),storage=STORAGE.get(path.stem,storage),
        cost=cost,sale=cost*2,weight=weight,volume=.002,parts=parts,materials=materials)
    return source,dict(key=path.stem,folder=path.parent.name,sha256=hashlib.sha256(blob).hexdigest(),
        storage=source['storage'],triangles=sum(len(part['indices'])//3 for part in parts))

def main():
    (ART/'Source').mkdir(parents=True,exist_ok=True);records=[]
    for folder,storage,cost,weight in GROUPS:
        paths=sorted((ROOT/'ArtSource'/folder).glob('*.glb'));assert len(paths)==6,folder
        for path in paths:
            source,record=convert(path,storage,cost,weight)
            (ART/'Source'/(path.stem+'.json')).write_text(json.dumps(source),encoding='utf-8')
            records.append(record)
    assert len(records)==len({record['key'] for record in records})==96
    (ART/'Manifest.json').write_text(json.dumps(dict(products=records),indent=2),encoding='utf-8')
    print('Prepared 96 real models, axis conversion, colours and purchase definitions.')

if __name__=='__main__':main()
