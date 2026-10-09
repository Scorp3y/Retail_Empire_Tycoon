using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    internal static class TownProps
    {
        public static TownMeshBuilder Create(System.Action<TownMeshBuilder> build)
        {
            var mesh=new TownMeshBuilder();build(mesh);return mesh;
        }

        public static void CardboardBox(TownMeshBuilder m,Vector3 bottom,Vector3 size)
        {
            m.Box(bottom+Vector3.up*size.y/2,size,TownColor.Box);
            m.Box(bottom+new Vector3(0,size.y+.002f,0),new Vector3(size.x*.14f,.008f,size.z),TownColor.BoxDark);
            m.Box(bottom+new Vector3(0,size.y*.6f,size.z/2+.003f),new Vector3(size.x*.3f,size.y*.28f,.008f),TownColor.White);
        }

        public static void Pallet(TownMeshBuilder m,Vector3 bottom)
        {
            foreach(float x in new[]{-.48f,0,.48f})m.Box(bottom+new Vector3(x,.08f,0),new Vector3(.17f,.16f,.8f),TownColor.Wood);
            for(int i=0;i<5;i++)m.Box(bottom+new Vector3(-.48f+i*.24f,.2f,0),new Vector3(.2f,.09f,.85f),TownColor.WoodLight);
        }

        public static void Crate(TownMeshBuilder m,Vector3 bottom,bool vegetables)
        {
            const float w=1.1f,d=.75f,h=.55f;
            m.Box(bottom+Vector3.up*.055f,new Vector3(w,.11f,d),TownColor.Wood);
            foreach(float x in new[]{-w/2,w/2})foreach(float z in new[]{-d/2,d/2})m.Box(bottom+new Vector3(x,h/2,z),new Vector3(.08f,h,.08f),TownColor.WoodLight);
            foreach(float y in new[]{.17f,.38f})
            {
                foreach(float z in new[]{-d/2,d/2})m.Box(bottom+new Vector3(0,y,z),new Vector3(w,.13f,.055f),TownColor.WoodLight);
                foreach(float x in new[]{-w/2,w/2})m.Box(bottom+new Vector3(x,y,0),new Vector3(.055f,.13f,d),TownColor.WoodLight);
            }
            for(int x=0;x<4;x++)for(int z=0;z<2;z++)
            {
                var position=bottom+new Vector3(-.37f+x*.245f,.32f,-.18f+z*.34f);
                m.Ellipsoid(position,vegetables?new Vector3(.1f,.1f,.18f):Vector3.one*.145f,vegetables?TownColor.Leaf:TownColor.Red,6,3);
                if(!vegetables)m.Cylinder(position+Vector3.up*.16f,.018f,.018f,.055f,TownColor.Trunk,4);
            }
        }

        public static void WarehouseRack(TownMeshBuilder m,Vector3 bottom)
        {
            const float w=3.2f,d=1.1f,h=3;
            foreach(float x in new[]{-w/2,w/2})foreach(float z in new[]{-d/2,d/2})
                m.Box(bottom+new Vector3(x,h/2,z),new Vector3(.1f,h,.1f),TownColor.Frame);
            for(int level=0;level<3;level++)
            {
                float y=.15f+level*.93f;
                m.Box(bottom+new Vector3(0,y,0),new Vector3(w,.1f,d),TownColor.Metal);
                foreach(float z in new[]{-d/2,d/2})m.Box(bottom+new Vector3(0,y+.03f,z),new Vector3(w,.16f,.1f),TownColor.Orange);
                for(int box=0;box<4;box++)CardboardBox(m,bottom+new Vector3(-1.12f+box*.74f,y+.05f,0),new Vector3(.62f,.64f,.8f));
            }
        }

        public static void Shelf(TownMeshBuilder m,Vector3 bottom,float width,float height,float depth,bool stock)
        {
            m.Box(bottom+new Vector3(0,height/2,-depth/2),new Vector3(width,height,.07f),TownColor.Frame);
            foreach(float x in new[]{-width/2,width/2})m.Box(bottom+new Vector3(x,height/2,0),new Vector3(.08f,height,depth),TownColor.Metal);
            for(int level=0;level<4;level++)
            {
                float y=.2f+level*(height-.3f)/3;
                m.Box(bottom+new Vector3(0,y,0),new Vector3(width,.075f,depth),TownColor.Metal);
                m.Box(bottom+new Vector3(0,y+.01f,depth/2),new Vector3(width,.13f,.045f),TownColor.White);
                if(stock&&level<3)for(int item=0;item<7;item++)
                {
                    var color=(TownColor)new[]{(int)TownColor.Yellow,(int)TownColor.Red,(int)TownColor.Blue}[level%3];
                    m.Box(bottom+new Vector3(-width*.4f+item*width*.8f/6,y+.24f,.1f),new Vector3(width*.085f,.4f,depth*.6f),color);
                }
            }
        }

        public static void Refrigerator(TownMeshBuilder m,Vector3 bottom)
        {
            m.Box(bottom+Vector3.up*1.15f,new Vector3(1.6f,2.3f,.9f),TownColor.Cream);
            foreach(float x in new[]{-.38f,.38f})
            {
                m.Box(bottom+new Vector3(x,1.2f,.47f),new Vector3(.7f,1.95f,.06f),TownColor.Glass);
                m.Box(bottom+new Vector3(x+.27f,1.2f,.53f),new Vector3(.045f,.75f,.07f),TownColor.Frame);
                for(int level=0;level<4;level++)m.Box(bottom+new Vector3(x,.45f+level*.43f,.515f),new Vector3(.64f,.05f,.015f),TownColor.White);
            }
        }

        public static void Checkout(TownMeshBuilder m,Vector3 bottom)
        {
            m.Box(bottom+new Vector3(0,.5f,0),new Vector3(2.3f,1,.9f),TownColor.GreenStripe);
            m.Box(bottom+new Vector3(0,1.06f,0),new Vector3(2.45f,.15f,1),TownColor.Metal);
            m.Box(bottom+new Vector3(-.5f,1.15f,0),new Vector3(1,.06f,.65f),TownColor.Rubber);
            m.Box(bottom+new Vector3(.65f,1.3f,0),new Vector3(.55f,.38f,.5f),TownColor.Frame);
            m.Box(bottom+new Vector3(.65f,1.57f,0),new Vector3(.42f,.32f,.08f),TownColor.Glass,Quaternion.Euler(-15,0,0));
        }

        public static void LoadingCanopy(TownMeshBuilder m)
        {
            foreach(float x in new[]{-4f,4f})foreach(float z in new[]{-3f,3f})m.Box(new Vector3(x,1.85f,z),new Vector3(.2f,3.7f,.2f),TownColor.Frame);
            m.Gable(new Vector3(0,3.7f,0),8.7f,6.7f,.55f,TownColor.Mint,TownColor.Mint);
        }

        public static void Fence(TownMeshBuilder m,float length,float height,bool industrial)
        {
            foreach(float x in new[]{-length/2,length/2})m.Box(new Vector3(x,height/2,0),new Vector3(.12f,height,.12f),industrial?TownColor.Frame:TownColor.Wood);
            for(float x=-length/2+.2f;x<length/2;x+=industrial?.23f:.18f)
            {
                if(industrial)m.Box(new Vector3(x,height*.52f,0),new Vector3(.045f,height*.92f,.045f),TownColor.Frame);
                else
                {
                    m.Box(new Vector3(x,height*.48f,0),new Vector3(.14f,height*.96f,.08f),TownColor.WoodLight);
                    m.Gable(new Vector3(x,height*.96f,0),.14f,.08f,.1f,TownColor.WoodLight,TownColor.WoodLight);
                }
            }
            foreach(float y in new[]{height*.25f,height*.75f})m.Box(new Vector3(0,y,-.04f),new Vector3(length,.07f,.065f),industrial?TownColor.Frame:TownColor.Wood);
        }

        public static void Bench(TownMeshBuilder m)
        {
            foreach(float x in new[]{-.7f,.7f})m.Box(new Vector3(x,.25f,0),new Vector3(.08f,.5f,.58f),TownColor.Frame);
            for(int i=0;i<4;i++)m.Box(new Vector3(0,.52f,-.21f+i*.14f),new Vector3(1.9f,.09f,.1f),TownColor.WoodLight);
            for(int i=0;i<3;i++)m.Box(new Vector3(0,.76f+i*.14f,-.3f),new Vector3(1.9f,.1f,.07f),TownColor.WoodLight);
            foreach(float x in new[]{-.7f,.7f})m.Box(new Vector3(x,.77f,-.34f),new Vector3(.07f,.63f,.07f),TownColor.Frame);
        }

        public static void Planter(TownMeshBuilder m,Vector3 bottom,bool flowers)
        {
            m.Box(bottom+new Vector3(0,.32f,0),new Vector3(1,.64f,.7f),TownColor.Cream);
            m.Ground(bottom.x,bottom.z,.85f,.55f,bottom.y+.65f,TownColor.Soil);
            for(int i=0;i<5;i++)
            {
                var p=bottom+new Vector3(-.33f+i*.165f,.82f,(i%2==0?-.12f:.12f));
                m.Ellipsoid(p,new Vector3(.18f,.25f,.2f),TownColor.Leaf,7,5);
                if(flowers)TownNature.Flower(m,p+Vector3.up*.22f,.09f,i%2==0?TownColor.Pink:TownColor.Flowers);
            }
        }

        public static TownMeshBuilder Container()
        {
            var m=new TownMeshBuilder();m.Box(new Vector3(0,1.3f,0),new Vector3(2.5f,2.6f,6),TownColor.Blue);
            for(int side=-1;side<=1;side+=2)for(float z=-2.8f;z<3;z+=.32f)m.Box(new Vector3(side*1.27f,1.3f,z),new Vector3(.045f,2.4f,.055f),TownColor.Metal);
            foreach(float x in new[]{-.62f,.62f})
            {
                m.Box(new Vector3(x,1.3f,3.025f),new Vector3(1.18f,2.5f,.055f),TownColor.Blue);
                m.Box(new Vector3(x+.32f,1.3f,3.075f),new Vector3(.045f,2.2f,.035f),TownColor.Metal);
            }
            return m;
        }

        public static TownMeshBuilder StreetLamp()
        {
            var m=new TownMeshBuilder();m.Cylinder(new Vector3(0,.12f,0),.25f,.22f,.24f,TownColor.Frame);
            m.Cylinder(new Vector3(0,2.7f,0),.085f,.055f,5.4f,TownColor.Frame);
            m.Box(new Vector3(0,5.4f,.5f),new Vector3(.1f,.1f,1),TownColor.Frame);
            m.Box(new Vector3(0,5.34f,.98f),new Vector3(.45f,.16f,.65f),TownColor.Frame);
            m.Box(new Vector3(0,5.25f,.98f),new Vector3(.36f,.04f,.56f),TownColor.Light);return m;
        }

        public static TownMeshBuilder WasteContainer()
        {
            var m=new TownMeshBuilder();
            m.Box(new Vector3(0,.73f,0),new Vector3(1.5f,1.05f,1.1f),TownColor.GreenStripe);
            m.Box(new Vector3(0,1.31f,0),new Vector3(1.64f,.13f,1.2f),TownColor.LeafDark);
            foreach(float x in new[]{-.58f,.58f})
            {
                foreach(float z in new[]{-.41f,.41f})
                {
                    m.Cylinder(new Vector3(x,.14f,z),.14f,.14f,.08f,TownColor.Rubber,8,Quaternion.Euler(0,0,90));
                    m.Box(new Vector3(x,.27f,z),new Vector3(.09f,.18f,.09f),TownColor.Metal);
                }
                m.Box(new Vector3(x,.86f,.59f),new Vector3(.24f,.06f,.08f),TownColor.Metal);
                m.Box(new Vector3(x,1.33f,-.63f),new Vector3(.22f,.09f,.12f),TownColor.Frame);
            }
            m.Box(new Vector3(0,.87f,.561f),new Vector3(.47f,.25f,.015f),TownColor.White);
            return m;
        }

        public static TownMeshBuilder BusStop()
        {
            var m=new TownMeshBuilder();foreach(float x in new[]{-2.3f,2.3f})foreach(float z in new[]{-.9f,.9f})m.Box(new Vector3(x,1.4f,z),new Vector3(.09f,2.8f,.09f),TownColor.Frame);
            m.Box(new Vector3(0,2.85f,0),new Vector3(4.9f,.15f,2.2f),TownColor.Mint);
            m.Box(new Vector3(0,1.45f,-.92f),new Vector3(4.5f,2.5f,.04f),TownColor.Glass);
            m.Box(new Vector3(-2.32f,1.45f,0),new Vector3(.04f,2.5f,1.8f),TownColor.Glass);
            m.Box(new Vector3(2.32f,1.45f,0),new Vector3(.04f,2.5f,1.8f),TownColor.Glass);
            Bench(m);return m;
        }

        public static TownMeshBuilder TrafficSign(string type)
        {
            var m=new TownMeshBuilder();m.Cylinder(new Vector3(0,1.3f,0),.045f,.045f,2.6f,TownColor.Metal,6);
            if(type=="stop")
            {
                m.Cylinder(new Vector3(0,2.6f,0),.48f,.48f,.06f,TownColor.White,8,Quaternion.Euler(90,0,0));
                m.Cylinder(new Vector3(0,2.6f,.045f),.43f,.43f,.035f,TownColor.Red,8,Quaternion.Euler(90,0,0));
                TownArchitecturalParts.Text(m,new Vector3(0,2.6f,.07f),.72f,.25f,"STOP",TownColor.White);
            }
            else if(type=="yield")
            {
                m.Triangle(new Vector3(-.48f,2.95f,.04f),new Vector3(0,2.1f,.04f),new Vector3(.48f,2.95f,.04f),TownColor.Red);
                m.Triangle(new Vector3(-.34f,2.86f,.045f),new Vector3(0,2.27f,.045f),new Vector3(.34f,2.86f,.045f),TownColor.White);
                // Closed rear and rim keep the sign visible when driving past its back.
                var a=new Vector3(-.48f,2.95f,.04f);var b=new Vector3(0,2.1f,.04f);var c=new Vector3(.48f,2.95f,.04f);
                var offset=Vector3.back*.055f;
                m.Triangle(c+offset,b+offset,a+offset,TownColor.Metal);
                m.Quad(a,a+offset,b+offset,b,TownColor.Red);
                m.Quad(b,b+offset,c+offset,c,TownColor.Red);
                m.Quad(c,c+offset,a+offset,a,TownColor.Red);
            }
            else
            {
                m.Box(new Vector3(0,2.6f,0),new Vector3(.8f,.9f,.06f),TownColor.Blue);
                if(type=="parking")TownArchitecturalParts.Text(m,new Vector3(0,2.6f,.035f),.55f,.66f,"P",TownColor.White);
                else
                {
                    m.Box(new Vector3(0,2.55f,.04f),new Vector3(.1f,.48f,.015f),TownColor.White);
                    m.Triangle(new Vector3(-.25f,2.72f,.05f),new Vector3(.25f,2.72f,.05f),new Vector3(0,2.94f,.05f),TownColor.White);
                }
            }
            return m;
        }

        public static TownMeshBuilder Barrier()
        {
            var m=new TownMeshBuilder();m.Box(new Vector3(-2.7f,.75f,0),new Vector3(.55f,1.5f,.65f),TownColor.Orange);
            m.Box(new Vector3(0,1.28f,0),new Vector3(5.6f,.14f,.16f),TownColor.White);
            for(int i=0;i<8;i++)m.Box(new Vector3(-2.4f+i*.65f,1.28f,.086f),new Vector3(.3f,.14f,.012f),TownColor.Red);
            m.Box(new Vector3(2.7f,.6f,0),new Vector3(.1f,1.2f,.1f),TownColor.Frame);return m;
        }
    }
}
