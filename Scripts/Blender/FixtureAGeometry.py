GROUP='A'
SCRIPT_NAME='BuildFixturesA.py'
JARS=['Wave2_03_Preserves/honey_jar.glb','Wave2_03_Preserves/jam_jar.glb','Wave2_03_Preserves/canned_corn.glb']
SAUCES=['Expansion_03_SaucesCondiments/ketchup.glb','Expansion_03_SaucesCondiments/mustard.glb']

def wall_shelf():
    parts=[];frame_posts(parts,1.2,.4,1.64,False)
    parts += [fbox('Cream back panel',(0,.167,.87),(1.09,.035,1.52)),fbox('Wide stable base',(0,0,.10),(1.14,.38,.08),'dark_green'),fbox('Cream top header',(0,0,1.67),(1.20,.40,.06))]
    for z in (.25,.70,1.15):shelf_board(parts,z,1.09,.32,y=-.025,count=4,products=JARS)
    if ITERATION>=2:parts.append(fbox('Cream base front inset',(0,-.202,.10),(.96,.012,.035)))
    return parts

def double_sided_aisle():
    parts=[fbox('Wide island base',(0,0,.08),(1.20,.70,.16),'dark_green')]
    for x in (-.535,0,.535):parts.append(fbox('Open central support',(x,0,.8),(.07,.07,1.60),'dark_green'))
    for z in (.25,.70,1.15):
        for side in (-1,1):
            shelf_board(parts,z,1.09,.30,y=side*.185,count=4,lip=False,products=SAUCES)
            if side==1:
                for slot in SLOTS[-4:]:slot['rotation_z']=math.pi
            parts.append(fbox('Outward shelf lip',(0,side*.323,z+.012),(1.09,.024,.024)))
        for x in (-.50,.50):parts.append(fbox('Open shelf cross support',(x,0,z-.064),(.035,.62,.028),'dark_green'))
    if ITERATION>=2:parts.append(fbox('Central top crossbar',(0,0,1.56),(1.09,.055,.055),'dark_green'))
    return parts

def aisle_endcap():
    parts=[];frame_posts(parts,.90,.50,1.42,False)
    parts += [fbox('Endcap cream back',(0,.20,.78),(.78,.035,1.24)),fbox('Endcap base',(0,0,.08),(.84,.46,.16),'dark_green')]
    rounded_sign(parts,.84,1.38,1.70,y=.14)
    for z in (.23,.65,1.07):shelf_board(parts,z,.79,.42,y=-.025,count=3,products=SAUCES)
    for side in (-1,1):
        for z in (.73,1.15):parts.append(frib('Rounded side handle',[(side*.415,.15,z),(side*.415,-.12,z),(side*.415,-.15,z-.055)],.012))
    if ITERATION>=2:parts.append(fbox('Cream sign lower rail',(0,.11,1.40),(.74,.05,.035)))
    return parts

def promotion_bin():
    parts=[];frame_posts(parts,1.0,.70,.60,False)
    parts.append(fbox('Real cream bin floor',(0,0,.275),(.90,.60,.050)))
    for side in (-1,1):
        parts.append(fbox('Cream inner long wall',(0,side*.302,.35),(.90,.035,.40)))
        parts.append(fbox('Cream inner end wall',(side*.452,0,.35),(.035,.60,.40)))
        parts.append(fbox('Chunky long top rim',(0,side*.315,.58),(1.004,.074,.06),'dark_green'))
        parts.append(fbox('Chunky short top rim',(side*.465,0,.58),(.074,.556,.06),'dark_green'))
    products=['Wave2_05_PetSupplies/pet_treats.glb','Wave2_05_PetSupplies/cat_food.glb']
    for y in (-.135,.135):slots_row(.30,.84,3,y,.25,.26,products=products)
    if ITERATION>=2:
        for side in (-1,1):parts.append(fbox('Cream inner rim insert',(0,side*.275,.557),(.88,.035,.035)))
    return parts

JOBS=[('fixture_01_wall_shelf',wall_shelf,2500),('fixture_02_double_sided_aisle',double_sided_aisle,2500),('fixture_03_aisle_endcap',aisle_endcap,2500),('fixture_04_promotion_bin',promotion_bin,2500)]
if __name__=='__main__':run_fixtures()
