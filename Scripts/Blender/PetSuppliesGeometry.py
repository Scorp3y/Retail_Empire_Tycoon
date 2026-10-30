CATEGORY_FOLDER="Wave2_05_PetSupplies"
SCRIPT_NAME="BuildPetSupplies.py"
ITERATION=3

def animal_face(kind,y,z,scale=1):
    s=scale
    parts=[ball("Friendly animal face",(0,y,z),(.022*s,.003*s,.020*s),"cream")]
    if kind=="dog":
        for side in (-1,1):
            ear=ball("Floppy ear",(side*.024*s,y+.001,z+.003*s),(.009*s,.003*s,.019*s),"dark_brown",6,4)
            ear.rotation_euler.y=side*.18;apply_transform(ear);parts.append(ear)
    else:
        for side in (-1,1):
            verts=[(side*.011*s,y,z+.012*s),(side*.024*s,y,z+.030*s),(side*.026*s,y,z+.009*s)]
            parts.append(mesh_object("Pointed cat ear",verts,[(0,1,2)],make_material("cream")))
            for h in (-1,1):
                parts.append(bar_between("Cat whisker",(side*.016*s,y-.003,z-.003*s),(side*.031*s,y-.003,z+(.003*h-.003)*s),.001*s,"dark_brown"))
    for side in (-1,1): parts.append(disk_front("Eye",side*.009*s,y-.004*s,z+.003*s,.002*s,.0027*s,"dark_brown",6))
    parts.append(disk_front("Nose",0,y-.0045*s,z-.006*s,.0035*s,.0025*s,"dark_brown",6))
    return parts

def pet_bag(label,height,width,depth,color,icon):
    shape=outline(width,depth,.009)
    rings=[]
    for z,sx,sy in ((0,.78,.74),(.008,.94,.94),(height*.34,1.04,1.08),(height*.74,1,1),(height-.014,.84,.32),(height-.007,.82,.22)):
        rings.append([(x*sx,y*sy,z) for x,y in shape])
    body=loft("Plump paper pouch",rings,make_material(color))
    parts=[body,box("Folded top seam",(0,0,height-.0035),(width*.83,depth*.17,.007),"cream")]
    front=-depth*.55-.002
    parts+=flat_label(label,front,height*.28,width*.83,.027)
    if icon in ("dog","cat"):
        parts+=animal_face(icon,front-.001,height*.57,.85 if height<.22 else 1)
    else:
        parts+=bone((0,front-.002,height*.58),.058,.007,"cream",vertical=False,icon=True)
    if ITERATION>=2:
        # A broad inset panel frames the icon without tiny decorative texture.
        panel=curved_label(5,height*.78,width*.80,.009,make_material("cream"))
        panel.location.y=front+5+.0006
        parts.append(panel)
    return parts

def bone(center,length,radius,color,vertical=False,icon=False):
    x,y,z=center
    parts=[ball("Rounded bone shaft",center,(length*.38,radius*.85,radius*.85),color,8,4)]
    for side in (-1,1):
        for offset in (-1,1):
            parts.append(ball("Soft knobby bone end",(x+side*length*.38,y,z+offset*radius*.70),
                              (radius*1.2,radius*.92,radius),color,8 if ITERATION>=3 else 6,4))
    return parts

def dog_food():return pet_bag("Dog food",.25,.132,.075,"leaf","dog")
def cat_food():return pet_bag("Cat food",.20,.113,.067,"blue","cat")
def pet_treats():return pet_bag("Pet treats",.15,.090,.052,"mango","bone")
def cat_litter():return pet_bag("Cat litter",.28,.151,.087,"leaf","cat")

def pet_bowl():
    profile=[(0,.070),(.002,.075),(.008,.079),(.067,.090),(.076,.089),(.080,.085),
             (.080,.078),(.076,.075),(.020,.064),(.014,.058)]
    bowl=hollow_vessel("True hollow flared pet bowl",profile,"dark_green",12)
    parts=[bowl]
    parts.append(disk_front("Paw pad",0,-.083,.042,.011,.008,"cream"))
    for x,z in ((-.014,.053),(-.005,.059),(.006,.059),(.015,.052)):
        parts.append(disk_front("Paw toe",x,-.083,z,.0044,.005,"cream",6))
    if ITERATION>=2:
        for part in parts[1:]:
            for vertex in part.data.vertices:
                x=vertex.co.x+part.location.x;z=vertex.co.z+part.location.z
                radius=.079+(z-.008)*.011/.059
                vertex.co.y=-math.sqrt(max(.001,radius*radius-x*x))-.0009-part.location.y
    if ITERATION>=3:
        # Same material, lighter geometric rim band emphasises real wall thickness.
        bowl.data.materials.append(make_material("cream"))
        for face in bowl.data.polygons:
            if 1+4*12<=face.index<1+6*12:face.material_index=1
    return parts

def chew_toy():
    parts=bone((0,0,.017),.15,.017,"mango")
    parts.append(ball("Raised lighter grip",(0,-.001,.017),(.024,.0155,.0155),"mango_flesh",8,4))
    if ITERATION>=2:
        for p in parts[1:5]:
            for v in p.data.vertices:v.co.x*=1.12
    return parts

JOBS=[("dog_food",dog_food,900),("cat_food",cat_food,900),("pet_treats",pet_treats,900),
      ("cat_litter",cat_litter,900),("pet_bowl",pet_bowl,900),("chew_toy",chew_toy,900)]
if __name__=="__main__":run_category()
