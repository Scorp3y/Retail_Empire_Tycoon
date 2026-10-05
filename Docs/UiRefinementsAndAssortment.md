# UI и расширение ассортимента

## Решение

Изменения используют существующие Canvas, контроллеры магазина, инвентаря, экономики и сохранения. Миграция запускается явно через `Retail Empire / UI / Apply current UI and assortment`, только в сохранённой сцене Game и вне Play Mode. Она не читает и не изменяет сохранение игрока.

- Магазин и инвентарь центрированы. Размер целого окна адаптируется к Canvas без обрезания содержимого.
- Дужка замка открывается/закрывается за 0,28 с по реальному времени. Состояние магазина меняется сразу; анимация только отражает его, включая клавишу O и обратное переключение во время перехода.
- В режиме покупки участков кнопка «К магазину» вызывает существующий `TerritoryPurchaseModeManager.Exit`, который возвращает камеру и выключает режим покупки.
- Ползунки используют геометрию капсулы вместо растянутых текстур. Область захвата шире тонкого трека.
- Подсказки клавиш не имеют общего фона: светлые подписи с тёмной обводкой читаются на игровой сцене, клавиши сохраняют компактные плашки. Настройка «Подсказки: вкл./выкл.» хранится в `PlayerPrefs` под ключом `ShopUi.ShowHints`. Само управление остаётся доступным при скрытых подсказках. Переназначенные клавиши отражаются в подсказках.
- Четыре профессии имеют четыре независимых прозрачных портрета в общей палитре.
- Подчёркивания убраны только из отображаемых названий. Идентификаторы сохранения не меняются.

## Товары и полки

В каталоге оборудования овощной стеллаж показан первым, хлебный — вторым, как базовая пара магазина. Меняется только порядок карточек; ID предметов и данные сохранения остаются прежними.

Добавлено 20 товаров из уже имеющихся моделей проекта; вместе со старыми пятью — 25. Покупка добавляет упаковку 10 шт. Выкладка, NPC, складовщик и сохранение используют существующий `ProductItemData` и `PlacedShelfStock`.

| Полка | Принимаемые товары |
| --- | --- |
| Хлебный стеллаж | Хлеб и выпечка |
| Новый овощной стеллаж | Яблоки, бананы, апельсины, груши, лимоны, виноград, морковь, огурцы, помидоры, брокколи, картофель, сладкий перец |
| Холодильная витрина | Молоко, сыр, яйца |
| Морозильная витрина | Стейк, колбаса, куриное филе, мороженое |
| Пристенный и двойной стеллажи | Масло, кола, сок, кетчуп, горчица |

Путь данных: карточка магазина списывает цену упаковки → `ProductInventory` принимает товар → выбор «Выложить» подсвечивает совместимые полки → выкладка переносит товар на выбранную полку → NPC берёт единицу этого же товара. Стабильный ID восстанавливается через `ProductCatalog`.

Совместимость задаётся типом хранения в данных полки, а не названием/иконкой. В карточках явно указана подходящая полка, каталог сгруппирован по типу хранения. Новые значения перечислений добавлены в конец, чтобы номера старых значений не изменились.

Хлебный стеллаж перестроен с деревянными лотками на трёх ярусах и отдельными позициями для 24 буханок; старый GUID и ID `shelf_fresh_01`, footprint 4 × 3 сохранены. Вместо прежнего прямоугольного хлеба используется гранёная округлая буханка с надрезами корочки. Овощной стеллаж — отдельный покупаемый предмет `shelf_produce_01`, 30 позиций, $140. Пока одна полка хранит один вид товара — как и остальные полки проекта.

Новые модели товаров обёрнуты отдельными prefab с нормализованным масштабом и нижним центральным pivot. Исходные пакеты моделей и их материалы не изменяются. Коллайдеры отдельных продуктов удалены, чтобы они не мешали проходу NPC; взаимодействие идёт через коллайдер полки.

## Проверка

Тесты запускаются только в `ShopGameplaySandbox`, где отсутствуют `SaveManager` и компоненты записи обучения. UI-проверка: Ctrl+Shift+F2; ассортимент: Ctrl+Shift+Alt+F12. Отчёты — `Library/ShopUiQA/validation.txt` и `Library/ShopAssortmentQA/validation.txt`.

UI-тест проверяет центрирование и границы окон, оба перехода замка, кнопку возврата участков, четыре разных портрета, геометрию ползунков, скрытие/показ и сохранение настройки подсказок. Исходные настройки языка и подсказок восстанавливаются в finally.

Окончательный UI-тест прошёл в форматах 1440 × 1080 (4:3), 1920 × 1080 (16:9) и 2560 × 1080 (21:9): все окна остаются в границах Canvas, магазин и инвентарь центрированы, ручки ползунков сохраняют высоту 26 и центр трека. Отчёты сохранены как `validation-1440x1080.txt`, `validation-1920x1080.txt` и `validation-2560x1080.txt` в `Library/ShopUiQA`. Full HD дополнительно проверен на копии сохранённой планировки. В UI-тесте игровое время приостановлено, чтобы зарплаты и незавершённые продажи не влияли на проверку точного списания; анимации интерфейса продолжаются по реальному времени.

Итоговые скриншоты: [персонал](Images/UiStaffRefined.png), [настройки](Images/UiSettingsRefined.png), [новый хлебный стеллаж в каталоге](Images/UiShelvesRefined.png).

Тест ассортимента проверяет каждый товар: наличие модели/иконки и ID, покупку с достаточным и недостаточным балансом, сохранение склада, разрешённые/запрещённые полки, выкладку, отображение товара, выдачу единицы NPC и восстановление остатка полки. Для хлеба и яблок дополнительно проверены начало мини-игры, отмена без расходования товара и завершение пяти жестов. Для овощного стеллажа — реальная покупка через карточку, восстановление инвентаря, установка через правила сетки и запись установленного предмета в данные сохранения.

Проверки ассортимента и игровой логики прошли в Unity Play Mode. На копии существующей планировки прошли торговля, выкладка, зарплаты и действия четырёх профессий; тест запуска и движения проверил позднее обновление денег, небольшие швы, тонкое препятствие, проход двери и восстановление заблокированного маршрута. Отдельная сборка приложения не запускалась.

## Происхождение портретов

### Cashier

Файл: `Assets/Art/ShopUi/Staff/Cashier.png`. Источник: встроенный ImageGen, прозрачный фон.

Промпт:

> Use case: stylized-concept. Asset type: one transparent staff avatar for a low-poly supermarket tycoon UI. Create exactly one character, waist-up centered portrait, front three-quarter view, face clearly visible, simple faceted polygon geometry, soft matte flat-color surfaces, friendly expressive face, rounded chunky proportions, warm cream and forest green supermarket palette. Consistent soft studio light from upper left. Real transparent background, no backdrop, no text, no logo, no frame, no watermark. Occupy 85% of a square canvas, leave a small clear margin around the silhouette. Readable at 64 pixels. Subject: a young woman cashier with auburn ponytail, forest-green polo shirt with cream collar, a small handheld barcode scanner held near her waist; confident cheerful smile.

### Guard

Файл: `Assets/Art/ShopUi/Staff/Guard.png`. Источник: встроенный ImageGen, прозрачный фон.

Промпт:

> Use case: stylized-concept. Asset type: one transparent staff avatar for a low-poly supermarket tycoon UI. Create exactly one character, waist-up centered portrait, front three-quarter view, face clearly visible, simple faceted polygon geometry, soft matte flat-color surfaces, friendly expressive face, rounded chunky proportions, warm cream and forest green supermarket palette. Consistent soft studio light from upper left. Real transparent background, no backdrop, no text, no logo, no frame, no watermark. Occupy 85% of a square canvas, leave a small clear margin around the silhouette. Readable at 64 pixels. Subject: a middle-aged broad-shouldered male security guard with short dark beard and navy cap, navy uniform with subtle green trim and a simple shield-shaped chest badge without text; calm reassuring expression; no weapons.

### Stocker

Файл: `Assets/Art/ShopUi/Staff/Stocker.png`. Источник: встроенный ImageGen, прозрачный фон.

Промпт:

> Use case: stylized-concept. Asset type: one transparent staff avatar for a low-poly supermarket tycoon UI. Create exactly one character, waist-up centered portrait, front three-quarter view, face clearly visible, simple faceted polygon geometry, soft matte flat-color surfaces, friendly expressive face, rounded chunky proportions, warm cream and forest green supermarket palette. Consistent soft studio light from upper left. Real transparent background, no backdrop, no text, no logo, no frame, no watermark. Occupy 85% of a square canvas, leave a small clear margin around the silhouette. Readable at 64 pixels. Subject: a young male stock replenisher with curly dark hair and orange knit cap, cream work shirt and forest-green vest, holding a small plain cardboard carton near his waist; friendly focused expression.

### Cleaner

Файл: `Assets/Art/ShopUi/Staff/Cleaner.png`. Источник: встроенный ImageGen, прозрачный фон.

Промпт:

> Use case: stylized-concept. Asset type: one transparent staff avatar for a low-poly supermarket tycoon UI. Create exactly one character, waist-up centered portrait, front three-quarter view, face clearly visible, simple faceted polygon geometry, soft matte flat-color surfaces, friendly expressive face, rounded chunky proportions, warm cream and forest green supermarket palette. Consistent soft studio light from upper left. Real transparent background, no backdrop, no text, no logo, no frame, no watermark. Occupy 85% of a square canvas, leave a small clear margin around the silhouette. Readable at 64 pixels. Subject: a middle-aged woman cleaner with grey hair tied in a bun, muted plum polo and forest-green apron, green rubber gloves, holding a simple mop handle beside one shoulder; warm smile.
