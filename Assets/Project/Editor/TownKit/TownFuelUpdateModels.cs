using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>English modelling brief: cream SHOP &amp; COFFEE with furnished glazed interior,
    /// teal canopy, two islands and exactly four red pumps; FUEL pylon and separate AIR &amp; WATER bay.</summary>
    internal static class TownFuelUpdateModels
    {
        internal static IReadOnlyList<EquipmentModel> Create()=>new[]{new EquipmentModel("FuelShop",Shop),new EquipmentModel("FuelCanopy",Canopy),new EquipmentModel("PumpIsland",Island),new EquipmentModel("FuelPump",Pump),new EquipmentModel("FuelPylon",Pylon),new EquipmentModel("AirWater",Air),new EquipmentModel("CoffeeCounter",Coffee),new EquipmentModel("CarGoods",Goods),new EquipmentModel("FireCabinet",Fire),new EquipmentModel("Drain",Drain),new EquipmentModel("BasketStack",Baskets),new EquipmentModel("RailSignal",Signal),new EquipmentModel("BarrierBase",BarrierBase),new EquipmentModel("BarrierArm",Arm),new EquipmentModel("ControlHut",Hut),new EquipmentModel("ControlCabinet",Cabinet),new EquipmentModel("CrossingSign",CrossingSign),new EquipmentModel("DistrictSign",District),new EquipmentModel("TreePlanter",Planter)};
        private static EquipmentDraft Shop()
        {
            var d=new EquipmentDraft();d.Solid("Shop floor",new Vector3(0,-.09f,0),new Vector3(14,.2f,10),TownColor.Pavement);
            d.Solid("Back wall",new Vector3(0,2,-4.85f),new Vector3(14,4,.3f),TownColor.Cream);
            foreach(float x in new[]{-6.85f,6.85f})d.Solid("Side wall",new Vector3(x,2,0),new Vector3(.3f,4,10),TownColor.Cream);
            foreach(float x in new[]{-6.8f,-1.3f,1.3f,6.8f})d.Solid("Front pier",new Vector3(x,1.7f,4.85f),new Vector3(.3f,3.4f,.3f),TownColor.Cream);
            d.Solid("Front header",new Vector3(0,3.7f,4.85f),new Vector3(14,.6f,.3f),TownColor.Cream);
            d.Box(new Vector3(0,4.1f,0),new Vector3(14.6f,.24f,10.6f),TownColor.Mint);d.Box(new Vector3(0,3.7f,5.05f),new Vector3(12,.65f,.15f),TownColor.GreenStripe);
            foreach(float x in new[]{-4f,4}){foreach(float y in new[]{.35f,3.3f})d.Box(new Vector3(x,y,5.01f),new Vector3(5.2f,.1f,.1f),TownColor.Frame);d.Box(new Vector3(x,1.8f,5.01f),new Vector3(.08f,2.9f,.12f),TownColor.Frame);}return d;
        }
        private static EquipmentDraft Canopy()
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-5f,5f})foreach(float z in new[]{-6.4f,6.4f})d.Solid("Fuel canopy post",new Vector3(x,2.9f,z),new Vector3(.35f,5.8f,.35f),TownColor.White);
            d.Box(new Vector3(0,5.95f,0),new Vector3(22,.3f,18),TownColor.Mint);
            foreach(float z in new[]{-9f,9f})d.Box(new Vector3(0,5.85f,z),new Vector3(22,.26f,.14f),TownColor.Red);
            foreach(float x in new[]{-7f,0,7})foreach(float z in new[]{-4f,4})d.Box(new Vector3(x,5.77f,z),new Vector3(1.8f,.04f,.6f),TownColor.Light);return d;
        }
        private static EquipmentDraft Island(){var d=new EquipmentDraft();d.Solid("Raised pump island",new Vector3(0,.12f,0),new Vector3(1.8f,.24f,10),TownColor.Pavement);return d;}
        private static EquipmentDraft Pump()
        {
            var d=new EquipmentDraft();d.Solid("Pump body",new Vector3(0,1.15f,0),new Vector3(.8f,2.3f,.65f),TownColor.Red);d.Box(new Vector3(0,1.85f,.34f),new Vector3(.72f,.65f,.04f),TownColor.White);
            d.Box(new Vector3(0,1.95f,.37f),new Vector3(.5f,.2f,.02f),TownColor.Frame);foreach(float x in new[]{-.18f,0,.18f})d.Box(new Vector3(x,1.65f,.38f),new Vector3(.09f,.1f,.02f),TownColor.Yellow);
            foreach(float x in new[]{-.55f,.55f}){d.Mesh.Cylinder(new Vector3(x,1.05f,0),.055f,.055f,1.7f,TownColor.Rubber,6);d.Box(new Vector3(x,1.7f,.2f),new Vector3(.12f,.35f,.12f),TownColor.Frame);d.Mesh.Cylinder(new Vector3(x*.7f,.25f,0),.2f,.2f,.08f,TownColor.Rubber,10,Quaternion.Euler(90,0,0));}return d;
        }
        private static EquipmentDraft Pylon(){var d=new EquipmentDraft();d.Solid("Pylon mast",Vector3.up*2.2f,new Vector3(.35f,4.4f,.35f),TownColor.Frame);d.Box(new Vector3(0,3.7f,0),new Vector3(2.5f,4.2f,.45f),TownColor.GreenStripe);foreach(float y in new[]{2.2f,2.8f,3.4f})d.Box(new Vector3(0,y,.24f),new Vector3(1.9f,.3f,.025f),TownColor.Yellow);return d;}
        private static EquipmentDraft Air(){var d=new EquipmentDraft();d.Solid("Air water cabinet",Vector3.up*.95f,new Vector3(1.5f,1.9f,.7f),TownColor.GreenStripe);d.Box(new Vector3(0,1.4f,.38f),new Vector3(.7f,.35f,.03f),TownColor.White);foreach(float x in new[]{-.45f,.45f})d.Mesh.Cylinder(new Vector3(x,.7f,.42f),.3f,.3f,.15f,TownColor.Rubber,12,Quaternion.Euler(90,0,0));return d;}
        private static EquipmentDraft Coffee(){var d=new EquipmentDraft();d.Solid("Coffee counter",Vector3.up*.5f,new Vector3(2.7f,1,1),TownColor.Wood);d.Box(new Vector3(-.5f,1.35f,0),new Vector3(1.1f,.7f,.65f),TownColor.Metal);d.Box(new Vector3(-.5f,1.4f,.35f),new Vector3(.8f,.35f,.07f),TownColor.Frame);for(int i=0;i<4;i++)d.Mesh.Cylinder(new Vector3(.3f+i*.23f,1.15f,0),.08f,.10f,.26f,TownColor.White,8);return d;}
        private static EquipmentDraft Goods(){var d=new EquipmentDraft();TownProps.Shelf(d.Mesh,Vector3.zero,2,1.8f,.6f,true);return d;}
        private static EquipmentDraft Fire(){var d=new EquipmentDraft();d.Solid("Fire cabinet",new Vector3(0,.8f,0),new Vector3(.8f,1.6f,.4f),TownColor.Red);d.Box(new Vector3(0,1,.22f),new Vector3(.5f,.7f,.03f),TownColor.White);return d;}
        private static EquipmentDraft Drain(){var d=new EquipmentDraft();d.Box(new Vector3(0,.012f,0),new Vector3(1.2f,.024f,.4f),TownColor.Frame);for(int i=0;i<9;i++)d.Box(new Vector3(i*.12f-.48f,.027f,0),new Vector3(.035f,.01f,.35f),TownColor.Metal);return d;}
        private static EquipmentDraft Baskets(){var d=new EquipmentDraft();for(int i=0;i<5;i++)d.Box(new Vector3(0,.12f+i*.08f,0),new Vector3(.5f,.15f,.35f),TownColor.GreenStripe);return d;}
        private static EquipmentDraft Signal()
        {
            var d=new EquipmentDraft();d.Solid("Signal mast",new Vector3(0,1.7f,0),new Vector3(.12f,3.4f,.12f),TownColor.White);
            foreach(float x in new[]{-.32f,.32f})d.Mesh.Cylinder(new Vector3(x,2.7f,.12f),.22f,.22f,.2f,TownColor.Frame,12,Quaternion.Euler(90,0,0));
            foreach(float a in new[]{-45f,45f})d.Mesh.Box(new Vector3(0,3.5f,0),new Vector3(1.25f,.13f,.1f),TownColor.White,Quaternion.Euler(0,0,a));return d;
        }
        private static EquipmentDraft BarrierBase(){var d=new EquipmentDraft();d.Solid("Barrier foundation",Vector3.up*.1f,new Vector3(.9f,.2f,.9f),TownColor.Stone);d.Solid("Barrier mechanism",Vector3.up*.7f,new Vector3(.6f,1.4f,.6f),TownColor.Red);return d;}
        private static EquipmentDraft Arm(){var d=new EquipmentDraft();d.Solid("Moving barrier",new Vector3(2.1f,0,0),new Vector3(4.2f,.16f,.14f),TownColor.White);for(float x=.3f;x<4.1f;x+=.6f)d.Box(new Vector3(x,0,.075f),new Vector3(.3f,.165f,.02f),TownColor.Red);return d;}
        private static EquipmentDraft Hut(){var d=new EquipmentDraft();d.Solid("Hut body",new Vector3(0,1.6f,0),new Vector3(4,3.2f,3.5f),TownColor.Cream);d.Box(new Vector3(0,3.3f,0),new Vector3(4.5f,.2f,4),TownColor.Mint);d.Box(new Vector3(-1,1.2f,1.8f),new Vector3(1,2.4f,.1f),TownColor.Blue);TownFarmUpdateModels.Window(d,1,1.8f,1.8f,1.8f,1);return d;}
        private static EquipmentDraft Cabinet()=>Box(new Vector3(1.2f,1.5f,.6f),TownColor.Metal);
        private static EquipmentDraft CrossingSign(){var d=new EquipmentDraft();d.Solid("Sign post",Vector3.up*1.3f,new Vector3(.07f,2.6f,.07f),TownColor.Frame);d.Box(new Vector3(0,2.2f,0),new Vector3(.85f,.85f,.08f),TownColor.White);d.Box(new Vector3(0,1.3f,.03f),new Vector3(1,.45f,.08f),TownColor.Red);return d;}
        private static EquipmentDraft District(){var d=new EquipmentDraft();foreach(float x in new[]{-1.8f,1.8f})d.Solid("Direction post",new Vector3(x,1.9f,0),new Vector3(.1f,3.8f,.1f),TownColor.Frame);d.Box(new Vector3(0,3.1f,0),new Vector3(4.2f,.9f,.15f),TownColor.GreenStripe);return d;}
        private static EquipmentDraft Planter(){var d=new EquipmentDraft();foreach(float x in new[]{-3.45f,3.45f})d.Solid("Planter rim",new Vector3(x,.2f,0),new Vector3(.16f,.4f,5),TownColor.Stone);foreach(float z in new[]{-2.45f,2.45f})d.Solid("Planter rim",new Vector3(0,.2f,z),new Vector3(7,.4f,.16f),TownColor.Stone);d.Mesh.Ground(0,0,6.8f,4.8f,.12f,TownColor.Soil);return d;}
        private static EquipmentDraft Box(Vector3 s,TownColor c){var d=new EquipmentDraft();d.Solid("Body",Vector3.up*s.y/2,s,c);return d;}
    }
}
