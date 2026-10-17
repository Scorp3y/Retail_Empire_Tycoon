using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>English modelling brief: cohesive rural farmstead with red timber barn, cream porch house,
    /// teal roofs, working packing shed, transparent greenhouses, grain silos, orchard and livestock pasture.
    /// Separate service logistics from the three-space farmers market. All new lettering is English.</summary>
    internal static class TownFarmUpdateModels
    {
        internal static IReadOnlyList<EquipmentModel> Create()
        {
            var list=new List<EquipmentModel>{new("FarmHouse",House),new("RedBarn",Barn),new("PackingHall",Packing),new("HayShelter",()=>Shelter(10,7,4)),new("SortingCanopy",()=>Shelter(25,6,4)),new("FarmMarket",Market),new("TractorGarage",Garage),new("GrainSilo",Silo),new("Greenhouse",Greenhouse),new("AnimalShelter",()=>Shelter(7,5,3)),new("ChickenCoop",Coop),new("Cow",Cow),new("Hen",()=>Chicken(false)),new("Rooster",()=>Chicken(true)),new("Tractor",Tractor),new("FarmTrailer",Trailer),new("SortingTable",Table),new("IrrigationStation",Irrigation),new("CompostBay",Compost),new("FuelTank",Tank),new("FarmTools",Tools),new("Wheelbarrow",Barrow),new("HayRound",Hay),new("HaySquare",()=>BoxProp(new Vector3(1.4f,.8f,.7f),TownColor.Sand)),new("GrainSacks",Sacks),new("FruitTree",FruitTree),new("FeedTrough",Trough),new("FarmCrates",Crates)};
            list.Add(new EquipmentModel("HandToolsAndSpares",HandTools));return list;
        }
        private static EquipmentDraft BoxProp(Vector3 size,TownColor color){var d=new EquipmentDraft();d.Solid("Body",Vector3.up*size.y/2,size,color);return d;}
        private static EquipmentDraft Hall(float w,float h,float depth,TownColor color)
        {
            var d=new EquipmentDraft();d.Solid("Back wall",new Vector3(0,h/2,-depth/2+.15f),new Vector3(w,h,.3f),color);
            foreach(float x in new[]{-w/2+.15f,w/2-.15f})d.Solid("Side wall",new Vector3(x,h/2,0),new Vector3(.3f,h,depth),color);
            d.Solid("Floor",new Vector3(0,-.09f,0),new Vector3(w,.2f,depth),TownColor.Pavement);
            d.Box(new Vector3(0,h,0),new Vector3(w+.7f,.18f,depth+.7f),TownColor.Mint);return d;
        }
        private static EquipmentDraft House()
        {
            var d=Hall(12,5,9,TownColor.Cream);d.Solid("Front wall",new Vector3(0,2.5f,4.35f),new Vector3(12,5,.3f),TownColor.Cream);
            d.Mesh.Gable(new Vector3(0,5,0),12.8f,9.8f,2.4f,TownColor.Mint,TownColor.Cream);
            foreach(float x in new[]{-4f,4}){Window(d,x,2.7f,4.54f,2,2.4f);d.Box(new Vector3(x,1.2f,4.75f),new Vector3(2.3f,.32f,.5f),TownColor.Wood);}
            d.Box(new Vector3(0,1.5f,4.55f),new Vector3(1.7f,3,.12f),TownColor.Blue);
            d.Solid("Porch",new Vector3(0,.13f,5.7f),new Vector3(5,.26f,2.5f),TownColor.Pavement);
            foreach(float x in new[]{-2.3f,2.3f})d.Solid("Porch post",new Vector3(x,1.8f,6.6f),new Vector3(.16f,3.6f,.16f),TownColor.White);
            d.Box(new Vector3(0,3.7f,5.7f),new Vector3(5.4f,.18f,3),TownColor.Mint);return d;
        }
        private static EquipmentDraft Barn()
        {
            var d=Hall(14,7,16,TownColor.Red);d.Solid("Barn front",new Vector3(0,3.5f,7.85f),new Vector3(14,7,.3f),TownColor.Red);
            d.Mesh.Gable(new Vector3(0,7,0),15,17,4,TownColor.Mint,TownColor.Red);
            for(float x=-6.5f;x<7;x+=.65f)d.Box(new Vector3(x,3.5f,8.02f),new Vector3(.04f,6.8f,.05f),TownColor.WoodLight);
            d.Box(new Vector3(0,2.7f,8.09f),new Vector3(6,5.4f,.12f),TownColor.BoxDark);
            foreach(float x in new[]{-3f,0,3})d.Box(new Vector3(x,2.7f,8.18f),new Vector3(.13f,5.6f,.1f),TownColor.White);
            foreach(float x in new[]{-1.5f,1.5f})foreach(float sign in new[]{-1f,1f})d.Mesh.Box(new Vector3(x,2.7f,8.2f),new Vector3(.13f,6,.1f),TownColor.White,Quaternion.Euler(0,0,sign*29));
            d.Box(new Vector3(0,5.5f,8.15f),new Vector3(6.3f,.16f,.18f),TownColor.White);return d;
        }
        private static EquipmentDraft Packing()
        {
            var d=Hall(32,5.4f,12,TownColor.Brick);
            foreach(float x in new[]{-11f,11})d.Solid("Front wall",new Vector3(x,2.7f,5.85f),new Vector3(10,5.4f,.3f),TownColor.Brick);
            d.Solid("Door header",new Vector3(0,4.95f,5.85f),new Vector3(12,.9f,.3f),TownColor.Brick);
            d.Box(new Vector3(0,2.2f,5.96f),new Vector3(6.5f,4.4f,.1f),TownColor.Mint);
            for(float y=.2f;y<4.3f;y+=.32f)d.Box(new Vector3(0,y,6.03f),new Vector3(6.5f,.025f,.025f),TownColor.Frame);
            d.Box(new Vector3(0,4.95f,6.1f),new Vector3(12,.75f,.18f),TownColor.GreenStripe);
            foreach(float x in new[]{-11f,11})Window(d,x,2.7f,6.02f,5,2);return d;
        }
        internal static EquipmentDraft Shelter(float w,float depth,float h)
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-w/2+.2f,w/2-.2f})foreach(float z in new[]{-depth/2+.2f,depth/2-.2f})d.Solid("Canopy post",new Vector3(x,h/2,z),new Vector3(.24f,h,.24f),TownColor.Wood);
            d.Box(new Vector3(0,h,0),new Vector3(w+.5f,.2f,depth+.5f),TownColor.Mint);return d;
        }
        private static EquipmentDraft Market()
        {
            var d=Shelter(13,5,3.5f);for(int i=0;i<13;i++)d.Box(new Vector3(i-6,3.63f,0),new Vector3(.98f,.06f,5.4f),i%2==0?TownColor.White:TownColor.Mint);
            foreach(float x in new[]{-4f,0,4}){d.Solid("Market counter",new Vector3(x,.55f,0),new Vector3(3.6f,1.1f,1.3f),TownColor.Wood);for(int k=0;k<3;k++)TownProps.Crate(d.Mesh,new Vector3(x-1+k,.59f,0),k%2==0);}
            d.Box(new Vector3(0,3.1f,2.6f),new Vector3(8,.65f,.14f),TownColor.GreenStripe);
            d.Box(new Vector3(4,1.2f,.2f),new Vector3(.7f,.12f,.55f),TownColor.Metal);return d;
        }
        private static EquipmentDraft Garage()
        {
            var d=Hall(14,5,13,TownColor.Cream);d.Mesh.Gable(new Vector3(0,5,0),14.7f,13.7f,2,TownColor.Mint,TownColor.Cream);return d;
        }
        private static EquipmentDraft Silo()
        {
            var d=new EquipmentDraft();d.Collision("Grain silo",new Vector3(0,6,0),new Vector3(5.2f,12,5.2f));
            d.Mesh.Cylinder(Vector3.up*.2f,2.8f,2.8f,.4f,TownColor.Stone,16);d.Mesh.Cylinder(Vector3.up*6,2.5f,2.5f,11.4f,TownColor.Cream,16);d.Mesh.Cylinder(Vector3.up*12.3f,2.6f,.2f,1.2f,TownColor.Metal,16);
            foreach(float y in new[]{1f,4f,7f,10f})d.Mesh.Cylinder(Vector3.up*y,2.53f,2.53f,.06f,TownColor.Metal,16);
            foreach(float x in new[]{-.35f,.35f})d.Box(new Vector3(x,6.4f,2.57f),new Vector3(.07f,12,.07f),TownColor.Frame);
            for(float y=.4f;y<12.5f;y+=.4f)d.Box(new Vector3(0,y,2.6f),new Vector3(.72f,.06f,.06f),TownColor.Frame);
            for(int i=0;i<12;i++){float a=i*Mathf.PI/6;var p=new Vector3(Mathf.Cos(a)*1.2f,13.5f,Mathf.Sin(a)*1.2f);d.Box(p,new Vector3(.06f,.8f,.06f),TownColor.Frame);}
            d.Mesh.Cylinder(new Vector3(0,13.9f,0),1.25f,1.25f,.05f,TownColor.Frame,12);return d;
        }
        private static EquipmentDraft Greenhouse()
        {
            var d=new EquipmentDraft();d.Solid("Greenhouse plinth",new Vector3(0,.12f,0),new Vector3(9,.24f,18),TownColor.Stone);
            foreach(float z in new[]{-9f,-6,-3,0,3,6,9})
            {
                foreach(float x in new[]{-4.4f,4.4f})d.Solid("Greenhouse frame",new Vector3(x,1.7f,z),new Vector3(.09f,3.4f,.09f),TownColor.White);
                foreach(float x in new[]{-2.2f,2.2f})d.Mesh.Box(new Vector3(x,4.13f,z),new Vector3(4.7f,.09f,.09f),TownColor.White,Quaternion.Euler(0,0,x<0?18:-18));
            }
            foreach(float x in new[]{-4.4f,0,4.4f})d.Box(new Vector3(x,x==0?4.85f:3.4f,0),new Vector3(.09f,.09f,18),TownColor.White);
            foreach(float x in new[]{-2.5f,2.5f}){d.Box(new Vector3(x,.37f,0),new Vector3(2.4f,.5f,15),TownColor.Soil);for(float z=-7;z<8;z+=1.25f)d.Mesh.Ellipsoid(new Vector3(x,.8f,z),new Vector3(.65f,.45f,.55f),TownColor.Leaf,6,3);}
            return d;
        }
        private static EquipmentDraft Coop(){var d=Hall(3,2,3,TownColor.Red);d.Mesh.Gable(new Vector3(0,2,0),3.5f,3.5f,1,TownColor.Mint,TownColor.Red);d.Box(new Vector3(0,.5f,1.55f),new Vector3(.8f,1,.05f),TownColor.BoxDark);return d;}
        private static EquipmentDraft Cow()
        {
            var d=new EquipmentDraft();d.Mesh.Ellipsoid(new Vector3(0,1.1f,0),new Vector3(.55f,.55f,1.1f),TownColor.White,8,4);
            d.Mesh.Ellipsoid(new Vector3(0,1.35f,1),new Vector3(.4f,.43f,.5f),TownColor.White,7,3);d.Box(new Vector3(0,1.1f,1.4f),new Vector3(.65f,.35f,.2f),TownColor.Pink);
            foreach(float x in new[]{-.35f,.35f})foreach(float z in new[]{-.65f,.65f})d.Box(new Vector3(x,.43f,z),new Vector3(.15f,.86f,.16f),TownColor.Frame);
            foreach(float x in new[]{-.49f,.49f})d.Mesh.Ellipsoid(new Vector3(x,1.2f,-.4f),new Vector3(.08f,.35f,.48f),TownColor.Frame,6,3);d.Collision("Decorative cow",new Vector3(0,.9f,.2f),new Vector3(1.1f,1.8f,2.8f));return d;
        }
        private static EquipmentDraft Chicken(bool rooster)
        {
            var d=new EquipmentDraft();d.Mesh.Ellipsoid(new Vector3(0,.4f,0),new Vector3(.23f,.25f,.32f),rooster?TownColor.BoxDark:TownColor.White,7,3);d.Mesh.Ellipsoid(new Vector3(0,.66f,.2f),Vector3.one*.15f,TownColor.White,6,3);d.Box(new Vector3(0,.81f,.2f),new Vector3(.08f,.13f,.2f),TownColor.Red);
            foreach(float x in new[]{-.1f,.1f})d.Box(new Vector3(x,.1f,0),new Vector3(.04f,.2f,.04f),TownColor.Yellow);return d;
        }
        private static EquipmentDraft Tractor()
        {
            var d=new EquipmentDraft();d.Box(new Vector3(0,.7f,0),new Vector3(1.7f,.4f,3.6f),TownColor.Frame);d.Box(new Vector3(0,1.3f,1),new Vector3(1.6f,1.2f,1.7f),TownColor.GreenStripe);d.Box(new Vector3(0,2.15f,-.5f),new Vector3(1.8f,1.8f,1.8f),TownColor.Glass);d.Box(new Vector3(0,3.1f,-.5f),new Vector3(2.1f,.18f,2),TownColor.GreenStripe);
            foreach(float z in new[]{-1.2f,1.2f})foreach(float x in new[]{-1f,1f}){float r=z<0?.8f:.5f;d.Mesh.Cylinder(new Vector3(x,r,z),r,r,.45f,TownColor.Rubber,12,Quaternion.Euler(0,0,90));d.Mesh.Cylinder(new Vector3(x*1.25f,r,z),r*.55f,r*.55f,.04f,TownColor.Yellow,12,Quaternion.Euler(0,0,90));}
            d.Box(new Vector3(.6f,2.2f,1.5f),new Vector3(.12f,1.4f,.12f),TownColor.Frame);d.Collision("Tractor",new Vector3(0,1.6f,0),new Vector3(2.5f,3.2f,4.1f));return d;
        }
        private static EquipmentDraft Trailer()
        {
            var d=new EquipmentDraft();d.Box(new Vector3(0,.9f,0),new Vector3(2.5f,.25f,4.3f),TownColor.Wood);foreach(float x in new[]{-1.2f,1.2f})d.Box(new Vector3(x,1.5f,0),new Vector3(.12f,1,4.3f),TownColor.GreenStripe);
            foreach(float z in new[]{-2.1f,2.1f})d.Box(new Vector3(0,1.5f,z),new Vector3(2.5f,1,.12f),TownColor.GreenStripe);foreach(float x in new[]{-1.25f,1.25f})d.Mesh.Cylinder(new Vector3(x,.5f,0),.5f,.5f,.3f,TownColor.Rubber,10,Quaternion.Euler(0,0,90));d.Collision("Trailer",new Vector3(0,1,0),new Vector3(2.8f,2,4.4f));return d;
        }
        private static EquipmentDraft Table(){var d=new EquipmentDraft();d.Box(new Vector3(0,1.05f,0),new Vector3(3,.18f,1.2f),TownColor.WoodLight);foreach(float x in new[]{-1.3f,1.3f})foreach(float z in new[]{-.4f,.4f})d.Solid("Table leg",new Vector3(x,.5f,z),new Vector3(.12f,1,.12f),TownColor.Frame);TownProps.Crate(d.Mesh,new Vector3(-.6f,1.15f,0),false);return d;}
        private static EquipmentDraft Irrigation(){var d=new EquipmentDraft();foreach(float x in new[]{-1f,1f})d.Mesh.Cylinder(new Vector3(x,.55f,0),.45f,.45f,1.1f,TownColor.Blue,12);d.Mesh.Cylinder(new Vector3(0,1,0),.4f,.4f,.2f,TownColor.Rubber,16,Quaternion.Euler(90,0,0));return d;}
        private static EquipmentDraft Compost(){var d=new EquipmentDraft();foreach(float x in new[]{-1.5f,0,1.5f})d.Solid("Compost divider",new Vector3(x,.6f,0),new Vector3(.12f,1.2f,2),TownColor.Wood);d.Box(new Vector3(0,.6f,-.9f),new Vector3(3,1.2f,.12f),TownColor.Wood);d.Mesh.Ellipsoid(new Vector3(0,.25f,0),new Vector3(1.35f,.3f,.8f),TownColor.Soil,7,3);return d;}
        private static EquipmentDraft Tank(){var d=new EquipmentDraft();d.Solid("Bunded base",new Vector3(0,.18f,0),new Vector3(3,.36f,2.5f),TownColor.Stone);d.Mesh.Cylinder(new Vector3(0,1.05f,0),.7f,.7f,2,TownColor.Metal,12,Quaternion.Euler(0,0,90));d.Box(new Vector3(1.2f,.8f,.8f),new Vector3(.5f,1.2f,.5f),TownColor.Red);return d;}
        private static EquipmentDraft Tools(){var d=new EquipmentDraft();d.Solid("Tool cabinet",new Vector3(-1,.85f,0),new Vector3(.9f,1.7f,.6f),TownColor.Blue);d.Box(new Vector3(.5f,1.05f,0),new Vector3(2,.14f,.7f),TownColor.WoodLight);foreach(float x in new[]{-.3f,1.3f})d.Box(new Vector3(x,.5f,0),new Vector3(.1f,1,.5f),TownColor.Frame);d.Box(new Vector3(.5f,1.8f,-.3f),new Vector3(2,1.3f,.08f),TownColor.Frame);for(int i=0;i<5;i++)d.Box(new Vector3(i*.3f-.1f,1.7f,-.22f),new Vector3(.06f,.55f,.04f),TownColor.Metal);return d;}
        private static EquipmentDraft Barrow(){var d=new EquipmentDraft();d.Box(new Vector3(0,.65f,0),new Vector3(.7f,.3f,1),TownColor.GreenStripe);d.Mesh.Cylinder(new Vector3(0,.25f,.65f),.25f,.25f,.15f,TownColor.Rubber,10,Quaternion.Euler(0,0,90));foreach(float x in new[]{-.3f,.3f})d.Box(new Vector3(x,.45f,-.65f),new Vector3(.06f,.8f,.06f),TownColor.Frame);return d;}
        private static EquipmentDraft Hay(){var d=new EquipmentDraft();d.Mesh.Cylinder(new Vector3(0,.8f,0),.8f,.8f,1.3f,TownColor.Sand,12,Quaternion.Euler(90,0,0));foreach(float z in new[]{-.4f,.4f})d.Mesh.Cylinder(new Vector3(0,.8f,z),.815f,.815f,.055f,TownColor.Yellow,12,Quaternion.Euler(90,0,0));return d;}
        private static EquipmentDraft Sacks(){var d=new EquipmentDraft();for(int i=0;i<4;i++)d.Mesh.Ellipsoid(new Vector3((i%2)*.65f,.4f+(i/2)*.5f,0),new Vector3(.4f,.45f,.5f),TownColor.Cream,7,4);return d;}
        private static EquipmentDraft FruitTree(){var d=new EquipmentDraft();TownNature.Tree(d.Mesh,Vector3.zero,0);for(int i=0;i<9;i++){float a=i*2.4f;d.Mesh.Ellipsoid(new Vector3(Mathf.Cos(a)*1.35f,2.6f+i%3*.5f,Mathf.Sin(a)*1.2f),Vector3.one*.12f,i%2==0?TownColor.Red:TownColor.Orange,6,3);}d.Collision("Fruit tree trunk",new Vector3(0,1.2f,0),new Vector3(.4f,2.4f,.4f));return d;}
        private static EquipmentDraft Trough(){var d=new EquipmentDraft();d.Solid("Trough",new Vector3(0,.3f,0),new Vector3(2,.6f,.8f),TownColor.Wood);d.Mesh.Ground(0,0,1.8f,.6f,.61f,TownColor.Glass);return d;}
        private static EquipmentDraft Crates(){var d=new EquipmentDraft();TownProps.Pallet(d.Mesh,Vector3.zero);for(int i=0;i<4;i++)TownProps.Crate(d.Mesh,new Vector3((i%2)*.6f-.3f,.2f+(i/2)*.45f,0),i%2==0);return d;}
        private static EquipmentDraft HandTools()
        {
            var d=new EquipmentDraft();
            // Tools are grouped against a rack, never scattered across the service route.
            for(int i=0;i<3;i++){float x=-1+i*.55f;d.Box(new Vector3(x,.9f,0),new Vector3(.06f,1.8f,.06f),TownColor.WoodLight);d.Box(new Vector3(x,.18f,.03f),new Vector3(.3f,.35f,.07f),TownColor.Metal);}
            foreach(float x in new[]{1.1f,1.7f})d.Box(new Vector3(x,1.35f,-.1f),new Vector3(.07f,2.7f,.08f),TownColor.Wood);
            for(float y=.2f;y<2.7f;y+=.35f)d.Box(new Vector3(1.4f,y,-.1f),new Vector3(.6f,.06f,.08f),TownColor.WoodLight);
            d.Mesh.Cylinder(new Vector3(-1.8f,.3f,0),.25f,.3f,.6f,TownColor.Metal,10);
            d.Mesh.Cylinder(new Vector3(-1.8f,.3f,.7f),.24f,.25f,.6f,TownColor.GreenStripe,10);d.Box(new Vector3(-1.8f,.4f,1.05f),new Vector3(.12f,.12f,.5f),TownColor.GreenStripe);
            d.Mesh.Cylinder(new Vector3(2.3f,.5f,0),.5f,.5f,.24f,TownColor.Rubber,12,Quaternion.Euler(90,0,0));d.Mesh.Cylinder(new Vector3(2.3f,.5f,.13f),.25f,.25f,.025f,TownColor.Metal,12,Quaternion.Euler(90,0,0));return d;
        }
        internal static void Window(EquipmentDraft d,float x,float y,float z,float w,float h){d.Box(new Vector3(x,y,z),new Vector3(w,h,.06f),TownColor.Glass);foreach(float s in new[]{-1f,1f}){d.Box(new Vector3(x+s*w/2,y,z+.04f),new Vector3(.1f,h+.1f,.1f),TownColor.White);d.Box(new Vector3(x,y+s*h/2,z+.04f),new Vector3(w,.1f,.1f),TownColor.White);}d.Box(new Vector3(x,y,z+.05f),new Vector3(.07f,h,.1f),TownColor.White);}
    }
}
