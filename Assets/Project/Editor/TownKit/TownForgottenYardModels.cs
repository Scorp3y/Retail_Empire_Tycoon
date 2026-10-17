using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>Native, reusable low-poly props. Dimensions are in the shared town's metres.</summary>
    internal static class TownForgottenYardModels
    {
        // Modelling brief: A once-used neighbourhood yard slowly reclaimed by plants. Faded paint,
        // missing wooden slats, bent tubing and small believable leftovers, not disaster debris.
        // Preserve readable silhouettes and real openings; no text or fake painted-on geometry.
        internal static Dictionary<string,TownMeshBuilder> Create()
        {
            return new Dictionary<string,TownMeshBuilder>
            {
                {"BrokenBench",Bench(true)}, {"WeatheredBench",Bench(false)},
                {"PicnicTable",Table()}, {"OldChair",Chair()}, {"FadedSlide",Slide()},
                {"OneSeatSwing",Swing()}, {"OvergrownSandbox",Sandbox()},
                {"FallenBicycle",Bicycle()}, {"DetachedWheel",Wheel()},
                {"DiscardedToilet",Toilet()}, {"OldTyre",Tyre()},
                {"LeaningPallet",Pallet()}, {"SplitCrate",Crate()},
                {"WateringCan",WateringCan()}, {"RustBucket",Bucket()},
                {"LooseBoards",Boards()}, {"Stump",Stump()}, {"FallenBranch",Branch()},
                {"TallWeeds",Weeds()}, {"Shrub",Shrub()}, {"Birch",Tree(false)},
                {"OldMaple",Tree(true)}, {"OldNoticeboard",Noticeboard()}, {"FenceRemnant",Fence()}
            };
        }

        private static void Rod(TownMeshBuilder m,Vector3 a,Vector3 b,float radius,TownColor color,int sides=6)
            => m.Cylinder((a+b)*.5f,radius,radius,(b-a).magnitude,color,sides,Quaternion.FromToRotation(Vector3.up,b-a));

        private static TownMeshBuilder Bench(bool broken)
        {
            var m=new TownMeshBuilder();
            foreach(float x in new[]{-1f,1f})
            {
                Rod(m,new Vector3(x,.06f,-.33f),new Vector3(x,.58f,-.29f),.055f,TownColor.Metal);
                Rod(m,new Vector3(x,.06f,.35f),new Vector3(x,.6f,.29f),.055f,TownColor.Metal);
                Rod(m,new Vector3(x,.55f,.3f),new Vector3(x,1.22f,.5f),.05f,TownColor.Metal);
                m.Box(new Vector3(x,.51f,0),new Vector3(.11f,.08f,.95f),TownColor.BoxDark);
                Rod(m,new Vector3(x,.74f,-.32f),new Vector3(x,.79f,.36f),.043f,TownColor.Metal);
            }
            for(int z=0;z<4;z++)
            {
                if(broken&&z==1)
                {
                    m.Box(new Vector3(-.76f,.58f,-.1f),new Vector3(1.08f,.09f,.17f),TownColor.WoodLight);
                    m.Box(new Vector3(.94f,.55f,-.08f),new Vector3(.72f,.09f,.17f),TownColor.Wood,Quaternion.Euler(0,0,-8));
                }
                else m.Box(new Vector3(0,.58f,-.3f+z*.2f),new Vector3(2.65f,.09f,.17f),z%2==0?TownColor.WoodLight:TownColor.Wood);
            }
            for(int y=0;y<3;y++)
            {
                if(broken&&y==1)continue;
                m.Box(new Vector3(0,.85f+y*.16f,.4f+y*.035f),new Vector3(2.65f,.12f,.075f),y%2==0?TownColor.Mint:TownColor.Wood);
                foreach(float x in new[]{-1f,1f})m.Cylinder(new Vector3(x,.85f+y*.16f,.35f+y*.035f),.019f,.019f,.025f,TownColor.Metal,6,Quaternion.Euler(90,0,0));
            }
            if(broken)m.Box(new Vector3(.45f,.07f,-.9f),new Vector3(1.45f,.07f,.14f),TownColor.WoodLight,Quaternion.Euler(0,24,2));
            return m;
        }
        private static TownMeshBuilder Table()
        {
            var m=new TownMeshBuilder();
            foreach(float x in new[]{-1f,1f})
            {
                Rod(m,new Vector3(x,.06f,-.65f),new Vector3(x,.95f,.35f),.065f,TownColor.Wood);
                Rod(m,new Vector3(x,.06f,.65f),new Vector3(x,.95f,-.35f),.065f,TownColor.Wood);
            }
            for(int i=0;i<5;i++)m.Box(new Vector3(0,.99f,(i-2)*.24f),new Vector3(2.65f,.1f,.21f),i==1?TownColor.Mint:TownColor.WoodLight,Quaternion.Euler(0,0,i==3?2:0));
            Rod(m,new Vector3(-1,.48f,0),new Vector3(1,.48f,0),.06f,TownColor.Wood);
            return m;
        }
        private static TownMeshBuilder Chair()
        {
            var m=new TownMeshBuilder();
            foreach(float x in new[]{-.27f,.27f})foreach(float z in new[]{-.28f,.28f})
                Rod(m,new Vector3(x,.04f,z),new Vector3(x,.57f,z),.035f,TownColor.Metal);
            for(int i=0;i<3;i++)m.Box(new Vector3(0,.59f,(i-1)*.18f),new Vector3(.68f,.06f,.16f),TownColor.Mint);
            foreach(float x in new[]{-.27f,.27f})Rod(m,new Vector3(x,.55f,.28f),new Vector3(x,1.12f,.35f),.035f,TownColor.Metal);
            m.Box(new Vector3(0,1,.33f),new Vector3(.67f,.18f,.05f),TownColor.Mint);return m;
        }
        private static TownMeshBuilder Slide()
        {
            var m=new TownMeshBuilder();
            foreach(float x in new[]{-.58f,.58f})
            {
                Rod(m,new Vector3(x,.03f,-1.38f),new Vector3(x,2.12f,-.55f),.065f,TownColor.Red);
                Rod(m,new Vector3(x,.03f,.02f),new Vector3(x,2.12f,-.55f),.065f,TownColor.Metal);
                Rod(m,new Vector3(x,1.85f,-.63f),new Vector3(x,2.65f,-.63f),.045f,TownColor.Red);
                Rod(m,new Vector3(x,2.65f,-.63f),new Vector3(x,2.65f,-.15f),.045f,TownColor.Red);
            }
            for(int i=0;i<7;i++)Rod(m,new Vector3(-.58f,.26f+i*.26f,-1.3f+i*.105f),new Vector3(.58f,.26f+i*.26f,-1.3f+i*.105f),.04f,TownColor.Metal);
            m.Box(new Vector3(0,2,-.4f),new Vector3(1.2f,.1f,.6f),TownColor.Mint);
            var profile=new[]{new Vector3(0,2,-.08f),new Vector3(0,1.9f,.3f),new Vector3(0,.25f,2.5f),new Vector3(0,.16f,3.05f)};
            for(int i=0;i<profile.Length-1;i++)
            {
                var a=profile[i];var b=profile[i+1];
                m.Quad(a+Vector3.left*.48f,b+Vector3.left*.48f,b+Vector3.right*.48f,a+Vector3.right*.48f,TownColor.Mint);
                m.Quad(a+Vector3.right*.48f,b+Vector3.right*.48f,b+Vector3.left*.48f,a+Vector3.left*.48f,TownColor.Mint);
                foreach(float side in new[]{-1f,1f})
                    Rod(m,a+new Vector3(side*.5f,.09f,0),b+new Vector3(side*.5f,.09f,0),.06f,TownColor.Metal);
            }
            return m;
        }
        private static TownMeshBuilder Swing()
        {
            var m=new TownMeshBuilder();
            foreach(float x in new[]{-1.65f,1.65f})foreach(float z in new[]{-1f,1f})
                Rod(m,new Vector3(x,.03f,z),new Vector3(x,2.7f,0),.075f,TownColor.Mint);
            Rod(m,new Vector3(-1.8f,2.7f,0),new Vector3(1.8f,2.7f,0),.09f,TownColor.Metal);
            foreach(float x in new[]{-.85f,-.15f})
            {
                Rod(m,new Vector3(x,2.6f,0),new Vector3(x,.64f,.14f),.013f,TownColor.Metal);
                for(float y=.75f;y<2.5f;y+=.17f)m.Ellipsoid(new Vector3(x,y,.14f*(2.6f-y)/2),new Vector3(.025f,.04f,.02f),TownColor.Rubber,5,3);
            }
            m.Box(new Vector3(-.5f,.61f,.14f),new Vector3(.84f,.09f,.4f),TownColor.Wood);
            Rod(m,new Vector3(.65f,2.6f,0),new Vector3(.77f,1.25f,.15f),.015f,TownColor.Metal);
            Rod(m,new Vector3(1.3f,2.6f,0),new Vector3(1.35f,1.7f,-.06f),.015f,TownColor.Metal);
            return m;
        }
        private static TownMeshBuilder Sandbox()
        {
            var m=new TownMeshBuilder();m.Ground(0,0,4.7f,3.4f,.025f,TownColor.Sand);
            foreach(float x in new[]{-2.4f,2.4f})m.Box(new Vector3(x,.18f,0),new Vector3(.15f,.32f,3.6f),TownColor.Wood);
            foreach(float z in new[]{-1.75f,1.75f})m.Box(new Vector3(0,.18f,z),new Vector3(4.9f,.32f,.15f),TownColor.WoodLight);
            foreach(float x in new[]{-2.3f,2.3f})foreach(float z in new[]{-1.65f,1.65f})m.Box(new Vector3(x,.38f,z),new Vector3(.65f,.08f,.65f),TownColor.Mint);
            m.Cylinder(new Vector3(.75f,.1f,.6f),.13f,.18f,.16f,TownColor.Red);return m;
        }
        private static void Ring(TownMeshBuilder m,Vector3 center,float radius,float tube,TownColor color,Quaternion pose,int sides=16)
        {
            Vector3 P(int i)=>center+pose*new Vector3(Mathf.Cos(i*2*Mathf.PI/sides)*radius,Mathf.Sin(i*2*Mathf.PI/sides)*radius,0);
            for(int i=0;i<sides;i++)Rod(m,P(i),P(i+1),tube,color,5);
        }
        private static void BikeWheel(TownMeshBuilder m,Vector3 center,Quaternion pose)
        {
            Ring(m,center,.38f,.045f,TownColor.Rubber,pose);Ring(m,center,.33f,.014f,TownColor.Metal,pose);
            for(int i=0;i<10;i++)Rod(m,center,center+pose*new Vector3(Mathf.Cos(i*Mathf.PI/5)*.32f,Mathf.Sin(i*Mathf.PI/5)*.32f,0),.009f,TownColor.Metal,4);
        }
        private static TownMeshBuilder Bicycle()
        {
            var m=new TownMeshBuilder();var tilt=Quaternion.Euler(77,0,-8);var baseP=new Vector3(0,.18f,0);
            Vector3 P(float x,float y,float z=0)=>baseP+tilt*new Vector3(x,y,z);
            BikeWheel(m,P(-.65f,0),tilt); // Front wheel is a separate prop next to the bent fork.
            var a=P(-.65f,0);var b=P(-.32f,.6f);var c=P(0,0);var d=P(.55f,.56f);var e=P(.75f,0);
            foreach(var edge in new[]{(a,b),(b,c),(c,a),(b,d),(d,c),(d,e)})Rod(m,edge.Item1,edge.Item2,.028f,TownColor.Blue);
            Rod(m,b,P(-.37f,.76f),.025f,TownColor.Metal);m.Box(P(-.4f,.8f),new Vector3(.35f,.07f,.2f),TownColor.Rubber,tilt);
            Rod(m,d,P(.5f,.8f),.026f,TownColor.Metal);Rod(m,P(.5f,.8f,-.23f),P(.57f,.8f,.23f),.025f,TownColor.Metal);
            Ring(m,c,.1f,.014f,TownColor.Metal,tilt);Rod(m,P(0,0,-.14f),P(0,0,.14f),.02f,TownColor.Metal);return m;
        }
        private static TownMeshBuilder Wheel(){var m=new TownMeshBuilder();BikeWheel(m,new Vector3(0,.065f,0),Quaternion.Euler(90,0,0));return m;}
        private static TownMeshBuilder Tyre(){var m=new TownMeshBuilder();Ring(m,new Vector3(0,.15f,0),.4f,.13f,TownColor.Rubber,Quaternion.Euler(90,0,0));return m;}
        private static TownMeshBuilder Toilet()
        {
            var m=new TownMeshBuilder();m.Box(new Vector3(0,.11f,.08f),new Vector3(.46f,.22f,.6f),TownColor.Cream);
            m.Cylinder(new Vector3(0,.35f,.08f),.21f,.31f,.38f,TownColor.White,10);
            // A real open bowl with an inner wall and dark well, not a solid white block.
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;
                Vector3 P(float t,float r,float y)=>new Vector3(Mathf.Cos(t)*r,y,Mathf.Sin(t)*r*1.22f-.12f);
                m.Quad(P(a,.34f,.56f),P(a,.23f,.56f),P(b,.23f,.56f),P(b,.34f,.56f),TownColor.White);
                m.Quad(P(b,.13f,.34f),P(b,.23f,.56f),P(a,.23f,.56f),P(a,.13f,.34f),TownColor.Cream);
                m.Quad(P(a,.22f,.27f),P(a,.34f,.56f),P(b,.34f,.56f),P(b,.22f,.27f),TownColor.White);
            }
            m.Cylinder(new Vector3(0,.33f,-.12f),.14f,.14f,.02f,TownColor.Soil);
            m.Box(new Vector3(0,.65f,.38f),new Vector3(.58f,.62f,.23f),TownColor.Cream);
            m.Box(new Vector3(-.08f,.98f,.36f),new Vector3(.37f,.055f,.26f),TownColor.White,Quaternion.Euler(0,7,2));
            m.Box(new Vector3(.19f,.78f,.245f),new Vector3(.12f,.035f,.03f),TownColor.Metal);return m;
        }
        private static TownMeshBuilder Pallet()
        {
            var m=new TownMeshBuilder();for(int i=0;i<5;i++)m.Box(new Vector3((i-2)*.27f,.2f,0),new Vector3(.22f,.075f,1.2f),TownColor.WoodLight);
            foreach(float z in new[]{-.45f,0,.45f})m.Box(new Vector3(0,.1f,z),new Vector3(1.4f,.13f,.16f),TownColor.Wood);return m;
        }
        private static TownMeshBuilder Crate()
        {
            var m=new TownMeshBuilder();foreach(float x in new[]{-.43f,.43f})foreach(float z in new[]{-.32f,.32f})m.Box(new Vector3(x,.25f,z),new Vector3(.06f,.5f,.06f),TownColor.Wood);
            for(int i=0;i<3;i++)foreach(float z in new[]{-.32f,.32f})m.Box(new Vector3(0,.12f+i*.15f,z),new Vector3(.92f,.1f,.05f),TownColor.WoodLight);
            for(int i=0;i<3;i++)m.Box(new Vector3(-.43f,.12f+i*.15f,0),new Vector3(.05f,.1f,.65f),TownColor.WoodLight);
            m.Box(new Vector3(.55f,.04f,-.05f),new Vector3(.1f,.04f,.65f),TownColor.WoodLight,Quaternion.Euler(0,15,0));return m;
        }
        private static TownMeshBuilder WateringCan()
        {
            var m=new TownMeshBuilder();m.Cylinder(new Vector3(0,.25f,0),.22f,.19f,.45f,TownColor.Mint);
            Rod(m,new Vector3(.14f,.15f,0),new Vector3(.55f,.5f,0),.055f,TownColor.Metal);
            m.Cylinder(new Vector3(.57f,.52f,0),.11f,.11f,.035f,TownColor.Metal,8,Quaternion.Euler(0,0,-50));
            Ring(m,new Vector3(-.13f,.3f,0),.23f,.025f,TownColor.Metal,Quaternion.identity,12);return m;
        }
        private static TownMeshBuilder Bucket()
        {
            var m=new TownMeshBuilder();m.Cylinder(new Vector3(0,.18f,0),.17f,.24f,.34f,TownColor.Metal);
            m.Cylinder(new Vector3(0,.355f,0),.205f,.205f,.012f,TownColor.Soil);
            Ring(m,new Vector3(0,.37f,0),.235f,.017f,TownColor.Metal,Quaternion.Euler(90,0,0));
            return m;
        }
        private static TownMeshBuilder Boards()
        {
            var m=new TownMeshBuilder();for(int i=0;i<4;i++)m.Box(new Vector3(i*.18f,.04f+i*.04f,i*.15f),new Vector3(.16f,.055f,1.5f-i*.16f),i%2==0?TownColor.Wood:TownColor.WoodLight,Quaternion.Euler(0,i*17,0));return m;
        }
        private static TownMeshBuilder Stump()
        {
            var m=new TownMeshBuilder();m.Cylinder(new Vector3(0,.22f,0),.48f,.33f,.44f,TownColor.Trunk,9);m.Cylinder(new Vector3(0,.447f,0),.29f,.29f,.018f,TownColor.WoodLight,9);
            for(int i=0;i<4;i++){float a=i*Mathf.PI/2;Rod(m,new Vector3(0,.09f,0),new Vector3(Mathf.Cos(a)*.65f,.03f,Mathf.Sin(a)*.65f),.09f,TownColor.Trunk);}return m;
        }
        private static TownMeshBuilder Branch()
        {
            var m=new TownMeshBuilder();Rod(m,new Vector3(-.9f,.08f,-.2f),new Vector3(1,.12f,.25f),.08f,TownColor.Trunk);
            Rod(m,new Vector3(.1f,.1f,.04f),new Vector3(.65f,.14f,.8f),.045f,TownColor.Trunk);return m;
        }
        private static TownMeshBuilder Weeds()
        {
            var m=new TownMeshBuilder();var random=new System.Random(83);
            for(int i=0;i<12;i++)
            {
                float x=(float)random.NextDouble()*.8f-.4f,z=(float)random.NextDouble()*.8f-.4f,h=.25f+(float)random.NextDouble()*.45f;
                var bottom=new Vector3(x,.01f,z);var top=bottom+new Vector3(.07f,h,.03f);var c=i%3==0?TownColor.LeafLight:TownColor.Leaf;
                m.Triangle(bottom-Vector3.right*.055f,bottom+Vector3.right*.055f,top,c);m.Triangle(bottom+Vector3.right*.055f,bottom-Vector3.right*.055f,top,c);
                m.Triangle(bottom-Vector3.forward*.05f,top,bottom+Vector3.forward*.05f,c);m.Triangle(bottom+Vector3.forward*.05f,top,bottom-Vector3.forward*.05f,c);
            }
            return m;
        }
        private static TownMeshBuilder Shrub()
        {
            var m=new TownMeshBuilder();m.Ellipsoid(new Vector3(-.35f,.38f,0),new Vector3(.6f,.4f,.55f),TownColor.LeafDark,7,3);
            m.Ellipsoid(new Vector3(.25f,.46f,.1f),new Vector3(.7f,.48f,.6f),TownColor.Leaf,7,3);return m;
        }
        private static TownMeshBuilder Tree(bool wide)
        {
            var m=new TownMeshBuilder();m.Cylinder(new Vector3(0,1.4f,0),.2f,.1f,2.8f,wide?TownColor.Trunk:TownColor.Cream);
            foreach(float x in new[]{-.7f,.8f})Rod(m,new Vector3(0,1.6f,0),new Vector3(x,2.9f,.1f),.09f,TownColor.Trunk);
            m.Ellipsoid(new Vector3(0,3.7f,0),wide?new Vector3(2.3f,1.8f,2):new Vector3(1.5f,2.2f,1.4f),TownColor.Leaf,9,4);
            m.Ellipsoid(new Vector3(-.85f,3.2f,.6f),new Vector3(1.3f,1.3f,1.1f),TownColor.LeafLight,7,3);
            m.Ellipsoid(new Vector3(.9f,3.1f,-.5f),new Vector3(1.2f,1.2f,1.1f),TownColor.LeafDark,7,3);return m;
        }
        private static TownMeshBuilder Noticeboard()
        {
            var m=new TownMeshBuilder();foreach(float x in new[]{-.65f,.65f})m.Box(new Vector3(x,.85f,0),new Vector3(.1f,1.7f,.1f),TownColor.Wood);
            m.Box(new Vector3(0,1.38f,0),new Vector3(1.65f,.92f,.12f),TownColor.Wood);
            foreach(var p in new[]{new Vector3(-.45f,1.5f,-.07f),new Vector3(.15f,1.3f,-.07f),new Vector3(.4f,1.62f,-.07f)})m.Box(p,new Vector3(.36f,.32f,.015f),TownColor.Cream,Quaternion.Euler(0,0,p.x*15));return m;
        }
        private static TownMeshBuilder Fence()
        {
            var m=new TownMeshBuilder();foreach(float x in new[]{-1.4f,1.4f})m.Box(new Vector3(x,.63f,0),new Vector3(.12f,1.25f,.12f),TownColor.Wood,Quaternion.Euler(0,0,x*3));
            m.Box(new Vector3(0,.8f,0),new Vector3(2.9f,.12f,.08f),TownColor.WoodLight,Quaternion.Euler(0,0,-3));
            m.Box(new Vector3(-.4f,.38f,0),new Vector3(2.2f,.12f,.08f),TownColor.Wood,Quaternion.Euler(0,0,7));return m;
        }
    }
}
