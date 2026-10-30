CATEGORY_FOLDER="Wave2_07_Stationery"
SCRIPT_NAME="BuildStationery.py"
ITERATION=3

def pigment(obj,color):
    # Several discrete pencil colours share one matte material through vertex
    # colours. This is glTF COLOR_0 geometry data, never an image texture.
    key="GeometryPigment"
    if key not in MATERIALS:
        mat=bpy.data.materials.new(key);mat.use_nodes=True
        shader=mat.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value=(1,1,1,1)
        shader.inputs["Metallic"].default_value=0;shader.inputs["Roughness"].default_value=.82
        attr=mat.node_tree.nodes.new("ShaderNodeVertexColor");attr.layer_name="Color"
        mat.node_tree.links.new(attr.outputs["Color"],shader.inputs["Base Color"])
        MATERIALS[key]=mat
    obj.data.materials.clear();obj.data.materials.append(MATERIALS[key])
    for face in obj.data.polygons:face.material_index=0
    layer=obj.data.color_attributes.new(name="Color",type="FLOAT_COLOR",domain="CORNER")
    rgba=color_rgba(PALETTE[color])
    for value in layer.data:value.color=rgba
    return obj

def notebook():
    parts=[box("Cream page block",(0,0,.014),(.137,.194,.020),"cream"),
           box("Green lower hardcover",(0,0,.002),(.145,.202,.004),"leaf"),
           box("Green upper hardcover",(0,0,.026),(.145,.202,.005),"leaf"),
           box("Rounded binding spine",(-.071,0,.014),(.007,.196,.025),"leaf"),
           box("Yellow elastic top",(.048,0,.029),(.006,.198,.002),"mango_flesh")]
    for y in (-.100,.100):parts.append(box("Elastic end",(.048,y,.015),(.006,.002,.026),"mango_flesh"))
    bookmark=prism("Bookmark ribbon",[(-.008,-.100),(.008,-.100),(.008,-.119),(0,-.114),(-.008,-.119)],.009,.011,make_material("mango_flesh"))
    parts.append(bookmark)
    if ITERATION>=2:
        for z in (.008,.013,.018):parts.append(box("Subtle page edge layer",(.069,0,z),(.001,.188,.0006),"mango_flesh"))
    if ITERATION>=3:
        patch=make_disk("Small cover medallion",(-.015,0,.029),(.014,.014),make_material("cream"),vertices=10);parts.append(patch)
    return parts

def stationery_cylinder(name,position,radius,height,color):
    bevel=min(BEVEL_WIDTH,height/4)
    obj=lathe(name,[(-height/2,radius-bevel*.7),(-height/2+bevel,radius),
                    (height/2-bevel,radius),(height/2,radius-bevel*.7)],make_material("cream"),8)
    obj.location=position
    return pigment(obj,color)

def pencil_holder():
    parts=[]
    colors=("blue","cherry","mango_flesh","leaf","plum","blush")
    for i,color in enumerate(colors):
        x=(i%3-1)*.020;y=(-.010 if i<3 else .010);height=.133+(i%3)*.006
        if ITERATION>=2 and i>=3:
            x-=.005;height+=.012
        shaft=lathe("Hexagonal pencil with real sharpened tip",[(.009,.0043),(.010,.0048),(height-.019,.0048),
                     (height-.018,.0044),(height-.005,.0011),(height,.0001)],make_material("cream"),6)
        shaft.location=(x,y,0);pigment(shaft,color)
        layer=shaft.data.color_attributes["Color"]
        for face in shaft.data.polygons:
            if 1+3*6<=face.index<1+4*6:
                for index in face.loop_indices:layer.data[index].color=color_rgba(PALETTE["cream"])
        parts.append(shaft)
    shape=outline(.075,.046,.005)
    rings=[[(x*.93,y*.93,z) for x,y in shape] for z in (0,.003)]
    rings.append([(x,y,.078) for x,y in shape])
    parts.append(loft("Open cream pencil holder",rings,make_material("cream"),cap_end=False))
    parts+=flat_label("Pencils",-.024,.044,.063,.023)
    if ITERATION>=2:
        parts.append(box("Holder folded front lip",(0,-.024,.079),(.075,.002,.005),"cream"))
    if ITERATION>=3:
        for x in (-.034,.034):parts.append(box("Folded holder corner",(x,-.024,.038),(.003,.002,.074),"cream"))
    return parts

def blister(width,height,depth):
    shape=outline(width,height,.008)
    rings=[[(x,y,z) for x,y in shape] for z in (0,depth)]
    obj=loft("Transparent formed blister",rings,clear_material(),cap_start=False)
    obj.rotation_euler.x=math.pi/2
    obj.location=(0,-.006,height/2)
    apply_transform(obj)
    return obj

def pens():
    parts=[box("Cream pen backing card",(0,.003,.090),(.082,.004,.180),"cream")]
    for i,color in enumerate(("cherry","blue","ink")):
        x=(i-1)*.024
        parts.append(stationery_cylinder("Pen barrel",(x,-.005,.081),.004,.112,color))
        parts.append(stationery_cylinder("Coloured pen cap",(x,-.005,.145),.005,.030,color))
        clip=bar_between("Real pen clip",(x+.004,-.009,.151),(x+.004,-.009,.133),.0016,"dark_brown");parts.append(clip)
    parts.append(blister(.074,.164,.013))
    if ITERATION>=2:
        header=curved_label(5,.167,.071,.016,make_material("cream"));header.location.y=5-.021;parts.append(header)
    if ITERATION>=3:
        for x in (-.035,.035):parts.append(box("Blister sealed edge",(x,-.008,.083),(.002,.001,.159),"cream"))
    return parts

def markers():
    parts=[box("Marker pack backing",(0,.003,.075),(.103,.004,.150),"cream")]
    for i,color in enumerate(("blue","cherry","mango_flesh","leaf","plum")):
        x=(i-2)*.018
        parts.append(stationery_cylinder("Chunky marker barrel",(x,-.006,.065),.0068,.100,color))
        parts.append(stationery_cylinder("Marker cap",(x,-.006,.124),.0075,.023,color))
    # Cream perimeter has a real open centre, covered by transparent geometry.
    for x in (-.049,.049):parts.append(box("Marker cardboard side",(x,-.015,.067),(.007,.004,.130),"cream"))
    parts.append(box("Marker label strip",(0,-.020 if ITERATION>=2 else -.016,.025),(.101,.004,.037),"cream"))
    parts+=flat_label("Markers",-.0225 if ITERATION>=2 else -.0185,.025,.089,.023)
    parts.append(blister(.096,.139,.014))
    if ITERATION>=2:
        # Shorter cap sections expose more barrel colour through the window.
        for p in parts:
            if p.name.startswith("Marker cap"):p.location.z-=.002
    if ITERATION>=3:
        parts.append(box("Folded pack top",(0,-.014,.145),(.103,.004,.008),"cream"))
    return parts

def glue_stick():
    parts=[role(soft_cylinder("Yellow glue barrel",.016,0,.073,make_material("mango_flesh"),10),"body"),
           role(soft_cylinder("Cream glue cap",.018,.072,.100,make_material("cream"),10),"cap")]
    parts+=flat_label("Glue\nstick",-.0167,.040,.026,.029)
    if ITERATION>=2:
        parts.append(role(soft_cylinder("Twist base rim",.017,.002,.010,make_material("mango_flesh"),10),"body"))
    if ITERATION>=3:
        for side in (-1,1):
            parts.append(box("Cap grip ridge",(side*.013,-.013,.087),(.002,.0018,.014),"cream"))
    return parts

def scissors():
    parts=[]
    for side in (-1,1):
        parts.append(tube_loop("Teal rounded finger loop",(side*.022,-.052,.008),(.017,.026),.005,"teal",12,6,"XY"))
        blade=prism("Rounded scissor blade",[(-.004,-.016),(.004,-.016),(.008,.075),(.002,.087),(-.004,.076)],.004,.009,make_material("metal"))
        blade.rotation_euler.z=side*.19;apply_transform(blade);parts.append(blade)
        parts.append(bar_between("Handle neck",(side*.014,-.029,.008),(0,-.006,.008),.009,"teal"))
    parts.append(cylinder("Round metal pivot",(0,-.003,.010),.006,.004,"metal",8))
    if ITERATION>=2:
        for p in parts:
            if p.name.startswith("Rounded scissor blade"):p.rotation_euler.z+=(-.05 if ".001" in p.name else .05);apply_transform(p)
    if ITERATION>=3:
        parts.append(cylinder("Teal pivot inset",(0,-.003,.0125),.0024,.001,"teal",6))
    return parts

JOBS=[("notebook",notebook,900),("pencils",pencil_holder,1200),("pens",pens,900),
      ("markers",markers,1200),("glue_stick",glue_stick,900),("scissors",scissors,900)]
if __name__=="__main__":run_category()
