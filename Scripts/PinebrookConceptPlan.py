"""English brief: the approved fan-shaped plan, three rear courts, southeast park, northwest townhouses."""
import json, math, random
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Art/Pinebrook/Layout';OUT.mkdir(parents=True,exist_ok=True)
placements=[];paths=[];roads=[];streets=[];clearings=[];parking=[]
models={m['name']:m for m in json.loads((ROOT/'ArtSource/Pinebrook/DistrictModels/Models.json').read_text())['models']}
def place(group,prefab,x,z,yaw=0,y=0,scale=1,label='',name=None):
    p=dict(name=name or f'{prefab.replace(chr(58),chr(95))}_{len(placements):04}',group=group,prefab=prefab,position=dict(x=x,y=y,z=z),yaw=yaw,scale=scale,label=label);placements.append(p);return p
def path(a,b,width=3,name='Walk'):
    if math.dist(a,b)>.1:paths.append(dict(name=f'{name}_{len(paths):03}',ax=a[0],az=a[1],bx=b[0],bz=b[1],width=width,height=.145))
def route(points,width=3,name='Walk',closed=False):
    for a,b in zip(points,points[1:]+(points[:1] if closed else [])):path(a,b,width,name)
def street(a,b):
    streets.append(dict(ax=a[0],az=a[1],bx=b[0],bz=b[1],width=8))
    if a[0]==b[0]:roads.append(dict(x=a[0]-4,z=min(a[1],b[1])-4,width=8,depth=abs(a[1]-b[1])+8))
    elif a[1]==b[1]:roads.append(dict(x=min(a[0],b[0])-4,z=a[1]-4,width=abs(a[0]-b[0])+8,depth=8))
    else:
        for i in range(math.ceil(math.dist(a,b))+1):
            t=i/math.ceil(math.dist(a,b));roads.append(dict(x=a[0]+(b[0]-a[0])*t-4,z=a[1]+(b[1]-a[1])*t-4,width=8,depth=8))
for a,b in [((-24,310),(-24,414)),((-224,310),(-24,310)),((-224,310),(-224,582)),((-104,310),(-104,452)),((-24,414),(-64,452)),((-24,414),(16,452)),((-224,452),(136,452)),((-104,452),(-104,582)),((16,452),(16,582)),((136,452),(136,582)),((-224,582),(136,582))]:street(a,b)
retained_poses=[(-164,400,180),(-208,365,90),(-120,365,270),(-186,328,0),(-142,328,0),(-120,410,270),(-68,344,180),(28,344,180),(-264,508,90),(-264,516.4,90),(-264,540,90),(-264,548.4,90),(120,402,180)]
retained=[dict(index=i,position=dict(x=x,y=.12,z=z),yaw=yaw) for i,(x,z,yaw) in enumerate(retained_poses)]
buildings=[]
types=[('Long_Cream_4F','Compact_Cream_3F','Loggia_Cream_3F','Corner_Sand_3F','Compact_Sand_3F','Long_Grey_4F'),('Mixed_LaundryPost_3F','Corner_Sand_3F','Compact_Grey_3F','Loggia_Grey_3F','Compact_Cream_3F','Long_Cream_4F'),('Long_Grey_4F','Loggia_Cream_3F','Corner_Sand_3F','Compact_Sand_3F','Loggia_Grey_3F','Long_Cream_4F')]
for c,variants in zip((-164,-44,76),types):
    for prefab,(x,z,yaw) in zip(variants,[(c,562,0),(c-42,498,270),(c-42,538,270),(c+42,498,90),(c+42,538,90),(c,472,180)]):buildings.append((prefab,x,z,yaw))
buildings += [('Mixed_Bakery_3F',-68,395,270),('Mixed_Pharmacy_3F',28,395,90),('Kindergarten_1F',169,508,90),('Townhouse_Cream_2F',-258,582,90),('Townhouse_Sand_2F',-238,616,25),('Townhouse_Cream_2F',-201,632,0),('Townhouse_Sand_2F',-164,632,0)]
def footprint(prefab,x,z,yaw,pad=0):
    m=models[prefab];w,d=m['width'],m['depth'];a=math.radians(yaw);w,d=abs(math.cos(a))*w+abs(math.sin(a))*d,abs(math.sin(a))*w+abs(math.cos(a))*d
    return x-w/2-pad,z-d/2-pad,w+2*pad,d+2*pad
for i,(prefab,x,z,yaw) in enumerate(buildings):
    place('Buildings',prefab,x,z,yaw,name=f'{i+1:02}_{prefab}');clearings.append(dict(zip(('x','z','width','depth'),footprint(prefab,x,z,yaw,4))))
    m=models[prefab];a=math.radians(yaw);front=(x-math.sin(a)*(m['depth']/2+1.1),z-math.cos(a)*(m['depth']/2+1.1))
    if i<18:
        c=(-164,-44,76)[i//6];end=(max(c-28,min(c+28,front[0])),max(486,min(550,front[1])))
    elif i==18:end=(-31,395)
    elif i==19:end=(-17,395)
    elif i==20:end=(142,508)
    else:end=((-277,582),(-247,597),(-201,611),(-164,611))[i-21]
    path(front,end,3,'Entrance')
for c,z,theme in [(-164,365,'play'),(-164,520,'quiet'),(-44,520,'garden'),(76,520,'sport')]:
    route([(c-28,z-31),(c+28,z-31),(c+28,z+30),(c-28,z+30)],3,'CourtLoop',True);path((c-37,z),(c+37,z),3,'CourtCross');path((c,z-41),(c,z+40),3,'CourtSpine')
    for dx in (-22,22):
        for dz in (-22,22):
            place('Props','native:TreePlanter',c+dx,z+dz);place('Greenery','nature:TreeRound',c+dx,z+dz,scale=1.45);place('Greenery','native:FlowerStrip',c+dx,z+dz+3,scale=1.15)
    for dx in (-17,17):
        place('Props','native:TimberBench',c+dx,z-10,y=.145);place('Props','native:StreetBin',c+dx+2,z-10,y=.145);place('Props','native:QuarterLamp',c+dx,z+14,y=.145)
    if theme=='play':place('Props','native:PlayTower',c-8,z+10,90,y=.145,scale=.8);place('Props','native:SwingSet',c+10,z+11,y=.145)
    elif theme=='quiet':place('Props','native:Pergola',c,z+12,y=.145);place('Props','Sculpture',c,z)
    elif theme=='garden':
        place('Props','native:Pergola',c,z+12,y=.145)
        for dx in (-8,8):place('Props','Raised_Garden',c+dx,z)
    else:place('Props','Table_Tennis',c-9,z+10);place('Props','Workout_Bars',c+10,z+10)
for x in (-68,28):
    path((x-16,329),(x+16,329),9,'ShopSquare');path((x,329),(x,334),6,'ShopDoor')
    for dx in (-14,14):place('Props','native:TreePlanter',x+dx,328);place('Greenery','nature:TreeRound',x+dx,328,scale=1.45)
    for dx in (-7,7):place('Props','native:CafeTable',x+dx,328,y=.145);place('Props','native:CafeUmbrella',x+dx,328,y=.145)
    place('Props','native:BikeRack',x+12,333,y=.145);path((x,358),(x,377),5,'ShopRear')
route([(-57,424),(-24,444),(0,432)],3,'ForkGarden');place('Props','Fountain',-24,438);place('Props','Notice_Board',-46,429,145,label='PINEBROOK SQUARE')
for x,z in [(-42,441),(-6,441),(-24,446)]:place('Greenery','nature:TreeWide',x,z,scale=1.5);place('Greenery','native:MixedPlanting',x+2,z,scale=1.2)
route([(48,362),(78,371),(99,384),(98,419),(108,432),(81,432),(70,423),(54,416),(46,390)],3.5,'ParkMeander',True);path((20,376),(47,376),4,'ParkEntrance');path((108,402),(112.5,402),3,'PavilionLink')
place('Props','Pond',70,408,y=.03);place('Props','Footbridge',70,408,y=.04);path((70,396),(70,405),2.7,'BridgeApproach');path((70,411),(70,423),2.7,'BridgeApproach');path((70,396),(46,390),3,'ParkPondLink')
for x,z in [(48,389),(90,399),(47,416),(91,430),(62,375)]:place('Props','native:TimberBench',x,z,160,y=.145);place('Greenery','nature:TreeWide',x+4,z+3,scale=1.6);place('Greenery','native:MixedPlanting',x+6,z+2,scale=1.3)
place('Props','Kiosk',45,406,270,label='PARK KIOSK');place('Props','Fountain',122,342,y=.04)
for i in range(16):
    a=i*math.tau/16;place('Greenery','native:FlowerStrip',122+math.cos(a)*7.7,342+math.sin(a)*7.7,90-i*22.5,scale=.9)
path((142,508),(160,508),3,'KindergartenDoor')
for x in (155,181):place('Greenery','nature:Hedge',x,532,scale=1.2)
place('Props','native:Sandpit',168,531,y=.145);place('Props','Spring_Rider',175,530,y=.145)
for x in range(154,184,3):place('Props','Garden_Fence',x,539)
for x in (152,184):
    for z in range(520,539,3):place('Props','Garden_Fence',x,z,90)
place('Props','native:BusShelter',-34.5,380,90,y=.16,label='PINEBROOK',name='SouthboundBusShelter');place('Props','native:BusShelter',-13.5,357,270,y=.16,label='PINEBROOK',name='NorthboundBusShelter')
path((-34.5,377),(-34.5,383),6,'StopPad');path((-13.5,354),(-13.5,360),6,'StopPad')
# Coherent single-row visitor lots: a 6.2 m aisle, 2.8 x 5.6 m bays,
# a dropped-curb access and a rear pedestrian connection. Occupancy is deliberate.
for number,(name,x,z,yaw,stalls,occupied,rx,rz) in enumerate([
    ('WestCourt',-234,350,270,5,[0,3],-224,350),
    ('BirchCourt',-114,518,270,6,[0,3],-104,518),
    ('CedarCourt',-94,518,90,6,[4],-104,518),
    ('AlderCourt',26,518,90,6,[1,4],16,518),
    ('ParkVisitors',146,554,90,8,[0,2,6],136,554),
    ('CafeVisitors',-38,365,270,6,[0,3,5],-24,365),
    ('MarketVisitors',-10,366,90,4,[],-24,366)]):
    a=math.radians(yaw);width=stalls*2.8+.8
    def local(lx,lz):return x+math.cos(a)*lx+math.sin(a)*lz,z-math.sin(a)*lx+math.cos(a)*lz
    parking.append(dict(name=name,x=x,z=z,yaw=yaw,stalls=stalls,occupied=occupied,roadX=rx,roadZ=rz))
    corners=[local(lx,lz) for lx in (-width/2-2,width/2+2) for lz in (-2,15)]
    clearings.append(dict(x=min(p[0] for p in corners),z=min(p[1] for p in corners),width=max(p[0] for p in corners)-min(p[0] for p in corners),depth=max(p[1] for p in corners)-min(p[1] for p in corners)))
    for slot in occupied:
        px,pz=local((slot-(stalls-1)/2)*2.8,9.1)
        car=('traffic:01_Hatchback','traffic:02_ClassicSedan','traffic:03_StationWagon','traffic:04_Coupe','traffic:05_BoxSUV')[(number+slot)%5]
        place('Props',car,px,pz,yaw+180,y=.13,name=f'Parked_{name}_{slot+1:02}')
    px,pz=local(-width/2-.5,1)
    place('Signs','Parking_Sign',px,pz,yaw+180,y=.13,label='P')
    # A sidewalk along the outside of the row keeps walking paths out of the bays.
    route([local(width/2+1,0),local(width/2+1,13.6),local(-width/2-1,13.6)],1.8,'ParkingWalk')
# Re-author the retained park's walks into the same surface union. Its play area,
# flower ring, pavilion and furniture stay intact.
path((108,342),(108,432),3,'RetainedParkWalk');path((99,384),(108,384),3,'RetainedParkWalk')
path((108,386),(154.7,386),2.5,'RetainedParkWalk');path((108,342),(114.6,342),2.6,'GardenEntrance')
route([(81,432),(100,438),(130,456),(153,435),(156,397),(151,354),(151,320),(131,301),(82,301)],2.5,'RetainedWoodlandWalk')
plazas=[dict(x=112.5,z=396,width=15,depth=12)]
route([(-277,475),(-278,521),(-277,562),(-273,601),(-245,637),(-203,651),(-160,651),(-139,618)],2.8,'ForestWalk')
route([(-229,474),(-242,474),(-242,584),(-247,597),(-225,610),(-201,611),(-164,611),(-145,594),(-145,589)],2.8,'TownhousePromenade')
for x,z,yaw in [(-264,508,90),(-264,540,90),(-258,582,90),(-238,616,25),(-201,632,0),(-164,632,0)]:
    a=math.radians(yaw)
    for dx in (-8,0,8):
        px=x+math.cos(a)*dx+math.sin(a)*7;pz=z-math.sin(a)*dx+math.cos(a)*7;place('Props','Garden_Fence',px,pz,yaw);place('Greenery','native:FlowerStrip',px,pz+1,yaw,scale=1.1)
    if yaw==90:path((x+6,z),(x+14,z),2,'TownhouseDoor')
place('Signs','Entrance_Sign',-14,267,180,y=.15,label='PINEBROOK',name='Entrance_Pinebrook')
for x in (-17,-11):place('Props','Planter_Long',x,268,y=.15,scale=.65)
place('Signs','label:Cafe',-68,337.39,180,y=3.57,label='PINEBROOK CAFE',name='PinebrookCafeFrontLabel');place('Signs','label:Cafe',-68,350.61,0,y=3.57,label='PINEBROOK CAFE',name='PinebrookCafeRearLabel');place('Signs','label:Pavilion',120,397.09,180,y=3.62,label='PARK PAVILION',name='ParkPavilionLabel')
for label,x,z in [('Pinebrook Avenue',-16,400),('Birch Lane',-215,457),('Cedar Row',-135,598),('Brookside Park',37,378)]:place('Signs','Street_Name_Sign',x,z,180,label=label)
# Junction signs are placed in Unity from the directed approach vectors, never guessed yaws.
for i,(prefab,x,z,yaw) in enumerate(buildings):
    m=models[prefab];a=math.radians(yaw);lx=m['width']/2-2;lz=-m['depth']/2-2.3;px=x+math.cos(a)*lx+math.sin(a)*lz;pz=z-math.sin(a)*lx+math.cos(a)*lz;place('Props','native:BikeRack',px,pz,yaw,y=.145)
    if i%3==0:place('Props','Bicycle',px,pz,yaw+15,y=.145)
def distance(p,a,b):
    dx,dz=b[0]-a[0],b[1]-a[1];t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dz)/(dx*dx+dz*dz)));return math.hypot(p[0]-a[0]-t*dx,p[1]-a[1]-t*dz)
def blocked(x,z,pad=3):
    for lot in parking:
        a=math.radians(lot['yaw']);dx=x-lot['x'];dz=z-lot['z'];lx=math.cos(a)*dx-math.sin(a)*dz;lz=math.sin(a)*dx+math.cos(a)*dz
        if abs(lx)<lot['stalls']*1.4+pad+2 and -pad-2<lz<14+pad:return True
        if distance((x,z),(lot['roadX'],lot['roadZ']),(lot['x'],lot['z']))<5:return True
    for prefab,bx,bz,yaw in buildings:
        rx,rz,w,d=footprint(prefab,bx,bz,yaw,pad)
        if rx<=x<=rx+w and rz<=z<=rz+d:return True
    for i,(bx,bz,yaw) in enumerate(retained_poses):
        w,d=(23.2,16) if i<8 else (8.4,11) if i<12 else (16,12)
        if yaw%180:w,d=d,w
        if abs(x-bx)<w/2+pad and abs(z-bz)<d/2+pad:return True
    if abs(x+24)<11 and z<414:return True
    if any(distance((x,z),(s['ax'],s['az']),(s['bx'],s['bz']))<11 for s in streets):return True
    if any(distance((x,z),(p['ax'],p['az']),(p['bx'],p['bz']))<p['width']/2+2 for p in paths):return True
    return False
rng=random.Random(93241)
for x1,z1,x2,z2,step in [(-375,270,-285,760,9),(-285,270,-235,470,9),(-285,662,250,800,9),(-135,610,250,662,10),(205,270,300,662,9),(-285,254,205,291,10)]:
    for row,z in enumerate(range(z1,z2,step)):
        for col,x in enumerate(range(x1,x2,step)):
            px=x+rng.uniform(-step*.49,step*.49);pz=z+rng.uniform(-step*.49,step*.49)
            if blocked(px,pz,5):continue
            place('Greenery',('nature:Pine','nature:TreeRound','nature:TreeWide')[(row+col)%3],px,pz,rng.randrange(360),scale=1.6+rng.random()*.7)
            if (row+col)%2==0:place('Greenery','native:MixedPlanting',px+2,pz-2,rng.randrange(360),scale=.9+rng.random()*.5)
for x in range(-212,195,13):
    for z in range(317,590,13):
        if blocked(x,z,5) or 101<x<145 and 326<z<420:continue
        place('Greenery','nature:TreeRound' if (x+z)%3 else 'nature:TreeWide',x+rng.uniform(-2,2),z+rng.uniform(-2,2),rng.randrange(360),scale=1.15+rng.random()*.5);place('Greenery','native:MixedPlanting',x+3,z+2,scale=1.1)
for s in streets:
    length=math.hypot(s['bx']-s['ax'],s['bz']-s['az']);dx=(s['bx']-s['ax'])/length;dz=(s['bz']-s['az'])/length
    for d in range(16,int(length)-10,26):
        x=s['ax']+dx*d-dz*7.5;z=s['az']+dz*d+dx*7.5
        if any(distance((x,z),(other['ax'],other['az']),(other['bx'],other['bz']))<7 for other in streets):continue
        place('Props','native:QuarterLamp',x,z,math.degrees(math.atan2(dx,dz))+90,y=.16)
assert len(buildings)==25
payload=dict(version=3,name='PINEBROOK',bounds=dict(x=-290,z=198,width=500,depth=470),roads=roads,streets=streets,paths=paths,plazas=plazas,parking=parking,clearings=clearings,retained=retained,placements=placements)
(OUT/'PinebrookLayout.json').write_text(json.dumps(payload,indent=2),encoding='utf-8')
print('Buildings',len(buildings),'Placements',len(placements),'Paths',len(paths),'Road shapes',len(roads))
