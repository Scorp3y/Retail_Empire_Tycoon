using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>English modelling brief: compact cream-and-warm-brick coffee house with green hip roof, wood flower kiosk, four-table pergola terrace and planted neighbourhood pocket gardens; the existing bakery stays dominant.</summary>
    internal static class TownBakeryQuarterModels
    {
        internal static Color32[] Atlas()
        {
            var colors=(Color32[])TownPalette.Colors.Clone();
            colors[(int)TownColor.Brick]=new Color32(194,154,113,255);
            colors[(int)TownColor.Cream]=new Color32(231,220,191,255);
            colors[(int)TownColor.RoofDark]=new Color32(43,75,62,255);
            colors[(int)TownColor.Pavement]=new Color32(207,201,178,255);
            colors[(int)TownColor.Flowers]=new Color32(247,205,73,255);
            return colors;
        }
        internal static IReadOnlyList<EquipmentModel> Create()=>new[]
        {
            new EquipmentModel("CoffeeHouse",Coffee),new EquipmentModel("FlowerKiosk",Kiosk),
            new EquipmentModel("TerracePergola",Pergola),new EquipmentModel("PatioTableSet",()=>Patio(false)),new EquipmentModel("UmbrellaTableSet",()=>Patio(true)),
            new EquipmentModel("SquareBench",Bench),new EquipmentModel("SquareLantern",Lantern),new EquipmentModel("LitterBin",Bin),
            new EquipmentModel("FlowerBed",FlowerBed),new EquipmentModel("FlowerStand",FlowerStand),new EquipmentModel("FlowerPot",FlowerPot),
            new EquipmentModel("GardenIsland",Island),new EquipmentModel("SquareTree",Tree),new EquipmentModel("LowHedge",Hedge),
            new EquipmentModel("BicycleRack",Bikes),new EquipmentModel("CoffeeMenu",Menu)
        };

        private static EquipmentDraft Coffee()
        {
            var d=new EquipmentDraft();d.Collision("Coffee building",new Vector3(0,2,0),new Vector3(10,4,8));
            d.Box(new Vector3(0,.13f,0),new Vector3(10.15f,.26f,8.15f),TownColor.Stone);
            d.Box(new Vector3(0,2.1f,-3.88f),new Vector3(10,3.8f,.24f),TownColor.Brick);
            foreach(float x in new[]{-4.88f,4.88f})d.Box(new Vector3(x,2.1f,0),new Vector3(.24f,3.8f,8),TownColor.Cream);
            d.Box(new Vector3(0,3.38f,3.9f),new Vector3(10,1.25f,.25f),TownColor.Cream);
            d.Box(new Vector3(0,.4f,3.9f),new Vector3(10,.5f,.25f),TownColor.Brick);
            foreach(float x in new[]{-4.8f,-1.25f,1.25f,4.8f})d.Box(new Vector3(x,1.6f,3.9f),new Vector3(.4f,2.65f,.25f),TownColor.Cream);
            foreach(float y in new[]{.25f,3.95f})d.Box(new Vector3(0,y,0),new Vector3(10.2f,.15f,8.2f),TownColor.White);
            for(float y=.7f;y<3.8f;y+=.45f)d.Box(new Vector3(0,y,-4.012f),new Vector3(9.8f,.018f,.018f),TownColor.BoxDark);
            foreach(float x in new[]{-3.05f,3.05f})Window(d,new Vector3(x,1.63f,4.03f),3.0f,2.35f);
            Window(d,new Vector3(0,1.6f,4.035f),1.95f,2.7f);
            d.Box(new Vector3(0,1.6f,4.10f),new Vector3(.06f,2.7f,.07f),TownColor.Frame);
            d.Box(new Vector3(.2f,1.2f,4.15f),new Vector3(.04f,.25f,.07f),TownColor.Metal);
            d.Box(new Vector3(0,3.3f,4.08f),new Vector3(7,.70f,.2f),TownColor.RoofDark);
            d.Box(new Vector3(0,2.8f,4.65f),new Vector3(3.1f,.12f,1.3f),TownColor.RoofDark);
            HipRoof(d,10.8f,8.8f,4.06f,1.45f);
            d.Box(new Vector3(3.1f,4.8f,-1.5f),new Vector3(.55f,1.6f,.55f),TownColor.Brick);
            d.Box(new Vector3(3.1f,5.63f,-1.5f),new Vector3(.72f,.15f,.72f),TownColor.Stone);
            // Visible fitted interior, kept inside the shop's vehicle envelope.
            d.Box(new Vector3(0,.52f,0),new Vector3(9.6f,.08f,7.6f),TownColor.WoodLight);
            d.Box(new Vector3(0,1.07f,-1.0f),new Vector3(5.0f,1.0f,.85f),TownColor.Wood);
            d.Box(new Vector3(0,1.60f,-1.0f),new Vector3(5.15f,.12f,1.02f),TownColor.White);
            d.Box(new Vector3(-1.5f,1.98f,-1.15f),new Vector3(1.2f,.65f,.58f),TownColor.Frame);
            foreach(float x in new[]{-1.75f,-1.35f})d.Mesh.Cylinder(new Vector3(x,2.27f,-.82f),.075f,.075f,.10f,TownColor.Metal,8);
            foreach(float x in new[]{-.4f,.1f,.6f})Cup(d,new Vector3(x,1.67f,-.8f));
            foreach(float y in new[]{1.25f,2.1f,2.95f})
            {
                d.Box(new Vector3(0,y,-3.6f),new Vector3(6.4f,.08f,.50f),TownColor.WoodLight);
                for(int i=0;i<7;i++)d.Box(new Vector3((i-3)*.75f,y+.25f,-3.6f),new Vector3(.34f,.43f,.30f),i%2==0?TownColor.Box:TownColor.White);
            }
            return d;
        }
        private static void Window(EquipmentDraft d,Vector3 p,float width,float height)
        {
            foreach(float x in new[]{-width/2,width/2})d.Box(p+new Vector3(x,0,0),new Vector3(.08f,height+.1f,.09f),TownColor.Frame);
            foreach(float y in new[]{-height/2,height/2})d.Box(p+new Vector3(0,y,0),new Vector3(width,.08f,.09f),TownColor.Frame);
            d.Box(p,new Vector3(.045f,height,.06f),TownColor.Frame);
        }
        private static void HipRoof(EquipmentDraft d,float width,float depth,float eave,float rise)
        {
            float ridge=(width-depth)*.5f;var a=new Vector3(-width/2,eave,depth/2);var b=new Vector3(width/2,eave,depth/2);
            var c=new Vector3(width/2,eave,-depth/2);var e=new Vector3(-width/2,eave,-depth/2);
            var l=new Vector3(-ridge,eave+rise,0);var r=new Vector3(ridge,eave+rise,0);
            d.Mesh.Quad(a,b,r,l,TownColor.RoofDark);d.Mesh.Quad(c,e,l,r,TownColor.RoofDark);
            d.Mesh.Triangle(b,c,r,TownColor.RoofDark);d.Mesh.Triangle(e,a,l,TownColor.RoofDark);
            foreach(float z in new[]{-depth/2,depth/2})d.Box(new Vector3(0,eave,z),new Vector3(width,.13f,.13f),TownColor.White);
            foreach(float x in new[]{-width/2,width/2})d.Box(new Vector3(x,eave,0),new Vector3(.13f,.13f,depth),TownColor.White);
            for(int i=1;i<7;i++)
            {
                float t=i/7f;float y=eave+rise*t+.008f;float half=depth*.5f*(1-t);
                foreach(float z in new[]{-half,half})d.Box(new Vector3(0,y,z),new Vector3(Mathf.Lerp(width,ridge*2,t),.025f,.035f),TownColor.Frame);
            }
        }
        private static EquipmentDraft Kiosk()
        {
            var d=new EquipmentDraft();d.Solid("Kiosk building",new Vector3(0,1.4f,0),new Vector3(6,2.8f,4.8f),TownColor.Wood);
            for(float y=.22f;y<2.7f;y+=.28f)d.Box(new Vector3(0,y,2.405f),new Vector3(5.9f,.022f,.015f),TownColor.WoodLight);
            d.Box(new Vector3(0,1.52f,2.43f),new Vector3(4.7f,1.45f,.055f),TownColor.Glass);
            foreach(float x in new[]{-2.45f,2.45f})d.Box(new Vector3(x,1.52f,2.48f),new Vector3(.09f,1.5f,.12f),TownColor.Frame);
            d.Box(new Vector3(0,.84f,2.63f),new Vector3(5.2f,.16f,.62f),TownColor.WoodLight);
            d.Box(new Vector3(0,2.52f,2.57f),new Vector3(5.65f,.64f,.18f),TownColor.RoofDark);
            HipRoof(d,6.65f,5.45f,2.90f,.95f);
            foreach(float x in new[]{-1.65f,-.55f,.55f,1.65f})Flowers(d,new Vector3(x,.92f,2.69f),.55f);
            return d;
        }
        private static EquipmentDraft Pergola()
        {
            var d=new EquipmentDraft();
            foreach(float x in new[]{-3.5f,3.5f})foreach(float z in new[]{-4.5f,0,4.5f})d.Solid("Pergola post",new Vector3(x,1.75f,z),new Vector3(.18f,3.5f,.18f),TownColor.Wood);
            foreach(float x in new[]{-3.5f,3.5f})d.Box(new Vector3(x,3.45f,0),new Vector3(.22f,.25f,9.5f),TownColor.WoodLight);
            foreach(float z in new[]{-4.5f,4.5f})d.Box(new Vector3(0,3.45f,z),new Vector3(7.5f,.25f,.22f),TownColor.WoodLight);
            for(float z=-4.5f;z<=4.5f;z+=.45f)d.Box(new Vector3(0,3.65f,z),new Vector3(7.7f,.12f,.15f),TownColor.WoodLight);
            return d;
        }
        private static EquipmentDraft Patio(bool umbrella)
        {
            var d=new EquipmentDraft();d.Collision("Patio seating",new Vector3(0,umbrella?1.42f:.62f,0),new Vector3(2.6f,umbrella?2.84f:1.24f,1.8f));
            d.Mesh.Cylinder(new Vector3(0,.75f,0),.58f,.58f,.12f,TownColor.WoodLight,12);
            d.Box(new Vector3(0,.37f,0),new Vector3(.10f,.74f,.10f),TownColor.Frame);
            foreach(float x in new[]{-1f,1f})
            {
                foreach(float dx in new[]{-.18f,.18f})foreach(float z in new[]{-.18f,.18f})d.Box(new Vector3(x+dx,.23f,z),new Vector3(.055f,.46f,.055f),TownColor.Frame);
                d.Box(new Vector3(x,.49f,0),new Vector3(.48f,.10f,.48f),TownColor.WoodLight);
                d.Box(new Vector3(x,.86f,-.22f),new Vector3(.48f,.66f,.07f),TownColor.WoodLight);
            }
            Cup(d,new Vector3(-.18f,.82f,0));Cup(d,new Vector3(.18f,.82f,0));
            if(umbrella)
            {
                d.Box(new Vector3(0,1.25f,0),new Vector3(.04f,2.5f,.04f),TownColor.Metal);
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;
                    d.Mesh.Triangle(new Vector3(0,2.84f,0),new Vector3(Mathf.Sin(a)*1.3f,2.48f,Mathf.Cos(a)*1.3f),new Vector3(Mathf.Sin(b)*1.3f,2.48f,Mathf.Cos(b)*1.3f),i%2==0?TownColor.White:TownColor.Mint);
                }
            }
            return d;
        }
        private static void Cup(EquipmentDraft d,Vector3 bottom)
        {
            d.Mesh.Cylinder(bottom+Vector3.up*.07f,.06f,.075f,.14f,TownColor.White,8);
            d.Mesh.Cylinder(bottom+Vector3.up*.143f,.06f,.06f,.005f,TownColor.BoxDark,8);
        }
        private static EquipmentDraft Bench()
        {
            var d=new EquipmentDraft();d.Collision("Bench",new Vector3(0,.62f,0),new Vector3(2.3f,1.24f,.90f));
            foreach(float x in new[]{-.85f,.85f})d.Box(new Vector3(x,.23f,0),new Vector3(.09f,.46f,.72f),TownColor.Frame);
            for(int i=0;i<4;i++)d.Box(new Vector3(0,.49f,-.3f+i*.2f),new Vector3(2.3f,.10f,.16f),TownColor.WoodLight);
            foreach(float y in new[]{.78f,1.07f})d.Box(new Vector3(0,y,-.38f),new Vector3(2.3f,.18f,.08f),TownColor.WoodLight);
            foreach(float x in new[]{-1f,1f})d.Box(new Vector3(x,.76f,-.38f),new Vector3(.06f,.88f,.06f),TownColor.Frame);
            return d;
        }
        private static EquipmentDraft Lantern()
        {
            var d=new EquipmentDraft();d.Collision("Lantern",new Vector3(0,1.9f,0),new Vector3(.30f,3.8f,.30f));
            d.Box(new Vector3(0,.10f,0),new Vector3(.30f,.20f,.30f),TownColor.Frame);
            d.Box(new Vector3(0,1.7f,0),new Vector3(.075f,3.4f,.075f),TownColor.Frame);
            d.Box(new Vector3(0,3.49f,0),new Vector3(.42f,.50f,.42f),TownColor.Light);
            d.Box(new Vector3(0,3.79f,0),new Vector3(.60f,.12f,.60f),TownColor.RoofDark);return d;
        }
        private static EquipmentDraft Bin()
        {
            var d=new EquipmentDraft();d.Collision("Litter bin",new Vector3(0,.45f,0),new Vector3(.60f,.9f,.60f));
            d.Mesh.Cylinder(new Vector3(0,.41f,0),.27f,.27f,.82f,TownColor.Frame,10);
            d.Mesh.Cylinder(new Vector3(0,.85f,0),.30f,.30f,.08f,TownColor.Metal,10);return d;
        }
        private static EquipmentDraft Island()
        {
            var d=new EquipmentDraft();d.Solid("Landscape soil",new Vector3(0,.035f,0),new Vector3(7,.07f,6),TownColor.Grass);
            foreach(float z in new[]{-3f,3f})d.Box(new Vector3(0,.035f,z),new Vector3(7,.07f,.09f),TownColor.Stone);
            foreach(float x in new[]{-3.5f,3.5f})d.Box(new Vector3(x,.035f,0),new Vector3(.09f,.07f,6),TownColor.Stone);return d;
        }
        private static EquipmentDraft Tree()
        {
            var d=new EquipmentDraft();var tree=new TownMeshBuilder();TownNature.Tree(tree,Vector3.zero,2);var mesh=tree.Build("Quarter tree source");
            try{d.Mesh.Append(mesh,Matrix4x4.Scale(Vector3.one*1.35f));}finally{Object.DestroyImmediate(mesh);}
            d.Collision("Tree trunk",new Vector3(0,1.7f,0),new Vector3(.44f,3.4f,.44f));return d;
        }
        private static EquipmentDraft Hedge()
        {
            var d=new EquipmentDraft();var mesh=TownNature.Hedge().Build("Quarter hedge source");
            try{d.Mesh.Append(mesh,Matrix4x4.Scale(new Vector3(1.3f,.55f,1)));}finally{Object.DestroyImmediate(mesh);}return d;
        }
        private static EquipmentDraft FlowerBed()
        {
            var d=new EquipmentDraft();d.Solid("Flower bed",new Vector3(0,.10f,0),new Vector3(4,.20f,1.2f),TownColor.Stone);
            d.Box(new Vector3(0,.205f,0),new Vector3(3.8f,.03f,1.02f),TownColor.Soil);
            for(int i=0;i<9;i++)
            {
                var p=new Vector3((i-4)*.39f,.24f,(i%2==0?-.25f:.25f));
                d.Mesh.Cylinder(p+Vector3.up*.16f,.025f,.025f,.32f,TownColor.Leaf,5);
                d.Mesh.Ellipsoid(p+Vector3.up*.10f,new Vector3(.19f,.13f,.18f),TownColor.Leaf,6,3);
                TownNature.Flower(d.Mesh,p+Vector3.up*.34f,.12f,i%3==0?TownColor.Pink:TownColor.Flowers);
            }
            return d;
        }
        private static void Flowers(EquipmentDraft d,Vector3 bottom,float scale)
        {
            d.Mesh.Cylinder(bottom+Vector3.up*.25f*scale,.25f*scale,.35f*scale,.5f*scale,TownColor.Orange,8);
            d.Mesh.Cylinder(bottom+Vector3.up*.49f*scale,.35f*scale,.35f*scale,.06f*scale,TownColor.Orange,8);
            for(int i=0;i<5;i++)
            {
                float a=i*2.4f;var p=bottom+new Vector3(Mathf.Cos(a)*.15f*scale,.55f*scale,Mathf.Sin(a)*.15f*scale);
                d.Mesh.Cylinder(p+Vector3.up*.18f*scale,.018f*scale,.018f*scale,.36f*scale,TownColor.Leaf,5);
                TownNature.Flower(d.Mesh,p+Vector3.up*.4f*scale,.105f*scale,i%2==0?TownColor.Pink:TownColor.Flowers);
            }
        }
        private static EquipmentDraft FlowerPot(){var d=new EquipmentDraft();Flowers(d,Vector3.zero,1);d.Collision("Flower pot",new Vector3(0,.27f,0),new Vector3(.75f,.54f,.75f));return d;}
        private static EquipmentDraft FlowerStand()
        {
            var d=new EquipmentDraft();d.Collision("Flower stand",new Vector3(0,.95f,0),new Vector3(2.4f,1.9f,1));
            foreach(float x in new[]{-1.1f,1.1f})foreach(float z in new[]{-.4f,.4f})d.Box(new Vector3(x,.80f,z),new Vector3(.10f,1.6f,.10f),TownColor.Wood);
            foreach(float y in new[]{.25f,1.1f})
            {d.Box(new Vector3(0,y,0),new Vector3(2.4f,.10f,1),TownColor.WoodLight);foreach(float x in new[]{-.8f,0,.8f})Flowers(d,new Vector3(x,y+.05f,0),.70f);}
            return d;
        }
        private static EquipmentDraft Bikes()
        {
            var d=new EquipmentDraft();var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownDetails/Prefabs/Street/BicycleRack.prefab");
            if(source==null)throw new System.InvalidOperationException("Existing native bicycle model is missing.");
            foreach(var f in source.GetComponentsInChildren<MeshFilter>())d.Mesh.Append(f.sharedMesh,f.transform.localToWorldMatrix);
            d.Collision("Bicycle parking",new Vector3(0,.55f,0),new Vector3(2.2f,1.1f,2.15f));return d;
        }
        private static EquipmentDraft Menu()
        {
            var d=new EquipmentDraft();d.Collision("Menu board",new Vector3(0,.7f,0),new Vector3(.75f,1.4f,.65f));
            foreach(float z in new[]{-.23f,.23f})d.Box(new Vector3(0,.7f,z),new Vector3(.75f,1.3f,.07f),TownColor.WoodLight);
            d.Box(new Vector3(0,.85f,.28f),new Vector3(.58f,.95f,.02f),TownColor.Frame);
            foreach(float y in new[]{.65f,.86f,1.07f})d.Box(new Vector3(0,y,.30f),new Vector3(.40f,.025f,.02f),TownColor.White);return d;
        }
    }
}
