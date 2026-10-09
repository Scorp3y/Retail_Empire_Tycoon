using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>Twelve-metre road modules with an eight-metre carriageway and matching end profiles.</summary>
    internal static class TownRoads
    {
        public static TownMeshBuilder Road(string shape)
        {
            var m=new TownMeshBuilder();m.Ground(0,0,12,12,0,TownColor.Grass);
            if(shape=="corner")
            {
                m.Arc(new Vector3(6,.03f,6),2,10,180,270,TownColor.Asphalt);
                m.Arc(new Vector3(6,.13f,6),10,12,180,270,TownColor.Pavement);
                m.Arc(new Vector3(6,.13f,6),0,2,180,270,TownColor.Pavement);
                for(int i=0;i<7;i++)m.Arc(new Vector3(6,.035f,6),5.94f,6.06f,183+i*12,189+i*12,TownColor.White,2);
                m.Arc(new Vector3(6,.035f,6),2.2f,2.26f,180,270,TownColor.White);
                m.Arc(new Vector3(6,.035f,6),9.74f,9.8f,180,270,TownColor.White);
                return m;
            }
            if(shape=="deadend")
            {
                m.Ground(0,-3,8,6,.03f,TownColor.Asphalt);
                m.Cylinder(new Vector3(0,.015f,1),5,5,.03f,TownColor.Asphalt,16);
                m.Arc(new Vector3(0,.13f,1),5,5.8f,0,360,TownColor.Pavement,24);
                m.Ground(0,-4.9f,8,2.2f,.031f,TownColor.Asphalt);
                return m;
            }
            m.Ground(0,0,8,12,.03f,TownColor.Asphalt);
            if(shape=="cross"||shape=="tee")
            {
                m.Ground(-5,0,2,8,.03f,TownColor.Asphalt);m.Ground(5,0,2,8,.03f,TownColor.Asphalt);
                if(shape=="tee")m.Ground(0,5,8,2,.13f,TownColor.Pavement);
                for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)
                    m.Ground(sx*5,sz*5,2,2,.13f,TownColor.Pavement);
                for(int i=0;i<2;i++)
                {
                    m.Ground(0,-5.5f+i, .12f,.5f,.035f,TownColor.White);
                    if(shape=="cross")m.Ground(0,5.5f-i,.12f,.5f,.035f,TownColor.White);
                    m.Ground(-5.5f+i,0,.5f,.12f,.035f,TownColor.White);m.Ground(5.5f-i,0,.5f,.12f,.035f,TownColor.White);
                }
            }
            else
            {
                foreach(float x in new[]{-5f,5f})m.Box(new Vector3(x,.065f,0),new Vector3(2,.13f,12),TownColor.Pavement);
                for(int i=0;i<8;i++)m.Ground(0,-5.2f+i*1.5f,.12f,.8f,.035f,TownColor.White);
                foreach(float x in new[]{-3.75f,3.75f})m.Ground(x,0,.065f,12,.035f,TownColor.White);
            }
            return m;
        }

        public static TownMeshBuilder Parking(int spaces,bool personal=false)
        {
            var m=new TownMeshBuilder();float width=spaces*2.8f;
            m.Box(new Vector3(0,.025f,0),new Vector3(width,.05f,5.6f),TownColor.Asphalt);
            var paint=personal?TownColor.Yellow:TownColor.White;
            for(int i=0;i<=spaces;i++)m.Ground(-width/2+i*2.8f,0,.075f,5.6f,.053f,paint);
            m.Ground(0,-2.8f,width,.075f,.053f,paint);
            foreach(float x in new[]{-width/2+.45f,width/2-.45f})m.Box(new Vector3(x,.12f,-2.4f),new Vector3(.8f,.17f,.2f),TownColor.Pavement);
            if(personal)m.Ground(0,0,1.4f,.15f,.054f,TownColor.Yellow);
            return m;
        }

        public static TownMeshBuilder StopZone()
        {
            var m=new TownMeshBuilder();m.Ground(0,0,4,7,.015f,TownColor.Asphalt);
            foreach(float x in new[]{-1.95f,1.95f})m.Ground(x,0,.08f,7,.02f,TownColor.Yellow);
            foreach(float z in new[]{-3.45f,3.45f})m.Ground(0,z,4,.08f,.02f,TownColor.Yellow);
            for(int i=0;i<6;i++)m.Box(new Vector3(0,.02f,-2.2f+i*.85f),new Vector3(3.5f,.004f,.08f),TownColor.Yellow,Quaternion.Euler(0,25,0));
            return m;
        }

        public static TownMeshBuilder Sidewalk(bool corner)
        {
            var m=new TownMeshBuilder();
            m.Box(new Vector3(0,.065f,0),new Vector3(corner?4:2,.13f,corner?4:12),TownColor.Pavement);
            float depth=corner?4:12;
            for(float z=-depth/2+1;z<depth/2;z++)m.Ground(0,z,corner?4:2,.02f,.133f,TownColor.Stone);
            if(corner)m.Ground(0,0,.02f,4,.133f,TownColor.Stone);
            return m;
        }

        public static TownMeshBuilder Plaza()
        {
            var m=new TownMeshBuilder();m.Box(new Vector3(0,.025f,0),new Vector3(4,.05f,4),TownColor.Pavement);
            for(int x=-1;x<=1;x++)m.Ground(x,0,.025f,4,.053f,TownColor.Stone);
            for(int z=-1;z<=1;z++)m.Ground(0,z,4,.025f,.053f,TownColor.Stone);return m;
        }

        public static TownMeshBuilder Crossing()
        {
            var m=new TownMeshBuilder();for(int i=0;i<10;i++)m.Ground(-3.6f+i*.8f,0,.43f,3,.012f,TownColor.White);return m;
        }
    }
}
