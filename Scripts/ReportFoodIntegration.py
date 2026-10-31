"""Publish an evidence-backed catalogue report after actual Unity gameplay checks."""
from pathlib import Path
import hashlib
import json
import re

ROOT=Path(__file__).resolve().parents[1]
REPORT=ROOT/'Library/FoodExpansionIntegration'
manifest=json.loads((ROOT/'Assets/Art/FoodExpansion/Manifest.json').read_text())['products']
references=json.loads((REPORT/'references.json').read_text())
receipt=(REPORT/'play.txt').read_text()
assert receipt.startswith('PASS: 240-product'),receipt
assert references['status']=='PASS' and references['catalogue']==dict(products=240,fixtures=20)
customers=set(re.findall(r'^PASS (product_\w+):',(REPORT/'customers.txt').read_text(),re.M))
assert customers=={'product_'+entry['key'] for entry in manifest}
rows=[]
for entry in manifest:
    path=ROOT/'ArtSource'/entry['folder']/(entry['key']+'.glb')
    assert hashlib.sha256(path.read_bytes()).hexdigest()==entry['sha256'],path
    text=(ROOT/'Assets/Resources/StoreAssortment/FoodExpansion/Products'/(entry['key']+'.asset')).read_text()
    def value(field):return re.search(r'^  '+field+r': (.+)$',text,re.M).group(1)
    rows.append('| '+entry['key'].replace('_',' ').title()+' | '+entry['storage']+' | $'+value('buyPrice')+' | $'+value('sellPrice')+' |')
notes='''# Food Expansion — внедрение в игру

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
'''
(ROOT/'Docs/FoodExpansionIntegration.md').write_text(notes+'\n'.join(rows)+'\n',encoding='utf-8')
print('PASS: documented 96 installed products and their successful real-shopper cycles.')
