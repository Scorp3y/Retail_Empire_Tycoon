using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>Distinct silhouettes and close-range storytelling details for the old neighbourhood yard.</summary>
    internal static class TownYardStoryModels
    {
        // English modelling brief: A lived-in, lightly neglected European neighbourhood yard.
        // Hand-built timber, chipped painted metal, real openings and damaged construction, not ruins.
        // Create readable focal structures surrounded by coherent small objects. No pristine park furniture.
        // The shed is a small disused timber tool hut with an incomplete roof, open doorway and broken glazing.
        internal static Dictionary<string,TownMeshBuilder> Create()=>new()
        {
            {"PlayFort",PlayFort()}, {"ClimbingDome",ClimbingDome()}, {"SpringRider",SpringRider()},
            {"BasketballHoop",BasketballHoop()}, {"BallAndShoes",BallAndShoes()},
            {"CrookedPergola",Pergola()}, {"ToolShed",ToolShed()}, {"RepairCanopy",Canopy()},
            {"AbandonedEstateCar",EstateCar()}, {"GardenTrailer",Trailer()},
            {"BrokenGreenhouse",Greenhouse()}, {"RaisedBed",RaisedBed()}, {"OvergrownBathtub",Bathtub()},
            {"Wheelbarrow",Wheelbarrow()}, {"HandPump",HandPump()}, {"WaterTank",WaterTank()},
            {"DryFountain",DryFountain()}, {"BrickWallRemnant",BrickWall()}, {"ChainlinkRemnant",Chainlink()},
            {"Workbench",Workbench()}, {"CableSpool",CableSpool()}, {"RustBarrel",Barrel()},
            {"ClayPots",Pots()}, {"PaintAndBrushes",Paint()}, {"Toolbox",Toolbox()},
            {"CoffeeLeftovers",Coffee()}, {"FoldedTarp",Tarp()}, {"PaverStack",Pavers()},
            {"FallenGate",Gate()}, {"LeafPile",Leaves()}, {"WildflowerClump",Flowers()},
            {"Reeds",Reeds()}, {"ElderTree",ElderTree()}, {"LeaningPine",Pine()},
            {"OldAppleTree",AppleTree()}, {"Woodpile",Woodpile()}, {"LowRetainingBricks",LowBricks()}
        };

        private static void Rod(TownMeshBuilder m,Vector3 a,Vector3 b,float radius,TownColor color,int sides=6)
            =>m.Cylinder((a+b)*.5f,radius,radius,(b-a).magnitude,color,sides,Quaternion.FromToRotation(Vector3.up,b-a));
        private static void Ring(TownMeshBuilder m,Vector3 center,float radius,float tube,TownColor color,Quaternion rotation,int segments=16)
        {
            Vector3 P(int i)=>center+rotation*new Vector3(Mathf.Cos(i*Mathf.PI*2/segments)*radius,0,Mathf.Sin(i*Mathf.PI*2/segments)*radius);
            for(int i=0;i<segments;i++)Rod(m,P(i),P(i+1),tube,color,5);
        }
        private static void Plank(TownMeshBuilder m,Vector3 center,Vector3 size,int index,Quaternion rotation=default)
        {
            m.Box(center,size,index%4==0?TownColor.Mint:index%3==0?TownColor.Wood:TownColor.WoodLight,rotation);
            if(size.x>1)
                foreach(float side in new[]{-1f,1f})m.Cylinder(center+new Vector3(side*(size.x*.5f-.12f),size.y*.5f+.004f,0),.016f,.016f,.009f,TownColor.Metal,5);
        }
        private static TownMeshBuilder PlayFort()
        {
            var m=new TownMeshBuilder();
            foreach(float x in new[]{-1.1f,1.1f})foreach(float z in new[]{-.85f,.85f})
            {
                m.Box(new Vector3(x,1.6f,z),new Vector3(.15f,3.2f,.15f),TownColor.Wood);
                m.Box(new Vector3(x,3.23f,z),new Vector3(.23f,.13f,.23f),TownColor.Orange);
            }
            for(int i=0;i<8;i++)Plank(m,new Vector3(0,1.65f,-.82f+i*.23f),new Vector3(2.5f,.12f,.2f),i);
            foreach(float side in new[]{-1f,1f})
            {
                Rod(m,new Vector3(side*1.12f,2.45f,-.85f),new Vector3(side*1.12f,2.45f,.85f),.06f,TownColor.Red);
                for(int i=0;i<6;i++)if(!(side>0&&i==2))m.Box(new Vector3(side*1.12f,2,-.65f+i*.26f),new Vector3(.07f,.6f,.12f),i%2==0?TownColor.Mint:TownColor.Orange);
            }
            for(int i=0;i<7;i++)
            {
                if(i==4)continue;
                float z=-1.05f+i*.34f;
                foreach(float side in new[]{-1f,1f})
                    m.Box(new Vector3(side*.66f,3.38f,z),new Vector3(1.5f,.095f,.31f),i%2==0?TownColor.Red:TownColor.Orange,Quaternion.Euler(0,0,-side*24));
            }
            foreach(float x in new[]{-.53f,.53f})Rod(m,new Vector3(x,.08f,-2.75f),new Vector3(x,1.8f,-.9f),.065f,TownColor.Mint);
            for(int i=0;i<6;i++)Rod(m,new Vector3(-.53f,.25f+i*.27f,-2.6f+i*.29f),new Vector3(.53f,.25f+i*.27f,-2.6f+i*.29f),.045f,TownColor.Metal);
            var top=new Vector3(0,1.65f,1);var bottom=new Vector3(0,.12f,4.5f);
            m.Quad(top+Vector3.left*.48f,bottom+Vector3.left*.48f,bottom+Vector3.right*.48f,top+Vector3.right*.48f,TownColor.Blue);
            foreach(float side in new[]{-1f,1f})Rod(m,top+new Vector3(side*.53f,.1f,0),bottom+new Vector3(side*.53f,.1f,0),.075f,TownColor.Mint);
            for(int i=0;i<3;i++)m.Box(new Vector3(-1.45f,.06f,1.8f+i*.3f),new Vector3(.12f,.08f,1.1f),TownColor.WoodLight,Quaternion.Euler(0,i*15,0));
            return m;
        }
        private static TownMeshBuilder ClimbingDome()
        {
            var m=new TownMeshBuilder();
            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI/5,b=(i+1)*Mathf.PI/5;
                var baseA=new Vector3(Mathf.Cos(a)*2,.08f,Mathf.Sin(a)*2);
                var baseB=new Vector3(Mathf.Cos(b)*2,.08f,Mathf.Sin(b)*2);
                var upper=new Vector3(Mathf.Cos(a+.3f)*1.45f,1.28f,Mathf.Sin(a+.3f)*1.45f);
                Rod(m,baseA,upper,.042f,i%2==0?TownColor.Blue:TownColor.Red);Rod(m,baseB,upper,.042f,TownColor.Metal);
                Rod(m,upper,new Vector3(0,2.12f,0),.042f,TownColor.Mint);
                if(i!=3)Rod(m,baseA,baseB,.04f,TownColor.Metal);
            }
            return m;
        }
        private static TownMeshBuilder SpringRider()
        {
            var m=new TownMeshBuilder();
            for(int i=0;i<5;i++)Ring(m,new Vector3(0,.14f+i*.09f,0),.16f,.035f,TownColor.Metal,Quaternion.identity,10);
            m.Ellipsoid(new Vector3(0,.7f,0),new Vector3(.65f,.23f,.3f),TownColor.Orange,8,3);
            m.Ellipsoid(new Vector3(.48f,.99f,0),new Vector3(.2f,.33f,.2f),TownColor.Orange,7,3);
            m.Box(new Vector3(.68f,1.1f,0),new Vector3(.23f,.12f,.23f),TownColor.Red);
            m.Box(new Vector3(-.1f,.96f,0),new Vector3(.45f,.06f,.33f),TownColor.Wood);
            Rod(m,new Vector3(.32f,1.1f,-.35f),new Vector3(.32f,1.1f,.35f),.035f,TownColor.Metal);return m;
        }
        private static TownMeshBuilder BasketballHoop()
        {
            var m=new TownMeshBuilder();
            m.Box(new Vector3(0,.12f,.3f),new Vector3(.9f,.24f,1.15f),TownColor.Stone);
            Rod(m,new Vector3(0,.15f,.3f),new Vector3(.14f,3.8f,0),.09f,TownColor.Metal);
            Rod(m,new Vector3(.14f,3.6f,0),new Vector3(.12f,3.8f,-.5f),.055f,TownColor.Red);
            m.Box(new Vector3(.12f,3.85f,-.59f),new Vector3(2.2f,1.4f,.12f),TownColor.WoodLight,Quaternion.Euler(0,0,4));
            m.Box(new Vector3(.12f,3.88f,-.67f),new Vector3(1.94f,1.17f,.025f),TownColor.Cream,Quaternion.Euler(0,0,4));
            foreach(float x in new[]{-.32f,.56f})m.Box(new Vector3(x,3.65f,-.69f),new Vector3(.05f,.55f,.025f),TownColor.Red);
            m.Box(new Vector3(.12f,3.93f,-.69f),new Vector3(.92f,.05f,.025f),TownColor.Red);
            Ring(m,new Vector3(.12f,3.37f,-1.05f),.38f,.035f,TownColor.Red,Quaternion.Euler(0,0,6));
            for(int i=0;i<9;i++)
            {
                float a=i*Mathf.PI*2/9;var start=new Vector3(.12f+Mathf.Cos(a)*.36f,3.37f,-1.05f+Mathf.Sin(a)*.36f);
                Rod(m,start,start+new Vector3(.02f,-(i%3==0?.25f:.58f),.06f),.009f,TownColor.Cream,4);
            }
            return m;
        }
        private static TownMeshBuilder BallAndShoes()
        {
            var m=new TownMeshBuilder();m.Ellipsoid(new Vector3(0,.17f,0),Vector3.one*.17f,TownColor.Orange,10,5);
            Ring(m,new Vector3(0,.17f,0),.172f,.008f,TownColor.Rubber,Quaternion.Euler(90,0,0));
            foreach(float x in new[]{.35f,.56f})m.Ellipsoid(new Vector3(x,.055f,.18f),new Vector3(.08f,.055f,.19f),TownColor.Blue,7,3);return m;
        }
        private static TownMeshBuilder Pergola()
        {
            var m=new TownMeshBuilder();foreach(float x in new[]{-2.7f,2.7f})foreach(float z in new[]{-1.8f,1.8f})
                m.Box(new Vector3(x,1.5f,z),new Vector3(.17f,3,.17f),TownColor.Wood,Quaternion.Euler(0,0,x<0?2:-1));
            foreach(float z in new[]{-1.8f,1.8f})m.Box(new Vector3(0,3,z),new Vector3(5.9f,.2f,.17f),TownColor.Wood);
            for(int i=0;i<11;i++)if(i!=3&&i!=8)m.Box(new Vector3(-2.8f+i*.56f,3.16f,0),new Vector3(.25f,.12f,4.2f),i%3==0?TownColor.Mint:TownColor.WoodLight,Quaternion.Euler(0,i==5?8:0,i==5?3:0));
            foreach(float x in new[]{-2.7f,2.7f})Rod(m,new Vector3(x,2.8f,-1.8f),new Vector3(x+(x<0?.8f:-.8f),2.05f,-1.8f),.055f,TownColor.Wood);
            for(int i=0;i<12;i++)m.Ellipsoid(new Vector3(-2.65f+i*.18f,2.65f+Mathf.Sin(i)*.22f,1.8f),new Vector3(.42f,.32f,.36f),i%3==0?TownColor.LeafLight:TownColor.LeafDark,6,3);return m;
        }
        private static TownMeshBuilder ToolShed()
        {
            var m=new TownMeshBuilder();m.Box(new Vector3(0,.12f,0),new Vector3(5.4f,.24f,4.5f),TownColor.Stone);
            for(int i=0;i<22;i++)
            {
                float x=-2.45f+i*.23f;
                if(x<-.72f||x>.72f)m.Box(new Vector3(x,1.32f,2.05f),new Vector3(.205f,2.4f,.11f),i%4==0?TownColor.Wood:TownColor.Mint);
                m.Box(new Vector3(x,1.32f,-2.05f),new Vector3(.205f,2.4f,.11f),i%4==1?TownColor.WoodLight:TownColor.Mint);
            }
            foreach(float x in new[]{-2.5f,2.5f})
            {
                m.Box(new Vector3(x,.67f,0),new Vector3(.12f,1.1f,4.15f),TownColor.Mint);
                m.Box(new Vector3(x,2.19f,0),new Vector3(.12f,.6f,4.15f),TownColor.Wood);
                foreach(float z in new[]{-1.95f,-.68f,.68f,1.95f})m.Box(new Vector3(x,1.47f,z),new Vector3(.15f,.8f,.13f),TownColor.WoodLight);
                if(x<0)m.Box(new Vector3(x,1.5f,-1.35f),new Vector3(.03f,.65f,1.05f),TownColor.Glass);
            }
            // One door hangs open; the other side of the opening is clear, not a dark painted cube.
            m.Box(new Vector3(-1.05f,1.17f,2.62f),new Vector3(1.25f,2.15f,.1f),TownColor.WoodLight,Quaternion.Euler(0,58,4));
            m.Box(new Vector3(-1.1f,1.15f,2.58f),new Vector3(1.2f,.09f,.13f),TownColor.Wood,Quaternion.Euler(0,58,4));
            for(int i=0;i<10;i++)
            {
                if(i==6||i==7)continue;float z=-2.35f+i*.51f;
                foreach(float side in new[]{-1f,1f})m.Box(new Vector3(side*1.33f,2.79f,z),new Vector3(2.95f,.1f,.47f),i%3==0?TownColor.Metal:TownColor.RoofDark,Quaternion.Euler(0,0,-side*15));
            }
            foreach(float z in new[]{-2f,0,2f})Rod(m,new Vector3(-2.6f,2.45f,z),new Vector3(0,3.15f,z),.065f,TownColor.Wood);
            m.Box(new Vector3(1.4f,1.65f,2.13f),new Vector3(1.1f,.5f,.08f),TownColor.Cream);return m;
        }
        private static TownMeshBuilder Canopy()
        {
            var m=new TownMeshBuilder();foreach(float x in new[]{-2.8f,2.8f})foreach(float z in new[]{-3.15f,3.15f})Rod(m,new Vector3(x,.03f,z),new Vector3(x,3.05f,z),.075f,TownColor.Metal);
            foreach(float z in new[]{-3.2f,0,3.2f})Rod(m,new Vector3(-2.95f,3.03f,z),new Vector3(2.95f,3.03f,z),.06f,TownColor.Metal);
            for(int i=0;i<11;i++)if(i!=8)m.Box(new Vector3(-2.8f+i*.56f,3.14f,0),new Vector3(.52f,.08f,6.9f),i%4==0?TownColor.Red:TownColor.Metal,Quaternion.Euler(0,0,i==7?3:0));
            return m;
        }
        private static TownMeshBuilder EstateCar()
        {
            var m=new TownMeshBuilder();m.Box(new Vector3(0,.48f,0),new Vector3(1.86f,.45f,4.15f),TownColor.Blue);
            m.Box(new Vector3(0,.75f,.58f),new Vector3(1.8f,.22f,2.92f),TownColor.Mint);
            m.ExtrudeX(new Vector3(0,0,0),1.7f,new[]{new Vector2(.83f,-1.52f),new Vector2(1.55f,-1.2f),new Vector2(1.6f,.6f),new Vector2(.83f,1.35f)},TownColor.Blue);
            foreach(float x in new[]{-.86f,.86f})
            {
                m.Box(new Vector3(x,1.22f,-.18f),new Vector3(.02f,.52f,1.95f),TownColor.Glass);
                foreach(float z in new[]{-.9f,.2f,.85f})m.Box(new Vector3(x*1.01f,1.22f,z),new Vector3(.04f,.59f,.055f),TownColor.Frame);
                foreach(float z in new[]{-1.3f,1.3f})
                {
                    var center=new Vector3(x*1.04f,.37f,z);m.Cylinder(center,.34f,.34f,.19f,TownColor.Rubber,12,Quaternion.Euler(0,0,90));
                    m.Cylinder(center+Vector3.right*Mathf.Sign(x)*.105f,.18f,.18f,.025f,TownColor.Metal,8,Quaternion.Euler(0,0,90));
                }
                m.Box(new Vector3(x*1.12f,1.14f,.97f),new Vector3(.2f,.14f,.25f),TownColor.Frame);
                m.Box(new Vector3(x*1.015f,.83f,-.25f),new Vector3(.04f,.04f,.22f),TownColor.Metal);
                for(int i=0;i<7;i++)m.Box(new Vector3(x*1.02f,.55f,-1.7f+i*.5f),new Vector3(.025f,.09f,.14f),TownColor.Red);
            }
            m.Box(new Vector3(0,1.16f,1.02f),new Vector3(1.56f,.68f,.035f),TownColor.Glass,Quaternion.Euler(46,0,0));
            m.Box(new Vector3(0,1.22f,-1.38f),new Vector3(1.55f,.52f,.035f),TownColor.Glass,Quaternion.Euler(-24,0,0));
            m.Box(new Vector3(0,1.26f,1.65f),new Vector3(1.75f,.08f,1.45f),TownColor.Mint,Quaternion.Euler(-55,0,0));
            m.Box(new Vector3(0,.64f,2.1f),new Vector3(.8f,.23f,.045f),TownColor.Rubber);
            foreach(float x in new[]{-.67f,.67f})m.Box(new Vector3(x,.69f,2.11f),new Vector3(.42f,.23f,.045f),TownColor.Cream);
            foreach(float z in new[]{-2.14f,2.14f})m.Box(new Vector3(0,.38f,z),new Vector3(1.95f,.14f,.09f),TownColor.Metal);
            m.Box(new Vector3(0,.31f,-1.7f),new Vector3(.45f,.4f,.65f),TownColor.Brick);return m;
        }
        private static TownMeshBuilder Trailer()
        {
            var m=new TownMeshBuilder();m.Box(new Vector3(0,.75f,0),new Vector3(2.1f,.18f,3.25f),TownColor.Metal);
            for(int i=0;i<9;i++)Plank(m,new Vector3(0,.88f,-1.4f+i*.35f),new Vector3(2,.09f,.31f),i);
            foreach(float x in new[]{-1.05f,1.05f})
            {
                m.Cylinder(new Vector3(x,.47f,0),.43f,.43f,.24f,TownColor.Rubber,12,Quaternion.Euler(0,0,90));
                m.Cylinder(new Vector3(x*1.13f,.47f,0),.22f,.22f,.025f,TownColor.Metal,8,Quaternion.Euler(0,0,90));
                for(int j=0;j<3;j++)m.Box(new Vector3(x,1.02f+j*.21f,0),new Vector3(.08f,.16f,3.2f),j%2==0?TownColor.WoodLight:TownColor.Mint);
            }
            Rod(m,new Vector3(-.9f,.7f,-1.5f),new Vector3(0,.57f,-3f),.065f,TownColor.Metal);Rod(m,new Vector3(.9f,.7f,-1.5f),new Vector3(0,.57f,-3f),.065f,TownColor.Metal);
            m.Box(new Vector3(0,.36f,-2.6f),new Vector3(.08f,.7f,.08f),TownColor.Metal);return m;
        }
        private static TownMeshBuilder Greenhouse()
        {
            var m=new TownMeshBuilder();foreach(float x in new[]{-2.3f,2.3f})foreach(float z in new[]{-3f,-1.5f,0,1.5f,3f})
            {
                Rod(m,new Vector3(x,.06f,z),new Vector3(x,2.05f,z),.035f,TownColor.Metal);
                Rod(m,new Vector3(x,2.05f,z),new Vector3(0,3.05f,z),.035f,TownColor.Metal);
            }
            foreach(float x in new[]{-2.3f,0,2.3f})Rod(m,new Vector3(x,x==0?3.05f:2.05f,-3),new Vector3(x,x==0?3.05f:2.05f,3),.04f,TownColor.Mint);
            foreach(float x in new[]{-2.3f,2.3f})
            {
                m.Box(new Vector3(x,.18f,0),new Vector3(.23f,.36f,6.3f),TownColor.Brick);
                for(int i=0;i<4;i++)if((i+(x>0?1:0))%3==0)m.Box(new Vector3(x,1.05f,-2.3f+i*1.5f),new Vector3(.025f,1.6f,1.4f),TownColor.Glass);
            }
            foreach(float z in new[]{-3f,3f})m.Box(new Vector3(0,.18f,z),new Vector3(4.8f,.36f,.23f),TownColor.Stone);
            for(int i=0;i<9;i++)m.Ellipsoid(new Vector3(-1.8f+i*.4f,.5f,2.8f),new Vector3(.45f,.55f,.4f),i%2==0?TownColor.Leaf:TownColor.LeafDark,6,3);return m;
        }
        private static TownMeshBuilder RaisedBed()
        {
            var m=new TownMeshBuilder();m.Box(new Vector3(0,.16f,0),new Vector3(4,.26f,1.65f),TownColor.Soil);
            foreach(float z in new[]{-.88f,.88f})Plank(m,new Vector3(0,.28f,z),new Vector3(4.25f,.3f,.1f),1);
            foreach(float x in new[]{-2.06f,2.06f})m.Box(new Vector3(x,.28f,0),new Vector3(.1f,.3f,1.85f),TownColor.Wood);
            for(int i=0;i<8;i++){float x=-1.7f+i*.47f;m.Ellipsoid(new Vector3(x,.46f,i%2==0?-.25f:.3f),new Vector3(.28f,.25f,.31f),i%3==0?TownColor.LeafLight:TownColor.Leaf,6,3);}
            foreach(float x in new[]{-1.3f,.4f,1.5f})Rod(m,new Vector3(x,.3f,.5f),new Vector3(x,1.38f,.5f),.025f,TownColor.WoodLight);return m;
        }
        private static TownMeshBuilder Bathtub()
        {
            var m=new TownMeshBuilder();m.Box(new Vector3(0,.28f,0),new Vector3(1.05f,.5f,2.1f),TownColor.Cream);
            m.Box(new Vector3(0,.54f,0),new Vector3(.85f,.025f,1.84f),TownColor.Soil);
            foreach(float x in new[]{-.54f,.54f})m.Box(new Vector3(x,.6f,0),new Vector3(.12f,.15f,2.16f),TownColor.White);
            foreach(float z in new[]{-1.05f,1.05f})m.Box(new Vector3(0,.6f,z),new Vector3(1.17f,.15f,.12f),TownColor.White);
            foreach(float x in new[]{-.35f,.35f})foreach(float z in new[]{-.72f,.72f})m.Box(new Vector3(x,.1f,z),new Vector3(.12f,.2f,.17f),TownColor.Metal);
            for(int i=0;i<7;i++)m.Ellipsoid(new Vector3(Mathf.Sin(i)*.3f,.74f,-.75f+i*.23f),new Vector3(.25f,.3f,.25f),TownColor.Leaf,6,3);return m;
        }
        private static TownMeshBuilder Wheelbarrow()
        {
            var m=new TownMeshBuilder();m.Cylinder(new Vector3(0,.32f,.72f),.32f,.32f,.18f,TownColor.Rubber,12,Quaternion.Euler(0,0,90));
            m.Box(new Vector3(0,.72f,0),new Vector3(.75f,.1f,1.15f),TownColor.Metal);
            foreach(float x in new[]{-.4f,.4f})
            {
                m.Box(new Vector3(x,.92f,0),new Vector3(.07f,.4f,1.2f),TownColor.Mint,Quaternion.Euler(0,0,-x*20));
                Rod(m,new Vector3(x,.65f,.5f),new Vector3(x,.95f,-1.4f),.035f,TownColor.Metal);
                Rod(m,new Vector3(x,.6f,-.5f),new Vector3(x,.08f,-.6f),.035f,TownColor.Metal);
            }
            m.Box(new Vector3(0,.9f,.6f),new Vector3(.85f,.35f,.07f),TownColor.Mint);m.Ellipsoid(new Vector3(.1f,.85f,.15f),new Vector3(.3f,.16f,.4f),TownColor.Soil,7,3);return m;
        }
        private static TownMeshBuilder HandPump()
        {
            var m=new TownMeshBuilder();m.Box(new Vector3(0,.08f,0),new Vector3(1.1f,.16f,1.1f),TownColor.Stone);
            m.Cylinder(new Vector3(0,.65f,0),.2f,.16f,1.1f,TownColor.Mint);m.Box(new Vector3(0,1.24f,0),new Vector3(.4f,.17f,.35f),TownColor.Mint);
            Rod(m,new Vector3(0,1.25f,-.12f),new Vector3(0,1.75f,-.7f),.045f,TownColor.Metal);
            Rod(m,new Vector3(0,.94f,.06f),new Vector3(0,.88f,.6f),.08f,TownColor.Mint);
            Rod(m,new Vector3(0,.88f,.6f),new Vector3(0,.7f,.6f),.08f,TownColor.Mint);return m;
        }
        private static TownMeshBuilder WaterTank()
        {
            var m=new TownMeshBuilder();foreach(float x in new[]{-1.1f,1.1f})foreach(float z in new[]{-.9f,.9f})
            {Rod(m,new Vector3(x,.08f,z),new Vector3(x,2.5f,z),.075f,TownColor.Metal);Rod(m,new Vector3(x,.3f,z),new Vector3(-x,2.25f,z),.035f,TownColor.Metal);}
            m.Cylinder(new Vector3(0,3.05f,0),1.5f,1.5f,1.8f,TownColor.Red,14);
            foreach(float y in new[]{2.2f,3.05f,3.9f})Ring(m,new Vector3(0,y,0),1.515f,.035f,TownColor.Metal,Quaternion.identity,14);
            m.Cylinder(new Vector3(0,3.97f,0),1.5f,.3f,.14f,TownColor.Metal,14);
            for(int i=0;i<10;i++)Rod(m,new Vector3(-1.55f,.25f+i*.36f,-.35f),new Vector3(-1.55f,.25f+i*.36f,.35f),.025f,TownColor.Metal);
            foreach(float z in new[]{-.35f,.35f})Rod(m,new Vector3(-1.55f,.1f,z),new Vector3(-1.55f,3.7f,z),.035f,TownColor.Metal);return m;
        }
        private static TownMeshBuilder DryFountain()
        {
            var m=new TownMeshBuilder();m.Cylinder(new Vector3(0,.09f,0),2.7f,2.7f,.18f,TownColor.Stone,20);
            m.Cylinder(new Vector3(0,.19f,0),2.3f,2.3f,.025f,TownColor.Soil,20);
            for(int i=0;i<18;i++)if(i!=3&&i!=4)
            {
                float a=i*Mathf.PI/9;m.Box(new Vector3(Mathf.Cos(a)*2.5f,.4f,Mathf.Sin(a)*2.5f),new Vector3(.43f,.55f,.87f),i%4==0?TownColor.Cream:TownColor.Stone,Quaternion.Euler(0,-a*Mathf.Rad2Deg,0));
            }
            m.Cylinder(new Vector3(0,.6f,0),.55f,.36f,1.1f,TownColor.Stone,10);
            m.Cylinder(new Vector3(0,1.19f,0),.9f,1.1f,.2f,TownColor.Cream,12);
            m.Cylinder(new Vector3(0,1.3f,0),.85f,.85f,.025f,TownColor.Soil,12);
            for(int i=0;i<5;i++)m.Ellipsoid(new Vector3(Mathf.Cos(i)*1.6f,.4f,Mathf.Sin(i)*1.6f),new Vector3(.5f,.45f,.4f),TownColor.Leaf,6,3);return m;
        }
        private static TownMeshBuilder BrickWall()
        {
            var m=new TownMeshBuilder();
            for(int row=0;row<7;row++)for(int x=0;x<15;x++)
            {
                if((x>5&&x<10&&row>1)||(x==14&&row>3)||(x==0&&row>5))continue;
                m.Box(new Vector3(-3.4f+x*.46f+(row%2)*.22f,.13f+row*.22f,0),new Vector3(.43f,.2f,.32f),x%4==0?TownColor.Orange:TownColor.Brick);
            }
            for(int i=0;i<5;i++)m.Box(new Vector3(-.4f+i*.38f,.11f,.5f+Mathf.Sin(i)*.3f),new Vector3(.43f,.2f,.25f),TownColor.Brick,Quaternion.Euler(0,i*31,0));return m;
        }
        private static TownMeshBuilder Chainlink()
        {
            var m=new TownMeshBuilder();foreach(float x in new[]{-2.8f,0,2.8f})Rod(m,new Vector3(x,.05f,0),new Vector3(x-.08f,2.6f,0),.035f,TownColor.Metal);
            Rod(m,new Vector3(-2.9f,2.6f,0),new Vector3(2.7f,2.6f,0),.03f,TownColor.Metal);
            for(float x=-2.65f;x<2.7f;x+=.32f)for(float y=.3f;y<2.4f;y+=.5f)
            {
                if(x>1.1f&&y<1.1f)continue;
                Rod(m,new Vector3(x,y,0),new Vector3(x+.28f,y+.46f,0),.006f,TownColor.Metal,4);
                Rod(m,new Vector3(x+.28f,y,0),new Vector3(x,y+.46f,0),.006f,TownColor.Metal,4);
            }
            return m;
        }
        private static TownMeshBuilder Workbench()
        {
            var m=new TownMeshBuilder();foreach(float x in new[]{-1.15f,1.15f})foreach(float z in new[]{-.45f,.45f})m.Box(new Vector3(x,.55f,z),new Vector3(.1f,1.1f,.1f),TownColor.Metal);
            for(int i=0;i<5;i++)Plank(m,new Vector3(0,1.12f,-.47f+i*.23f),new Vector3(2.65f,.1f,.2f),i);
            m.Box(new Vector3(.7f,1.3f,.1f),new Vector3(.4f,.28f,.3f),TownColor.Blue);Rod(m,new Vector3(.7f,1.37f,-.25f),new Vector3(.7f,1.37f,.3f),.035f,TownColor.Metal);
            m.Box(new Vector3(-.5f,1.19f,0),new Vector3(.6f,.07f,.08f),TownColor.WoodLight);m.Box(new Vector3(-.75f,1.24f,0),new Vector3(.14f,.16f,.32f),TownColor.Metal);
            m.Box(new Vector3(-.8f,.55f,.05f),new Vector3(.75f,.45f,.8f),TownColor.Wood);return m;
        }
        private static TownMeshBuilder CableSpool()
        {
            var m=new TownMeshBuilder();foreach(float y in new[]{.1f,.85f})m.Cylinder(new Vector3(0,y,0),.85f,.85f,.13f,TownColor.WoodLight,12);
            m.Cylinder(new Vector3(0,.48f,0),.4f,.4f,.7f,TownColor.Wood,10);Ring(m,new Vector3(0,.95f,0),.2f,.025f,TownColor.Metal,Quaternion.identity);return m;
        }
        private static TownMeshBuilder Barrel()
        {
            var m=new TownMeshBuilder();m.Cylinder(new Vector3(0,.53f,0),.4f,.4f,1.04f,TownColor.Blue,12);
            foreach(float y in new[]{.09f,.35f,.72f,1.03f})Ring(m,new Vector3(0,y,0),.405f,.022f,TownColor.Metal,Quaternion.identity,12);
            m.Cylinder(new Vector3(0,1.065f,0),.36f,.36f,.015f,TownColor.Soil,12);
            for(int i=0;i<6;i++)m.Box(new Vector3(Mathf.Cos(i)*.405f,.22f+i*.1f,Mathf.Sin(i)*.405f),new Vector3(.12f,.13f,.035f),TownColor.Red,Quaternion.Euler(0,-i*Mathf.Rad2Deg,0));return m;
        }
        private static TownMeshBuilder Pots()
        {
            var m=new TownMeshBuilder();for(int i=0;i<3;i++)
            {
                var p=new Vector3(i*.43f,0,i%2*.3f);m.Cylinder(p+Vector3.up*.22f,.17f,.27f,.42f,TownColor.Orange,9);
                m.Cylinder(p+Vector3.up*.44f,.235f,.235f,.015f,TownColor.Soil,9);
                if(i!=1)m.Ellipsoid(p+Vector3.up*.59f,new Vector3(.23f,.22f,.22f),TownColor.Leaf,7,3);
            }
            m.Box(new Vector3(.25f,.025f,-.3f),new Vector3(.18f,.04f,.3f),TownColor.Orange,Quaternion.Euler(0,32,0));return m;
        }
        private static TownMeshBuilder Paint()
        {
            var m=new TownMeshBuilder();foreach(float x in new[]{-.23f,.23f})
            {m.Cylinder(new Vector3(x,.18f,0),.17f,.17f,.34f,TownColor.Cream,10);m.Cylinder(new Vector3(x,.36f,0),.15f,.15f,.015f,x<0?TownColor.Blue:TownColor.Red,10);}
            Rod(m,new Vector3(-.3f,.08f,-.35f),new Vector3(.25f,.09f,-.3f),.025f,TownColor.WoodLight);m.Box(new Vector3(.29f,.07f,-.3f),new Vector3(.15f,.07f,.1f),TownColor.Metal);return m;
        }
        private static TownMeshBuilder Toolbox()
        {
            var m=new TownMeshBuilder();m.Box(new Vector3(0,.2f,0),new Vector3(.7f,.36f,.4f),TownColor.Red);
            m.Box(new Vector3(0,.47f,.2f),new Vector3(.7f,.06f,.42f),TownColor.Red,Quaternion.Euler(-55,0,0));
            foreach(float x in new[]{-.19f,.19f})Rod(m,new Vector3(x,.36f,-.09f),new Vector3(x,.52f,-.09f),.018f,TownColor.Metal);
            Rod(m,new Vector3(-.19f,.52f,-.09f),new Vector3(.19f,.52f,-.09f),.025f,TownColor.Rubber);return m;
        }
        private static TownMeshBuilder Coffee()
        {
            var m=new TownMeshBuilder();m.Cylinder(new Vector3(0,.25f,0),.1f,.09f,.48f,TownColor.Metal,10);
            m.Cylinder(new Vector3(0,.51f,0),.095f,.095f,.08f,TownColor.Rubber,10);
            foreach(float x in new[]{.25f,.5f}){m.Cylinder(new Vector3(x,.07f,0),.065f,.07f,.14f,TownColor.Cream,9);m.Cylinder(new Vector3(x,.145f,0),.058f,.058f,.01f,TownColor.Wood);}
            m.Box(new Vector3(.32f,.02f,-.25f),new Vector3(.35f,.03f,.23f),TownColor.White,Quaternion.Euler(0,17,0));return m;
        }
        private static TownMeshBuilder Tarp()
        {
            var m=new TownMeshBuilder();for(int i=0;i<8;i++)m.Box(new Vector3(-.9f+i*.25f,.07f+(i%2)*.06f,0),new Vector3(.3f,.08f,1.5f),i%3==0?TownColor.Mint:TownColor.Blue,Quaternion.Euler(0,0,i%2==0?12:-12));return m;
        }
        private static TownMeshBuilder Pavers()
        {
            var m=new TownMeshBuilder();for(int y=0;y<4;y++)for(int x=0;x<3;x++)for(int z=0;z<2;z++)
                if(!(y==3&&x==2))m.Box(new Vector3((x-1)*.44f,.08f+y*.13f,(z-.5f)*.47f),new Vector3(.42f,.11f,.45f),y%2==0?TownColor.Stone:TownColor.Pavement);return m;
        }
        private static TownMeshBuilder Gate()
        {
            var m=new TownMeshBuilder();foreach(float z in new[]{-.7f,.7f})m.Box(new Vector3(0,.09f,z),new Vector3(3,.12f,.1f),TownColor.Mint);
            foreach(float x in new[]{-1.45f,1.45f})m.Box(new Vector3(x,.09f,0),new Vector3(.1f,.12f,1.5f),TownColor.Metal);
            for(int i=0;i<9;i++)if(i!=3)m.Box(new Vector3(-1.25f+i*.31f,.09f,0),new Vector3(.06f,.09f,1.4f),TownColor.Metal,Quaternion.Euler(0,i==6?10:0,0));return m;
        }
        private static TownMeshBuilder Leaves()
        {
            var m=new TownMeshBuilder();for(int i=0;i<18;i++)
            {float a=i*2.39f,r=Mathf.Sqrt(i/18f)*.9f;m.Ellipsoid(new Vector3(Mathf.Cos(a)*r,.025f+(1-r)*.08f,Mathf.Sin(a)*r),new Vector3(.13f,.025f,.19f),i%3==0?TownColor.Orange:TownColor.BoxDark,5,2);}return m;
        }
        private static TownMeshBuilder Flowers()
        {
            var m=new TownMeshBuilder();for(int i=0;i<7;i++)
            {
                float a=i*2.4f;var p=new Vector3(Mathf.Cos(a)*.42f,.24f+i%3*.11f,Mathf.Sin(a)*.4f);Rod(m,new Vector3(p.x,0,p.z),p,.012f,TownColor.LeafDark,4);
                for(int j=0;j<5;j++){float b=j*Mathf.PI*.4f;m.Ellipsoid(p+new Vector3(Mathf.Cos(b)*.055f,0,Mathf.Sin(b)*.055f),new Vector3(.055f,.022f,.045f),i%3==0?TownColor.Flowers:TownColor.White,5,2);}
                m.Cylinder(p+Vector3.up*.01f,.023f,.023f,.025f,TownColor.Yellow,5);
            }
            return m;
        }
        private static TownMeshBuilder Reeds()
        {
            var m=new TownMeshBuilder();for(int i=0;i<9;i++)
            {
                float a=i*2.4f;var p=new Vector3(Mathf.Cos(a)*.45f,.8f+i%3*.25f,Mathf.Sin(a)*.45f);Rod(m,new Vector3(p.x,0,p.z),p,.015f,TownColor.LeafDark,4);
                m.Cylinder(p+Vector3.down*.15f,.035f,.035f,.29f,TownColor.Wood,5);
            }
            return m;
        }
        private static TownMeshBuilder ElderTree()
        {
            var m=new TownMeshBuilder();Rod(m,new Vector3(0,.05f,0),new Vector3(.22f,3.2f,.1f),.22f,TownColor.Trunk,8);
            for(int i=0;i<5;i++)
            {
                float a=i*Mathf.PI*.4f;var p=new Vector3(Mathf.Cos(a)*1.6f,3.7f+(i%2)*.65f,Mathf.Sin(a)*1.4f);
                Rod(m,new Vector3(.1f,2.2f,0),p,.085f,TownColor.Trunk);
                m.Ellipsoid(p,new Vector3(1.65f,1.55f,1.5f),i%3==0?TownColor.LeafLight:i%2==0?TownColor.LeafDark:TownColor.Leaf,8,4);
            }
            foreach(float x in new[]{-.5f,.5f})Rod(m,new Vector3(0,.18f,0),new Vector3(x,.04f,.45f),.1f,TownColor.Trunk);return m;
        }
        private static TownMeshBuilder Pine()
        {
            var m=new TownMeshBuilder();Rod(m,new Vector3(0,.04f,0),new Vector3(.25f,5.8f,.1f),.16f,TownColor.Trunk);
            for(int i=0;i<4;i++)m.Cylinder(new Vector3(.08f+i*.05f,2.3f+i*.95f,0),2.05f-i*.37f,.05f,2.15f,TownColor.LeafDark,7);return m;
        }
        private static TownMeshBuilder AppleTree()
        {
            var m=new TownMeshBuilder();Rod(m,new Vector3(0,.04f,0),new Vector3(-.18f,2.25f,0),.14f,TownColor.Trunk);
            m.Ellipsoid(new Vector3(0,2.9f,0),new Vector3(1.95f,1.6f,1.8f),TownColor.Leaf,8,4);
            m.Ellipsoid(new Vector3(-1.05f,2.7f,.3f),new Vector3(.95f,1.1f,1.05f),TownColor.LeafLight,7,3);
            for(int i=0;i<13;i++){float a=i*2.4f;m.Ellipsoid(new Vector3(Mathf.Cos(a)*1.65f,2.4f+(i%3)*.4f,Mathf.Sin(a)*1.5f),Vector3.one*.095f,i%3==0?TownColor.Yellow:TownColor.Red,6,3);}return m;
        }
        private static TownMeshBuilder Woodpile()
        {
            var m=new TownMeshBuilder();for(int row=0;row<3;row++)for(int x=0;x<5-row;x++)
            {
                var p=new Vector3((x-(4-row)*.5f)*.42f,.23f+row*.35f,0);m.Cylinder(p,.22f,.22f,2.15f,TownColor.Trunk,8,Quaternion.Euler(90,0,0));
                m.Cylinder(p+Vector3.forward*1.081f,.18f,.18f,.025f,TownColor.WoodLight,8,Quaternion.Euler(90,0,0));
            }
            m.Box(new Vector3(0,1.17f,-.25f),new Vector3(2.8f,.09f,1.65f),TownColor.Blue,Quaternion.Euler(0,0,6));return m;
        }
        private static TownMeshBuilder LowBricks()
        {
            var m=new TownMeshBuilder();for(int y=0;y<3;y++)for(int x=0;x<8;x++)if(!(y==2&&x>5))
                m.Box(new Vector3(-1.7f+x*.45f+(y%2)*.2f,.12f+y*.22f,0),new Vector3(.43f,.2f,.34f),x%3==0?TownColor.Stone:TownColor.Brick);return m;
        }
    }
}
