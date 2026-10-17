# Железнодорожный переезд — концепт v1

Визуальный план для Артёма. Сцену Unity и существующие объекты не меняли.

## Идея
Продолжить существующую улицу по отмеченному красным коридору, пересечь один железнодорожный путь в зелёной отметке и открыть дорогу в будущий район. Сохранить парк, направление железной дороги и лес за пределами узкого дорожного коридора.

## Объекты
1. Двухполосное продолжение улицы с плавным примыканием к перекрёстку.
2. Настил переезда заподлицо с рельсами.
3. Шлагбаумы, сигнальные стойки и стоп-линии на обоих подходах.
4. Непрерывный пешеходный маршрут с направляющими ограждениями.
5. Технический домик и шкаф управления вне коридора движения.
6. Указатель NEW DISTRICT, освещение, газоны и лесная опушка.

## Этапы реализации
1. Уточнить координаты, масштаб, габариты и рельеф. Разметить дорожный коридор и сохранить окружающий лес.
2. Собрать модульный переезд, дорогу, пешеходный проход и техническое оборудование.
3. Добавить ограждения, свет, разметку и растительность, не закрывая обзор и проходы.
4. Проверить проезд транспорта, коллизии и пешеходный маршрут. Если переезд станет действующим, отдельно реализовать согласованное управление поездом, сигналами и шлагбаумами.

## Проверка и ограничения
Изображение визуально просмотрено: показаны существующее примыкание, одиночный путь, продолжение дороги, парк и лес; основная перспектива и план сверху согласованы по структуре.
Это художественный концепт, не инженерная схема и не подтверждение безопасности. Точные расстояния, положение рельсов внутри настила, число и ориентацию сигнальных стоек и шлагбаумов необходимо уточнить при сборке. Физика, обзорность, маршруты и взаимодействие поезда с автомобилями не тестировались. Обещание безопасности в декоративном тексте арта не является результатом проверки.

## Генерация
Использован навык imagegen и встроенная генерация изображений с двумя пользовательскими скриншотами в качестве референсов. Все новые надписи — английские.
Исходный результат: C:/Users/artem/.codex/generated_images/01a122f0-0110-7302-aaa6-2ce9343d3c58/exec-1da40487-8939-4db6-be06-3f53957844e0.png

### Финальный промпт
```text
Create a polished landscape concept-art planning board for a low-poly city-building game: RAILWAY CROSSING — NEW DISTRICT GATEWAY. Use the two attached Unity screenshots as site and visual-style references. The second screenshot is the authoritative layout: the red hand-drawn corridor continues the existing city street from its T-junction across the single railway track into the forest; the green square marks the level crossing. Remove all screenshot UI, gizmos and colored markup in the artwork.

Match the simple faceted trees, restrained cream/teal city palette, clean modular geometry and sunny game lighting. This is a plausible upgrade of THIS place, not an unrelated transport complex. Preserve the existing straight single-track railway, existing urban street and junction, neighboring park and city edge. Clear only a narrow road corridor through the woodland; retain dense mixed conifer and broadleaf forest to either side.

Board layout: a large beautiful three-quarter aerial perspective occupying the top two-thirds, a clearly legible small orthographic site plan at bottom left matching the SAME geometry, a compact numbered object key at bottom right, and a four-stage work-plan footer. Use short crisp English labels only, no Russian.

Design: a two-lane local road extends from the existing junction, crosses the railway approximately perpendicularly on flush dark rubber/concrete crossing panels, then continues a modest distance into the forest toward a lightly indicated future district. Draw continuous rails through the flush road deck, no missing track and no steps in the road. The road continuation should read immediately. Paired red-and-white barrier arms and railway warning signal masts serve both approaches, arms raised and signals inactive, no train occupying the crossing. Stop lines are before the barriers, outside the rail corridor. Include a coherent pedestrian route along one road edge, its own simple protected passage across the track and short guardrails guiding pedestrians. No zebra stripes on the rails. Keep all buildings, props, lamps and vegetation well clear of tracks and roadway. Include a small cream technical hut with teal roof and a control cabinet off to one side outside the railway corridor, modest road lighting, low roadside fencing that stops at the crossing, tidy grassy verges and a small NEW DISTRICT directional sign beyond the track. No overhead gantry, extra rail tracks, station, freight terminal, bridge, tunnel, enormous paved plaza, invented industrial buildings or dense new neighborhood. Do not obstruct the future road with the directional sign. No vehicles needed.

Six numbered callouts: 1 ROAD CONNECTION, 2 CROSSING DECK, 3 BARRIERS & SIGNALS, 4 PEDESTRIAN ROUTE, 5 CONTROL HUT, 6 FUTURE DISTRICT. Keep callouts away from focal geometry. Footer: 01 SITE & ROAD / 02 CROSSING KIT / 03 LANDSCAPE / 04 GAMEPLAY TESTS. Make the main perspective and plan consistent: one city road joins the existing street, crosses one track and reaches the forest. This is a visual game-art proposal, not a detailed engineering drawing. Render at high quality with clear spatial hierarchy and generous readable layout.
```

