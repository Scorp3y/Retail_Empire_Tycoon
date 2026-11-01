"""Rebuild the measured audit plan; this does not propose final placement coordinates."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT=Path(__file__).resolve().parents[1]
survey=json.loads((ROOT/'ArtSource/Pinebrook/Audit/Survey.json').read_text())
output=ROOT/'Docs/Pinebrook/ExistingCoordinatePlan.png'
image=Image.new('RGB',(1650,1260),'#f4f1e6');draw=ImageDraw.Draw(image)
font=lambda size:ImageFont.truetype('C:/Windows/Fonts/arial.ttf',size)
small,normal,title=font(16),font(20),font(30)
def point(x,z):return (80+(x+220)*2.45,1120-(z-190)*2.45)
def rect(x,z,w,d,fill,outline=None,width=1):
    a,b=point(x,z+d),point(x+w,z);draw.rectangle((*a,*b),fill=fill,outline=outline,width=width)
def label(x,z,text,fill='#253f34',f=normal):draw.text(point(x,z),text,font=f,fill=fill)
draw.text((60,25),'PINEBROOK / EXISTING DISTRICT AUDIT',font=title,fill='#253f34')
draw.text((60,65),'Measured scene geometry. KEEP buildings, roads, park and the single crossing.',font=normal,fill='#536653')
rect(-220,190,440,400,'#e1e9d1')
for x in range(-200,241,40):
    draw.line((point(x,190),point(x,590)),fill='#c2ceb8');draw.text((point(x,590)[0]-15,128),str(x),font=small,fill='#536653')
for z in range(190,591,40):
    draw.line((point(-220,z),point(220,z)),fill='#c2ceb8');draw.text((27,point(-220,z)[1]-8),str(z),font=small,fill='#536653')
rect(82,300,76,156,'#b0ce99','#73946b',2)
label(89,383,'EXISTING PARK',f=small);label(89,373,'KEEP',f=small)
for road in survey['roads']:
    rect(road['x']-3,road['z']-3,road['width']+6,road['depth']+6,'#ddd9c8')
    rect(road['x'],road['z'],road['width'],road['depth'],'#68797a')
for n,b in enumerate(survey['buildings'],1):
    p,s=b['position'],b['size'];rect(p['x']-s['x']/2,p['z']-s['z']/2,s['x'],s['z'],'#d0ac6c','#80633d',2)
    draw.text(point(p['x']-2,p['z']+2),str(n),font=small,fill='#273932')
for z in (226.8,229.2):draw.line((point(-220,z),point(220,z)),fill='#8a7156',width=3)
for x in range(-220,221,4):draw.line((point(x,225.5),point(x,230.5)),fill='#a18c6b',width=2)
cross=survey['crossing'];x,z=cross['x'],cross['z'];rect(x-5,z-4,10,8,'#dcd8c7','#a45d44',3)
label(-14,218,'ONE CROSSING / KEEP',f=small)
label(-190,564,'NORTH / NORTHWEST EXPANSION STUDY')
label(-190,551,'No new building positions committed.',f=small)
label(-22,299,'Pinebrook axis',f=small)
label(-192,204,'Railway: retained alignment',f=small)
legend_x=1210
draw.text((legend_x,150),'EXISTING BUILDINGS',font=normal,fill='#253f34')
for n,b in enumerate(survey['buildings'],1):
    draw.text((legend_x,192+(n-1)*34),f'{n:02}  '+b['name'],font=small,fill='#253f34')
notes=['Floor spacing: 3.2 m','Road / sidewalk: 8 / 3 m','Grid spacing: 40 m','Crossing: X -24, Z 228','13 architecture objects retained','','Ochre: measured building bounds','Green: existing park study area','Grey: road rectangles','Cream: sidewalk width guide','','Park boundary is schematic;','building and road bounds are measured.']
for i,text in enumerate(notes):draw.text((legend_x,690+i*26),text,font=small,fill='#536653')
draw.text((70,1170),'X / Z in metres. North is +Z. Road Y 0.12 m, sidewalk Y 0.16 m.',font=normal,fill='#536653')
draw.text((70,1206),'Phase 0 evidence only. Existing sky, vegetation, buildings and scene assets are unchanged.',font=small,fill='#536653')
image.save(output)
