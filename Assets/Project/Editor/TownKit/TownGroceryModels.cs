using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>English modelling brief: ivory grocery depot, blue fascia and shutters, enclosed warehouse, sorting canopy and packaged stock.</summary>
    internal static class TownGroceryModels
    {
        public static Color32[] Atlas()
        {
            var colors=TownEquipmentModels.Atlas();
            colors[(int)TownColor.Cream]=new Color32(224,222,204,255);
            colors[(int)TownColor.Blue]=new Color32(39,101,170,255);
            colors[(int)TownColor.RoofDark]=new Color32(104,114,122,255);
            colors[(int)TownColor.Brick]=new Color32(173,181,178,255);
            return colors;
        }
        public static IReadOnlyList<EquipmentModel> Create()
        {
            var models=new List<EquipmentModel>
            {
                new("GroceryShop",Shop),new("GroceryWarehouse",Warehouse),new("GroceryLink",Link),new("SortingCanopy",Canopy),
                new("CoolingUnit",Cooling),new("GroceryPallet",Cartons),new("DrinkPallet",Drinks),
                new("TransportCrates",Crates),new("CoveredPallet",Covered),new("GroceryRack",Rack),
                new("GroceryShelf",Shelf),new("WasteBin",Bin),new("WasteScreen",Screen)
            };
            var shared=new HashSet<string>{"CheckoutCounter","LoadedDolly","WallLamp","SafetyBollard","EquipmentPlanter","EquipmentFence","BrickPier","SiteTree","SiteLamp","BlueCustomerCar","GreenCustomerCar"};
            models.AddRange(TownEquipmentModels.Create().Where(m=>shared.Contains(m.Id)));
            models.AddRange(TownConstructionModels.Create().Where(m=>m.Id=="PalletJack"||m.Id=="HandTrolley"));
            return models;
        }
        private static EquipmentDraft Shop()
        {
            var d=new EquipmentDraft();d.Collision("Shop vehicle envelope",new Vector3(0,2.25f,0),new Vector3(22,4.5f,16));
            d.Box(new Vector3(0,.15f,0),new Vector3(22.2f,.3f,16.2f),TownColor.Cream);
            d.Solid("Entry step",new Vector3(0,.075f,8.5f),new Vector3(3,.15f,.75f),TownColor.Cream);
            d.Box(new Vector3(0,2.35f,-7.88f),new Vector3(22,4.1f,.24f),TownColor.Cream);
            d.Box(new Vector3(10.88f,2.35f,0),new Vector3(.24f,4.1f,16),TownColor.Cream);
            d.Box(new Vector3(-10.88f,3.7f,0),new Vector3(.24f,1.4f,16),TownColor.Cream);
            d.Box(new Vector3(0,3.7f,7.88f),new Vector3(22,1.4f,.24f),TownColor.Cream);
            foreach(float x in new[]{-10.8f,-1.6f,1.6f,10.8f})d.Box(new Vector3(x,1.68f,8),new Vector3(.5f,2.8f,.24f),TownColor.Cream);
            foreach(float z in new[]{-7.75f,-2.5f,2.5f,7.75f})d.Box(new Vector3(-11,1.68f,z),new Vector3(.24f,2.8f,.5f),TownColor.Cream);
            d.Box(new Vector3(0,.46f,8),new Vector3(22,.32f,.24f),TownColor.Cream);
            d.Box(new Vector3(-11,.46f,0),new Vector3(.24f,.32f,16),TownColor.Cream);
            foreach(float x in new[]{-6.2f,6.2f})Window(d,new Vector3(x,1.76f,8.06f),8.7f,2.35f);
            foreach(float z in new[]{-5f,0,5f})Window(d,new Vector3(-11.06f,1.76f,z),4.45f,2.35f,270);
            Window(d,new Vector3(0,1.68f,8.06f),2.6f,2.72f);
            d.Box(new Vector3(0,1.68f,8.14f),new Vector3(.065f,2.7f,.06f),TownColor.Frame);
            foreach(float x in new[]{-.16f,.16f})d.Box(new Vector3(x,1.45f,8.18f),new Vector3(.045f,.40f,.055f),TownColor.Metal);
            d.Box(new Vector3(0,3.42f,8.18f),new Vector3(22.3f,.70f,.30f),TownColor.Blue);
            d.Box(new Vector3(-11.06f,3.42f,0),new Vector3(.16f,.70f,16),TownColor.Blue);
            d.Box(new Vector3(0,3.05f,8.75f),new Vector3(3.65f,.16f,1.35f),TownColor.Blue);
            Roof(d,22,16,4.4f,false);
            d.Box(new Vector3(0,4.84f,8),new Vector3(21.76f,.4f,.24f),TownColor.Cream);
            // The six-metre roof junction is open only where the office link joins the rear wall.
            d.Box(new Vector3(-6.75f,4.84f,-8),new Vector3(8.5f,.4f,.24f),TownColor.Cream);
            d.Box(new Vector3(7.25f,4.84f,-8),new Vector3(7.5f,.4f,.24f),TownColor.Cream);
            return d;
        }
        private static void Window(EquipmentDraft d,Vector3 p,float width,float height,float yaw=0)
        {
            var q=Quaternion.Euler(0,yaw,0);
            foreach(float x in new[]{-width/2,width/2})d.Mesh.Box(p+q*new Vector3(x,0,.04f),new Vector3(.09f,height+.1f,.09f),TownColor.Frame,q);
            foreach(float y in new[]{-height/2,height/2})d.Mesh.Box(p+q*new Vector3(0,y,.04f),new Vector3(width+.1f,.09f,.09f),TownColor.Frame,q);
            d.Mesh.Box(p+q*new Vector3(0,0,.06f),new Vector3(.055f,height,.07f),TownColor.Frame,q);
        }
        private static void Roof(EquipmentDraft d,float width,float depth,float eave,bool endParapets=true)
        {
            d.Box(new Vector3(0,eave+.02f,0),new Vector3(width+.4f,.18f,depth+.4f),TownColor.White);
            d.Box(new Vector3(0,eave+.14f,0),new Vector3(width+.2f,.20f,depth+.2f),TownColor.RoofDark);
            foreach(float x in new[]{-width/2,width/2})d.Box(new Vector3(x,eave+.44f,0),new Vector3(.24f,.40f,depth+.24f),TownColor.Cream);
            if(endParapets)foreach(float z in new[]{-depth/2,depth/2})d.Box(new Vector3(0,eave+.44f,z),new Vector3(width-.24f,.40f,.24f),TownColor.Cream);
        }
        private static EquipmentDraft Warehouse()
        {
            var d=new EquipmentDraft();d.Solid("Warehouse",new Vector3(0,3.1f,0),new Vector3(30,6.2f,14),TownColor.Cream);Roof(d,30,14,6.2f);
            foreach(float x in new[]{-6.4f,6.4f})
            {
                d.Box(new Vector3(x,2.35f,7.08f),new Vector3(6.4f,4.7f,.16f),TownColor.White);
                d.Box(new Vector3(x,2.30f,7.20f),new Vector3(6.1f,4.6f,.06f),TownColor.Blue);
                for(float y=.16f;y<4.5f;y+=.23f)d.Box(new Vector3(x,y,7.245f),new Vector3(6.0f,.027f,.027f),TownColor.Frame);
            }
            d.Box(new Vector3(12.7f,1.5f,7.10f),new Vector3(1.7f,3,.14f),TownColor.White);
            d.Box(new Vector3(12.7f,1.5f,7.2f),new Vector3(1.48f,2.8f,.045f),TownColor.Blue);
            d.Box(new Vector3(12.2f,1.4f,7.24f),new Vector3(.045f,.22f,.045f),TownColor.Metal);
            d.Box(new Vector3(0,5.42f,7.075f),new Vector3(7.0f,.78f,.10f),TownColor.Blue);
            for(float y=1.1f;y<6;y+=1.2f)d.Box(new Vector3(0,y,7.025f),new Vector3(29.8f,.024f,.025f),TownColor.Metal);
            return d;
        }
        private static EquipmentDraft Link()
        {
            var d=new EquipmentDraft();d.Solid("Link building",new Vector3(0,2.2f,0),new Vector3(6,4.4f,10),TownColor.Cream);
            Roof(d,6,10,4.4f,false);
            d.Box(new Vector3(-2.05f,4.84f,-5),new Vector3(1.9f,.4f,.24f),TownColor.Cream);
            return d;
        }
        private static EquipmentDraft Canopy()
        {
            var d=new EquipmentDraft();d.Solid("Sorting roof",new Vector3(0,3.45f,0),new Vector3(5.8f,.20f,11.5f),TownColor.RoofDark);
            foreach(float x in new[]{-2.65f,2.65f})foreach(float z in new[]{-5.2f,0,5.2f})d.Solid("Sorting post",new Vector3(x,1.67f,z),new Vector3(.14f,3.34f,.14f),TownColor.Frame);
            foreach(float z in new[]{-5.2f,0,5.2f})d.Box(new Vector3(0,3.28f,z),new Vector3(5.4f,.16f,.14f),TownColor.Frame);
            return d;
        }
        private static EquipmentDraft Cooling()
        {
            var d=new EquipmentDraft();d.Solid("Cooling unit",new Vector3(0,.64f,0),new Vector3(1.7f,1.28f,1.2f),TownColor.White);
            d.Box(new Vector3(0,.65f,.63f),new Vector3(1.42f,1.03f,.05f),TownColor.Frame);
            for(float y=.23f;y<1.1f;y+=.12f)d.Box(new Vector3(0,y,.665f),new Vector3(1.3f,.03f,.02f),TownColor.Metal);
            d.Mesh.Cylinder(new Vector3(.4f,1.3f,0),.36f,.36f,.06f,TownColor.Frame,12);
            d.Box(new Vector3(-1.1f,.32f,0),new Vector3(.54f,.5f,.55f),TownColor.Metal);return d;
        }
        private static void Pallet(EquipmentDraft d)
        {
            foreach(float x in new[]{-.85f,0,.85f})d.Box(new Vector3(x,.09f,0),new Vector3(.18f,.18f,1.8f),TownColor.Wood);
            for(int i=0;i<6;i++)d.Box(new Vector3((i-2.5f)*.36f,.205f,0),new Vector3(.32f,.05f,1.8f),TownColor.WoodLight);
        }
        private static void Carton(EquipmentDraft d,Vector3 p,Vector3 size,bool white=false)
        {
            d.Box(p,size,white?TownColor.White:TownColor.Box);
            d.Box(p+new Vector3(0,0,size.z/2+.008f),new Vector3(size.x*.5f,size.y*.38f,.014f),TownColor.White);
            d.Mesh.Cylinder(p+new Vector3(0,0,size.z/2+.021f),size.y*.12f,size.y*.12f,.01f,TownColor.Orange,8,Quaternion.Euler(90,0,0));
        }
        private static EquipmentDraft Cartons()
        {
            var d=new EquipmentDraft();Pallet(d);
            for(int y=0;y<3;y++)for(int x=0;x<2;x++)for(int z=0;z<2;z++)Carton(d,new Vector3((x-.5f)*1.0f,.49f+y*.52f,(z-.5f)*.84f),new Vector3(.94f,.50f,.80f),y%2==0);
            d.Collision("Grocery pallet",new Vector3(0,.9f,0),new Vector3(2.2f,1.8f,1.8f));return d;
        }
        private static EquipmentDraft Drinks()
        {
            var d=new EquipmentDraft();Pallet(d);
            for(int y=0;y<2;y++)for(int x=0;x<5;x++)for(int z=0;z<4;z++)Bottle(d,new Vector3((x-2)*.36f,.24f+y*.65f,(z-1.5f)*.38f));
            d.Collision("Drink pallet",new Vector3(0,.74f,0),new Vector3(2.2f,1.48f,1.8f));return d;
        }
        private static void Bottle(EquipmentDraft d,Vector3 bottom)
        {
            d.Mesh.Cylinder(bottom+Vector3.up*.22f,.12f,.10f,.44f,TownColor.Glass,8);
            d.Mesh.Cylinder(bottom+Vector3.up*.22f,.124f,.124f,.14f,TownColor.White,8);
            d.Mesh.Cylinder(bottom+Vector3.up*.47f,.055f,.055f,.08f,TownColor.Blue,8);
        }
        private static EquipmentDraft Crates()
        {
            var d=new EquipmentDraft();Pallet(d);
            for(int y=0;y<3;y++)foreach(float x in new[]{-.5f,.5f})
            {
                var p=new Vector3(x,.45f+y*.46f,0);var c=y%2==0?TownColor.GreenStripe:TownColor.Blue;
                d.Box(p,new Vector3(.92f,.08f,1.5f),c);
                foreach(float dx in new[]{-.44f,.44f})d.Box(p+new Vector3(dx,.18f,0),new Vector3(.06f,.4f,1.5f),c);
                foreach(float z in new[]{-.72f,.72f})d.Box(p+new Vector3(0,.18f,z),new Vector3(.92f,.4f,.06f),c);
                for(int i=0;i<4;i++)d.Box(p+new Vector3((i-1.5f)*.2f,.2f,.756f),new Vector3(.11f,.20f,.008f),TownColor.Frame);
            }
            d.Collision("Transport crates",new Vector3(0,.88f,0),new Vector3(2.2f,1.76f,1.8f));return d;
        }
        private static EquipmentDraft Covered()
        {
            var d=new EquipmentDraft();Pallet(d);d.Box(new Vector3(0,1.01f,0),new Vector3(2.06f,1.56f,1.66f),TownColor.White);
            foreach(float x in new[]{-.60f,.60f})d.Box(new Vector3(x,1.01f,.843f),new Vector3(.035f,1.56f,.02f),TownColor.Metal);
            d.Collision("Covered pallet",new Vector3(0,.9f,0),new Vector3(2.2f,1.8f,1.8f));return d;
        }
        private static EquipmentDraft Rack()
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-1.55f,1.55f})foreach(float z in new[]{-.65f,.65f})d.Box(new Vector3(x,1.4f,z),new Vector3(.10f,2.8f,.10f),TownColor.Blue);
            foreach(float y in new[]{.2f,1.1f,2.0f})
            {
                d.Box(new Vector3(0,y,0),new Vector3(3.2f,.1f,1.4f),TownColor.Metal);
                for(int i=0;i<3;i++)Carton(d,new Vector3((i-1)*.97f,y+.35f,0),new Vector3(.88f,.60f,1.2f));
            }
            d.Collision("Grocery rack",new Vector3(0,1.4f,0),new Vector3(3.2f,2.8f,1.4f));return d;
        }
        private static EquipmentDraft Shelf()
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-1.45f,1.45f})d.Box(new Vector3(x,1.1f,0),new Vector3(.10f,2.2f,.75f),TownColor.White);
            foreach(float y in new[]{.15f,.78f,1.42f})
            {
                d.Box(new Vector3(0,y,0),new Vector3(3,.075f,.8f),TownColor.WoodLight);
                for(int i=0;i<6;i++)
                {
                    var p=new Vector3((i-2.5f)*.44f,y+.25f,0);
                    if(y<.7f)Carton(d,p,new Vector3(.36f,.40f,.6f),true);
                    else if(y<1.0f)Bottle(d,p-Vector3.up*.21f);
                    else d.Mesh.Cylinder(p,.14f,.14f,.40f,i%2==0?TownColor.Red:TownColor.Orange,8);
                }
            }
            d.Collision("Grocery shelf",new Vector3(0,1.1f,0),new Vector3(3,2.2f,.8f));return d;
        }
        private static EquipmentDraft Bin()
        {
            var d=new EquipmentDraft();d.Solid("Waste bin",new Vector3(0,.65f,0),new Vector3(1.5f,1.1f,1.1f),TownColor.GreenStripe);
            d.Box(new Vector3(0,1.25f,0),new Vector3(1.6f,.15f,1.2f),TownColor.Frame);
            foreach(float x in new[]{-.57f,.57f})foreach(float z in new[]{-.37f,.37f})d.Mesh.Cylinder(new Vector3(x,.12f,z),.12f,.12f,.1f,TownColor.Rubber,8,Quaternion.Euler(0,0,90));return d;
        }
        private static EquipmentDraft Screen()
        {
            var d=new EquipmentDraft();d.Solid("Waste screen",new Vector3(0,.95f,0),new Vector3(3,1.9f,.12f),TownColor.RoofDark);
            foreach(float x in new[]{-1.45f,1.45f})d.Box(new Vector3(x,1.0f,0),new Vector3(.12f,2,.18f),TownColor.Frame);return d;
        }
    }
}
