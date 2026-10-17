# Молочный завод — концепт v1

Арт для согласования будущего объекта в Retail Empire Tycoon. Это не построенное здание и не инженерный проект; сцены, префабы и существующие здания не изменены. Место на карте ещё не выбрано.

Изображение: `DairyPlantPlan-v1.png`.

## Идея и зоны

1. PRODUCTION — центральный производственный корпус с верхними окнами, вентиляцией и видимыми через витрины ёмкостями.
2. MILK INTAKE — отдельная приёмка сырого молока: три силоса, короткие трубопроводы, навес подключения цистерны и свободная площадка.
3. COLD STORE — соединённый с производством холодильный склад с утеплёнными стенами и холодильными агрегатами на крыше.
4. DISPATCH — самостоятельный двор выдачи готовой продукции: ворота PICKUP, навес, короткий фургон, палеты вдоль стен, открытое пространство для заднего подъезда.
5. OFFICE & LANDSCAPE — небольшой офисный вход, парковка сотрудников, скамейки, фонари, озеленение и ограждение с раздельными въездами.

Главный принцип: приёмку сырья и выдачу товара разделить по двум дворам. Объёмные корпуса связаны в один завод; зелень размещена вне транспортных площадок. Общий вид и план сверху показывают одинаковую основную расстановку.

## План после согласования

Выбрать участок и проверить его размеры → разметить два въезда и пешеходные подходы → создать модульные корпуса и силосы → добавить производственное оборудование, навесы, ограду и озеленение → проверить габариты пикапа, опору и пересечения → снять настоящие кадры Unity → установить с резервной копией.

Реальные размеры, траектории поворотов, устройство производства и отдельные игровые механики требуют проверки при реализации. Нарисованные стрелки — идея движения, не подтверждение проходимости.

## Генерация и визуальная проверка

Использован встроенный image_gen по навыку imagegen, не CLI/API. Навык использован для растрового концепт-арта: английский промпт, английские надписи, общий перспективный вид и согласованный план сверху. Проверены наличие пяти зон, читаемость надписей, отдельные въезды, три силоса, сохранение свободных рабочих площадок и соответствие основных корпусов в двух видах. Существующие модели не использованы как цели редактирования и не изменены.

## Финальный англоязычный промпт

```text
Use case: stylized-concept.
Asset type: landscape concept art board for a low-poly Unity city tycoon game; proposed environment, not an implemented screenshot.
Primary request: Design a coherent, attractive, buildable medium-small dairy processing plant, visibly distinct from the existing retail warehouses.
Style: polished flat-shaded low-poly 3D game environment, simple faceted deciduous trees, chunky compact vehicles, cream plaster, light brick, restrained dairy-blue trim, charcoal roofs, light grey metal tanks. Match a modest European town with low-rise cream apartment blocks, roads with zebra crossings, grass and mixed pine/deciduous forest in background. Bright daylight, readable silhouettes, warm natural colors, no photorealism.
Composition: generous large elevated three-quarter FRONT view across the upper two-thirds of the landscape board. Lower left: clean true TOP-DOWN miniature of the SAME site and layout. Lower right: compact numbered English legend. Cream border, polished editorial presentation. The plant fills most of the main view; show every boundary, gate and vehicle circulation route, no cut-off roofs. Five discreet numbered markers identify the zones consistently in both views.
Architecture and function:
1 PRODUCTION: main one-storey industrial processing hall with slightly taller clerestory roof, blue-framed glazing with a glimpse of stainless processing equipment, roof vents and a modest front administrative entrance. Large front English sign "DAIRY". No smokestacks: it is a clean food factory, not a refinery.
2 MILK INTAKE: dedicated side raw-milk receiving yard with three upright cylindrical stainless milk silos, clearly modelled short connecting pipes, a roofed hose connection station and one modest milk tanker on its own unobstructed apron. Intake infrastructure sits beside production, away from public entrance.
3 COLD STORE: connected rear refrigerated warehouse, insulated cream walls, blue loading shutters, refrigeration plant on roof. Avoid a detached generic hangar. Give it a readable small "COLD STORE" sign.
4 DISPATCH: separate finished-goods loading court on the other side with one small orange delivery pickup or short van, a flush ground-level blue "PICKUP" shutter, canopy, protected staged dairy crates/pallets placed beside the wall, not in front of the shutter. Enough visibly open manoeuvring space for reversing and clear gate access. Pedestrian approach is distinct from industrial vehicle paths.
5 OFFICE & LANDSCAPE: modest front office annex/entrance, two or three staff parking spaces, sidewalk, benches, tidy low planting beds, trees outside working yards; simple metal perimeter fence with proper gate openings, lamps, bins, utility cabinet. Green buffer around rear and street edges; not dense plants between trucks and loading doors.
Operational layout: two clearly separate vehicle zones, raw-milk intake versus finished-product pickup, with separate driveway gates off the same existing street, connected halls in the middle. No gates across paths, no wheels floating, no pallets intersecting walls, no pipes through traffic lanes. Top-down layout MUST match the main illustration; do not invent a second facility.
Text verbatim, English only: heading "DAIRY PLANT — CONCEPT PLAN"; main building "DAIRY"; receiving station "MILK INTAKE"; warehouse "COLD STORE"; goods shutter "PICKUP". Legend "1 PRODUCTION", "2 MILK INTAKE", "3 COLD STORE", "4 DISPATCH", "5 OFFICE & LANDSCAPE". No other text, no real brand names, no Cyrillic. Labels should be legible, minimal and aligned. Show routes as thin dashed arrows ONLY in the top-down inset, blue for intake and warm orange for dispatch.
Avoid: giant factory footprint, smoke, refinery towers, sprawling pipe forests, sci-fi forms, enormous articulated trucks, crowds, fountain, extra detached stores, illegible labels, chaotic prop scattering, editor UI, watermark. This is a game environment concept, not a certified engineering or food-safety blueprint.
```

