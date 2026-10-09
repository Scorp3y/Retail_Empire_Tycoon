using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>Flat-shaded geometry with one palette material, including all architectural detail.</summary>
    internal sealed class TownMeshBuilder
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<Vector3> normals = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<int> triangles = new();

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, TownColor color)
        {
            var cross = Vector3.Cross(b-a,c-a);
            if (cross.sqrMagnitude < .00000000001f) return;
            int first = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            var normal = cross.normalized;
            var uv = TownPalette.UV(color);
            for (int i=0;i<3;i++) { normals.Add(normal); uvs.Add(uv); triangles.Add(first+i); }
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, TownColor color)
        {
            Triangle(a,b,c,color); Triangle(a,c,d,color);
        }

        public void Ground(float x, float z, float width, float depth, float height, TownColor color)
        {
            Quad(new Vector3(x-width/2,height,z+depth/2),new Vector3(x+width/2,height,z+depth/2),
                new Vector3(x+width/2,height,z-depth/2),new Vector3(x-width/2,height,z-depth/2),color);
        }

        public void Box(Vector3 center, Vector3 size, TownColor color, Quaternion rotation = default)
        {
            if (rotation == default) rotation = Quaternion.identity;
            Vector3 h = size*.5f;
            Vector3 P(float x,float y,float z) => center+rotation*Vector3.Scale(new Vector3(x,y,z),h);
            Quad(P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1),color);
            Quad(P(1,-1,-1),P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),color);
            Quad(P(1,-1,1),P(1,-1,-1),P(1,1,-1),P(1,1,1),color);
            Quad(P(-1,-1,-1),P(-1,-1,1),P(-1,1,1),P(-1,1,-1),color);
            Quad(P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1),color);
            Quad(P(-1,-1,-1),P(1,-1,-1),P(1,-1,1),P(-1,-1,1),color);
        }

        public void Cylinder(Vector3 center,float bottomRadius,float topRadius,float height,TownColor color,int sides=8,Quaternion rotation=default)
        {
            if (rotation==default) rotation=Quaternion.identity;
            Vector3 Point(int i,float radius,float y) => center+rotation*new Vector3(Mathf.Cos(i*Mathf.PI*2/sides)*radius,y,Mathf.Sin(i*Mathf.PI*2/sides)*radius);
            var bottom=center+rotation*Vector3.down*height*.5f;
            var top=center+rotation*Vector3.up*height*.5f;
            for(int i=0;i<sides;i++)
            {
                var a=Point(i,bottomRadius,-height*.5f);var b=Point(i,topRadius,height*.5f);
                var c=Point(i+1,topRadius,height*.5f);var d=Point(i+1,bottomRadius,-height*.5f);
                Quad(a,b,c,d,color); Triangle(bottom,a,d,color); Triangle(top,c,b,color);
            }
        }

        public void Ellipsoid(Vector3 center,Vector3 radius,TownColor color,int sides=8,int rings=4)
        {
            Vector3 P(int ring,int side)
            {
                float theta=ring*Mathf.PI/rings, phi=side*Mathf.PI*2/sides;
                return center+Vector3.Scale(new Vector3(Mathf.Sin(theta)*Mathf.Cos(phi),Mathf.Cos(theta),Mathf.Sin(theta)*Mathf.Sin(phi)),radius);
            }
            for(int ring=0;ring<rings;ring++) for(int side=0;side<sides;side++)
                Quad(P(ring,side),P(ring,side+1),P(ring+1,side+1),P(ring+1,side),color);
        }

        public void Gable(Vector3 baseCenter,float width,float depth,float rise,TownColor roof,TownColor end)
        {
            var a=baseCenter+new Vector3(-width/2,0,depth/2);var b=baseCenter+new Vector3(width/2,0,depth/2);
            var c=baseCenter+new Vector3(0,rise,depth/2);var d=baseCenter+new Vector3(-width/2,0,-depth/2);
            var e=baseCenter+new Vector3(width/2,0,-depth/2);var f=baseCenter+new Vector3(0,rise,-depth/2);
            Triangle(a,b,c,end);Triangle(e,d,f,end);
            Quad(b,e,f,c,roof);Quad(d,a,c,f,roof);Quad(d,e,b,a,roof);
        }

        public void Arc(Vector3 center,float inner,float outer,float start,float end,TownColor color,int steps=16)
        {
            Vector3 P(float angle,float radius) => center+new Vector3(Mathf.Cos(angle*Mathf.Deg2Rad)*radius,0,Mathf.Sin(angle*Mathf.Deg2Rad)*radius);
            for(int i=0;i<steps;i++)
            {
                float a=Mathf.Lerp(start,end,(float)i/steps),b=Mathf.Lerp(start,end,(float)(i+1)/steps);
                Quad(P(a,inner),P(b,inner),P(b,outer),P(a,outer),color);
            }
        }

        public void ExtrudeX(Vector3 center,float width,Vector2[] profile,TownColor color)
        {
            // Profile is clockwise in the Y/Z plane as seen from +X.
            Vector3 P(int index,float x)=>center+new Vector3(x,profile[index].x,profile[index].y);
            for(int i=1;i<profile.Length-1;i++)
            {
                Triangle(P(0,width/2),P(i,width/2),P(i+1,width/2),color);
                Triangle(P(0,-width/2),P(i+1,-width/2),P(i,-width/2),color);
            }
            for(int i=0;i<profile.Length;i++)
            {
                int next=(i+1)%profile.Length;
                Quad(P(i,-width/2),P(next,-width/2),P(next,width/2),P(i,width/2),color);
            }
        }

        public Mesh Build(string name)
        {
            var mesh=new Mesh {name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);
            mesh.RecalculateBounds();return mesh;
        }
    }
}
