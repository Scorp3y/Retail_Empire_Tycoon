CATEGORY_FOLDER='Expansion_07_GardenSupplies'
SCRIPT_NAME='BuildExpansionGarden.py'
ITERATION=2


def arrange_expansion_preview(objects):layout_large_props(objects)


def sprout_icon(y,z,size):
    parts=[leaf_icon(y,z+size*.25,size*.65,'leaf',-size*.22),
           leaf_icon(y-.0005,z+size*.42,size*.65,'leaf',size*.25)]
    parts.append(pigment(rounded_rib('Sprout stem',[(0,y,z-size*.3),(0,y,z+size*.1),(.003,y,z+size*.4)],.0013,'cream'),'leaf'))
    return parts


def potted_cactus():
    expansion_palette()
    pot=hollow_vessel('Terracotta pot with inner floor',[(0,.026),(.003,.030),(.056,.040),(.064,.040),(.062,.035),(.006,.026)],'terracotta',10)
    parts=[pot,tube_loop('Thick terracotta rim',(0,0,.060),(.038,.038),.0035,'terracotta',12,6,'XY'),
           cylinder('Separate soil',(0,0,.052),.033,.009,'dark_brown',8),
           colored_ball('Round cactus body',(0,0,.109),(.026,.024,.054),'leaf',10,6)]
    for side in (-1,1):
        pts=[(side*.018,0,.095),(side*.038,0,.102),(side*.039,0,.126)]
        parts.append(pigment(rounded_rib('Curved cactus arm',pts,.009,'cream'),'leaf'))
    for i in range(6 if ITERATION==1 else 9):
        a=i*2.399;z=.085+(i%3)*.024
        spine=colored_ball('Tiny soft cactus spine',(.025*math.cos(a),.023*math.sin(a),z),(.001,.001,.004),'cream',4,3)
        spine.rotation_euler.y=.7;apply_transform(spine);parts.append(spine)
    for i in range(5):
        a=i*math.tau/5
        parts.append(colored_ball('Pink flower petal',(.008*math.cos(a),.008*math.sin(a),.165),(.006,.006,.004),'pink',6,3))
    parts.append(colored_ball('Flower centre',(0,0,.168),(.004,.004,.003),'custard',6,3))
    return parts


def seeds():
    expansion_palette()
    parts=[box('Cream paper seed packet',(0,0,.070),(.085,.012,.140),'cream'),
           box('Folded packet top',(0,0,.142),(.081,.015,.006),'cream')]
    strip=box('Soil strip',(0,-.0068,.022),(.078,.001,.023),'cream');parts.append(pigment(strip,'kiwi_skin'))
    parts+=sprout_icon(-.0075,.075,.036)
    if ITERATION>=2:
        parts.append(box('Green top seal',(0,-.008,.143),(.073,.001,.003),'leaf'))
    return parts


def watering_can():
    expansion_palette()
    body=hollow_vessel('Hollow watering can body',[(0,.043),(.004,.052),(.116,.052),(.125,.049),(.123,.046),(.006,.043)],'leaf',10)
    parts=[body,tube_loop('Curved open can handle',(-.051,0,.097),(.035,.066),.006,'leaf',12,6,'XZ')]
    start=Vector((.045,0,.032));end=Vector((.141,0,.132));axis=end-start;length=axis.length
    pipe=open_profile('Hollow long spout',[(0,.009),(length,.011),(length,.008),(0,.006)],'leaf',8)
    pipe.rotation_euler=axis.to_track_quat('Z','Y').to_euler();pipe.location=start;apply_transform(pipe);parts.append(pipe)
    rose=cylinder('Perforated rose head',(0,0,0),.022,.008,'metal',10)
    # Tapered cutters create a small physical chamfer at each spray opening.
    for x,y in ((-.008,-.008),(.008,-.008),(-.008,.008),(.008,.008)):
        cutter=lathe('Temporary rose hole',[(-.020,.002),(.002,.002),(.006,.004),(.020,.004)],make_material('cream'),6)
        cutter.location=(x,y,0);apply_modifiers(cutter)
        mod=rose.modifiers.new('True spray opening','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
        apply_modifiers(rose);bpy.data.objects.remove(cutter,do_unlink=True)
    rose.rotation_euler=axis.to_track_quat('Z','Y').to_euler();rose.location=end;apply_transform(rose);parts.append(rose)
    if ITERATION>=2:
        handle=parts[1];handle.data.materials.append(make_material('metal'))
        for face in handle.data.polygons:
            if face.center.z>.157:face.material_index=1
    return parts


def hand_trowel():
    expansion_palette()
    contour=[(-.022,0),(-.035,.040),(-.029,.091),(-.013,.128),(0,.140),(.013,.128),(.029,.091),(.035,.040),(.022,0)]
    verts=[(x,y,.016 if y<.12 else .008) for x,y in contour]
    verts += [(x,y,.013 if y<.12 else .005) for x,y in contour]
    verts += [(0,.061,.003),(0,.061,0)];n=len(contour);faces=[]
    for i in range(n):
        j=(i+1)%n;faces.extend([(n*2,i,j),(n*2+1,n+j,n+i),(i,n+i,n+j,j)])
    blade=mesh_object('Real scooped metal blade',verts,faces,make_material('metal'));add_bevel(blade)
    parts=[blade,box('Chunky green grip',(0,-.056,.011),(.027,.107,.020),'leaf'),
           tube_loop('Grip hanging hole',(0,-.124,.011),(.009,.012),.004,'leaf',10,6,'XY')]
    if ITERATION>=2:
        parts.append(box('Pale handle inset',(0,-.056,.0218),(.011,.064,.001),'cream'))
    return parts


def garden_gloves():
    expansion_palette();parts=[]
    contour=[(-.035,-.100),(-.045,-.055),(-.041,.005),(-.068,.025),(-.074,.044),(-.066,.054),(-.055,.051),(-.033,.032),
       (-.036,.103),(-.030,.119),(-.021,.118),(-.016,.105),(-.014,.060),(-.012,.130),(-.005,.145),(.006,.145),(.014,.131),(.014,.061),
       (.018,.118),(.025,.131),(.035,.126),(.040,.112),(.034,.053),(.040,.096),(.048,.108),(.058,.104),(.064,.090),(.050,.021),(.046,-.057),(.035,-.100)]
    area=sum(x*contour[(i+1)%len(contour)][1]-contour[(i+1)%len(contour)][0]*y for i,(x,y) in enumerate(contour))
    if area<0:contour.reverse()
    for side in (-1,1):
        hand=[(side*x,y) for x,y in contour]
        if side==-1:hand.reverse()
        angle=-side*.12;rotation=Matrix.Rotation(angle,4,'Z')
        offset=Vector((side*.055,side*.006,.004 if side==1 else 0))
        glove=prism('Soft yellow garden glove',hand,0,.015,make_material('custard'))
        glove.location=offset;glove.rotation_euler.z=angle;apply_transform(glove);parts.append(glove)
        cuff=box('Green glove cuff',rotation@Vector((0,-.084,.011))+offset,(.074,.030,.019),'leaf')
        cuff.rotation_euler.z=angle;apply_transform(cuff);parts.append(cuff)
        if ITERATION>=2:
            patch=ball('Raised glove seam',rotation@Vector((0,-.010,.0165))+offset,(.022,.001,.001),'golden',6,3)
            patch.rotation_euler.z=angle;apply_transform(patch);parts.append(patch)
    return parts


def plant_food():
    expansion_palette();shape=outline(.130,.061,.010)
    bag=loft('Plump cream plant food bag',[[(x*sx,y*sy,z) for x,y in shape] for z,sx,sy in
        ((0,.88,.85),(.012,1,1),(.213,.92,.90),(.244,.88,.18))],make_material('cream'))
    parts=[bag,box('Green folded top',(0,0,.245),(.121,.016,.010),'dark_green')]
    parts+=sprout_icon(-.0315,.158,.038)
    text=label_text('Plant\nfood',5,.079,.092,.043);text.location.y=5-.0325;parts.append(text)
    if ITERATION>=2:
        parts.append(box('Green bottom band',(0,-.031,.026),(.111,.001,.010),'dark_green'))
    return parts


JOBS=[('potted_cactus',potted_cactus,1200),('seeds',seeds,900),('watering_can',watering_can,900),
      ('hand_trowel',hand_trowel,900),('garden_gloves',garden_gloves,1200),('plant_food',plant_food,900)]
if __name__=='__main__':run_category()
