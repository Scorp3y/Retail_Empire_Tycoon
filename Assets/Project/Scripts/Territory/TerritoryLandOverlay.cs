using System.Collections.Generic;
using RetailEmpireTycoon.Territory;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Flat selection graphics. Never grants land rights or creates physical barriers.</summary>
public sealed class TerritoryLandOverlay : System.IDisposable
{
    private readonly GameObject root;
    private readonly Mesh mesh;
    private readonly Material material;
    private readonly List<Vector3> vertices = new();
    private readonly List<int> triangles = new();
    private readonly List<Color> colors = new();
    public GameObject Root => root;
    public TerritoryLandOverlay()
    {
        root = new GameObject("Land purchase ground outlines");
        mesh = new Mesh {name = "Land selection"};
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.AddComponent<MeshRenderer>();
        material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        material.SetFloat("_Surface", 1);
        material.SetFloat("_Blend", 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0);
        material.SetFloat("_Cull", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
    public void Refresh(TerritorySubdivisionRules rules, StoreProgression progression, TerritoryId? selected)
    {
        vertices.Clear(); triangles.Clear(); colors.Clear();
        var owned = new List<Rect> {rules.InitialBounds};
        foreach (var parcel in rules.Parcels)
            if (progression.State.IsPurchased(parcel.Id)) owned.Add(parcel.Bounds);
        foreach (var rect in owned) Outline(rect, new Color(.86f,.88f,.81f,.9f), .07f, owned);
        foreach (var parcel in rules.Parcels)
        {
            if (progression.State.IsPurchased(parcel.Id)) continue;
            bool active = selected == parcel.Id;
            bool available = progression.IsTerritoryAvailable(parcel.Id);
            if (!available && !active) continue;
            var color = available ? new Color(.82f,.62f,.23f,.95f) : new Color(.6f,.64f,.59f,.8f);
            Outline(parcel.Bounds, color, active ? .15f : .09f, null);
            if (active)
            {
                var rect = parcel.Bounds;
                Quad(new Vector3(rect.xMin,.04f,rect.yMin),new Vector3(rect.xMin,.04f,rect.yMax),
                    new Vector3(rect.xMax,.04f,rect.yMax),new Vector3(rect.xMax,.04f,rect.yMin),
                    new Color(color.r,color.g,color.b,.12f));
            }
        }
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
    }
    private void Outline(Rect rect, Color color, float width, List<Rect> owned)
    {
        var points = new[] {new Vector3(rect.xMin,.06f,rect.yMin), new Vector3(rect.xMax,.06f,rect.yMin),
            new Vector3(rect.xMax,.06f,rect.yMax),new Vector3(rect.xMin,.06f,rect.yMax)};
        // Split edges at every owned corner, removing interior seams even for unequal parcels.
        for (int edge=0;edge<4;edge++)
        {
            var a=points[edge];var b=points[(edge+1)%4];var direction=(b-a).normalized;float length=Vector3.Distance(a,b);
            var cuts=new List<float>{0,length};
            if (owned != null) foreach(var other in owned)
                foreach(var corner in new[]{new Vector3(other.xMin,0,other.yMin),new Vector3(other.xMax,0,other.yMax)})
                {float t=Vector3.Dot(corner-a,direction);if(t>0&&t<length)cuts.Add(t);}
            cuts.Sort();
            for(int i=1;i<cuts.Count;i++)
            {
                var start=a+direction*cuts[i-1];var end=a+direction*cuts[i];
                if(Vector3.Distance(start,end)<.001f)continue;
                var normal=Vector3.Cross(Vector3.up,direction);var middle=(start+end)*.5f;
                bool inside=false;
                if(owned!=null)foreach(var other in owned)
                {var test=middle+normal*.01f;if(other.Contains(new Vector2(test.x,test.z))){inside=true;break;}}
                if(inside)continue;
                var side=normal*width*.5f;
                Quad(start-side,end-side,end+side,start+side,color);
            }
        }
    }
    private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
    {
        int i=vertices.Count;vertices.AddRange(new[]{a,b,c,d});for(int n=0;n<4;n++)colors.Add(color);
        triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
    }
    public void Dispose() { Object.Destroy(root);Object.Destroy(mesh);Object.Destroy(material); }
}
