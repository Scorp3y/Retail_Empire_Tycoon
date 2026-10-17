using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>English art brief: a cohesive, richly dressed stylized farm and neighbourhood fuel station.
    /// Farm pickup is a timber-clad agricultural barn with a ribbed pitched teal roof, deep produce veranda,
    /// stone plinth, working shutters and roof drainage, not an orange urban warehouse. Fuel architecture uses
    /// cream masonry, layered teal/red/white canopy fascia, readable equipment and planted pedestrian edges.</summary>
    internal static class TownRuralDetailModels
    {
        internal static Color32[] Palette()
        {
            var p=(Color32[])TownPalette.Colors.Clone();
            p[(int)TownColor.Mint]=new Color32(58,132,116,255);p[(int)TownColor.Brick]=new Color32(169,121,79,255);
            p[(int)TownColor.Sand]=new Color32(178,166,137,255);p[(int)TownColor.Asphalt]=new Color32(65,72,75,255);
            p[(int)TownColor.Pavement]=new Color32(201,195,175,255);p[(int)TownColor.RoofDark]=new Color32(43,70,65,255);return p;
        }
        internal static IReadOnlyList<EquipmentModel> Create()=>new[]{
            new EquipmentModel("PackingBarn",Packing),new EquipmentModel("Workshop",Workshop),new EquipmentModel("FarmHouseDress",HouseDress),new EquipmentModel("BarnDress",BarnDress),
            new EquipmentModel("GrainSiloDetailed",Silo),new EquipmentModel("ProduceStand",Stand),new EquipmentModel("GreenhouseWorkstation",Potting),new EquipmentModel("HarvestPallet",Harvest),new EquipmentModel("ToolWall",Tools),new EquipmentModel("FarmTrailerDetailed",Trailer),
            new EquipmentModel("FuelShopDetailed",Shop),new EquipmentModel("FuelCanopyDetailed",Canopy),new EquipmentModel("FuelPumpDetailed",Pump),new EquipmentModel("AirWaterStation",Air),new EquipmentModel("FuelPylonDetailed",Pylon),
            new EquipmentModel("CoffeeMenuBoard",Menu),new EquipmentModel("CoffeeService",Coffee),new EquipmentModel("ServiceKit",Service),new EquipmentModel("RecyclingStation",Recycling),
            new EquipmentModel("FlowerCluster",()=>Flowers(0)),new EquipmentModel("FlowerClusterPink",()=>Flowers(1)),new EquipmentModel("MeadowClump",Meadow),new EquipmentModel("MixedShrub",Shrub),new EquipmentModel("WheelStop",WheelStop),
            new EquipmentModel("FieldWheatClump",Wheat),new EquipmentModel("FieldLeafClump",Lettuce),new EquipmentModel("FieldRootClump",Carrot),new EquipmentModel("MarketPriceBoard",PriceBoard),new EquipmentModel("MarketHarvestDisplay",MarketHarvest)};

        private static EquipmentDraft Packing()
        {
            var d=Shell(32,16,5.7f,TownColor.WoodLight);
            // Front opening is aligned with the retained native Farmers checkpoint at world x=560.
            foreach(float x in new[]{-11f,11})d.Solid("Pickup front wall",new Vector3(x,2.85f,7.85f),new Vector3(10,5.7f,.3f),TownColor.WoodLight);
            d.Solid("Pickup lintel",new Vector3(0,5.2f,7.85f),new Vector3(12,1,.3f),TownColor.WoodLight);
            Planks(d,32,16,5.7f);StoneBase(d,32,16);RibbedRoof(d,33.2f,17.3f,5.8f,3.3f);
            for(float x=-15.5f;x<16;x+=.6f){float h=3.3f*(1-Mathf.Abs(x)/16.6f);foreach(float z in new[]{-8.68f,8.68f})d.Box(new Vector3(x,5.8f+h/2,z),new Vector3(.035f,h,.04f),TownColor.Wood);}
            d.Box(new Vector3(0,7.25f,8.73f),new Vector3(2.5f,1.05f,.09f),TownColor.Cream);for(int i=0;i<6;i++)d.Box(new Vector3(0,6.84f+i*.16f,8.8f),new Vector3(2.2f,.07f,.08f),TownColor.Frame);
            foreach(float x in new[]{-15.7f,-6.2f,6.2f,15.7f})d.Box(new Vector3(x,2.8f,8.05f),new Vector3(.28f,5.6f,.3f),TownColor.Cream);
            foreach(float x in new[]{-11f,11})Window(d,new Vector3(x,3.6f,8.05f),6.5f,2.2f);
            d.Box(new Vector3(0,3.95f,8.06f),new Vector3(6.6f,1.1f,.14f),TownColor.Frame);
            for(float y=3.55f;y<4.45f;y+=.16f)d.Box(new Vector3(0,y,8.15f),new Vector3(6.5f,.04f,.06f),TownColor.Metal);
            // Attached veranda: front posts flank the vehicle opening, with lateral produce displays.
            foreach(float x in new[]{-15f,-5.2f,5.2f,15f})d.Solid("Veranda post",new Vector3(x,2.25f,13),new Vector3(.23f,4.5f,.23f),TownColor.Wood);
            var a=new Vector3(-16,5.05f,8.3f);var b=new Vector3(16,5.05f,8.3f);var c=new Vector3(16,4.45f,13.8f);var e=new Vector3(-16,4.45f,13.8f);
            d.Mesh.Quad(e,c,b,a,TownColor.Mint);d.Mesh.Quad(a,b,c,e,TownColor.RoofDark);
            d.Box(new Vector3(0,4.38f,13.8f),new Vector3(32,.22f,.18f),TownColor.WoodLight);
            for(float x=-15.5f;x<16;x+=.7f)Beam(d,new Vector3(x,5.07f,8.3f),new Vector3(x,4.47f,13.8f),.035f,TownColor.RoofDark);
            d.Box(new Vector3(0,5.2f,13.87f),new Vector3(14,1.05f,.18f),TownColor.GreenStripe);
            foreach(float x in new[]{-15.7f,15.7f}){Beam(d,new Vector3(x,5.65f,8.4f),new Vector3(x,5.65f,-8.4f),.15f,TownColor.Metal);Beam(d,new Vector3(x,5.65f,8.4f),new Vector3(x,.15f,8.4f),.10f,TownColor.Metal);}
            foreach(float x in new[]{-8f,8})d.Box(new Vector3(x,7.6f,-1),new Vector3(2.5f,.1f,3),TownColor.Glass);
            return d;
        }
        private static EquipmentDraft Workshop()
        {
            var d=Shell(15,14,4.7f,TownColor.Cream);StoneBase(d,15,14);RibbedRoof(d,16,15,4.8f,2.4f);
            foreach(float x in new[]{-7.2f,7.2f})d.Solid("Workshop door jamb",new Vector3(x,2.35f,7),new Vector3(.4f,4.7f,.4f),TownColor.Wood);
            d.Box(new Vector3(0,4.65f,7.15f),new Vector3(14.4f,.45f,.4f),TownColor.Wood);
            foreach(float x in new[]{-6.5f,6.5f})d.Box(new Vector3(x,2.2f,7.2f),new Vector3(1,4.4f,.15f),TownColor.Mint);
            foreach(float x in new[]{-4f,4})d.Box(new Vector3(x,4.15f,7.4f),new Vector3(1,.15f,.4f),TownColor.Light);
            foreach(float sign in new[]{-1f,1f})Beam(d,new Vector3(0,7.15f,7.56f),new Vector3(sign*6,4.95f,7.56f),.11f,TownColor.Wood);
            d.Box(new Vector3(-7.57f,2.8f,-2),new Vector3(.08f,1.5f,2.8f),TownColor.Glass);
            for(float z=-6;z<7;z+=.8f)foreach(float x in new[]{-7.55f,7.55f})d.Box(new Vector3(x,2.5f,z),new Vector3(.04f,4.1f,.035f),TownColor.WoodLight);
            return d;
        }
        private static EquipmentDraft Shell(float w,float depth,float h,TownColor color)
        {
            var d=new EquipmentDraft();d.Solid("Rear wall",new Vector3(0,h/2,-depth/2+.15f),new Vector3(w,h,.3f),color);
            foreach(float x in new[]{-w/2+.15f,w/2-.15f})d.Solid("Side wall",new Vector3(x,h/2,0),new Vector3(.3f,h,depth),color);
            // Flush foundations do not form thresholds in the driving or pedestrian path.
            d.Solid("Flush floor",new Vector3(0,-.09f,0),new Vector3(w,.2f,depth),TownColor.Pavement);return d;
        }
        private static EquipmentDraft HouseDress()
        {
            var d=new EquipmentDraft();RibbedRoof(d,12.9f,9.9f,5.03f,2.4f);
            d.Box(new Vector3(-3.7f,6.2f,-2.5f),new Vector3(.8f,2.5f,.7f),TownColor.Brick);d.Box(new Vector3(-3.7f,7.5f,-2.5f),new Vector3(1,.13f,.9f),TownColor.RoofDark);
            foreach(float x in new[]{-4f,4})for(int i=0;i<4;i++){var p=new Vector3(x-1+i*.65f,1.65f,4.9f);d.Mesh.Ellipsoid(p,new Vector3(.25f,.23f,.22f),TownColor.Leaf,7,3);d.Mesh.Ellipsoid(p+Vector3.up*.23f,Vector3.one*.10f,i%2==0?TownColor.Flowers:TownColor.Pink,6,3);}
            foreach(float y in new[]{.6f,3.1f,4.9f})d.Box(new Vector3(0,y,-4.55f),new Vector3(12,.05f,.05f),TownColor.WoodLight);
            foreach(float x in new[]{-5.8f,5.8f})d.Box(new Vector3(x,2.5f,4.58f),new Vector3(.18f,5,.1f),TownColor.White);return d;
        }
        private static EquipmentDraft BarnDress()
        {
            var d=new EquipmentDraft();RibbedRoof(d,15.1f,17.1f,7.02f,4,TownColor.Red);
            d.Box(new Vector3(0,8.1f,8.65f),new Vector3(2,1.8f,.1f),TownColor.White);d.Box(new Vector3(0,8.1f,8.73f),new Vector3(1.7f,1.5f,.08f),TownColor.Red);
            foreach(float a in new[]{-45f,45f})d.Mesh.Box(new Vector3(0,8.1f,8.8f),new Vector3(.10f,2,.08f),TownColor.White,Quaternion.Euler(0,0,a));
            foreach(float x in new[]{-7.06f,7.06f})for(float z=-7.8f;z<8;z+=.65f)d.Box(new Vector3(x,3.5f,z),new Vector3(.04f,6.8f,.045f),TownColor.WoodLight);return d;
        }
        private static EquipmentDraft Silo()
        {
            var d=new EquipmentDraft();d.Collision("Silo body",new Vector3(0,6.5f,0),new Vector3(5.2f,13,5.2f));
            d.Mesh.Cylinder(Vector3.up*.16f,2.8f,2.8f,.32f,TownColor.Stone,20);d.Mesh.Cylinder(Vector3.up*6.3f,2.5f,2.5f,12,TownColor.Cream,20);d.Mesh.Cylinder(Vector3.up*13.05f,2.7f,.15f,1.5f,TownColor.Metal,20);
            for(float y=.8f;y<12.4f;y+=.8f)d.Mesh.Cylinder(Vector3.up*y,2.515f,2.515f,.045f,TownColor.Metal,20);
            foreach(float x in new[]{-.36f,.36f})d.Box(new Vector3(x,6.7f,2.58f),new Vector3(.06f,12.8f,.06f),TownColor.Frame);
            for(float y=.4f;y<13.3f;y+=.37f)d.Box(new Vector3(0,y,2.61f),new Vector3(.75f,.055f,.07f),TownColor.Frame);
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Beam(d,new Vector3(Mathf.Cos(a)*1.1f,14,Mathf.Sin(a)*1.1f),new Vector3(Mathf.Cos(a)*1.1f,14.8f,Mathf.Sin(a)*1.1f),.05f,TownColor.Frame);}
            d.Mesh.Cylinder(new Vector3(0,14.8f,0),1.13f,1.13f,.05f,TownColor.Frame,16);d.Mesh.Cylinder(new Vector3(0,14,0),1.13f,1.13f,.06f,TownColor.Metal,16);
            foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-.6f,.6f})Beam(d,new Vector3(x,12.3f,z),new Vector3(x,14,z),.08f,TownColor.Frame);
            d.Box(new Vector3(0,.85f,2.6f),new Vector3(.8f,1.4f,.2f),TownColor.Frame);return d;
        }
        private static EquipmentDraft Stand()
        {
            var d=new EquipmentDraft();d.Solid("Produce counter",new Vector3(0,.5f,0),new Vector3(3,1,1.1f),TownColor.Wood);
            foreach(float x in new[]{-1f,0,1}){TownProps.Crate(d.Mesh,new Vector3(x,1.02f,0),x!=0);for(int i=0;i<5;i++)d.Mesh.Ellipsoid(new Vector3(x+(i%3-.8f)*.2f,1.5f,i/3*.23f-.2f),Vector3.one*.12f,x<0?TownColor.Orange:x==0?TownColor.Red:TownColor.LeafLight,6,3);}
            foreach(float x in new[]{-1.5f,1.5f})d.Box(new Vector3(x,1.75f,-.5f),new Vector3(.07f,1.5f,.07f),TownColor.Wood);return d;
        }
        private static EquipmentDraft MarketHarvest()
        {
            var d=new EquipmentDraft();
            // The retained market counters are 1.1 m tall; their old produce was buried inside them.
            foreach(float stall in new[]{-4f,0,4})foreach(float offset in new[]{-1f,0,1})
            {
                float x=stall+offset;TownProps.Crate(d.Mesh,new Vector3(x,1.12f,0),offset!=0);
                for(int i=0;i<8;i++)d.Mesh.Ellipsoid(new Vector3(x+(i%3-1)*.17f,1.57f+(i/6)*.12f,(i/3%2)*.25f-.125f),new Vector3(.11f,.10f,.11f),stall<0?TownColor.Red:stall>0?TownColor.Orange:TownColor.LeafLight,6,3);
            }
            return d;
        }
        private static EquipmentDraft Potting()
        {
            var d=new EquipmentDraft();d.Solid("Potting bench",new Vector3(0,.5f,0),new Vector3(2.8f,1,.85f),TownColor.WoodLight);
            for(int i=0;i<6;i++){float x=-1+i*.4f;d.Mesh.Cylinder(new Vector3(x,1.13f,0),.12f,.17f,.25f,TownColor.Brick,8);d.Mesh.Ellipsoid(new Vector3(x,1.4f,0),new Vector3(.18f,.25f,.15f),TownColor.Leaf,6,3);}
            d.Mesh.Cylinder(new Vector3(1.8f,.5f,0),.4f,.4f,1,TownColor.Blue,12);return d;
        }
        private static EquipmentDraft Harvest(){var d=new EquipmentDraft();TownProps.Pallet(d.Mesh,Vector3.zero);for(int i=0;i<6;i++)TownProps.Crate(d.Mesh,new Vector3((i%2)*.58f-.29f,.2f+(i/2)*.45f,0),i%3==0);return d;}
        private static EquipmentDraft Tools()
        {
            var d=new EquipmentDraft();d.Solid("Workbench cabinet",new Vector3(0,.5f,0),new Vector3(3.6f,1,.8f),TownColor.Blue);d.Box(new Vector3(0,1.06f,0),new Vector3(3.8f,.13f,1),TownColor.WoodLight);
            d.Box(new Vector3(0,1.8f,-.4f),new Vector3(3.6f,1.5f,.12f),TownColor.Frame);
            for(int i=0;i<8;i++){float x=-1.5f+i*.43f;d.Box(new Vector3(x,1.85f,-.30f),new Vector3(.06f,.65f,.05f),TownColor.Metal);d.Box(new Vector3(x,1.55f,-.30f),new Vector3(.20f,.08f,.05f),TownColor.Metal);}
            foreach(float x in new[]{-1.2f,0,1.2f}){d.Box(new Vector3(x,.55f,.42f),new Vector3(1.05f,.75f,.03f),TownColor.Frame);d.Box(new Vector3(x,.65f,.46f),new Vector3(.3f,.04f,.04f),TownColor.Metal);}return d;
        }
        private static EquipmentDraft Trailer()
        {
            var d=new EquipmentDraft();d.Solid("Trailer body",new Vector3(0,1.25f,0),new Vector3(2.6f,1,4.2f),TownColor.Wood);d.Box(new Vector3(0,.67f,0),new Vector3(2.5f,.22f,4.3f),TownColor.Frame);
            foreach(float x in new[]{-1.31f,1.31f}){for(float y=.9f;y<1.9f;y+=.25f)d.Box(new Vector3(x,y,0),new Vector3(.08f,.19f,4.3f),TownColor.GreenStripe);foreach(float z in new[]{-1.8f,0,1.8f})d.Box(new Vector3(x,1.3f,z),new Vector3(.10f,1.2f,.10f),TownColor.Metal);}
            foreach(float x in new[]{-1.35f,1.35f})foreach(float z in new[]{-.75f,.75f})Wheel(d,new Vector3(x,.52f,z),.52f,.27f);
            Beam(d,new Vector3(-1,.65f,2.1f),new Vector3(0,.65f,3.5f),.1f,TownColor.Frame);Beam(d,new Vector3(1,.65f,2.1f),new Vector3(0,.65f,3.5f),.1f,TownColor.Frame);
            foreach(float x in new[]{-.9f,.9f})d.Box(new Vector3(x,.9f,-2.16f),new Vector3(.28f,.15f,.06f),TownColor.Red);
            for(int i=0;i<5;i++)TownProps.Crate(d.Mesh,new Vector3((i%2)*.8f-.4f,1.8f,i/2*.8f-.8f),true);return d;
        }
        private static EquipmentDraft Shop()
        {
            var d=Shell(14,11,4.25f,TownColor.Cream);StoneBase(d,14,11);RibbedRoof(d,15.1f,12.1f,4.35f,1.35f);
            foreach(float x in new[]{-6.8f,-1.3f,1.3f,6.8f})d.Solid("Shop front pier",new Vector3(x,1.85f,5.4f),new Vector3(.3f,3.7f,.3f),TownColor.Cream);
            d.Solid("Shop front fascia",new Vector3(0,3.95f,5.4f),new Vector3(14,.6f,.3f),TownColor.Cream);
            d.Box(new Vector3(0,3.95f,5.61f),new Vector3(13,.7f,.17f),TownColor.GreenStripe);
            foreach(float x in new[]{-4f,4})Window(d,new Vector3(x,1.9f,5.55f),5.15f,3.1f,false);
            Window(d,new Vector3(0,1.8f,5.58f),2.4f,3.4f,false);
            foreach(float x in new[]{-.85f,.85f})d.Box(new Vector3(x,1.65f,5.7f),new Vector3(.05f,.45f,.06f),TownColor.Metal);
            foreach(float x in new[]{-6.5f,6.5f})d.Box(new Vector3(x,3.3f,5.9f),new Vector3(.35f,.15f,.5f),TownColor.Light);
            d.Box(new Vector3(0,3.4f,6.2f),new Vector3(4,.18f,1.5f),TownColor.Mint);
            d.Box(new Vector3(4,4.6f,-3),new Vector3(1.3f,.8f,1),TownColor.Metal);for(int i=0;i<6;i++)d.Box(new Vector3(3.5f+i*.18f,5.02f,-3),new Vector3(.05f,.02f,.8f),TownColor.Frame);
            return d;
        }
        private static EquipmentDraft Canopy()
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-5f,5f})foreach(float z in new[]{-6.4f,6.4f}){d.Solid("Canopy column",new Vector3(x,2.85f,z),new Vector3(.3f,5.7f,.3f),TownColor.Cream);d.Box(new Vector3(x,.22f,z),new Vector3(.6f,.44f,.6f),TownColor.Frame);}
            d.Box(new Vector3(0,5.75f,0),new Vector3(22,.2f,18),TownColor.White);d.Box(new Vector3(0,6.05f,0),new Vector3(22.3f,.4f,18.3f),TownColor.Mint);
            foreach(float z in new[]{-9.2f,9.2f}){d.Box(new Vector3(0,5.86f,z),new Vector3(22.4f,.20f,.12f),TownColor.Red);d.Box(new Vector3(0,6.3f,z),new Vector3(22.4f,.10f,.14f),TownColor.White);}
            foreach(float x in new[]{-11.2f,11.2f})d.Box(new Vector3(x,5.86f,0),new Vector3(.12f,.2f,18.4f),TownColor.Red);
            for(float x=-10;x<11;x+=.55f)d.Box(new Vector3(x,6.28f,0),new Vector3(.035f,.05f,18),TownColor.RoofDark);
            foreach(float x in new[]{-7f,0,7})foreach(float z in new[]{-4.5f,4.5f}){d.Box(new Vector3(x,5.62f,z),new Vector3(1.9f,.08f,.65f),TownColor.Frame);d.Box(new Vector3(x,5.57f,z),new Vector3(1.7f,.025f,.5f),TownColor.Light);}
            return d;
        }
        private static EquipmentDraft Pump()
        {
            var d=new EquipmentDraft();d.Solid("Pump lower body",new Vector3(0,.65f,0),new Vector3(.85f,1.3f,.65f),TownColor.Red);d.Solid("Pump display housing",new Vector3(0,1.65f,0),new Vector3(1,.75f,.7f),TownColor.White);d.Box(new Vector3(0,2.08f,0),new Vector3(1.05f,.12f,.76f),TownColor.Frame);
            foreach(float side in new[]{-1f,1f})
            {
                float z=side*.365f;d.Box(new Vector3(0,1.79f,z),new Vector3(.72f,.26f,.025f),TownColor.Frame);for(int i=0;i<5;i++)d.Box(new Vector3(-.26f+i*.13f,1.8f,z+side*.02f),new Vector3(.07f,.10f,.015f),TownColor.LeafLight);
                foreach(float x in new[]{-.28f,0,.28f})d.Box(new Vector3(x,1.45f,z),new Vector3(.12f,.12f,.03f),TownColor.Yellow);
            }
            foreach(float x in new[]{-.65f,.65f})
            {
                var points=new[]{new Vector3(x,1.85f,-.10f),new Vector3(x*1.4f,1.45f,-.10f),new Vector3(x*1.45f,.40f,-.10f),new Vector3(x*.8f,.30f,-.1f),new Vector3(x*.76f,1.30f,.2f)};
                for(int i=1;i<points.Length;i++)Beam(d,points[i-1],points[i],.07f,TownColor.Rubber);d.Box(new Vector3(x*.76f,1.45f,.2f),new Vector3(.11f,.32f,.14f),TownColor.Frame);
            }
            return d;
        }
        private static EquipmentDraft Air()
        {
            var d=new EquipmentDraft();d.Solid("Air compressor cabinet",new Vector3(-.65f,.85f,0),new Vector3(1,1.7f,.75f),TownColor.Cream);d.Solid("Water service cabinet",new Vector3(.65f,.85f,0),new Vector3(1,1.7f,.75f),TownColor.GreenStripe);
            foreach(float x in new[]{-1.6f,1.6f})d.Solid("Air station frame",new Vector3(x,1.9f,-.3f),new Vector3(.12f,3.8f,.12f),TownColor.Frame);
            d.Box(new Vector3(0,3.2f,-.3f),new Vector3(3.5f,.9f,.18f),TownColor.Mint);
            foreach(float x in new[]{-.65f,.65f}){d.Mesh.Cylinder(new Vector3(x,1.3f,.40f),.32f,.32f,.16f,TownColor.Rubber,16,Quaternion.Euler(90,0,0));Beam(d,new Vector3(x,1.3f,.5f),new Vector3(x,.25f,.8f),.045f,TownColor.Rubber);d.Box(new Vector3(x,1.75f,.4f),new Vector3(.4f,.25f,.03f),TownColor.Frame);}return d;
        }
        private static EquipmentDraft Pylon()
        {
            var d=new EquipmentDraft();d.Solid("Pylon foundation",new Vector3(0,.12f,0),new Vector3(3,.24f,1),TownColor.Stone);d.Solid("Pylon body",new Vector3(0,2.7f,0),new Vector3(2.5f,5.4f,.5f),TownColor.GreenStripe);
            d.Mesh.Cylinder(new Vector3(0,4.55f,.27f),.58f,.58f,.04f,TownColor.Mint,20,Quaternion.Euler(90,0,0));
            foreach(float y in new[]{1.5f,2.2f,2.9f}){d.Box(new Vector3(0,y,.27f),new Vector3(2.1f,.38f,.05f),TownColor.Frame);for(int i=0;i<4;i++)d.Box(new Vector3(-.65f+i*.43f,y,.31f),new Vector3(.19f,.19f,.02f),TownColor.Yellow);}return d;
        }
        private static EquipmentDraft Menu(){var d=new EquipmentDraft();foreach(float x in new[]{-.5f,.5f})d.Mesh.Box(new Vector3(x,.65f,0),new Vector3(.06f,1.3f,.07f),TownColor.Wood,Quaternion.Euler(12,0,0));d.Box(new Vector3(0,.9f,.08f),new Vector3(1.2f,1.25f,.12f),TownColor.Frame);foreach(float y in new[]{.6f,.8f,1f})d.Box(new Vector3(0,y,.15f),new Vector3(.85f,.035f,.01f),TownColor.Cream);return d;}
        private static EquipmentDraft Coffee(){var d=new EquipmentDraft();d.Solid("Coffee counter",new Vector3(0,.52f,0),new Vector3(3.2f,1.04f,.85f),TownColor.WoodLight);d.Box(new Vector3(0,1.1f,0),new Vector3(3.4f,.12f,1),TownColor.Frame);d.Box(new Vector3(-.5f,1.45f,0),new Vector3(1.4f,.6f,.65f),TownColor.Metal);foreach(float x in new[]{-.9f,-.5f,-.1f}){d.Box(new Vector3(x,1.5f,.35f),new Vector3(.12f,.15f,.06f),TownColor.Frame);d.Mesh.Cylinder(new Vector3(x,1.24f,.32f),.08f,.10f,.19f,TownColor.White,10);}for(int i=0;i<5;i++)d.Mesh.Cylinder(new Vector3(.6f+i*.17f,1.26f,0),.07f,.09f,.25f,TownColor.White,8);return d;}
        private static EquipmentDraft Service(){var d=new EquipmentDraft();d.Solid("Service bin",new Vector3(-.45f,.48f,0),new Vector3(.55f,.96f,.5f),TownColor.Frame);d.Box(new Vector3(.35f,.65f,0),new Vector3(.1f,1.3f,.1f),TownColor.Metal);d.Box(new Vector3(.35f,1.25f,0),new Vector3(.7f,.12f,.6f),TownColor.Frame);d.Box(new Vector3(.35f,1.34f,0),new Vector3(.6f,.08f,.5f),TownColor.Glass);Beam(d,new Vector3(.65f,1.4f,0),new Vector3(.85f,1.9f,0),.04f,TownColor.Frame);return d;}
        private static EquipmentDraft Recycling(){var d=new EquipmentDraft();foreach(float x in new[]{-1f,0,1}){d.Solid("Recycling bin",new Vector3(x,.5f,0),new Vector3(.8f,1,.7f),x<0?TownColor.Blue:x>0?TownColor.GreenStripe:TownColor.Frame);d.Box(new Vector3(x,1.05f,0),new Vector3(.85f,.12f,.75f),TownColor.Metal);}return d;}
        private static EquipmentDraft Flowers(int variant)
        {
            var d=new EquipmentDraft();var random=new System.Random(834+variant);
            for(int i=0;i<12;i++){float a=i*2.399f,r=.20f*Mathf.Sqrt(i);var p=new Vector3(Mathf.Cos(a)*r,.15f,Mathf.Sin(a)*r);d.Mesh.Ellipsoid(p,new Vector3(.32f,.2f,.30f),i%3==0?TownColor.LeafLight:TownColor.Leaf,6,3);var top=p+Vector3.up*(.15f+(float)random.NextDouble()*.25f);Beam(d,p,top,.025f,TownColor.LeafDark);d.Mesh.Ellipsoid(top,new Vector3(.13f,.07f,.13f),variant==0?(i%2==0?TownColor.Flowers:TownColor.White):TownColor.Pink,6,3);d.Mesh.Ellipsoid(top+Vector3.up*.055f,Vector3.one*.045f,TownColor.Yellow,5,2);}return d;
        }
        private static EquipmentDraft Meadow(){var d=new EquipmentDraft();for(int i=0;i<9;i++){float a=i*2.4f;var p=new Vector3(Mathf.Cos(a)*.35f,0,Mathf.Sin(a)*.35f);d.Mesh.Triangle(p+Vector3.left*.04f,p+Vector3.right*.04f,p+new Vector3(.12f,.3f+i%3*.15f,.1f),TownColor.Leaf);d.Mesh.Triangle(p+Vector3.right*.04f,p+Vector3.left*.04f,p+new Vector3(.12f,.3f+i%3*.15f,.1f),TownColor.Leaf);}return d;}
        private static EquipmentDraft Shrub(){var d=new EquipmentDraft();d.Mesh.Ellipsoid(new Vector3(0,.45f,0),new Vector3(1,.5f,.75f),TownColor.LeafDark,8,4);d.Mesh.Ellipsoid(new Vector3(.4f,.65f,0),new Vector3(.65f,.45f,.65f),TownColor.Leaf,7,4);return d;}
        private static EquipmentDraft WheelStop(){var d=new EquipmentDraft();d.Solid("Parking wheel stop",Vector3.up*.08f,new Vector3(1.7f,.16f,.25f),TownColor.Stone);foreach(float x in new[]{-.5f,.5f})d.Box(new Vector3(x,.17f,0),new Vector3(.25f,.02f,.22f),TownColor.Yellow);return d;}
        private static EquipmentDraft Wheat(){var d=new EquipmentDraft();for(int i=0;i<9;i++){float x=(i%3-1)*.22f,z=(i/3-1)*.22f,h=.65f+i%3*.12f;d.Box(new Vector3(x,h/2,z),new Vector3(.035f,h,.035f),TownColor.Yellow);d.Mesh.Ellipsoid(new Vector3(x,h,z),new Vector3(.10f,.18f,.08f),TownColor.Sand,5,2);}return d;}
        private static EquipmentDraft Lettuce(){var d=new EquipmentDraft();for(int i=0;i<4;i++){float a=i*Mathf.PI/2;d.Mesh.Ellipsoid(new Vector3(Mathf.Cos(a)*.18f,.22f,Mathf.Sin(a)*.18f),new Vector3(.22f,.2f,.27f),i%2==0?TownColor.Leaf:TownColor.LeafLight,6,3);}return d;}
        private static EquipmentDraft Carrot(){var d=new EquipmentDraft();d.Mesh.Cylinder(new Vector3(0,.08f,0),.08f,.14f,.16f,TownColor.Orange,7);for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;var p=new Vector3(Mathf.Cos(a)*.32f,.45f,Mathf.Sin(a)*.32f);d.Mesh.Triangle(Vector3.zero,new Vector3(.06f,.1f,0),p,TownColor.LeafLight);d.Mesh.Triangle(p,new Vector3(.06f,.1f,0),Vector3.zero,TownColor.LeafLight);}return d;}
        private static EquipmentDraft PriceBoard(){var d=new EquipmentDraft();d.Solid("Price board leg",Vector3.up*.5f,new Vector3(.1f,1,.1f),TownColor.Wood);d.Box(new Vector3(0,1.1f,0),new Vector3(1.3f,.65f,.12f),TownColor.Frame);return d;}

        internal static void Window(EquipmentDraft d,Vector3 p,float w,float h,bool filled=true)
        {
            if(filled)d.Box(p,new Vector3(w,h,.06f),TownColor.Glass);
            foreach(float x in new[]{-w/2,w/2})d.Box(p+new Vector3(x,0,.06f),new Vector3(.12f,h+.12f,.13f),TownColor.Cream);
            foreach(float y in new[]{-h/2,h/2})d.Box(p+new Vector3(0,y,.06f),new Vector3(w,.12f,.13f),TownColor.Cream);
            for(float x=-w/2+1.4f;x<w/2;x+=1.4f)d.Box(p+new Vector3(x,0,.08f),new Vector3(.08f,h,.12f),TownColor.Frame);
        }
        internal static void Beam(EquipmentDraft d,Vector3 a,Vector3 b,float width,TownColor color)=>d.Mesh.Box((a+b)/2,new Vector3(width,width,Vector3.Distance(a,b)),color,Quaternion.LookRotation(b-a));
        private static void Planks(EquipmentDraft d,float w,float depth,float h){for(float x=-w/2+.4f;x<w/2;x+=.55f){if(Mathf.Abs(x)>6.1f)d.Box(new Vector3(x,h/2,depth/2+.025f),new Vector3(.025f,h-.5f,.04f),TownColor.Wood);d.Box(new Vector3(x,h/2,-depth/2-.025f),new Vector3(.025f,h-.5f,.04f),TownColor.Wood);}foreach(float side in new[]{-1f,1f})for(float z=-depth/2+.4f;z<depth/2;z+=.6f)d.Box(new Vector3(side*(w/2+.025f),h/2,z),new Vector3(.04f,h-.5f,.025f),TownColor.Wood);}
        private static void StoneBase(EquipmentDraft d,float w,float depth){foreach(float x in new[]{-w/2,w/2})d.Box(new Vector3(x,.3f,0),new Vector3(.34f,.6f,depth),TownColor.Stone);d.Box(new Vector3(0,.3f,-depth/2),new Vector3(w,.6f,.34f),TownColor.Stone);}
        private static void RibbedRoof(EquipmentDraft d,float w,float depth,float eave,float rise,TownColor endColor=TownColor.WoodLight)
        {
            d.Mesh.Gable(new Vector3(0,eave,0),w,depth,rise,TownColor.Mint,endColor);
            foreach(float sign in new[]{-1f,1f}){d.Box(new Vector3(sign*w/2,eave-.05f,0),new Vector3(.18f,.23f,depth+.1f),TownColor.RoofDark);Beam(d,new Vector3(sign*w/2,eave,depth/2),new Vector3(0,eave+rise,depth/2),.13f,TownColor.Cream);}
            d.Box(new Vector3(0,eave+rise+.04f,0),new Vector3(.2f,.12f,depth+.2f),TownColor.RoofDark);
            for(float x=-w/2+.25f;x<w/2;x+=.65f){float y=eave+rise*(1-Mathf.Abs(x)/(w/2))+.025f;d.Box(new Vector3(x,y,0),new Vector3(.028f,.035f,depth),TownColor.RoofDark);}
        }
        private static void Wheel(EquipmentDraft d,Vector3 p,float r,float width){d.Mesh.Cylinder(p,r,r,width,TownColor.Rubber,16,Quaternion.Euler(0,0,90));d.Mesh.Cylinder(p+new Vector3(Mathf.Sign(p.x)*width*.51f,0,0),r*.55f,r*.55f,.03f,TownColor.Metal,12,Quaternion.Euler(0,0,90));for(int i=0;i<12;i++){float a=i*Mathf.PI/6;d.Mesh.Box(p+new Vector3(0,Mathf.Cos(a)*r,Mathf.Sin(a)*r),new Vector3(width+.02f,.08f,.12f),TownColor.Frame,Quaternion.Euler(a*Mathf.Rad2Deg,0,0));}}
    }
}
