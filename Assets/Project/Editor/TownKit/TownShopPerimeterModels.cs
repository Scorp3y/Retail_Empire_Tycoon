using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>English modelling brief: a lived-in European neighbourhood around a small starter
    /// store. Warm brick walk-up apartments, pale stucco houses, a striped-awning deli, a practical
    /// cycle repair shop and a timber newsstand. Slate roofs, chimneys, balconies, gutters, fitted
    /// windows and restrained weathering. Ground-centred metre pivots. All signs in English.</summary>
    internal static class TownShopPerimeterModels
    {
        internal static IReadOnlyList<EquipmentModel> Create()=>new[]{
            new EquipmentModel("BrickWalkup",()=>Residence(true)),
            new EquipmentModel("StuccoHouse",()=>Residence(false)),
            new EquipmentModel("CornerDeli",()=>Shop(false)),
            new EquipmentModel("CoffeeRoom",()=>Shop(false,true)),
            new EquipmentModel("CycleWorkshop",()=>Shop(true)),
            new EquipmentModel("Newsstand",Newsstand),
            new EquipmentModel("GardenFence",Fence),
            new EquipmentModel("Clothesline",Clothesline),
            new EquipmentModel("RepairDisplay",RepairDisplay)
        };
        internal static Color32[] Palette()
        {
            var p=(Color32[])TownPalette.Colors.Clone();
            p[(int)TownColor.Brick]=new Color32(177,102,69,255);
            p[(int)TownColor.Cream]=new Color32(227,214,183,255);
            p[(int)TownColor.RoofDark]=new Color32(57,65,65,255);
            p[(int)TownColor.Mint]=new Color32(102,139,123,255);
            p[(int)TownColor.Frame]=new Color32(49,67,66,255);
            p[(int)TownColor.Glass]=new Color32(84,127,140,255);
            p[(int)TownColor.Pavement]=new Color32(199,191,169,255);
            return p;
        }
        private static EquipmentDraft Residence(bool brick)
        {
            var d=new EquipmentDraft();float w=brick?18:14,h=brick?9.6f:6.6f,depth=10;
            d.Solid("Closed residential envelope",new Vector3(0,h/2,0),new Vector3(w,h,depth),brick?TownColor.Brick:TownColor.Cream);
            d.Box(new Vector3(0,.25f,0),new Vector3(w+.18f,.5f,depth+.18f),TownColor.Stone);
            foreach(float z in new[]{-5.06f,5.06f})
            {
                float yaw=z>0?0:180;
                for(int floor=0;floor<(brick?3:2);floor++)
                {
                    float y=1.85f+floor*3;
                    for(int bay=0;bay<(brick?5:4);bay++)
                    {
                        float x=(bay-((brick?5:4)-1)*.5f)*3.35f;
                        if(floor==0&&bay==(brick?2:1))continue;
                        if(z>0&&floor>0&&bay==(brick?2:1))continue;
                        Window(d,new Vector3(x,y,z),1.7f,1.95f,yaw,!brick);
                        if(floor>0&&bay%2==0)Balcony(d,new Vector3(x,y-1,z),yaw);
                    }
                    d.Box(new Vector3(0,y+1.37f,z),new Vector3(w+.2f,.12f,.18f),TownColor.Pavement);
                }
                float entrance=brick?0:-1.675f;
                if(z>0)for(int landing=0;landing<(brick?2:1);landing++)
                    Window(d,new Vector3(entrance,3.6f+landing*3,z),1,1,0,false);
                Door(d,new Vector3(entrance,0,z),yaw);
                var q=Quaternion.Euler(0,yaw,0);
                d.Mesh.Box(new Vector3(entrance,2.9f,z)+q*Vector3.forward*.75f,new Vector3(3,.13f,1.5f),TownColor.RoofDark,q);
                d.Mesh.Box(new Vector3(entrance+1.2f,1.35f,z)+q*Vector3.forward*.12f,new Vector3(.35f,.42f,.12f),TownColor.Metal,q);
                for(float x=-w/2+.2f;x<w/2;x+=1.9f)for(float y=.8f;y<h;y+=1.25f)
                    if(brick)d.Box(new Vector3(x,y,z*.99f),new Vector3(.55f,.055f,.12f),TownColor.BoxDark);
            }
            foreach(float side in new[]{-1f,1f})
            {
                for(float z=-2.7f;z<3;z+=5.4f)for(float y=1.85f;y<h;y+=3)Window(d,new Vector3(side*(w/2+.06f),y,z),1.7f,1.95f,side*90,false);
                foreach(float z in new[]{-5.17f,5.17f})
                {
                    d.Box(new Vector3(side*(w/2-.2f),h/2,z),new Vector3(.075f,h,.075f),TownColor.Frame);
                    d.Box(new Vector3(side*(w/2-.2f),h,z),new Vector3(.15f,.12f,.15f),TownColor.Metal);
                }
            }
            Roof(d,w+.8f,11.1f,h+.1f,brick?2.5f:2.2f);
            foreach(float x in new[]{-w*.3f,w*.3f})
            {
                d.Box(new Vector3(x,h+1.9f,-1),new Vector3(.8f,2.6f,.7f),TownColor.Brick);
                d.Box(new Vector3(x,h+3.23f,-1),new Vector3(1,.14f,.9f),TownColor.Stone);
            }
            return d;
        }
        private static void Window(EquipmentDraft d,Vector3 p,float w,float h,float yaw,bool shutters)
        {
            var q=Quaternion.Euler(0,yaw,0);
            void B(Vector3 o,Vector3 s,TownColor c)=>d.Mesh.Box(p+q*o,s,c,q);
            B(Vector3.zero,new Vector3(w+.18f,h+.18f,.10f),TownColor.Frame);
            B(new Vector3(0,0,.07f),new Vector3(w,h,.045f),TownColor.Glass);
            foreach(float x in new[]{-w/2,0,w/2})B(new Vector3(x,0,.12f),new Vector3(.065f,h,.07f),TownColor.White);
            foreach(float y in new[]{-h/2,h/2})B(new Vector3(0,y,.12f),new Vector3(w,.07f,.07f),TownColor.White);
            B(new Vector3(0,-h/2-.13f,.19f),new Vector3(w+.35f,.15f,.4f),TownColor.Pavement);
            B(new Vector3(-w*.28f,h*.12f,.1f),new Vector3(w*.16f,h*.7f,.02f),TownColor.Metal);
            if(shutters)foreach(float side in new[]{-1f,1f})
            {
                B(new Vector3(side*(w/2+.34f),0,.08f),new Vector3(.48f,h,.10f),TownColor.Mint);
                for(float y=-h*.4f;y<h*.45f;y+=.22f)B(new Vector3(side*(w/2+.34f),y,.15f),new Vector3(.42f,.035f,.03f),TownColor.Frame);
            }
        }
        private static void Door(EquipmentDraft d,Vector3 p,float yaw)
        {
            var q=Quaternion.Euler(0,yaw,0);
            d.Mesh.Box(p+Vector3.up*1.35f,new Vector3(1.5f,2.7f,.16f),TownColor.Frame,q);
            d.Mesh.Box(p+Vector3.up*1.65f+q*Vector3.forward*.1f,new Vector3(1.2f,1.7f,.06f),TownColor.Glass,q);
            d.Mesh.Box(p+new Vector3(0,.35f,0)+q*Vector3.forward*.1f,new Vector3(1.2f,.5f,.06f),TownColor.Wood,q);
            d.Mesh.Box(p+q*new Vector3(.48f,1.15f,.18f),new Vector3(.04f,.30f,.06f),TownColor.White,q);
        }
        private static void Balcony(EquipmentDraft d,Vector3 p,float yaw)
        {
            var q=Quaternion.Euler(0,yaw,0);
            void B(Vector3 o,Vector3 s,TownColor c)=>d.Mesh.Box(p+q*o,s,c,q);
            B(new Vector3(0,0,.6f),new Vector3(2.1f,.14f,1.25f),TownColor.Pavement);
            B(new Vector3(0,.94f,1.15f),new Vector3(2.1f,.06f,.06f),TownColor.Frame);
            foreach(float x in new[]{-1f,1f})B(new Vector3(x,.94f,.6f),new Vector3(.06f,.06f,1.15f),TownColor.Frame);
            for(float x=-1;x<=1.01f;x+=.25f)B(new Vector3(x,.48f,1.15f),new Vector3(.035f,.9f,.035f),TownColor.Frame);
            B(new Vector3(.5f,.22f,1),new Vector3(.65f,.30f,.32f),TownColor.Wood);
            for(int i=0;i<3;i++)d.Mesh.Ellipsoid(p+q*new Vector3(.3f+i*.2f,.47f,1),new Vector3(.16f,.17f,.17f),i%2==0?TownColor.Leaf:TownColor.Flowers,5,3);
        }
        private static void Roof(EquipmentDraft d,float w,float depth,float eave,float rise)
        {
            var a=new Vector3(-w/2,eave,-depth/2);var b=new Vector3(w/2,eave,-depth/2);
            var c=new Vector3(w/2,eave,depth/2);var e=new Vector3(-w/2,eave,depth/2);
            var l=new Vector3(-w/2,eave+rise,0);var r=new Vector3(w/2,eave+rise,0);
            d.Mesh.Quad(e,c,r,l,TownColor.RoofDark);d.Mesh.Quad(b,a,l,r,TownColor.RoofDark);
            d.Mesh.Triangle(a,e,l,TownColor.Cream);d.Mesh.Triangle(c,b,r,TownColor.Cream);
            for(int i=0;i<7;i++)
            {
                float t=i/7f;
                foreach(float side in new[]{-1f,1f})d.Box(new Vector3(0,eave+rise*t+.015f,side*depth*.5f*(1-t)),new Vector3(w,.025f,.04f),TownColor.Frame);
            }
            foreach(float z in new[]{-depth/2,depth/2})d.Box(new Vector3(0,eave,z),new Vector3(w,.13f,.16f),TownColor.Frame);
            d.Box(new Vector3(0,eave+rise,0),new Vector3(w+.05f,.12f,.12f),TownColor.Frame);
        }
        private static EquipmentDraft Shop(bool repair,bool coffee=false)
        {
            var d=new EquipmentDraft();const float w=11,depth=6,h=3.6f;
            d.Solid("Closed neighbourhood shop envelope",new Vector3(0,h/2,0),new Vector3(w,h,depth),repair||coffee?TownColor.Cream:TownColor.Brick);
            d.Box(new Vector3(0,.18f,0),new Vector3(w+.15f,.36f,depth+.15f),TownColor.Stone);
            foreach(float x in new[]{-3.5f,3.5f})Window(d,new Vector3(x,1.65f,3.06f),3,2.25f,0,false);
            Door(d,new Vector3(0,0,3.07f),0);
            d.Box(new Vector3(0,3.15f,3.15f),new Vector3(10.2f,.65f,.24f),repair?TownColor.Frame:TownColor.Mint);
            if(!repair&&!coffee)
            {
                for(int i=0;i<12;i++)d.Mesh.Box(new Vector3(-5.05f+i*.92f,2.75f,3.65f),new Vector3(.92f,.09f,1.2f),i%2==0?TownColor.Cream:TownColor.Mint,Quaternion.Euler(9,0,0));
                foreach(float x in new[]{-3.5f,3.5f})
                {
                    d.Box(new Vector3(x,.55f,3.5f),new Vector3(2.4f,1.1f,.65f),TownColor.Wood);
                    for(int i=0;i<5;i++)d.Mesh.Ellipsoid(new Vector3(x-1+i*.5f,1.2f,3.5f),Vector3.one*.18f,i%2==0?TownColor.Yellow:TownColor.Orange,6,3);
                }
            }
            else if(repair)
            {
                d.Box(new Vector3(0,2,-3.035f),new Vector3(5,2.7f,.08f),TownColor.Metal);
                for(float y=.7f;y<3.2f;y+=.3f)d.Box(new Vector3(0,y,-3.09f),new Vector3(5,.04f,.06f),TownColor.Frame);
            }
            foreach(float x in new[]{-5.55f,5.55f})Window(d,new Vector3(x,1.8f,0),2.5f,1.7f,x<0?270:90,false);
            if(coffee)
            {
                d.Box(new Vector3(0,3.65f,0),new Vector3(11.7f,.22f,6.8f),TownColor.RoofDark);
                foreach(float x in new[]{-5.65f,5.65f})d.Box(new Vector3(x,3.95f,0),new Vector3(.20f,.45f,6.8f),TownColor.Mint);
                foreach(float z in new[]{-3.3f,3.3f})d.Box(new Vector3(0,3.95f,z),new Vector3(11.5f,.45f,.20f),TownColor.Mint);
                d.Box(new Vector3(0,2.74f,3.65f),new Vector3(10.8f,.14f,1.3f),TownColor.Mint);
                d.Box(new Vector3(2.7f,3.98f,-.5f),new Vector3(1.6f,.45f,1.1f),TownColor.Metal);
                for(int i=0;i<7;i++)d.Box(new Vector3(2.1f+i*.2f,4.22f,-.5f),new Vector3(.07f,.025f,.95f),TownColor.Frame);
            }
            else Roof(d,11.7f,6.8f,3.65f,.95f);
            d.Box(new Vector3(-3.7f,4.3f,-1),new Vector3(.5f,1.4f,.5f),TownColor.Brick);return d;
        }
        private static EquipmentDraft Newsstand()
        {
            var d=new EquipmentDraft();d.Solid("Closed kiosk",new Vector3(0,1.35f,0),new Vector3(4.4f,2.7f,3.2f),TownColor.Wood);
            d.Box(new Vector3(0,1.6f,1.63f),new Vector3(3.5f,1.5f,.07f),TownColor.Glass);
            d.Box(new Vector3(0,.95f,1.8f),new Vector3(3.9f,.13f,.55f),TownColor.WoodLight);
            d.Box(new Vector3(0,2.48f,1.74f),new Vector3(4.2f,.48f,.18f),TownColor.Frame);
            for(int i=0;i<6;i++)d.Box(new Vector3(-1.45f+i*.58f,1.16f,1.84f),new Vector3(.45f,.32f,.30f),i%2==0?TownColor.White:TownColor.Blue);
            Roof(d,4.95f,3.9f,2.8f,.7f);return d;
        }
        private static EquipmentDraft Fence()
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-2f,2f})d.Solid("Fence post",new Vector3(x,.7f,0),new Vector3(.16f,1.4f,.16f),TownColor.Brick);
            foreach(float y in new[]{.35f,1.1f})d.Box(new Vector3(0,y,0),new Vector3(4,.07f,.07f),TownColor.Frame);
            for(float x=-1.8f;x<2;x+=.3f)d.Box(new Vector3(x,.7f,0),new Vector3(.04f,1.1f,.04f),TownColor.Frame);return d;
        }
        private static EquipmentDraft Clothesline()
        {
            var d=new EquipmentDraft();foreach(float x in new[]{-2f,2f})d.Solid("Laundry post",new Vector3(x,1.5f,0),new Vector3(.07f,3,.07f),TownColor.Frame);
            d.Box(new Vector3(0,2.8f,0),new Vector3(4,.025f,.025f),TownColor.Frame);
            for(int i=0;i<4;i++)d.Box(new Vector3(-1.4f+i*.9f,2.25f,0),new Vector3(.65f,1.05f,.035f),i%2==0?TownColor.Cream:TownColor.Blue);return d;
        }
        private static EquipmentDraft RepairDisplay()
        {
            var d=new EquipmentDraft();d.Collision("Repair stand",new Vector3(0,.6f,0),new Vector3(2,1.2f,.65f));
            foreach(float x in new[]{-.8f,.8f})d.Box(new Vector3(x,.45f,0),new Vector3(.07f,.9f,.5f),TownColor.Frame);
            d.Box(new Vector3(0,.95f,0),new Vector3(2.1f,.12f,.65f),TownColor.Wood);
            foreach(float x in new[]{-.65f,0,.65f})d.Mesh.Cylinder(new Vector3(x,1.12f,0),.18f,.18f,.25f,TownColor.Rubber,10);
            return d;
        }
    }
}
