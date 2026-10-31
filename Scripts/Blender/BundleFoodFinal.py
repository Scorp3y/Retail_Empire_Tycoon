"""Self-contained final-wave category scripts, sharing all previous helpers."""
from pathlib import Path
import subprocess
import sys
root=Path(__file__).resolve().parents[2]
shared='\n'.join((root/'Scripts/Blender'/file).read_text() for file in
                 ('FoodPantryGeometry.py','FoodDairyProteinGeometry.py','FoodBakerySnacksGeometry.py'))
groups=[('FoodBakery','Food_13_Bakery',[(n,n,1200 if n!='madeleines' else 900) for n in ['waffles','pancakes','apple_pie','madeleines','meringues','cannoli']]),
 ('FoodSweets','Food_14_SweetSnacks',[(n,f'lambda:sweets({n!r})',1200 if n in ('marshmallows','jelly_beans','lollipops') else 900)
  for n in ['popcorn','marshmallows','jelly_beans','licorice','caramels','lollipops']]),
 ('FoodReady','Food_15_ReadyFood',[(n,f'lambda:ready_food({n!r})',1200 if n=='falafel' else 900)
  for n in ['burrito','calzone','samosas','spring_rolls','falafel','onigiri']]),
 ('FoodSpreads','Food_16_SpreadsTreats',[(n,f'lambda:spread_jar({n!r})',900)
  for n in ['chocolate_spread','almond_butter','cashew_butter','sunflower_seed_butter']]+[('halva','halva',900),('fruit_leather','fruit_leather',900)])]
for name,folder,jobs in groups:
    (root/f'Scripts/Blender/{name}Geometry.py').write_text(shared+f'\nCATEGORY_FOLDER={folder!r}\nSCRIPT_NAME={"Build"+name+".py"!r}\n'+
       'ITERATION=3\nJOBS=['+','.join(f'({key!r},{builder},{budget})' for key,builder,budget in jobs)+']\n'+
       "if __name__=='__main__':run_category()\n",encoding='utf-8')
    subprocess.run([sys.executable,str(root/'Scripts/Blender/BuildExpansionBundle.py'),name,folder],check=True)
