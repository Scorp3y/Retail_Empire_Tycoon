using System;
using RetailEmpireTycoon.BuildSystem;
using UnityEngine;

internal static class WallCollectionFinishes
{
    internal readonly struct Finish
    {
        public readonly string English, Russian, Description, RussianDescription;
        public readonly int Price;
        public Finish(string english,string russian,int price,string description,string russianDescription)
        { English=english; Russian=russian; Price=price; Description=description; RussianDescription=russianDescription; }
    }

    internal static readonly Finish[] Styles =
    {
        new("Store Stripe","Фирменная полоса",105,"Cream plaster, dark green base and charcoal cap.","Кремовая штукатурка, зелёный низ и графитовый кант."),
        new("Warm Plaster","Тёплая штукатурка",85,"Warm cream plaster with a slim cornice.","Тёплая кремовая штукатурка с узким карнизом."),
        new("Sage Wainscot","Шалфейные панели",105,"Sage-green recessed panels below cream plaster.","Шалфейные филёнки под кремовой штукатуркой."),
        new("Teal Wainscot","Бирюзовые панели",105,"Deep teal panels with an ivory upper wall.","Тёмно-бирюзовые панели под светлой штукатуркой."),
        new("Oak Slats","Дубовые рейки",105,"Honey-oak battens with soft edges.","Медовые дубовые рейки с мягкими гранями."),
        new("Fluted Plaster","Рифлёная штукатурка",105,"Vertical flutes in warm white plaster.","Вертикальные канелюры на тёплой белой штукатурке."),
        new("White Subway","Белый кабанчик",105,"Off-white satin tile with staggered joints.","Белая сатиновая плитка с укладкой вразбежку."),
        new("Mint Subway","Мятный кабанчик",105,"Staggered mint-green ceramic tile.","Мятная керамическая плитка с укладкой вразбежку."),
        new("Cream Squares","Кремовые квадраты",105,"Cream square tile with fine joints.","Кремовая квадратная плитка с тонкими швами."),
        new("Checker Tile","Шахматная плитка",105,"Alternating cream and slate ceramic squares.","Чередование кремовых и графитовых квадратов."),
        new("Blue Washroom","Синяя плитка для санузла",105,"Dusty blue tile below warm plaster.","Приглушённая синяя плитка под тёплой штукатуркой."),
        new("Terrazzo","Терраццо",145,"Warm terrazzo with large coloured chips.","Тёплое терраццо с крупной цветной крошкой."),
        new("Red Brick","Красный кирпич",105,"Terracotta masonry with pale grey mortar.","Терракотовая кладка со светло-серым раствором."),
        new("Painted Brick","Крашеный кирпич",105,"Warm white brick with shallow joints.","Тёплый белый кирпич с неглубокими швами."),
        new("Sandstone","Песчаник",145,"Golden ashlar with varied block sizes.","Золотистый песчаник из блоков разной ширины."),
        new("River Stone","Речной камень",145,"Soft grey rounded stones on dark mortar.","Окатанные серые камни на тёмном растворе."),
        new("Shiplap","Доска внахлёст",105,"Horizontal sage boards with overlapped edges.","Горизонтальные шалфейные доски внахлёст."),
        new("Fiber Cement","Фиброцемент",105,"Wide slate-blue panels with fine joints.","Широкие серо-синие панели с тонкими стыками."),
        new("Concrete","Бетонные плиты",85,"Pale cast concrete with form-tie marks.","Светлый бетон со следами крепления опалубки."),
        new("Cinder Block","Шлакоблок",85,"Grey masonry blocks in running bond.","Серые блоки с перевязкой рядов."),
        new("Corrugated Steel","Гофрированная сталь",85,"Dark blue-grey vertical steel ribs.","Тёмная серо-синяя сталь с вертикальными рёбрами."),
        new("Insulated Panel","Сэндвич-панель",85,"Cream insulated panels above a dark kickplate.","Кремовые сэндвич-панели с тёмным цоколем."),
        new("Utility Stripe","Служебная полоса",85,"Light grey with a blue protective band.","Светло-серая стена с синей защитной полосой."),
        new("Safety Stripe","Предупреждающая полоса",85,"Grey wall with diagonal amber safety markings.","Серая стена с диагональной жёлтой разметкой."),
        new("Glass Partition","Стеклянная перегородка",145,"Clear blue-tinted glazing in a charcoal frame.","Прозрачное голубоватое стекло в графитовой раме."),
        new("Frosted Glass","Матовое стекло",145,"A frosted privacy band in a dark teal frame.","Матовая полоса в тёмно-бирюзовой раме."),
        new("Shopfront","Витрина",145,"Display glazing over cream recessed panels.","Витринное стекло над кремовыми филёнками."),
        new("Glass Block","Стеклоблоки",145,"Translucent blue glass blocks with pale joints.","Голубые полупрозрачные стеклоблоки со светлыми швами."),
        new("Timber Screen","Деревянный экран",145,"Open honey-oak slats within a slim frame.","Медовые дубовые рейки с просветами в тонкой раме."),
        new("Half Wall","Низкая перегородка",145,"Sage panels with a glass screen and brass rail.","Шалфейные филёнки, стеклянный экран и латунный поручень.")
    };

    internal const int AtlasWidth=2048, AtlasHeight=1024, Tile=256, Padding=32, FacePixels=192, VerticalPadding=8;
    private static readonly float Period=ShopProportions.CellSize*WallPattern.RepeatCells;
    internal static readonly Color32[] Swatches =
    {
        Hex(0xE9D5AC), Hex(0x33363A), Hex(0x245440), Hex(0x85976C), Hex(0x225052),
        Hex(0xBC8950), Hex(0xD4A465), Hex(0xF0DDBA), Hex(0x586472), Hex(0x444F59),
        Hex(0xC39543), new Color32(126,191,212,80), new Color32(204,226,219,178),
        new Color32(158,209,225,162), Hex(0xA5CED6), Hex(0xD7D9CB)
    };

    internal static Vector2 Swatch(int index) => new((6*Tile+8+index%16*16f)/AtlasWidth,(3*Tile+8+index/16*16f)/AtlasHeight);
    internal static Vector2 Pattern(int style,float y,float height) => new(
        ((style%8)*Tile+Padding)/ (float)AtlasWidth,
        ((style/8)*Tile+VerticalPadding+.5f+Mathf.Clamp01(y/height)*(Tile-2*VerticalPadding-1))/AtlasHeight);

    internal static Texture2D Bake(float height)
    {
        var pixels=new Color32[AtlasWidth*AtlasHeight];
        for(int style=0;style<Styles.Length;style++)
        for(int row=0;row<Tile;row++)
        for(int column=0;column<Tile;column++)
        {
            float x=Mathf.Repeat((column+.5f-Padding)/FacePixels,1)*Period;
            float y=Mathf.Clamp01((row-VerticalPadding)/(float)(Tile-2*VerticalPadding-1))*height;
            pixels[((style/8)*Tile+row)*AtlasWidth+(style%8)*Tile+column]=Sample(style,x,y,height);
        }
        for(int index=0;index<Swatches.Length;index++)
        for(int y=0;y<16;y++)
        for(int x=0;x<16;x++)
            pixels[(3*Tile+index/16*16+y)*AtlasWidth+6*Tile+index%16*16+x]=Swatches[index];
        var texture=new Texture2D(AtlasWidth,AtlasHeight,TextureFormat.RGBA32,false);
        texture.SetPixels32(pixels);texture.Apply();return texture;
    }

    private static Color32 Sample(int style,float x,float y,float height)
    {
        float top=y/height;
        return style switch
        {
            0 => top>.925f?Swatches[1]:top<.35f?Swatches[2]:Swatches[0],
            1 => top>.94f?Swatches[7]:Swatches[0],
            2 => top<.36f?Panel(x,y,.36f*height,0x84966F,0x697E58):Swatches[0],
            3 => top<.36f?Panel(x,y,.36f*height,0x265457,0x1C4042):Swatches[7],
            4 => Boards(x,y,16,0xBC8950),
            5 => Shade(Swatches[7],.95f+.07f*Mathf.Cos(x/Period*16*Mathf.PI*2)),
            6 => TileFace(x,y,15,.075f,.003f,true,0xEEECE3,0xBDB9AB,.025f),
            7 => TileFace(x,y,15,.075f,.003f,true,0xA2C3AA,0x758F7F,.06f),
            8 => TileFace(x,y,8,.275f,.0035f,false,0xE8D2AE,0xB9A283,.045f),
            9 => Checker(x,y),
            10 => top<.40f?TileFace(x,y,11,.19f,.003f,false,0x557C94,0x8EA2A7,.07f):Swatches[7],
            11 => Terrazzo(x,y),
            12 => TileFace(x,y,10,.070f,.0045f,true,0xB2644A,0xA39B8A,.17f),
            13 => TileFace(x,y,10,.070f,.003f,true,0xEBE4D2,0xC2B9A4,.045f),
            14 => TileFace(x,y,5,.255f,.006f,true,0xD3AF75,0xAA8B63,.13f),
            15 => Stone(x,y),
            16 => Shade(Hex(0x889A77),.84f+.19f*Mathf.Repeat(y/.18f,1)),
            17 => TileFace(x,y,1,.32f,.004f,false,0x627984,0x43565E,.06f),
            18 => Concrete(x,y),
            19 => TileFace(x,y,6,.20f,.005f,true,0x777B78,0x545A56,.09f),
            20 => Shade(Swatches[9],.91f+.15f*Mathf.Cos(x/Period*16*Mathf.PI*2)),
            21 => top<.13f?Swatches[1]:TileFace(x,y,4,height*2,.0025f,false,0xE8DBB9,0xB4AA90,.035f),
            22 => top<.26f?Hex(0x427492):Hex(0xB9BABB),
            23 => top<.20f?(Mathf.FloorToInt((x-y)*16/Period)%2==0?Hex(0xD4A538):Swatches[1]):Hex(0xA8A7A0),
            26 => Panel(x,y,.36f*height,0xDEC8A4,0xAA9477),
            27 => TileFace(x,y,11,.20f,.008f,false,0xA4CDD8,0xD9E7DD,.09f,150),
            29 => Panel(x,y,.43f*height,0x87996F,0x5F7655),
            _ => Swatches[7]
        };
    }

    private static Color32 TileFace(float x,float y,int columns,float rise,float gap,bool stagger,uint face,uint grout,float variation,byte alpha=255)
    {
        float width=Period/columns;
        int row=Mathf.FloorToInt(y/rise);
        float at=x+(stagger && row%2!=0?width*.5f:0);
        float u=Mathf.Repeat(at,width),v=Mathf.Repeat(y,rise);
        if(Mathf.Min(u,width-u)<gap || Mathf.Min(v,rise-v)<gap) return Hex(grout);
        float edge=Mathf.Min(Mathf.Min(u,width-u),Mathf.Min(v,rise-v));
        float light=.98f+variation*(Noise(Wrap(Mathf.FloorToInt(at/width),columns),row)-.5f)*2;
        if(edge<gap+.005f) light*=v>rise*.5f||u<width*.5f?1.08f:.89f;
        var color=Shade(Hex(face),light);color.a=alpha;return color;
    }

    private static Color32 Panel(float x,float y,float height,uint face,uint inset)
    {
        float width=Period/6,u=Mathf.Repeat(x,width),v=Mathf.Min(y,height);
        float edge=Mathf.Min(Mathf.Min(u,width-u),Mathf.Min(v,height-v));
        if(edge<.018f)return Hex(face);
        if(edge<.031f)return Shade(Hex(inset),v>height*.5f||u<width*.5f?1.22f:.85f);
        return Hex(inset);
    }
    private static Color32 Checker(float x,float y)
    {
        float size=Period/8;
        bool light=(Mathf.FloorToInt(x/size)+Mathf.FloorToInt(y/size))%2==0;
        return TileFace(x,y,8,size,.002f,false,light?0xE9D7B3u:0x4B5056u,0xAAA293,.035f);
    }
    private static Color32 Boards(float x,float y,int columns,uint face)
    {
        float u=x/Period*columns;
        int column=Mathf.FloorToInt(u);
        return Shade(Hex(face),.93f+.14f*Noise(Wrap(column,columns),0)+.027f*Mathf.Cos(u*28+y*5));
    }
    private static Color32 Concrete(float x,float y)
    {
        float width=Period/3,u=Mathf.Repeat(x,width),v=Mathf.Repeat(y,.55f);
        float boltX=Mathf.Min(Mathf.Abs(u-.055f),Mathf.Abs(u-width+.055f));
        float boltY=Mathf.Min(Mathf.Abs(v-.057f),Mathf.Abs(v-.493f));
        if(boltX*boltX+boltY*boltY<.009f*.009f)return Hex(0x858681);
        return TileFace(x,y,3,.55f,.0045f,false,0xB0AFAB,0x828681,.055f);
    }
    private static Color32 Terrazzo(float x,float y)
    {
        const int columns=18;
        float step=Period/columns;
        int i=Mathf.FloorToInt(x/step),j=Mathf.FloorToInt(y/.115f);
        float dx=x/step-i-.22f-.50f*Noise(i,j),dy=y/.115f-j-.26f-.48f*Noise(i,j+21);
        float size=.12f+.15f*Noise(i,j+47);
        if(Mathf.Abs(dx)*.85f+Mathf.Abs(dy+dx*.25f)<size)
        {
            uint[] chips={0xBC7658,0xBDA059,0x84936C,0x707D7D};
            return Hex(chips[(int)(Noise(i,j+67)*4)]);
        }
        return Hex(0xEAD6AD);
    }
    private static Color32 Stone(float x,float y)
    {
        const int columns=8;
        float u=x/(Period/columns),v=y/.23f;
        int col=Mathf.FloorToInt(u),row=Mathf.FloorToInt(v);
        float first=float.MaxValue,second=float.MaxValue,shade=1;
        for(int a=col-1;a<=col+1;a++)
        for(int b=row-1;b<=row+1;b++)
        {
            int wrapped=Wrap(a,columns);
            float dx=u-(a+.22f+Noise(wrapped,b)*.56f);
            float dy=v-(b+.19f+Noise(wrapped,b+53)*.62f);
            float distance=dx*dx+dy*dy;
            if(distance<first){second=first;first=distance;shade=.86f+.24f*Noise(wrapped,b+93);}
            else if(distance<second)second=distance;
        }
        float edge=second-first;
        if(edge<.037f)return Hex(0x62605A);
        return Shade(Hex(0xA6A298),shade*Mathf.Lerp(.78f,1,Mathf.Clamp01((edge-.037f)/.10f)));
    }
    private static int Wrap(int value,int period)=>(value%period+period)%period;
    private static float Noise(int x,int y)
    {
        unchecked
        {
            uint hash=(uint)(x*73856093 ^ y*19349663);
            hash=(hash^(hash>>13))*1274126177u;
            return ((hash^(hash>>16))&65535)/65536f;
        }
    }
    private static Color32 Hex(uint value) => new((byte)(value>>16),(byte)(value>>8),(byte)value,255);
    private static Color32 Shade(Color32 c,float amount) => new(
        (byte)Mathf.Clamp(Mathf.RoundToInt(c.r*amount),0,255),
        (byte)Mathf.Clamp(Mathf.RoundToInt(c.g*amount),0,255),
        (byte)Mathf.Clamp(Mathf.RoundToInt(c.b*amount),0,255),c.a);
}
