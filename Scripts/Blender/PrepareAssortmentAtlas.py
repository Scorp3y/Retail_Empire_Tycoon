"""Offline packaging artwork; no generated brand or paid service dependency."""
import argparse
import json
import sys
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).parent))
from PIL import Image,ImageDraw,ImageFont
from AssortmentDesigns import COLORS,manifest,products

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--project',required=True)
    root=Path(parser.parse_args().project);folder=root/'ArtSource/Assortment'
    folder.mkdir(parents=True,exist_ok=True)
    atlas=Image.new('RGB',(2048,2048),'#efe6cc');draw=ImageDraw.Draw(atlas)
    font_path='C:/Windows/Fonts/arialbd.ttf'
    for index,(name,color) in enumerate(COLORS.items()):
        x=index%16*128;y=index//16*256
        draw.rectangle((x,y,x+127,y+255),fill=color)
    for p in products():
        x=p['index']%8*256;y=512+p['index']//8*256
        primary=COLORS[p['color']]
        draw.rounded_rectangle((x+5,y+5,x+251,y+251),radius=20,fill=COLORS['ivory'],outline=primary,width=12)
        draw.rectangle((x+15,y+17,x+241,y+49),fill=primary)
        # A simple category pictogram remains readable on tiny shelf objects.
        center=x+128
        if p['storage'] in ('Produce','Groceries'):
            draw.ellipse((center-14,y+68,center+14,y+118),fill=primary)
            draw.ellipse((center-36,y+72,center-5,y+101),fill=primary)
            draw.ellipse((center+5,y+72,center+36,y+101),fill=primary)
        elif p['storage']=='Household':
            draw.polygon([(center,y+65),(center+10,y+83),(center+32,y+90),(center+10,y+99),(center,y+119),(center-10,y+99),(center-32,y+90),(center-10,y+83)],fill=primary)
        else:
            draw.rounded_rectangle((center-27,y+68,center+27,y+113),radius=12,fill=primary)
        words=p['name'].upper().split();lines=[];line=''
        font=ImageFont.truetype(font_path,23)
        for word in words:
            attempt=(line+' '+word).strip()
            if draw.textlength(attempt,font=font)>216 and line:lines.append(line);line=word
            else:line=attempt
        lines.append(line)
        for i,line in enumerate(lines):
            draw.text((center,y+142+i*29),line,font=font,anchor='mm',fill=COLORS['dark'])
        draw.line((x+40,y+213,x+216,y+213),fill=primary,width=5)
    atlas.save(folder/'AssortmentAtlas.png')
    (folder/'Assortment.json').write_text(json.dumps(manifest(),indent=2)+'\n',encoding='utf-8')
    print('Prepared 48 product definitions, 4 fixture definitions and shared 2048px packaging atlas.')

if __name__=='__main__':main()
