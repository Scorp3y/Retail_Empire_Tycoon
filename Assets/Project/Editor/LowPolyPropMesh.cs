using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
internal sealed class LowPolyPropMesh
{
    private readonly string assetFolder;
    public LowPolyPropMesh(string assetFolder){this.assetFolder=assetFolder;}
    private readonly List<Vector3> vertices=new();
    private readonly List<int>[] triangles=Enumerable.Range(0,6).Select(_=>new List<int>()).ToArray();
    public void Triangle(Vector3 a,Vector3 b,Vector3 c,int material)
    {int first=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles[material].AddRange(new[]{first,first+1,first+2});}
    public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material){Triangle(a,b,c,material);Triangle(a,c,d,material);}
    public void TopRect(Vector3 center,float width,float depth,int material)
    {
        var x=Vector3.right*(width*.5f);var z=Vector3.forward*(depth*.5f);
        Quad(center-x-z,center-x+z,center+x+z,center+x-z,material);
    }
    public void BeveledBox(Vector3 center,Vector3 size,float bevel,int material)
    {
        var half=size*.5f;
        Profile(new[]{new Vector2(center.z-half.z,center.y-half.y),new Vector2(center.z+half.z,center.y-half.y),
            new Vector2(center.z+half.z,center.y+half.y),new Vector2(center.z-half.z,center.y+half.y)},center.x,size.x,
            Mathf.Min(bevel,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.24f),material);
    }
    public void Box(Vector3 center,Vector3 size,int material)
    {
        var h=size*.5f;Vector3 P(float x,float y,float z)=>center+new Vector3(x*h.x,y*h.y,z*h.z);
        Quad(P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1),material);
        Quad(P(1,-1,1),P(1,1,1),P(-1,1,1),P(-1,-1,1),material);
        Quad(P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1),material);
        Quad(P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1),P(1,-1,1),material);
        Quad(P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1),material);
        Quad(P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1),material);
    }
    public void Profile(Vector2[] source,float x,float thickness,float bevel,int material)
    {
        var profile=(Vector2[])source.Clone();
        float area=0;for(int i=0;i<profile.Length;i++)area+=Cross(profile[i],profile[(i+1)%profile.Length]);
        if(area<0)Array.Reverse(profile);
        var center=profile.Aggregate(Vector2.zero,(sum,p)=>sum+p)/profile.Length;
        var inset=profile.Select(p=>Vector2.MoveTowards(p,center,bevel)).ToArray();
        Vector3 P(int ring,int index)=>new Vector3(x+(ring==0?-thickness*.5f:ring==1?-thickness*.5f+bevel:ring==2?thickness*.5f-bevel:thickness*.5f),
            (ring==0||ring==3?inset:profile)[index].y,(ring==0||ring==3?inset:profile)[index].x);
        var cap=Triangulate(inset);
        foreach(var tri in cap){Triangle(P(0,tri[0]),P(0,tri[1]),P(0,tri[2]),material);Triangle(P(3,tri[2]),P(3,tri[1]),P(3,tri[0]),material);}
        for(int ring=0;ring<3;ring++)for(int i=0;i<profile.Length;i++){int j=(i+1)%profile.Length;Quad(P(ring,i),P(ring+1,i),P(ring+1,j),P(ring,j),material);}
    }
    public void Handrail(Vector2[] path,float x,float width,float radius,int material)
    {
        var section=new[]{new Vector2(-1,-.45f),new Vector2(-.65f,-1),new Vector2(.65f,-1),new Vector2(1,-.45f),new Vector2(1,.45f),new Vector2(.65f,1),new Vector2(-.65f,1),new Vector2(-1,.45f)};
        Vector3 P(int i,int corner)
        {
            var tangent=(path[(i+1)%path.Length]-path[(i+path.Length-1)%path.Length]).normalized;
            var normal=new Vector2(-tangent.y,tangent.x);var p=path[i]+normal*section[corner].y*radius;
            return new Vector3(x+section[corner].x*width*.5f,p.y,p.x);
        }
        for(int i=0;i<path.Length;i++)for(int j=0;j<section.Length;j++)Quad(P(i,j),P(i,(j+1)%section.Length),P((i+1)%path.Length,(j+1)%section.Length),P((i+1)%path.Length,j),material);
    }
    public Mesh Save(string name)
    {
        string path=assetFolder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create=mesh==null;if(create)mesh=new Mesh{name=name};else mesh.Clear();
        // Update through Mesh APIs so re-authoring also refreshes Unity's live GPU buffers.
        mesh.SetVertices(vertices);mesh.subMeshCount=6;for(int i=0;i<6;i++)mesh.SetTriangles(triangles[i],i);
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);return mesh;
    }
    private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    private static List<int[]> Triangulate(Vector2[] points)
    {
        var remaining=Enumerable.Range(0,points.Length).ToList();var result=new List<int[]>();
        bool Inside(Vector2 p,Vector2 a,Vector2 b,Vector2 c)=>Cross(b-a,p-a)>=-1e-6f&&Cross(c-b,p-b)>=-1e-6f&&Cross(a-c,p-c)>=-1e-6f;
        while(remaining.Count>3)
        {
            bool cut=false;
            for(int i=0;i<remaining.Count;i++)
            {
                int a=remaining[(i+remaining.Count-1)%remaining.Count],b=remaining[i],c=remaining[(i+1)%remaining.Count];
                if(Cross(points[b]-points[a],points[c]-points[b])<=1e-7f || remaining.Any(p=>p!=a&&p!=b&&p!=c&&Inside(points[p],points[a],points[b],points[c])))continue;
                result.Add(new[]{a,b,c});remaining.RemoveAt(i);cut=true;break;
            }
            if(!cut)throw new InvalidOperationException("Cannot triangulate prop profile; check its contour.");
        }
        result.Add(remaining.ToArray());return result;
    }
}

