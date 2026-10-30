"""Six approved bakery props. Costs are per unit; the game orders cartons of ten."""
import argparse
import json
import sys
from pathlib import Path

PALETTE = {
    'dough':'#d59b56', 'baked':'#bd823e', 'light_dough':'#e2ae6a',
    'cream':'#f4e8c8', 'paper':'#e6d5b7', 'paper_shade':'#d4c09e',
    'rose':'#d39790', 'icing':'#e7aaa5', 'berry':'#ba493e',
    'leaf':'#608546', 'cocoa':'#71513e', 'blue':'#99bbce',
    'mint':'#b2c28b', 'yellow':'#ecd58e', 'ivory':'#f8efdc', 'filling':'#b86e65'
}

PRODUCTS = [
    dict(key='croissant',name='Croissant',storage='Bakery',cost=4,sale=7,weight=.10,volume=.0011),
    dict(key='doughnut',name='Doughnut',storage='Bakery',cost=3,sale=6,weight=.09,volume=.0008),
    dict(key='muffin',name='Muffin',storage='Bakery',cost=4,sale=7,weight=.12,volume=.0010),
    dict(key='pretzel',name='Pretzel',storage='Bakery',cost=3,sale=6,weight=.12,volume=.0011),
    dict(key='cupcake',name='Cupcake',storage='Bakery',cost=5,sale=9,weight=.12,volume=.0011),
    dict(key='cake_slice',name='Cake slice',storage='Bakery',cost=6,sale=11,weight=.18,volume=.0014),
]

def prepare(root):
    from PIL import Image,ImageDraw
    folder=Path(root)/'ArtSource/Bakery';folder.mkdir(parents=True,exist_ok=True)
    palette=Image.new('RGB',(256,256));draw=ImageDraw.Draw(palette)
    for index,color in enumerate(PALETTE.values()):
        x=index%4*64;y=index//4*64;draw.rectangle((x,y,x+63,y+63),fill=color)
    palette.save(folder/'BakeryPalette.png')
    (folder/'Definitions.json').write_text(json.dumps(dict(products=PRODUCTS),indent=2)+'\n',encoding='utf-8')
    print('Prepared six bakery products and sixteen matte colours.')

if __name__=='__main__':
    sys.dont_write_bytecode=True
    parser=argparse.ArgumentParser();parser.add_argument('--project',required=True)
    prepare(parser.parse_args().project)
