# Food Expansion — обновлённая часть 3

Готовы 24 отдельных GLB. В каждой папке: Models.blend с редактируемыми частями,
BuildFood*.py, Preview.png, Validation.json. Итерации 04, 05 и 06 сохраняют
первый рендер обновлённой версии и два прохода исправлений.

Общие формы лотка, банки специй, бутылки и пакета переиспользуются. Все детали
и значки — геометрия, без картинок и текстур. Прозрачные материалы: alpha 0.25,
roughness 0.2. Непрозрачные поверхности матовые. Экспорт — одна сетка/узел на продукт,
содержимое и упаковка раздельны в редактируемом исходнике. Все продукты целые;
Brie без вырезанного сектора. Несколько мидий естественно раскрыты в раковине.

Автоматически проверены бинарная геометрия, индексы, нормали, материалы,
бюджеты треугольников, масштаб в метрах, нижний центр на Z=0 и отсутствие текстур.
Фактически использован Blender 5.2.3 LTS; отдельный запуск в Blender 4.x не проверялся.

Вручную в Unity проверить прозрачность плёнки, видимость содержимого, ориентацию
Z-up и поддержку цветов вершин GeometryPigment при игровом освещении. Мелкие
сложные надписи заменены значками там, где текст превышал бюджет. В каталог
закупок и на игровые полки эта работа модели автоматически не добавляет.

| Модель | Треугольники | Папка |
| --- | ---: | --- |
| Ricotta | 833 | [Food_09_CheeseDairy](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_09_CheeseDairy>) |
| Mozzarella | 1029 | [Food_09_CheeseDairy](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_09_CheeseDairy>) |
| Feta | 476 | [Food_09_CheeseDairy](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_09_CheeseDairy>) |
| Brie | 505 | [Food_09_CheeseDairy](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_09_CheeseDairy>) |
| Gouda | 583 | [Food_09_CheeseDairy](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_09_CheeseDairy>) |
| Mascarpone | 870 | [Food_09_CheeseDairy](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_09_CheeseDairy>) |
| Turkey | 777 | [Food_10_MeatSeafood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_10_MeatSeafood>) |
| Duck | 784 | [Food_10_MeatSeafood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_10_MeatSeafood>) |
| Trout | 994 | [Food_10_MeatSeafood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_10_MeatSeafood>) |
| Mackerel | 1040 | [Food_10_MeatSeafood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_10_MeatSeafood>) |
| Mussels | 1165 | [Food_10_MeatSeafood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_10_MeatSeafood>) |
| Squid | 1124 | [Food_10_MeatSeafood](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_10_MeatSeafood>) |
| Lemonade | 791 | [Food_11_Drinks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_11_Drinks>) |
| Ginger Ale | 883 | [Food_11_Drinks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_11_Drinks>) |
| Kombucha | 822 | [Food_11_Drinks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_11_Drinks>) |
| Coconut Water | 849 | [Food_11_Drinks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_11_Drinks>) |
| Grape Juice | 873 | [Food_11_Drinks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_11_Drinks>) |
| Tomato Juice | 895 | [Food_11_Drinks](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_11_Drinks>) |
| Cinnamon Sticks | 1168 | [Food_12_Spices](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_12_Spices>) |
| Vanilla Pods | 897 | [Food_12_Spices](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_12_Spices>) |
| Turmeric | 756 | [Food_12_Spices](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_12_Spices>) |
| Paprika | 678 | [Food_12_Spices](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_12_Spices>) |
| Black Pepper | 1153 | [Food_12_Spices](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_12_Spices>) |
| Bay Leaves | 883 | [Food_12_Spices](<C:/Users/artem/Retail Empire Tycoon/ArtSource/Food_12_Spices>) |
