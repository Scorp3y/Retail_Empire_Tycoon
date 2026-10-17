using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>English modelling brief: compact cream-and-blue dairy factory, glazed production hall, connected cold store and dispatch annex, three stainless silos, roofed milk intake, tanker and fitted processing equipment. All new signage is English.</summary>
    internal static class TownDairyModels
    {
        internal static Color32[] Atlas(){var p=TownGroceryModels.Atlas();p[(int)TownColor.Brick]=new Color32(191,167,125,255);p[(int)TownColor.Metal]=new Color32(185,196,200,255);p[(int)TownColor.RoofDark]=new Color32(62,74,83,255);return p;}
        internal static IReadOnlyList<EquipmentModel> Create()
        {
            var models=new List<EquipmentModel>{new("DairyProduction",Production),new("DairyColdStore",ColdStore),new("DairyDispatch",Dispatch),new("DairyOffice",Office),new("MilkSilo",Silo),new("IntakeCanopy",()=>Canopy(12,12,4.8f)),new("DispatchCanopy",()=>Canopy(10,10,4.4f)),new("MilkTanker",Tanker),new("HoseStation",Hose),new("MilkPipe",Pipe)};
            var shared=new HashSet<string>{"CoolingUnit","GroceryPallet","TransportCrates","WasteBin","SafetyBollard","EquipmentPlanter","BlueCustomerCar","GreenCustomerCar","SiteLamp"};
            models.AddRange(TownGroceryModels.Create().Where(m=>shared.Contains(m.Id)));
            models.AddRange(TownBakeryQuarterModels.Create().Where(m=>new[]{"SquareBench","SquareTree","LowHedge","BicycleRack","FlowerBed"}.Contains(m.Id)));return models;
        }
        private static EquipmentDraft Production()
        {
            var d=new EquipmentDraft();d.Collision("Production building",new Vector3(0,3.25f,0),new Vector3(32,6.5f,22));
            d.Box(new Vector3(0,.12f,0),new Vector3(32,.24f,22),TownColor.Stone);d.Box(new Vector3(0,.27f,0),new Vector3(31.5f,.06f,21.5f),TownColor.White);
            d.Box(new Vector3(0,3.3f,-10.85f),new Vector3(32,6.4f,.3f),TownColor.Cream);
            foreach(float x in new[]{-15.85f,15.85f})d.Box(new Vector3(x,3.3f,0),new Vector3(.3f,6.4f,22),TownColor.Cream);
            d.Box(new Vector3(0,5,10.85f),new Vector3(32,3,.3f),TownColor.Cream);
            foreach(float x in new[]{-9f,9})d.Box(new Vector3(x,.7f,10.85f),new Vector3(14,1.3f,.3f),TownColor.Brick);
            d.Box(new Vector3(0,.17f,10.85f),new Vector3(3.85f,.34f,.3f),TownColor.Stone);
            foreach(float x in new[]{-15.75f,-2.3f,2.3f,15.75f})d.Box(new Vector3(x,2.9f,10.92f),new Vector3(.50f,3.2f,.35f),TownColor.Brick);
            foreach(float x in new[]{-9f,9f})Frame(d,new Vector3(x,2.75f,11.12f),12.3f,3.0f,false);
            Frame(d,new Vector3(0,2.25f,11.16f),3.85f,3.7f,false);
            d.Box(new Vector3(0,5.0f,11.2f),new Vector3(13,1.12f,.35f),TownColor.Blue);
            d.Box(new Vector3(0,6.55f,0),new Vector3(32.6f,.26f,22.6f),TownColor.RoofDark);
            d.Box(new Vector3(0,7.25f,0),new Vector3(22,1.30f,6),TownColor.Cream);
            foreach(float z in new[]{-3.02f,3.02f}){d.Box(new Vector3(0,7.30f,z),new Vector3(21,.72f,.06f),TownColor.Glass);for(float x=-10;x<=10;x+=2)d.Box(new Vector3(x,7.3f,z),new Vector3(.075f,.8f,.10f),TownColor.Blue);}
            d.Box(new Vector3(0,7.95f,0),new Vector3(22.6f,.18f,6.6f),TownColor.RoofDark);
            foreach(float x in new[]{-10f,10f})d.Box(new Vector3(x,6.99f,-7),new Vector3(.9f,.65f,.9f),TownColor.Metal);
            for(float x=-14;x<=14;x+=2)d.Box(new Vector3(x,6.71f,0),new Vector3(.028f,.025f,22.3f),TownColor.Frame);
            // Visible food-processing equipment belongs to the factory interior, not the truck apron.
            foreach(float x in new[]{-11f,-6f,6f,11f})
            {
                d.Mesh.Cylinder(new Vector3(x,1.8f,3),1.0f,1.0f,2.9f,TownColor.Metal,12);d.Mesh.Cylinder(new Vector3(x,3.36f,3),1.0f,.15f,.26f,TownColor.White,12);
                foreach(float leg in new[]{-.65f,.65f})d.Box(new Vector3(x+leg,.24f,3),new Vector3(.12f,.48f,.12f),TownColor.Frame);
                d.Box(new Vector3(x,1.5f,4.05f),new Vector3(.46f,.56f,.14f),TownColor.Blue);
                d.Mesh.Cylinder(new Vector3(x,3.6f,0),.09f,.09f,6,TownColor.Metal,8,Quaternion.Euler(90,0,0));
            }
            d.Box(new Vector3(0,1.1f,-4),new Vector3(22,.25f,2.5f),TownColor.Frame);
            foreach(float x in new[]{-10f,-5,0,5,10})foreach(float z in new[]{-4.9f,-3.1f})d.Box(new Vector3(x,.64f,z),new Vector3(.12f,.68f,.12f),TownColor.Frame);
            for(int i=0;i<16;i++)d.Box(new Vector3(-10+i*1.3f,1.35f,-4),new Vector3(.42f,.45f,.5f),i%2==0?TownColor.White:TownColor.Blue);
            return d;
        }
        private static EquipmentDraft ColdStore()
        {
            var d=Hall(32,16,8,"Cold storage building");d.Collision("Cold roof",new Vector3(0,8.12f,0),new Vector3(32.6f,.24f,16.6f));
            for(float x=-15;x<=15;x+=2)d.Box(new Vector3(x,4,-8.02f),new Vector3(.028f,7.6f,.035f),TownColor.Brick);
            d.Box(new Vector3(0,6.4f,8.1f),new Vector3(12,1,.25f),TownColor.Blue);
            foreach(float x in new[]{-12f,-5f,5f,12f})d.Box(new Vector3(x,8.2f,-5),new Vector3(1,.45f,1),TownColor.Metal);return d;
        }
        private static EquipmentDraft Dispatch()
        {
            var d=Hall(14,32,6.5f,"Dispatch building");
            d.Box(new Vector3(0,2.15f,16.06f),new Vector3(7.0f,4.3f,.13f),TownColor.Blue);
            foreach(float x in new[]{-3.6f,3.6f})d.Box(new Vector3(x,2.2f,16.17f),new Vector3(.15f,4.4f,.15f),TownColor.Frame);
            for(float y=.25f;y<4.3f;y+=.27f)d.Box(new Vector3(0,y,16.14f),new Vector3(7,.025f,.025f),TownColor.Frame);
            d.Box(new Vector3(0,5.15f,16.16f),new Vector3(9,.8f,.26f),TownColor.Blue);return d;
        }
        private static EquipmentDraft Office()
        {
            var d=Hall(8,8,3.8f,"Office building");Frame(d,new Vector3(1,1.8f,4.1f),3.5f,2.5f,true);Frame(d,new Vector3(-2,1.5f,4.12f),1.5f,2.8f,true);
            d.Box(new Vector3(-2,1.5f,4.19f),new Vector3(.06f,2.8f,.08f),TownColor.Frame);d.Box(new Vector3(0,3.30f,4.13f),new Vector3(6,.55f,.15f),TownColor.Blue);return d;
        }
        private static EquipmentDraft Hall(float width,float depth,float height,string id)
        {
            var d=new EquipmentDraft();d.Solid(id,new Vector3(0,height/2,0),new Vector3(width,height,depth),TownColor.Cream);
            d.Box(new Vector3(0,.2f,0),new Vector3(width,.4f,depth),TownColor.Stone);d.Box(new Vector3(0,height+.12f,0),new Vector3(width+.6f,.24f,depth+.6f),TownColor.RoofDark);
            foreach(float z in new[]{-depth/2,depth/2})d.Box(new Vector3(0,height+.34f,z),new Vector3(width+.6f,.24f,.18f),TownColor.Cream);
            foreach(float x in new[]{-width/2,width/2})d.Box(new Vector3(x,height+.34f,0),new Vector3(.18f,.24f,depth),TownColor.Cream);return d;
        }
        private static void Frame(EquipmentDraft d,Vector3 p,float width,float height,bool opaque)
        {
            if(opaque)d.Box(p,new Vector3(width,height,.06f),TownColor.Glass);
            foreach(float x in new[]{-width/2,width/2})d.Box(p+new Vector3(x,0,.02f),new Vector3(.1f,height+.1f,.12f),TownColor.Blue);
            foreach(float y in new[]{-height/2,height/2})d.Box(p+new Vector3(0,y,.02f),new Vector3(width,.1f,.12f),TownColor.Blue);
            for(float x=-width/2+1.8f;x<width/2;x+=1.8f)d.Box(p+new Vector3(x,0,.04f),new Vector3(.075f,height,.10f),TownColor.Blue);
        }
        private static EquipmentDraft Silo()
        {
            var d=new EquipmentDraft();d.Collision("Milk silo",new Vector3(0,5,0),new Vector3(3.9f,10,3.9f));
            d.Mesh.Cylinder(new Vector3(0,.13f,0),2.0f,2,.26f,TownColor.Stone,16);d.Mesh.Cylinder(new Vector3(0,4.8f,0),1.85f,1.85f,9.0f,TownColor.Metal,16);
            d.Mesh.Cylinder(new Vector3(0,9.43f,0),1.85f,.25f,.30f,TownColor.White,16);
            foreach(float y in new[]{.5f,3f,6f,9f})d.Mesh.Cylinder(new Vector3(0,y,0),1.88f,1.88f,.07f,TownColor.White,16);
            foreach(float x in new[]{-.32f,.32f})d.Box(new Vector3(x,4.9f,-1.92f),new Vector3(.06f,9.3f,.06f),TownColor.Frame);
            for(float y=.35f;y<9.6f;y+=.4f)d.Box(new Vector3(0,y,-1.95f),new Vector3(.68f,.045f,.055f),TownColor.Metal);
            d.Mesh.Cylinder(new Vector3(0,9.74f,0),.35f,.35f,.22f,TownColor.Blue,10);return d;
        }
        private static EquipmentDraft Canopy(float width,float depth,float height)
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-width/2,width/2})foreach(float z in new[]{-depth/2,depth/2})d.Solid("Canopy post",new Vector3(x,height/2,z),new Vector3(.20f,height,.20f),TownColor.Frame);
            d.Box(new Vector3(0,height+.05f,0),new Vector3(width+.7f,.22f,depth+.7f),TownColor.RoofDark);
            if(width==12)d.Box(new Vector3(0,height-.35f,depth/2+.03f),new Vector3(11,.85f,.18f),TownColor.Blue);
            return d;
        }
        private static EquipmentDraft Tanker()
        {
            var d=new EquipmentDraft();d.Collision("Parked milk tanker",new Vector3(0,1.65f,0),new Vector3(2.65f,3.3f,8.8f));
            d.Box(new Vector3(0,.72f,0),new Vector3(2.35f,.30f,8.4f),TownColor.Frame);d.Box(new Vector3(0,1.55f,3.0f),new Vector3(2.35f,2.0f,2.4f),TownColor.Blue);
            d.Box(new Vector3(0,2.05f,4.22f),new Vector3(2.05f,.85f,.045f),TownColor.Glass);
            foreach(float x in new[]{-1.19f,1.19f})d.Box(new Vector3(x,2.0f,3),new Vector3(.03f,.8f,1.8f),TownColor.Glass);
            d.Mesh.Cylinder(new Vector3(0,1.95f,-1),1.08f,1.08f,5.3f,TownColor.Metal,16,Quaternion.Euler(90,0,0));
            foreach(float z in new[]{-3f,-1.5f,3f})foreach(float x in new[]{-1.12f,1.12f}){d.Mesh.Cylinder(new Vector3(x,.48f,z),.48f,.48f,.30f,TownColor.Rubber,10,Quaternion.Euler(0,0,90));d.Mesh.Cylinder(new Vector3(x*1.14f,.48f,z),.24f,.24f,.04f,TownColor.Metal,10,Quaternion.Euler(0,0,90));}
            foreach(float z in new[]{-2.4f,-.2f})d.Box(new Vector3(0,3.05f,z),new Vector3(.35f,.20f,.35f),TownColor.Metal);
            foreach(float x in new[]{-.85f,.85f})d.Box(new Vector3(x,1,4.23f),new Vector3(.35f,.22f,.045f),TownColor.Light);return d;
        }
        private static EquipmentDraft Hose()
        {
            var d=new EquipmentDraft();d.Solid("Milk hose cabinet",new Vector3(0,.9f,0),new Vector3(1.4f,1.8f,.8f),TownColor.Metal);
            d.Box(new Vector3(0,1.25f,.43f),new Vector3(.6f,.55f,.06f),TownColor.Blue);
            d.Mesh.Cylinder(new Vector3(.80f,.85f,.25f),.35f,.35f,.16f,TownColor.Rubber,12,Quaternion.Euler(90,0,0));return d;
        }
        private static EquipmentDraft Pipe()
        {
            var d=new EquipmentDraft();d.Mesh.Cylinder(new Vector3(0,1.9f,0),.09f,.09f,16,TownColor.Metal,8,Quaternion.Euler(0,0,90));
            foreach(float x in new[]{-5f,5f})d.Solid("Pipe support",new Vector3(x,.92f,0),new Vector3(.12f,1.84f,.12f),TownColor.Frame);return d;
        }
    }
}
