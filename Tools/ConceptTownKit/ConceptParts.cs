using System;
using UnityEngine;
using RetailEmpireTycoon.Editor.TownKit;

namespace RetailEmpireTycoon.Editor.ConceptTownKit
{
    /// <summary>Opaque architecture and optional glazing share the same origin and metre scale.</summary>
    internal sealed class ConceptGeometry
    {
        public TownMeshBuilder Solid { get; } = new TownMeshBuilder();
        public TownMeshBuilder Glass { get; } = new TownMeshBuilder();
        public bool HasGlass { get; private set; }

        public void GlassPanel(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Glass.Quad(a,b,c,d,TownColor.Glass);
            HasGlass=true;
        }
    }

    internal sealed class ConceptDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public Func<ConceptGeometry> Build { get; }
        public ConceptDefinition(string id,string name,Func<ConceptGeometry> build)
        { Id=id; Name=name; Build=build; }
    }

    internal static class ConceptParts
    {
        public static Vector3 P(float x,float y,float z)=>new Vector3(x,y,z);

        public static void Box(TownMeshBuilder m,Vector3 p,float w,float h,float d,TownColor color)
            =>m.Box(p,new Vector3(w,h,d),color);

        public static void Beam(TownMeshBuilder m,Vector3 a,Vector3 b,float width,TownColor color)
        {
            var direction=b-a;
            if(direction.sqrMagnitude<.00001f)throw new ArgumentException("Beam endpoints coincide.");
            m.Box((a+b)*.5f,new Vector3(width,direction.magnitude,width),color,
                Quaternion.FromToRotation(Vector3.up,direction));
        }

        public static void Pad(TownMeshBuilder m,float width,float depth,bool rural=false)
        {
            Box(m,P(0,.12f,0),width,.24f,depth,rural?TownColor.Grass:TownColor.Pavement);
            if(rural)return;
            // Inlaid joints remain visible at the town camera distance without a texture dependency.
            for(float x=-width/2+2;x<width/2;x+=2)
                Box(m,P(x,.243f,0),.015f,.006f,depth,TownColor.Stone);
            for(float z=-depth/2+2;z<depth/2;z+=2)
                Box(m,P(0,.243f,z),width,.006f,.015f,TownColor.Stone);
        }

        public static void WallBody(TownMeshBuilder m,Vector3 origin,float w,float h,float d,TownColor color)
        {
            Box(m,origin+P(0,h/2,0),w,h,d,color);
            Box(m,origin+P(0,.2f,0),w+.1f,.4f,d+.1f,TownColor.Stone);
            foreach(float x in new[]{-w/2+.12f,w/2-.12f})
                Box(m,origin+P(x,h/2,d/2+.035f),.24f,h,.1f,TownColor.White);
        }

        public static void HipRoof(TownMeshBuilder m,Vector3 origin,float w,float d,float rise)
        {
            float ridge=Mathf.Max(.2f,d-w*.7f)/2;
            var lf=origin+P(-w/2,0,d/2);var rf=origin+P(w/2,0,d/2);
            var lb=origin+P(-w/2,0,-d/2);var rb=origin+P(w/2,0,-d/2);
            var f=origin+P(0,rise,ridge);var b=origin+P(0,rise,-ridge);
            m.Triangle(lf,rf,f,TownColor.Mint);m.Triangle(rb,lb,b,TownColor.Mint);
            m.Quad(rf,rb,b,f,TownColor.Mint);m.Quad(lb,lf,f,b,TownColor.Mint);
            Box(m,origin+P(0,-.08f,0),w,.16f,d,TownColor.White);
            Beam(m,f,b,.13f,TownColor.Mint);
            // Standing-seam roofing defines the roof planes, instead of relying on painted detail.
            for(int i=1;i<13;i++)
            {
                float z=-d/2+i*d/13;
                float hip=Mathf.Clamp01((Mathf.Abs(z)-ridge)/(d/2-ridge));
                foreach(float side in new[]{-1f,1f})
                    Beam(m,origin+P(side*w/2,.022f,z),origin+P(side*w/2*hip,rise*(1-hip)+.022f,z),.025f,TownColor.GreenStripe);
            }
        }

        public static void GableRoof(TownMeshBuilder m,Vector3 origin,float w,float d,float rise,TownColor end)
        {
            m.Gable(origin,w,d,rise,TownColor.Mint,end);
            Box(m,origin+P(0,-.07f,0),w,.14f,d,TownColor.White);
            for(int i=0;i<=12;i++)
            {
                float z=-d/2+i*d/12;
                Beam(m,origin+P(-w/2,.025f,z),origin+P(0,rise+.025f,z),.025f,TownColor.GreenStripe);
                Beam(m,origin+P(w/2,.025f,z),origin+P(0,rise+.025f,z),.025f,TownColor.GreenStripe);
            }
            Beam(m,origin+P(0,rise+.04f,-d/2),origin+P(0,rise+.04f,d/2),.1f,TownColor.Mint);
        }

        public static void Window(TownMeshBuilder m,Vector3 center,float w=1.3f,float h=1.5f,bool side=false,bool reverse=false)
        {
            var rotation=Quaternion.Euler(0,side?(reverse?-90:90):(reverse?180:0),0);
            m.Box(center,P(w+.14f,h+.14f,.1f),TownColor.White,rotation);
            m.Box(center+rotation*P(0,0,.065f),P(w,h,.025f),TownColor.Glass,rotation);
            m.Box(center+rotation*P(0,0,.085f),P(.045f,h,.03f),TownColor.Frame,rotation);
            m.Box(center+rotation*P(0,0,.085f),P(w,.045f,.03f),TownColor.Frame,rotation);
            m.Box(center+rotation*P(0,-h/2-.07f,.1f),P(w+.26f,.11f,.24f),TownColor.Pavement,rotation);
        }

        public static void Door(TownMeshBuilder m,Vector3 center,float w=1.3f,float h=2.3f)
            =>TownArchitecturalParts.Door(m,center,w,h,w>1.5f);

        public static void Sign(TownMeshBuilder m,Vector3 center,float w,string text)
            =>TownArchitecturalParts.Sign(m,center,w,.65f,text);

        public static void Awning(TownMeshBuilder m,Vector3 center,float width,TownColor color)
        {
            const int count=12;
            for(int i=0;i<count;i++)
            {
                float x=center.x-width/2+(i+.5f)*width/count;
                var c=i%2==0?color:TownColor.White;
                m.Box(P(x,center.y,center.z),P(width/count,.09f,1.35f),c,Quaternion.Euler(12,0,0));
                Box(m,P(x,center.y-.29f,center.z+.65f),width/count,.26f,.08f,c);
            }
            foreach(float x in new[]{-width/2+.1f,width/2-.1f})
                Beam(m,center+P(x,-.65f,-.58f),center+P(x,-.12f,.55f),.065f,TownColor.Frame);
        }

        public static void Planter(TownMeshBuilder m,Vector3 bottom,float width=1.2f)
        {
            Box(m,bottom+P(0,.2f,0),width,.4f,.65f,TownColor.Cream);
            Box(m,bottom+P(0,.405f,0),width-.12f,.025f,.52f,TownColor.Soil);
            for(int i=0;i<4;i++)
            {
                var p=bottom+P(-width*.34f+i*width*.23f,.6f,0);
                m.Ellipsoid(p,P(.22f,.3f,.24f),i%2==0?TownColor.Leaf:TownColor.LeafLight,6,3);
                TownNature.Flower(m,p+Vector3.up*.25f,.095f,i%2==0?TownColor.Flowers:TownColor.Pink);
            }
        }

        public static void Tree(TownMeshBuilder m,Vector3 p,float height=4.4f)
        {
            m.Cylinder(p+P(0,height*.3f,0),.16f,.1f,height*.6f,TownColor.Trunk,6);
            m.Ellipsoid(p+P(0,height*.66f,0),P(height*.23f,height*.38f,height*.23f),TownColor.Leaf,7,4);
            m.Ellipsoid(p+P(-height*.1f,height*.77f,.2f),P(height*.19f,height*.25f,height*.18f),TownColor.LeafLight,7,4);
        }

        public static void Lamp(TownMeshBuilder m,Vector3 bottom,float height=3.2f)
        {
            m.Cylinder(bottom+P(0,.09f,0),.16f,.16f,.18f,TownColor.Frame);
            m.Cylinder(bottom+P(0,height/2,0),.045f,.035f,height,TownColor.Frame,6);
            Box(m,bottom+P(0,height+.14f,0),.3f,.3f,.3f,TownColor.Light);
            m.Cylinder(bottom+P(0,height+.36f,0),.25f,0,.17f,TownColor.Frame,4,Quaternion.Euler(0,45,0));
            foreach(float x in new[]{-.16f,.16f})foreach(float z in new[]{-.16f,.16f})
                Box(m,bottom+P(x,height+.14f,z),.035f,.34f,.035f,TownColor.Frame);
        }

        public static void Bench(TownMeshBuilder m,Vector3 bottom,float width=2.3f)
        {
            foreach(float x in new[]{-width*.35f,width*.35f})
            {
                Box(m,bottom+P(x,.3f,0),.08f,.6f,.6f,TownColor.Frame);
                Box(m,bottom+P(x,.85f,-.27f),.07f,.65f,.07f,TownColor.Frame);
            }
            for(int i=0;i<4;i++)Box(m,bottom+P(0,.57f,-.22f+i*.14f),width,.08f,.1f,TownColor.WoodLight);
            for(int i=0;i<3;i++)Box(m,bottom+P(0,.8f+i*.15f,-.28f),width,.1f,.07f,TownColor.WoodLight);
        }

        public static void Rail(TownMeshBuilder m,Vector3 a,Vector3 b,float h,TownColor color=TownColor.Frame)
        {
            Beam(m,a+P(0,h,0),b+P(0,h,0),.065f,color);
            Beam(m,a+P(0,.18f,0),b+P(0,.18f,0),.05f,color);
            int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.32f));
            for(int i=0;i<=count;i++)Beam(m,Vector3.Lerp(a,b,(float)i/count),Vector3.Lerp(a,b,(float)i/count)+P(0,h,0),.045f,color);
        }

        public static void WoodenFence(TownMeshBuilder m,Vector3 a,Vector3 b)
        {
            for(int level=0;level<2;level++)Beam(m,a+P(0,.45f+level*.5f,0),b+P(0,.45f+level*.5f,0),.11f,TownColor.WoodLight);
            int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/2));
            for(int i=0;i<=count;i++)Box(m,Vector3.Lerp(a,b,(float)i/count)+P(0,.65f,0),.17f,1.3f,.17f,TownColor.Wood);
        }

        public static void Chimney(TownMeshBuilder m,Vector3 p)
        {
            Box(m,p,.55f,1.2f,.65f,TownColor.Cream);
            Box(m,p+P(0,.65f,0),.7f,.1f,.8f,TownColor.Stone);
            Box(m,p+P(0,.71f,0),.4f,.03f,.5f,TownColor.Charcoal);
        }

        public static void RoofVent(TownMeshBuilder m,Vector3 bottom)
        {
            Box(m,bottom+P(0,.3f,0),1.1f,.6f,.95f,TownColor.Metal);
            m.Cylinder(bottom+P(0,.61f,0),.33f,.33f,.03f,TownColor.Frame,10);
            for(int i=-2;i<=2;i++)Box(m,bottom+P(i*.12f,.64f,0),.04f,.02f,.6f,TownColor.Stone);
        }

        public static void Add(TownMeshBuilder target,TownMeshBuilder source,Vector3 offset)
        {
            var mesh=source.Build("temporary component");
            try
            {
                var v=mesh.vertices;var uv=mesh.uv;var t=mesh.triangles;
                for(int i=0;i<t.Length;i+=3)
                {
                    int index=Mathf.Clamp(Mathf.FloorToInt(uv[t[i]].x*TownPalette.Colors.Length),0,TownPalette.Colors.Length-1);
                    target.Triangle(v[t[i]]+offset,v[t[i+1]]+offset,v[t[i+2]]+offset,(TownColor)index);
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
