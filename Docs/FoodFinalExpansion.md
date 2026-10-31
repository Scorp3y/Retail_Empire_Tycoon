# Food Expansion — часть 4

Экспорты: один объект/mesh/node на GLB, Z-up, метры, основание Z=0 и центр в плане в начале координат.
Нет изображений и текстур; 2–4 материала на продукт, мягкие фаски и простые тёплые цвета.
Цвета вершин используют GeometryPigment, совместимый с имеющимся конвертером проекта.
В каждой папке сохранены Models.blend, самостоятельный скрипт, Preview.png и Validation.json.
Последняя часть прошла первую версию и два прохода исправлений, все превью просмотрены.

Все продукты целые: буррито закрыты, образцов со срезами нет. Отверстия канноли и
свёрнутой коры корицы — естественная форма продукта. Содержимое и упаковка раздельны
в редактируемом исходнике и объединены в экспортную сетку с отдельными материалами.

Проверены реальные бинарные позиции и индексы GLB, границы индексов, бюджеты треугольников,
нормали, отсутствие текстур, материалы, прозрачность alpha 0.25 / roughness 0.2,
матовые поверхности roughness 0.82, размеры, начало координат и наличие превью.

Фактическая сборка — Blender 5.2.3 LTS. Отдельный запуск скриптов в 4.x не проверялся.
Перед использованием в Unity проверить прозрачность, ориентацию Z-up и цвета вершин
в игровом освещении. Эти 96 продуктов ещё не включены в каталог закупок Unity.

| Модель | Треугольники | Папка |
| --- | ---: | --- |
| Waffles | 1154 | [Food_13_Bakery](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_13_Bakery>) |
| Pancakes | 1038 | [Food_13_Bakery](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_13_Bakery>) |
| Apple Pie | 1020 | [Food_13_Bakery](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_13_Bakery>) |
| Madeleines | 822 | [Food_13_Bakery](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_13_Bakery>) |
| Meringues | 890 | [Food_13_Bakery](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_13_Bakery>) |
| Cannoli | 1066 | [Food_13_Bakery](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_13_Bakery>) |
| Popcorn | 838 | [Food_14_SweetSnacks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_14_SweetSnacks>) |
| Marshmallows | 998 | [Food_14_SweetSnacks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_14_SweetSnacks>) |
| Jelly Beans | 904 | [Food_14_SweetSnacks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_14_SweetSnacks>) |
| Licorice | 770 | [Food_14_SweetSnacks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_14_SweetSnacks>) |
| Caramels | 752 | [Food_14_SweetSnacks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_14_SweetSnacks>) |
| Lollipops | 966 | [Food_14_SweetSnacks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_14_SweetSnacks>) |
| Burrito | 512 | [Food_15_ReadyFood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_15_ReadyFood>) |
| Calzone | 572 | [Food_15_ReadyFood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_15_ReadyFood>) |
| Samosas | 414 | [Food_15_ReadyFood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_15_ReadyFood>) |
| Spring Rolls | 490 | [Food_15_ReadyFood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_15_ReadyFood>) |
| Falafel | 674 | [Food_15_ReadyFood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_15_ReadyFood>) |
| Onigiri | 734 | [Food_15_ReadyFood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_15_ReadyFood>) |
| Chocolate Spread | 776 | [Food_16_SpreadsTreats](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_16_SpreadsTreats>) |
| Almond Butter | 552 | [Food_16_SpreadsTreats](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_16_SpreadsTreats>) |
| Cashew Butter | 876 | [Food_16_SpreadsTreats](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_16_SpreadsTreats>) |
| Sunflower Seed Butter | 592 | [Food_16_SpreadsTreats](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_16_SpreadsTreats>) |
| Halva | 740 | [Food_16_SpreadsTreats](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_16_SpreadsTreats>) |
| Fruit Leather | 848 | [Food_16_SpreadsTreats](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_16_SpreadsTreats>) |
