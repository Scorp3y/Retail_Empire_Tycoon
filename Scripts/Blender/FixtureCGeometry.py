GROUP='C'
SCRIPT_NAME='BuildFixturesC.py'

def pegboard_display():
    parts=[];frame_posts(parts,1.0,.40,1.70,False)
    parts += [fbox('Cream pegboard panel',(0,.135,.94),(.86,.040,1.42)),
              fbox('Green pegboard base',(0,0,.08),(.94,.36,.16),'dark_green')]
    for i in range(8):
        x=-.35+i*.10
        for j in range(10):
            z=.37+j*.125
            obj=make_disk('Lightweight dark perforation',(x*AUTHOR_SCALE,.113*AUTHOR_SCALE,z*AUTHOR_SCALE),
                          (.006*AUTHOR_SCALE,.006*AUTHOR_SCALE),make_material('ink'),vertices=6)
            obj.rotation_euler.x=math.pi/2;apply_transform(obj);parts.append(obj)
    products=['Expansion_04_PersonalCare/toothbrush.glb','Wave2_07_Stationery/pens.glb','Wave2_07_Stationery/markers.glb']
    for z in (.87,1.36):
        for i in range(4):
            x=-.4+.8*(i+.5)/4
            parts.append(frib('Real pegboard hook',[(x,.12,z-.04),(x,-.08,z-.04),(x,-.11,z),(x,-.10,z+.015)],.009,'metal'))
        slots_row(z+.015,.8,4,-.10,.12,.36,kind='hang',products=products)
    shelf_board(parts,.23,.88,.34,y=-.015,count=3,height=.35,products=['Wave2_07_Stationery/notebook.glb'])
    if ITERATION>=2:parts.append(fbox('Cream top pegboard trim',(0,.105,1.65),(.80,.025,.035)))
    return parts

def cleaning_tool_rack():
    parts=[];frame_posts(parts,1.0,.50,1.76,True)
    parts.append(fbox('Top hanging bar',(0,.215,1.76),(.94,.055,.075),'dark_green'))
    for side in (-1,1):parts.append(fbox('Wide stable rack foot',(side*.465,0,.045),(.080,.50,.09),'dark_green'))
    products=['Expansion_05_CleaningTools/broom.glb','Expansion_05_CleaningTools/mop.glb']
    for i in range(5):
        x=-.41+.82*(i+.5)/5
        parts.append(frib('Rounded tool hanging hook',[(x,.20,1.75),(x,-.05,1.75),(x,-.10,1.62),(x,-.08,1.59),(x,-.05,1.62)],.011,'metal'))
    slots_row(1.62,.82,5,-.05,.20,.70,kind='hang',products=products)
    shelf_board(parts,.18,.90,.43,count=3,height=.70,products=['Expansion_05_CleaningTools/bucket.glb','Expansion_05_CleaningTools/cleaning_spray.glb'])
    if ITERATION>=2:parts.append(fbox('Rear low stability brace',(0,.215,.12),(.89,.045,.045),'dark_green'))
    return parts

def pet_supplies_rack():
    parts=[];frame_posts(parts,1.2,.40,1.5,False)
    parts += [fbox('Cream pet rack back',(0,.167,.81),(1.08,.035,1.32)),
              fbox('Pet rack green base',(0,0,.08),(1.14,.38,.16),'dark_green')]
    for z,products in ((.22,['Wave2_05_PetSupplies/dog_food.glb','Wave2_05_PetSupplies/cat_food.glb']),
                       (.62,['Wave2_05_PetSupplies/pet_treats.glb','Wave2_05_PetSupplies/chew_toy.glb']),
                       (1.02,['Wave2_05_PetSupplies/pet_bowl.glb'])):
        shelf_board(parts,z,1.09,.32,y=-.025,count=3,height=.32,products=products)
    if ITERATION>=2:parts.append(fbox('Cream upper pet rack trim',(0,.145,1.47),(1.02,.028,.035)))
    return parts

def garden_display():
    parts=[];frame_posts(parts,1.3,.60,.80,False)
    products=['Expansion_07_GardenSupplies/potted_cactus.glb','Expansion_07_GardenSupplies/seeds.glb','Expansion_07_GardenSupplies/watering_can.glb']
    for z,depth,y0 in ((.28,.60,0),(.80,.48,.05)):
        plank_depth=(depth-.024)/3
        for i in range(3):
            y=y0-depth/2+plank_depth/2+i*(plank_depth+.012)
            parts.append(fbox('Real separated wooden plank',(0,y,z-.025),(1.28,plank_depth,.050),'peach'))
            slots_row(z,1.18,3,y,plank_depth-.02,.40,products=products)
    for side in (-1,1):parts.append(fbox('Rear garden rail post',(side*.615,.265,.945),(.07,.07,.29),'dark_green'))
    parts.append(fbox('Green garden back rail',(0,.265,1.065),(1.20,.055,.07),'dark_green'))
    if ITERATION>=2:parts.append(fbox('Warm wood rear display rail',(0,.233,1.02),(1.18,.025,.045),'peach'))
    return parts

JOBS=[('fixture_09_pegboard_display',pegboard_display,4000),('fixture_10_cleaning_tool_rack',cleaning_tool_rack,2500),
      ('fixture_11_pet_supplies_rack',pet_supplies_rack,2500),('fixture_12_garden_display',garden_display,2500)]
if __name__=='__main__':run_fixtures()
