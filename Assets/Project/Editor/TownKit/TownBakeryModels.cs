using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>Native metre-scale bakery assets. Facade is +Z; loading door is -Z.</summary>
    internal static class TownBakeryModels
    {
        public const float Width = 13;
        public const float Depth = 11;
        public const float Eaves = 6.8f;
        public static readonly Color32[] Colors = {
            new(239,221,185,255), new(250,237,207,255), new(176,155,128,255),
            new(159,48,54,255), new(113,33,41,255), new(192,78,43,255),
            new(225,112,64,255), new(129,66,46,255), new(57,73,73,255),
            new(55,72,78,255), new(104,140,146,255), new(121,78,43,255),
            new(187,136,68,255), new(226,159,58,255), new(249,201,98,255),
            new(67,127,60,255), new(105,155,66,255), new(44,93,56,255),
            new(124,134,132,255), new(239,235,214,255), new(62,66,61,255),
            new(216,175,47,255), new(81,92,96,255), new(193,180,156,255)
        };
        // Kept away from the town shader's terrain-detail columns.
        private static readonly int[] Columns = {0,1,2,3,4,5,6,9,10,11,13,14,16,17,18,19,20,21,22,23,24,28,29,30};
        internal enum Paint { Cream, Trim, Stone, Burgundy, BurgundyDark, Tile, TileLight, TileDark,
            Frame, GlassDark, Glass, Wood, WoodLight, Bread, BreadLight, Leaf, LeafLight, LeafDark,
            Metal, White, Black, Yellow, Asphalt, Pavement }
        public static Vector2 UV(TownColor color)
        {
            int i=(int)color-64;
            if(i<0||i>=Colors.Length)throw new ArgumentOutOfRangeException(nameof(color));
            return new Vector2((Columns[i]+.5f)/32,.25f);
        }
        public static Color32[] Atlas()
        {
            var pixels=new Color32[64];
            for(int i=0;i<pixels.Length;i++)pixels[i]=Colors[0];
            for(int i=0;i<Colors.Length;i++)pixels[Columns[i]]=Colors[i];
            return pixels;
        }
        public static TownColor Color(Paint paint)=>(TownColor)(64+(int)paint);
        public static IReadOnlyList<BakeryAsset> Create() => new[] {
            new BakeryAsset("BakeryHouse", House), new BakeryAsset("WindowPlanter",Planter),
            new BakeryAsset("TopiaryPlanter",Topiary), new BakeryAsset("MenuBoard",Menu),
            new BakeryAsset("BreadTrolley",Trolley), new BakeryAsset("CratePallet",Crates),
            new BakeryAsset("IronFence",Fence), new BakeryAsset("BrickPier",Pier),
            new BakeryAsset("StreetLantern",StreetLamp), new BakeryAsset("GardenBed",Garden)
        };
        private static BakeryDraft House()
        {
            var d=new BakeryDraft();
            d.Solid("Building",new Vector3(0,3.4f,0),new Vector3(Width,Eaves,Depth),Paint.Cream);
            d.Box(new Vector3(0,.16f,0),new Vector3(13.18f,.32f,11.18f),Paint.Stone);
            foreach(float y in new[]{.4f,3.35f,6.62f})
                d.Box(new Vector3(0,y,0),new Vector3(13.24f,.18f,11.24f),Paint.Trim);
            // Facade masonry joints are relief, not a texture or floating decals.
            for(float y=.68f;y<6.5f;y+=.58f)
            {
                d.Box(new Vector3(0,y,5.505f),new Vector3(13,.022f,.015f),Paint.Stone);
                d.Box(new Vector3(0,y,-5.505f),new Vector3(13,.022f,.015f),Paint.Stone);
            }
            foreach(float x in new[]{-6.36f,6.36f})
                foreach(float z in new[]{-5.43f,5.43f})
                    for(float y=.7f;y<6.5f;y+=.56f)
                        d.Box(new Vector3(x,y,z),new Vector3(.34f,.5f,.3f),Paint.Trim);
            foreach(float x in new[]{-4.35f,0,4.35f})Window(d,new Vector3(x,4.94f,5.56f),1.9f,1.96f,0);
            foreach(float x in new[]{-3.35f,3.35f})Window(d,new Vector3(x,5,-5.56f),1.8f,1.9f,180);
            foreach(float x in new[]{-6.56f,6.56f})
                foreach(float z in new[]{-3.3f,.1f,3.5f})Window(d,new Vector3(x,5,z),1.65f,1.9f,x<0?270:90);
            foreach(float x in new[]{-4.15f,4.15f})
            {
                Display(d,new Vector3(x,0,5.53f),3.5f,0);
                Awning(d,new Vector3(x,3.25f,5.55f),3.8f,0);
            }
            foreach(float z in new[]{-2.75f,1.2f})
            {
                Display(d,new Vector3(-6.53f,0,z),3.2f,270);
                Awning(d,new Vector3(-6.55f,3.25f,z),3.5f,270);
            }
            FrontDoor(d);
            d.Box(new Vector3(0,3.34f,5.85f),new Vector3(5.8f,.95f,.36f),Paint.BurgundyDark);
            d.Box(new Vector3(0,3.34f,6.06f),new Vector3(5.64f,.85f,.1f),Paint.Burgundy);
            foreach(float x in new[]{-1.55f,1.55f})WallLamp(d,new Vector3(x,2.62f,5.85f),0);
            // Rear goods door is closed scenery; collection happens in the bay outside it.
            var rear=new BakeryDraft();
            rear.Box(new Vector3(0,1.53f,0),new Vector3(4.5f,3.06f,.22f),Paint.Frame);
            rear.Box(new Vector3(0,1.49f,.15f),new Vector3(4.14f,2.98f,.1f),Paint.Metal);
            for(float y=.16f;y<3;y+=.19f)rear.Box(new Vector3(0,y,.215f),new Vector3(4.1f,.025f,.028f),Paint.Frame);
            rear.Solid("Canopy",new Vector3(0,3.82f,1.06f),new Vector3(6.15f,.15f,2.25f),Paint.Frame);
            for(float x=-2.8f;x<=2.8f;x+=.32f)rear.Box(new Vector3(x,3.91f,1.06f),new Vector3(.024f,.02f,2.25f),Paint.Metal);
            foreach(float x in new[]{-2.8f,2.8f})
            {
                rear.Beam(new Vector3(x,3.15f,.18f),new Vector3(x,3.75f,1.95f),.1f,Paint.Frame);
                rear.Box(new Vector3(x,3.42f,.1f),new Vector3(.13f,.65f,.13f),Paint.Frame);
            }
            rear.Box(new Vector3(4.85f,1.18f,.08f),new Vector3(1.1f,2.36f,.14f),Paint.Frame);
            rear.Box(new Vector3(5.15f,1.08f,.2f),new Vector3(.07f,.18f,.06f),Paint.Metal);
            WallLamp(rear,new Vector3(4.9f,2.83f,.25f),0);
            d.Append(rear,new Vector3(0,0,-5.57f),180);
            Roof(d);
            foreach(float x in new[]{-6.64f,6.64f})
            {
                d.Cylinder(new Vector3(x,3.43f,-5.4f),.07f,.07f,6.5f,Paint.Metal,8);
                d.Beam(new Vector3(x,.22f,-5.4f),new Vector3(x,.11f,-5.67f),.14f,Paint.Metal);
                foreach(float y in new[]{1f,3.4f,5.9f})d.Box(new Vector3(x,y,-5.4f),new Vector3(.18f,.07f,.19f),Paint.Frame);
            }
            return d;
        }
        private static void Roof(BakeryDraft d)
        {
            Vector3 a=new(-6.95f,Eaves,5.95f),b=new(6.95f,Eaves,5.95f),c=new(6.95f,Eaves,-5.95f),e=new(-6.95f,Eaves,-5.95f);
            Vector3 r=new(1,9.05f,0),l=new(-1,9.05f,0);
            Tiles(d,a,b,r,l,11,28);Tiles(d,c,e,l,r,11,28);
            Tiles(d,b,c,r,r,11,24);Tiles(d,e,a,l,l,11,24);
            foreach(var pair in new[]{(a,l),(b,r),(c,r),(e,l),(l,r)})d.Beam(pair.Item1+Vector3.up*.06f,pair.Item2+Vector3.up*.06f,.14f,Paint.TileLight);
            foreach(float z in new[]{-5.84f,5.84f})d.Box(new Vector3(0,6.77f,z),new Vector3(13.9f,.13f,.14f),Paint.Frame);
            // Chimney penetrates the roof, its base is below the actual sloped surface.
            d.Box(new Vector3(-2.5f,8.94f,-1.15f),new Vector3(.95f,2.7f,.9f),Paint.Trim);
            d.Box(new Vector3(-2.5f,10.31f,-1.15f),new Vector3(1.12f,.15f,1.05f),Paint.Stone);
            d.Box(new Vector3(-2.5f,10.49f,-1.15f),new Vector3(.56f,.25f,.52f),Paint.Frame);
            d.Box(new Vector3(-2.5f,10.65f,-1.15f),new Vector3(.92f,.12f,.86f),Paint.Frame);
        }
        private static void Tiles(BakeryDraft d,Vector3 a,Vector3 b,Vector3 c,Vector3 e,int rows,int columns)
        {
            var normal=Vector3.Cross(b-a,e-a).normalized;
            if(normal.y<0){var swap=a;a=b;b=swap;swap=c;c=e;e=swap;normal=Vector3.Cross(b-a,e-a).normalized;}
            d.Quad(a,b,c,e,Paint.TileDark);
            Vector3 P(float u,float v)=>Vector3.Lerp(Vector3.Lerp(a,b,u),Vector3.Lerp(e,c,u),v);
            for(int row=0;row<rows;row++)
                for(int col=0;col<columns;col++)
                {
                    float u0=(col+.045f)/columns,u1=(col+.955f)/columns,v0=(row+.035f)/rows,v1=(row+.97f)/rows;
                    var p=P(u0,v0)+normal*.023f;var q=P(u1,v0)+normal*.023f;
                    var s=P(u1,v1)+normal*.009f;var t=P(u0,v1)+normal*.009f;
                    d.Quad(p,q,s,t,(row+col*3)%7==0?Paint.TileLight:Paint.Tile);
                    d.Quad(p-normal*.021f,q-normal*.021f,q,p,Paint.TileDark);
                }
        }
        private static void Window(BakeryDraft d,Vector3 center,float width,float height,float yaw)
        {
            var w=new BakeryDraft();
            w.Box(Vector3.zero,new Vector3(width+.26f,height+.28f,.15f),Paint.Trim);
            w.Box(new Vector3(0,0,.11f),new Vector3(width,height,.06f),Paint.GlassDark);
            w.Box(new Vector3(-width*.25f,height*.25f,.15f),new Vector3(width*.44f,height*.43f,.012f),Paint.Glass);
            w.Box(new Vector3(0,0,.18f),new Vector3(.075f,height,.06f),Paint.Trim);
            w.Box(new Vector3(0,0,.18f),new Vector3(width,.065f,.06f),Paint.Trim);
            w.Box(new Vector3(0,-height/2-.13f,.12f),new Vector3(width+.4f,.12f,.38f),Paint.Wood);
            d.Append(w,center,yaw);
        }
        private static void Display(BakeryDraft d,Vector3 position,float width,float yaw)
        {
            var w=new BakeryDraft();
            w.Box(new Vector3(0,1.5f,.1f),new Vector3(width,2.4f,.16f),Paint.Wood);
            w.Box(new Vector3(0,1.55f,.2f),new Vector3(width-.15f,2.12f,.06f),Paint.GlassDark);
            foreach(float x in new[]{-width/2,width/2})w.Box(new Vector3(x,1.53f,.55f),new Vector3(.14f,2.45f,.85f),Paint.WoodLight);
            for(int shelf=0;shelf<3;shelf++)
            {
                float y=.63f+shelf*.63f;
                w.Box(new Vector3(0,y,.57f),new Vector3(width-.15f,.09f,.74f),Paint.WoodLight);
                for(int i=0;i<6;i++)
                {
                    var p=new Vector3((i-2.5f)*(width-.4f)/6,y+.16f,.58f);
                    Loaf(w,p,(i+shelf)%3);
                }
            }
            foreach(float y in new[]{.31f,2.75f})w.Box(new Vector3(0,y,.55f),new Vector3(width+.1f,.14f,.85f),Paint.WoodLight);
            w.Box(new Vector3(0,1.53f,.93f),new Vector3(.07f,2.4f,.055f),Paint.WoodLight);
            w.Box(new Vector3(0,2.55f,.46f),new Vector3(width-.25f,.045f,.04f),Paint.BreadLight);
            d.Append(w,position,yaw);
        }
        private static void Loaf(BakeryDraft d,Vector3 p,int kind)
        {
            d.Ellipsoid(p,new Vector3(kind==1?.15f:.24f,.13f,kind==1?.32f:.19f),kind==2?Paint.BreadLight:Paint.Bread,8,4);
            for(int i=-1;i<=1;i++)d.Box(p+new Vector3(i*.08f,.11f,0),new Vector3(.024f,.025f,.18f),Paint.BreadLight,Quaternion.Euler(0,-23,0));
        }
        private static void Awning(BakeryDraft d,Vector3 position,float width,float yaw)
        {
            var w=new BakeryDraft();
            w.Mesh.ExtrudeX(Vector3.zero,width,new[]{new Vector2(0,0),new Vector2(-.5f,1.3f),new Vector2(-.56f,1.3f),new Vector2(-.06f,0)},Color(Paint.Burgundy));
            w.Box(new Vector3(0,-.53f,1.26f),new Vector3(width,.12f,.09f),Paint.BurgundyDark);
            foreach(float x in new[]{-width/2+.12f,width/2-.12f})w.Beam(new Vector3(x,-.78f,.06f),new Vector3(x,-.52f,1.2f),.055f,Paint.Frame);
            d.Append(w,position,yaw);
        }
        private static void FrontDoor(BakeryDraft d)
        {
            d.Box(new Vector3(0,1.4f,5.69f),new Vector3(2.22f,2.8f,.24f),Paint.WoodLight);
            d.Box(new Vector3(0,1.37f,5.86f),new Vector3(1.82f,2.65f,.12f),Paint.Wood);
            d.Box(new Vector3(0,1.81f,5.94f),new Vector3(1.57f,1.6f,.03f),Paint.Glass);
            d.Box(new Vector3(0,1.81f,5.98f),new Vector3(.065f,1.6f,.035f),Paint.WoodLight);
            d.Box(new Vector3(0,.49f,5.95f),new Vector3(1.55f,.5f,.045f),Paint.WoodLight);
            d.Box(new Vector3(-.68f,1.25f,6.02f),new Vector3(.05f,.38f,.08f),Paint.Metal);
        }
        private static void WallLamp(BakeryDraft d,Vector3 position,float yaw)
        {
            var w=new BakeryDraft();
            w.Box(new Vector3(0,0,-.07f),new Vector3(.17f,.33f,.13f),Paint.Frame);
            w.Beam(new Vector3(0,.12f,0),new Vector3(0,.12f,.3f),.055f,Paint.Frame);
            w.Box(new Vector3(0,-.16f,.3f),new Vector3(.21f,.32f,.21f),Paint.BreadLight);
            foreach(float x in new[]{-.11f,.11f})foreach(float z in new[]{.19f,.41f})w.Box(new Vector3(x,-.16f,z),new Vector3(.025f,.36f,.025f),Paint.Frame);
            w.Box(new Vector3(0,.035f,.3f),new Vector3(.31f,.075f,.31f),Paint.Frame);
            w.Box(new Vector3(0,-.37f,.3f),new Vector3(.27f,.07f,.27f),Paint.Frame);
            d.Append(w,position,yaw);
        }
        private static BakeryDraft Planter()
        {
            var d=new BakeryDraft();d.Solid("Box",new Vector3(0,.27f,0),new Vector3(3.6f,.54f,.65f),Paint.Wood);
            foreach(float y in new[]{.07f,.47f})d.Box(new Vector3(0,y,0),new Vector3(3.67f,.065f,.71f),Paint.WoodLight);
            for(float x=-1.6f;x<1.7f;x+=.4f)d.Ellipsoid(new Vector3(x,.7f,0),new Vector3(.29f,.29f,.29f),Paint.Leaf,8,4);
            return d;
        }
        private static BakeryDraft Topiary()
        {
            var d=new BakeryDraft();d.Solid("Pot",new Vector3(0,.28f,0),new Vector3(.74f,.56f,.74f),Paint.Wood);
            d.Box(new Vector3(0,.55f,0),new Vector3(.81f,.1f,.81f),Paint.WoodLight);
            d.Cylinder(new Vector3(0,.84f,0),.04f,.04f,.6f,Paint.Wood,6);
            d.Ellipsoid(new Vector3(0,1.02f,0),new Vector3(.35f,.51f,.35f),Paint.Leaf,8,5);
            d.Ellipsoid(new Vector3(0,1.42f,0),new Vector3(.25f,.41f,.25f),Paint.LeafLight,8,4);
            return d;
        }
        private static BakeryDraft Menu()
        {
            var d=new BakeryDraft();
            foreach(float x in new[]{-.41f,.41f})
            {
                d.Beam(new Vector3(x,.04f,.43f),new Vector3(x,1.22f,0),.065f,Paint.WoodLight);
                d.Beam(new Vector3(x,.04f,-.43f),new Vector3(x,1.22f,0),.065f,Paint.WoodLight);
            }
            d.Solid("Board",new Vector3(0,.77f,.17f),new Vector3(.9f,.85f,.12f),Paint.WoodLight);
            d.Box(new Vector3(0,.77f,.24f),new Vector3(.76f,.72f,.025f),Paint.Black);
            for(int i=0;i<4;i++)d.Box(new Vector3(i%2*.055f,.78f-i*.12f,.263f),new Vector3(.5f-i*.035f,.017f,.006f),Paint.White);
            return d;
        }
        private static BakeryDraft Trolley()
        {
            var d=new BakeryDraft();
            foreach(float x in new[]{-.49f,.49f})foreach(float z in new[]{-.36f,.36f})
            {
                d.Box(new Vector3(x,1.1f,z),new Vector3(.055f,1.9f,.055f),Paint.Metal);
                d.Cylinder(new Vector3(x,.12f,z),.12f,.12f,.055f,Paint.Black,8,Quaternion.Euler(0,0,90));
            }
            for(int i=0;i<5;i++)
            {
                float y=.3f+i*.35f;d.Box(new Vector3(0,y,0),new Vector3(1.03f,.035f,.78f),Paint.Metal);
                for(int j=0;j<3;j++)Loaf(d,new Vector3((j-1)*.31f,y+.12f,0),i%3);
            }
            d.Solids.Add(new BakerySolid("Rack",new Vector3(0,1.02f,0),new Vector3(1.04f,2.04f,.81f),Quaternion.identity));
            return d;
        }
        private static BakeryDraft Crates()
        {
            var d=new BakeryDraft();
            for(int i=0;i<5;i++)d.Box(new Vector3((i-2)*.23f,.16f,0),new Vector3(.18f,.08f,1.1f),Paint.WoodLight);
            foreach(float z in new[]{-.4f,.4f})d.Box(new Vector3(0,.075f,z),new Vector3(1.2f,.15f,.15f),Paint.Wood);
            for(int level=0;level<3;level++)
                {
                    float y=.36f+level*.4f;d.Box(new Vector3(0,y,-.37f),new Vector3(1.02f,.09f,.055f),Paint.WoodLight);
                    d.Box(new Vector3(0,y,.37f),new Vector3(1.02f,.09f,.055f),Paint.WoodLight);
                    d.Box(new Vector3(-.49f,y,0),new Vector3(.055f,.09f,.74f),Paint.WoodLight);
                    d.Box(new Vector3(.49f,y,0),new Vector3(.055f,.09f,.74f),Paint.WoodLight);
                    // Three slats per crate, keeping the visible gaps real.
                    for(int slat=1;slat<3;slat++)
                    {
                        d.Box(new Vector3(0,y+slat*.12f,-.37f),new Vector3(1.02f,.09f,.055f),Paint.WoodLight);
                        d.Box(new Vector3(0,y+slat*.12f,.37f),new Vector3(1.02f,.09f,.055f),Paint.WoodLight);
                    }
                }
            foreach(float x in new[]{-.48f,.48f})foreach(float z in new[]{-.36f,.36f})d.Box(new Vector3(x,.89f,z),new Vector3(.07f,1.38f,.07f),Paint.Wood);
            d.Solids.Add(new BakerySolid("Stack",new Vector3(0,.85f,0),new Vector3(1.2f,1.7f,1.1f),Quaternion.identity));return d;
        }
        private static BakeryDraft Fence()
        {
            var d=new BakeryDraft();
            for(float x=-1.4f;x<=1.4f;x+=.2f)d.Box(new Vector3(x,.95f,0),new Vector3(.045f,1.8f,.045f),Paint.Frame);
            foreach(float y in new[]{.25f,1.58f})d.Box(new Vector3(0,y,0),new Vector3(3,.045f,.055f),Paint.Frame);
            d.Solids.Add(new BakerySolid("Rail",new Vector3(0,.9f,0),new Vector3(3,1.8f,.065f),Quaternion.identity));return d;
        }
        private static BakeryDraft Pier()
        {
            var d=new BakeryDraft();d.Solid("Masonry",new Vector3(0,1,0),new Vector3(.65f,2,.65f),Paint.Tile);
            for(float y=.2f;y<2;y+=.22f)d.Box(new Vector3(0,y,0),new Vector3(.665f,.025f,.665f),Paint.Stone);
            d.Box(new Vector3(0,2.04f,0),new Vector3(.8f,.14f,.8f),Paint.Trim);return d;
        }
        private static BakeryDraft StreetLamp()
        {
            var d=new BakeryDraft();d.Solid("Base",new Vector3(0,.13f,0),new Vector3(.5f,.26f,.5f),Paint.Frame);
            d.Cylinder(new Vector3(0,1.75f,0),.085f,.055f,3.3f,Paint.Frame,8);
            d.Box(new Vector3(0,3.57f,0),new Vector3(.4f,.5f,.4f),Paint.BreadLight);
            foreach(float x in new[]{-.22f,.22f})foreach(float z in new[]{-.22f,.22f})d.Box(new Vector3(x,3.57f,z),new Vector3(.04f,.54f,.04f),Paint.Frame);
            d.Box(new Vector3(0,3.9f,0),new Vector3(.6f,.16f,.6f),Paint.Frame);return d;
        }
        private static BakeryDraft Garden()
        {
            var d=new BakeryDraft();d.Solid("Bed",new Vector3(0,.09f,0),new Vector3(2.6f,.18f,1.5f),Paint.Stone);
            d.Box(new Vector3(0,.2f,0),new Vector3(2.45f,.07f,1.35f),Paint.Wood);
            for(int i=0;i<5;i++)d.Ellipsoid(new Vector3((i-2)*.45f,.5f,0),new Vector3(.34f,.36f,.48f),i%2==0?Paint.Leaf:Paint.LeafLight,8,4);
            return d;
        }
    }
    internal sealed class BakeryAsset
    {
        public string Id { get; }
        public BakeryDraft Draft { get; }
        public BakeryAsset(string id,Func<BakeryDraft> build){Id=id;Draft=build();}
    }
    internal sealed class BakerySolid
    {
        public string Id {get;}
        public Vector3 Center {get;}
        public Vector3 Size {get;}
        public Quaternion Rotation {get;}
        public BakerySolid(string id,Vector3 center,Vector3 size,Quaternion rotation){Id=id;Center=center;Size=size;Rotation=rotation;}
    }
    internal sealed class BakeryDraft
    {
        public TownMeshBuilder Mesh {get;}=new(TownBakeryModels.UV);
        public List<BakerySolid> Solids {get;}=new();
        public void Box(Vector3 p,Vector3 size,TownBakeryModels.Paint c,Quaternion rotation=default)=>Mesh.Box(p,size,TownBakeryModels.Color(c),rotation);
        public void Solid(string id,Vector3 p,Vector3 size,TownBakeryModels.Paint c){Box(p,size,c);Solids.Add(new BakerySolid(id,p,size,Quaternion.identity));}
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,TownBakeryModels.Paint color)=>Mesh.Quad(a,b,c,d,TownBakeryModels.Color(color));
        public void Cylinder(Vector3 p,float r,float top,float h,TownBakeryModels.Paint c,int sides=8,Quaternion rotation=default)=>Mesh.Cylinder(p,r,top,h,TownBakeryModels.Color(c),sides,rotation);
        public void Ellipsoid(Vector3 p,Vector3 size,TownBakeryModels.Paint c,int sides,int rings)=>Mesh.Ellipsoid(p,size,TownBakeryModels.Color(c),sides,rings);
        public void Beam(Vector3 a,Vector3 b,float width,TownBakeryModels.Paint c)=>Box((a+b)/2,new Vector3(width,width,(b-a).magnitude),c,Quaternion.LookRotation(b-a));
        public void Append(BakeryDraft part,Vector3 position,float yaw)
        {
            var rotation=Quaternion.Euler(0,yaw,0);var source=part.Mesh.Build("Part");
            try{Mesh.Append(source,Matrix4x4.TRS(position,rotation,Vector3.one));}
            finally{UnityEngine.Object.DestroyImmediate(source);}
            foreach(var solid in part.Solids)Solids.Add(new BakerySolid(solid.Id,position+rotation*solid.Center,solid.Size,rotation*solid.Rotation));
        }
    }
}
