"""Independent audit of exported GLBs for Food Expansion part two."""
from pathlib import Path
import json
import struct

root=Path(__file__).resolve().parents[1]
folders=['Food_05_Grains','Food_06_DriedLegumes','Food_07_PlantBasedSweet','Food_08_CookingEssentials']
rows=[]
for folder in folders:
    directory=root/'ArtSource'/folder
    validation=json.loads((directory/'Validation.json').read_text())
    assert len(validation['models'])==6
    for item in validation['models']:
        name=item['name'];path=directory/(name+'.glb');blob=path.read_bytes()
        assert blob[:4]==b'glTF'
        assert struct.unpack_from('<I',blob,8)[0]==len(blob)
        length=struct.unpack_from('<I',blob,12)[0];doc=json.loads(blob[20:20+length])
        assert len(doc['nodes'])==len(doc['meshes'])==1
        assert not any(k in doc['nodes'][0] for k in ('translation','rotation','scale','matrix'))
        assert not doc.get('textures') and not doc.get('images')
        primitives=doc['meshes'][0]['primitives']
        triangles=sum(doc['accessors'][p['indices']]['count']//3 for p in primitives)
        assert triangles==item['triangles'] and triangles<item['budget_exclusive']
        assert 2<=len(doc['materials'])<=4
        for material in doc['materials']:
            pbr=material['pbrMetallicRoughness'];assert pbr['metallicFactor']==0
            clear=material.get('alphaMode')=='BLEND'
            assert abs(pbr['roughnessFactor']-(.2 if clear else .82))<1e-6
            if clear:assert abs(pbr['baseColorFactor'][3]-.25)<1e-6
        positions=[doc['accessors'][p['attributes']['POSITION']] for p in primitives]
        low=[min(a['min'][i] for a in positions) for i in range(3)]
        high=[max(a['max'][i] for a in positions) for i in range(3)]
        size=[b-a for a,b in zip(low,high)]
        assert abs(low[2])<1e-7 and abs(low[0]+high[0])<1e-7 and abs(low[1]+high[1])<1e-7
        assert .07<max(size)<.30 and all(abs(a-b)<1e-6 for a,b in zip(size,item['size_m']))
        if name in ('maple_syrup','molasses','apple_cider_vinegar','balsamic_vinegar'):
            assert any('COLOR_0' in p['attributes'] for p in primitives),'Missing geometry ink colours'
            assert any(doc['materials'][p['material']]['name']=='GeometryPigment'
                       for p in primitives if 'COLOR_0' in p['attributes']),'Unity ink material contract'
        for iteration in ('01','02','03'):
            assert (directory/'Iterations'/iteration/'Preview.png').is_file()
        rows.append(f'| {name} | {triangles} | [{folder}](<{directory.as_posix()}>) |')
text='''# Food Expansion — часть 2

24 отдельных GLB, 4 исходника Models.blend с редактируемыми частями, 4 самодостаточных
скрипта Blender, финальные превью и три визуальных прохода для каждой категории.
В папках Iterations сохранены первая версия и два прохода исправлений.

Использованы прежние функции материалов, фасок (2 сегмента), геометрии и экспорта.
Основная палитра сохранена, дополнительные оттенки заданы в PALETTE. Нет текстур;
2–4 материала на модель. Крышки и значки бутылок используют один материал
с цветами вершин: это цвета самой сетки, без картинок, с экспортом COLOR_0.

Бумажный пакет и дой-пак — две общие переиспользуемые формы, с отдельными
прозрачными окнами и реальными отверстиями в бумаге. Стенки сосудов имеют толщину;
содержимое отделено от тары в исходнике. Полость проверяется лучом до внутреннего дна.
Экспортная копия объединена в одну сетку с отдельными материалами.

Все продукты целые, кокосовые значки тоже. Хумус показан с открытой крышкой,
согласно прямому требованию этой части; пища не разрезана.

Проверки GLB: бюджет треугольников, один mesh/node, отсутствие текстур, 2–4 материала,
alpha 0.25 / roughness 0.2 у прозрачных частей, roughness 0.82 у остальных,
центр основания в начале координат, Z=0 снизу, Z-up, реальные размеры в метрах,
экспорт цветов вершин бутылок и наличие всех трёх превью.

Фактический Blender — установленный 5.2.3 LTS. Код использует API семейства 4.x,
но запуск в отдельной установке 4.x не проверялся. В целевом движке нужно проверить
прозрачность окон, поддержку COLOR_0 и ориентацию нестандартного для glTF Z-up.
Добавление этих 24 товаров в каталог закупок Unity не входит в текущую сборку моделей.

| Модель | Треугольники | Папка |
| --- | ---: | --- |
'''+ '\n'.join(rows)+'\n'
(root/'Docs/FoodPantryExpansion.md').write_text(text,encoding='utf-8')
print('PASS: all 24 pantry exports and three iterations per category.')
print('\n'.join(rows))
