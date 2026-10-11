using UnityEngine;
using RetailEmpireTycoon.Editor.TownKit;
using static RetailEmpireTycoon.Editor.ConceptTownKit.ConceptParts;

namespace RetailEmpireTycoon.Editor.ConceptTownKit
{
    internal static class ConceptFarm
    {
        public static ConceptGeometry Farmstead()
        {
            var g=new ConceptGeometry();var m=g.Solid;Pad(m,27,22,true);
            Box(m,P(0,.26f,1),22,.04f,15,TownColor.Sand);
            var house=P(-7,.3f,-3);WallBody(m,house,7,3.7f,6.4f,TownColor.Cream);
            GableRoof(m,house+P(0,3.7f,0),7.6f,7,2,TownColor.Cream);
            foreach(float x in new[]{-2.3f,2.3f})Window(m,house+P(x,1.9f,3.25f),1.3f,1.5f);
            foreach(float z in new[]{-1.7f,1.7f})Window(m,house+P(3.54f,1.9f,z),1.3f,1.5f,true);
            foreach(float z in new[]{-1.7f,1.7f})Window(m,house+P(-3.54f,1.9f,z),1.3f,1.5f,true,true);
            foreach(float x in new[]{-2.1f,2.1f})Window(m,house+P(x,1.9f,-3.25f),1.3f,1.5f,false,true);
            Door(m,house+P(0,1.3f,3.3f),1.1f,2.3f);
            Window(m,house+P(0,4.45f,3.56f),.75f,.75f);
            Chimney(m,house+P(-2,4.7f,-1));
            GableRoof(m,house+P(0,2.7f,4.2f),2.9f,2.1f,.6f,TownColor.White);
            foreach(float x in new[]{-1.3f,1.3f})Box(m,house+P(x,1.32f,4.9f),.1f,2.65f,.1f,TownColor.White);
            Box(m,house+P(0,.1f,4.2f),3,.2f,2,TownColor.Stone);
            var barn=P(4.8f,.3f,-3);WallBody(m,barn,8,5.2f,9,TownColor.Red);
            for(int i=0;i<18;i++)Box(m,barn+P(-3.8f+i*.45f,2.7f,4.53f),.045f,4.9f,.045f,TownColor.Brick);
            GambrelRoof(m,barn+P(0,5.2f,0),8.6f,9.7f);
            Box(m,barn+P(0,2.05f,4.6f),4.3f,4.1f,.14f,TownColor.White);
            Box(m,barn+P(0,2.05f,4.69f),3.95f,3.85f,.06f,TownColor.Red);
            foreach(float x in new[]{-2f,0,2f})Box(m,barn+P(x,2.05f,4.75f),.08f,3.92f,.06f,TownColor.White);
            for(int side=-1;side<=1;side+=2)
            {
                Beam(m,barn+P(side*1.95f,.18f,4.79f),barn+P(0,3.9f,4.79f),.11f,TownColor.White);
                Beam(m,barn+P(0,.18f,4.79f),barn+P(side*1.95f,3.9f,4.79f),.11f,TownColor.White);
            }
            Box(m,barn+P(0,5.4f,4.9f),1.4f,1.4f,.08f,TownColor.White);
            Box(m,barn+P(0,5.4f,4.96f),1.15f,1.15f,.04f,TownColor.Red);
            Beam(m,barn+P(-.5f,4.9f,5),barn+P(.5f,5.9f,5),.09f,TownColor.White);
            Beam(m,barn+P(.5f,4.9f,5),barn+P(-.5f,5.9f,5),.09f,TownColor.White);
            foreach(float x in new[]{-13f,13f})WoodenFence(m,P(x,.24f,-10),P(x,.24f,10));
            WoodenFence(m,P(-13,.24f,-10),P(13,.24f,-10));
            WoodenFence(m,P(-13,.24f,10),P(-3,.24f,10));
            WoodenFence(m,P(3,.24f,10),P(13,.24f,10));
            foreach(var p in new[]{P(9,.3f,3),P(10,.3f,5),P(8.5f,.3f,6.8f)})Hay(m,p);
            Hay(m,P(9,1.6f,4.1f));
            Tree(m,P(-11.5f,.24f,-7.5f),5);Tree(m,P(11,.24f,-7.7f),4.4f);
            Planter(m,P(-8.8f,.3f,1.2f),1.3f);Planter(m,P(-5.2f,.3f,1.2f),1.3f);
            for(int i=0;i<6;i++)Box(m,P(-5.4f+i*.5f,.3f,5.6f-i*.6f),.8f,.12f,.6f,TownColor.Stone);
            return g;
        }

        private static void GambrelRoof(TownMeshBuilder m,Vector3 o,float w,float d)
        {
            var profile=new[]{P(-w/2,0,0),P(-w*.29f,1.8f,0),P(0,2.8f,0),P(w*.29f,1.8f,0),P(w/2,0,0)};
            for(int i=0;i<4;i++)
            {
                var a=o+profile[i]+P(0,0,-d/2);var b=o+profile[i]+P(0,0,d/2);
                var c=o+profile[i+1]+P(0,0,d/2);var e=o+profile[i+1]+P(0,0,-d/2);
                m.Quad(a,b,c,e,TownColor.Mint);
                Beam(m,b,c,.1f,TownColor.White);Beam(m,a,e,.1f,TownColor.White);
                for(int seam=1;seam<15;seam++)
                    Beam(m,Vector3.Lerp(a,b,seam/15f)+P(0,.02f,0),Vector3.Lerp(e,c,seam/15f)+P(0,.02f,0),.025f,TownColor.GreenStripe);
            }
            for(int i=0;i<4;i++)
            {
                m.Triangle(o+P(0,0,d/2),o+profile[i+1]+P(0,0,d/2),o+profile[i]+P(0,0,d/2),TownColor.Red);
                m.Triangle(o+P(0,0,-d/2),o+profile[i]+P(0,0,-d/2),o+profile[i+1]+P(0,0,-d/2),TownColor.Red);
            }
            Box(m,o+P(0,-.08f,0),w,.16f,d,TownColor.White);
        }

        private static void Hay(TownMeshBuilder m,Vector3 bottom)
        {
            m.Cylinder(bottom+P(0,.66f,0),.66f,.66f,1.4f,TownColor.Yellow,10,Quaternion.Euler(90,0,0));
            foreach(float z in new[]{-.4f,.4f})m.Cylinder(bottom+P(0,.66f,z),.672f,.672f,.045f,TownColor.WoodLight,10,Quaternion.Euler(90,0,0));
            for(int i=0;i<3;i++)m.Cylinder(bottom+P(0,.66f,.708f+i*.003f),.16f+i*.17f,.16f+i*.17f,.008f,i%2==0?TownColor.Sand:TownColor.Yellow,10,Quaternion.Euler(90,0,0));
        }

        public static ConceptGeometry Greenhouse()
        {
            var g=new ConceptGeometry();var m=g.Solid;Pad(m,15,14,true);
            for(int bay=0;bay<2;bay++)
            {
                float x=-3+bay*6;const float depth=9;
                var o=P(x,.24f,-1);const float half=2.8f,eave=2.8f,ridge=4.4f;
                Box(m,o+P(0,.09f,0),5.9f,.18f,9.3f,TownColor.Stone);
                Box(m,o+P(0,.19f,0),5.6f,.04f,9,TownColor.Sand);
                for(int row=0;row<3;row++)
                {
                    float bedX=-1.8f+row*1.8f;
                    Box(m,o+P(bedX,.3f,0),1.2f,.2f,7.7f,TownColor.WoodLight);
                    Box(m,o+P(bedX,.42f,0),1.08f,.05f,7.5f,TownColor.Soil);
                    for(int plant=0;plant<8;plant++)
                    {
                        var p=o+P(bedX,.45f,-3.3f+plant*.9f);
                        Beam(m,p,p+P(0,1.1f,0),.045f,TownColor.Wood);
                        for(int leaf=0;leaf<3;leaf++)m.Ellipsoid(p+P((leaf%2==0?.13f:-.13f),.35f+leaf*.27f,0),P(.3f,.25f,.25f),leaf%2==0?TownColor.Leaf:TownColor.LeafLight,6,3);
                        m.Ellipsoid(p+P(.2f,.5f,.2f),P(.09f,.09f,.09f),TownColor.Red,6,3);
                    }
                }
                for(int frame=0;frame<=4;frame++)
                {
                    float z=-depth/2+frame*depth/4;
                    foreach(float side in new[]{-half,half})
                    {
                        Beam(m,o+P(side,.2f,z),o+P(side,eave,z),.085f,TownColor.White);
                        Beam(m,o+P(side,eave,z),o+P(0,ridge,z),.09f,TownColor.White);
                    }
                }
                foreach(float side in new[]{-half,half})foreach(float h in new[]{.25f,1.5f,eave})
                    Beam(m,o+P(side,h,-depth/2),o+P(side,h,depth/2),.07f,TownColor.White);
                Beam(m,o+P(0,ridge,-depth/2),o+P(0,ridge,depth/2),.085f,TownColor.White);
                foreach(float side in new[]{-1f,1f})
                {
                    Beam(m,o+P(side*half/2,(eave+ridge)/2,-depth/2),o+P(side*half/2,(eave+ridge)/2,depth/2),.065f,TownColor.White);
                    g.GlassPanel(o+P(side*half,.25f,-depth/2),o+P(side*half,.25f,depth/2),o+P(side*half,eave,depth/2),o+P(side*half,eave,-depth/2));
                    g.GlassPanel(o+P(side*half,eave,-depth/2),o+P(side*half,eave,depth/2),o+P(0,ridge,depth/2),o+P(0,ridge,-depth/2));
                }
                foreach(float z in new[]{-depth/2,depth/2})
                {
                    foreach(float side in new[]{-1f,1f})
                    {
                        g.GlassPanel(o+P(side*.62f,.25f,z),o+P(side*half,.25f,z),o+P(side*half,eave,z),o+P(side*.62f,eave,z));
                        g.GlassPanel(o+P(0,eave,z),o+P(side*half,eave,z),o+P(0,ridge,z),o+P(0,ridge-.02f,z));
                    }
                    if(z<0)g.GlassPanel(o+P(-.62f,.25f,z),o+P(.62f,.25f,z),o+P(.62f,eave,z),o+P(-.62f,eave,z));
                    foreach(float xDoor in new[]{-.62f,.62f})Beam(m,o+P(xDoor,.25f,z),o+P(xDoor,eave,z),.075f,TownColor.White);
                    Beam(m,o+P(-.62f,eave,z),o+P(.62f,eave,z),.075f,TownColor.White);
                }
                // Front entrance intentionally remains open, so plants and the passage are visible.
                Box(m,o+P(0,.14f,5),1.5f,.28f,1,TownColor.Stone);
                TownProps.Crate(m,P(x-1,.24f,5.3f),true);
                Planter(m,P(x+1.4f,.24f,5.3f),1.1f);
            }
            return g;
        }

        public static ConceptGeometry SilosAndGarage()
        {
            var g=new ConceptGeometry();var m=g.Solid;Pad(m,25,20);
            foreach(float x in new[]{-8f,-3f})
            {
                m.Cylinder(P(x,.5f,-3),2.2f,2.2f,.52f,TownColor.Stone,16);
                m.Cylinder(P(x,5.9f,-3),2,2,10.3f,TownColor.Cream,16);
                m.Cylinder(P(x,11.7f,-3),2.14f,.35f,1.3f,TownColor.Metal,16);
                m.Cylinder(P(x,12.4f,-3),.35f,.35f,.2f,TownColor.Frame,10);
                for(int band=0;band<6;band++)m.Cylinder(P(x,1.3f+band*1.7f,-3),2.015f,2.015f,.045f,TownColor.Metal,16);
                foreach(float side in new[]{-.35f,.35f})Beam(m,P(x+side,.75f,-.94f),P(x+side,11.4f,-.94f),.06f,TownColor.Metal);
                for(int rung=0;rung<31;rung++)Beam(m,P(x-.35f,.9f+rung*.33f,-.91f),P(x+.35f,.9f+rung*.33f,-.91f),.045f,TownColor.Metal);
                Box(m,P(x,1.1f,-.94f),.9f,1.4f,.16f,TownColor.Frame);
                Box(m,P(x,1.1f,-.83f),.68f,1.15f,.06f,TownColor.Metal);
            }
            Box(m,P(-5.5f,12.4f,-3),5.5f,.16f,1.2f,TownColor.Metal);
            Rail(m,P(-8.2f,12.5f,-3.55f),P(-2.8f,12.5f,-3.55f),.8f,TownColor.Metal);
            Rail(m,P(-8.2f,12.5f,-2.45f),P(-2.8f,12.5f,-2.45f),.8f,TownColor.Metal);
            var o=P(6,.24f,-2.5f);
            // Garage walls leave an actual opening, rather than painting black over a solid facade.
            Box(m,o+P(0,.1f,0),9,.2f,8,TownColor.Stone);
            foreach(float x in new[]{-4.5f,4.5f})Box(m,o+P(x,2.5f,0),.25f,5,8,TownColor.Cream);
            Box(m,o+P(0,2.5f,-3.9f),9,5,.25f,TownColor.Cream);
            Box(m,o+P(0,4.6f,3.95f),9,.8f,.25f,TownColor.Cream);
            foreach(float x in new[]{-4.1f,4.1f})Box(m,o+P(x,2.1f,3.95f),.7f,4.2f,.3f,TownColor.White);
            Box(m,o+P(0,4.1f,3.9f),7.6f,.12f,.35f,TownColor.Frame);
            GableRoof(m,o+P(0,5,0),9.8f,8.8f,1.6f,TownColor.Cream);
            Tractor(m,P(5,.24f,4.7f));
            TownProps.Pallet(m,P(-8,.24f,4.8f));
            TownProps.CardboardBox(m,P(-8,.49f,4.8f),P(.85f,1.1f,.8f));
            Planter(m,P(11,.24f,6),1.7f);Tree(m,P(11,.24f,-7),4.6f);return g;
        }

        private static void Tractor(TownMeshBuilder m,Vector3 o)
        {
            foreach(int axle in new[]{-1,1})foreach(int side in new[]{-1,1})
            {
                float r=axle<0?.8f:.52f,z=axle<0?-1.15f:1.55f;
                var p=o+P(side*1.12f,r,z);
                m.Cylinder(p,r,r,.46f,TownColor.Rubber,12,Quaternion.Euler(0,0,90));
                m.Cylinder(p+P(side*.25f,0,0),r*.55f,r*.55f,.03f,TownColor.Yellow,10,Quaternion.Euler(0,0,90));
                for(int tread=0;tread<12;tread++)
                {
                    float a=tread*Mathf.PI/6;
                    m.Box(p+P(0,Mathf.Cos(a)*(r-.005f),Mathf.Sin(a)*(r-.005f)),P(.49f,.09f,.12f),TownColor.Charcoal,Quaternion.Euler(a*Mathf.Rad2Deg,0,0));
                }
            }
            Box(m,o+P(0,.75f,.2f),1.7f,.5f,3.7f,TownColor.GreenStripe);
            Box(m,o+P(0,1.35f,1.3f),1.3f,.85f,1.85f,TownColor.Leaf);
            Box(m,o+P(0,1.37f,2.25f),1.1f,.65f,.06f,TownColor.Frame);
            for(int i=0;i<7;i++)Box(m,o+P(-.45f+i*.15f,1.37f,2.3f),.025f,.5f,.02f,TownColor.Metal);
            foreach(float x in new[]{-.52f,.52f})Box(m,o+P(x,1.77f,2.25f),.22f,.15f,.055f,TownColor.Light);
            Box(m,o+P(0,.72f,2.4f),1.6f,.25f,.22f,TownColor.Metal);
            Box(m,o+P(0,1.45f,-.6f),1.3f,.95f,1.5f,TownColor.Leaf);
            Box(m,o+P(0,2.25f,-.6f),1.25f,.85f,1.45f,TownColor.Glass);
            foreach(float x in new[]{-.67f,.67f})foreach(float z in new[]{-1.35f,.15f})
                Box(m,o+P(x,2.22f,z),.06f,.95f,.06f,TownColor.Frame);
            Box(m,o+P(0,2.76f,-.6f),1.65f,.16f,1.85f,TownColor.Leaf);
            foreach(float x in new[]{-1f,1f})Box(m,o+P(x,1.62f,-1.15f),.65f,.13f,1.85f,TownColor.Leaf);
            Beam(m,o+P(.5f,1.8f,.85f),o+P(.5f,2.8f,.85f),.09f,TownColor.Frame);
            Box(m,o+P(-.85f,.57f,-.4f),.48f,.1f,.6f,TownColor.Metal);
        }
    }
}
