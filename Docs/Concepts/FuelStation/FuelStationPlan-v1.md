# Заправка — концепт обновления v1

Дата: 15 октября 2026. Предложение для согласования на основе двух пользовательских скриншотов. Это художественный план, не построенная сцена и не инженерный проект АЗС. Существующие сцены, здания и игровые механики не изменены.

Финальное изображение: `FuelStationPlan-v1.png`.

## Основная идея

Сохранить узнаваемый небольшой городской объект с кремовым магазином, бирюзовым навесом и красными колонками, но сделать территорию цельной. Вместо огромной разветвлённой площадки — компактная зона заправки с двумя островками, магазин с кофе, отдельная парковка и зелёные буферы. Не превращать заправку в грузовой терминал и не занимать берег реки или соседние кварталы.

## Шесть зон на арте

1. **FUEL COURT** — простой поддержанный колоннами навес, два островка и четыре топливораздаточные колонки, защитные столбики и свободные проезды для легковых машин и небольшого пикапа.
2. **SHOP & COFFEE** — небольшой магазин с прозрачным входом, полками и кофейной стойкой; два столика сбоку, вне зоны заправки и манёвров. Посадочные места отделены пешеходным мощением и низкими посадками.
3. **PARKING** — три парковочных места возле магазина: два заняты и одно свободно. Подход ко входу не проходит между топливными колонками.
4. **CAR CARE** — небольшой пост AIR & WATER с катушками шлангов и кратковременным местом остановки, урнами и техническим шкафом. Отдельная автомойка или ремонтный гараж в этот вариант не входят.
5. **GREEN BUFFER** — газонные островки, деревья, низкие кусты, цветники, бордюры и светильники. Посадки особенно важны со стороны жилых домов, но не должны закрывать обзор на выездах или занимать габарит проезда.
6. **ACCESS & PYLON** — раздельные въезд и выезд с существующей улицы, бирюзовый указатель FUEL. В плане предлагается понятное одностороннее движение через площадку. Цены на топливо не придумываются.

Публичные улицы, переходы, соседние дома, река и мост остаются ориентирами исходного места. Точные координаты и отступы пока не рассчитаны. Озеленение здесь формирует границы; высокий складской забор вокруг публичной заправки не предлагается.

## План работ после согласования

1. **ACCESS & LAYOUT.** Проверить реальные границы участка, существующие подключения к дороге и тротуарам; разметить въезд, выезд, топливные островки, очередь, парковку и пешеходный подход. Проверить, что будущие территории игрока или действующие механизмы не затрагиваются.
2. **SHOP & CANOPY.** Создать/доработать магазин и навес, сделать поддержанные конструкции, окна и английские вывески. Существующие надписи не менять автоматически до согласования замены модели.
3. **EQUIPMENT & PLANTING.** Добавить колонки, шланги, защитные столбики, пост воздуха/воды, указатель, парковку, мебель, освещение и посадки. Не ставить декор в дверях, на поворотах и в зоне остановки машин.
4. **DRIVE TESTS.** Проверить физическую опору, коллизии, вход магазина, обзор, проезды и фактическое движение пикапа в Unity. Снять нативные кадры, сохранить резервную копию, только затем устанавливать результат.

Стрелки на концепте иллюстративные: они не подтверждают проходимость, реальные радиусы поворота, дорожные ограничения или пожарную безопасность. Новую заправочную экономику, расход топлива, оплату, персонал и транспортный AI этот арт не определяет. Перед реализацией такие механики согласовываются отдельно.

После уточнения на основном виде показаны четыре колонки. В малом плане остаются графические условности: встречные стрелки на нижнем внутреннем участке и дополнительная машина в зоне воздуха/воды. Их не следует переносить в игровую сцену как техническое задание: целевой вариант предусматривает односторонний маршрут и один карман AIR & WATER. При реализации разметка строится заново по физическим габаритам.

## Генерация и проверка

Использован встроенный image_gen по навыку imagegen, без CLI/API. Два скриншота переданы как референсы текущего участка и внешнего вида станции, а не как изменяемые игровые файлы. Арт содержит общий перспективный вид, план сверху, шесть функциональных зон и четыре этапа работы. Выполнено отдельное уточнение количества колонок и направления движения, чтобы уменьшить неоднозначность общего вида и схемы. Сцена не изменялась и физические испытания не запускались.

## Финальный англоязычный набор промптов

### Основной концепт

```text
Use case: stylized-concept.
Asset type: a landscape environment redesign concept and work-plan art board for a low-poly Unity retail tycoon game, not an implemented scene or engineering blueprint.
Input images: Image 1 is the exact existing gas-station urban block and surrounding-road/river/apartment context reference; Image 2 is the close-up reference of its small cream shop, teal pump canopy, red fuel pumps and teal price pylon. Both are references, not edit targets.
Primary request: Redesign and upgrade this existing tiny gas station into an attractive coherent neighbourhood filling station on its present site, replacing the enormous empty branching asphalt pad with a compact purposeful forecourt and generous planted buffers.

Style: genuinely flat-shaded low-poly 3D game concept, simple faceted trees and chunky modest cars, matching the supplied town rather than photorealistic architecture. Keep the cream walls, muted teal canopy/pylon identity, small red accents, dark asphalt and pale pavement. No real oil-company brand, no futuristic megastation.
Composition: landscape cream-bordered board. Upper 65 percent: one large elevated three-quarter overview clearly showing the station, its two road connections and nearby urban landmarks. Lower left: a clean TOP-DOWN view of exactly the same site and proposed layout. Lower right: concise numbered six-zone legend. Along bottom: four-phase work strip. Filling station occupies most of the main illustration; enough context to recognize its existing block. No editor UI or watermark.

Context invariants: maintain the existing street grid, road widths, crossings, surrounding cream/red apartment blocks and distant river/bridge in their original relative positions. Do not build in the riverbank, move houses, add a road through the block, or turn the station into a giant truck stop. Use only the existing parcel. The large reference asphalt arms become clearly defined shorter driveways and planted islands rather than more parking lots.
Proposed layout:
1 FUEL COURT: a slightly enlarged simple flat TEAL canopy with slim supported columns and a restrained red fascia line; two parallel raised pump islands, two red-and-cream pumps per island, four pumps TOTAL, clear vehicle lanes and protective bollards. Grounded pumps, no columns in driving lanes, no suspended hoses across lanes. Ordinary small cars / compact pickups, no semitrailers.
2 SHOP & COFFEE: compact single-storey cream convenience shop near its current rear-of-forecourt location, teal trim, clear glass entrance, visible shelves and a small coffee counter through windows, readable English "SHOP & COFFEE" fascia. Small shaded two-table seating area directly beside the shop, physically separated from pumps and manoeuvring space by a pedestrian strip and low planting. Do not place tables among pump islands.
3 PARKING: three customer parking spaces beside the shop rather than in the fuel queue, two parked compact cars and one empty space. Clear pedestrian approach from the public sidewalk to the shop.
4 CAR CARE: small freestanding air-and-water point with hose reels and one short-stop space at the side of the forecourt, bins and a modest utility cabinet. No carwash building, no auto repair garage, no giant new commercial feature.
5 GREEN BUFFER: planted perimeter islands with mature faceted shade trees, low shrubs, flowers and clipped hedges, tidy curbs and pedestrian paving. Turn unused asphalt into grass without stealing pump or gate clearances. More greenery near the apartment side; keep sightlines at street corners and driveway exits open. Do not surround this public station with a warehouse-style tall security fence.
6 ACCESS & PYLON: two sensible existing-road connections as distinct entry and exit, simple teal freestanding "FUEL" pylon by the entry, no fake currency or prices. Short logical circulation through the pumps and back to the same adjoining public road. No exit across a zebra crossing, planter obstructing a turning lane, parked car across the shop door, or asphalt extending into the public roadway. Keep underground fuel service access as a subtle unoccupied utility pocket, not above-ground industrial tanks.

Map/main consistency: same canopy position, exactly four pumps on two islands, same shop, three parking bays, air/water corner, planted buffers and access points in both views. The small numbered callouts attach to their actual respective zones. Show simple dashed amber one-way vehicle-flow arrows ONLY in the top-down map, and a distinct thin green walking line to the shop that does not run through pump islands. This is a planning suggestion, not a physically tested turning-radius claim.

English text only, legible and minimal:
Heading "FUEL STATION — UPGRADE PLAN".
Building/pylon signs exactly "SHOP & COFFEE", "FUEL", "AIR & WATER".
Key exactly "1 FUEL COURT", "2 SHOP & COFFEE", "3 PARKING", "4 CAR CARE", "5 GREEN BUFFER", "6 ACCESS & PYLON".
Bottom work strip exactly "01 ACCESS & LAYOUT  >  02 SHOP & CANOPY  >  03 EQUIPMENT & PLANTING  >  04 DRIVE TESTS".
No other text, no Cyrillic, no trademarks, no crowds, no smoke, no arbitrary extra buildings. This image is a proposed future redesign only; it must not imply changes to the actual game have already been made.
```

### Уточнение согласованности

```text
Use case: precise-object-edit.
Input image 1 is the finished FUEL STATION — UPGRADE PLAN concept board. Preserve its overall layout, architecture, landscaping, existing surrounding urban context, roads, river, typography, legend, four-phase strip and composition. Make only these small consistency corrections:
1. In the LARGE upper scene, show exactly FOUR individual red-and-cream fuel dispenser units total: TWO dispensers on each of the TWO long parallel raised islands under the canopy. Remove the extra middle dispenser on each island; retain canopy columns, curbs, hoses on the remaining pumps and vehicle clearances. Correct the small FUEL COURT thumbnail in the legend to match the same four-pump arrangement. Do not add a third island.
2. In the TOP-DOWN inset, make the dashed amber vehicle-flow route unambiguously clockwise: enter through the LEFT driveway beside the teal FUEL pylon, travel UP the left side of the canopy, RIGHT along the back lane in front of the shop, DOWN the right side, and leave through the RIGHT driveway. Remove any counter-direction arrows. Left driveway white arrow points only INWARD/up into the station; right driveway white arrow points only OUTWARD/down to the public road. Keep the distinct green pedestrian path separate.
3. In the LARGE upper scene, align driveway arrows consistently: left/pylon driveway inbound, right/car-care driveway outbound toward the adjoining public road.
4. The AIR & WATER corner has ONE short-stop bay with ONE grey car in the large scene. Make the lower inset show that same one-bay, one-car layout, not two cars. Keep the air-and-water equipment and landscaped boundary.
Everything else stays unchanged. Do not redesign the site, change the new building signs, add buildings, change the three shop parking spaces or alter any surrounding public street/crossing/house.
```

