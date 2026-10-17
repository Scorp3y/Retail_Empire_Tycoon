# Район пекарни — нативная сборка

## Решение

Кофейня и цветочный киоск — небольшие дополнительные здания по согласованному концепту `BakeryQuarterPlan-v1.png`. Существующая пекарня остаётся главным зданием, её надписи и задняя выдача не меняются. Кофейня находится восточнее пекарни (слева при взгляде с северной улицы), киоск — западнее, за границей служебного двора.

Отдельный узел `Bakery quarter public amenities` содержит общественные площадки, четыре столика под перголой, восемь газонных островков с деревьями и цветами, шесть скамеек, фонари, урны, стойки цветов и велосипедные парковки. Два существующих уличных дерева и светофор сохранены. Старая небольшая клумба и велосипедная стойка заменены только в двух узких пятнах расстановки; общие исходные меши не перезаписываются.

Новые модели сделаны как настоящие объёмные Unity-меши, а не плоская подстановка арта. Набор из 16 префабов лежит в `Assets/Art/TownBakeryQuarter/Prefabs`. Чтобы менять расстановку, правьте координаты в `TownBakeryQuarterPlacement` и повторяйте сборку от исходной резервной копии. В установленном районе видимая геометрия объединена по трём материалам для сокращения числа отрисовок; перетаскивание одного оставшегося узла коллайдера не перемещает объединённую картинку. Для ручной независимой расстановки используйте исходные префабы набора.

## Путь сборки

Англоязычные задания моделей → `TownBakeryQuarterModels` → меши, материалы, английские вывески и физические объёмы → `TownBakeryQuarterPlacement` → отдельный узел в существующем TownEnvironment → проверки сохранённой сцены → реальный задний подъезд пикапа в Play Mode → повторная проверка и кадры в свежем Unity → резервная копия и установка в основной проект.

`Scripts/BuildTownBakeryQuarter.ps1`: режимы `Inspect`, `Generate`, `Play`, `Review`, `Apply`. Каждый генератор работает только в `Library/CityUpdateQA/IsolatedProject`. Установка требует успешных трёх отчётов и совпадения хеша префаба с проверенным. При изменении входных файлов основного проекта установка прекращается, не затирая пользовательские правки.

## Англоязычные задания новых объектов

**Coffee house:** Create a compact single-storey low-poly neighbourhood coffee house, 10 by 8 metres, pale cream walls and warm brick accents, muted dark green hip roof, English COFFEE sign, three transparent front glazing panels, a fitted coffee counter with machine and cups, stocked rear shelves. Keep it lower and smaller than the existing two-storey bakery. No Cyrillic labels, no new gameplay checkpoint.

**Flower kiosk:** Create a small 6 by 4.8 metre low-poly wooden flower kiosk with a dark green hip roof, English FLOWERS sign, front serving counter, coloured potted flowers and two freestanding flower displays. Clear customer approach; no commercial vehicle props in front.

**Public amenities:** Create a modular wooden pergola with four two-seat patio table sets, two striped umbrellas, cups, six wood-and-metal benches, restrained town lanterns, litter bins, low stone flower beds, planted lawn islands, eight deciduous shade trees, low hedges, bicycle racks and a compact coffee menu board. Match the existing town's flat-shaded palette and metre scale. Keep the bakery entrance, fenced service route, surrounding roads and sidewalks unobstructed.

## Проверки и ограничения

Фактический результат проверок хранится в `Docs/TownBakeryQuarterReview`: `result.txt`, `protection.txt`, `access.txt`, `play.txt`, `review.txt` и шесть нативных снимков. Только наличие отчётов PASS подтверждает завершённую установку; этот документ сам по себе не является подтверждением.

Проверяются все новые физические объёмы: опора на поверхности, отсутствие пересечений и нахождение внутри общественных участков. Отдельно проверяются геометрия целиком (включая кроны и свесы), подходы к новым зданиям, передние пешеходные полосы, весь маршрут пекарни для объёма 2.7 × 6.8 м и фактическая выдача с правильным задним подъездом. Точные отпечатки геометрии, материалов и коллизий защищают весь город за пределами двух разрешённых общественных участков.

Кофейня и киоск пока являются окружением: отдельные торговые интерфейсы, персонал, NPC и бизнес-механики не добавляются. Это не заявка на поддержку длинных грузовиков и не замер FPS. Посадка пикапа для снимков временная: сцена и старт игрока не сохраняются. Коммиты и отправка в GitHub не выполняются.

## Результат установки 14 октября 2026

- Generate, Play и свежий Review завершились PASS.
- 118 точек маршрута проверены с физической опорой и габаритом 2.7 × 6.8 м. Реальный пикап проехал задним ходом 11.08 м и получил доступ к выдаче; неправильное направление и выдача на ходу отклонены.
- 61 848 треугольников в трёх визуальных группах. Все новые физические объёмы проверены на опору, границы и пересечения. Саму пекарню и остальные участки защищают точные отпечатки геометрии и коллизий.
- Установленный префаб и все 97 файлов набора совпадают с проверенной копией. SHA256 TownEnvironment: `BC5D99E7473D4EF486F6A68FEEFC857AFD34268455F55FF7AF8ABDA8FDE654E8`.
- Исходный префаб для восстановления: `Library/TownBakeryQuarterPipeline/BeforeApply_20261014_035054/Assets/Prefabs/TownWorld/TownEnvironment.prefab`. Не подменяйте им более поздние изменения без отдельного согласования.
