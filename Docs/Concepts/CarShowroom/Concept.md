# Car Showroom — концепт городского автосалона

Статус: концепт-арт, не реализованная модель. Место в городе пока не выбрано; размеры и ориентация уточняются по реальному участку. Генерация встроенным ImageGen, промпт на английском.

После этого концепта Артём указал участок на берегу и запросил реализацию. Нативный вариант описан в `Docs/TownShowroom.md`; исходный арт не является скриншотом игры. Архитектурный ориентир — `car-showroom-concept-v1.png`. В `pickup-orientation-v2.png` исправлено направление пикапа, но генератор убрал фасадную надпись: это изменение не принято, в модели остаётся `CAR SHOWROOM`.

Уточнение после проверки кода: существующий автосалон поддерживает только осмотр машин. Задний двор остаётся служебной декорацией; предложение о новой выдаче автотоваров из первоначального концепта не реализуется без отдельного игрового запроса.

## Принцип

Компактный одноэтажный автосалон с низкой служебной пристройкой: светло-серый корпус, графитовые рамы, небольшой кирпичный акцент и синяя вывеска `CAR SHOWROOM`. Большие витрины показывают автомобили, а не повторяют складской фасад прежнего поставщика.

Клиентский вход, парковка и выставочные машины — спереди. Отдельный боковой проезд ведёт в задний двор. Там находятся подготовка автомобилей и отдельная выдача автотоваров для пикапа. Выдача товаров и продажа машин — разные сценарии; этот арт сам по себе не добавляет новые игровые механики.

Озеленение группируется по краям и у входа. Проезд, площадка загрузки, пешеходный вход и обзор витрин остаются свободными. Декор: стеллаж с шинами, тележка с коробками, светильники, вазоны, выставочный подиум и ограждение. Без людей, остановки и случайного мусора.

## План реализации после согласования

1. Выбрать участок и проверить габариты, подъезд, соседние объекты и существующую выдачу автотоваров.
2. Сделать простую объёмную расстановку корпуса, служебной зоны, парковки и маршрута пикапа; проверить реальные коллайдеры.
3. Создать модули здания, витрины с выставочными машинами и отдельный набор декора.
4. Расставить озеленение и детали, не перекрывая транспортный и пешеходный маршруты.
5. Подключить существующую выдачу без дубликатов, проверить заезд, сохранность соседнего города и получить реальные кадры Unity.

## English generation prompt

```text
Use case: stylized-concept.
Asset type: multi-view game environment concept board for Retail Empire Tycoon, a Unity low-poly town.
Primary request: design a compact neighborhood CAR SHOWROOM with a beautiful public frontage and a functional rear service/auto-goods collection yard. This is a design proposal, not a screenshot of an implemented building.

Composition: one high-resolution wide landscape concept sheet, three clearly separated equally sized vertical panels: left FRONT, middle SERVICE COURT, right TOP VIEW. Each panel depicts the SAME building and SAME site from another angle, rigorously consistent roof footprint, driveway position, door locations, parked cars, landscaping and fences. Panel headers in simple dark navy bars with white readable uppercase English titles: "FRONT", "SERVICE COURT", "TOP VIEW". Generous view framing, no clipped building. No extra narrative text. The right panel is a true vertical overhead plan, not a second isometric view.

Architecture: a modest single-storey L-shaped car dealership, not a gigantic luxury megastore, not a warehouse or gas station. Main rectangular street-facing showroom with flat low roof and slim stepped parapet, warm light-gray plaster, a limited warm red-brown brick accent wall, dark graphite metal frames, muted cobalt-blue fascia. Large transparent glazed FRONT and one SIDE window reveal THREE distinct low-poly display sedans inside with simplified interior, sales desk, pale tiled floor. A well-defined pedestrian glass entrance under a small graphite canopy. Signage above glazing reads exactly "CAR SHOWROOM". A lower compact utilitarian service wing forms the rear of the L shape, two gray roller doors and one personnel door, no text banners above rear doors. Preserve distinct contemporary silhouette while fitting the existing simple residential town.

Site organization, consistent across every panel: street and cream concrete public sidewalk at FRONT; two customer parking spaces on one front edge, a single small outdoor vehicle display plinth on the other, pedestrian entrance path not blocked by cars. On the RIGHT of the showroom, a visibly generous unobstructed vehicle driveway, approximately six metres wide at concept scale, connects the front street opening directly to the rear court. Do not put decorative cars or planters in this driveway. Rear courtyard sized for an ordinary six-to-seven-metre pickup, not an articulated truck; one orange compact boxy pickup is shown aligned backwards toward an auto-goods roller door, nose toward the open side driveway, rear cargo bed facing that door. A discrete yellow rectangular loading outline under the pickup, no road arrows. Adjacent preparation bay has one ordinary green sedan. A small trolley with boxed car accessories, a low rack with four tires and two crates sits beside the wall, outside the driving corridor. No tire piles, no oily asphalt stains. Rear and side perimeter enclosed by low gray brick base and simple dark steel mesh fence, an obvious open access gap connects the driveway; don't fence off the showroom entrance.

Landscaping and detail: clustered trimmed shrubs, grass strips, two or three low-poly street-scale deciduous trees and simple rectangular planters along pedestrian and perimeter edges. Enough greenery to soften the site, without overwhelming vehicles or hiding windows. Two slender practical street lamps, a small freestanding dealership sign, clean parking lines and curb transitions, tasteful manageable detail density. No bus shelter, no people, no outdoor café, no flowers everywhere. Showcase modular components naturally: window bays, canopy, display plinth, fence sections, planters and accessory trolley.

Style: practical native-game-buildable low-poly 3D concept art, clearly faceted tree crowns, simple boxy sedans like the existing stylized Unity town, matte flat-color materials, restrained architectural bevels, readable geometry, gentle ambient occlusion, warm midday sunlight, consistent soft shadows, clear blue sky. Background shows a few simple four-to-five-storey cream and terracotta apartment buildings with blue window rectangles, standard gray asphalt roads, cream paving and low-poly trees. The dealership uses its own gray/brick/blue palette, NOT the identical cream-and-turquoise warehouse kit, NOT the bakery terracotta roof, NOT the florist green gable roof. No photorealism, no excessive reflective glass, no complex futuristic organic architecture, no branded cars, no real-world logos, no watermark. All signage English only. Avoid inconsistency between the top-down layout and perspective views.
```

## Pickup orientation correction prompt

```text
Use case: precise-object-edit. Input image: the attached three-view CAR SHOWROOM concept board is the edit target. Make ONE targeted correction only: rotate the orange pickup by 180 degrees about its vertical axis in every panel where it appears, without moving its bay location. The open cargo BED and rear TAILGATE must be closest to the rear roller garage door. The driver's CAB, windshield, hood and front headlights must point AWAY from the roller door, toward the courtyard and side driveway. In the middle SERVICE COURT panel, show the FRONT of the orange pickup toward the viewer and its open cargo bed on the far side next to the garage wall. In the right TOP VIEW panel, the orange pickup's dark open BED must be at the TOP next to the garage wall and its orange hood must be at the BOTTOM toward the courtyard. Apply the same physical orientation to the tiny pickup seen in the FRONT panel. Preserve absolutely everything else: same three panels and headers, building footprint, architecture, blue English CAR SHOWROOM fascia, rear two doors, all other cars, lighting, framing, glass, furnishings, trees, shrubs, fence, yellow bay rectangle, sidewalks, street, camera views and image quality. Do not redesign the building, do not add any text, people or arrows.
```
