using UnityEngine;
using RetailEmpireTycoon.Editor.TownKit;
using static RetailEmpireTycoon.Editor.ConceptTownKit.ConceptParts;

namespace RetailEmpireTycoon.Editor.ConceptTownKit
{
    internal static class ConceptInfrastructure
    {
        public static ConceptGeometry FuelStation()
        {
            var g=new ConceptGeometry();var m=g.Solid;Pad(m,24,21);
            Box(m,P(0,.27f,1),22,.055f,17,TownColor.Asphalt);
            var o=P(6,.3f,-5);WallBody(m,o,7,3.6f,6,TownColor.Cream);
            HipRoof(m,o+P(0,3.6f,0),7.5f,6.5f,1);
            Window(m,o+P(-1.5f,1.65f,3.04f),2.5f,1.8f);Door(m,o+P(1.7f,1.2f,3.04f),1.2f,2.3f);
            foreach(float z in new[]{-1.5f,1.5f})Window(m,o+P(3.54f,1.7f,z),1.8f,1.8f,true);
            foreach(float z in new[]{-1.5f,1.5f})Window(m,o+P(-3.54f,1.7f,z),1.8f,1.8f,true,true);
            foreach(float x in new[]{-1.8f,1.8f})Window(m,o+P(x,1.7f,-3.04f),1.8f,1.8f,false,true);
            Sign(m,o+P(0,3.25f,3.07f),5,"МАРКЕТ");
            var canopy=P(-3,5.3f,0);
            Box(m,canopy,11.5f,.44f,8,TownColor.White);
            Box(m,canopy+P(0,.27f,0),11.8f,.16f,8.3f,TownColor.Mint);
            foreach(float z in new[]{-4.04f,4.04f})Box(m,canopy+P(0,-.05f,z),11.3f,.18f,.06f,TownColor.Red);
            for(int i=0;i<14;i++)Box(m,canopy+P(-5.6f+i*.85f,.36f,0),.035f,.025f,8,TownColor.GreenStripe);
            foreach(float x in new[]{-6f,0f})
            {
                Box(m,P(x,2.75f,0),.25f,4.95f,.25f,TownColor.White);
                Box(m,P(x,.43f,0),2.5f,.3f,4.8f,TownColor.Pavement);
                Pump(m,P(x,.59f,1.2f));Pump(m,P(x,.59f,-1.2f));
                foreach(float z in new[]{-2.25f,2.25f})m.Cylinder(P(x,.86f,z),.09f,.09f,.7f,TownColor.Frame,6);
            }
            foreach(float x in new[]{-9f,3.5f})
            {
                Arrow(m,P(x,.307f,6),x<0?1:-1);
                for(int i=0;i<3;i++)Box(m,P(x,.307f,-5+i*1.4f),.12f,.008f,.7f,TownColor.White);
            }
            Box(m,P(10.4f,2.15f,6.6f),.2f,3.7f,.2f,TownColor.Frame);
            Box(m,P(10.4f,3.2f,6.6f),1.6f,3.6f,.3f,TownColor.Mint);
            m.Cylinder(P(10.4f,4.1f,6.79f),.4f,.4f,.04f,TownColor.White,12,Quaternion.Euler(90,0,0));
            for(int i=0;i<3;i++)Box(m,P(10.4f,2.1f+i*.4f,6.78f),1.2f,.16f,.03f,TownColor.Light);
            Planter(m,P(-10,.3f,7),1.8f);Tree(m,P(10,.3f,-9),4.1f);Lamp(m,P(-10,.3f,-7));return g;
        }

        private static void Pump(TownMeshBuilder m,Vector3 p)
        {
            Box(m,p+P(0,.5f,0),.75f,1,.5f,TownColor.Red);
            Box(m,p+P(0,1.25f,0),.82f,.62f,.6f,TownColor.White);
            Box(m,p+P(0,1.36f,.31f),.54f,.2f,.025f,TownColor.Frame);
            for(int i=0;i<3;i++)Box(m,p+P(-.17f+i*.17f,1.36f,.327f),.09f,.08f,.008f,TownColor.Light);
            Box(m,p+P(0,1.06f,.32f),.4f,.05f,.03f,TownColor.Yellow);
            Beam(m,p+P(.47f,1.4f,0),p+P(.62f,.6f,0),.045f,TownColor.Rubber);
            Beam(m,p+P(.62f,.6f,0),p+P(.46f,.38f,0),.045f,TownColor.Rubber);
            Beam(m,p+P(.46f,.38f,0),p+P(.44f,1.1f,.1f),.045f,TownColor.Rubber);
            Box(m,p+P(.45f,1.1f,.1f),.09f,.25f,.12f,TownColor.Frame);
        }

        private static void Arrow(TownMeshBuilder m,Vector3 p,int direction)
        {
            Box(m,p,.14f,.009f,1.1f,TownColor.White);
            m.Ground(p.x,p.z+direction*.7f,.6f,.12f,p.y,TownColor.White);
            m.Triangle(p+P(-.4f,0,direction*.5f),p+P(0,0,direction*1.05f),p+P(.4f,0,direction*.5f),TownColor.White);
            m.Triangle(p+P(.4f,0,direction*.5f),p+P(0,0,direction*1.05f),p+P(-.4f,0,direction*.5f),TownColor.White);
        }

        public static ConceptGeometry BusStop()
        {
            var g=new ConceptGeometry();var m=g.Solid;Pad(m,8.5f,5);
            foreach(float x in new[]{-2.5f,2.5f})foreach(float z in new[]{-.85f,.85f})
                Box(m,P(x,1.62f,z-.45f),.11f,2.75f,.11f,TownColor.Frame);
            Box(m,P(0,3.05f,-.45f),5.5f,.22f,2.6f,TownColor.Mint);
            Box(m,P(0,3.22f,-.45f),5.6f,.12f,2.7f,TownColor.Mint);
            for(int i=-2;i<=2;i++)Box(m,P(i,1.65f,-1.3f),.065f,2.7f,.065f,TownColor.Metal);
            g.GlassPanel(P(-2.44f,.35f,-1.3f),P(2.44f,.35f,-1.3f),P(2.44f,2.93f,-1.3f),P(-2.44f,2.93f,-1.3f));
            foreach(float x in new[]{-2.5f,2.5f})
                g.GlassPanel(P(x,.35f,-1.24f),P(x,.35f,.35f),P(x,2.93f,.35f),P(x,2.93f,-1.24f));
            Bench(m,P(.5f,.24f,-.5f),3.1f);
            Box(m,P(-1.75f,1.7f,-1.24f),.85f,1.35f,.06f,TownColor.White);
            for(int row=0;row<6;row++)Box(m,P(-1.75f,1.25f+row*.18f,-1.2f),.6f,.035f,.015f,TownColor.Blue);
            Box(m,P(3.5f,1.67f,.7f),.08f,2.86f,.08f,TownColor.Frame);
            Box(m,P(3.5f,2.85f,.7f),.9f,.9f,.12f,TownColor.White);
            Box(m,P(3.5f,2.85f,.78f),.74f,.74f,.025f,TownColor.Blue);
            Box(m,P(3.5f,2.85f,.8f),.43f,.34f,.02f,TownColor.White);
            Box(m,P(3.5f,2.91f,.82f),.32f,.15f,.01f,TownColor.Glass);
            foreach(float x in new[]{3.34f,3.66f})m.Cylinder(P(x,2.65f,.83f),.055f,.055f,.02f,TownColor.White,8,Quaternion.Euler(90,0,0));
            m.Cylinder(P(3.5f,.65f,-1),.27f,.27f,.82f,TownColor.Metal,8);
            m.Cylinder(P(3.5f,1.08f,-1),.29f,.29f,.05f,TownColor.Frame,8);
            Planter(m,P(-3.55f,.24f,-.5f),.9f);return g;
        }

        public static ConceptGeometry ConstructionSite()
        {
            var g=new ConceptGeometry();var m=g.Solid;Pad(m,22,22,true);
            Box(m,P(-1,.3f,-1),13,.12f,13,TownColor.Sand);
            for(int level=0;level<3;level++)
            {
                float y=.5f+level*3.1f;
                Box(m,P(-1,y,-1),9.5f,.28f,9.5f,TownColor.Stone);
                foreach(float x in new[]{-5.3f,-1f,3.3f})foreach(float z in new[]{-5.3f,-1f,3.3f})
                    Box(m,P(x,y+1.55f,z),.38f,3.1f,.38f,TownColor.Cream);
                if(level==0)for(int i=0;i<4;i++)Box(m,P(-4.8f+i*1.25f,y+1.1f,-5.2f),1.1f,2.2f,.2f,TownColor.Brick);
            }
            Box(m,P(-1,9.8f,-1),9.5f,.28f,9.5f,TownColor.Stone);
            for(int i=0;i<5;i++)Box(m,P(-3+i*.55f,10.45f,-1.5f),.44f,1.2f,2.1f,TownColor.WoodLight);
            for(float z=-5;z<=4;z+=2)
            {
                Beam(m,P(-6,.4f,z),P(-6,10.8f,z),.08f,TownColor.Metal);
                Beam(m,P(-7.1f,.4f,z),P(-7.1f,10.8f,z),.08f,TownColor.Metal);
                for(int level=1;level<=3;level++)Beam(m,P(-6,level*3.1f,z),P(-7.1f,level*3.1f,z),.08f,TownColor.Metal);
            }
            for(int level=1;level<=3;level++)Box(m,P(-6.55f,level*3.1f,-.5f),1.2f,.08f,11,TownColor.Wood);
            Crane(m,P(7,.24f,-5));
            for(int i=0;i<3;i++)
            {
                var p=P(-7+i*2.3f,.24f,7);TownProps.Pallet(m,p);
                for(int j=0;j<3;j++)Box(m,p+P(0,.4f+j*.26f,0),1.2f,.22f,.8f,TownColor.Stone);
            }
            Box(m,P(7.5f,1.4f,6),2.4f,2.3f,2.5f,TownColor.Blue);
            Box(m,P(7.5f,2.63f,6),2.6f,.16f,2.7f,TownColor.White);
            Door(m,P(7.5f,1.45f,7.29f),.95f,2.05f);
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<7;i++)Box(m,P(side*10,1.1f,-9+i*3),.12f,1.7f,2.9f,TownColor.Blue);
                Box(m,P(0,1.1f,side*10),20,1.7f,.12f,TownColor.Blue);
            }
            // Front gate leaves a readable entrance between temporary barriers.
            Box(m,P(0,1.1f,10.08f),4,1.7f,.12f,TownColor.White);
            for(int i=0;i<6;i++)m.Box(P(-1.7f+i*.65f,1.1f,10.16f),P(.22f,1.55f,.02f),TownColor.Red,Quaternion.Euler(0,0,-25));
            return g;
        }

        private static void Crane(TownMeshBuilder m,Vector3 p)
        {
            Box(m,p+P(0,.35f,0),3,.7f,3,TownColor.Stone);
            const float height=16;
            foreach(float x in new[]{-.6f,.6f})foreach(float z in new[]{-.6f,.6f})
                Beam(m,p+P(x,.7f,z),p+P(x,height,z),.14f,TownColor.Yellow);
            for(int level=0;level<8;level++)
            {
                float y=1+level*1.85f;
                foreach(float side in new[]{-.6f,.6f})
                {
                    Beam(m,p+P(-.6f,y,side),p+P(.6f,y+1.8f,side),.065f,TownColor.Yellow);
                    Beam(m,p+P(side,y,-.6f),p+P(side,y+1.8f,.6f),.065f,TownColor.Yellow);
                    Beam(m,p+P(-.6f,y,side),p+P(.6f,y,side),.08f,TownColor.Yellow);
                }
            }
            Box(m,p+P(.85f,15.1f,0),1.45f,1.2f,1.5f,TownColor.Yellow);
            Box(m,p+P(1.6f,15.25f,0),.035f,.65f,1.1f,TownColor.Glass);
            foreach(float z in new[]{-.5f,.5f})
            {
                Beam(m,p+P(-12,16,z),p+P(4,16,z),.1f,TownColor.Yellow);
                Beam(m,p+P(-12,17,z),p+P(4,17,z),.1f,TownColor.Yellow);
                for(int i=0;i<16;i++)Beam(m,p+P(-12+i,16,z),p+P(-11+i,17,z),.06f,TownColor.Yellow);
            }
            for(int i=0;i<=16;i++)Beam(m,p+P(-12+i,16,-.5f),p+P(-12+i,16,.5f),.06f,TownColor.Yellow);
            Box(m,p+P(3,15.8f,0),2.2f,1.1f,1.7f,TownColor.Stone);
            Beam(m,p+P(-8,16,0),p+P(-8,8,0),.035f,TownColor.Frame);
            Box(m,p+P(-8,7.7f,0),.22f,.65f,.3f,TownColor.Yellow);
            m.Cylinder(p+P(-8,7.3f,0),.16f,.16f,.12f,TownColor.Frame,8,Quaternion.Euler(90,0,0));
        }

        public static ConceptGeometry RailwayStation()
        {
            var g=new ConceptGeometry();var m=g.Solid;Pad(m,25,19);
            Box(m,P(0,.44f,-1),24,.4f,10,TownColor.Pavement);
            var o=P(1,.64f,-2.4f);WallBody(m,o,10,4.6f,6,TownColor.Cream);
            GableRoof(m,o+P(0,4.6f,0),10.7f,6.7f,2.1f,TownColor.Cream);
            Door(m,o+P(0,1.35f,3.05f),2,2.55f);
            foreach(float x in new[]{-3.3f,3.3f})Window(m,o+P(x,1.95f,3.06f),1.6f,2.5f);
            Window(m,o+P(5.04f,2,-1),1.7f,2.5f,true);
            Window(m,o+P(-5.04f,2,-1),1.7f,2.5f,true,true);
            foreach(float x in new[]{-3f,0,3f})Window(m,o+P(x,2,-3.04f),1.6f,2.3f,false,true);
            Sign(m,o+P(0,4.05f,3.12f),6.2f,"ВОКЗАЛ");
            var clock=o+P(0,5.25f,3.4f);
            m.Cylinder(clock,.55f,.55f,.12f,TownColor.Frame,16,Quaternion.Euler(90,0,0));
            m.Cylinder(clock+P(0,0,.07f),.48f,.48f,.025f,TownColor.White,16,Quaternion.Euler(90,0,0));
            Beam(m,clock+P(0,0,.1f),clock+P(0,.32f,.1f),.035f,TownColor.Frame);
            Beam(m,clock+P(0,0,.1f),clock+P(.25f,-.05f,.1f),.04f,TownColor.Frame);
            WallBody(m,P(8.3f,.64f,-2.4f),3.8f,3.2f,6,TownColor.Cream);
            GableRoof(m,P(8.3f,3.84f,-2.4f),4.3f,6.6f,1.2f,TownColor.Cream);
            Window(m,P(8.3f,2.3f,.66f),1.8f,1.9f);
            for(int i=0;i<4;i++)Box(m,P(-10+i*3,2.14f,1.9f),.13f,3,.13f,TownColor.Frame);
            GableRoof(m,P(-5.5f,3.64f,2),10.6f,3.2f,.5f,TownColor.Mint);
            Bench(m,P(-6,.64f,1.7f),3);Planter(m,P(5,.64f,2.1f),1.5f);
            for(float x=-12;x<=12;x+=1.1f)Box(m,P(x,.36f,6.7f),.22f,.18f,3.3f,TownColor.Wood);
            foreach(float z in new[]{5.8f,7.6f})
            {
                Box(m,P(0,.54f,z),25,.16f,.09f,TownColor.Metal);
                Box(m,P(0,.43f,z),25,.06f,.18f,TownColor.Frame);
            }
            Box(m,P(0,.65f,4),24,.035f,.12f,TownColor.Yellow);
            foreach(float x in new[]{-11f,11f})Lamp(m,P(x,.64f,2.5f),3.6f);
            Tree(m,P(-10,.24f,-7),4.4f);return g;
        }

        public static ConceptGeometry Bridge()
        {
            var g=new ConceptGeometry();var m=g.Solid;
            Box(m,P(0,.2f,0),22,.4f,18,TownColor.Sand);
            Box(m,P(0,.42f,0),10,.06f,18,TownColor.Glass);
            foreach(float x in new[]{-8.5f,8.5f})Box(m,P(x,.75f,0),5,1.1f,18,TownColor.Grass);
            foreach(float x in new[]{-6f,6f})
            {
                Box(m,P(x,1.95f,0),.8f,3.1f,8.8f,TownColor.Stone);
                Box(m,P(x,3.5f,0),1.2f,.25f,9,TownColor.Pavement);
            }
            foreach(float z in new[]{-3.2f,3.2f})Box(m,P(0,2.35f,z),1,3.8f,1,TownColor.Stone);
            Box(m,P(0,3.65f,0),22,.5f,9,TownColor.Pavement);
            Box(m,P(0,3.93f,0),22,.06f,6.6f,TownColor.Asphalt);
            foreach(float z in new[]{-4f,4f})
            {
                Box(m,P(0,4,z),22,.12f,1.25f,TownColor.Pavement);
                Rail(m,P(-10.8f,4.07f,z),P(10.8f,4.07f,z),1);
                Box(m,P(0,4,z*.83f),22,.12f,.12f,TownColor.White);
            }
            for(int i=0;i<10;i++)Box(m,P(-9.8f+i*2.1f,3.967f,0),1.1f,.01f,.1f,TownColor.Yellow);
            foreach(float z in new[]{-2.8f,2.8f})Box(m,P(0,3.967f,z),22,.01f,.08f,TownColor.White);
            foreach(float x in new[]{-9f,9f})Lamp(m,P(x,4.07f,4.1f),3);
            foreach(float side in new[]{-1f,1f})for(int i=0;i<3;i++)
            {
                m.Ellipsoid(P(side*(5.7f+i*.8f),.95f,-6+i*.7f),P(.8f,.65f,.8f),TownColor.Stone,6,3);
                Tree(m,P(side*(8.2f+i*.6f),1.3f,6-i*1.2f),2.6f+i*.2f);
            }
            for(int i=0;i<8;i++)Box(m,P(Mathf.Sin(i*1.4f)*3,.459f,-7+i*2),.6f,.005f,.06f,TownColor.White);
            return g;
        }
    }
}
