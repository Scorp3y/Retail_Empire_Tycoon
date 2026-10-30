CATEGORY_FOLDER="Wave2_08_Kitchenware"
SCRIPT_NAME="BuildKitchenware.py"
ITERATION=3

def curved_handle(name,path,radius,color):
    rings=[]
    for i,point in enumerate(path):
        tangent=(Vector(path[min(i+1,len(path)-1)])-Vector(path[max(0,i-1)])).normalized()
        across=Vector((-tangent.z,0,tangent.x)).normalized()
        rings.append([tuple(Vector(point)+radius*(math.cos(j*math.tau/6)*across+math.sin(j*math.tau/6)*Vector((0,1,0)))) for j in range(6)])
    return loft(name,rings,make_material(color),part_role="component")

def band_material(obj,color,bands,segments=12):
    index=len(obj.data.materials);obj.data.materials.append(make_material(color))
    for face in obj.data.polygons:
        if face.index>0 and (face.index-1)//segments in bands:face.material_index=index

def mug():
    green_height=.037 if ITERATION>=2 else .030
    profile=[(0,.029),(.002,.033),(.008,.035),(green_height,.037),(.085,.041),(.092,.041),
             (.095,.039),(.095,.034),(.091,.0325),(.012,.0275),(.008,.025)]
    body=hollow_vessel("Thick hollow cream mug",profile,"cream",12)
    band_material(body,"leaf",{0,1,2})
    handle=curved_handle("Rounded C shaped mug handle",[(.040,0,.077),(.059,0,.082),(.077,0,.069),
                          (.077,0,.046),(.061,0,.029),(.036,0,.029)],.0065 if ITERATION>=3 else .006,"cream")
    parts=[body,handle]
    return parts

def plate():
    body=hollow_vessel("Chunky raised cream plate",[(0,.078),(.001,.083),(.004,.094),(.008,.116),
          (.012,.125),(.016,.124),(.017,.120),(.016,.112),(.010,.099),(.007,.090)],"cream",16)
    band_material(body,"leaf",{4,5} if ITERATION==1 else {3,4,5,6},16)
    if ITERATION>=3:
        # Broad flat central well, preserving the real raised rim and depth.
        for vertex in body.data.vertices:
            if abs(vertex.co.z-.007)<1e-6:vertex.co.z=.006
    return [body]

def kitchen_bowl():
    body=hollow_vessel("Deep green and cream bowl",[(0,.038),(.002,.043),(.012,.047),(.060,.074),(.070,.075),
          (.075,.071),(.075,.065),(.070,.062),(.018,.040),(.008,.034)],"leaf",12)
    band_material(body,"cream",{5,6,7,8} if ITERATION==1 else {4,5,6,7,8},12)
    body.data.polygons[-1].material_index=1
    if ITERATION>=3:
        for vertex in body.data.vertices:
            if abs(vertex.co.z-.008)<1e-6:vertex.co.z=.009
    return [body]

def frying_pan():
    body=hollow_vessel("Hollow dark non stick pan",[(0,.065),(.002,.070),(.005,.075),(.026,.086),(.030,.0875),
          (.034,.086),(.034,.080),(.030,.078),(.010,.068),(.006,.062)],"ink",12)
    parts=[body,box("Wooden orange pan handle",(.137,0,.024),(.104,.025,.018),"mango"),
           tube_loop("Real hanging hole",(.202,0,.024),(.011,.008),.004,"mango",12,6,"XY")]
    if ITERATION>=2:
        rotation=Matrix.Rotation(.50,4,"Z")
        for part in parts:
            part.location=rotation@part.location
            part.rotation_euler.z+=.50;apply_transform(part)
    if ITERATION>=3:band_material(body,"metal",{4,5},12)
    return parts

def saucepan():
    body=hollow_vessel("Hollow cream saucepan",[(0,.057),(.002,.062),(.008,.070),(.083,.075),(.091,.075),
          (.094,.072),(.094,.067),(.089,.065),(.012,.060),(.007,.055)],"cream",12)
    lid=lathe("Sage saucepan lid",[(.091,.069),(.092,.078),(.094,.078),(.097,.072),(.102,.050),(.103,.020)],make_material("leaf"),12)
    parts=[body,lid,ball("Rounded lid knob",(0,0,.108),(.013,.013,.009),"leaf",8,4)]
    for side in (-1,1):
        path=[(side*.072,0,.072),(side*.092,0,.078),(side*.105,0,.066),(side*.105,0,.044),(side*.074,0,.036)]
        handle=curved_handle("Small saucepan side handle",path,.005,"cream")
        parts.append(handle)
    if ITERATION>=2:band_material(lid,"cream",{0},12)
    if ITERATION>=3:
        # A low green collar visually joins the knob to the domed lid.
        parts.append(cylinder("Lid knob collar",(0,0,.103),.013,.004,"leaf",8))
    return parts

def food_container():
    outer=outline(.17,.115,.009);inner=outline(.164,.109,.007)
    rings=[]
    for z,shape,scale in ((0,outer,.94),(.001,outer,.97),(.048,outer,1),(.052,outer,1),
                          (.052,inner,1),(.049,inner,1),(.005,inner,.93)):
        rings.append([(x*scale,y*scale,z) for x,y in shape])
    body=loft("Hollow clear container with actual walls",rings,clear_material())
    body["hollow_opening"]=True
    edge=make_material("plastic_edge",.25)
    edge.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value=.2
    body.data.materials.append(edge)
    for face in body.data.polygons:
        if 1+2*8<=face.index<1+5*8:face.material_index=1
    lid=soft_box("Semi transparent lid",(0,0,.055),(.174,.119,.007),clear_material(),"lid")
    parts=[body,lid,box("Tiny sage front latch",(0,-.061,.051),(.018,.007,.014),"leaf")]
    if ITERATION>=2:
        # Define the lid silhouette using a darker translucent edge material.
        lid.data.materials.append(edge)
        for face in lid.data.polygons:
            if face.index<1+8:face.material_index=1
    if ITERATION>=3:
        parts.append(box("Latch thumb tab",(0,-.065,.053),(.010,.003,.004),"leaf"))
    return parts

JOBS=[("mug",mug,900),("plate",plate,900),("bowl",kitchen_bowl,900),
      ("frying_pan",frying_pan,900),("saucepan",saucepan,900),("food_container",food_container,1200)]
if __name__=="__main__":run_category()
