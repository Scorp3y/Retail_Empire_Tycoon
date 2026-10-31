"""Validate and document the revised 24-product delivery using actual GLB data."""
from pathlib import Path
import json
import runpy

ROOT=Path(__file__).resolve().parents[1]
audit=runpy.run_path(str(ROOT/'Scripts/AuditFoodExpansion96.py'))
folders=['Food_09_CheeseDairy','Food_10_MeatSeafood','Food_11_Drinks','Food_12_Spices']
records=[]
for folder in folders:
    directory=ROOT/'ArtSource'/folder
    report=json.loads((directory/'Validation.json').read_text(encoding='utf-8'))
    assert report['iteration']==6,folder
    for revision in ('04','05','06'):
        for filename in ('Preview.png','Models.blend','Validation.json'):
            assert (directory/'Iterations'/revision/filename).is_file(),(folder,revision,filename)
    for model in report['models']:
        audit['audit_model'](directory/(model['name']+'.glb'),model)
        records.append(dict(name=model['name'],triangles=model['triangles'],folder=str(directory)))
assert len(records)==24
notes='''Готовы 24 отдельных GLB. В каждой папке: Models.blend с редактируемыми частями,
BuildFood*.py, Preview.png, Validation.json. Итерации 04, 05 и 06 сохраняют
первый рендер обновлённой версии и два прохода исправлений.

Общие формы лотка, банки специй, бутылки и пакета переиспользуются. Все детали
и значки — геометрия, без картинок и текстур. Прозрачные материалы: alpha 0.25,
roughness 0.2. Непрозрачные поверхности матовые. Экспорт — одна сетка/узел на продукт,
содержимое и упаковка раздельны в редактируемом исходнике. Все продукты целые;
Brie без вырезанного сектора. Несколько мидий естественно раскрыты в раковине.

Автоматически проверены бинарная геометрия, индексы, нормали, материалы,
бюджеты треугольников, масштаб в метрах, нижний центр на Z=0 и отсутствие текстур.
Фактически использован Blender 5.2.3 LTS; отдельный запуск в Blender 4.x не проверялся.

Вручную в Unity проверить прозрачность плёнки, видимость содержимого, ориентацию
Z-up и поддержку цветов вершин GeometryPigment при игровом освещении. Мелкие
сложные надписи заменены значками там, где текст превышал бюджет. В каталог
закупок и на игровые полки эта работа модели автоматически не добавляет.
'''
report_path=ROOT/'Docs/FoodPart3Revision.md'
report_path.write_text('# Food Expansion — обновлённая часть 3\n\n'+notes+'\n'+audit['table'](records)+'\n',encoding='utf-8')
print('PASS: revised part three, 24 models. Report: Docs/FoodPart3Revision.md')
