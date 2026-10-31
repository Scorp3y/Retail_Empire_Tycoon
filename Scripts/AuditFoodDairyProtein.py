"""Independently verify the 24 exported assets for Food Expansion part three."""
from pathlib import Path
import json
import struct

root=Path(__file__).resolve().parents[1]
folders=['Food_09_CheeseDairy','Food_10_MeatSeafood','Food_11_Drinks','Food_12_Spices']
rows=[]
for folder in folders:
    directory=root/'ArtSource'/folder
    report=json.loads((directory/'Validation.json').read_text())
    assert len(report['models'])==6
    for model in report['models']:
        name=model['name'];path=directory/(name+'.glb');blob=path.read_bytes()
        assert blob[:4]==b'glTF' and struct.unpack_from('<I',blob,8)[0]==len(blob)
        length=struct.unpack_from('<I',blob,12)[0];doc=json.loads(blob[20:20+length])
        assert len(doc['nodes'])==len(doc['meshes'])==1
        assert not any(k in doc['nodes'][0] for k in ('translation','rotation','scale','matrix'))
        assert not doc.get('textures') and not doc.get('images')
        primitives=doc['meshes'][0]['primitives']
        count=sum(doc['accessors'][p['indices']]['count']//3 for p in primitives)
        assert count==model['triangles'] and count<model['budget_exclusive']
        assert 2<=len(doc['materials'])<=4
        for m in doc['materials']:
            pbr=m['pbrMetallicRoughness'];clear=m.get('alphaMode')=='BLEND'
            assert pbr['metallicFactor']==0
            assert abs(pbr['roughnessFactor']-(.2 if clear else .82))<1e-6
            if clear:assert abs(pbr['baseColorFactor'][3]-.25)<1e-6
            if m['name']=='GeometryPigment':
                index=doc['materials'].index(m)
                assert all('COLOR_0' in p['attributes'] for p in primitives if p['material']==index)
        accessors=[doc['accessors'][p['attributes']['POSITION']] for p in primitives]
        low=[min(a['min'][i] for a in accessors) for i in range(3)]
        high=[max(a['max'][i] for a in accessors) for i in range(3)]
        size=[b-a for a,b in zip(low,high)]
        assert abs(low[2])<1e-7 and abs(low[0]+high[0])<1e-7 and abs(low[1]+high[1])<1e-7
        assert .06<max(size)<.30
        assert all(abs(a-b)<1e-6 for a,b in zip(size,model['size_m']))
        if folder=='Food_10_MeatSeafood':
            assert .215<size[0]<.23 and model['cavities']
        for iteration in ('01','02','03'):
            assert (directory/'Iterations'/iteration/'Preview.png').is_file()
        assert (directory/'Models.blend').is_file()
        rows.append(f'| {name} | {count} | [{folder}](<{directory.as_posix()}>) |')
text='''# Food Expansion — часть 3

24 отдельных GLB в четырёх категориях. В каждой папке: самодостаточный скрипт,
Models.blend с отдельными редактируемыми частями, финальный Preview.png,
Validation.json и три прохода с превью в Iterations/01, 02, 03.

Сохранены функции палитры, примитивов, фасок с двумя сегментами, экспорта и рендера
из предыдущих частей. Общие формы: лоток белковых продуктов, банка специй,
картонная упаковка напитков и бутылка. Геометрия содержимого отделена от упаковки.
Для полых сосудов толщина и внутреннее дно проверяются лучом в Blender.

По подтверждённому правилу проекта бри сделан целым, мидии — закрытыми;
нет клиньев, разрезанных образцов или рисунков срезов на упаковке.
Текст — сетка; если длинная надпись превышает бюджет, используется простой значок.
Цвета вершин пищевых частей используют GeometryPigment — имя уже поддержано
существующим конвертером проекта. Текстур и сторонних зависимостей нет.

Независимая проверка GLB: один mesh/node, бюджеты треугольников, 2–4 материала,
отсутствие изображений и текстур, metallic 0, roughness 0.82 / 0.2,
alpha 0.25 у прозрачных частей, COLOR_0 у GeometryPigment, основание Z=0,
центр в плане в начале координат, размеры в метрах, все три превью на категорию.

Фактический Blender — установленный 5.2.3 LTS; отдельный запуск в 4.x не проверялся.
При импорте в Unity проверить прозрачность, цвета вершин и Z-up (стандарт glTF — Y-up).
Эти 24 модели пока не добавлены в каталог закупок Unity.

| Модель | Треугольники | Папка |
| --- | ---: | --- |
'''+ '\n'.join(rows)+'\n'
(root/'Docs/FoodDairyProteinExpansion.md').write_text(text,encoding='utf-8')
print('PASS: 24 models, material/geometry checks and three previews per category.')
print('\n'.join(rows))
