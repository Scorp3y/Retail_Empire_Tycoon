# Food Expansion — первая часть

24 целых продукта; разрезы, половинки и очищенные образцы исключены по подтверждённому правилу пользователя.
Экспорт: один mesh/node на GLB, Z-up, метры, основание на Z=0, центр в плане в начале координат.
Каждая категория содержит самодостаточный Python-скрипт, Models.blend с редактируемыми частями,
финальный Preview.png, Validation.json и два прохода в Iterations/01 и Iterations/02.

Применены прежние помощники геометрии, палитра с дополнительными тонами продуктов, фаски с двумя сегментами,
матовые материалы без текстур. Пластик — alpha 0.25, roughness 0.2. Содержимое и тара раздельны
в редактируемом исходнике и объединены в одну экспортную сетку с отдельными материалами.

Blender в этой среде: 5.2.3 LTS. Скрипты используют совместимые с 4.x API; фактический запуск
в Blender 4.x не проверялся. Проверять прозрачность в целевом движке: GLB экспортирован как Z-up,
поэтому импортёр со стандартным glTF Y-up может потребовать изменения ориентации.
Эта задача создаёт исходники и экспорты; новые 24 продукта ещё не добавлены в закупку Unity.

| Модель | Треугольники | Путь относительно ArtSource |
| --- | ---: | --- |
| pumpkin | 596 | `Food_01A_RootsSquash/pumpkin.glb` |
| sweet_potato | 200 | `Food_01A_RootsSquash/sweet_potato.glb` |
| turnip | 644 | `Food_01A_RootsSquash/turnip.glb` |
| parsnips | 488 | `Food_01A_RootsSquash/parsnips.glb` |
| kohlrabi | 796 | `Food_01A_RootsSquash/kohlrabi.glb` |
| fennel | 1024 | `Food_01A_RootsSquash/fennel.glb` |
| leek | 664 | `Food_01B_GreensVegetables/leek.glb` |
| celery | 676 | `Food_01B_GreensVegetables/celery.glb` |
| asparagus | 832 | `Food_01B_GreensVegetables/asparagus.glb` |
| artichoke | 476 | `Food_01B_GreensVegetables/artichoke.glb` |
| brussels_sprouts | 1132 | `Food_01B_GreensVegetables/brussels_sprouts.glb` |
| romaine_lettuce | 632 | `Food_01B_GreensVegetables/romaine_lettuce.glb` |
| passion_fruit | 432 | `Food_01C_TropicalFruit/passion_fruit.glb` |
| dragon_fruit | 1128 | `Food_01C_TropicalFruit/dragon_fruit.glb` |
| lychee | 1080 | `Food_01C_TropicalFruit/lychee.glb` |
| guava | 144 | `Food_01C_TropicalFruit/guava.glb` |
| starfruit | 400 | `Food_01C_TropicalFruit/starfruit.glb` |
| pomelo | 144 | `Food_01C_TropicalFruit/pomelo.glb` |
| figs | 432 | `Food_01D_SmallFruitBerries/figs.glb` |
| dates | 1088 | `Food_01D_SmallFruitBerries/dates.glb` |
| kumquats | 680 | `Food_01D_SmallFruitBerries/kumquats.glb` |
| gooseberries | 728 | `Food_01D_SmallFruitBerries/gooseberries.glb` |
| cranberries | 728 | `Food_01D_SmallFruitBerries/cranberries.glb` |
| redcurrants | 1100 | `Food_01D_SmallFruitBerries/redcurrants.glb` |
