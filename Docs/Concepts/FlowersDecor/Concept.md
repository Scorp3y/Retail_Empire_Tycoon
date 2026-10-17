# Flowers & Decor — концепт и план реализации

Концепт для обсуждения с Артёмом, не установленная модель Unity. Арт: `flowers-decor-concept-v1.png`. Создан встроенным image_gen по приложенному визуальному референсу. Четыре ракурса: фасад со стороны улицы, задний двор, вид сверху и боковой вид.

## Идея

Небольшой садовый магазин с кремовыми стенами, тёмно-зелёной крышей, деревянными навесами и витринами цветов и домашнего декора. Название на фасаде — `FLOWERS & DECOR`. Зелёная крыша, дерево и стеклянная теплица отличают его от терракотовой пекарни с бордовыми маркизами.

Перед магазином — две клиентские парковки и свободный пешеходный вход. Справа — отдельный проезд к рабочему двору. Сзади слева — теплица; справа — компактный склад с воротами и навесом загрузки. Цветочные стеллажи, горшки и коробки расставляются группами вне траектории пикапа. Людей, автобусной остановки, белых стрелок на асфальте и задних вывесок нет.

Размер участка около 30×42 м и проезд около 6 м — предварительные проектные ориентиры. Это художественный концепт, не точный строительный чертёж: размеры и совпадение ракурсов требуют проверки на реальном участке Unity.

## Этапы

1. Проверить участок и собрать черновые объёмы магазина, теплицы, склада и проезда. Сначала обеспечить манёвр игрового пикапа и парковку задом у выдачи.
2. Сделать основные модели: корпус, зелёную крышу, двери, витрины, деревянные навесы, теплицу, складские ворота и ограждение. Все надписи — на английском.
3. Создать модульный декор: несколько форм цветов и растений, горшки и вазы, стеллажи, садовые ящики, тележку, коробки домашнего декора и паллеты. Расставить их по зонам, не забивая проезд.
4. Подключить существующую выдачу цветов/декора, исключить дубликат поставщика, проверить карту, получение товаров, коллайдеры, производительность и внешний вид с игровой камеры. Применять только согласованные изменения, ферму и соседние объекты сохранять.

## Что важно при реализации

- Теплица даёт узнаваемый силуэт, но прозрачное стекло требует ограничения числа перекрывающихся поверхностей и отдельной проверки производительности.
- Богатые цветочные витрины нужны у фасада; дальние растения делаются проще и повторяются из небольшого набора моделей.
- Рабочий двор сохраняет пространство для манёвра. Не заполнять всю пустоту декором ради плотности.
- Склад поддерживает магазин, а не выглядит отдельным огромным гаражом. Декор должен включать не только растения, но и вазы, кашпо и упакованные предметы оформления.

## Финальный промпт генерации

Встроенный image_gen; изображение пользователя использовано как референс дизайна и стиля, не как цель редактирования.

```text
Use case: stylized-concept. Asset type: architectural concept sheet for a low-poly Unity driving/retail game. Input image 1 is a DESIGN AND STYLE REFERENCE, not an edit target. Create ONE beautiful large landscape multi-view presentation board showing a coherent proposed Flowers & Decor shop based closely on that reference. This is a proposed design, not a screenshot of an implemented game asset.
Layout: four clearly separated large panels in a tidy 2x2 grid with narrow light cream gutters and understated dark forest green header strips. Panel headings, exact English text: '01 STREET FRONT', '02 REAR LOADING COURT', '03 TOP VIEW', '04 SIDE VIEW'. Every panel must depict the SAME building and site, with consistent roof, windows, greenhouse position, entrance, fences, service shed, truck route and furniture. Include the full plot, not cropped architectural elements. Panels 1 and 2 eye-level slightly elevated three-quarter views (front from the street; rear toward the service canopy). Panel 3 truly vertical overhead site plan, roof visible; panel 4 elevated opposite-side view showing the connection of street, driveway, plant displays and greenhouse. No underside rendering.
Architecture: a charming modest one-and-a-half-storey cream plaster garden boutique, a dark forest-green tiled pitched/gabled roof with a small chimney, sage green door and window trim, a small arched attic window in the FRONT gable, warm timber porch and pergola frames, wide front windows showing bouquets, ceramic pots and tasteful home decor. One elegant dark green storefront sign reading EXACTLY 'FLOWERS & DECOR'. Restrained warm wood and terracotta pots, varied grouped yellow, pink, white and violet flowers; recognizable low-poly leaves, no hyperrealistic detail. Visually DISTINCT from the cream/red-roof/burgundy-awning bakery: dominant green roof, wooden lattice/pergolas and greenhouse, not red roof or bakery-style awnings.
Site design locked across all panels: street along the SOUTH/front edge; proposed plot about 30 m wide by 42 m deep, not surveying-accurate. Main shop in FRONT-LEFT portion, about 14 m wide by 14 m deep. Two modest customer parking spaces at the front LEFT, separate from the vehicle entrance. Pedestrian frontage and a wooden porch remain clear. A continuous approx 6 m-wide unobstructed driveway enters at the FRONT-RIGHT and reaches the rear court; a pickup never drives through the front display or greenhouse. A rectangular transparent glass greenhouse in dark green frames occupies the BACK-LEFT, showing tables with seedlings and flowers. A compact pale sage service/store shed occupies the BACK-RIGHT, with a plain grey roll-up door facing SOUTH toward the clear rear court and a small timber-and-dark-green canopy. It is a quiet supporting structure, not a second industrial warehouse. A single yellow rectangular loading bay in front of that rear door, with one orange low-poly pickup parked nose facing SOUTH and open pickup bed toward the NORTH goods door (rear docking). Enough empty maneuvering space before the bay; do not crowd the driving corridor with pots. Stacked plant trays, a hand trolley, wooden crates and packaged home-decor cartons grouped BESIDE the rear door, outside the vehicle envelope. Wrought-iron side and back fences with low cream brick piers and planted edging; keep vehicle entrance open. Shelving with garden plants and sample pots groups near the shop's side pergola, never across the 6 m lane. Sparse neighboring simple pastel low-poly city homes and faceted trees beyond the property give context. Do not add people, buses, bus stops, street stalls, cafe umbrellas, arrows painted on the asphalt, rear 'collection' signs or blank white signage panels. Keep only simple practical white parking lines and the yellow loading bay. Render manageable game-ready modular forms, matte materials, simple realistic proportion, pleasant daylight, legible silhouettes and soft directional shadows.
Top panel should clearly communicate zoning without covering the artwork: five small circled English key labels with thin leader lines: 'SHOP', 'GREENHOUSE', 'DISPLAY', 'DRIVEWAY 6 m', 'LOADING BAY'. Do not show complex UI, excessive annotations, unrelated logos, watermarks, or a text-heavy work schedule. Ensure facade text is clean and correctly spelled, no Cyrillic anywhere.
```
