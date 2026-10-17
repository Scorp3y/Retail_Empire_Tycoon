# Концепт развития района пекарни — версия 1

Арт для обсуждения, а не изменённая сцена Unity. Основан на скриншоте Артёма `codex-clipboard-c5fd96d4-f888-41f2-ba25-fb17de3c8efb.png`. Пекарня остаётся главным ориентиром; новые здания ниже и меньше неё. Геометрия, дороги и существующие надписи в проекте не менялись.

## Зоны на рисунке

1. Слева — одноэтажная кофейня COFFEE с небольшой террасой и деревянной перголой. Дополняет пекарню и создаёт причину пользоваться площадью.
2. Между кофейней и пекарней — сквер: газонные островки, взрослые городские деревья, цветники, дорожки и скамейки. Подход ко входу пекарни остаётся открытым.
3. В правой общественной части — небольшой цветочный киоск FLOWERS с горшками и стойками, вне ограды служебного двора.
4. Остальная правая часть — сад отдыха: деревья, клумбы, скамейки и парковка велосипедов возле публичного тротуара.
5. Непосредственно справа от пекарни — сохранённый свободный служебный проезд. Никаких клумб, столиков, фонарей или построек на въезде и маршруте выдачи.

Фонари, урны и низкие живые изгороди связывают обе стороны площади. Новая автобусная остановка, крупный склад и высокие жилые корпуса не предлагаются.

## Последовательность после согласования

1. Проверить реальные границы участка, входы, заднюю выдачу и траекторию пикапа. Концепт не заменяет проверку точных размеров в Unity.
2. Разметить общественные зелёные зоны, дорожки и открытые подходы.
3. Создать и разместить два небольших здания — кофейню и цветочный киоск — с английскими промптами и вывесками.
4. Добавить террасу, посадки и мебель с контролем масштаба и пересечений.
5. Проверить игровой проезд и выдачу, снять нативные кадры, установить изменения только после проверки и резервной копии.

## Артефакты и генерация

- Финальное изображение: `BakeryQuarterPlan-v1.png`.
- Использован встроенный инструмент image_gen по навыку imagegen, не CLI/API.
- Проверены узнаваемость пекарни, согласованность основного вида и плана сверху, правильность номеров зон и свободный проезд. Это художественная визуализация, не подтверждение размеров или физической проходимости.

### Англоязычный промпт концепта

Use case: stylized-concept.
Asset type: game environment planning concept board, not an implemented Unity screenshot.
Input image 1 is the exact existing bakery district and composition reference. Create a polished landscape low-poly 3D game concept board based on this actual location, showing how its empty paved areas could become a cohesive small neighbourhood plaza.
Composition: one large three-quarter elevated street view occupying the upper 75 percent, plus a clearly separated small top-down layout inset and a compact numbered key below. Use a clean cream presentation margin, readable callouts, daylight, simple flat-shaded low-poly shapes and colors matching the provided game, not photorealism. Show enough elevation to understand both side areas, but keep the bakery centrally recognisable facing the foreground road. This is a concrete plausible future layout, not a different generic town.
MUST PRESERVE: the existing two-storey cream bakery at the centre, orange-red hip roof and chimney, original facade windows, burgundy awnings, existing facade lettering unchanged, and the fence with brick posts along its right-hand service driveway. Preserve every existing road, crossing, street edge, neighbouring apartment/building position and background forest. Do not enlarge or replace the bakery. Remove editor UI only.
Proposed additions, consistent in the main view and top-down inset:
1. A modest one-storey coffee house in the far LEFT part of the empty paved parcel, clearly smaller than the bakery, warm pale brick, muted dark green roof, English sign "COFFEE". A wooden pergola terrace with four small tables, umbrellas and chairs facing the central plaza.
2. A compact landscaped pocket square BETWEEN the coffee house and the bakery: several sizeable deciduous shade trees in planted lawn islands, flower beds, gently connected pedestrian paths, four benches, elegant modest town lamps, litter bins, low hedges. Leave an open generous pedestrian arrival space directly in front of the bakery, do not cover its facade with trees.
3. A small wooden flower kiosk with English sign "FLOWERS", flower pots and open display racks in the FAR RIGHT public area, well outside the service-yard fence, smaller than both main buildings.
4. A relaxed public garden in the remaining RIGHT area with shade trees, benches, low flower beds and a tiny paved seating plaza. Integrate a bicycle rack near the public sidewalk, never across an entrance.
5. The existing fenced service access immediately to the RIGHT of the bakery stays completely unobstructed, a clearly visible wide dark-grey vehicle lane from the front street to the rear yard. No tables, plants, poles or new structures in this lane or at its entrance. No new bus shelter, warehouse or oversized apartment block.
Graphic text outside the scene in English only: heading "BAKERY QUARTER — CONCEPT PLAN"; numbered key "1 COFFEE & TERRACE", "2 POCKET SQUARE", "3 FLOWER KIOSK", "4 GARDEN & BIKES", "5 CLEAR SERVICE ACCESS". Small numbered circular markers with fine leader lines pointing to these zones, not huge blocks over the scene. Top-down inset has the SAME coffee-left/bakery-centre/service-lane-right/kiosk-far-right spatial arrangement, and uses a dashed blue route only for the service lane. No other text. All NEW building signs are English; keep the existing bakery lettering as provided.
Intent: make the district lively and less empty through two small complementary buildings, coherent greenery, pedestrian paths and functional street furniture, while the existing bakery remains the principal visual landmark. Clear modular construction potential. No people crowds, no giant fountain, no roads cut off, no ornamental clutter, no impossible intersecting objects, no watermark.

### Финальная корректировка указателей

Use case: precise-object-edit. Input image 1 is the finished bakery quarter concept board. Make ONE tiny graphic correction only: in the LARGE UPPER VIEW, swap the numbers inside the two right-hand circular callouts currently labelled 3 and 4, so that number 3 points to the FLOWERS kiosk roof and number 4 points to the public garden behind/right of the kiosk. Keep the leader lines attached to their same physical targets. The lower top-down inset and numbered legend are already correct; DO NOT alter them. Preserve every building, tree, road, sidewalk, service lane, sign, texture, camera, margin, heading, composition and all other callouts exactly. No additions or removals. English text remains unchanged.

