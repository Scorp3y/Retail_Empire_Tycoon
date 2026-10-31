# Food Expansion — внедрение в игру

Добавлены все **96** утверждённых товаров. Вместе с предыдущими 144 позициями
пакет StoreAssortment/Expansion содержит **240 товаров и 20 видов оборудования**.
Прежние ID, GUID и ссылки старого каталога сохранены. Стартовые остатки и
планировка игрока не заменялись, бесплатная выдача нового ассортимента не добавлялась.

## Путь товара

Store → Products → Order оплачивает упаковку из 10 единиц. Заказ направляется
на Farmers market (овощи/фрукты), Bakery (выпечка) или Wholesale depot (остальное).
Получение у поставщика и выгрузка доставляют товар на склад. После назначения
подходящему стеллажу игрок или работник выкладывает его в существующие точки.
Покупатель берёт товар, приходит на кассу, оплачивает один раз и уходит.
Остатки на складе и полках сохраняются по стабильному ID товара.

## Выкладка

- Овощи и фрукты: Produce stand и существующий Produce rack.
- Выпечка: Bakery rack и существующие хлебные стеллажи.
- Молочные продукты, мясо/морепродукты, готовая еда и хумус: Chilled display и подходящее холодильное оборудование.
- Крупы, бобовые, специи, напитки, масла, соусы и сладости: Wall shelf, Double-sided aisle и Aisle endcap.
- Promotion bin принимает новые товары без необходимости холодильного хранения.

У нового оборудования явно дополнены списки совместимости. Модели ставятся
нижним центром на полку, учитывают её поворот и автоматически уменьшаются до
свободного места, сохраняя пропорции. Содержимое и прозрачная упаковка импортированы
из настоящих GLB в нативные Unity Mesh/Material/Prefab, цвета вершин сохранены,
Z-up преобразован в Unity Y-up с исправлением порядка вершин треугольников.
Существующий принцип «один назначенный товар на стеллаж» сохранён.

## Проверено

- Импорт 96 моделей, соответствие исходному количеству треугольников, иконки и экономика закупок.
- Ссылки установленных Unity-ресурсов: отсутствующих ссылок и дублирующихся GUID в каталоге нет.
- Play Mode в отдельной копии проекта: заказ через UI, отказ при нехватке денег,
  оплата, сохранение заказа, правильный поставщик, груз, однократная выгрузка,
  склад и восстановление остатков для всех 240 позиций.
- Все 613 разрешённых сочетаний новых видов оборудования и товаров: полная выкладка,
  размер в границах слота и доступные точки работы сотрудников.
- Все 20 видов оборудования: доставка, установка в четырёх поворотах и восстановление.
- Каждый из 96 новых товаров: ручная выкладка с отменой без потерь, пять действий
  выкладки, сохранение полки и настоящий покупатель с маршрутом, очередью,
  однократной оплатой и выходом. Сохранения игрока тестами не использовались.
- Просмотрены рендеры заполненных овощного прилавка, хлебной витрины,
  холодильника и пристенного стеллажа с настоящими новыми моделями.

Результаты: Library/FoodExpansionIntegration/build.txt, play.txt,
customers.txt, delivery-and-fit.txt и references.json. Резервные копии заменённых
каталога и витрин находятся в Library/FoodExpansionIntegration/Before.

## Каталог

Цена закупки указана за упаковку из 10 единиц, продажи — за одну единицу.

| Товар | Категория | Закупка упаковки | Продажа единицы |
| --- | --- | ---: | ---: |
| Fennel | Produce | $50 | $10 |
| Kohlrabi | Produce | $50 | $10 |
| Parsnips | Produce | $50 | $10 |
| Pumpkin | Produce | $50 | $10 |
| Sweet Potato | Produce | $50 | $10 |
| Turnip | Produce | $50 | $10 |
| Artichoke | Produce | $40 | $8 |
| Asparagus | Produce | $40 | $8 |
| Brussels Sprouts | Produce | $40 | $8 |
| Celery | Produce | $40 | $8 |
| Leek | Produce | $40 | $8 |
| Romaine Lettuce | Produce | $40 | $8 |
| Dragon Fruit | Produce | $60 | $12 |
| Guava | Produce | $60 | $12 |
| Lychee | Produce | $60 | $12 |
| Passion Fruit | Produce | $60 | $12 |
| Pomelo | Produce | $60 | $12 |
| Starfruit | Produce | $60 | $12 |
| Cranberries | Produce | $50 | $10 |
| Dates | Produce | $50 | $10 |
| Figs | Produce | $50 | $10 |
| Gooseberries | Produce | $50 | $10 |
| Kumquats | Produce | $50 | $10 |
| Redcurrants | Produce | $50 | $10 |
| Bulgur | Groceries | $50 | $10 |
| Millet | Groceries | $50 | $10 |
| Pearl Barley | Groceries | $50 | $10 |
| Polenta | Groceries | $50 | $10 |
| Quinoa | Groceries | $50 | $10 |
| Semolina | Groceries | $50 | $10 |
| Black Beans | Groceries | $50 | $10 |
| Chickpeas | Groceries | $50 | $10 |
| Dried Peas | Groceries | $50 | $10 |
| Green Lentils | Groceries | $50 | $10 |
| Red Kidney Beans | Groceries | $50 | $10 |
| White Beans | Groceries | $50 | $10 |
| Almond Milk | Drinks | $60 | $12 |
| Coconut Milk | Groceries | $60 | $12 |
| Maple Syrup | Preserves | $60 | $12 |
| Molasses | Preserves | $60 | $12 |
| Oat Milk | Drinks | $60 | $12 |
| Tahini | Preserves | $60 | $12 |
| Apple Cider Vinegar | Groceries | $60 | $12 |
| Balsamic Vinegar | Groceries | $60 | $12 |
| Coconut Oil | Oil | $60 | $12 |
| Hummus | ReadyMeals | $60 | $12 |
| Pesto | Preserves | $60 | $12 |
| Tomato Paste | Preserves | $60 | $12 |
| Brie | Dairy | $70 | $14 |
| Feta | Dairy | $70 | $14 |
| Gouda | Dairy | $70 | $14 |
| Mascarpone | Dairy | $70 | $14 |
| Mozzarella | Dairy | $70 | $14 |
| Ricotta | Dairy | $70 | $14 |
| Duck | Meat | $120 | $24 |
| Mackerel | Meat | $120 | $24 |
| Mussels | Meat | $120 | $24 |
| Squid | Meat | $120 | $24 |
| Trout | Meat | $120 | $24 |
| Turkey | Meat | $120 | $24 |
| Coconut Water | Drinks | $50 | $10 |
| Ginger Ale | Drinks | $50 | $10 |
| Grape Juice | Drinks | $50 | $10 |
| Kombucha | Drinks | $50 | $10 |
| Lemonade | Drinks | $50 | $10 |
| Tomato Juice | Drinks | $50 | $10 |
| Bay Leaves | Groceries | $40 | $8 |
| Black Pepper | Groceries | $40 | $8 |
| Cinnamon Sticks | Groceries | $40 | $8 |
| Paprika | Groceries | $40 | $8 |
| Turmeric | Groceries | $40 | $8 |
| Vanilla Pods | Groceries | $40 | $8 |
| Apple Pie | Bakery | $60 | $12 |
| Cannoli | Bakery | $60 | $12 |
| Madeleines | Bakery | $60 | $12 |
| Meringues | Bakery | $60 | $12 |
| Pancakes | Bakery | $60 | $12 |
| Waffles | Bakery | $60 | $12 |
| Caramels | Snacks | $40 | $8 |
| Jelly Beans | Snacks | $40 | $8 |
| Licorice | Snacks | $40 | $8 |
| Lollipops | Snacks | $40 | $8 |
| Marshmallows | Snacks | $40 | $8 |
| Popcorn | Snacks | $40 | $8 |
| Burrito | ReadyMeals | $80 | $16 |
| Calzone | ReadyMeals | $80 | $16 |
| Falafel | ReadyMeals | $80 | $16 |
| Onigiri | ReadyMeals | $80 | $16 |
| Samosas | ReadyMeals | $80 | $16 |
| Spring Rolls | ReadyMeals | $80 | $16 |
| Almond Butter | Preserves | $70 | $14 |
| Cashew Butter | Preserves | $70 | $14 |
| Chocolate Spread | Preserves | $70 | $14 |
| Fruit Leather | Snacks | $70 | $14 |
| Halva | Snacks | $70 | $14 |
| Sunflower Seed Butter | Preserves | $70 | $14 |
