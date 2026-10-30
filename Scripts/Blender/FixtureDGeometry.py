GROUP='D'
SCRIPT_NAME='BuildFixturesD.py'

def toy_cubby_shelf():
    parts=[]
    for side in (-1,1):
        parts.append(fbox('Rounded outer cubby side',(side*.465,0,.50),(.070,.40,1.0),'dark_green'))
        parts.append(fbox('Rounded outer cubby crossbar',(0,0,.50+side*.465),(.86,.40,.070),'dark_green'))
    parts.append(fbox('Cream cubby back',(0,.170,.50),(.86,.035,.86)))
    for x in (-.14,.14):parts.append(fbox('Cream vertical cubby divider',(x,.005,.50),(.035,.30,.86)))
    products=['Wave2_06_ToysGames/teddy_bear.glb','Wave2_06_ToysGames/toy_car.glb','Wave2_06_ToysGames/building_blocks.glb']
    for z in (.115,.535):
        parts.append(fbox('Cream cubby floor',(0,0,z-.0225),(.86,.32,.045)))
        slots_row(z,.84,3,-.015,.27,.35,products=products)
        for slot in SLOTS[-3:]:slot['space'][0]=.23
    if ITERATION>=2:parts.append(fbox('Cream upper inside lining',(0,0,.921),(.86,.32,.025)))
    return parts

def stationery_stand():
    parts=[]
    profile=[(-.25,.035),(.25,.035),(.25,1.20),(.14,1.20),(-.25,.45)]
    for side in (-1,1):parts.append(fside('Slanted green stationery side',profile,.045,side*.425,'dark_green'))
    parts.append(fbox('Low open box floor',(0,0,.075),(.80,.43,.050)))
    for y in (-.20,.20):parts.append(fbox('Low box lip',(0,y,.15),(.80,.030,.10)))
    slots_row(.10,.75,3,0,.30,.27,products=['Wave2_07_Stationery/notebook.glb'])
    products=['Wave2_07_Stationery/pencils.glb','Wave2_07_Stationery/pens.glb','Wave2_07_Stationery/markers.glb']
    for z,y in ((.45,-.17),(.80,-.015),(1.15,.14)):
        shelf_board(parts,z,.81,.18,y,count=3,height=.27,products=products)
    if ITERATION>=2:
        for z,y in ((.45,-.17),(.80,-.015),(1.15,.14)):
            for x in (-.135,.135):parts.append(fbox('Small stationery compartment divider',(x,y,z+.045),(.012,.15,.09)))
    return parts

def homeware_shelf():
    parts=[];frame_posts(parts,1.1,.40,1.50,False)
    products=['Expansion_08_HomeComfort/blanket.glb','Expansion_08_HomeComfort/cushion.glb',
              'Wave2_08_Kitchenware/saucepan.glb','Expansion_08_HomeComfort/table_lamp.glb']
    for z in (.22,.65,1.08):shelf_board(parts,z,1.0,.38,color='peach',count=2,height=.36,lip=False,products=products)
    parts.append(fbox('Open back upper frame brace',(0,.165,1.455),(1.00,.055,.045),'dark_green'))
    if ITERATION>=2:
        for x in (-.48,.48):parts.append(fbox('Rear stability support',(x,.162,.85),(.035,.045,1.15),'dark_green'))
    return parts

def baby_care_shelf():
    parts=[];frame_posts(parts,.90,.40,1.35,False)
    parts += [fbox('Cream baby shelf back',(0,.165,.76),(.79,.035,1.14)),
              fbox('Narrow stable baby shelf base',(0,0,.08),(.84,.36,.16),'dark_green')]
    rounded_sign(parts,.84,1.30,1.60,depth=.040,y=.13)
    rows=[(.24,['Expansion_06_BabyCare/diapers.glb']),
          (.65,['Expansion_06_BabyCare/baby_bottle.glb','Expansion_06_BabyCare/baby_wipes.glb']),
          (1.06,['Expansion_06_BabyCare/baby_food.glb','Expansion_06_BabyCare/baby_cereal.glb'])]
    for z,products in rows:shelf_board(parts,z,.79,.32,y=-.025,count=2,height=.33,products=products)
    if ITERATION>=2:parts.append(fbox('Cream rounded header lower trim',(0,.105,1.33),(.74,.030,.030)))
    return parts

JOBS=[('fixture_13_toy_cubby_shelf',toy_cubby_shelf,2500),('fixture_14_stationery_stand',stationery_stand,2500),
      ('fixture_15_homeware_shelf',homeware_shelf,2500),('fixture_16_baby_care_shelf',baby_care_shelf,2500)]
if __name__=='__main__':run_fixtures()
