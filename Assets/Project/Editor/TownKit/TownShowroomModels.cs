using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>Native metre-scale showroom kit. Public facades point along +Z.</summary>
    internal static class TownShowroomModels
    {
        internal const float InteriorFloor=.286f;
        internal const int OriginalInteriorTriangles=1872; // Three 592-triangle display cars and 96 furniture triangles.
        public static Color32[] Atlas()
        {
            var colors=(Color32[])TownPalette.Colors.Clone();
            colors[(int)TownColor.Cream]=new Color32(219,221,211,255);
            colors[(int)TownColor.RoofDark]=new Color32(119,123,120,255);
            colors[(int)TownColor.Frame]=new Color32(45,55,58,255);
            colors[(int)TownColor.Blue]=new Color32(44,76,117,255);
            colors[(int)TownColor.Asphalt]=new Color32(76,86,90,255);
            colors[(int)TownColor.Pavement]=new Color32(204,196,173,255);
            return colors;
        }
        public static IReadOnlyList<ShowroomModel> Create()=>new[]{
            new ShowroomModel("ShowroomHall",Hall),new ShowroomModel("ServiceWing",ServiceWing),new ShowroomModel("OfficeLink",OfficeLink),
            new ShowroomModel("TireRack",TireRack),new ShowroomModel("AccessoryTrolley",Trolley),new ShowroomModel("ShowroomPlanter",Planter),
            new ShowroomModel("DealershipPylon",Pylon),new ShowroomModel("DisplayPlinth",Plinth),new ShowroomModel("SiteLamp",Lamp),
            new ShowroomModel("MeshFence",Fence),new ShowroomModel("SiteTree",Tree),
            new ShowroomModel("BlueSedan",()=>Car(TownColor.Blue)),new ShowroomModel("WhiteSedan",()=>Car(TownColor.White)),
            new ShowroomModel("RedSedan",()=>Car(TownColor.Red)),new ShowroomModel("GreenSedan",()=>Car(TownColor.GreenStripe)),
            new ShowroomModel("InteriorDressing",InteriorDressing),new ShowroomModel("GarageWorkbench",Workbench),
            new ShowroomModel("PartsCabinet",Cabinet),new ShowroomModel("HoseReel",Hose),new ShowroomModel("ServicePlanter",ServicePlanter)};
        public static IReadOnlyList<ShowroomModel> RevisedShells()=>new[]{new ShowroomModel("ShowroomHall",Hall),new ShowroomModel("ServiceWing",ServiceWing),new ShowroomModel("OfficeLink",OfficeLink)};
        public static IReadOnlyList<ShowroomModel> DressingModels()=>new[]{new ShowroomModel("InteriorDressing",InteriorDressing),new ShowroomModel("GarageWorkbench",Workbench),new ShowroomModel("PartsCabinet",Cabinet),new ShowroomModel("HoseReel",Hose),new ShowroomModel("ServicePlanter",ServicePlanter)};
        public static ShowroomModel ServiceRoofRevision()=>new("ServiceWing",ServiceWing);
        private static ShowroomDraft Hall()
        {
            var d=new ShowroomDraft();
            // Hollow visual shell reveals the exhibition. Vehicle collider is an envelope: no walk-in gameplay.
            d.Solids.Add(new FloristSolid("Hall vehicle envelope",new Vector3(0,2.5f,0),new Vector3(20,5,12),Quaternion.identity));
            d.Box(new Vector3(0,.12f,0),new Vector3(20.3f,.24f,12.3f),TownColor.Stone);
            d.Box(new Vector3(0,.25f,0),new Vector3(19.8f,.05f,11.8f),TownColor.White);
            for(float x=-9.5f;x<10;x+=1.5f)d.Box(new Vector3(x,.282f,0),new Vector3(.018f,.008f,11.8f),TownColor.Stone);
            for(float z=-5.5f;z<6;z+=1.5f)d.Box(new Vector3(0,.282f,z),new Vector3(19.8f,.008f,.018f),TownColor.Stone);
            d.Box(new Vector3(0,2.6f,-5.9f),new Vector3(20,4.8f,.22f),TownColor.Cream);
            d.Box(new Vector3(-9.9f,2.6f,0),new Vector3(.22f,4.8f,12),TownColor.Cream);
            d.Box(new Vector3(-9.67f,2.6f,5.92f),new Vector3(.66f,4.8f,.4f),TownColor.Brick);
            for(float y=.55f;y<4.8f;y+=.29f)d.Box(new Vector3(-9.67f,y,6.125f),new Vector3(.67f,.022f,.025f),TownColor.BoxDark);
            foreach(float y in new[]{.4f,4.23f})d.Box(new Vector3(0,y,6.07f),new Vector3(20,.13f,.15f),TownColor.Frame);
            foreach(float x in new[]{-9.2f,-6.5f,-2.0f,2.0f,6.5f,9.95f})d.Box(new Vector3(x,2.3f,6.07f),new Vector3(.10f,3.85f,.15f),TownColor.Frame);
            foreach(float z in new[]{-5.9f,-3f,0f,3f,5.9f})d.Box(new Vector3(10.06f,2.3f,z),new Vector3(.15f,3.85f,.1f),TownColor.Frame);
            foreach(float y in new[]{.4f,4.23f})d.Box(new Vector3(10.06f,y,0),new Vector3(.15f,.13f,12),TownColor.Frame);
            d.Box(new Vector3(0,4.76f,6.15f),new Vector3(20.3f,.92f,.3f),TownColor.Blue);
            d.Box(new Vector3(10.05f,4.76f,0),new Vector3(.3f,.92f,12.3f),TownColor.Cream);
            FlatRoof(d,20.7f,12.7f,5.1f);
            d.Box(new Vector3(0,3.42f,7),new Vector3(4.0f,.18f,2.05f),TownColor.Frame);
            foreach(float x in new[]{-1.9f,1.9f})d.Solid("Entrance canopy post",new Vector3(x,1.65f,7.83f),new Vector3(.11f,3.3f,.11f),TownColor.Frame);
            d.Box(new Vector3(0,1.62f,6.12f),new Vector3(.08f,2.9f,.16f),TownColor.Frame);
            foreach(float x in new[]{-.17f,.17f})d.Box(new Vector3(x,1.53f,6.24f),new Vector3(.05f,.45f,.05f),TownColor.Metal);
            d.Box(new Vector3(0,InteriorFloor+.6f,-4.0f),new Vector3(3.2f,1.2f,1.0f),TownColor.WoodLight);
            d.Box(new Vector3(0,InteriorFloor+1.22f,-4.0f),new Vector3(3.5f,.12f,1.15f),TownColor.White);
            d.Box(new Vector3(.55f,InteriorFloor+1.47f,-4.05f),new Vector3(.6f,.4f,.08f),TownColor.Frame);
            foreach(float x in new[]{-7.4f,-5.8f})
            {d.Box(new Vector3(x,InteriorFloor+.3f,-4.0f),new Vector3(1.2f,.6f,1.2f),TownColor.Blue);d.Box(new Vector3(x,1.1f,-4.5f),new Vector3(1.2f,.6f,.2f),TownColor.Blue);}
            d.Box(new Vector3(-6.6f,.73f,-2.8f),new Vector3(1.2f,.10f,.75f),TownColor.WoodLight);
            foreach(float x in new[]{-7.06f,-6.14f})foreach(float z in new[]{-3.04f,-2.56f})
                d.Box(new Vector3(x,(InteriorFloor+.68f)/2,z),new Vector3(.10f,.68f-InteriorFloor,.10f),TownColor.Metal);
            foreach(var p in new[]{new Vector3(-7,5.53f,-3.9f),new Vector3(7,5.53f,-3.9f)})Vent(d,p);
            return d;
        }
        private static void FlatRoof(ShowroomDraft d,float w,float depth,float h,bool frontEdge=true)
        {
            d.Box(new Vector3(0,h,0),new Vector3(w,.24f,depth),TownColor.RoofDark);
            d.Box(new Vector3(0,h+.26f,-depth/2),new Vector3(w,.5f,.2f),TownColor.Cream);
            if(frontEdge)d.Box(new Vector3(0,h+.26f,depth/2),new Vector3(w,.5f,.2f),TownColor.Cream);
            foreach(float x in new[]{-w/2,w/2})d.Box(new Vector3(x,h+.26f,0),new Vector3(.2f,.5f,depth),TownColor.Cream);
            for(float x=-w/2+.6f;x<w/2;x+=2.5f)d.Box(new Vector3(x,h+.125f,0),new Vector3(.018f,.01f,depth-.4f),TownColor.Stone);
        }
        private static void Vent(ShowroomDraft d,Vector3 p)
        {d.Box(p,new Vector3(1.0f,.5f,.8f),TownColor.Metal);for(int i=0;i<5;i++)d.Box(p+new Vector3((i-2)*.16f,.26f,0),new Vector3(.07f,.015f,.6f),TownColor.Frame);}
        private static ShowroomDraft ServiceWing()
        {
            var d=new ShowroomDraft();d.Solid("Service building",new Vector3(0,1.9f,0),new Vector3(26,3.8f,5),TownColor.Cream);FlatRoof(d,26.4f,5.4f,3.91f);
            // Continuous service parapet closes the junction. The office has no independent parapet loop.
            foreach(float x in new[]{2.5f,9.5f})
            {
                d.Box(new Vector3(x,1.65f,2.56f),new Vector3(4.45f,3.3f,.1f),TownColor.Frame);
                d.Box(new Vector3(x,1.65f,2.62f),new Vector3(4.25f,3.1f,.05f),TownColor.Metal);
                for(float y=.2f;y<3.2f;y+=.18f)d.Box(new Vector3(x,y,2.656f),new Vector3(4.22f,.027f,.018f),TownColor.Frame);
                d.Box(new Vector3(x,3.57f,2.72f),new Vector3(.35f,.16f,.15f),TownColor.Light);
            }
            d.Box(new Vector3(-4.9f,1.4f,2.58f),new Vector3(1.5f,2.8f,.1f),TownColor.Frame);
            d.Box(new Vector3(-4.9f,1.8f,2.64f),new Vector3(1.26f,1.28f,.03f),TownColor.Glass);
            Vent(d,new Vector3(-6,4.36f,-.5f));return d;
        }
        private static ShowroomDraft OfficeLink()
        {
            var d=new ShowroomDraft();d.Solid("Office connection",new Vector3(0,2,0),new Vector3(3,4,10),TownColor.Cream);
            // Bare linking slab meets the service roof at the same elevation and ends at its front edge.
            d.Box(new Vector3(0,3.91f,.25f),new Vector3(3.3f,.24f,8.3f),TownColor.RoofDark);return d;
        }
        private static ShowroomDraft InteriorDressing()
        {
            var d=new ShowroomDraft();float floor=InteriorFloor;
            foreach(float x in new[]{-4.35f,1.65f,7.65f})
            {
                d.Box(new Vector3(x,floor+.04f,4.25f),new Vector3(.58f,.08f,.48f),TownColor.Frame);
                d.Box(new Vector3(x,floor+.415f,4.25f),new Vector3(.07f,.75f,.07f),TownColor.Metal);
                d.Box(new Vector3(x,floor+1,4.25f),new Vector3(.68f,.45f,.055f),TownColor.White);
                foreach(float y in new[]{floor+1.08f,floor+.96f})d.Box(new Vector3(x,y,4.284f),new Vector3(.48f,.035f,.012f),TownColor.Blue);
            }
            var table=new Vector3(5.8f,floor+.79f,-3.6f);
            d.Mesh.Cylinder(table,.73f,.73f,.10f,TownColor.WoodLight,8);
            d.Mesh.Cylinder(new Vector3(5.8f,floor+.38f,-3.6f),.055f,.055f,.76f,TownColor.Metal,6);
            d.Mesh.Cylinder(new Vector3(5.8f,floor+.035f,-3.6f),.34f,.34f,.07f,TownColor.Frame,6);
            foreach(float x in new[]{4.65f,6.95f})
            {
                d.Box(new Vector3(x,floor+.47f,-3.6f),new Vector3(.8f,.14f,.8f),TownColor.Blue);
                d.Box(new Vector3(x,floor+.8f,-3.96f),new Vector3(.8f,.65f,.12f),TownColor.Blue);
                foreach(float side in new[]{-.3f,.3f})d.Box(new Vector3(x+side,floor+.20f,-3.6f),new Vector3(.045f,.4f,.72f),TownColor.Metal);
            }
            foreach(float x in new[]{2.25f,3.15f})d.Box(new Vector3(x,floor+.55f,-4.65f),new Vector3(.07f,1.1f,.07f),TownColor.Frame);
            foreach(float y in new[]{floor+.35f,floor+.9f})d.Box(new Vector3(2.7f,y,-4.65f),new Vector3(1.05f,.05f,.42f),TownColor.Metal);
            for(int i=0;i<3;i++)d.Box(new Vector3(2.4f+i*.3f,floor+.975f,-4.65f),new Vector3(.23f,.10f,.3f),i==1?TownColor.Blue:TownColor.White);
            foreach(float x in new[]{-3.4f,4.1f})
            {
                d.Box(new Vector3(x,2.72f,-5.75f),new Vector3(1.9f,1.16f,.06f),TownColor.Frame);
                d.Box(new Vector3(x,2.72f,-5.713f),new Vector3(1.75f,1.01f,.015f),TownColor.White);
                d.Box(new Vector3(x,2.83f,-5.7f),new Vector3(1.73f,.5f,.012f),TownColor.Blue);
                d.Box(new Vector3(x,2.40f,-5.7f),new Vector3(1.30f,.055f,.012f),TownColor.Blue);
            }
            foreach(var p in new[]{new Vector3(-8.7f,floor,-2.0f),new Vector3(8.6f,floor,-4.7f)})
            {
                d.Mesh.Cylinder(p+Vector3.up*.24f,.27f,.35f,.48f,TownColor.Wood,8);
                d.Mesh.Cylinder(p+Vector3.up*.486f,.31f,.31f,.02f,TownColor.Soil,8);
                d.Box(p+Vector3.up*.85f,new Vector3(.065f,.75f,.065f),TownColor.Trunk);
                foreach(var crown in new[]{new Vector3(-.13f,1.19f,0),new Vector3(.15f,1.49f,0)})
                    d.Mesh.Ellipsoid(p+crown,new Vector3(.46f,.43f,.42f),crown.x<0?TownColor.Leaf:TownColor.LeafLight,6,2);
            }
            return d;
        }
        private static ShowroomDraft Workbench()
        {
            var d=new ShowroomDraft();foreach(float x in new[]{-1.25f,1.25f})foreach(float z in new[]{-.4f,.4f})d.Box(new Vector3(x,.5f,z),new Vector3(.085f,1,.085f),TownColor.Metal);
            d.Box(new Vector3(0,1.03f,0),new Vector3(2.8f,.12f,1.0f),TownColor.WoodLight);
            d.Box(new Vector3(0,.25f,0),new Vector3(2.65f,.07f,.9f),TownColor.Metal);
            d.Box(new Vector3(0,1.68f,-.46f),new Vector3(2.75f,1.12f,.08f),TownColor.Frame);
            for(int i=0;i<5;i++){float x=(i-2)*.47f;d.Box(new Vector3(x,1.7f,-.39f),new Vector3(.055f,.48f,.04f),TownColor.Metal);d.Box(new Vector3(x,1.43f,-.37f),new Vector3(.11f,.10f,.06f),TownColor.Blue);}
            foreach(float x in new[]{-.8f,.6f}){d.Box(new Vector3(x,.49f,0),new Vector3(.65f,.42f,.55f),TownColor.Blue);d.Box(new Vector3(x,1.25f,0),new Vector3(.6f,.30f,.40f),TownColor.Red);}
            d.Solids.Add(new FloristSolid("Workbench",new Vector3(0,1.1f,0),new Vector3(2.8f,2.2f,1),Quaternion.identity));return d;
        }
        private static ShowroomDraft Cabinet()
        {
            var d=new ShowroomDraft();d.Solid("Parts cabinet",new Vector3(0,.9f,0),new Vector3(2,1.8f,.75f),TownColor.Frame);
            foreach(float x in new[]{-.5f,.5f}){d.Box(new Vector3(x,.96f,.395f),new Vector3(.94f,1.6f,.04f),TownColor.Blue);d.Box(new Vector3(x,.9f,.43f),new Vector3(.05f,.25f,.06f),TownColor.Metal);}
            for(float y=.3f;y<1.7f;y+=.2f)d.Box(new Vector3(0,y,.423f),new Vector3(.8f,.025f,.012f),TownColor.Metal);return d;
        }
        private static ShowroomDraft Hose()
        {
            var d=new ShowroomDraft();d.Box(new Vector3(0,0,-.14f),new Vector3(.20f,.20f,.25f),TownColor.Metal);
            for(int i=0;i<3;i++)
            {
                float z=i*.065f;for(int segment=0;segment<14;segment++)
                {
                    Vector3 P(int s)=>new Vector3(Mathf.Cos(s*Mathf.PI*2/14)*.36f,Mathf.Sin(s*Mathf.PI*2/14)*.36f,z);
                    var a=P(segment);var b=P(segment+1);d.Mesh.Box((a+b)/2,new Vector3(.05f,.05f,Vector3.Distance(a,b)),TownColor.Rubber,Quaternion.LookRotation(b-a));
                }
            }
            d.Box(new Vector3(.20f,-.52f,.10f),new Vector3(.05f,.45f,.05f),TownColor.Rubber);return d;
        }
        private static ShowroomDraft ServicePlanter()
        {
            var d=new ShowroomDraft();d.Solid("Service planter",new Vector3(0,.2f,0),new Vector3(1.4f,.4f,.75f),TownColor.Stone);
            for(int i=0;i<3;i++)d.Mesh.Ellipsoid(new Vector3((i-1)*.4f,.64f,0),new Vector3(.32f,.39f,.3f),i==1?TownColor.LeafLight:TownColor.Leaf,6,3);return d;
        }
        private static ShowroomDraft TireRack()
        {
            var d=new ShowroomDraft();foreach(float x in new[]{-1.5f,1.5f})foreach(float z in new[]{-.45f,.45f})d.Box(new Vector3(x,.85f,z),new Vector3(.06f,1.7f,.06f),TownColor.Frame);
            foreach(float y in new[]{.2f,1.0f})
            {d.Box(new Vector3(0,y,0),new Vector3(3.1f,.06f,1.0f),TownColor.Metal);for(int i=0;i<4;i++)
            {var p=new Vector3((i-1.5f)*.7f,y+.39f,0);d.Mesh.Cylinder(p,.38f,.38f,.24f,TownColor.Rubber,10,Quaternion.Euler(0,0,90));d.Mesh.Cylinder(p+Vector3.right*.125f,.22f,.22f,.015f,TownColor.Metal,8,Quaternion.Euler(0,0,90));}}
            d.Solids.Add(new FloristSolid("Tire display",new Vector3(0,.85f,0),new Vector3(3.1f,1.7f,1),Quaternion.identity));return d;
        }
        private static ShowroomDraft Trolley()
        {
            var d=new ShowroomDraft();d.Box(new Vector3(0,.3f,0),new Vector3(1.2f,.12f,1),TownColor.Blue);
            foreach(float x in new[]{-.5f,.5f})foreach(float z in new[]{-.4f,.4f})d.Mesh.Cylinder(new Vector3(x,.16f,z),.14f,.14f,.09f,TownColor.Rubber,8,Quaternion.Euler(0,0,90));
            foreach(float x in new[]{-.5f,.5f})d.Box(new Vector3(x,.8f,-.45f),new Vector3(.045f,1.05f,.045f),TownColor.Metal);
            d.Box(new Vector3(0,1.33f,-.45f),new Vector3(1.05f,.045f,.045f),TownColor.Metal);
            foreach(var p in new[]{new Vector3(-.3f,.58f,.1f),new Vector3(.3f,.58f,.1f),new Vector3(0,.98f,0)})
            {d.Box(p,new Vector3(.55f,.42f,.65f),TownColor.Box);d.Box(p+new Vector3(0,0,.331f),new Vector3(.2f,.11f,.01f),TownColor.White);}
            d.Solids.Add(new FloristSolid("Accessory trolley",new Vector3(0,.65f,0),new Vector3(1.2f,1.3f,1),Quaternion.identity));return d;
        }
        private static ShowroomDraft Planter()
        {
            var d=new ShowroomDraft();d.Solid("Concrete planter",new Vector3(0,.22f,0),new Vector3(3.4f,.44f,1.0f),TownColor.Stone);
            d.Box(new Vector3(0,.45f,0),new Vector3(3.15f,.02f,.8f),TownColor.Soil);
            for(int i=0;i<6;i++)d.Mesh.Ellipsoid(new Vector3((i-2.5f)*.5f,.85f,0),new Vector3(.39f,.42f,.39f),i%2==0?TownColor.Leaf:TownColor.LeafLight,7,3);return d;
        }
        private static ShowroomDraft Pylon()
        {var d=new ShowroomDraft();d.Solid("Pylon",new Vector3(0,1.75f,0),new Vector3(2.4f,3.5f,.45f),TownColor.Blue);d.Box(new Vector3(0,.12f,0),new Vector3(2.8f,.24f,.9f),TownColor.Stone);return d;}
        private static ShowroomDraft Plinth()
        {var d=new ShowroomDraft();d.Solid("Exhibition platform",new Vector3(0,.14f,0),new Vector3(3.1f,.28f,5.7f),TownColor.Stone);d.Box(new Vector3(0,.292f,0),new Vector3(2.9f,.025f,5.5f),TownColor.White);return d;}
        private static ShowroomDraft Lamp()
        {var d=new ShowroomDraft();d.Solid("Lamp foot",new Vector3(0,.12f,0),new Vector3(.34f,.24f,.34f),TownColor.Frame);d.Box(new Vector3(0,1.9f,0),new Vector3(.085f,3.8f,.085f),TownColor.Frame);d.Box(new Vector3(0,3.85f,0),new Vector3(.6f,.12f,.6f),TownColor.Frame);d.Box(new Vector3(0,3.77f,0),new Vector3(.45f,.04f,.45f),TownColor.Light);return d;}
        private static ShowroomDraft Fence()
        {
            var d=new ShowroomDraft();d.Solid("Fence base",new Vector3(0,.2f,0),new Vector3(3,.4f,.24f),TownColor.Stone);
            foreach(float x in new[]{-1.5f,1.5f})d.Box(new Vector3(x,.85f,0),new Vector3(.085f,1.7f,.085f),TownColor.Frame);
            for(float x=-1.4f;x<1.5f;x+=.22f)d.Box(new Vector3(x,1.05f,0),new Vector3(.02f,1.25f,.02f),TownColor.Frame);
            for(float y=.55f;y<1.7f;y+=.20f)d.Box(new Vector3(0,y,0),new Vector3(3,.018f,.018f),TownColor.Frame);
            d.Solids.Add(new FloristSolid("Fence",new Vector3(0,1.05f,0),new Vector3(3,1.3f,.04f),Quaternion.identity));return d;
        }
        private static ShowroomDraft Tree()
        {var d=new ShowroomDraft();TownNature.Tree(d.Mesh,Vector3.zero,2);d.Solids.Add(new FloristSolid("Tree trunk",new Vector3(0,1.15f,0),new Vector3(.35f,2.3f,.35f),Quaternion.identity));return d;}
        private static ShowroomDraft Car(TownColor color)
        {
            var d=new ShowroomDraft();var mesh=TownVehicles.Passenger(color).Build("Showroom sedan");
            try{d.Mesh.Append(mesh,Matrix4x4.identity);d.Solids.Add(new FloristSolid("Vehicle body",mesh.bounds.center,mesh.bounds.size,Quaternion.identity));}finally{UnityEngine.Object.DestroyImmediate(mesh);}return d;
        }
    }
    internal sealed class ShowroomModel
    {
        public string Id{get;}public ShowroomDraft Draft{get;}
        public ShowroomModel(string id,Func<ShowroomDraft> build){Id=id;Draft=build();}
    }
    internal sealed class ShowroomDraft
    {
        public TownMeshBuilder Mesh{get;}=new();
        public List<FloristSolid> Solids{get;}=new();
        public void Box(Vector3 p,Vector3 s,TownColor color)=>Mesh.Box(p,s,color);
        public void Solid(string id,Vector3 p,Vector3 s,TownColor color){Box(p,s,color);Solids.Add(new FloristSolid(id,p,s,Quaternion.identity));}
    }
}
