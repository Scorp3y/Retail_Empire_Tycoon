using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Serializable, repeatable visual repair contract. Existing templates and all other native geometry stay read-only.</summary>
[Serializable]
internal sealed class TownSeamReplacement
{
    public string id,kind,model;
    public Matrix4x4 originalPose,pose;
    public Vector3[] roofCorners;
    public float ground,top,plate;
    public float braceLeft,braceRight;
    public Rect area,scope;
    [NonSerialized] public List<TownSeamGeometry.Triangle> originalTriangles;
    public Mesh Build()
    {
        var m=new TownMeshBuilder();var parts=new List<CombineInstance>();
        if(kind=="Roof")
        {
            parts.Add(new CombineInstance{mesh=TownSeamGeometry.Template(model),transform=pose});
            var bottom=roofCorners.Select(p=>p-Vector3.up*.012f).ToArray();
            var cap=bottom.Select(p=>new Vector3(p.x,pose.m13,p.z)).ToArray();
            for(int i=0;i<4;i++){int n=(i+1)%4;m.Quad(bottom[i],bottom[n],cap[n],cap[i],TownColor.Metal);}
            m.Quad(cap[0],cap[1],cap[2],cap[3],TownColor.Metal);
        }
        else if(kind=="Steps")
        {
            float rise=(top-ground)/3;
            if(rise<.06f||rise>.2f)throw new InvalidOperationException(id+": unsafe or unexplained stair rise.");
            // Native door sill ends 0.98m beyond the wall; join it, without coplanar overlapping top faces.
            for(int i=0;i<3;i++)m.Box(new Vector3(0,ground+rise*(i+1)/2,.73f-.34f*i),new Vector3(2.5f,rise*(i+1),.34f),TownColor.Pavement);
            foreach(float x in new[]{-1.2f,1.2f})
            {
                Beam(m,new Vector3(x,ground,.8f),new Vector3(x,ground+1,.8f),.04f);
                Beam(m,new Vector3(x,ground,-.09f),new Vector3(x,top+1,-.09f),.04f);
                Beam(m,new Vector3(x,ground+1,.8f),new Vector3(x,top+1,-.09f),.045f);
            }
        }
        else if(kind=="Canopy")
        {
            m.Box(new Vector3(0,plate,0),new Vector3(3.6f,.2f,1.7f),TownColor.Mint);
            foreach(float x in new[]{braceLeft,braceRight})
            {
                Beam(m,new Vector3(x,plate-.75f,-.82f),new Vector3(x,plate-.1f,.65f),.055f);
                Beam(m,new Vector3(x,plate-.1f,-.82f),new Vector3(x,plate-.1f,.65f),.055f);
            }
        }
        else if(kind=="Pipe")
        {
            float height=top-ground;
            m.Cylinder(new Vector3(0,ground+height/2,0),.06f,.06f,height,TownColor.Frame,6);
            for(int i=1;i<=4;i++)m.Box(new Vector3(0,ground+height*i/5,-.095f),new Vector3(.18f,.055f,.22f),TownColor.Metal);
            Beam(m,new Vector3(0,ground+.15f,0),new Vector3(0,ground+.08f,.25f),.12f);
        }
        else if(kind=="Platform")m.Ground(area.center.x,area.center.y,area.width,area.height,ground,TownColor.Pavement);
        else throw new InvalidOperationException("Unknown repair kind: "+kind);
        var drawn=m.Build(id);
        parts.Add(new CombineInstance{mesh=drawn,transform=kind=="Roof"||kind=="Platform"?Matrix4x4.identity:pose});
        try
        {
            var mesh=new Mesh{name=id,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.ToArray(),true,true);
            mesh.uv2=mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();return mesh;
        }
        finally{UnityEngine.Object.DestroyImmediate(drawn);}
    }
    public void MeasureScope()
    {
        var before=originalTriangles.SelectMany(t=>new[]{t.A,t.B,t.C});var mesh=Build();
        try{var p=before.Concat(mesh.vertices).ToArray();scope=Rect.MinMaxRect(p.Min(v=>v.x)-.03f,p.Min(v=>v.z)-.03f,p.Max(v=>v.x)+.03f,p.Max(v=>v.z)+.03f);}
        finally{UnityEngine.Object.DestroyImmediate(mesh);}
    }
    private static void Beam(TownMeshBuilder m,Vector3 from,Vector3 to,float width)
    {var delta=to-from;m.Box((from+to)/2,new Vector3(width,delta.magnitude,width),TownColor.Frame,Quaternion.FromToRotation(Vector3.up,delta.normalized));}
}
