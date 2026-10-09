using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>Visual vehicle shells only. Forward is +Z; no driving or cargo logic is attached.</summary>
    internal static class TownVehicles
    {
        private static void Wheels(TownMeshBuilder m,float width,float wheelbase,float radius=.42f)
        {
            for(int side=-1;side<=1;side+=2)for(int axle=-1;axle<=1;axle+=2)
            {
                var position=new Vector3(side*width/2,radius,axle*wheelbase/2);
                m.Cylinder(position,radius,radius,.25f,TownColor.Rubber,10,Quaternion.Euler(0,0,90));
                m.Cylinder(position+Vector3.right*side*.14f,radius*.55f,radius*.55f,.025f,TownColor.Metal,8,Quaternion.Euler(0,0,90));
                m.Cylinder(position+Vector3.right*side*.16f,radius*.2f,radius*.2f,.03f,TownColor.Frame,6,Quaternion.Euler(0,0,90));
            }
        }

        private static void Front(TownMeshBuilder m,float width,float front,float y)
        {
            m.Box(new Vector3(0,y-.2f,front+.08f),new Vector3(width+.08f,.2f,.22f),TownColor.Metal);
            m.Box(new Vector3(0,y,front+.04f),new Vector3(width*.42f,.23f,.05f),TownColor.Frame);
            foreach(float x in new[]{-width*.34f,width*.34f})m.Box(new Vector3(x,y+.04f,front+.075f),new Vector3(width*.22f,.2f,.045f),TownColor.Light);
            m.Box(new Vector3(0,y-.15f,front+.205f),new Vector3(.4f,.13f,.01f),TownColor.White);
        }

        private static void Windshield(TownMeshBuilder m,float width,float lowerY,float lowerZ,float upperY,float upperZ,bool rear=false)
        {
            // The panel follows the opaque body plane, with a small offset to prevent depth flicker.
            float offset=rear?-.012f:.012f;
            var a=new Vector3(-width/2,lowerY,lowerZ+offset);
            var b=new Vector3(width/2,lowerY,lowerZ+offset);
            var c=new Vector3(width/2,upperY,upperZ+offset);
            var d=new Vector3(-width/2,upperY,upperZ+offset);
            if(rear)m.Quad(b,a,d,c,TownColor.Glass);
            else m.Quad(a,b,c,d,TownColor.Glass);
        }

        public static TownMeshBuilder Pickup()
        {
            var m=new TownMeshBuilder();Wheels(m,1.85f,3.1f);
            m.Box(new Vector3(0,.7f,0),new Vector3(1.92f,.5f,4.4f),TownColor.Orange);
            var cabin=new[]{new Vector2(.95f,-.3f),new Vector2(1.68f,-.3f),new Vector2(1.8f,.95f),new Vector2(.95f,1.55f)};
            m.ExtrudeX(Vector3.zero,1.85f,cabin,TownColor.Orange);
            var glass=new[]{new Vector2(1.12f,-.13f),new Vector2(1.57f,-.13f),new Vector2(1.65f,.88f),new Vector2(1.12f,1.27f)};
            m.ExtrudeX(Vector3.zero,1.87f,glass,TownColor.Glass);
            Windshield(m,1.5f,1.15f,1.40882f,1.64f,1.06294f);
            Windshield(m,1.45f,1.15f,-.3f,1.52f,-.3f,true);
            m.Box(new Vector3(0,1.77f,.35f),new Vector3(1.94f,.12f,1.32f),TownColor.Orange);
            m.Box(new Vector3(0,.98f,-1.27f),new Vector3(1.6f,.07f,1.82f),TownColor.Frame);
            foreach(float x in new[]{-.9f,.9f})m.Box(new Vector3(x,1.17f,-1.25f),new Vector3(.14f,.48f,1.94f),TownColor.Orange);
            m.Box(new Vector3(0,1.17f,-2.15f),new Vector3(1.82f,.48f,.12f),TownColor.Orange);
            foreach(float x in new[]{-.77f,.77f})m.Box(new Vector3(x,1.18f,-2.23f),new Vector3(.18f,.28f,.035f),TownColor.Red);
            m.Box(new Vector3(0,.53f,-2.25f),new Vector3(1.9f,.15f,.17f),TownColor.Metal);
            Front(m,1.92f,2.2f,.73f);return m;
        }

        public static TownMeshBuilder Passenger(TownColor color,bool hatchback=false)
        {
            var m=new TownMeshBuilder();float length=hatchback?3.8f:4.4f;
            Wheels(m,1.75f,hatchback?2.45f:2.85f,.36f);
            m.Box(new Vector3(0,.64f,0),new Vector3(1.83f,.47f,length),color);
            var cabin=new[]{new Vector2(.87f,hatchback?-1.5f:-1.25f),new Vector2(1.55f,-.8f),new Vector2(1.57f,.55f),new Vector2(.87f,1.22f)};
            m.ExtrudeX(Vector3.zero,1.69f,cabin,color);
            var glass=new[]{new Vector2(1.03f,hatchback?-1.25f:-.95f),new Vector2(1.43f,-.72f),new Vector2(1.45f,.5f),new Vector2(1.03f,.98f)};
            m.ExtrudeX(Vector3.zero,1.71f,glass,TownColor.Glass);
            Windshield(m,1.45f,1.02f,1.07643f,1.43f,.684f);
            float rearBase=hatchback?-1.5f:-1.25f;
            float rearLower=Mathf.Lerp(rearBase,-.8f,(1.02f-.87f)/.68f);
            float rearUpper=Mathf.Lerp(rearBase,-.8f,(1.4f-.87f)/.68f);
            Windshield(m,1.4f,1.02f,rearLower,1.4f,rearUpper,true);
            m.Box(new Vector3(0,1.54f,-.1f),new Vector3(1.76f,.1f,1.25f),color);
            foreach(float x in new[]{-.87f,.87f})
            {
                m.Box(new Vector3(x,1.24f,-.16f),new Vector3(.055f,.55f,.075f),color);
                m.Box(new Vector3(x,.91f,-.35f),new Vector3(.06f,.04f,.22f),TownColor.Frame);
                m.Box(new Vector3(x*1.14f,1.17f,.72f),new Vector3(.17f,.13f,.2f),color);
            }
            Front(m,1.83f,length/2,.64f);
            foreach(float x in new[]{-.65f,.65f})m.Box(new Vector3(x,.72f,-length/2-.025f),new Vector3(.4f,.2f,.045f),TownColor.Red);
            return m;
        }

        public static TownMeshBuilder Truck()
        {
            var m=new TownMeshBuilder();Wheels(m,2.2f,4.7f,.48f);
            m.Box(new Vector3(0,.7f,0),new Vector3(2.25f,.4f,6.6f),TownColor.Frame);
            m.Box(new Vector3(0,2.08f,-.95f),new Vector3(2.5f,2.55f,4.6f),TownColor.White);
            m.Box(new Vector3(0,1.3f,2.4f),new Vector3(2.25f,1.8f,1.8f),TownColor.Orange);
            m.Box(new Vector3(0,1.8f,3.32f),new Vector3(1.9f,.65f,.04f),TownColor.Glass);
            foreach(float x in new[]{-1.145f,1.145f})m.Box(new Vector3(x,1.8f,2.5f),new Vector3(.04f,.62f,1.1f),TownColor.Glass);
            m.Box(new Vector3(0,2.22f,2.4f),new Vector3(2.35f,.15f,1.95f),TownColor.Orange);
            TownArchitecturalParts.LoadingDoor(m,new Vector3(0,2,-3.34f),2.2f,2.3f,true,bollards:false);
            Front(m,2.25f,3.3f,1.05f);return m;
        }

        public static TownMeshBuilder Forklift()
        {
            var m=new TownMeshBuilder();Wheels(m,1.1f,1.55f,.25f);
            m.Box(new Vector3(0,.55f,-.2f),new Vector3(1.25f,.65f,1.8f),TownColor.Yellow);
            m.Box(new Vector3(0,1,-.25f),new Vector3(.6f,.15f,.6f),TownColor.Rubber);
            m.Box(new Vector3(0,1.28f,-.55f),new Vector3(.6f,.65f,.13f),TownColor.Rubber);
            foreach(float x in new[]{-.6f,.6f})foreach(float z in new[]{-.9f,.6f})m.Box(new Vector3(x,1.6f,z),new Vector3(.065f,1.5f,.065f),TownColor.Frame);
            m.Box(new Vector3(0,2.35f,-.15f),new Vector3(1.4f,.12f,1.8f),TownColor.Frame);
            foreach(float x in new[]{-.4f,.4f})
            {
                m.Box(new Vector3(x,1.45f,1),new Vector3(.12f,2.8f,.15f),TownColor.Frame);
                m.Box(new Vector3(x,.12f,1.7f),new Vector3(.15f,.1f,1.3f),TownColor.Metal);
            }
            m.Box(new Vector3(0,.38f,1.1f),new Vector3(1,.55f,.14f),TownColor.Metal);return m;
        }

        public static TownMeshBuilder Cargo()
        {
            var m=new TownMeshBuilder();for(int x=0;x<2;x++)for(int z=0;z<2;z++)TownProps.CardboardBox(m,new Vector3(-.35f+x*.7f,0,-.42f+z*.84f),new Vector3(.62f,.5f,.74f));
            TownProps.CardboardBox(m,new Vector3(0,.5f,0),new Vector3(.7f,.38f,.7f));return m;
        }
    }
}
