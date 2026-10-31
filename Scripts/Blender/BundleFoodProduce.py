"""Build self-contained category scripts from established helpers and whole foods."""
from pathlib import Path
import subprocess
import sys

root=Path(__file__).resolve().parents[2]
groups=[
 ('FoodRoots','Food_01A_RootsSquash',[('pumpkin','pumpkin',700),('sweet_potato','sweet_potato',700),('turnip','turnip',700),('parsnips','parsnip',700),('kohlrabi','kohlrabi',1200),('fennel','fennel',1200)]),
 ('FoodGreens','Food_01B_GreensVegetables',[('leek','leek',700),('celery','celery',700),('asparagus','asparagus',1200),('artichoke','artichoke',700),('brussels_sprouts','sprouts',1200),('romaine_lettuce','romaine',700)]),
 ('FoodTropical','Food_01C_TropicalFruit',[('passion_fruit','passion',1200),('dragon_fruit','dragon',1200),('lychee','lychee',1200),('guava','guava',700),('starfruit','starfruit',700),('pomelo','pomelo',700)]),
 ('FoodBerries','Food_01D_SmallFruitBerries',[('figs','figs',700),('dates','dates',1200),('kumquats','kumquats',700),('gooseberries',"lambda:berries('gooseberries')",1200),('cranberries',"lambda:berries('cranberries')",1200),('redcurrants',"lambda:berries('redcurrants')",1200)])]
geometry=(root/'Scripts/Blender/FoodProduceGeometry.py').read_text()
for name,folder,jobs in groups:
    source=root/f'Scripts/Blender/{name}Geometry.py'
    source.write_text(geometry+f'\nCATEGORY_FOLDER={folder!r}\nSCRIPT_NAME={"Build"+name+".py"!r}\n'+
        'JOBS=['+','.join(f'({key!r},{builder},{budget})' for key,builder,budget in jobs)+']\n'+
        "if __name__=='__main__':run_category()\n",encoding='utf-8')
    subprocess.run([sys.executable,str(root/'Scripts/Blender/BuildExpansionBundle.py'),name,folder],check=True)
