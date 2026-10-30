CATEGORY_FOLDER='Expansion_03_SaucesCondiments'
SCRIPT_NAME='BuildExpansionSauces.py'
ITERATION=2


def bottle_shell(name, profile, color, transparent=False):
    if transparent:
        mat=make_material(color,.25)
        mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.2
    else: mat=make_material(color)
    # Follow the outer profile, cross the lip and descend the separate inner wall.
    inner=[(z if i else .003,max(.001,r-.0012)) for i,(z,r) in enumerate(profile)]
    obj=lathe(name,profile+list(reversed(inner)),mat,10)
    obj['hollow_opening']=True
    return obj


def tomato_icon(y,z):
    return [colored_ball('Raised tomato icon',(0,y,z),(.010,.0016,.009),'cherry',8,4),
            leaf_icon(y-.001,z+.009,.009)]


def sauce_contents(height,radius,color):
    obj=lathe('Separate sauce filling',[(.003,radius-.002),(.005,radius),
             (height-.002,radius),(height,radius-.002)],make_material('cream'),8)
    return pigment(obj,color)


def cap_band(z,radius,color):
    return lathe('Rounded cap seating band',[(z-.0015,radius-.0005),
        (z-.001,radius),(z+.001,radius),(z+.0015,radius-.0005)],make_material(color),8)


def squeeze_bottle(kind):
    expansion_palette();color='cherry' if kind=='ketchup' else 'custard'
    profile=[(0,.021),(.003,.026),(.125,.026),(.145,.020),(.155,.010),(.161,.010)]
    parts=[bottle_shell('Hollow squeeze bottle',profile,color)]
    parts.append(lathe('Pointed dispensing cap',[(.159,.011),(.163,.012),(.170,.010),(.193,.003),(.200,.002)],make_material('cream' if kind=='ketchup' else color),10))
    parts.append(curved_label(.0265,.085,.040,.050,make_material('cream')))
    if kind=='ketchup':parts+=tomato_icon(-.027,.083)
    else:
        parts.append(leaf_icon(-.027,.084,.023))
        for x in (-.006,.006):parts.append(colored_ball('Mustard seed icon',(x,-.028,.074),(.003,.001,.003),'golden',6,3))
    if ITERATION>=2:
        parts.append(cap_band(.163,.012,'cream' if kind=='ketchup' else color))
        parts.append(sauce_contents(.121,.023,color))
    return parts


def ketchup():return squeeze_bottle('ketchup')
def mustard():return squeeze_bottle('mustard')


def mayonnaise():
    expansion_palette()
    parts=[bottle_shell('Clear mayonnaise jar',[(0,.030),(.004,.037),(.083,.037),(.097,.033),(.102,.032)],'plastic',True),
           lathe('Cream mayonnaise contents',[(.003,.028),(.006,.035),(.083,.035),(.093,.030)],make_material('cream'),10),
           cylinder('Blue lid',(0,0,.110),.037,.018,'blue',10),
           cylinder('Cream lid band',(0,0,.104),.0375,.003,'cream',10),
           curved_label(.0377,.055,.053,.044,make_material('blue'))]
    parts.append(front_shape('Cream mayonnaise dollop icon',[(-.010,.048),(-.008,.058),(0,.068),(.005,.061),(.011,.055),(.009,.048)],-.038,'cream'))
    if ITERATION>=2:parts.append(colored_ball('Small sage leaf on label',(.006,-.039,.048),(.006,.001,.003),'leaf',8,3))
    return parts


def soy_sauce():
    expansion_palette();PALETTE['soy_glass']=PALETTE['kiwi_skin']
    parts=[bottle_shell('Tinted dark glass soy bottle',[(0,.021),(.004,.025),(.120,.025),(.149,.011),(.179,.011)],'soy_glass',True)]
    contents=lathe('Dark soy contents',[(.003,.020),(.006,.023),(.119,.023),(.145,.009)],make_material('cream'),10)
    parts.append(pigment(contents,'dark_brown'))
    parts += [cylinder('Red soy cap',(0,0,.188),.013,.023,'cherry',10),
              curved_label(.0255,.075,.038,.052,make_material('cream'))]
    hexagon=[(.009*math.cos(i*math.tau/6),.075+.011*math.sin(i*math.tau/6)) for i in range(6)]
    parts.append(front_shape('Hexagonal soy icon',hexagon,-.026,'dark_brown'))
    if ITERATION>=2:parts.append(leaf_icon(-.027,.075,.012,'cream'))
    return parts


def hot_sauce():
    expansion_palette()
    parts=[bottle_shell('Slim hot sauce bottle',[(0,.017),(.003,.021),(.123,.021),(.145,.011),(.179,.010)],'cherry'),
           cylinder('Dark hot sauce cap',(0,0,.189),.012,.022,'dark_green',10),
           curved_label(.0215,.081,.034,.054,make_material('cream'))]
    chili=[(-.006,.093),(.002,.094),(.007,.088),(.007,.079),(.001,.070),(-.008,.067),(-.002,.075),(.001,.083),(-.001,.089)]
    parts.append(front_shape('Curved chili icon',chili,-.022,'cherry'))
    parts.append(leaf_icon(-.023,.096,.008))
    if ITERATION>=2:
        parts.append(cap_band(.178,.0125,'dark_green'))
        parts.append(sauce_contents(.119,.018,'cherry'))
    return parts


def bbq_sauce():
    expansion_palette()
    parts=[bottle_shell('Wide BBQ sauce bottle',[(0,.026),(.004,.034),(.117,.034),(.144,.021),(.168,.014)],'dark_brown'),
           cylinder('Black BBQ cap',(0,0,.184),.018,.032,'ink',10),
           curved_label(.0345,.079,.050,.059,make_material('cream'))]
    flame=[(-.012,.070),(-.014,.081),(-.006,.094),(-.003,.084),(.004,.101),(.011,.087),(.014,.079),(.010,.069),(0,.065)]
    parts.append(front_shape('Raised flame icon',flame,-.035,'kiwi_skin'))
    if ITERATION>=2:
        parts.append(front_shape('Inner flame', [(-.004,.070),(-.006,.077),(.001,.087),(.006,.077),(.004,.070)],-.036,'custard'))
        parts.append(sauce_contents(.114,.031,'dark_brown'))
    return parts


JOBS=[('ketchup',ketchup,900),('mustard',mustard,900),('mayonnaise',mayonnaise,900),
      ('soy_sauce',soy_sauce,900),('hot_sauce',hot_sauce,900),('bbq_sauce',bbq_sauce,900)]
if __name__=='__main__':run_category()
