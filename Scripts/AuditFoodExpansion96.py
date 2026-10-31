"""Audit actual binary geometry and report all four Food Expansion parts."""
from pathlib import Path
import json
import math
import re
import struct

ROOT=Path(__file__).resolve().parents[1]
FOLDERS=['Food_01A_RootsSquash','Food_01B_GreensVegetables','Food_01C_TropicalFruit','Food_01D_SmallFruitBerries',
 'Food_05_Grains','Food_06_DriedLegumes','Food_07_PlantBasedSweet','Food_08_CookingEssentials',
 'Food_09_CheeseDairy','Food_10_MeatSeafood','Food_11_Drinks','Food_12_Spices',
 'Food_13_Bakery','Food_14_SweetSnacks','Food_15_ReadyFood','Food_16_SpreadsTreats']

def load_glb(path):
    blob=path.read_bytes()
    assert blob[:4]==b'glTF' and struct.unpack_from('<I',blob,8)[0]==len(blob),path
    size,kind=struct.unpack_from('<II',blob,12);assert kind==0x4E4F534A
    doc=json.loads(blob[20:20+size]);bin_size,bin_kind=struct.unpack_from('<II',blob,20+size)
    assert bin_kind==0x004E4942 and 28+size+bin_size==len(blob)
    return doc,blob,28+size

def accessor(doc,blob,start,index):
    a=doc['accessors'][index];view=doc['bufferViews'][a['bufferView']]
    n={'SCALAR':1,'VEC3':3,'VEC4':4}[a['type']]
    fmt={5126:'f',5125:'I',5123:'H',5121:'B'}[a['componentType']]
    size=struct.calcsize(fmt)*n;stride=view.get('byteStride',size)
    offset=start+view.get('byteOffset',0)+a.get('byteOffset',0)
    assert offset+(a['count']-1)*stride+size<=len(blob)
    return [struct.unpack_from('<'+fmt*n,blob,offset+i*stride) for i in range(a['count'])]

def audit_model(path,record):
    doc,blob,start=load_glb(path)
    assert len(doc['meshes'])==len(doc['nodes'])==1 and not doc.get('images') and not doc.get('textures')
    assert not any(k in doc['nodes'][0] for k in ('translation','rotation','scale','matrix'))
    assert 2<=len(doc['materials'])<=4
    positions=[];triangles=0
    for primitive in doc['meshes'][0]['primitives']:
        points=accessor(doc,blob,start,primitive['attributes']['POSITION'])
        indices=accessor(doc,blob,start,primitive['indices'])
        assert len(indices)%3==0 and all(0<=i[0]<len(points) for i in indices)
        assert all(math.isfinite(v) for point in points for v in point)
        assert 'NORMAL' in primitive['attributes']
        triangles+=len(indices)//3;positions+=points
        if doc['materials'][primitive['material']]['name']=='GeometryPigment':
            assert 'COLOR_0' in primitive['attributes']
            color_index=primitive['attributes']['COLOR_0']
            colors=accessor(doc,blob,start,color_index)
            assert len(colors)==len(points)
            component=doc['accessors'][color_index]['componentType']
            maximum={5121:255,5123:65535,5126:1}[component]
            assert all(math.isfinite(value) and 0<=value<=maximum for color in colors for value in color)
    assert triangles==record['triangles'] and triangles<record['budget_exclusive'],path
    low=[min(p[i] for p in positions) for i in range(3)]
    high=[max(p[i] for p in positions) for i in range(3)]
    assert abs(low[2])<1e-7 and abs(low[0]+high[0])<1e-7 and abs(low[1]+high[1])<1e-7,path
    dims=[b-a for a,b in zip(low,high)]
    assert all(abs(a-b)<1e-6 for a,b in zip(dims,record['size_m'])) and .02<max(dims)<.4
    for material in doc['materials']:
        pbr=material['pbrMetallicRoughness'];clear=material.get('alphaMode')=='BLEND'
        assert pbr['metallicFactor']==0 and abs(pbr['roughnessFactor']-(.2 if clear else .82))<1e-6
        if clear:assert abs(pbr['baseColorFactor'][3]-.25)<1e-6
    return triangles

records=[]
for category,folder in enumerate(FOLDERS,1):
    directory=ROOT/'ArtSource'/folder;report=json.loads((directory/'Validation.json').read_text())
    assert len(report['models'])==len(list(directory.glob('*.glb')))==6
    assert (directory/'Models.blend').is_file() and (directory/'Preview.png').is_file()
    scripts=list(directory.glob('Build*.py'));assert len(scripts)==1
    defaults=re.findall(r'^ITERATION\s*=\s*(\d+)\s*$',scripts[0].read_text(encoding='utf-8-sig'),re.M)
    assert defaults and all(int(n)==report['iteration'] for n in defaults)
    for iteration in (('01','02') if category<=4 else ('01','02','03')):
        assert (directory/'Iterations'/iteration/'Preview.png').is_file()
    for model in report['models']:
        count=audit_model(directory/(model['name']+'.glb'),model)
        records.append(dict(name=model['name'],triangles=count,folder=str(directory),category=category))
assert len(records)==len({r['name'] for r in records})==96

def table(items):
    return '\n'.join(['| Модель | Треугольники | Папка |','| --- | ---: | --- |']+[
        f"| {r['name'].replace('_',' ').title()} | {r['triangles']} | [{Path(r['folder']).name}](<{Path(r['folder']).as_posix()}>) |" for r in items])

notes='''Экспорты: один объект/mesh/node на GLB, Z-up, метры, основание Z=0 и центр в плане в начале координат.
Нет изображений и текстур; 2–4 материала на продукт, мягкие фаски и простые тёплые цвета.
Цвета вершин используют GeometryPigment, совместимый с имеющимся конвертером проекта.
В каждой папке сохранены Models.blend, самостоятельный скрипт, Preview.png и Validation.json.
Последняя часть прошла первую версию и два прохода исправлений, все превью просмотрены.

Все продукты целые: буррито закрыты, образцов со срезами нет. Отверстия канноли и
свёрнутой коры корицы — естественная форма продукта. Содержимое и упаковка раздельны
в редактируемом исходнике и объединены в экспортную сетку с отдельными материалами.

Проверены реальные бинарные позиции и индексы GLB, границы индексов, бюджеты треугольников,
нормали, отсутствие текстур, материалы, прозрачность alpha 0.25 / roughness 0.2,
матовые поверхности roughness 0.82, размеры, начало координат и наличие превью.

Фактическая сборка — Blender 5.2.3 LTS. Отдельный запуск скриптов в 4.x не проверялся.
Перед использованием в Unity проверить прозрачность, ориентацию Z-up и цвета вершин
в игровом освещении. Эти 96 продуктов ещё не включены в каталог закупок Unity.
'''
final=[r for r in records if r['category']>=13]
(ROOT/'Docs/FoodFinalExpansion.md').write_text('# Food Expansion — часть 4\n\n'+notes+'\n'+table(final)+'\n',encoding='utf-8')
combined='# Food Expansion — все 96 моделей\n\n'+notes+f"\nИтого: 96 моделей, {sum(r['triangles'] for r in records)} треугольников.\n\n"+table(records)+'\n'
(ROOT/'Docs/FoodExpansion96.md').write_text(combined,encoding='utf-8')
(ROOT/'ArtSource/FoodExpansion96_Validation.json').write_text(json.dumps(dict(status='PASS',models=records),indent=2),encoding='utf-8')
print('PASS: 96 binary GLBs; 16 categories; geometry, materials, budgets, origins and previews.')
print('Reports: Docs/FoodFinalExpansion.md; Docs/FoodExpansion96.md')
