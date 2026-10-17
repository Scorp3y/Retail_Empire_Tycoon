using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>Modular garden boutique models in native metres. Building facades point along +Z.</summary>
    internal static class TownFloristModels
    {
        internal enum Paint {Cream,Trim,Stone,Roof,RoofLight,Wood,WoodLight,Frame,Glass,Pot,Soil,Leaf,LeafLight,FlowerYellow,FlowerPink,FlowerPurple,White,Yellow,Black,Pavement,Asphalt,Metal,Blue}
        private static readonly int[] Columns={0,1,2,3,4,5,6,9,10,11,13,14,16,17,18,19,20,21,22,23,24,28,29};
        private static readonly Color32[] Colors={new(229,220,188,255),new(249,239,205,255),new(171,164,143,255),new(40,74,58,255),new(62,99,76,255),new(133,88,45,255),new(194,142,74,255),new(48,65,58,255),new(88,128,130,255),new(182,90,55,255),new(74,53,36,255),new(52,116,47,255),new(111,156,61,255),new(245,197,51,255),new(231,96,139,255),new(144,88,180,255),new(242,235,208,255),new(219,175,48,255),new(49,53,49,255),new(192,179,153,255),new(78,91,96,255),new(130,139,128,255),new(72,134,167,255)};
        public static TownColor Color(Paint p)=>(TownColor)(128+(int)p);
        public static Vector2 UV(TownColor c){int p=(int)c-128;if(p<0||p>=Columns.Length)throw new ArgumentOutOfRangeException(nameof(c));return new Vector2((Columns[p]+.5f)/32,.25f);}
        public static Color32[] Atlas(){var pixels=new Color32[64];for(int i=0;i<pixels.Length;i++)pixels[i]=Colors[0];for(int i=0;i<Columns.Length;i++)pixels[Columns[i]]=Colors[i];return pixels;}
        public static IReadOnlyList<FloristModel> Create()=>new[]{
            new FloristModel("GardenShop",Shop),new FloristModel("Greenhouse",Greenhouse),new FloristModel("GardenStore",Store),
            new FloristModel("FlowerStand",Stand),new FloristModel("TerracottaFlowers",()=>FlowerPot(Paint.FlowerPink)),
            new FloristModel("YellowFlowers",()=>FlowerPot(Paint.FlowerYellow)),new FloristModel("PurpleFlowers",()=>FlowerPot(Paint.FlowerPurple)),
            new FloristModel("DecorDisplay",Decor),new FloristModel("SeedlingTable",Seedlings),new FloristModel("PlantTrolley",Trolley),
            new FloristModel("DecorCrates",Crates),new FloristModel("GardenFence",Fence),new FloristModel("GardenPier",Pier),
            new FloristModel("HedgePlanter",Hedge),new FloristModel("ShopMenu",Menu),new FloristModel("GardenLantern",Lantern)};
        // Additive kit: large silhouettes stay readable through glass and at the city camera distance.
        public static IReadOnlyList<FloristModel> CreateGardenDetails()=>new[]{
            new FloristModel("BroadleafPot",()=>LeafyPot(false)),new FloristModel("FernPot",()=>LeafyPot(true)),
            new FloristModel("NurseryFoliage",NurseryFoliage),new FloristModel("HangingBasket",HangingBasket),
            new FloristModel("FlowerBed",FlowerBed),new FloristModel("PottedTree",PottedTree),
            new FloristModel("PottingWorkbench",PottingWorkbench),new FloristModel("GreenhousePlantRails",GreenhousePlantRails)};
        private static FloristDraft GreenhousePlantRails()
        {
            var d=new FloristDraft();foreach(float z in new[]{-2.15f,2.15f})d.Box(new Vector3(0,3.17f,z),new Vector3(10.5f,.07f,.07f),Paint.Frame);return d;
        }
        private static void Leaf(FloristDraft d,Vector3 root,float angle,float length,float width,float rise,Paint color)
        {
            var axis=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var across=new Vector3(-axis.z,0,axis.x)*width;
            var middle=root+axis*length*.48f+Vector3.up*rise;
            var tip=root+axis*length+Vector3.up*rise*.55f;
            var left=middle-across;var right=middle+across;var ridge=middle+Vector3.up*.08f;
            foreach(var face in new[]{new[]{root,left,ridge},new[]{root,ridge,right},new[]{left,tip,ridge},new[]{ridge,tip,right}})
            {d.Mesh.Triangle(face[0],face[1],face[2],Color(color));d.Mesh.Triangle(face[2],face[1],face[0],Color(color));}
        }
        private static void Foliage(FloristDraft d,Vector3 root,float scale,bool fern)
        {
            for(int i=0;i<9;i++)
            {
                float angle=i*2.39996f,length=scale*(.58f+(i%3)*.12f),rise=scale*(.38f+(i%4)*.13f);
                var axis=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                if(!fern){Leaf(d,root,angle,length,scale*.19f,rise,i%3==0?Paint.LeafLight:Paint.Leaf);continue;}
                var tip=root+axis*length+Vector3.up*rise;
                d.Beam(root,tip,.018f*scale,Paint.Leaf);
                for(int j=1;j<=4;j++)foreach(float side in new[]{-1f,1f})
                    Leaf(d,Vector3.Lerp(root,tip,j/5f),angle+side*.85f,length*(.40f-j*.045f),scale*.065f,scale*.09f,i%2==0?Paint.Leaf:Paint.LeafLight);
            }
        }
        private static void EmptyPot(FloristDraft d,Vector3 p,float scale)
        {
            d.Mesh.Cylinder(p+Vector3.up*.24f*scale,.28f*scale,.38f*scale,.48f*scale,Color(Paint.Pot),8);
            d.Mesh.Cylinder(p+Vector3.up*.47f*scale,.39f*scale,.39f*scale,.08f*scale,Color(Paint.Pot),8);
            d.Mesh.Cylinder(p+Vector3.up*.514f*scale,.34f*scale,.34f*scale,.008f*scale,Color(Paint.Soil),8);
        }
        private static FloristDraft LeafyPot(bool fern)
        {
            var d=new FloristDraft();EmptyPot(d,Vector3.zero,fern?1:1.2f);
            if(fern)Foliage(d,new Vector3(0,.52f,0),1,true);
            else foreach(float h in new[]{.62f,1.04f,1.48f})
            {d.Beam(new Vector3(0,.52f,0),new Vector3(0,h,0),.045f,Paint.Leaf);Foliage(d,new Vector3(0,h,0),.78f,false);}
            d.Solids.Add(new FloristSolid("Plant pot",new Vector3(0,.27f,0),new Vector3(.88f,.54f,.88f),Quaternion.identity));return d;
        }
        private static FloristDraft NurseryFoliage()
        {
            var d=new FloristDraft();Foliage(d,Vector3.zero,.55f,false);
            foreach(float angle in new[]{0f,2.1f,4.2f})
            {
                var top=new Vector3(Mathf.Cos(angle)*.17f,.65f,Mathf.Sin(angle)*.17f);
                d.Beam(Vector3.zero,top,.018f,Paint.Leaf);
                d.Mesh.Ellipsoid(top,new Vector3(.16f,.12f,.16f),Color(angle==0?Paint.FlowerYellow:Paint.FlowerPink),6,3);
            }
            return d;
        }
        private static FloristDraft HangingBasket()
        {
            var d=new FloristDraft();EmptyPot(d,Vector3.zero,.85f);Foliage(d,new Vector3(0,.45f,0),.75f,true);
            foreach(float angle in new[]{0f,2.1f,4.2f})
            {
                var edge=new Vector3(Mathf.Cos(angle)*.3f,.40f,Mathf.Sin(angle)*.3f);
                d.Beam(edge,new Vector3(0,1.32f,0),.018f,Paint.Metal);
                var flower=edge+new Vector3(Mathf.Cos(angle)*.16f,-.1f,Mathf.Sin(angle)*.16f);
                d.Mesh.Ellipsoid(flower,new Vector3(.20f,.14f,.20f),Color(Paint.FlowerPurple),6,3);
            }
            return d;
        }
        private static FloristDraft FlowerBed()
        {
            var d=new FloristDraft();d.Solid("Raised bed",new Vector3(0,.17f,0),new Vector3(3.1f,.34f,1.15f),Paint.Wood);
            d.Box(new Vector3(0,.35f,0),new Vector3(2.92f,.035f,.97f),Paint.Soil);
            for(int i=0;i<7;i++)
            {
                float x=(i-3)*.41f,z=i%2==0?.19f:-.19f;
                Foliage(d,new Vector3(x,.37f,z),.48f,i%3==0);
                var flower=new Vector3(x,.96f+(i%2)*.15f,z);
                d.Beam(new Vector3(x,.37f,z),flower,.025f,Paint.Leaf);
                d.Mesh.Ellipsoid(flower,new Vector3(.23f,.17f,.23f),Color(i%3==0?Paint.FlowerPink:i%3==1?Paint.FlowerPurple:Paint.FlowerYellow),6,3);
            }
            return d;
        }
        private static FloristDraft PottedTree()
        {
            var d=new FloristDraft();d.Solid("Tree planter",new Vector3(0,.3f,0),new Vector3(1.0f,.6f,1.0f),Paint.Wood);
            d.Box(new Vector3(0,.61f,0),new Vector3(.90f,.025f,.90f),Paint.Soil);
            d.Mesh.Cylinder(new Vector3(0,1.35f,0),.10f,.06f,1.5f,Color(Paint.Wood),7);
            foreach(var p in new[]{new Vector3(0,2.22f,0),new Vector3(-.45f,1.95f,.1f),new Vector3(.4f,2.5f,-.05f)})
                d.Mesh.Ellipsoid(p,new Vector3(.66f,.72f,.61f),Color(p.x<0?Paint.LeafLight:Paint.Leaf),7,4);
            return d;
        }
        private static FloristDraft PottingWorkbench()
        {
            var d=new FloristDraft();foreach(float x in new[]{-1.1f,1.1f})foreach(float z in new[]{-.38f,.38f})d.Box(new Vector3(x,.5f,z),new Vector3(.1f,1,.1f),Paint.Wood);
            d.Box(new Vector3(0,1.02f,0),new Vector3(2.5f,.12f,1.0f),Paint.WoodLight);
            d.Box(new Vector3(0,.24f,0),new Vector3(2.4f,.09f,.9f),Paint.Wood);
            for(int i=0;i<3;i++){EmptyPot(d,new Vector3(-.8f+i*.32f,1.09f,0),.42f);d.Box(new Vector3(-.6f+i*.6f,.42f,0),new Vector3(.46f,.3f,.6f),i==1?Paint.Cream:Paint.RoofLight);}
            // Watering can, curved handle and spout; a rake and hand trowel complete the working corner.
            d.Mesh.Cylinder(new Vector3(.7f,1.32f,.05f),.19f,.19f,.44f,Color(Paint.Blue),8);
            d.Beam(new Vector3(.84f,1.28f,.05f),new Vector3(1.12f,1.6f,.05f),.07f,Paint.Blue);
            d.Beam(new Vector3(.52f,1.32f,.05f),new Vector3(.39f,1.32f,.05f),.04f,Paint.Blue);
            d.Beam(new Vector3(.39f,1.32f,.05f),new Vector3(.39f,1.63f,.05f),.04f,Paint.Blue);
            d.Beam(new Vector3(.39f,1.63f,.05f),new Vector3(.52f,1.63f,.05f),.04f,Paint.Blue);
            d.Beam(new Vector3(-1.14f,.05f,-.35f),new Vector3(-1.14f,1.91f,-.55f),.045f,Paint.WoodLight);
            d.Box(new Vector3(-1.14f,1.9f,-.55f),new Vector3(.48f,.06f,.07f),Paint.Metal);
            for(int i=0;i<5;i++)d.Box(new Vector3(-1.34f+i*.1f,1.99f,-.55f),new Vector3(.025f,.18f,.035f),Paint.Metal);
            d.Box(new Vector3(.05f,1.13f,.25f),new Vector3(.12f,.04f,.37f),Paint.Metal);
            d.Box(new Vector3(.05f,1.14f,.55f),new Vector3(.055f,.06f,.24f),Paint.Wood);
            d.Solids.Add(new FloristSolid("Workbench",new Vector3(0,.55f,0),new Vector3(2.5f,1.1f,1),Quaternion.identity));return d;
        }
        private static FloristDraft Shop()
        {
            var d=new FloristDraft();d.Solid("Shop body",new Vector3(0,2.1f,0),new Vector3(12,4.2f,9),Paint.Cream);
            d.Box(new Vector3(0,.16f,0),new Vector3(12.18f,.32f,9.18f),Paint.Stone);
            d.Box(new Vector3(0,4.08f,0),new Vector3(12.4f,.19f,9.4f),Paint.Trim);
            d.Mesh.Gable(new Vector3(0,4.2f,0),12.6f,9.6f,2.9f,Color(Paint.Black),Color(Paint.Cream));
            RoofTiles(d,6.3f,4.8f,4.2f,7.1f);
            d.Box(new Vector3(-2.9f,6.3f,-2.6f),new Vector3(.7f,2.1f,.7f),Paint.Pot);
            d.Box(new Vector3(-2.9f,7.39f,-2.6f),new Vector3(.92f,.14f,.9f),Paint.Frame);
            foreach(float x in new[]{-3.8f,3.8f})
            {
                DisplayWindow(d,new Vector3(x,1.5f,4.59f),3.55f,2.55f,0);
                for(int i=0;i<3;i++)Append(d,FlowerPot(i%2==0?Paint.FlowerYellow:Paint.FlowerPink),new Vector3(x+(i-1)*.9f,.25f,4.52f),0,.65f);
            }
            d.Box(new Vector3(0,1.35f,4.61f),new Vector3(1.55f,2.7f,.18f),Paint.Wood);
            d.Box(new Vector3(0,1.65f,4.73f),new Vector3(1.25f,1.9f,.04f),Paint.Glass);
            d.Box(new Vector3(.52f,1.25f,4.80f),new Vector3(.055f,.27f,.055f),Paint.Metal);
            d.Box(new Vector3(0,3.53f,4.76f),new Vector3(8.1f,.88f,.18f),Paint.Roof);
            d.Box(new Vector3(0,3.53f,4.86f),new Vector3(7.9f,.69f,.035f),Paint.RoofLight);
            // Front porch is open at the central pedestrian entrance.
            foreach(float x in new[]{-5.8f,-1.5f,1.5f,5.8f})d.Solid("Porch post",new Vector3(x,1.48f,6.15f),new Vector3(.13f,2.96f,.13f),Paint.Wood);
            foreach(float z in new[]{4.85f,6.15f})d.Box(new Vector3(0,3.04f,z),new Vector3(12,.16f,.16f),Paint.Wood);
            for(float x=-5.9f;x<6;x+=.45f)d.Box(new Vector3(x,3.14f,5.5f),new Vector3(.17f,.11f,1.66f),Paint.WoodLight);
            foreach(float z in new[]{-2.6f,.3f,2.9f})DisplayWindow(d,new Vector3(6.09f,1.55f,z),1.95f,2.05f,90);
            foreach(float z in new[]{-2.4f,2.4f})DisplayWindow(d,new Vector3(-6.09f,1.7f,z),2.2f,2.0f,270);
            foreach(float x in new[]{-3.2f,3.2f})DisplayWindow(d,new Vector3(x,1.7f,-4.59f),2.2f,2.0f,180);
            foreach(float z in new[]{-3.4f,3.4f})d.Solid("Side pergola post",new Vector3(7.9f,1.42f,z),new Vector3(.14f,2.84f,.14f),Paint.Wood);
            d.Box(new Vector3(7.05f,2.94f,0),new Vector3(2.1f,.16f,7.2f),Paint.Wood);
            for(float z=-3.5f;z<3.6f;z+=.5f)d.Box(new Vector3(7.05f,3.06f,z),new Vector3(2.15f,.11f,.17f),Paint.WoodLight);
            // Arched attic window: flat polygon with a matching segmented frame.
            var outline=new List<Vector3>{new(-.73f,4.62f,4.81f),new(.73f,4.62f,4.81f),new(.73f,5.35f,4.81f)};
            for(int i=1;i<=10;i++){float t=i*Mathf.PI/10;outline.Add(new Vector3(Mathf.Cos(t)*.73f,5.35f+Mathf.Sin(t)*.73f,4.81f));}
            var center=new Vector3(0,5.18f,4.81f);
            for(int i=0;i<outline.Count;i++){d.Mesh.Triangle(center,outline[i],outline[(i+1)%outline.Count],Color(Paint.Glass));d.Beam(outline[i]+Vector3.forward*.04f,outline[(i+1)%outline.Count]+Vector3.forward*.04f,.105f,Paint.Trim);}
            d.Box(new Vector3(0,5.17f,4.89f),new Vector3(.07f,1.1f,.035f),Paint.Trim);
            return d;
        }
        private static void RoofTiles(FloristDraft d,float width,float depth,float eaves,float ridge)
        {
            foreach(float side in new[]{-1f,1f})
            {
                var normal=new Vector3(side*(ridge-eaves),width,0).normalized;
                for(int row=0;row<9;row++)for(int col=0;col<22;col++)
                {
                    Vector3 P(float t,float z)=>new Vector3(side*width*(1-t),Mathf.Lerp(eaves,ridge,t),z)+normal*.025f;
                    float t0=(row+.06f)/9,t1=(row+.94f)/9,z0=Mathf.Lerp(-depth,depth,(col+.06f)/22),z1=Mathf.Lerp(-depth,depth,(col+.94f)/22);
                    var a=P(t0,z0);var b=P(t0,z1);var c=P(t1,z1);var e=P(t1,z0);
                    if(Vector3.Cross(b-a,e-a).y>0)d.Mesh.Quad(a,b,c,e,Color((row+col)%8==0?Paint.RoofLight:Paint.Roof));else d.Mesh.Quad(b,a,e,c,Color((row+col)%8==0?Paint.RoofLight:Paint.Roof));
                }
            }
            d.Beam(new Vector3(0,ridge+.04f,-depth),new Vector3(0,ridge+.04f,depth),.12f,Paint.RoofLight);
        }
        private static void DisplayWindow(FloristDraft d,Vector3 p,float w,float h,float yaw)
        {
            var frame=new FloristDraft();
            frame.Box(new Vector3(0,0,-.01f),new Vector3(w,h,.025f),Paint.Glass);
            foreach(float x in new[]{-w/2,w/2})frame.Box(new Vector3(x,0,0),new Vector3(.12f,h+.16f,.16f),Paint.RoofLight);
            foreach(float y in new[]{-h/2,h/2})frame.Box(new Vector3(0,y,0),new Vector3(w+.12f,.12f,.16f),Paint.RoofLight);
            frame.Box(new Vector3(0,0,0),new Vector3(.065f,h,.11f),Paint.WoodLight);
            frame.Box(new Vector3(0,-h*.05f,0),new Vector3(w,.07f,.11f),Paint.WoodLight);
            frame.Box(new Vector3(0,-h/2-.12f,.06f),new Vector3(w+.25f,.14f,.4f),Paint.Wood);
            d.Append(frame,p,yaw);
        }
        private static FloristDraft Greenhouse()
        {
            var d=new FloristDraft();d.Solids.Add(new FloristSolid("Glasshouse envelope",new Vector3(0,2,0),new Vector3(10.5f,4,6.5f),Quaternion.identity));
            d.Box(new Vector3(0,.10f,0),new Vector3(10.6f,.2f,6.6f),Paint.Stone);
            foreach(float z in new[]{-3.25f,3.25f})
            {
                foreach(float x in new[]{-5.25f,5.25f})d.Box(new Vector3(x,1.6f,z),new Vector3(.12f,3.2f,.12f),Paint.Roof);
                foreach(float y in new[]{.25f,1.65f,3.2f})d.Box(new Vector3(0,y,z),new Vector3(10.5f,.09f,.1f),Paint.Roof);
                for(float x=-5.25f;x<=5.3f;x+=1.5f){d.Box(new Vector3(x,1.65f,z),new Vector3(.07f,3.1f,.08f),Paint.Roof);d.Beam(new Vector3(x,3.2f,z),new Vector3(x,4.7f,0),.08f,Paint.Roof);}
            }
            foreach(float x in new[]{-5.25f,5.25f})
            {
                foreach(float z in new[]{-1.62f,0,1.62f})d.Box(new Vector3(x,1.65f,z),new Vector3(.08f,3.1f,.07f),Paint.Roof);
                foreach(float y in new[]{.25f,1.65f,3.2f})d.Box(new Vector3(x,y,0),new Vector3(.09f,.09f,6.5f),Paint.Roof);
                d.Beam(new Vector3(x,3.2f,-3.25f),new Vector3(x,4.7f,0),.12f,Paint.Roof);d.Beam(new Vector3(x,4.7f,0),new Vector3(x,3.2f,3.25f),.12f,Paint.Roof);
            }
            d.Beam(new Vector3(-5.25f,4.7f,0),new Vector3(5.25f,4.7f,0),.14f,Paint.Roof);
            foreach(float z in new[]{-2.2f,2.2f})foreach(float x in new[]{-3.5f,0,3.5f})Append(d,Seedlings(),new Vector3(x,.2f,z),0,.82f);
            return d;
        }
        private static FloristDraft Store()
        {
            var d=new FloristDraft();d.Solid("Store body",new Vector3(0,1.8f,0),new Vector3(7,3.6f,5.8f),Paint.Cream);
            d.Box(new Vector3(0,3.7f,0),new Vector3(7.35f,.18f,6.15f),Paint.Roof);
            for(float x=-3.45f;x<3.5f;x+=.35f)d.Box(new Vector3(x,3.81f,0),new Vector3(.035f,.035f,6.1f),Paint.RoofLight);
            d.Box(new Vector3(0,1.43f,2.96f),new Vector3(4.35f,2.86f,.1f),Paint.Frame);
            d.Box(new Vector3(0,1.41f,3.03f),new Vector3(4.1f,2.8f,.08f),Paint.Metal);
            for(float y=.1f;y<2.8f;y+=.17f)d.Box(new Vector3(0,y,3.085f),new Vector3(4.08f,.025f,.02f),Paint.Frame);
            d.Solid("Store canopy",new Vector3(0,3.31f,3.65f),new Vector3(5.3f,.14f,1.55f),Paint.Roof);
            foreach(float x in new[]{-2.4f,2.4f})d.Beam(new Vector3(x,2.72f,3.03f),new Vector3(x,3.27f,4.24f),.11f,Paint.Wood);
            return d;
        }
        private static FloristDraft FlowerPot(Paint flower)
        {
            var d=new FloristDraft();Pot(d,Vector3.zero,flower);d.Solids.Add(new FloristSolid("Pot",new Vector3(0,.25f,0),new Vector3(.7f,.5f,.7f),Quaternion.identity));return d;
        }
        private static void Pot(FloristDraft d,Vector3 p,Paint flower,float scale=1)
        {
            Vector3 P(float x,float y,float z)=>p+new Vector3(x,y,z)*scale;
            d.Mesh.Cylinder(P(0,.23f,0),.25f*scale,.34f*scale,.46f*scale,Color(Paint.Pot),8);
            d.Mesh.Cylinder(P(0,.445f,0),.30f*scale,.30f*scale,.025f*scale,Color(Paint.Soil),8);
            for(int i=0;i<3;i++)
            {
                float x=Mathf.Cos(i*2.1f)*.18f,z=Mathf.Sin(i*2.1f)*.18f,h=.84f+(i%2)*.16f;
                d.Beam(P(x,.43f,z),P(x,h,z),.025f*scale,Paint.Leaf);
                foreach(float side in new[]{-1f,1f})d.Mesh.Ellipsoid(P(x+side*.10f,h-.22f,z),new Vector3(.13f,.045f,.075f)*scale,Color(Paint.LeafLight),6,2);
                for(int petal=0;petal<5;petal++){float t=petal*Mathf.PI*2/5;d.Mesh.Ellipsoid(P(x+Mathf.Cos(t)*.09f,h,z+Mathf.Sin(t)*.09f),new Vector3(.085f,.045f,.085f)*scale,Color(flower),6,2);}
                d.Mesh.Cylinder(P(x,h+.045f,z),.04f*scale,.04f*scale,.04f*scale,Color(Paint.FlowerYellow),6);
            }
        }
        private static FloristDraft Stand()
        {
            var d=new FloristDraft();foreach(float x in new[]{-1.2f,1.2f})foreach(float z in new[]{-.4f,.4f})d.Box(new Vector3(x,.75f,z),new Vector3(.075f,1.5f,.075f),Paint.Wood);
            foreach(float y in new[]{.20f,.95f}){d.Box(new Vector3(0,y,0),new Vector3(2.7f,.1f,1),Paint.WoodLight);for(int i=0;i<4;i++)Pot(d,new Vector3((i-1.5f)*.58f,y+.06f,0),i%4==0?Paint.FlowerPurple:i%4==1?Paint.FlowerPink:i%4==2?Paint.FlowerYellow:Paint.White,.6f);}
            d.Solids.Add(new FloristSolid("Display rack",new Vector3(0,.8f,0),new Vector3(2.7f,1.6f,1),Quaternion.identity));return d;
        }
        private static FloristDraft Seedlings()
        {
            var d=new FloristDraft();foreach(float x in new[]{-1.1f,1.1f})foreach(float z in new[]{-.32f,.32f})d.Box(new Vector3(x,.45f,z),new Vector3(.09f,.9f,.09f),Paint.Wood);
            d.Box(new Vector3(0,.91f,0),new Vector3(2.6f,.1f,.85f),Paint.WoodLight);
            for(int i=0;i<5;i++)Pot(d,new Vector3((i-2)*.48f,.98f,0),i%2==0?Paint.FlowerYellow:Paint.FlowerPurple,.48f);return d;
        }
        private static FloristDraft Decor()
        {
            var d=new FloristDraft();foreach(float x in new[]{-1.1f,1.1f})d.Box(new Vector3(x,.8f,0),new Vector3(.1f,1.6f,.8f),Paint.Wood);
            foreach(float y in new[]{.2f,.9f})
            {
                d.Box(new Vector3(0,y,0),new Vector3(2.4f,.1f,.85f),Paint.WoodLight);
                for(int i=0;i<4;i++)
                {
                    var p=new Vector3((i-1.5f)*.5f,y+.08f,0);var color=i%2==0?Paint.Trim:Paint.Blue;
                    d.Mesh.Cylinder(p+Vector3.up*.17f,.13f,.2f,.34f,Color(color),8);
                    d.Mesh.Cylinder(p+Vector3.up*.4f,.2f,.08f,.12f,Color(color),8);
                    d.Mesh.Cylinder(p+Vector3.up*.49f,.08f,.095f,.075f,Color(color),8);
                    d.Mesh.Cylinder(p+Vector3.up*.535f,.065f,.065f,.005f,Color(Paint.Soil),8);
                }
            }
            d.Solids.Add(new FloristSolid("Decor rack",new Vector3(0,.8f,0),new Vector3(2.4f,1.6f,.85f),Quaternion.identity));return d;
        }
        private static FloristDraft Trolley()
        {
            var d=new FloristDraft();foreach(float x in new[]{-.4f,.4f})foreach(float z in new[]{-.3f,.3f}){d.Box(new Vector3(x,.7f,z),new Vector3(.05f,1.2f,.05f),Paint.Metal);d.Mesh.Cylinder(new Vector3(x,.12f,z),.1f,.1f,.065f,Color(Paint.Black),8,Quaternion.Euler(0,0,90));}
            foreach(float y in new[]{.24f,.85f}){d.Box(new Vector3(0,y,0),new Vector3(.95f,.06f,.75f),Paint.Metal);Pot(d,new Vector3(-.2f,y+.06f,0),Paint.FlowerPink,.45f);Pot(d,new Vector3(.2f,y+.06f,0),Paint.FlowerYellow,.45f);}
            d.Solids.Add(new FloristSolid("Trolley",new Vector3(0,.6f,0),new Vector3(.95f,1.2f,.75f),Quaternion.identity));return d;
        }
        private static FloristDraft Crates()
        {
            var d=new FloristDraft();d.Box(new Vector3(0,.10f,0),new Vector3(1.2f,.2f,1),Paint.Wood);
            foreach(float x in new[]{-.32f,.32f})foreach(float z in new[]{-.25f,.25f})
            {d.Box(new Vector3(x,.58f,z),new Vector3(.58f,.76f,.46f),Paint.WoodLight);d.Box(new Vector3(x,.72f,z),new Vector3(.60f,.05f,.48f),Paint.Wood);d.Box(new Vector3(x,.66f,z+.24f),new Vector3(.22f,.13f,.006f),Paint.White);}
            d.Solids.Add(new FloristSolid("Packed decor",new Vector3(0,.5f,0),new Vector3(1.2f,1,1),Quaternion.identity));return d;
        }
        private static FloristDraft Fence()
        {
            var d=new FloristDraft();for(float x=-1.45f;x<1.5f;x+=.19f)d.Box(new Vector3(x,.85f,0),new Vector3(.04f,1.6f,.04f),Paint.Frame);
            foreach(float y in new[]{.24f,1.45f})d.Box(new Vector3(0,y,0),new Vector3(3,.05f,.055f),Paint.Frame);
            d.Solids.Add(new FloristSolid("Fence",new Vector3(0,.8f,0),new Vector3(3,1.6f,.065f),Quaternion.identity));return d;
        }
        private static FloristDraft Pier(){var d=new FloristDraft();d.Solid("Pier",new Vector3(0,.9f,0),new Vector3(.55f,1.8f,.55f),Paint.Cream);for(float y=.2f;y<1.8f;y+=.24f)d.Box(new Vector3(0,y,0),new Vector3(.56f,.025f,.56f),Paint.Stone);d.Box(new Vector3(0,1.84f,0),new Vector3(.7f,.12f,.7f),Paint.Trim);return d;}
        private static FloristDraft Hedge(){var d=new FloristDraft();d.Solid("Planter",new Vector3(0,.2f,0),new Vector3(2.6f,.4f,.7f),Paint.Wood);for(int i=0;i<5;i++)d.Mesh.Ellipsoid(new Vector3((i-2)*.46f,.6f,0),new Vector3(.38f,.35f,.32f),Color(i%2==0?Paint.Leaf:Paint.LeafLight),6,3);return d;}
        private static FloristDraft Menu(){var d=new FloristDraft();foreach(float x in new[]{-.4f,.4f})foreach(float z in new[]{-.23f,.23f})d.Box(new Vector3(x,.55f,z),new Vector3(.055f,1.1f,.055f),Paint.WoodLight,Quaternion.Euler(z>0?12:-12,0,0));d.Box(new Vector3(0,.8f,.22f),new Vector3(.85f,.8f,.05f),Paint.Roof);for(int i=0;i<4;i++)d.Box(new Vector3(0,.8f-i*.12f,.25f),new Vector3(.55f,.018f,.01f),Paint.Trim);return d;}
        private static FloristDraft Lantern(){var d=new FloristDraft();d.Solid("Lamp foot",new Vector3(0,.15f,0),new Vector3(.42f,.3f,.42f),Paint.Frame);d.Mesh.Cylinder(new Vector3(0,1.5f,0),.07f,.05f,2.8f,Color(Paint.Frame),8);d.Box(new Vector3(0,3.1f,0),new Vector3(.4f,.45f,.4f),Paint.FlowerYellow);d.Box(new Vector3(0,3.37f,0),new Vector3(.54f,.14f,.54f),Paint.Roof);return d;}
        private static void Append(FloristDraft target,FloristDraft part,Vector3 p,float yaw,float scale)
        {
            var mesh=part.Mesh.Build("Decor component");try{target.Mesh.Append(mesh,Matrix4x4.TRS(p,Quaternion.Euler(0,yaw,0),Vector3.one*scale));}finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
    internal sealed class FloristModel
    {
        public string Id{get;}public FloristDraft Draft{get;}
        public FloristModel(string id,Func<FloristDraft> build){Id=id;Draft=build();}
    }
    internal sealed class FloristSolid
    {
        public string Id{get;}public Vector3 Center{get;}public Vector3 Size{get;}public Quaternion Rotation{get;}
        public FloristSolid(string id,Vector3 center,Vector3 size,Quaternion rotation){Id=id;Center=center;Size=size;Rotation=rotation;}
    }
    internal sealed class FloristDraft
    {
        public TownMeshBuilder Mesh{get;}=new(TownFloristModels.UV);
        public List<FloristSolid> Solids{get;}=new();
        public void Box(Vector3 p,Vector3 s,TownFloristModels.Paint c,Quaternion rotation=default)=>Mesh.Box(p,s,TownFloristModels.Color(c),rotation);
        public void Solid(string id,Vector3 p,Vector3 s,TownFloristModels.Paint c){Box(p,s,c);Solids.Add(new FloristSolid(id,p,s,Quaternion.identity));}
        public void Beam(Vector3 a,Vector3 b,float w,TownFloristModels.Paint c)=>Box((a+b)/2,new Vector3(w,w,Vector3.Distance(a,b)),c,Quaternion.LookRotation(b-a));
        public void Append(FloristDraft part,Vector3 p,float yaw)
        {
            var rotation=Quaternion.Euler(0,yaw,0);var mesh=part.Mesh.Build("Component");
            try{Mesh.Append(mesh,Matrix4x4.TRS(p,rotation,Vector3.one));}finally{UnityEngine.Object.DestroyImmediate(mesh);}
            foreach(var solid in part.Solids)Solids.Add(new FloristSolid(solid.Id,p+rotation*solid.Center,solid.Size,rotation*solid.Rotation));
        }
    }
}
