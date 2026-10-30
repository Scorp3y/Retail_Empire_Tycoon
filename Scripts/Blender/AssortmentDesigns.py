"""Approved assortment: names, packaging, economics and physical cargo sizes.

Prices are per unit here; Unity converts cost into the price of one ten-unit carton.
Storage names are serialized enum names, never their positional indices.
"""

COLORS = {
    'cream': '#e8dfbd', 'ivory': '#f6edd8', 'green': '#294f40',
    'sage': '#83a66c', 'metal': '#758481', 'dark': '#263632',
    'wood': '#b98545', 'wood_dark': '#82582f', 'red': '#c64235',
    'berry': '#e25745', 'blue': '#357ba5', 'blue_dark': '#344c70',
    'pale_blue': '#b3d6df', 'yellow': '#e9bc46', 'orange': '#dd8950',
    'white': '#f1f0df', 'brown': '#73513c', 'pink': '#d1877a',
    'salmon': '#ec966b', 'meat': '#b95755', 'leaf': '#588443',
    'purple': '#8b6b99', 'teal': '#529e93', 'lime': '#a8bb5e',
    'beige': '#c8b58b', 'silver': '#b3bab4', 'clear': '#d2e3dc',
    'chocolate': '#60402d', 'coffee': '#4a362b', 'gold': '#be913e',
    'peach': '#e9bb8d', 'navy': '#34526b',
}

# key, display name, storage, geometry, primary palette colour, unit cost/sale,
# unit weight in kg, packed volume in cubic metres.
ROWS = [
 ('strawberries','Strawberries','Produce','berries','red',5,9,.25,.0015),
 ('blueberries','Blueberries','Produce','berries','blue_dark',6,10,.20,.0012),
 ('pineapple','Pineapple','Produce','pineapple','yellow',5,9,1.2,.004),
 ('watermelon','Watermelon','Produce','watermelon','green',7,12,3,.008),
 ('cabbage','Cabbage','Produce','cabbage','sage',2,5,1,.003),
 ('mushrooms','Mushrooms','Produce','mushrooms','ivory',4,7,.30,.002),
 ('yogurt','Yogurt','Dairy','tub','blue',3,5,.20,.0006),
 ('butter','Butter','Dairy','block','yellow',4,7,.20,.0004),
 ('cream','Cream','Dairy','carton','blue',4,7,.50,.001),
 ('cottage_cheese','Cottage cheese','Dairy','open_tub','blue',4,7,.25,.0008),
 ('sour_cream','Sour cream','Dairy','tub','sage',3,6,.30,.0008),
 ('kefir','Kefir','Dairy','bottle','white',3,6,.95,.0015),
 ('salmon','Salmon','Meat','salmon','salmon',12,20,.40,.0018),
 ('tuna','Tuna','Meat','tuna','meat',11,19,.40,.0018),
 ('shrimp','Shrimp','Meat','shrimp','salmon',10,17,.30,.0018),
 ('bacon','Bacon','Meat','bacon','pink',6,10,.30,.0014),
 ('ground_beef','Ground beef','Meat','mince','meat',7,12,.50,.0018),
 ('drumsticks','Drumsticks','Meat','drumsticks','peach',5,9,.60,.002),
 ('pasta','Pasta','Groceries','window_bag','green',2,4,.50,.0018),
 ('rice','Rice','Groceries','bag','green',2,4,1,.002),
 ('flour','Flour','Groceries','paper_bag','cream',2,4,1,.002),
 ('sugar','Sugar','Groceries','paper_bag','white',2,4,1,.002),
 ('oats','Oats','Groceries','box','beige',3,5,.50,.002),
 ('canned_beans','Canned beans','Groceries','can','green',2,4,.40,.0006),
 ('cookies','Cookies','Snacks','cookies','brown',3,6,.25,.0014),
 ('crackers','Crackers','Snacks','box','red',3,5,.25,.0015),
 ('chips','Chips','Snacks','pouch','yellow',3,6,.15,.003),
 ('chocolate','Chocolate','Snacks','chocolate','chocolate',3,6,.10,.0003),
 ('nuts','Nuts','Snacks','window_bag','beige',5,9,.25,.001),
 ('gummies','Gummies','Snacks','window_bag','red',3,6,.20,.001),
 ('water','Water','Drinks','bottle','pale_blue',1,3,1,.0016),
 ('sparkling_water','Sparkling water','Drinks','bottle','green',2,4,1,.0016),
 ('iced_tea','Iced tea','Drinks','bottle','orange',3,6,.50,.001),
 ('energy_drink','Energy drink','Drinks','can','purple',4,7,.33,.0006),
 ('coffee','Coffee','Groceries','bag','brown',7,12,.25,.0014),
 ('tea','Tea','Groceries','box','sage',4,7,.10,.001),
 ('frozen_pizza','Frozen pizza','Frozen','pizza_box','red',6,11,.50,.0035),
 ('dumplings','Dumplings','Frozen','window_bag','blue',5,9,.70,.0025),
 ('mixed_vegetables','Mixed vegetables','Frozen','window_bag','sage',3,6,.50,.002),
 ('frozen_berries','Frozen berries','Frozen','window_bag','blue',5,9,.40,.002),
 ('fries','Fries','Frozen','window_bag','red',3,6,.70,.0025),
 ('fish_sticks','Fish sticks','Frozen','box','blue',5,9,.40,.0018),
 ('laundry_detergent','Laundry detergent','Household','jug','blue',9,16,1.5,.003),
 ('dish_soap','Dish soap','Household','soap','green',4,7,.50,.001),
 ('sponges','Sponges','Household','sponges','yellow',2,5,.08,.001),
 ('paper_towels','Paper towels','Household','roll','white',3,6,.25,.004),
 ('toilet_paper','Toilet paper','Household','roll_pack','white',4,8,.50,.008),
 ('shampoo','Shampoo','Household','bottle','cream',5,9,.40,.001),
]

FIXTURES = [
 dict(key='chilled_counter',name='Chilled counter',shelfType='ChilledCounter',
      accepts=['Meat'],price=650,weight=90,volume=.9,width=1.10,depth=.60,height=.80,
      footprintX=5,footprintZ=3,capacity=12,columns=4,rows=3,tiers=1,
      slotY=.43,slotZ=-.17,spacingZ=.16,slotWidth=.23,slotDepth=.14,slotHeight=.13,
      description='Refrigerated display for meat and seafood. Stock one product at a time.'),
 dict(key='chest_freezer',name='Chest freezer',shelfType='ChestFreezer',
      accepts=['Frozen'],price=550,weight=75,volume=.8,width=1.03,depth=.60,height=.67,
      footprintX=4,footprintZ=3,capacity=12,columns=4,rows=3,tiers=1,
      slotY=.39,slotZ=-.17,spacingZ=.16,slotWidth=.21,slotDepth=.14,slotHeight=.17,
      description='Sliding-lid freezer for frozen food. Stock one product at a time.'),
 dict(key='household_rack',name='Household rack',shelfType='Household',
      accepts=['Household'],price=350,weight=55,volume=.6,width=.84,depth=.44,height=1.03,
      footprintX=4,footprintZ=2,capacity=24,columns=4,rows=2,tiers=3,
      slotY=.13,slotZ=-.10,spacingZ=.20,slotWidth=.175,slotDepth=.18,slotHeight=.23,
      description='Separate shelving for cleaning products and personal care.'),
 dict(key='snack_endcap',name='Snack endcap',shelfType='SnackEndcap',
      accepts=['Snacks','Groceries'],price=280,weight=35,volume=.45,width=.69,depth=.44,height=.88,
      footprintX=3,footprintZ=2,capacity=18,columns=3,rows=2,tiers=3,
      slotY=.13,slotZ=-.10,spacingZ=.20,slotWidth=.18,slotDepth=.18,slotHeight=.18,
      description='Compact endcap for snacks, sweets and dry groceries.'),
]

def products():
    keys = ('key','name','storage','shape','color','cost','sale','weight','volume')
    return [dict(zip(keys,row),index=index) for index,row in enumerate(ROWS)]

def manifest():
    return dict(version=1,products=products(),fixtures=FIXTURES)
