using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>English modelling brief: red-brick equipment shop, navy sign, charcoal warehouse, wall-side canopy, commercial retail equipment, clear loading apron.</summary>
    internal static class TownEquipmentModels
    {
        public static Color32[] Atlas()
        {
            var p=(Color32[])TownPalette.Colors.Clone();
            p[(int)TownColor.Brick]=new Color32(173,91,58,255);p[(int)TownColor.BoxDark]=new Color32(113,63,43,255);
            p[(int)TownColor.Blue]=new Color32(40,82,124,255);p[(int)TownColor.RoofDark]=new Color32(51,65,74,255);
            p[(int)TownColor.Frame]=new Color32(38,49,53,255);p[(int)TownColor.Cream]=new Color32(224,213,181,255);
            p[(int)TownColor.Asphalt]=new Color32(76,85,90,255);p[(int)TownColor.Pavement]=new Color32(203,196,178,255);return p;
        }
        public static IReadOnlyList<EquipmentModel> Create()=>new[]{
            new EquipmentModel("EquipmentShop",Shop),new EquipmentModel("EquipmentWarehouse",Warehouse),new EquipmentModel("EquipmentCanopy",Canopy),
            new EquipmentModel("DisplayFridge",Fridge),new EquipmentModel("RefrigeratedCounter",Counter),new EquipmentModel("RetailShelf",Shelf),new EquipmentModel("CheckoutCounter",Checkout),
            new EquipmentModel("LoadedDolly",Dolly),new EquipmentModel("PalletRack",Rack),new EquipmentModel("WrappedPallet",Wrapped),new EquipmentModel("PalletStack",Pallets),
            new EquipmentModel("WallLamp",WallLamp),new EquipmentModel("SafetyBollard",Bollard),new EquipmentModel("EquipmentPlanter",Planter),
            new EquipmentModel("EquipmentFence",Fence),new EquipmentModel("BrickPier",Pier),new EquipmentModel("SiteTree",Tree),new EquipmentModel("SiteLamp",Lamp),
            new EquipmentModel("BlueCustomerCar",()=>Car(TownColor.Blue)),new EquipmentModel("GreenCustomerCar",()=>Car(TownColor.GreenStripe))};
        private static EquipmentDraft Shop()
        {
            var d=new EquipmentDraft();d.Collision("Shop vehicle envelope",new Vector3(0,3.8f,0),new Vector3(26,7.6f,14));
            d.Box(new Vector3(0,.12f,0),new Vector3(26.3f,.24f,14.3f),TownColor.Stone);
            d.Box(new Vector3(0,.25f,0),new Vector3(25.6f,.05f,13.7f),TownColor.White);
            d.Box(new Vector3(0,3.9f,-6.92f),new Vector3(26,7.4f,.24f),TownColor.Brick);
            d.Box(new Vector3(12.92f,3.9f,0),new Vector3(.24f,7.4f,14),TownColor.Brick);
            d.Box(new Vector3(-12.92f,5.55f,0),new Vector3(.24f,4.1f,14),TownColor.Brick);
            d.Box(new Vector3(0,5.55f,6.96f),new Vector3(26,4.1f,.24f),TownColor.Brick);
            for(float y=.65f;y<7.4f;y+=.34f)
            {
                d.Box(new Vector3(0,y,-7.05f),new Vector3(25.8f,.022f,.026f),TownColor.BoxDark);
                d.Box(new Vector3(13.05f,y,0),new Vector3(.026f,.022f,13.8f),TownColor.BoxDark);
                if(y<3.9f)continue;
                d.Box(new Vector3(0,y,7.1f),new Vector3(25.8f,.022f,.026f),TownColor.BoxDark);
                d.Box(new Vector3(-13.05f,y,0),new Vector3(.026f,.022f,13.8f),TownColor.BoxDark);
                for(float x=-12.3f;x<13;x+=2.7f)d.Box(new Vector3(x+((int)(y/.34f)%2)*.9f,y-.17f,7.1f),new Vector3(.026f,.32f,.026f),TownColor.BoxDark);
            }
            foreach(float x in new[]{-12.75f,12.75f})foreach(float z in new[]{-6.75f,6.75f})
                for(float y=.55f;y<7.5f;y+=.65f)d.Box(new Vector3(x,y,z),new Vector3(.60f,.40f,.60f),TownColor.Cream);
            foreach(float y in new[]{.32f,3.40f,7.42f})d.Box(new Vector3(0,y,0),new Vector3(26.4f,.19f,14.4f),TownColor.Cream);
            foreach(float x in new[]{-12.75f,-1.45f,1.45f,12.75f})d.Box(new Vector3(x,1.84f,7.03f),new Vector3(.64f,3.30f,.28f),TownColor.Cream);
            foreach(float z in new[]{-6.75f,-2.25f,2.25f,6.75f})d.Box(new Vector3(-12.98f,1.84f,z),new Vector3(.28f,3.30f,.48f),TownColor.Cream);
            foreach(float x in new[]{-9.2f,-3.05f,3.05f,9.2f})Window(d,new Vector3(x,5.59f,7.20f),3.75f,2.48f,0,true);
            foreach(float z in new[]{-4.5f,0,4.5f})Window(d,new Vector3(-13.13f,5.59f,z),2.8f,2.48f,270,true);
            foreach(float x in new[]{-7.12f,7.12f})Window(d,new Vector3(x,1.88f,7.12f),10.65f,2.9f,0,false);
            foreach(float z in new[]{-4.5f,0,4.5f})Window(d,new Vector3(-13.14f,1.88f,z),4.05f,2.9f,270,false);
            Window(d,new Vector3(0,1.82f,7.16f),2.6f,2.95f,0,false);
            d.Box(new Vector3(0,1.82f,7.18f),new Vector3(.065f,2.9f,.06f),TownColor.Frame);
            foreach(float x in new[]{-.17f,.17f})d.Box(new Vector3(x,1.55f,7.27f),new Vector3(.05f,.45f,.05f),TownColor.Metal);
            d.Box(new Vector3(0,3.71f,7.25f),new Vector3(25.9f,.74f,.32f),TownColor.Blue);
            d.Box(new Vector3(0,7.62f,0),new Vector3(26.6f,.24f,14.6f),TownColor.RoofDark);
            foreach(float z in new[]{-7.17f,7.17f})d.Box(new Vector3(0,7.96f,z),new Vector3(26.6f,.46f,.26f),TownColor.Cream);
            foreach(float x in new[]{-13.17f,13.17f})d.Box(new Vector3(x,7.96f,0),new Vector3(.26f,.46f,14.08f),TownColor.Cream);
            d.Box(new Vector3(-5,8.14f,-3.6f),new Vector3(1.4f,.8f,1.1f),TownColor.Brick);
            d.Box(new Vector3(-5,8.57f,-3.6f),new Vector3(1.6f,.12f,1.3f),TownColor.Frame);
            return d;
        }
        private static void Window(EquipmentDraft d,Vector3 p,float w,float h,float yaw,bool filled)
        {
            var q=Quaternion.Euler(0,yaw,0);
            void B(Vector3 offset,Vector3 size,TownColor color)=>d.Mesh.Box(p+q*offset,size,color,q);
            if(filled)B(Vector3.zero,new Vector3(w,h,.05f),TownColor.Glass);
            foreach(float x in new[]{-w/2,w/2})B(new Vector3(x,0,.04f),new Vector3(.10f,h+.15f,.10f),TownColor.Cream);
            foreach(float y in new[]{-h/2,h/2})B(new Vector3(0,y,.04f),new Vector3(w+.15f,.1f,.1f),TownColor.Cream);
            B(new Vector3(0,0,.055f),new Vector3(.07f,h,.08f),TownColor.Frame);
            if(filled)B(new Vector3(0,0,.055f),new Vector3(w,.07f,.08f),TownColor.Frame);
        }
        private static EquipmentDraft Warehouse()
        {
            var d=new EquipmentDraft();d.Solid("Warehouse",new Vector3(0,2.6f,0),new Vector3(24,5.2f,10),TownColor.RoofDark);
            for(float x=-11.8f;x<12;x+=.52f)foreach(float z in new[]{-5.025f,5.025f})d.Box(new Vector3(x,2.6f,z),new Vector3(.035f,5.2f,.025f),TownColor.Frame);
            foreach(float x in new[]{-12.025f,12.025f})for(float z=-4.8f;z<5;z+=.52f)d.Box(new Vector3(x,2.6f,z),new Vector3(.025f,5.2f,.035f),TownColor.Frame);
            var slope=Quaternion.Euler(2.75f,0,0);d.Mesh.Box(new Vector3(0,5.46f,0),new Vector3(24.5f,.23f,10.5f),TownColor.RoofDark,slope);
            d.Box(new Vector3(0,5.45f,-4.97f),new Vector3(24,.6f,.10f),TownColor.RoofDark);
            foreach(float x in new[]{-11.98f,11.98f})
                if(x<0)d.Mesh.Triangle(new Vector3(x,5.2f,4.9f),new Vector3(x,5.73f,-5),new Vector3(x,5.2f,-5),TownColor.RoofDark);
                else d.Mesh.Triangle(new Vector3(x,5.2f,-5),new Vector3(x,5.73f,-5),new Vector3(x,5.2f,4.9f),TownColor.RoofDark);
            for(float x=-11.8f;x<12;x+=.52f)d.Mesh.Box(new Vector3(x,5.61f,0),new Vector3(.055f,.07f,10.35f),TownColor.Metal,slope);
            d.Box(new Vector3(0,2.19f,5.10f),new Vector3(5.62f,4.38f,.15f),TownColor.Cream);
            d.Box(new Vector3(0,2.13f,5.20f),new Vector3(5.3f,4.15f,.055f),TownColor.Metal);
            for(float y=.15f;y<4.1f;y+=.2f)d.Box(new Vector3(0,y,5.236f),new Vector3(5.26f,.028f,.022f),TownColor.Frame);
            d.Box(new Vector3(7.3f,1.5f,5.1f),new Vector3(1.55f,3.0f,.10f),TownColor.Frame);
            d.Box(new Vector3(7.3f,1.5f,5.17f),new Vector3(1.35f,2.8f,.04f),TownColor.Metal);
            d.Box(new Vector3(6.83f,1.45f,5.205f),new Vector3(.05f,.2f,.045f),TownColor.Frame);return d;
        }
        private static EquipmentDraft Canopy()
        {
            var d=new EquipmentDraft();d.Solid("Canopy roof",new Vector3(0,3.58f,0),new Vector3(6.25f,.20f,13.6f),TownColor.RoofDark);
            foreach(float x in new[]{2.88f})foreach(float z in new[]{-6.35f,0,6.35f})
                d.Solid("Canopy post",new Vector3(x,1.76f,z),new Vector3(.14f,3.52f,.14f),TownColor.Frame);
            foreach(float z in new[]{-6.35f,0,6.35f})d.Box(new Vector3(0,3.38f,z),new Vector3(5.9f,.18f,.13f),TownColor.Frame);
            foreach(float z in new[]{-3.7f,3.7f})d.Box(new Vector3(0,3.45f,z),new Vector3(1.25f,.04f,.35f),TownColor.Light);return d;
        }
        private static EquipmentDraft Fridge()
        {
            var d=new EquipmentDraft();d.Box(new Vector3(0,.06f,0),new Vector3(1.3f,.12f,.88f),TownColor.Frame);
            d.Box(new Vector3(0,1.22f,-.35f),new Vector3(1.25f,2.2f,.1f),TownColor.White);
            foreach(float x in new[]{-.58f,.58f})d.Box(new Vector3(x,1.22f,0),new Vector3(.1f,2.2f,.8f),TownColor.White);
            foreach(float y in new[]{.17f,2.34f})d.Box(new Vector3(0,y,0),new Vector3(1.25f,.1f,.8f),TownColor.White);
            foreach(float y in new[]{.52f,1.06f,1.60f,2.13f})
            {
                d.Box(new Vector3(0,y,0),new Vector3(1.06f,.035f,.63f),TownColor.Metal);
                for(int i=0;i<4;i++)d.Box(new Vector3((i-1.5f)*.23f,y+.16f,.02f),new Vector3(.16f,.29f,.21f),i%2==0?TownColor.White:TownColor.Mint);
            }
            foreach(float x in new[]{-.55f,.55f})d.Box(new Vector3(x,1.25f,.435f),new Vector3(.06f,2.15f,.06f),TownColor.Frame);
            d.Box(new Vector3(.46f,1.2f,.49f),new Vector3(.045f,.48f,.05f),TownColor.Metal);
            d.Collision("Display fridge",new Vector3(0,1.22f,0),new Vector3(1.3f,2.44f,.95f));return d;
        }
        private static EquipmentDraft Counter()
        {
            var d=new EquipmentDraft();d.Solid("Cold counter",new Vector3(0,.34f,0),new Vector3(2.8f,.68f,1.1f),TownColor.White);
            d.Box(new Vector3(0,.87f,-.49f),new Vector3(2.8f,.4f,.08f),TownColor.Metal);
            foreach(float x in new[]{-1.36f,1.36f})d.Box(new Vector3(x,.91f,0),new Vector3(.08f,.5f,1.08f),TownColor.Frame);
            d.Box(new Vector3(0,.76f,0),new Vector3(2.62f,.04f,.84f),TownColor.Metal);
            for(int i=0;i<6;i++)d.Box(new Vector3((i-2.5f)*.39f,.84f,0),new Vector3(.28f,.12f,.32f),TownColor.Box);
            foreach(float z in new[]{-.50f,.50f})d.Box(new Vector3(0,1.17f,z),new Vector3(2.8f,.05f,.06f),TownColor.Frame);return d;
        }
        private static EquipmentDraft Shelf()
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-.96f,.96f})foreach(float z in new[]{-.31f,.31f})d.Box(new Vector3(x,1.16f,z),new Vector3(.06f,2.32f,.06f),TownColor.Blue);
            foreach(float y in new[]{.16f,.85f,1.55f,2.25f})
            {
                d.Box(new Vector3(0,y,0),new Vector3(2.05f,.07f,.7f),TownColor.Metal);
                if(y>2)continue;for(int i=0;i<4;i++)Box(d,new Vector3((i-1.5f)*.47f,y+.20f,0),new Vector3(.38f,.34f,.52f));
            }
            d.Collision("Retail shelf",new Vector3(0,1.16f,0),new Vector3(2.05f,2.32f,.7f));return d;
        }
        private static EquipmentDraft Checkout()
        {
            var d=new EquipmentDraft();d.Solid("Checkout counter",new Vector3(0,.45f,0),new Vector3(2.5f,.9f,1),TownColor.White);
            d.Box(new Vector3(0,.94f,0),new Vector3(2.6f,.08f,1.08f),TownColor.Frame);
            d.Box(new Vector3(.55f,1.05f,0),new Vector3(.12f,.22f,.10f),TownColor.Metal);
            d.Box(new Vector3(.55f,1.3f,0),new Vector3(.55f,.35f,.08f),TownColor.Frame);
            d.Box(new Vector3(.55f,1.3f,.046f),new Vector3(.48f,.28f,.018f),TownColor.Glass);
            d.Box(new Vector3(.55f,1.0f,.29f),new Vector3(.52f,.04f,.23f),TownColor.Metal);
            d.Box(new Vector3(-.65f,1.02f,0),new Vector3(.26f,.10f,.24f),TownColor.Frame);return d;
        }
        private static void Box(EquipmentDraft d,Vector3 p,Vector3 size)
        {d.Box(p,size,TownColor.Box);d.Box(p+new Vector3(0,0,size.z/2+.006f),new Vector3(size.x*.3f,size.y*.2f,.008f),TownColor.White);}
        private static EquipmentDraft Dolly()
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-.46f,.46f})foreach(float z in new[]{-.30f,.30f})d.Mesh.Cylinder(new Vector3(x,.14f,z),.14f,.14f,.08f,TownColor.Rubber,8,Quaternion.Euler(0,0,90));
            d.Box(new Vector3(0,.31f,0),new Vector3(1.02f,.07f,.85f),TownColor.Frame);
            foreach(float x in new[]{-.45f,.45f})d.Box(new Vector3(x,.8f,-.34f),new Vector3(.055f,1.05f,.055f),TownColor.Metal);
            d.Box(new Vector3(0,1.33f,-.34f),new Vector3(.94f,.055f,.055f),TownColor.Metal);
            Box(d,new Vector3(0,.60f,.05f),new Vector3(.78f,.50f,.66f));Box(d,new Vector3(0,1.03f,.05f),new Vector3(.63f,.35f,.56f));
            d.Collision("Loaded dolly",new Vector3(0,.69f,0),new Vector3(1.1f,1.38f,.86f));return d;
        }
        private static EquipmentDraft Rack()
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-2.12f,2.12f})foreach(float z in new[]{-.52f,.52f})d.Box(new Vector3(x,1.6f,z),new Vector3(.10f,3.2f,.1f),TownColor.Blue);
            foreach(float y in new[]{.16f,1.25f,2.35f})
            {
                d.Box(new Vector3(0,y,0),new Vector3(4.4f,.1f,1.18f),TownColor.Metal);
                for(int i=0;i<4;i++)Box(d,new Vector3((i-1.5f)*1.0f,y+.43f,0),new Vector3(.84f,.76f,1.0f));
            }
            d.Collision("Pallet rack",new Vector3(0,1.6f,0),new Vector3(4.4f,3.2f,1.18f));return d;
        }
        private static void Pallet(EquipmentDraft d,float y)
        {
            foreach(float x in new[]{-.51f,0,.51f})d.Box(new Vector3(x,y+.06f,0),new Vector3(.16f,.12f,1.12f),TownColor.Wood);
            for(int i=0;i<5;i++)d.Box(new Vector3((i-2)*.27f,y+.135f,0),new Vector3(.23f,.03f,1.16f),TownColor.WoodLight);
        }
        private static EquipmentDraft Wrapped()
        {
            var d=new EquipmentDraft();Pallet(d,0);d.Box(new Vector3(0,.66f,0),new Vector3(1.18f,1.02f,1.0f),TownColor.White);d.Collision("Wrapped equipment",new Vector3(0,.585f,0),new Vector3(1.38f,1.17f,1.16f));
            foreach(float x in new[]{-.36f,.36f})d.Box(new Vector3(x,.66f,.51f),new Vector3(.025f,1.02f,.012f),TownColor.Metal);
            d.Box(new Vector3(0,.66f,.515f),new Vector3(1.18f,.023f,.012f),TownColor.Metal);return d;
        }
        private static EquipmentDraft Pallets()
        {var d=new EquipmentDraft();for(int i=0;i<4;i++)Pallet(d,i*.16f);d.Collision("Pallet stack",new Vector3(0,.32f,0),new Vector3(1.38f,.64f,1.16f));return d;}
        private static EquipmentDraft WallLamp()
        {var d=new EquipmentDraft();d.Box(new Vector3(0,.10f,-.10f),new Vector3(.18f,.30f,.15f),TownColor.Frame);d.Box(new Vector3(0,.22f,.12f),new Vector3(.48f,.12f,.45f),TownColor.Frame);d.Box(new Vector3(0,.145f,.12f),new Vector3(.38f,.035f,.36f),TownColor.Light);return d;}
        private static EquipmentDraft Bollard()
        {var d=new EquipmentDraft();d.Solid("Bollard",new Vector3(0,.51f,0),new Vector3(.20f,1.02f,.20f),TownColor.Yellow);foreach(float y in new[]{.23f,.70f})d.Box(new Vector3(0,y,0),new Vector3(.205f,.13f,.205f),TownColor.Frame);return d;}
        private static EquipmentDraft Planter()
        {
            var d=new EquipmentDraft();d.Solid("Planter",new Vector3(0,.23f,0),new Vector3(2.4f,.46f,.92f),TownColor.Stone);
            d.Box(new Vector3(0,.47f,0),new Vector3(2.22f,.03f,.76f),TownColor.Soil);
            for(int i=0;i<4;i++)d.Mesh.Ellipsoid(new Vector3((i-1.5f)*.52f,.83f,0),new Vector3(.37f,.39f,.34f),i%2==0?TownColor.Leaf:TownColor.LeafLight,7,3);return d;
        }
        private static EquipmentDraft Fence()
        {
            var d=new EquipmentDraft();d.Collision("Fence",new Vector3(0,.95f,0),new Vector3(3,1.9f,.06f));
            foreach(float x in new[]{-1.48f,1.48f})d.Box(new Vector3(x,.95f,0),new Vector3(.08f,1.9f,.08f),TownColor.Frame);
            for(float x=-1.3f;x<1.5f;x+=.20f)d.Box(new Vector3(x,1.03f,0),new Vector3(.025f,1.7f,.025f),TownColor.Frame);
            foreach(float y in new[]{.25f,1.53f})d.Box(new Vector3(0,y,0),new Vector3(3,.05f,.06f),TownColor.Frame);return d;
        }
        private static EquipmentDraft Pier()
        {var d=new EquipmentDraft();d.Solid("Brick pier",new Vector3(0,.93f,0),new Vector3(.46f,1.86f,.46f),TownColor.Brick);d.Box(new Vector3(0,1.95f,0),new Vector3(.61f,.18f,.61f),TownColor.Cream);return d;}
        private static EquipmentDraft Tree()
        {var d=new EquipmentDraft();TownNature.Tree(d.Mesh,Vector3.zero,2);d.Collision("Tree trunk",new Vector3(0,1.15f,0),new Vector3(.35f,2.3f,.35f));return d;}
        private static EquipmentDraft Lamp()
        {var d=new EquipmentDraft();d.Solid("Lamp foot",new Vector3(0,.1f,0),new Vector3(.32f,.20f,.32f),TownColor.Frame);d.Box(new Vector3(0,1.86f,0),new Vector3(.075f,3.72f,.075f),TownColor.Frame);d.Box(new Vector3(0,3.79f,0),new Vector3(.6f,.13f,.6f),TownColor.Frame);d.Box(new Vector3(0,3.71f,0),new Vector3(.45f,.03f,.45f),TownColor.Light);return d;}
        private static EquipmentDraft Car(TownColor color)
        {var d=new EquipmentDraft();var m=TownVehicles.Passenger(color).Build("Customer car");try{d.Mesh.Append(m,Matrix4x4.identity);d.Collision("Customer car",m.bounds.center,m.bounds.size);}finally{UnityEngine.Object.DestroyImmediate(m);}return d;}
    }
    internal sealed class EquipmentModel
    {
        public string Id{get;}public EquipmentDraft Draft{get;}
        public EquipmentModel(string id,Func<EquipmentDraft> factory){Id=id;Draft=factory();}
    }
    internal sealed class EquipmentDraft
    {
        public TownMeshBuilder Mesh{get;}=new();public List<EquipmentSolid> Solids{get;}=new();
        public void Box(Vector3 p,Vector3 s,TownColor c)=>Mesh.Box(p,s,c);
        public void Collision(string id,Vector3 p,Vector3 s)=>Solids.Add(new EquipmentSolid(id,p,s));
        public void Solid(string id,Vector3 p,Vector3 s,TownColor c){Box(p,s,c);Collision(id,p,s);}
    }
    internal readonly struct EquipmentSolid
    {
        public readonly string Id;public readonly Vector3 Center,Size;
        public EquipmentSolid(string id,Vector3 center,Vector3 size){Id=id;Center=center;Size=size;}
    }
}
