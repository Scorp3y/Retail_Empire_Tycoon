using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>English modelling brief: low orange building-supplies shop, dark gabled warehouse, timber canopy and categorized pallet stock.</summary>
    internal static class TownConstructionModels
    {
        public static Color32[] Atlas()
        {
            var palette = TownEquipmentModels.Atlas();
            palette[(int)TownColor.Orange] = new Color32(203, 106, 43, 255);
            palette[(int)TownColor.RoofDark] = new Color32(53, 63, 73, 255);
            palette[(int)TownColor.Mint] = new Color32(151, 164, 118, 255);
            return palette;
        }

        public static IReadOnlyList<EquipmentModel> Create()
        {
            var models = new List<EquipmentModel>
            {
                new("SupplyShop", Shop), new("SupplyWarehouse", Warehouse), new("TimberCanopy", Canopy),
                new("BrickPallet", () => Blocks(false)), new("BlockPallet", () => Blocks(true)),
                new("CementPallet", Cement), new("LumberStack", Lumber), new("SheetRack", Sheets),
                new("PipeCradle", Pipes), new("PalletJack", Jack), new("HandTrolley", Trolley),
                new("PaintRack", Paint), new("ToolWall", Tools)
            };
            // Reuse existing metre-scale prop recipes, not the installed equipment building or its materials.
            var shared = new HashSet<string>{"CheckoutCounter", "WallLamp", "SafetyBollard", "EquipmentPlanter", "EquipmentFence", "BrickPier", "SiteTree", "SiteLamp", "BlueCustomerCar", "GreenCustomerCar"};
            models.AddRange(TownEquipmentModels.Create().Where(model => shared.Contains(model.Id)));
            return models;
        }

        private static EquipmentDraft Shop()
        {
            var d = new EquipmentDraft();
            d.Collision("Shop vehicle envelope", new Vector3(0, 3.45f, 0), new Vector3(24, 6.9f, 16));
            d.Box(new Vector3(0, .15f, 0), new Vector3(24.2f, .3f, 16.2f), TownColor.Cream);
            d.Solid("Door step",new Vector3(0,.075f,8.5f),new Vector3(2.85f,.15f,.7f),TownColor.Cream);
            d.Box(new Vector3(0, 2.2f, -7.88f), new Vector3(24, 3.9f, .24f), TownColor.Orange);
            d.Box(new Vector3(11.88f, 2.2f, 0), new Vector3(.24f, 3.9f, 16), TownColor.Orange);
            d.Box(new Vector3(-11.88f, 3.57f, 0), new Vector3(.24f, 1.16f, 16), TownColor.Orange);
            d.Box(new Vector3(0, 3.57f, 7.88f), new Vector3(24, 1.16f, .24f), TownColor.Orange);
            d.Box(new Vector3(0, .42f, 7.98f), new Vector3(24, .24f, .24f), TownColor.Cream);
            d.Box(new Vector3(-11.98f, .42f, 0), new Vector3(.24f, .24f, 16), TownColor.Cream);
            foreach(float x in new[]{-11.75f, -1.55f, 1.55f, 11.75f})
                d.Box(new Vector3(x, 1.7f, 8), new Vector3(.6f, 2.8f, .24f), TownColor.Cream);
            foreach(float z in new[]{-7.75f, -2.5f, 2.5f, 7.75f})
                d.Box(new Vector3(-12, 1.7f, z), new Vector3(.24f, 2.8f, .5f), TownColor.Cream);
            foreach(float x in new[]{-6.65f, 6.65f}) Window(d, new Vector3(x, 1.72f, 8.07f), 9.6f, 2.5f);
            Window(d, new Vector3(0, 1.64f, 8.07f), 2.55f, 2.66f);
            foreach(float z in new[]{-5f, 0, 5f}) Window(d, new Vector3(-12.07f, 1.72f, z), 4.45f, 2.5f, 270);
            d.Box(new Vector3(0, 1.64f, 8.16f), new Vector3(.06f, 2.66f, .07f), TownColor.Frame);
            foreach(float x in new[]{-.16f, .16f}) d.Box(new Vector3(x, 1.4f, 8.22f), new Vector3(.04f, .35f, .05f), TownColor.Metal);
            d.Box(new Vector3(0, 3.36f, 8.18f), new Vector3(24.25f, .72f, .3f), TownColor.Orange);
            GabledRoof(d, 24.8f, 16.8f, 4.15f, 2.85f, TownColor.Wood);
            Window(d, new Vector3(0, 4.95f, 8.425f), 2.9f, 1.35f);
            d.Box(new Vector3(0, 4.95f, 8.425f), new Vector3(2.9f, 1.35f, .05f), TownColor.Glass);
            d.Box(new Vector3(0, 4.95f, 8.49f), new Vector3(.06f, 1.35f, .05f), TownColor.Frame);
            return d;
        }

        private static void Window(EquipmentDraft d, Vector3 p, float w, float h, float yaw=0)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            void Bar(Vector3 offset, Vector3 size) => d.Mesh.Box(p+q*offset, size, TownColor.Cream, q);
            foreach(float x in new[]{-w/2, w/2}) Bar(new Vector3(x,0,.04f),new Vector3(.09f,h+.09f,.09f));
            foreach(float y in new[]{-h/2,h/2}) Bar(new Vector3(0,y,.04f),new Vector3(w+.09f,.09f,.09f));
            d.Mesh.Box(p+q*new Vector3(0,0,.06f),new Vector3(.06f,h,.07f),TownColor.Frame,q);
        }

        private static void GabledRoof(EquipmentDraft d, float width, float depth, float eave, float rise, TownColor end)
        {
            d.Mesh.Gable(new Vector3(0,eave,0),width,depth,rise,TownColor.RoofDark,end);
            foreach(float z in new[]{-depth/2,depth/2})
                foreach(int side in new[]{-1,1})
                    d.Mesh.Box(new Vector3(side*width/4,eave+rise/2,z),new Vector3(Mathf.Sqrt(width*width/4+rise*rise),.10f,.10f),TownColor.Cream,Quaternion.Euler(0,0,-side*Mathf.Atan2(rise,width/2)*Mathf.Rad2Deg));
            for(float x=-width/2+.5f;x<width/2;x+=.85f)
                d.Box(new Vector3(x,eave+rise*(1-Mathf.Abs(x)/(width/2))+.045f,0),new Vector3(.035f,.075f,depth-.06f),TownColor.Frame);
        }

        private static EquipmentDraft Warehouse()
        {
            var d = new EquipmentDraft();
            d.Solid("Warehouse",new Vector3(0,2.8f,0),new Vector3(22,5.6f,10),TownColor.RoofDark);
            for(float x=-10.7f;x<11;x+=.58f)foreach(float z in new[]{-5.03f,5.03f})d.Box(new Vector3(x,2.8f,z),new Vector3(.035f,5.6f,.03f),TownColor.Frame);
            foreach(float x in new[]{-11.03f,11.03f})for(float z=-4.7f;z<5;z+=.58f)d.Box(new Vector3(x,2.8f,z),new Vector3(.03f,5.6f,.035f),TownColor.Frame);
            GabledRoof(d,22.7f,10.7f,5.6f,2.0f,TownColor.RoofDark);
            d.Box(new Vector3(0,2.1f,5.09f),new Vector3(5.9f,4.2f,.16f),TownColor.Cream);
            d.Box(new Vector3(0,2.04f,5.20f),new Vector3(5.58f,4.08f,.05f),TownColor.Metal);
            for(float y=.16f;y<4;y+=.2f)d.Box(new Vector3(0,y,5.24f),new Vector3(5.5f,.03f,.03f),TownColor.Frame);
            d.Box(new Vector3(-7.7f,1.5f,5.1f),new Vector3(1.6f,3,.12f),TownColor.Cream);
            d.Box(new Vector3(-7.7f,1.5f,5.18f),new Vector3(1.4f,2.8f,.04f),TownColor.Frame);
            d.Box(new Vector3(-8.17f,1.4f,5.22f),new Vector3(.04f,.22f,.04f),TownColor.Metal);
            return d;
        }

        private static EquipmentDraft Canopy()
        {
            var d = new EquipmentDraft();
            d.Solid("Timber roof",new Vector3(0,3.48f,0),new Vector3(9.4f,.22f,12.4f),TownColor.Wood);
            foreach(float x in new[]{-4.15f,4.15f})foreach(float z in new[]{-5.65f,0,5.65f})d.Solid("Timber post",new Vector3(x,1.68f,z),new Vector3(.18f,3.36f,.18f),TownColor.Wood);
            foreach(float z in new[]{-5.65f,0,5.65f})d.Box(new Vector3(0,3.32f,z),new Vector3(8.6f,.22f,.18f),TownColor.WoodLight);
            for(float z=-5.95f;z<6;z+=.48f)d.Box(new Vector3(0,3.62f,z),new Vector3(9.3f,.07f,.23f),TownColor.WoodLight);
            return d;
        }

        private static void Pallet(EquipmentDraft d,float width=2.2f,float depth=1.8f)
        {
            foreach(float x in new[]{-.4f,0,.4f})d.Box(new Vector3(x*width,.09f,0),new Vector3(.16f,.18f,depth),TownColor.Wood);
            for(int i=0;i<6;i++)d.Box(new Vector3((i-2.5f)*width/6,.205f,0),new Vector3(width/6-.03f,.05f,depth),TownColor.WoodLight);
        }

        private static EquipmentDraft Blocks(bool white)
        {
            var d = new EquipmentDraft();Pallet(d);
            int rows=white?4:5,columns=white?3:5;float h=white?.35f:.22f,w=2.02f/columns,depth=.81f;
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)for(int z=0;z<2;z++)
                d.Box(new Vector3((x-(columns-1)/2f)*w,.25f+(y+.5f)*h,(z-.5f)*.84f),new Vector3(w-.025f,h-.015f,depth),white?TownColor.White:TownColor.Brick);
            float height=.25f+rows*h;
            foreach(float x in new[]{-.58f,.58f})d.Box(new Vector3(x,height/2+.12f,.84f),new Vector3(.035f,height-.23f,.025f),TownColor.Frame);
            d.Collision("Material pallet",new Vector3(0,height/2,0),new Vector3(2.2f,height,1.8f));return d;
        }

        private static EquipmentDraft Cement()
        {
            var d = new EquipmentDraft();Pallet(d);
            for(int y=0;y<4;y++)for(int x=0;x<3;x++)for(int z=0;z<2;z++)
            {
                var p=new Vector3((x-1)*.65f,.35f+y*.24f,(z-.5f)*.80f);
                d.Box(p,new Vector3(.61f,.22f,.73f),TownColor.Sand);
                d.Box(p+new Vector3(0,.115f,0),new Vector3(.35f,.015f,.46f),TownColor.Mint);
            }
            d.Collision("Cement pallet",new Vector3(0,.59f,0),new Vector3(2.2f,1.18f,1.8f));return d;
        }

        private static EquipmentDraft Lumber()
        {
            var d = new EquipmentDraft();Pallet(d,3.6f,2.0f);
            for(int y=0;y<5;y++)for(int z=0;z<6;z++)d.Box(new Vector3(0,.3f+y*.14f,(z-2.5f)*.3f),new Vector3(3.5f,.12f,.27f),TownColor.WoodLight);
            foreach(float x in new[]{-1.25f,1.25f})d.Box(new Vector3(x,.6f,0),new Vector3(.05f,.86f,1.84f),TownColor.Wood);
            d.Collision("Lumber stack",new Vector3(0,.5f,0),new Vector3(3.6f,1.0f,2));return d;
        }

        private static EquipmentDraft Sheets()
        {
            var d = new EquipmentDraft();
            d.Box(new Vector3(0,.12f,0),new Vector3(2.8f,.24f,1.4f),TownColor.Wood);
            foreach(float x in new[]{-1.3f,1.3f})d.Box(new Vector3(x,1.2f,-.5f),new Vector3(.1f,2.4f,.1f),TownColor.Frame);
            for(int i=0;i<7;i++)d.Box(new Vector3(0,1.27f,-.38f+i*.14f),new Vector3(2.45f,2.15f,.10f),i%2==0?TownColor.WoodLight:TownColor.Box);
            d.Collision("Sheet rack",new Vector3(0,1.2f,0),new Vector3(2.8f,2.4f,1.4f));return d;
        }

        private static EquipmentDraft Pipes()
        {
            var d = new EquipmentDraft();
            foreach(float x in new[]{-1.65f,1.65f})d.Box(new Vector3(x,.12f,0),new Vector3(.16f,.24f,2.25f),TownColor.Frame);
            foreach(float x in new[]{-1.65f,1.65f})foreach(float z in new[]{-1.02f,1.02f})d.Box(new Vector3(x,.65f,z),new Vector3(.12f,1.3f,.12f),TownColor.Frame);
            foreach(var yz in new[]{new Vector2(.60f,-.68f),new Vector2(.60f,0),new Vector2(.60f,.68f),new Vector2(1.17f,-.34f),new Vector2(1.17f,.34f)})
            {
                var p=new Vector3(0,yz.x,yz.y);d.Mesh.Cylinder(p,.33f,.33f,3.9f,TownColor.Metal,12,Quaternion.Euler(0,0,90));
                foreach(float x in new[]{-1.957f,1.957f})d.Mesh.Cylinder(p+Vector3.right*x,.235f,.235f,.012f,TownColor.Charcoal,12,Quaternion.Euler(0,0,90));
            }
            d.Collision("Pipe cradle",new Vector3(0,.75f,0),new Vector3(3.93f,1.5f,2.25f));return d;
        }

        private static EquipmentDraft Jack()
        {
            var d = new EquipmentDraft();
            foreach(float x in new[]{-.30f,.30f})d.Box(new Vector3(x,.16f,.4f),new Vector3(.19f,.12f,1.65f),TownColor.Yellow);
            d.Box(new Vector3(0,.33f,-.42f),new Vector3(.72f,.35f,.45f),TownColor.Yellow);
            d.Box(new Vector3(0,.94f,-.55f),new Vector3(.07f,1.12f,.07f),TownColor.Frame);
            d.Box(new Vector3(0,1.5f,-.55f),new Vector3(.60f,.07f,.07f),TownColor.Frame);
            foreach(float x in new[]{-.29f,.29f})foreach(float z in new[]{-.5f,1.0f})d.Mesh.Cylinder(new Vector3(x,.12f,z),.12f,.12f,.1f,TownColor.Rubber,8,Quaternion.Euler(0,0,90));
            d.Collision("Pallet jack",new Vector3(0,.76f,.28f),new Vector3(.8f,1.52f,1.95f));return d;
        }

        private static EquipmentDraft Trolley()
        {
            var d = new EquipmentDraft();
            foreach(float x in new[]{-.34f,.34f}){d.Mesh.Cylinder(new Vector3(x,.18f,0),.18f,.18f,.13f,TownColor.Rubber,8,Quaternion.Euler(0,0,90));d.Box(new Vector3(x,1.02f,0),new Vector3(.06f,1.68f,.06f),TownColor.Blue);}
            foreach(float y in new[]{.35f,.95f,1.7f})d.Box(new Vector3(0,y,0),new Vector3(.7f,.06f,.06f),TownColor.Blue);
            d.Box(new Vector3(0,.14f,.22f),new Vector3(.75f,.07f,.50f),TownColor.Blue);
            d.Collision("Hand trolley",new Vector3(0,.93f,.17f),new Vector3(.81f,1.86f,.57f));return d;
        }

        private static EquipmentDraft Paint()
        {
            var d = new EquipmentDraft();
            foreach(float x in new[]{-1.15f,1.15f})d.Box(new Vector3(x,1.1f,0),new Vector3(.09f,2.2f,.65f),TownColor.Wood);
            foreach(float y in new[]{.20f,.85f,1.50f})
            {
                d.Box(new Vector3(0,y,0),new Vector3(2.4f,.08f,.72f),TownColor.WoodLight);
                for(int i=0;i<5;i++){var p=new Vector3((i-2)*.43f,y+.24f,0);d.Mesh.Cylinder(p,.17f,.17f,.40f,TownColor.White,8);d.Mesh.Cylinder(p,.175f,.175f,.13f,i%2==0?TownColor.Orange:TownColor.Blue,8);}
            }
            d.Collision("Paint rack",new Vector3(0,1.1f,0),new Vector3(2.4f,2.2f,.72f));return d;
        }

        private static EquipmentDraft Tools()
        {
            var d = new EquipmentDraft();d.Solid("Tool display",new Vector3(0,1.2f,0),new Vector3(2.4f,2.4f,.30f),TownColor.Wood);
            for(int i=0;i<5;i++){float x=(i-2)*.43f;d.Box(new Vector3(x,1.4f,.2f),new Vector3(.055f,.6f,.06f),TownColor.WoodLight);d.Box(new Vector3(x,1.71f,.21f),new Vector3(.27f,.14f,.08f),TownColor.Metal);}
            d.Box(new Vector3(0,.32f,.25f),new Vector3(2.4f,.12f,.55f),TownColor.WoodLight);return d;
        }
    }
}
