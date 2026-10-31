# Food Expansion — часть 3

24 отдельных GLB в четырёх категориях. В каждой папке: самодостаточный скрипт,
Models.blend с отдельными редактируемыми частями, финальный Preview.png,
Validation.json и три прохода с превью в Iterations/01, 02, 03.

Сохранены функции палитры, примитивов, фасок с двумя сегментами, экспорта и рендера
из предыдущих частей. Общие формы: лоток белковых продуктов, банка специй,
картонная упаковка напитков и бутылка. Геометрия содержимого отделена от упаковки.
Для полых сосудов толщина и внутреннее дно проверяются лучом в Blender.

По подтверждённому правилу проекта бри сделан целым, мидии — закрытыми;
нет клиньев, разрезанных образцов или рисунков срезов на упаковке.
Текст — сетка; если длинная надпись превышает бюджет, используется простой значок.
Цвета вершин пищевых частей используют GeometryPigment — имя уже поддержано
существующим конвертером проекта. Текстур и сторонних зависимостей нет.

Независимая проверка GLB: один mesh/node, бюджеты треугольников, 2–4 материала,
отсутствие изображений и текстур, metallic 0, roughness 0.82 / 0.2,
alpha 0.25 у прозрачных частей, COLOR_0 у GeometryPigment, основание Z=0,
центр в плане в начале координат, размеры в метрах, все три превью на категорию.

Фактический Blender — установленный 5.2.3 LTS; отдельный запуск в 4.x не проверялся.
При импорте в Unity проверить прозрачность, цвета вершин и Z-up (стандарт glTF — Y-up).
Эти 24 модели пока не добавлены в каталог закупок Unity.

| Модель | Треугольники | Папка |
| --- | ---: | --- |
| ricotta | 833 | [Food_09_CheeseDairy](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_09_CheeseDairy>) |
| mozzarella | 1023 | [Food_09_CheeseDairy](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_09_CheeseDairy>) |
| feta | 476 | [Food_09_CheeseDairy](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_09_CheeseDairy>) |
| brie | 397 | [Food_09_CheeseDairy](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_09_CheeseDairy>) |
| gouda | 439 | [Food_09_CheeseDairy](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_09_CheeseDairy>) |
| mascarpone | 742 | [Food_09_CheeseDairy](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_09_CheeseDairy>) |
| turkey | 688 | [Food_10_MeatSeafood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_10_MeatSeafood>) |
| duck | 688 | [Food_10_MeatSeafood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_10_MeatSeafood>) |
| trout | 923 | [Food_10_MeatSeafood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_10_MeatSeafood>) |
| mackerel | 1040 | [Food_10_MeatSeafood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_10_MeatSeafood>) |
| mussels | 888 | [Food_10_MeatSeafood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_10_MeatSeafood>) |
| squid | 1124 | [Food_10_MeatSeafood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_10_MeatSeafood>) |
| lemonade | 831 | [Food_11_Drinks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_11_Drinks>) |
| ginger_ale | 800 | [Food_11_Drinks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_11_Drinks>) |
| kombucha | 702 | [Food_11_Drinks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_11_Drinks>) |
| coconut_water | 849 | [Food_11_Drinks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_11_Drinks>) |
| grape_juice | 869 | [Food_11_Drinks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_11_Drinks>) |
| tomato_juice | 685 | [Food_11_Drinks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_11_Drinks>) |
| cinnamon_sticks | 1168 | [Food_12_Spices](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_12_Spices>) |
| vanilla_pods | 874 | [Food_12_Spices](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_12_Spices>) |
| turmeric | 756 | [Food_12_Spices](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_12_Spices>) |
| paprika | 678 | [Food_12_Spices](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_12_Spices>) |
| black_pepper | 1129 | [Food_12_Spices](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_12_Spices>) |
| bay_leaves | 877 | [Food_12_Spices](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_12_Spices>) |
