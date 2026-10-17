using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>English generation brief: a restrained contemporary woodland neighbourhood, cream brick,
    /// warm timber accents, sage frames, graphite standing-seam roofs, readable entrances and rich but
    /// economical street details. Ground-centred metre pivots; all future model lettering in English.</summary>
    internal static class TownForestModels
    {
        internal static Color32[] Palette()
        {
            var p=(Color32[])TownPalette.Colors.Clone();
            p[(int)TownColor.Cream]=new Color32(222,214,190,255);
            p[(int)TownColor.Brick]=new Color32(190,178,151,255);
            p[(int)TownColor.RoofDark]=new Color32(54,61,65,255);
            p[(int)TownColor.Frame]=new Color32(48,62,57,255);
            p[(int)TownColor.Glass]=new Color32(75,110,119,255);
            p[(int)TownColor.Mint]=new Color32(128,149,125,255);
            p[(int)TownColor.WoodLight]=new Color32(177,134,83,255);
            p[(int)TownColor.Pavement]=new Color32(197,193,177,255);
            p[(int)TownColor.Asphalt]=new Color32(65,72,76,255);
            p[(int)TownColor.Sand]=new Color32(187,176,146,255);
            return p;
        }
        internal static IReadOnlyList<EquipmentModel> Create()=>new[]{
            new EquipmentModel("CourtyardHouse",()=>Apartment(false,false)),
            new EquipmentModel("PassageHouse",()=>Apartment(true,false)),
            new EquipmentModel("CafeCorner",()=>Apartment(false,true)),
            new EquipmentModel("MarketCorner",()=>Apartment(false,true)),
            new EquipmentModel("TownhouseA",()=>Townhouse(false)),new EquipmentModel("TownhouseB",()=>Townhouse(true)),
            new EquipmentModel("CommunityPavilion",Pavilion),new EquipmentModel("TimberBench",()=>Bench(true)),
            new EquipmentModel("ShortBench",()=>Bench(false)),new EquipmentModel("QuarterLamp",Lamp),
            new EquipmentModel("TreePlanter",Planter),new EquipmentModel("UrbanTree",Tree),
            new EquipmentModel("MixedPlanting",Planting),new EquipmentModel("FlowerStrip",Flowers),
            new EquipmentModel("ParcelLocker",Parcels),new EquipmentModel("BinEnclosure",Bins),
            new EquipmentModel("CycleShelter",()=>Shelter(false)),new EquipmentModel("BusShelter",()=>Shelter(true)),
            new EquipmentModel("StreetBin",Bin),new EquipmentModel("Pergola",Pergola),
            new EquipmentModel("PlayTower",Play),new EquipmentModel("SwingSet",Swing),
            new EquipmentModel("Sandpit",Sandpit),new EquipmentModel("DistrictMarker",()=>Marker(false)),
            new EquipmentModel("TrailMarker",()=>Marker(true)),new EquipmentModel("CafeTable",Table),new EquipmentModel("CafeUmbrella",Umbrella),
            new EquipmentModel("CafeMenu",Menu),new EquipmentModel("Mailboxes",Mailboxes),
            new EquipmentModel("UtilityScreen",Utility),new EquipmentModel("BikeRack",BikeRack)
        };
        private static EquipmentDraft Apartment(bool passage,bool shop)
        {
            var d=new EquipmentDraft();const float w=22,depth=12,h=10.2f;
            if(passage)
            {
                foreach(float x in new[]{-6.6f,6.6f})d.Solid("Building wing",new Vector3(x,h/2,0),new Vector3(8.8f,h,depth),TownColor.Cream);
                d.Solid("Passage lintel",new Vector3(0,6.85f,0),new Vector3(4.4f,6.7f,depth),TownColor.Cream);
                d.Box(new Vector3(0,3.5f,0),new Vector3(4.35f,.15f,11.8f),TownColor.Wood);
                foreach(float x in new[]{-2.25f,2.25f})d.Box(new Vector3(x,1.65f,0),new Vector3(.12f,3.3f,11.8f),TownColor.Brick);
            }
            else d.Solid("Closed building envelope",new Vector3(0,h/2,0),new Vector3(w,h,depth),TownColor.Cream);
            foreach(float z in new[]{-6.03f,6.03f})
            {
                // Mortar is sparse and shallow, not thousands of individually modelled bricks.
                for(float y=.65f;y<h-.2f;y+=.7f)
                {
                    if(passage&&y<3.5f)foreach(float side in new[]{-1f,1f})d.Box(new Vector3(side*6.6f,y,z),new Vector3(8.8f,.025f,.025f),TownColor.Brick);
                    else d.Box(new Vector3(0,y,z),new Vector3(w,.025f,.025f),TownColor.Brick);
                }
                foreach(float x in new[]{-7.5f,7.5f})
                {
                    d.Box(new Vector3(x,5.1f,z*1.005f),new Vector3(3.45f,10,.12f),TownColor.WoodLight);
                    for(float sx=x-1.6f;sx<x+1.7f;sx+=.22f)d.Box(new Vector3(sx,5.1f,z*1.02f),new Vector3(.035f,10,.035f),TownColor.Wood);
                }
                float yaw=z>0?0:180;
                foreach(float y in new[]{2f,5.2f,8.4f})foreach(float x in new[]{-8.5f,-4.3f,0,4.3f,8.5f})
                {
                    if(y<3&&(shop||passage&&x==0))continue;
                    Window(d,new Vector3(x,y,z*1.025f),2.1f,2.15f,yaw);
                    if(y>3&&(Mathf.Abs(x)==4.3f||Mathf.Abs(x)==8.5f))Balcony(d,new Vector3(x,y-1.1f,z*1.035f),yaw);
                }
                if(!passage&&!shop)Entrance(d,new Vector3(0,0,z*1.025f),yaw);
                if(shop)
                {
                    foreach(float x in new[]{-7.2f,-2.4f,2.4f,7.2f})Window(d,new Vector3(x,1.65f,z*1.03f),4.35f,2.85f,yaw);
                    Door(d,new Vector3(0,1.5f,z*1.055f),yaw);
                    var q=Quaternion.Euler(0,yaw,0);var p=new Vector3(0,3.45f,z*1.04f);
                    d.Mesh.Box(p+q*new Vector3(0,0,.15f),new Vector3(21.8f,.65f,.3f),TownColor.Frame,q);
                    d.Mesh.Box(p+q*new Vector3(0,-.5f,1),new Vector3(21.8f,.16f,2.1f),TownColor.Mint,q);
                }
                foreach(float y in new[]{.22f,3.35f,6.55f,10.1f})
                {
                    if(passage&&y<3.5f)foreach(float side in new[]{-1f,1f})d.Box(new Vector3(side*6.65f,y,z*1.027f),new Vector3(8.9f,.13f,.22f),TownColor.Pavement);
                    else d.Box(new Vector3(0,y,z*1.027f),new Vector3(22.2f,.13f,.22f),TownColor.Pavement);
                }
            }
            foreach(float x in new[]{-11.03f,11.03f})
            {
                foreach(float z in new[]{-3.8f,0,3.8f})foreach(float y in new[]{2f,5.2f,8.4f})Window(d,new Vector3(x*1.01f,y,z),2,2.1f,x>0?90:270);
                for(float y=.65f;y<h;y+=.7f)d.Box(new Vector3(x,y,0),new Vector3(.025f,.025f,12),TownColor.Brick);
            }
            Roof(d,23.2f,13.2f,10.25f,2.5f);
            foreach(float x in new[]{-10.8f,10.8f})foreach(float z in new[]{-6.38f,6.38f})Beam(d,new Vector3(x,.2f,z),new Vector3(x,10.1f,z),.09f,TownColor.Frame);
            d.Box(new Vector3(-6,12,-1),new Vector3(.9f,2.7f,.8f),TownColor.Brick);
            d.Box(new Vector3(-6,13.4f,-1),new Vector3(1.1f,.16f,1),TownColor.RoofDark);
            return d;
        }
        private static EquipmentDraft Townhouse(bool timber)
        {
            var d=new EquipmentDraft();d.Solid("Townhouse envelope",new Vector3(0,3.15f,0),new Vector3(8.4f,6.3f,10),TownColor.Cream);
            foreach(float z in new[]{-5.05f,5.05f})
            {
                float yaw=z>0?0:180;
                d.Box(new Vector3(timber?-2.1f:2.1f,3.2f,z),new Vector3(3.8f,6.2f,.15f),TownColor.WoodLight);
                for(float x=-3.9f;x<4;x+=.24f)if((x<0)==timber)d.Box(new Vector3(x,3.2f,z*1.02f),new Vector3(.035f,6.2f,.045f),TownColor.Wood);
                foreach(float x in new[]{-2.15f,2.15f})Window(d,new Vector3(x,4.65f,z*1.03f),2.5f,2.1f,yaw);
                Window(d,new Vector3(-2.15f,1.7f,z*1.03f),2.5f,2.4f,yaw);Door(d,new Vector3(2.15f,1.5f,z*1.04f),yaw);
                var q=Quaternion.Euler(0,yaw,0);d.Mesh.Box(new Vector3(2.15f,3.15f,z*1.03f)+q*Vector3.forward*.8f,new Vector3(3,.16f,1.6f),TownColor.RoofDark,q);
            }
            foreach(float x in new[]{-4.25f,4.25f})foreach(float y in new[]{1.8f,4.6f})Window(d,new Vector3(x,y,0),2,2,x>0?90:270);
            // No end overhang: adjoining sections share a party wall without intersecting roofs.
            Roof(d,8.4f,11,6.4f,2.2f);return d;
        }
        private static void Window(EquipmentDraft d,Vector3 p,float w,float h,float yaw)
        {
            var q=Quaternion.Euler(0,yaw,0);
            void B(Vector3 o,Vector3 s,TownColor c)=>d.Mesh.Box(p+q*o,s,c,q);
            B(Vector3.zero,new Vector3(w+.2f,h+.2f,.12f),TownColor.Frame);
            B(new Vector3(0,0,.075f),new Vector3(w,h,.045f),TownColor.Glass);
            foreach(float x in new[]{-w/2,0,w/2})B(new Vector3(x,0,.13f),new Vector3(.065f,h+.08f,.09f),TownColor.Mint);
            foreach(float y in new[]{-h/2,h/2})B(new Vector3(0,y,.13f),new Vector3(w+.1f,.07f,.1f),TownColor.Mint);
            B(new Vector3(0,-h/2-.12f,.18f),new Vector3(w+.35f,.12f,.4f),TownColor.Pavement);
            B(new Vector3(-w*.28f,h*.22f,.11f),new Vector3(w*.12f,h*.4f,.012f),TownColor.Metal);
        }
        private static void Door(EquipmentDraft d,Vector3 p,float yaw)
        {
            var q=Quaternion.Euler(0,yaw,0);d.Mesh.Box(p,new Vector3(1.5f,2.9f,.15f),TownColor.Frame,q);
            d.Mesh.Box(p+q*new Vector3(0,.3f,.1f),new Vector3(1.2f,1.85f,.06f),TownColor.Glass,q);
            d.Mesh.Box(p+q*new Vector3(.45f,-.2f,.17f),new Vector3(.05f,.4f,.06f),TownColor.White,q);
        }
        private static void Entrance(EquipmentDraft d,Vector3 p,float yaw)
        {
            var q=Quaternion.Euler(0,yaw,0);Door(d,p+Vector3.up*1.5f,yaw);
            d.Mesh.Box(p+q*new Vector3(0,3.1f,.8f),new Vector3(3.5f,.17f,1.9f),TownColor.RoofDark,q);
            foreach(float x in new[]{-1.5f,1.5f})d.Mesh.Box(p+q*new Vector3(x,2.65f,.2f),new Vector3(.2f,.35f,.18f),TownColor.Light,q);
        }
        private static void Balcony(EquipmentDraft d,Vector3 p,float yaw)
        {
            var q=Quaternion.Euler(0,yaw,0);
            void B(Vector3 o,Vector3 s,TownColor c)=>d.Mesh.Box(p+q*o,s,c,q);
            B(new Vector3(0,-.08f,.65f),new Vector3(2.55f,.16f,1.5f),TownColor.Pavement);
            B(new Vector3(0,1,1.35f),new Vector3(2.55f,.07f,.07f),TownColor.Frame);
            foreach(float x in new[]{-1.22f,1.22f})B(new Vector3(x,1,.7f),new Vector3(.07f,.07f,1.35f),TownColor.Frame);
            for(float x=-1.2f;x<1.3f;x+=.24f)B(new Vector3(x,.48f,1.35f),new Vector3(.04f,1,.04f),TownColor.Frame);
            B(new Vector3(.65f,.2f,1.15f),new Vector3(.7f,.35f,.35f),TownColor.Wood);
            d.Mesh.Ellipsoid(p+q*new Vector3(.65f,.5f,1.15f),new Vector3(.42f,.22f,.25f),TownColor.Leaf,6,3);
        }
        private static void Roof(EquipmentDraft d,float w,float depth,float y,float rise)
        {
            var roof=new TownMeshBuilder();roof.Gable(Vector3.zero,depth,w,rise,TownColor.RoofDark,TownColor.Cream);
            var m=roof.Build("Graphite roof");try{d.Mesh.Append(m,Matrix4x4.TRS(Vector3.up*y,Quaternion.Euler(0,90,0),Vector3.one));}finally{UnityEngine.Object.DestroyImmediate(m);}
            d.Box(new Vector3(0,y+rise+.03f,0),new Vector3(w,.1f,.16f),TownColor.Frame);
            for(float x=-w/2+.35f;x<w/2;x+=.75f)foreach(float sign in new[]{-1f,1f})Beam(d,new Vector3(x,y+.035f,sign*depth/2),new Vector3(x,y+rise+.04f,0),.035f,TownColor.Metal);
            foreach(float sign in new[]{-1f,1f})d.Box(new Vector3(0,y-.1f,sign*depth/2),new Vector3(w,.12f,.15f),TownColor.Frame);
        }
        private static EquipmentDraft Pavilion()
        {
            var d=new EquipmentDraft();
            d.Solid("Pavilion back",new Vector3(0,1.7f,-4.8f),new Vector3(12,3.4f,.2f),TownColor.WoodLight);
            foreach(float x in new[]{-5.8f,5.8f})foreach(float z in new[]{-4.7f,0,4.7f})d.Solid("Pavilion post",new Vector3(x,1.8f,z),new Vector3(.22f,3.6f,.22f),TownColor.Wood);
            for(float x=-5.7f;x<6;x+=.3f)d.Box(new Vector3(x,1.7f,-4.95f),new Vector3(.06f,3.4f,.05f),TownColor.Wood);
            Roof(d,13.2f,11,3.65f,2.3f);
            d.Box(new Vector3(0,3.5f,4.8f),new Vector3(6,.6f,.16f),TownColor.Frame);return d;
        }
        private static EquipmentDraft Bench(bool back)
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-.85f,.85f})d.Solid("Bench leg",new Vector3(x,.23f,0),new Vector3(.1f,.46f,.6f),TownColor.Frame);
            for(int i=0;i<4;i++)d.Box(new Vector3(0,.48f,-.25f+i*.16f),new Vector3(2.2f,.075f,.13f),TownColor.WoodLight);
            if(back){foreach(float x in new[]{-.85f,.85f})d.Box(new Vector3(x,.7f,-.3f),new Vector3(.06f,.7f,.06f),TownColor.Frame);for(int i=0;i<3;i++)d.Box(new Vector3(0,.68f+i*.17f,-.32f),new Vector3(2.2f,.13f,.07f),TownColor.WoodLight);}
            d.Collision("Bench seat",new Vector3(0,.45f,0),new Vector3(2.2f,.2f,.65f));return d;
        }
        private static EquipmentDraft Lamp()
        {
            var d=new EquipmentDraft();d.Solid("Lamp pole",Vector3.up*2.7f,new Vector3(.12f,5.4f,.12f),TownColor.Frame);
            d.Box(new Vector3(.6f,5.32f,0),new Vector3(1.2f,.1f,.13f),TownColor.Frame);
            d.Box(new Vector3(1.05f,5.2f,0),new Vector3(.8f,.12f,.38f),TownColor.RoofDark);
            d.Box(new Vector3(1.05f,5.13f,0),new Vector3(.62f,.025f,.28f),TownColor.Light);return d;
        }
        private static EquipmentDraft Planter()
        {
            var d=new EquipmentDraft();d.Mesh.Ground(0,0,3.1f,3.1f,.03f,TownColor.Soil);
            foreach(float x in new[]{-1.65f,1.65f})d.Solid("Planter edge",new Vector3(x,.16f,0),new Vector3(.18f,.32f,3.5f),TownColor.Pavement);
            foreach(float z in new[]{-1.65f,1.65f})d.Solid("Planter edge",new Vector3(0,.16f,z),new Vector3(3.1f,.32f,.18f),TownColor.Pavement);
            int seed=0;for(float x=-1.35f;x<1.4f;x+=.45f)foreach(float z in new[]{-1.3f,1.3f})FlowerSprig(d.Mesh,new Vector3(x,.03f,z),seed++);return d;
        }
        private static EquipmentDraft Tree()
        {
            var d=new EquipmentDraft();TownNature.Tree(d.Mesh,Vector3.zero,1);d.Collision("Tree trunk",new Vector3(0,1.6f,0),new Vector3(.36f,3.2f,.36f));return d;
        }
        private static EquipmentDraft Planting()
        {
            var d=new EquipmentDraft();for(int i=0;i<4;i++)d.Mesh.Ellipsoid(new Vector3(-1.3f+i*.85f,.5f,(i%2)*.35f),new Vector3(.7f,.6f,.65f),i%2==0?TownColor.Leaf:TownColor.LeafLight,7,4);return d;
        }
        private static EquipmentDraft Flowers()
        {
            var d=new EquipmentDraft();d.Mesh.Ground(0,0,4,1,.025f,TownColor.Soil);
            for(int i=0;i<12;i++)FlowerSprig(d.Mesh,new Vector3(-1.8f+i%6*.7f,.03f,-.24f+i/6*.5f),i);return d;
        }
        private static void FlowerSprig(TownMeshBuilder m,Vector3 p,int seed)
        {
            // Small flat petal fans read better at game distance than 3D ellipsoids per petal.
            float height=.26f+(seed%3)*.07f;var top=p+Vector3.up*height;
            m.Box(p+Vector3.up*(height/2),new Vector3(.018f,height,.018f),TownColor.Leaf);
            m.Quad(p+new Vector3(0,.1f,0),p+new Vector3(.14f,.19f,.02f),p+new Vector3(.08f,.21f,.05f),p+new Vector3(0,.15f,0),TownColor.LeafLight);
            var color=seed%3==0?TownColor.Pink:TownColor.Flowers;
            for(int i=0;i<5;i++)
            {
                float angle=i*Mathf.PI*2/5;var v=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var side=Vector3.Cross(Vector3.up,v)*.045f;
                var a=top+v*.025f;var b=top+v*.07f+side;var c=top+v*.14f+Vector3.up*.02f;var e=top+v*.07f-side;
                m.Quad(a,b,c,e,color);m.Quad(e,c,b,a,color);
                float next=(i+1)*Mathf.PI*2/5;m.Triangle(top+Vector3.up*.01f,top+v*.035f+Vector3.up*.01f,top+new Vector3(Mathf.Cos(next)*.035f,.01f,Mathf.Sin(next)*.035f),TownColor.Yellow);
            }
        }
        private static EquipmentDraft Parcels()
        {
            var d=new EquipmentDraft();d.Solid("Locker bank",new Vector3(0,1.1f,0),new Vector3(3,2.2f,.65f),TownColor.Frame);
            for(int x=0;x<5;x++)for(int y=0;y<5;y++){var p=new Vector3(-1.2f+x*.6f,.25f+y*.39f,.34f);d.Box(p,new Vector3(.55f,.35f,.06f),TownColor.WoodLight);d.Box(p+new Vector3(.18f,0,.04f),new Vector3(.08f,.025f,.02f),TownColor.Frame);}
            d.Box(new Vector3(0,2.45f,0),new Vector3(3.3f,.45f,.8f),TownColor.Frame);return d;
        }
        private static EquipmentDraft Bins()
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-3.2f,3.2f})d.Solid("Screen side",new Vector3(x,1,0),new Vector3(.12f,2,3.5f),TownColor.Wood);
            d.Solid("Screen back",new Vector3(0,1,-1.7f),new Vector3(6.4f,2,.12f),TownColor.Wood);
            for(float x=-3;x<3.2f;x+=.22f)d.Box(new Vector3(x,1,-1.8f),new Vector3(.06f,2,.05f),TownColor.WoodLight);
            foreach(float x in new[]{-2f,0,2f}){d.Solid("Waste bin",new Vector3(x,.65f,0),new Vector3(1.4f,1.3f,1.2f),TownColor.Mint);d.Box(new Vector3(x,1.35f,0),new Vector3(1.5f,.15f,1.3f),TownColor.Frame);}return d;
        }
        private static EquipmentDraft Shelter(bool bus)
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-3f,3f})foreach(float z in new[]{-1.4f,1.4f})d.Solid("Shelter post",new Vector3(x,1.4f,z),new Vector3(.12f,2.8f,.12f),TownColor.Frame);
            d.Box(new Vector3(0,2.85f,0),new Vector3(6.5f,.18f,3.2f),TownColor.RoofDark);
            for(float x=-2.8f;x<3;x+=.28f)d.Box(new Vector3(x,1.4f,-1.4f),new Vector3(.08f,2.6f,.1f),TownColor.WoodLight);
            if(bus){d.Box(new Vector3(-.7f,.5f,-.8f),new Vector3(3.7f,.12f,.55f),TownColor.WoodLight);d.Box(new Vector3(2.1f,1.65f,-1.25f),new Vector3(.9f,1.6f,.1f),TownColor.White);d.Box(new Vector3(0,3.15f,-.6f),new Vector3(5.8f,.45f,.1f),TownColor.Frame);}
            else for(float x=-2;x<2.5f;x+=1)Hoop(d,new Vector3(x,0,0));return d;
        }
        private static void Hoop(EquipmentDraft d,Vector3 p)
        {foreach(float z in new[]{-.55f,.55f})Beam(d,p+new Vector3(0,0,z),p+new Vector3(0,.8f,z),.06f,TownColor.Frame);Beam(d,p+new Vector3(0,.8f,-.55f),p+new Vector3(0,.8f,.55f),.06f,TownColor.Frame);}
        private static EquipmentDraft BikeRack(){var d=new EquipmentDraft();for(int i=0;i<3;i++)Hoop(d,new Vector3(i*.9f-.9f,0,0));return d;}
        private static EquipmentDraft Bin(){var d=new EquipmentDraft();d.Solid("Bin body",Vector3.up*.5f,new Vector3(.55f,1,.55f),TownColor.Frame);d.Box(Vector3.up*1.03f,new Vector3(.64f,.08f,.64f),TownColor.WoodLight);d.Box(new Vector3(0,.86f,.285f),new Vector3(.38f,.18f,.04f),TownColor.Rubber);return d;}
        private static EquipmentDraft Pergola()
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-3f,3f})foreach(float z in new[]{-2.2f,2.2f})d.Solid("Pergola post",new Vector3(x,1.5f,z),new Vector3(.17f,3,.17f),TownColor.Wood);
            foreach(float z in new[]{-2.2f,2.2f})d.Box(new Vector3(0,3,z),new Vector3(6.6f,.25f,.2f),TownColor.WoodLight);
            for(float x=-3;x<3.2f;x+=.55f)d.Box(new Vector3(x,3.22f,0),new Vector3(.13f,.2f,5.4f),TownColor.WoodLight);return d;
        }
        private static EquipmentDraft Play()
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})d.Solid("Play post",new Vector3(x,1.5f,z),new Vector3(.15f,3,.15f),TownColor.Wood);
            d.Solid("Play platform",Vector3.up*1.4f,new Vector3(2.3f,.15f,2.3f),TownColor.WoodLight);Roof(d,2.9f,2.9f,3.1f,.9f);
            for(int i=0;i<6;i++)d.Box(new Vector3(0,.23f+i*.22f,-1.7f),new Vector3(.9f,.07f,.18f),TownColor.WoodLight);
            Beam(d,new Vector3(-.5f,0,-1.7f),new Vector3(-.5f,1.6f,-1.7f),.08f,TownColor.Frame);Beam(d,new Vector3(.5f,0,-1.7f),new Vector3(.5f,1.6f,-1.7f),.08f,TownColor.Frame);
            d.Mesh.Quad(new Vector3(-.55f,.15f,4.3f),new Vector3(.55f,.15f,4.3f),new Vector3(.55f,1.43f,1.2f),new Vector3(-.55f,1.43f,1.2f),TownColor.Mint);
            foreach(float x in new[]{-.6f,.6f})Beam(d,new Vector3(x,1.6f,1.2f),new Vector3(x,.32f,4.3f),.13f,TownColor.Mint);return d;
        }
        private static EquipmentDraft Swing()
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-2.4f,2.4f})foreach(float z in new[]{-1.3f,1.3f})Beam(d,new Vector3(x,0,z),new Vector3(x,2.9f,0),.13f,TownColor.Wood);
            Beam(d,new Vector3(-2.6f,2.9f,0),new Vector3(2.6f,2.9f,0),.16f,TownColor.Wood);
            foreach(float x in new[]{-1.1f,1.1f}){foreach(float side in new[]{-.3f,.3f})Beam(d,new Vector3(x+side,.6f,0),new Vector3(x+side,2.9f,0),.025f,TownColor.Frame);d.Box(new Vector3(x,.55f,0),new Vector3(.75f,.1f,.4f),TownColor.Mint);}
            d.Collision("Swing frame",new Vector3(0,1.45f,0),new Vector3(5.3f,2.9f,2.7f));return d;
        }
        private static EquipmentDraft Sandpit(){var d=new EquipmentDraft();d.Mesh.Ground(0,0,4,3,.04f,TownColor.Sand);foreach(float z in new[]{-1.5f,1.5f})d.Solid("Sandpit edge",new Vector3(0,.17f,z),new Vector3(4.4f,.3f,.25f),TownColor.WoodLight);foreach(float x in new[]{-2.1f,2.1f})d.Solid("Sandpit edge",new Vector3(x,.17f,0),new Vector3(.25f,.3f,3),TownColor.WoodLight);return d;}
        private static EquipmentDraft Marker(bool trail){var d=new EquipmentDraft();d.Solid("Sign post",Vector3.up*1.3f,new Vector3(.15f,2.6f,.15f),TownColor.Wood);d.Box(new Vector3(0,2.25f,.06f),new Vector3(trail?2.7f:5.5f,.65f,.18f),TownColor.Frame);return d;}
        private static EquipmentDraft Table()
        {
            var d=new EquipmentDraft();d.Mesh.Cylinder(Vector3.up*.76f,.65f,.65f,.08f,TownColor.WoodLight,12);
            d.Solid("Table stem",Vector3.up*.35f,new Vector3(.16f,.7f,.16f),TownColor.Frame);
            d.Box(Vector3.up*.06f,new Vector3(.75f,.08f,.5f),TownColor.Frame);
            foreach(float x in new[]{-1.2f,1.2f})
            {
                d.Collision("Cafe chair",new Vector3(x,.45f,0),new Vector3(.55f,.9f,.6f));
                d.Box(new Vector3(x,.46f,0),new Vector3(.55f,.08f,.55f),TownColor.WoodLight);
                foreach(float dx in new[]{-.23f,.23f})foreach(float z in new[]{-.23f,.23f})d.Box(new Vector3(x+dx,.24f,z),new Vector3(.045f,.46f,.045f),TownColor.Frame);
                foreach(float dx in new[]{-.23f,.23f})d.Box(new Vector3(x+dx,.74f,-.23f),new Vector3(.045f,.55f,.045f),TownColor.Frame);
                foreach(float y in new[]{.7f,.85f,.98f})d.Box(new Vector3(x,y,-.24f),new Vector3(.5f,.09f,.045f),TownColor.Mint);
            }
            return d;
        }
        private static EquipmentDraft Umbrella()
        {
            var d=new EquipmentDraft();d.Mesh.Cylinder(Vector3.up*1.45f,.035f,.035f,2.9f,TownColor.Frame,8);
            d.Mesh.Cylinder(Vector3.up*2.75f,1.7f,.08f,.65f,TownColor.Mint,12);
            for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Beam(d,new Vector3(0,3.08f,0),new Vector3(Mathf.Cos(a)*1.7f,2.43f,Mathf.Sin(a)*1.7f),.025f,TownColor.Frame);}
            return d;
        }
        private static EquipmentDraft Menu(){var d=new EquipmentDraft();d.Solid("Menu board",new Vector3(0,.7f,0),new Vector3(.85f,1.4f,.18f),TownColor.Wood);d.Box(new Vector3(0,.83f,.105f),new Vector3(.72f,1,.06f),TownColor.Frame);return d;}
        private static EquipmentDraft Mailboxes(){var d=new EquipmentDraft();d.Solid("Mail stand",new Vector3(0,.75f,0),new Vector3(1.2f,1.5f,.4f),TownColor.Mint);for(int i=0;i<5;i++)d.Box(new Vector3(0,.25f+i*.26f,.22f),new Vector3(.9f,.035f,.04f),TownColor.Frame);return d;}
        private static EquipmentDraft Utility(){var d=new EquipmentDraft();d.Solid("Utility cabinet",new Vector3(0,.8f,0),new Vector3(1.8f,1.6f,.8f),TownColor.Mint);foreach(float x in new[]{-1.4f,1.4f})d.Solid("Utility screen",new Vector3(x,1,0),new Vector3(.1f,2,2),TownColor.Wood);for(int i=0;i<8;i++)d.Box(new Vector3(0,.25f+i*.17f,.43f),new Vector3(1.5f,.05f,.025f),TownColor.Frame);return d;}
        private static void Beam(EquipmentDraft d,Vector3 a,Vector3 b,float size,TownColor color)=>d.Mesh.Box((a+b)/2,new Vector3(size,size,(b-a).magnitude),color,Quaternion.LookRotation(b-a));
    }
}
