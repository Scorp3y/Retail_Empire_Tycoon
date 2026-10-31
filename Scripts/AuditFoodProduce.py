"""Audit exported whole-food GLBs independently of Blender's in-memory meshes."""
from pathlib import Path
import json
import struct

root=Path(__file__).resolve().parents[1]
folders=['Food_01A_RootsSquash','Food_01B_GreensVegetables','Food_01C_TropicalFruit','Food_01D_SmallFruitBerries']
rows=[]
for folder in folders:
    directory=root/'ArtSource'/folder
    report=json.loads((directory/'Validation.json').read_text())
    assert len(report['models'])==6
    for model in report['models']:
        key=model['name'];blob=(directory/(key+'.glb')).read_bytes()
        assert blob[:4]==b'glTF'
        length=struct.unpack_from('<I',blob,12)[0];doc=json.loads(blob[20:20+length])
        assert len(doc['meshes'])==len(doc['nodes'])==1
        assert not doc.get('images') and not doc.get('textures')
        prims=doc['meshes'][0]['primitives']
        triangles=sum(doc['accessors'][p['indices']]['count']//3 for p in prims)
        assert triangles==model['triangles'] and triangles<model['budget_exclusive']
        assert 2<=len(doc['materials'])<=4
        for mat in doc['materials']:
            pbr=mat['pbrMetallicRoughness'];assert pbr['metallicFactor']==0
            assert abs(pbr['roughnessFactor']-(.2 if mat.get('alphaMode')=='BLEND' else .82))<1e-6
        accessors=[doc['accessors'][p['attributes']['POSITION']] for p in prims]
        low=[min(a['min'][i] for a in accessors) for i in range(3)]
        high=[max(a['max'][i] for a in accessors) for i in range(3)]
        assert abs(low[2])<1e-7 and abs(low[0]+high[0])<1e-7 and abs(low[1]+high[1])<1e-7
        dimensions=[b-a for a,b in zip(low,high)]
        assert all(d>0 for d in dimensions) and .02<max(dimensions)<.4
        assert all(abs(a-b)<1e-6 for a,b in zip(dimensions,model['size_m']))
        if key in ('gooseberries','cranberries','redcurrants'):
            assert any(m.get('alphaMode')=='BLEND' for m in doc['materials'])
        for iteration in ('01','02'):
            assert (directory/'Iterations'/iteration/'Preview.png').is_file()
        rows.append(f'| {key} | {triangles} | `{folder}/{key}.glb` |')
text='''# Food Expansion — первая часть

24 целых продукта; разрезы, половинки и очищенные образцы исключены по подтверждённому правилу пользователя.
Экспорт: один mesh/node на GLB, Z-up, метры, основание на Z=0, центр в плане в начале координат.
Каждая категория содержит самодостаточный Python-скрипт, Models.blend с редактируемыми частями,
финальный Preview.png, Validation.json и два прохода в Iterations/01 и Iterations/02.

Применены прежние помощники геометрии, палитра с дополнительными тонами продуктов, фаски с двумя сегментами,
матовые материалы без текстур. Пластик — alpha 0.25, roughness 0.2. Содержимое и тара раздельны
в редактируемом исходнике и объединены в одну экспортную сетку с отдельными материалами.

Blender в этой среде: 5.2.3 LTS. Скрипты используют совместимые с 4.x API; фактический запуск
в Blender 4.x не проверялся. Проверять прозрачность в целевом движке: GLB экспортирован как Z-up,
поэтому импортёр со стандартным glTF Y-up может потребовать изменения ориентации.
Эта задача создаёт исходники и экспорты; новые 24 продукта ещё не добавлены в закупку Unity.

| Модель | Треугольники | Путь относительно ArtSource |
| --- | ---: | --- |
'''+ '\n'.join(rows)+'\n'
(root/'Docs/FoodProduceExpansion.md').write_text(text,encoding='utf-8')
print('PASS: 24 GLBs, triangle budgets, materials, origins, scale bounds, transparency and both previews.')
print('\n'.join(rows))
