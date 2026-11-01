"""Write delivery facts from the actual placement plan, Blender manifest and accepted Unity receipts."""
import csv,json,re
from collections import Counter
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
docs=ROOT/'Docs/Pinebrook'
plan=json.loads((ROOT/'Assets/Art/Pinebrook/Layout/PinebrookLayout.json').read_text(encoding='utf-8'))
models=json.loads((ROOT/'ArtSource/Pinebrook/DistrictModels/Models.json').read_text(encoding='utf-8'))['models']
models += [dict(name='Pond',kind='prop',triangles=96),dict(name='ParkingPocket',kind='prop',triangles=10)]
placements=Counter(p['prefab'] for p in plan['placements'])
groups=Counter(p['group'] for p in plan['placements'])
triangles=sum(placements[m['name']]*m['triangles'] for m in models if m['kind']=='building')
receipt=json.loads((ROOT/'Library/PinebrookDistrictPipeline/applied.json').read_text(encoding='utf-8-sig'))
qa=docs/'Final/QA'
for stage in ('result','review','play'):
    if not (qa/f'{stage}.txt').read_text(encoding='utf-8-sig').startswith('PASS:'):raise RuntimeError(f'{stage} was not accepted')
    if (qa/f'{stage}-prefab.sha256').read_text(encoding='utf-8-sig').strip()!=receipt['prefab']:raise RuntimeError('Stale delivery receipt')
summary=dict(name='PINEBROOK',revision=plan['version'],buildings=38,retained=13,new=25,groups=dict(groups),walks=len(plan['paths']),street_segments=len(plan['streets']),map_rectangles=len(plan['roads'])+1,parking_lots=len(plan.get('parking',[])),parking_spaces=sum(p['stalls'] for p in plan.get('parking',[])),parked_cars=sum(len(p['occupied']) for p in plan.get('parking',[])),building_lod0_triangles=triangles,accepted_prefab_sha256=receipt['prefab'],backup=receipt['backup'])
(docs/'Summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
with (docs/'Models.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.writer(f);writer.writerow(['Model','LOD0 triangles','LOD1 triangles','Placed','Prefab'])
    for m in models:writer.writerow([m['name'],m['triangles'],m.get('lod1_triangles',''),placements[m['name']],'Assets/Art/Pinebrook/Prefabs/'+m['name']+'.prefab'])
previous=docs/'README.md'
if not (docs/'InitialDelivery.md').exists():
    (docs/'InitialDelivery.md').write_text('> Исторический отчёт первой реализации. Текущий результат описан в README.md.\n\n'+previous.read_text(encoding='utf-8'),encoding='utf-8')
report=f'''# PINEBROOK — дороги, дорожки, парковки и редкий трафик

Текущая редакция по замечаниям Артёма от 1 ноября 2026 года. Эталон общей композиции: [утверждённый вид сверху](ApprovedPlan.png). Первая реализация сохранена в историческом отчёте InitialDelivery.md.

## Исправления дорог, дорожек и парковок

- Убрана лишняя пятая ветка перекрёстка: диагональ приходит в отдельное Т-образное примыкание. Переходы вынесены за зоны поворотов; их прямоугольники проверяются на взаимные пересечения. Закругления бордюров, переходы и знаки рассчитываются из общей геометрии перекрёстка.
- Въезд перед районом закрыт непрерывной асфальтовой поверхностью на всю ширину. Проверка проезда начинается до этого стыка.
- Старые копии парковых дорожек и полосы от прежних садиков удалены из объединённых грунтовых сеток. Все новые парковые соединения выполнены единой поверхностью и связаны по концам.
- Вместо семи двухместных площадок — {summary['parking_lots']} парковок с въездами через тротуар, проездом 6,2 м и местами 2,8 × 5,6 м. Всего {summary['parking_spaces']} место: {summary['parked_cars']} занято и {summary['parking_spaces']-summary['parked_cars']} свободно; одна площадка полностью свободна. Ряды имеют пешеходные обходы. Эти стоянки служат окружением района; симуляция поиска места жителями в этом обновлении не добавлялась.
- В существующем общем пуле поддерживаются две фоновые машины района, даже когда наблюдатель находится в старом городе. При приближении игрока местная численность может вырасти до четырёх. Новые местные появления разделены интервалом 8 секунд; городской предел 29 машин сохранён. Въезжающие машины учитываются в местной квоте до пересечения её границы.

{(qa/'roads-and-parking.txt').read_text(encoding='utf-8-sig').strip()}

{(qa/'population.txt').read_text(encoding='utf-8-sig').strip()}

Крупные виды: Final/RoadEntry.png, Final/WestJunction.png, Final/EastJunction.png, Final/PathJoin.png, Final/Parking.png, Final/RetailParking.png.

## Что исправлено

1. Асфальт использует точную палитру, шейдер и настройки существующего городского подъезда. Материал фасадов больше не используется для дорожного покрытия.
2. Равномерная сетка заменена композицией из концепта: один въезд через прежний переезд, развилка с зелёным островком, три жилых квартала сзади, отдельный двор слева спереди, кафе и рынок у центральной улицы, большой парк справа и изгибающаяся полоса таунхаусов у леса слева. Сохранены модели прежних 13 зданий; их положения изменены намеренно по новому плану.
3. Новые фасады пересобраны в Blender: повторяющиеся балконы на верхних этажах, частые балясины и боковые ограждения, отдельные деревянные рейки, выступающие оконные рамы, простеночные швы и межэтажные пояса. Мягкие фаски и общий атлас сохранены. Переход на упрощённый LOD отодвинут, чтобы детали не исчезали слишком близко к камере.
4. Добавлено покрытие земли 760 × 602 м и плотные нерегулярные посадки с кустарниками по краям. Использованы существующие виды деревьев и городская растительная палитра. Полоса переезда сохранена свободной.
5. Оставлена одна въездная табличка PINEBROOK. Названия улиц и небольшие надписи на остановках служат отдельной навигацией.
6. Знаки создаются от вектора подъезда: на правой обочине своей полосы, лицом к приближающейся машине. Направление не задаётся подбором угла вручную.
7. Две остановки открыты к своей стороне дороги. Парковочные карманы сдвинуты от проезжей части; фонари проверены на пересечения с машинами.

## Данные и результат

| Показатель | Значение |
| --- | ---: |
| Архитектурных объектов | 38: 13 сохранённых моделей + 25 новых |
| Линий улиц в новой планировке | {len(plan['streets'])} |
| Участков пешеходных дорожек | {len(plan['paths'])} |
| Предметов окружения | {groups['Props']} |
| Записей озеленения | {groups['Greenery']} |
| Новые здания в расстановке, LOD0 | {triangles:,} треугольников |
| Железнодорожных переездов | 1, существующий |

Полная модельная ведомость: Models.csv. Новая детализация увеличила число полигонов относительно первой версии; это реальная геометрия, а не попытка скрыть плоский фасад текстурой. Общие материалы, GPU instancing, статические объекты и LOD сохранены. Отдельный замер частоты кадров на слабом компьютере не проводился.

## Как устроено

Blender строит модели в метрах → экспортирует FBX → Unity сохраняет нативные Mesh.asset и Prefab → JSON задаёт здания, дорожки, улицы, зелень и положения сохранённых домов → один генератор применяет район к общему TownEnvironment.prefab, используемому City и Game.

Одна и та же линия улицы задаёт покрытие, обе правосторонние полосы, пешеходные стороны и положение знаков. Поверхности объединяются геометрическим вычитанием: у диагоналей ровные края, пересечения не накладываются плоскостями друг на друга. Старые копии дорожек из прежней системы ремонта пола удалены только внутри перепланируемого района. Дорожный граф сохраняет индексы существующих городских светофоров и подъезд к переезду; старые местные петли заменены новыми связями.

## Проверки

Модели зданий из предыдущей редакции сохранены. В этой редакции пересобраны покрытия, разметка, расстановка и маршруты. В Unity выполнены создание результата, повторное открытие и Play Mode. Результаты привязаны к контрольной сумме префаба и всех собственных активов, поэтому устаревшие проверки не допускаются при переносе.

- { (qa/'metrics.txt').read_text(encoding='utf-8-sig').strip() }
- { (qa/'traffic-clearance.txt').read_text(encoding='utf-8-sig').strip() }
- { (qa/'drive.txt').read_text(encoding='utf-8-sig').strip() }
- Проверены две сцены, совпадение геометрии с JSON, отсутствие пересечений новых зданий, цветовой материал дороги, одна въездная табличка и направление остановок.
- Движение существующих машин и пешеходов проверено в Play Mode; второй контроллер населения не добавлен. Сохранение игрока не изменилось.
- За пределами разрешённой зоны геометрия, материалы и коллизии не изменились; поставщики, существующие светофоры и переезд сохранены.

Новые здания остаются внешним окружением, без игровых интерьеров. Архивные снимки не являются текущим результатом; актуальные — в Final.

## Файлы

- План: Assets/Art/Pinebrook/Layout/PinebrookLayout.json.
- Префабы и сетки: Assets/Art/Pinebrook.
- Blender: ArtSource/Pinebrook/DistrictModels/PinebrookDistrictModels.blend.
- Общая геометрия: Scripts/Blender/PinebrookGeometry.py.
- Моделирование: Scripts/Blender/BuildPinebrookDistrictModels.py.
- Расстановка: Scripts/PinebrookConceptPlan.py, входная команда Scripts/PreparePinebrookLayout.py.
- Поверхности и движение: PinebrookSurfaceUnion.cs, PinebrookMobility.cs, PinebrookDistrictPlacement.cs.
- Актуальные снимки Unity: Final/Overview.png, Top.png, NewResidential.png, BusStops.png, Entrance.png, BrooksidePark.png.
- Полные результаты: Final/QA.

Повторная сборка из Unity: остановить Play Mode и выбрать Tools → Pinebrook → Build District. Инструмент использует проверочную копию проекта, проверяет результат, затем переносит его с резервной копией. Если основной префаб был изменён за время проверки, перенос отменяется.

Резервная копия перед текущим переносом: `{receipt['backup']}`.

Принятый SHA256: `{receipt['prefab']}`. Коммиты и отправка в GitHub не выполнялись.
'''
previous.write_text(report,encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False,indent=2))
