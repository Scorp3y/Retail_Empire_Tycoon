"""Separate player-facing browsing departments from existing storage/save enums."""
from pathlib import Path
import json,re
ROOT=Path(__file__).resolve().parents[1]
types={0:('Pantry','Essentials'),1:('Bakery & Desserts','Bread'),2:('Drinks','Drinks'),3:('Pantry','Oils & Vinegar'),
4:('Meat & Seafood','Meat & Poultry'),5:('Dairy & Eggs','Dairy'),6:('Fruit & Vegetables','Vegetables'),
7:('Pantry','Essentials'),8:('Frozen','Frozen food'),9:('Snacks & Sweets','Snacks'),10:('Household & Care','Household'),
11:('Preserves & Sauces','Sauces'),12:('Ready Meals','Prepared meals'),13:('Pet Supplies','Food & Accessories'),
14:('Toys & Games','Toys'),15:('Stationery','Writing & Paper'),16:('Home & Garden','Kitchenware')}
overrides={}
groups={'Food_01A_RootsSquash':('Fruit & Vegetables','Vegetables'),'Food_01B_GreensVegetables':('Fruit & Vegetables','Vegetables'),
'Food_01C_TropicalFruit':('Fruit & Vegetables','Fruits'),'Food_01D_SmallFruitBerries':('Fruit & Vegetables','Berries & Small fruits'),
'Food_05_Grains':('Pantry','Grains & Pasta'),'Food_06_DriedLegumes':('Pantry','Legumes'),
'Food_07_PlantBasedSweet':('Preserves & Sauces','Sweet spreads'),'Food_08_CookingEssentials':('Preserves & Sauces','Sauces'),
'Food_09_CheeseDairy':('Dairy & Eggs','Cheese'),'Food_10_MeatSeafood':('Meat & Seafood','Seafood'),
'Food_11_Drinks':('Drinks','Juices & Soda'),'Food_12_Spices':('Pantry','Spices'),
'Food_13_Bakery':('Bakery & Desserts','Pastries & Cakes'),'Food_14_SweetSnacks':('Snacks & Sweets','Candy & Treats'),
'Food_15_ReadyFood':('Ready Meals','Prepared meals'),'Food_16_SpreadsTreats':('Preserves & Sauces','Sweet spreads'),
'Expansion_04_PersonalCare':('Household & Care','Personal care'),'Expansion_05_CleaningTools':('Household & Care','Cleaning'),
'Expansion_06_BabyCare':('Household & Care','Baby care'),'Expansion_07_GardenSupplies':('Home & Garden','Gardening'),
'Expansion_08_HomeComfort':('Home & Garden','Home accessories'),'Expansion_02_IceCreamDesserts':('Frozen','Ice cream'),
'Expansion_01_BreadPastries':('Bakery & Desserts','Pastries & Cakes'),'Wave2_02_FreshProduce':('Fruit & Vegetables','Fruits'),
'Wave2_03_Preserves':('Preserves & Sauces','Preserves & Spreads')}
for folder,pair in groups.items():
 for path in (ROOT/'ArtSource'/folder).glob('*.glb'):overrides['product_'+path.stem]=pair
for key in ['apple','banana','pear','orange','lemon','grape','kiwi','peaches','plums','mango','pomelo','guava','starfruit','figs','dates','kumquats']:
 overrides['product_'+key]=('Fruit & Vegetables','Fruits')
for key in ['oat_milk','almond_milk']:overrides['product_'+key]=('Drinks','Plant-based drinks')
for key in ['turkey','duck']:overrides['product_'+key]=('Meat & Seafood','Meat & Poultry')
for key in ['ricotta','mascarpone']:overrides['product_'+key]=('Dairy & Eggs','Dairy')
for key in ['coconut_oil','apple_cider_vinegar','balsamic_vinegar']:overrides['product_'+key]=('Pantry','Oils & Vinegar')
for key in ['halva','fruit_leather']:overrides['product_'+key]=('Snacks & Sweets','Candy & Treats')
overrides['product_eggs']=('Dairy & Eggs','Eggs');overrides['product_hummus']=('Ready Meals','Soups & Salads')
overrides['product_coconut_milk']=('Pantry','Cooking ingredients')
for key in ['trout','mackerel']:overrides['product_'+key]=('Meat & Seafood','Fish')
for key in ['honey_jar','jam_jar','peanut_butter_jar']:overrides['product_'+key]=('Preserves & Sauces','Sweet spreads')
records={}
for folder in ['Assets/Prefabs/Product','Assets/Resources/StoreAssortment']:
 for path in (ROOT/folder).rglob('*.asset'):
  if path.stat().st_size>30000:continue
  text=path.read_text(encoding='utf-8-sig');identity=re.search(r'^  id: (product_\w+)',text,re.M);storage=re.search(r'^  storageType: (\d+)',text,re.M)
  if not identity or not storage:continue
  key=identity[1];department,section=overrides.get(key,types[int(storage[1])])
  records[key]=dict(id=key,department=department,section=section)
target=ROOT/'Assets/Resources/ShopUi/ProductDepartments.json'
target.write_text(json.dumps(dict(products=sorted(records.values(),key=lambda e:e['id'])),indent=2),encoding='utf-8')
print('Defined browsing departments for',len(records),'distinct product IDs.')
