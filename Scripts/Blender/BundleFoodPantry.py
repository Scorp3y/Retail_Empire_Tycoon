"""Assemble standalone scripts using the exact previous exporter and helpers."""
from pathlib import Path
import subprocess
import sys
root=Path(__file__).resolve().parents[2]
source=(root/'Scripts/Blender/FoodPantryGeometry.py').read_text()
groups=[('PantryGrains','Food_05_Grains',[(name,f'lambda:grain({i})',900) for i,name in enumerate(['quinoa','bulgur','millet','pearl_barley','semolina','polenta'])]),
 ('PantryLegumes','Food_06_DriedLegumes',[(name,f'lambda:legume({i})',1200) for i,name in enumerate(['chickpeas','green_lentils','red_kidney_beans','white_beans','black_beans','dried_peas'])]),
 ('PantryPlant','Food_07_PlantBasedSweet',[
  ('coconut_milk',"lambda:can_food(.11,.035,'ivory','coconut','dark_brown','blue')",900),
  ('oat_milk',"lambda:carton('oat')",900),('almond_milk',"lambda:carton('almond')",900),
  ('maple_syrup',"lambda:bottle('maple')",1200),('molasses',"lambda:bottle('molasses')",900),
  ('tahini',"lambda:jar_food('Tahini',.10,.036,'grain','sage','sesame','grain')",900)]),
 ('PantryCooking','Food_08_CookingEssentials',[
  ('hummus','hummus',1200),('pesto',"lambda:jar_food('Pesto',.09,.030,'sage','dark_green','leaf','sage')",900),
  ('tomato_paste',"lambda:can_food(.08,.032,'red','tomato','ivory')",900),
  ('coconut_oil',"lambda:jar_food('Coconut oil',.10,.039,'ivory','sage','coconut','dark_brown')",900),
  ('apple_cider_vinegar',"lambda:bottle('apple')",900),('balsamic_vinegar',"lambda:bottle('balsamic')",900)])]
for name,folder,jobs in groups:
    path=root/f'Scripts/Blender/{name}Geometry.py'
    path.write_text(source+f'\nCATEGORY_FOLDER={folder!r}\nSCRIPT_NAME={"Build"+name+".py"!r}\n'+
      'JOBS=['+','.join(f'({key!r},{builder},{budget})' for key,builder,budget in jobs)+']\n'+
      "if __name__=='__main__':run_category()\n",encoding='utf-8')
    subprocess.run([sys.executable,str(root/'Scripts/Blender/BuildExpansionBundle.py'),name,folder],check=True)
