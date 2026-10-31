# Food Expansion — часть 2

24 отдельных GLB, 4 исходника Models.blend с редактируемыми частями, 4 самодостаточных
скрипта Blender, финальные превью и три визуальных прохода для каждой категории.
В папках Iterations сохранены первая версия и два прохода исправлений.

Использованы прежние функции материалов, фасок (2 сегмента), геометрии и экспорта.
Основная палитра сохранена, дополнительные оттенки заданы в PALETTE. Нет текстур;
2–4 материала на модель. Крышки и значки бутылок используют один материал
с цветами вершин: это цвета самой сетки, без картинок, с экспортом COLOR_0.

Бумажный пакет и дой-пак — две общие переиспользуемые формы, с отдельными
прозрачными окнами и реальными отверстиями в бумаге. Стенки сосудов имеют толщину;
содержимое отделено от тары в исходнике. Полость проверяется лучом до внутреннего дна.
Экспортная копия объединена в одну сетку с отдельными материалами.

Все продукты целые, кокосовые значки тоже. Хумус показан с открытой крышкой,
согласно прямому требованию этой части; пища не разрезана.

Проверки GLB: бюджет треугольников, один mesh/node, отсутствие текстур, 2–4 материала,
alpha 0.25 / roughness 0.2 у прозрачных частей, roughness 0.82 у остальных,
центр основания в начале координат, Z=0 снизу, Z-up, реальные размеры в метрах,
экспорт цветов вершин бутылок и наличие всех трёх превью.

Фактический Blender — установленный 5.2.3 LTS. Код использует API семейства 4.x,
но запуск в отдельной установке 4.x не проверялся. В целевом движке нужно проверить
прозрачность окон, поддержку COLOR_0 и ориентацию нестандартного для glTF Z-up.
Добавление этих 24 товаров в каталог закупок Unity не входит в текущую сборку моделей.

| Модель | Треугольники | Папка |
| --- | ---: | --- |
| quinoa | 840 | [Food_05_Grains](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_05_Grains>) |
| bulgur | 840 | [Food_05_Grains](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_05_Grains>) |
| millet | 840 | [Food_05_Grains](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_05_Grains>) |
| pearl_barley | 840 | [Food_05_Grains](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_05_Grains>) |
| semolina | 840 | [Food_05_Grains](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_05_Grains>) |
| polenta | 784 | [Food_05_Grains](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_05_Grains>) |
| chickpeas | 840 | [Food_06_DriedLegumes](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_06_DriedLegumes>) |
| green_lentils | 1164 | [Food_06_DriedLegumes](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_06_DriedLegumes>) |
| red_kidney_beans | 840 | [Food_06_DriedLegumes](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_06_DriedLegumes>) |
| white_beans | 840 | [Food_06_DriedLegumes](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_06_DriedLegumes>) |
| black_beans | 840 | [Food_06_DriedLegumes](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_06_DriedLegumes>) |
| dried_peas | 840 | [Food_06_DriedLegumes](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_06_DriedLegumes>) |
| coconut_milk | 748 | [Food_07_PlantBasedSweet](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_07_PlantBasedSweet>) |
| oat_milk | 528 | [Food_07_PlantBasedSweet](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_07_PlantBasedSweet>) |
| almond_milk | 532 | [Food_07_PlantBasedSweet](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_07_PlantBasedSweet>) |
| maple_syrup | 1164 | [Food_07_PlantBasedSweet](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_07_PlantBasedSweet>) |
| molasses | 606 | [Food_07_PlantBasedSweet](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_07_PlantBasedSweet>) |
| tahini | 752 | [Food_07_PlantBasedSweet](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_07_PlantBasedSweet>) |
| hummus | 1136 | [Food_08_CookingEssentials](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_08_CookingEssentials>) |
| pesto | 582 | [Food_08_CookingEssentials](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_08_CookingEssentials>) |
| tomato_paste | 594 | [Food_08_CookingEssentials](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_08_CookingEssentials>) |
| coconut_oil | 628 | [Food_08_CookingEssentials](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_08_CookingEssentials>) |
| apple_cider_vinegar | 576 | [Food_08_CookingEssentials](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_08_CookingEssentials>) |
| balsamic_vinegar | 776 | [Food_08_CookingEssentials](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_08_CookingEssentials>) |
