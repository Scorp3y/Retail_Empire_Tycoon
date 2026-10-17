# Ферма — концепт пересборки v1

Дата: 15 октября 2026. Арт-предложение для согласования, не реализованная сцена и не подтверждённая транспортная схема. Unity-сцены, префабы, исходная ферма и её поставщик не изменены.

Финальное изображение: `FarmRebuildPlan-v1.png`.

## Что предлагается изменить

Вместо разрозненных построек и огромного пустого асфальтового двора — связное сельское хозяйство на том же участке. Сохраняются узнаваемые красный амбар, кремовый фермерский дом, зелёные крыши, три крупных поля, плодовый сад и парные силосы. Оранжевый городской склад предлагается заменить сельскохозяйственным корпусом упаковки с двускатной крышей и навесом. Асфальт оставить только там, где он нужен для машин; остальное занять гравийными дворами, дорожками, травой и осмысленными рабочими зонами.

## Семь зон

1. **FARMSTEAD** — дом и амбар с общим хозяйственным двором, сенником, небольшим огородом, воротами и деревом для тени.
2. **PASTURE** — пастбище с животными, укрытием, поилкой и кормушкой; небольшой обособленный курятник. Животные на арте обозначают будущие визуальные объекты, а не обещание готовой симуляции.
3. **FARM MARKET** — рынок возле въезда: крытая торговля, ящики продукции, три парковочных места, отдельный пешеходный подход и посадки. Посетители отделены от погрузочного двора.
4. **PACKING & PICKUP** — сельский упаковочный корпус с крытым сортировочным участком, выдачей и понятной площадкой для заднего подъезда пикапа. Палеты у стен, погрузчик в боковом кармане, ворота и двери свободны.
5. **GREENHOUSES** — две теплицы с плотной видимой растительностью, рабочими столами, поливом, бочками и компостными отсеками. Между ними остаются проходы.
6. **FIELDS & ORCHARD** — три разных поля, зрелый плодовый сад, технологические дорожки и места для собранного урожая. Посадки не должны перекрывать тракторные проезды.
7. **SILOS & WORKSHOP** — два узнаваемых силоса, открытый гараж для трактора, верстак, запасное колесо и сгруппированные запасы/технические шкафы. Прямая связь с полями и упаковкой.

## Этапы после согласования

1. **Участок и подъезды.** Снять реальные размеры, зафиксировать существующую точку выдачи и её ограничения, проверить дорожную связь, фермерские ворота, железную дорогу и границы соседних объектов. Разделить гостевой, хозяйственный и пешеходный потоки. Подготовить резервную копию.
2. **Здания.** Пересобрать дом/амбар и хозяйственные дворы, заменить оранжевый склад упаковочным корпусом, связать его с теплицами и силосами. Создавать новые модели с английскими заданиями и вывесками. Существующие надписи не менять автоматически.
3. **Посадки и детали.** Наполнить теплицы, разнообразить поля и сад, оформить пастбище и рынок. Добавить мебель, ограждения, сгруппированный инвентарь и освещение. Не заполнять проезды декоративными предметами.
4. **Проверка.** Контроль опоры и пересечений, открытых дверей и ворот, фактический проезд и задний подъезд пикапа, сохранность поставщика и остальной карты. Только после нативных снимков Unity и проверок устанавливать результат.

Нарисованные стрелки — художественная идея логистики, не рассчитанные траектории. Число объектов и точные размеры уточняются по сцене. Реализация животных, анимаций, производства, торговых интерфейсов или новой экономики требует отдельного определения объёма работ. Соседний молочный завод в эту пересборку не входит.

## Генерация и визуальная проверка

Использован встроенный image_gen по навыку imagegen, без CLI/API. Пять приложенных скриншотов переданы как визуальные референсы: общий вид, складской двор, силосы с гаражом, дом с амбаром, рынок. Общий вид также показывает поля; шестой пользовательский снимок полей изучен в сообщении. Проверены узнаваемость фермы, семь зон, согласованность главных построек в перспективе и плане сверху, заполненные теплицы, отделение посетителей от выдачи и четыре этапа работы. Это концепт, не проверка физики и не утверждение о завершённой реконструкции.

## Финальный набор англоязычных промптов

### Основная генерация

```text
Use case: stylized-concept.
Asset type: a landscape game-environment concept and rebuild planning board, not an implemented Unity screenshot.
Primary request: Thoughtfully redesign THIS existing farm into a cohesive, lived-in working farm for a low-poly city retail tycoon game. Preserve its identity and general site organization while substantially improving the emptiness and disconnected spaces.

Reference images, all references not edit targets:
1: existing whole farm, its single curved approach road from the foreground, orchard and pasture on the left, agricultural fields in the rear, farm buildings in the middle, machinery/silos on the right, railway along the far boundary.
2: oversized empty asphalt yard and orange urban-looking warehouse that should be REPLACED in the proposal by a much more compact agricultural packing-and-collection barn with pitched roof.
3: existing tractor garage and twin grain silos, retain and improve this recognisable machinery landmark.
4: existing red gambrel-roof barn with white door braces and cream farmhouse with teal roofs; retain their identity and arrange them as a connected farmstead.
5: striped-canopy produce market and two-car visitor parking, turn into an inviting separated visitor entrance area.

Style/medium: polished but genuinely flat-shaded LOW-POLY 3D concept, faceted tree crowns, simple chunky tractors and compact cars matching the references. Large simple buildable shapes with selective functional details; avoid photorealism or detailed miniature model-railway aesthetic. Cream plaster, red timber barn, muted teal-green metal roofs, weathered wood fencing, pale gravel and earth tracks, restrained asphalt only where vehicles need it. Bright daylight and lush but organized planting.

Composition: clean cream-margin landscape board with one dominant elevated three-quarter overall view occupying upper 65 percent. A coherent TOP-DOWN site plan of the SAME farm occupies lower left; a compact seven-item numbered key occupies lower right. A short four-phase work strip along bottom. Main view faces along the existing road into the farm so foreground entrance, midground buildings and background fields are clear. Keep the railway and forest outside the rear site boundary, do not move them into fields or block the approach road.

Site design, consistent in main view and top-down:
1 FARMSTEAD: modest red barn and cream farmhouse grouped together in the middle-left with connected pale gravel working courtyard, hay storage lean-to, a few hay bales, timber gates, small kitchen garden and shade tree. Farmhouse remains a residence, not an industrial office.
2 PASTURE: front-left enclosed green paddock with a few chunky low-poly cows, water trough, feeder, simple shelter and small separated chicken-coop corner; no animals in vehicle lanes. Fill this previously empty fenced area with readable purpose, not clutter.
3 FARM MARKET: inviting striped-canopy produce stall at the front near the approach road, three customer parking bays, fruit/vegetable crates, pedestrian path, benches, low flower planting and sign "FARM MARKET". Keep customers separated from the delivery yard by a fence and planting strip, with a real gate opening.
4 PACKING & PICKUP: replace the huge orange box and vast paved rectangle with a medium agricultural gabled packing shed, green roof, timber/cream walls, English "FARM PICKUP" sign, visible sorting tables and crate shelves under an attached roof. A properly sized open gravel-and-asphalt collection court, short orange delivery pickup REVERSING to its ground-level loading door, pallets beside walls not in front of doors, one small forklift parked in its own side pocket. Retain a clear continuous delivery route from the existing approach road. No tiny decorative turning circle that cannot fit the pickup.
5 GREENHOUSES: two transparent planted greenhouses near the middle-right with DENSE visible vegetables, potting tables, watering barrels, compost bays and irrigation detail. Clear pedestrian/tractor gaps between glass structures, no trees growing through glazing.
6 FIELDS & ORCHARD: three large crop plots remain in the background, with visibly varied wheat, leafy vegetables and root-crop beds, readable crop rows and broad connecting tractor tracks; a structured mature fruit orchard on the left behind the pasture, grass between rows, a few harvest crates. No random crop/forest mixture. Trees/hedges define edges and shade public areas without covering the fields.
7 SILOS & WORKSHOP: twin cream grain silos with ladders and top rails beside an open-front green-roof tractor shed on the right, one green tractor, tool bench, spare wheel, grouped fuel/service cabinets and an orderly grain/seed storage pocket. Connect this area directly to the packing yard and field tracks.

Overall: replace unused asphalt with meadows, purposeful small yards and connected farm tracks. Warm rural character rather than a supermarket logistics base. Continuous wood fencing around public/pasture edges and modest secure service boundary; entrances align with paths, gates never obstruct vehicle lanes. Ground all props. No floating tables, no barrels in doorways, no intersecting planters, no dead-end asphalt strips, no piles scattered at random. Existing remote dairy factory outside this site is NOT part of this farm project.

Text, English only, readable and minimal:
Heading "FARM — REBUILD CONCEPT".
Only building signs "FARM MARKET" and "FARM PICKUP".
Legend exactly "1 FARMSTEAD", "2 PASTURE", "3 FARM MARKET", "4 PACKING & PICKUP", "5 GREENHOUSES", "6 FIELDS & ORCHARD", "7 SILOS & WORKSHOP".
Work strip "01 SITE & ACCESS  >  02 BUILDINGS  >  03 PLANTING & DETAILS  >  04 DRIVE TESTS".
Seven small numbered circular callouts in the main view and corresponding numbers in the top-down map, accurately attached to the same zones. Inset only: dashed amber delivery path from the existing road to the collection bay, thin separate green pedestrian path to the market. No arrows across pasture fences or fields. No other text, no Cyrillic, no brand names, no watermark, no editor UI.
This is an artistic proposal, not a claim of engineering, vehicle-path validation or completed construction.
```

### Уточнение вывесок

```text
Use case: precise-object-edit.
Input image 1 is the completed FARM — REBUILD CONCEPT board. Make only a tiny English building-signage correction. In the large upper view, replace the small illegible signboard immediately to the right of the striped market canopy with a neat dark-green rectangular sign bearing exactly "FARM MARKET" in clean white uppercase lettering, legible at the scene scale. Add a modest dark-green fascia sign with white uppercase "FARM PICKUP" on the front upper wall of the central timber packing shed, just above its attached sorting canopy and below its main roof, not over the loading shutter. No other new lettering. Preserve all existing buildings, animals, crops, roads, fences, trees, vehicles, lighting, camera, board margins, numbered callouts and every detail of the lower plan and bottom work strip exactly. Do not change the layout or regenerate the farm. This is a concept illustration, not a Unity screenshot.
```

