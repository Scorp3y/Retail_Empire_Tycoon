"""Bundle part three with the original palette, packaging and export helpers."""
from pathlib import Path
import subprocess
import sys
root=Path(__file__).resolve().parents[2]
shared='\n'.join((root/'Scripts/Blender'/file).read_text() for file in
    ['FoodPantryGeometry.py','FoodDairyProteinGeometry.py','FoodPart3Revision.py'])
groups=[('FoodDairy','Food_09_CheeseDairy',[
 ('ricotta',"lambda:dairy_tub('ricotta')",900),('mozzarella','mozzarella',1200),('feta','feta',900),
 ('brie',"lambda:cheese_wheel('brie')",1200),('gouda',"lambda:cheese_wheel('gouda')",900),('mascarpone',"lambda:dairy_tub('mascarpone')",900)]),
 ('FoodProtein','Food_10_MeatSeafood',[(name,f'lambda:protein({name!r})',1200) for name in ['turkey','duck','trout','mackerel','mussels','squid']]),
 ('FoodDrinks','Food_11_Drinks',[(name,f'lambda:drink_bottle({name!r})',900) for name in ['lemonade','ginger_ale','kombucha']]+
  [(name,f'lambda:juice_carton({name!r})',900) for name in ['coconut_water','grape_juice','tomato_juice']]),
 ('FoodSpices','Food_12_Spices',[(name,f'lambda:spice_jar({name!r})',1200 if name in ('cinnamon_sticks','black_pepper') else 900)
  for name in ['cinnamon_sticks','vanilla_pods','turmeric','paprika','black_pepper']]+[('bay_leaves','bay_leaves',900)])]
for name,folder,jobs in groups:
    (root/f'Scripts/Blender/{name}Geometry.py').write_text(shared+f'\nCATEGORY_FOLDER={folder!r}\nSCRIPT_NAME={"Build"+name+".py"!r}\n'+
        'ITERATION=6\nJOBS=['+','.join(f'({key!r},{builder},{budget})' for key,builder,budget in jobs)+']\n'+
        "if __name__=='__main__':run_category()\n",encoding='utf-8')
    subprocess.run([sys.executable,str(root/'Scripts/Blender/BuildExpansionBundle.py'),name,folder],check=True)
