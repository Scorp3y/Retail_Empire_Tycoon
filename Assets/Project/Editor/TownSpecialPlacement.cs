using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Replaces authorised local triangles only; shared existing assets and the stop bay stay intact.</summary>
internal sealed class TownSpecialPlacement
{
    private readonly TownSpecialPlan plan;
    private readonly GameObject town;
    private readonly Material material;
    private readonly Dictionary<string,Mesh> templates=new Dictionary<string,Mesh>();
    public TownSpecialPlacement(TownSpecialPlan plan,GameObject town)
    {
        this.plan=plan;this.town=town;
        material=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
        if(material==null)throw new InvalidOperationException("Shared native town material is missing.");
    }

    public void Build()
    {
        foreach(var site in plan.Sites)if(town.transform.Find(site.RootPath)!=null)throw new InvalidOperationException("Generate from the immutable stage-four town.");
        foreach(string folder in new[]{"Meshes","Models","Prefabs"})Directory.CreateDirectory(TownSpecialPlan.AssetRoot+"/"+folder);
        AssetDatabase.Refresh();
        TownSpecialValidation.ValidateCatalogue(plan.Models);
        foreach(var model in plan.Models)
        {
            var mesh=SaveMesh(model.BuildMesh(),"Models/"+model.Id);templates.Add(model.Id,mesh);
            var prototype=new GameObject(model.Id,typeof(MeshFilter),typeof(MeshRenderer));
            try
            {
                prototype.GetComponent<MeshFilter>().sharedMesh=mesh;prototype.GetComponent<MeshRenderer>().sharedMaterial=material;
                AddParts(prototype.transform,model);
                PrefabUtility.SaveAsPrefabAsset(prototype,TownSpecialPlan.AssetRoot+"/Prefabs/"+model.Id+".prefab");
            }
            finally{Object.DestroyImmediate(prototype);}
        }
        Directory.CreateDirectory("Library/TownSpecialQA");
        File.WriteAllText("Library/TownSpecialQA/templates.txt","PASS native industrial models and per-part physical prefabs generated BEFORE placement.\n"+
            string.Join("\n",plan.Models.Select(m=>m.Id+": "+m.VisualBounds+"; "+m.Solids.Count+" real physical parts; coverage part "+m.CoveragePart)));
        TrimLegacy();
        foreach(var site in plan.Sites)BuildSite(site);
    }

    private void TrimLegacy()
    {
        int removed=0,colliders=0,index=0;
        foreach(var filter in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var source=filter.sharedMesh;if(source==null)throw new InvalidOperationException("Legacy mesh is missing.");
            var local=source.vertices;
            var positions=local.Select(filter.transform.TransformPoint).ToArray();
            var retained=new List<int>[source.subMeshCount];int count=0;
            for(int sub=0;sub<retained.Length;sub++)
            {
                if(source.GetTopology(sub)!=MeshTopology.Triangles)throw new InvalidOperationException("Unsupported legacy topology.");
                retained[sub]=new List<int>();var triangles=source.GetTriangles(sub);
                for(int t=0;t<triangles.Length;t+=3)
                {
                    int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                    bool authorised=plan.AllowedAreas.Any(r=>Inside(r,positions[a])&&Inside(r,positions[b])&&Inside(r,positions[c]));
                    bool interior=false;
                    if(authorised)
                    {
                        var triangleBounds=Rect.MinMaxRect(Mathf.Min(positions[a].x,Mathf.Min(positions[b].x,positions[c].x)),Mathf.Min(positions[a].z,Mathf.Min(positions[b].z,positions[c].z)),
                            Mathf.Max(positions[a].x,Mathf.Max(positions[b].x,positions[c].x)),Mathf.Max(positions[a].z,Mathf.Max(positions[b].z,positions[c].z)));
                        interior=plan.Sites.Any(s=>Touches(s.Bounds,positions[a],positions[b],positions[c])||s.Ramps.Any(r=>r.Bounds.Overlaps(triangleBounds)));
                    }
                    bool raisedSiteDetail=authorised&&Mathf.Max(positions[a].y,positions[b].y,positions[c].y)>.25f&&
                        plan.SiteAreas.Any(r=>Inside(r,positions[a])&&Inside(r,positions[b])&&Inside(r,positions[c]));
                    // Clear complete raised details in the old site scope; the extra gate ROI only replaces intersecting ground.
                    if(authorised&&(interior||raisedSiteDetail))count++;
                    else{retained[sub].Add(a);retained[sub].Add(b);retained[sub].Add(c);}
                }
            }
            if(count==0)continue;removed+=count;
            if(retained.All(t=>t.Count==0)){Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);continue;}
            var used=retained.SelectMany(t=>t).Distinct().OrderBy(i=>i).ToArray();var mapping=new int[source.vertexCount];
            for(int i=0;i<used.Length;i++)mapping[used[i]]=i;
            var mesh=new Mesh{name="SpecialTrim_"+source.name,indexFormat=IndexFormat.UInt32};
            mesh.vertices=used.Select(i=>local[i]).ToArray();
            var normals=source.normals;var uv=source.uv;var uv2=source.uv2;var colors=source.colors32;var tangents=source.tangents;
            if(normals.Length==source.vertexCount)mesh.normals=used.Select(i=>normals[i]).ToArray();
            if(uv.Length==source.vertexCount)mesh.uv=used.Select(i=>uv[i]).ToArray();
            if(uv2.Length==source.vertexCount)mesh.uv2=used.Select(i=>uv2[i]).ToArray();
            if(colors.Length==source.vertexCount)mesh.colors32=used.Select(i=>colors[i]).ToArray();
            if(tangents.Length==source.vertexCount)mesh.tangents=used.Select(i=>tangents[i]).ToArray();
            mesh.subMeshCount=retained.Length;
            for(int sub=0;sub<retained.Length;sub++)mesh.SetTriangles(retained[sub].Select(i=>mapping[i]).ToArray(),sub);
            mesh.RecalculateBounds();filter.sharedMesh=SaveMesh(mesh,"Meshes/LegacyTrim"+index++);
        }
        foreach(var collider in town.GetComponentsInChildren<Collider>(true))
        {
            var b=TownResidentialPlacement.ColliderBounds(collider);
            if(!plan.AllowedAreas.Any(r=>Inside(r,b.min)&&Inside(r,b.max)))continue;
            var footprint=TownCourtyardGeometry.Rect(b);
            if(!plan.Sites.Any(s=>s.Bounds.Overlaps(footprint)||s.Ramps.Any(r=>r.Bounds.Overlaps(footprint))))continue;
            Object.DestroyImmediate(collider);colliders++;
        }
        File.WriteAllText("Library/TownSpecialQA/placement.txt",$"PASS {removed} authorised legacy triangles and {colliders} local colliders removed; existing mesh assets unchanged. Only owned sites and their gate transitions edited; stop bay, street carriageways and previous stages excluded.\n");
    }

    private void BuildSite(TownSpecialSite site)
    {
        string[] path=site.RootPath.Split('/');var parent=town.transform.Find(path[0]);
        if(parent==null){parent=new GameObject(path[0]).transform;parent.SetParent(town.transform,false);}
        var root=new GameObject(path[1]).transform;root.SetParent(parent,false);
        var instances=new List<CombineInstance>();var props=new GameObject("Props").transform;props.SetParent(root,false);
        foreach(var prop in site.Props)
        {
            instances.Add(new CombineInstance{mesh=templates[prop.Model.Id],transform=prop.Pose});
            var marker=Point(props,prop.Id,prop.Position);marker.localRotation=Quaternion.Euler(0,prop.Yaw,0);marker.localScale=prop.Scale;
            new GameObject(prop.Model.Id).transform.SetParent(marker,false);AddParts(marker,prop.Model);
        }
        var surfaces=new GameObject("Surfaces").transform;surfaces.SetParent(root,false);
        foreach(var surface in site.Surfaces)
        {
            var marker=Point(surfaces,surface.Id,Vector3.zero);int piece=0;
            foreach(var area in SurfacePieces(surface.Bounds,site.Ramps))
            {
                var part=Point(marker,"Piece"+piece++,new Vector3(area.center.x,surface.Y/2,area.center.y));
                part.gameObject.isStatic=true;part.gameObject.AddComponent<BoxCollider>().size=new Vector3(area.width,surface.Y,area.height);
            }
        }
        foreach(var ramp in site.Ramps)
        {
            var marker=Point(surfaces,ramp.Id,Vector3.zero);marker.gameObject.isStatic=true;
            marker.gameObject.AddComponent<MeshCollider>().sharedMesh=SaveMesh(ramp.BuildMesh(),"Meshes/"+ramp.Id+".Collision");
        }
        var routes=new GameObject("Routes").transform;routes.SetParent(root,false);
        foreach(var route in site.Routes)
        {var marker=Point(routes,route.Id,Vector3.zero);Point(marker,"From",route.From);Point(marker,"To",route.To);}
        var layout=BuildLayout(site,plan.AllowedAreas);
        try
        {
            instances.Add(new CombineInstance{mesh=layout,transform=Matrix4x4.identity});
            var mesh=new Mesh{name=site.Id+" industrial place",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(instances.ToArray(),true,true);
            mesh.uv2=mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();
            var geometry=new GameObject("Site geometry",typeof(MeshFilter),typeof(MeshRenderer));geometry.transform.SetParent(root,false);geometry.isStatic=true;
            geometry.GetComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,"Meshes/"+site.Id);
            geometry.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        finally{Object.DestroyImmediate(layout);}
    }

    private static void AddParts(Transform marker,TownSpecialModel model)
    {
        foreach(var solid in model.Solids)
        {
            var part=Point(marker,"Part_"+solid.Id,solid.Centre);part.gameObject.isStatic=true;
            part.gameObject.AddComponent<BoxCollider>().size=solid.Size;
        }
    }
    private static Transform Point(Transform parent,string name,Vector3 position)
    {var result=new GameObject(name).transform;result.SetParent(parent,false);result.localPosition=position;return result;}

    internal static Mesh BuildLayout(TownSpecialSite site,IReadOnlyList<Rect> permissions)
    {
        var m=new TownMeshBuilder();
        foreach(var s in site.Surfaces)
        {
            foreach(var area in SurfacePieces(s.Bounds,site.Ramps))m.Ground(area.center.x,area.center.y,area.width,area.height,s.Y,s.Color);
            if(s.Color==TownColor.Asphalt||s.Color==TownColor.Stone)Edge(m,s.Bounds,s.Y+.025f,site.Ramps);
        }
        foreach(var route in site.Routes.Where(r=>r.Draw))
        {
            var bounds=Rect.MinMaxRect(Mathf.Min(route.From.x,route.To.x)-route.Width/2,Mathf.Min(route.From.z,route.To.z)-route.Width/2,
                Mathf.Max(route.From.x,route.To.x)+route.Width/2,Mathf.Max(route.From.z,route.To.z)+route.Width/2);
            // Permission rectangles overlap for preservation, so clip once per actual physical site.
            var clip=TownCourtyardGeometry.Expand(site.Bounds,1.8f);
            bounds=Rect.MinMaxRect(Mathf.Max(bounds.xMin,clip.xMin),Mathf.Max(bounds.yMin,clip.yMin),Mathf.Min(bounds.xMax,clip.xMax),Mathf.Min(bounds.yMax,clip.yMax));
            if(bounds.width<=0||bounds.height<=0)continue;
            if(!permissions.Any(p=>TownCourtyardGeometry.Contains(p,bounds)))throw new InvalidOperationException(route.Id+": new surface leaves its authorised scope.");
            foreach(var area in SurfacePieces(bounds,site.Ramps))
                m.Ground(area.center.x,area.center.y,area.width,area.height,route.Vehicle?.094f:.10f,route.Vehicle?(site.Id=="Construction"?TownColor.Soil:TownColor.Asphalt):TownColor.Pavement);
        }
        foreach(var ramp in site.Ramps)
        {
            if(!permissions.Any(p=>TownCourtyardGeometry.Contains(p,ramp.Bounds)))throw new InvalidOperationException(ramp.Id+": ramp leaves its authorised scope.");
            ramp.Append(m);
        }
        if(site.Id=="Workshop")
        {
            foreach(float x in new[]{102f,109f,116f})Hatch(m,new Rect(x-2.6f,-171,5.2f,3.7f));
            foreach(float x in new[]{97f,104f,111f})
            {Line(m,new Vector3(x,.105f,-184),new Vector3(x,.105f,-176),TownColor.White,.08f);}
            for(float x=122;x<154;x+=5)Line(m,new Vector3(x,.105f,-179),new Vector3(x+2,.105f,-179),TownColor.White,.08f);
            // A 4.5m marked working aisle gives the 3.8m swept route a visible side buffer.
            foreach(float z in new[]{-173f-2.25f,-173f+2.25f})
                Line(m,new Vector3(98,.108f,z),new Vector3(154,.108f,z),TownColor.White,.12f);
            for(float x=142.5f;x<146;x+=.6f)
            {
                Vector3 P(float dx,float z){var p=new Vector3(x+dx,0,z);return new Vector3(p.x,site.Ramps[0].HeightAt(p)+.012f,p.z);}
                m.Quad(P(-.15f,-186.2f),P(.15f,-186.2f),P(.15f,-187.8f),P(-.15f,-187.8f),TownColor.White);
            }
        }
        else
        {
            Hatch(m,new Rect(146,-63,10,7));
            Arrow(m,new Vector3(135,.108f,-52),90);
            for(float z=-58;z>-64;z-=1)m.Ground(145,z,22,.08f,.11f,TownColor.Yellow);
        }
        var mesh=m.Build(site.Id+" layout");mesh.uv2=mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();return mesh;
    }
    internal static IReadOnlyList<Rect> SurfacePieces(Rect area,IReadOnlyList<TownSpecialRamp> ramps)
    {
        var pieces=new List<Rect>{area};
        foreach(var ramp in ramps)
        {
            var next=new List<Rect>();
            foreach(var source in pieces)
            {
                if(!source.Overlaps(ramp.Bounds)){next.Add(source);continue;}
                var cut=Rect.MinMaxRect(Mathf.Max(source.xMin,ramp.Bounds.xMin),Mathf.Max(source.yMin,ramp.Bounds.yMin),
                    Mathf.Min(source.xMax,ramp.Bounds.xMax),Mathf.Min(source.yMax,ramp.Bounds.yMax));
                void Add(float x0,float z0,float x1,float z1){if(x1-x0>.0001f&&z1-z0>.0001f)next.Add(Rect.MinMaxRect(x0,z0,x1,z1));}
                Add(source.xMin,source.yMin,cut.xMin,source.yMax);Add(cut.xMax,source.yMin,source.xMax,source.yMax);
                Add(cut.xMin,source.yMin,cut.xMax,cut.yMin);Add(cut.xMin,cut.yMax,cut.xMax,source.yMax);
            }
            pieces=next;
        }
        return pieces;
    }
    private static void Edge(TownMeshBuilder m,Rect r,float y,IReadOnlyList<TownSpecialRamp> ramps)
    {
        foreach(float z in new[]{r.yMin,r.yMax})
            foreach(var piece in SurfacePieces(new Rect(r.xMin,z-.06f,r.width,.12f),ramps))
                m.Box(new Vector3(piece.center.x,y,piece.center.y),new Vector3(piece.width,.05f,piece.height),TownColor.Stone);
        foreach(float x in new[]{r.xMin,r.xMax})
            foreach(var piece in SurfacePieces(new Rect(x-.06f,r.yMin,.12f,r.height),ramps))
                m.Box(new Vector3(piece.center.x,y,piece.center.y),new Vector3(piece.width,.05f,piece.height),TownColor.Stone);
    }
    private static void Line(TownMeshBuilder m,Vector3 a,Vector3 b,TownColor color,float width)
    {var d=b-a;m.Box((a+b)/2,new Vector3(width,.008f,d.magnitude),color,Quaternion.LookRotation(d));}
    private static void Hatch(TownMeshBuilder m,Rect r)
    {
        foreach(float z in new[]{r.yMin,r.yMax})Line(m,new Vector3(r.xMin,.108f,z),new Vector3(r.xMax,.108f,z),TownColor.Yellow,.1f);
        foreach(float x in new[]{r.xMin,r.xMax})Line(m,new Vector3(x,.108f,r.yMin),new Vector3(x,.108f,r.yMax),TownColor.Yellow,.1f);
        for(float x=r.xMin+.5f;x<r.xMax-.5f;x+=.8f)Line(m,new Vector3(x,.11f,r.yMin+.2f),new Vector3(Mathf.Min(x+1,r.xMax-.2f),.11f,r.yMax-.2f),TownColor.Yellow,.09f);
    }
    private static void Arrow(TownMeshBuilder m,Vector3 p,float yaw)
    {
        var q=Quaternion.Euler(0,yaw,0);Vector3 P(float x,float z)=>p+q*new Vector3(x,0,z);
        m.Quad(P(-.12f,-1),P(-.12f,.5f),P(.12f,.5f),P(.12f,-1),TownColor.White);
        m.Triangle(P(-.6f,.3f),P(0,1.2f),P(.6f,.3f),TownColor.White);
    }
    private static bool Inside(Rect r,Vector3 p)=>p.x>=r.xMin&&p.x<=r.xMax&&p.z>=r.yMin&&p.z<=r.yMax;
    private static bool Touches(Rect r,Vector3 a,Vector3 b,Vector3 c)
    {
        bool Interior(Vector3 p)=>p.x>r.xMin+.15f&&p.x<r.xMax-.15f&&p.z>r.yMin+.15f&&p.z<r.yMax-.15f;
        return Interior(a)||Interior(b)||Interior(c);
    }
    private static Mesh SaveMesh(Mesh mesh,string relative)
    {
        string path=TownSpecialPlan.AssetRoot+"/"+relative+".asset";var current=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(current==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,current);EditorUtility.SetDirty(current);Object.DestroyImmediate(mesh);return current;
    }
}
