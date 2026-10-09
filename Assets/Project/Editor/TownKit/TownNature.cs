using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    internal static class TownNature
    {
        public static void Tree(TownMeshBuilder m,Vector3 bottom,int variant)
        {
            float height=variant==1?6.2f:variant==2?4.2f:4.8f;
            m.Cylinder(bottom+Vector3.up*height*.3f,.19f,.13f,height*.6f,TownColor.Trunk,6);
            var radius=variant==1?new Vector3(1.4f,2,1.4f):variant==2?new Vector3(2.1f,1.35f,1.7f):new Vector3(1.65f,1.65f,1.65f);
            m.Ellipsoid(bottom+Vector3.up*height*.67f,radius,TownColor.Leaf,8,6);
            m.Ellipsoid(bottom+new Vector3(-radius.x*.6f,height*.62f,.35f),radius*.72f,TownColor.LeafLight,7,5);
            m.Ellipsoid(bottom+new Vector3(radius.x*.42f,height*.8f,-.2f),radius*.6f,TownColor.LeafDark,7,5);
        }

        public static void Pine(TownMeshBuilder m,Vector3 bottom)
        {
            m.Cylinder(bottom+Vector3.up*1.5f,.16f,.11f,3,TownColor.Trunk,6);
            for(int tier=0;tier<4;tier++)m.Cylinder(bottom+Vector3.up*(2+tier*.85f),1.65f-tier*.33f,0,2.1f,TownColor.LeafDark,7);
        }

        public static TownMeshBuilder Bush(bool wide)
        {
            var m=new TownMeshBuilder();m.Ellipsoid(new Vector3(0,.65f,0),new Vector3(wide?1.3f:.8f,.7f,.75f),TownColor.Leaf,8,5);
            m.Ellipsoid(new Vector3(.35f,.95f,.1f),new Vector3(.6f,.55f,.55f),TownColor.LeafLight,7,5);return m;
        }

        public static TownMeshBuilder Hedge()
        {
            var m=new TownMeshBuilder();for(int i=0;i<5;i++)m.Ellipsoid(new Vector3(-.85f+i*.425f,.75f,0),new Vector3(.45f,.75f,.5f),i%2==0?TownColor.Leaf:TownColor.LeafLight,7,5);return m;
        }

        public static void Grass(TownMeshBuilder m,Vector3 bottom,bool flowers,int count=10)
        {
            for(int i=0;i<count;i++)
            {
                float angle=i*2.39996f,r=.13f*Mathf.Sqrt(i);
                Vector3 root=bottom+new Vector3(Mathf.Cos(angle)*r,0,Mathf.Sin(angle)*r);
                float height=.26f+(i%4)*.07f;
                var a=root+Vector3.left*.035f;var b=root+Vector3.right*.035f;var top=root+new Vector3(.09f,height,.04f);
                m.Triangle(a,b,top,TownColor.Leaf);m.Triangle(b,a,top,TownColor.Leaf);
                if(flowers&&i%3==0)Flower(m,top,.065f,i%2==0?TownColor.Pink:TownColor.Flowers);
            }
        }

        public static void Flower(TownMeshBuilder m,Vector3 center,float radius,TownColor color)
        {
            for(int petal=0;petal<5;petal++)
            {
                float angle=petal*Mathf.PI*2/5;
                var offset=new Vector3(Mathf.Cos(angle),.1f,Mathf.Sin(angle))*radius;
                m.Ellipsoid(center+offset,new Vector3(radius*.75f,radius*.3f,radius*.75f),color,5,4);
            }
            m.Ellipsoid(center+Vector3.up*radius*.12f,Vector3.one*radius*.4f,TownColor.Yellow,5,4);
        }

        public static TownMeshBuilder Rocks()
        {
            var m=new TownMeshBuilder();m.Ellipsoid(new Vector3(-.65f,.47f,0),new Vector3(.8f,.6f,.7f),TownColor.Stone,7,4);
            m.Ellipsoid(new Vector3(.6f,.3f,.3f),new Vector3(.6f,.4f,.48f),TownColor.Metal,6,4);
            m.Ellipsoid(new Vector3(.15f,.2f,-.6f),new Vector3(.35f,.3f,.4f),TownColor.Pavement,5,4);return m;
        }

        public static TownMeshBuilder Hill()
        {
            var m=new TownMeshBuilder();const int sides=10;
            var peak=new Vector3(-.4f,2.4f,.3f);
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                var p=new Vector3(Mathf.Cos(a)*5,0,Mathf.Sin(a)*4);
                var q=new Vector3(Mathf.Cos(b)*5,0,Mathf.Sin(b)*4);
                m.Triangle(q,p,peak,i%3==0?TownColor.LeafLight:TownColor.Grass);
            }
            return m;
        }

        public static TownMeshBuilder Meadow()
        {
            var m=new TownMeshBuilder();m.Ground(0,0,4,4,.003f,TownColor.Grass);
            for(int i=0;i<9;i++)Grass(m,new Vector3(-1.2f+(i%3)*1.2f,0,-1.2f+(i/3)*1.2f),true,6);return m;
        }

        public static TownMeshBuilder TreeBelt()
        {
            var m=new TownMeshBuilder();for(int i=0;i<6;i++)
            {
                var bottom=new Vector3(-5+i*2,0,(i%2==0?-.7f:.7f));
                if(i%2==0)Pine(m,bottom);else Tree(m,bottom,i%3);
            }
            return m;
        }
    }
}
