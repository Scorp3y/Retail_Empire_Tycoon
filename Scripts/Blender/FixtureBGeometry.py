GROUP='B'
SCRIPT_NAME='BuildFixturesB.py'
BAKERY=['Expansion_01_BreadPastries/bagel.glb','Expansion_01_BreadPastries/baguette.glb','Expansion_01_BreadPastries/eclair.glb']
PRODUCE=['FreshProduceGLB/Peaches_Pair.glb','FreshProduceGLB/Mango_Whole_and_Half.glb','FreshProduceGLB/Plums_Pair.glb']
MEALS=['Wave2_04_ReadyMeals/sushi.glb','Wave2_04_ReadyMeals/ready_salad.glb','Wave2_04_ReadyMeals/sandwich.glb']

def bakery_rack():
    parts=[];frame_posts(parts,1.0,.50,1.48,False)
    for z in (.24,.68,1.12):display_tray(parts,z,.90,.48,0,BAKERY)
    rounded_sign(parts,1.0,1.47,1.60,depth=.50,y=0,color='cream')
    if ITERATION>=2:
        for x in (-.43,.43):parts.append(fbox('Bakery low support foot',(x,0,.06),(.095,.46,.10),'dark_green'))
    return parts

def produce_stand():
    parts=[]
    profile=[(-.35,.03),(.35,.03),(.35,.90),(.23,.90),(-.35,.14)]
    for side in (-1,1):parts.append(fside('Green triangular step support',profile,.055,side*.50,'dark_green'))
    for z,y in ((.14,-.22),(.44,0),(.74,.22)):display_tray(parts,z,1.02,.25,y,PRODUCE,4,True)
    if ITERATION>=2:
        for x in (-.48,.48):parts.append(fbox('Crate back upright',(x,.33,.49),(.035,.035,.80),'dark_green'))
    return parts

def chilled_display():
    parts=[];fixture_glass()
    parts += [fbox('Fridge green base',(0,0,.12),(1.20,.60,.24),'dark_green'),
              fbox('Cream inner back',(0,.265,.94),(1.08,.055,1.37)),
              fbox('Cream fridge canopy',(0,0,1.65),(1.20,.60,.10))]
    for side in (-1,1):
        parts.append(fbox('Green rear fridge upright',(side*.56,.26,.95),(.08,.07,1.45),'dark_green'))
        profile=[(-.27,.24),(.235,.24),(.235,1.58),(-.06,1.58),(-.16,1.48),(-.23,1.30),(-.27,1.05)]
        parts.append(fside('Curved pale glass side',profile,.014,side*.55,'fixture_glass'))
    for z in (.28,.72,1.16):shelf_board(parts,z,1.06,.49,y=-.015,count=4,height=.35,products=MEALS)
    parts.append(fbox('Dark vent recess',(0,-.304,.125),(1.06,.014,.14),'ink'))
    for z in (.073,.108,.143,.178):parts.append(fbox('Real vent grille bar',(0,-.317,z),(1.02,.018,.013),'dark_green'))
    if ITERATION>=2:parts.append(fbox('Canopy green front accent',(0,-.307,1.65),(1.05,.014,.045),'dark_green'))
    return parts

def glass_top_freezer():
    parts=[];fixture_glass()
    parts.append(fbox('Freezer insulated lower shell',(0,0,.13),(1.38,.68,.26),'dark_green'))
    for side in (-1,1):
        parts.append(fbox('Green freezer long outer wall',(0,side*.315,.55),(1.40,.070,.60),'dark_green'))
        parts.append(fbox('Green freezer short outer wall',(side*.665,0,.55),(.070,.56,.60),'dark_green'))
        parts.append(fbox('Cream inner long wall',(0,side*.270,.55),(1.25,.018,.57)))
        parts.append(fbox('Cream inner end wall',(side*.620,0,.55),(.018,.53,.57)))
        parts.append(fbox('Cream top long rim',(0,side*.316,.859),(1.405,.079,.072)))
        parts.append(fbox('Cream top short rim',(side*.665,0,.859),(.079,.553,.072)))
    parts.append(fbox('Real freezer floor',(0,0,.57),(1.25,.53,.060)))
    products=['Expansion_02_IceCreamDesserts/ice_cream_tub.glb','Expansion_02_IceCreamDesserts/popsicle.glb']
    for y in (-.125,.125):slots_row(.60,1.18,3,y,.23,.25,products=products)
    for side in (-1,1):
        z=.880 if side==-1 else .904;x=side*.322
        parts.append(fbox('Transparent sliding glass lid',(x,0,z),(.620,.546,.010),'fixture_glass'))
        for yy in (-.279,.279):parts.append(fbox('Sliding lid long frame',(x,yy,z),(.644,.022,.020)))
        for xx in (x-.322,x+.322):parts.append(fbox('Sliding lid end frame',(xx,0,z),(.022,.536,.020)))
    parts.append(fbox('Dark front control panel',(.48,-.357,.24),(.13,.012,.060),'ink'))
    if ITERATION>=2:
        parts.append(fbox('Cream control indicator',(.48,-.365,.24),(.07,.006,.018)))
        for x,z in ((-.18,.898),(.18,.922)):parts.append(fbox('Lid grip',(x,-.16,z),(.12,.03,.022)))
    return parts

JOBS=[('fixture_05_bakery_rack',bakery_rack,4000),('fixture_06_produce_stand',produce_stand,4000),
      ('fixture_07_chilled_display',chilled_display,4000),('fixture_08_glass_top_freezer',glass_top_freezer,4000)]
if __name__=='__main__':run_fixtures()
