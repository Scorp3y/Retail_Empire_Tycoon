using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Replaces only geometry fully inside residential parcels; never rewrites legacy meshes/materials.</summary>
internal sealed class TownResidentialPlacement
{
    private readonly TownResidentialPlan plan;
    private readonly GameObject root;
    private readonly Material groundMaterial;
    private int removedTriangles;
    private int removedColliders;

    public TownResidentialPlacement(TownResidentialPlan plan,GameObject root)
    {
        this.plan=plan??throw new ArgumentNullException(nameof(plan));
        this.root=root??throw new ArgumentNullException(nameof(root));
        groundMaterial=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
        if(groundMaterial==null) throw new InvalidOperationException("Existing town material is missing.");
    }

    public void Build()
    {
        Directory.CreateDirectory(TownResidentialPlan.AssetRoot+"/Meshes");
        AssetDatabase.Refresh();
        TrimPreviousGeometry();
        var district=new GameObject(TownResidentialPlan.HierarchyRoot);
        district.transform.SetParent(root.transform,false);
        foreach(var block in plan.Blocks) BuildBlock(block,district.transform);
        File.WriteAllText("Library/TownResidentialQA/placement.txt",
            $"PASS replaced {plan.Blocks.Count} residential parcels; removed {removedTriangles} old triangles and {removedColliders} local colliders.\n"+
            "Old mesh/material assets unchanged. New building colliders follow individual wings, never enclosing courtyards.\n");
    }

    private void TrimPreviousGeometry()
    {
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var source=filter.sharedMesh;
            if(source==null) throw new InvalidOperationException("A saved town mesh is missing.");
            var vertices=source.vertices;
            var world=vertices.Select(filter.transform.TransformPoint).ToArray();
            var retained=new List<int>[source.subMeshCount];
            int removed=0;
            for(int sub=0;sub<retained.Length;sub++)
            {
                if(source.GetTopology(sub)!=MeshTopology.Triangles) throw new InvalidOperationException("Unsupported town mesh topology.");
                retained[sub]=new List<int>();
                var triangles=source.GetTriangles(sub);
                for(int t=0;t<triangles.Length;t+=3)
                {
                    int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                    bool authorised=plan.AllowedAreas.Any(area=>Inside(area,world[a])&&Inside(area,world[b])&&Inside(area,world[c]));
                    // The permission seam includes the roadside pavement. Keep its low strips/curbs;
                    // they are a public street, not the parcel's old oversized paving pad.
                    bool aboveGround=Mathf.Max(world[a].y,world[b].y,world[c].y)>.25f;
                    bool touchesParcel=plan.Blocks.Any(block=>TouchesInterior(block.Bounds,world[a],world[b],world[c]));
                    if(authorised&&(aboveGround||touchesParcel)) removed++;
                    else { retained[sub].Add(a);retained[sub].Add(b);retained[sub].Add(c); }
                }
            }
            if(removed==0)continue;
            removedTriangles+=removed;
            if(retained.All(t=>t.Count==0))
            { Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);continue; }
            var used=retained.SelectMany(t=>t).Distinct().OrderBy(i=>i).ToArray();
            var mapping=new int[vertices.Length];
            for(int i=0;i<used.Length;i++)mapping[used[i]]=i;
            var mesh=new Mesh {name="ResidentialTrim_"+source.name,indexFormat=IndexFormat.UInt32};
            mesh.vertices=used.Select(i=>vertices[i]).ToArray();
            CopyAttributes(source,mesh,used);
            mesh.subMeshCount=retained.Length;
            for(int sub=0;sub<retained.Length;sub++)mesh.SetTriangles(retained[sub].Select(i=>mapping[i]).ToArray(),sub);
            mesh.RecalculateBounds();
            filter.sharedMesh=SaveMesh(mesh);
        }
        // Ground/river collision spans cannot fit in one parcel and therefore remain untouched.
        foreach(var collider in root.GetComponentsInChildren<Collider>(true))
        {
            Bounds bounds=ColliderBounds(collider);
            if(!plan.AllowedAreas.Any(area=>Inside(area,bounds.min)&&Inside(area,bounds.max)))continue;
            removedColliders++;Object.DestroyImmediate(collider);
        }
    }

    private static void CopyAttributes(Mesh source,Mesh target,int[] used)
    {
        var normals=source.normals;var uv=source.uv;var uv2=source.uv2;var colors=source.colors32;var tangents=source.tangents;
        if(normals.Length==source.vertexCount)target.normals=used.Select(i=>normals[i]).ToArray();
        if(uv.Length==source.vertexCount)target.uv=used.Select(i=>uv[i]).ToArray();
        if(uv2.Length==source.vertexCount)target.uv2=used.Select(i=>uv2[i]).ToArray();
        if(colors.Length==source.vertexCount)target.colors32=used.Select(i=>colors[i]).ToArray();
        if(tangents.Length==source.vertexCount)target.tangents=used.Select(i=>tangents[i]).ToArray();
    }

    private void BuildBlock(TownResidentialBlock block,Transform district)
    {
        var parent=new GameObject(block.Id).transform;parent.SetParent(district,false);
        foreach(var building in block.Buildings)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TownArchitecturePalette.Root+"/Prefabs/"+building.ModelId+".prefab");
            if(prefab==null)throw new InvalidOperationException("Architecture prefab missing: "+building.ModelId);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            instance.name=building.Id;instance.transform.SetParent(parent,false);
            instance.transform.localPosition=building.Position;instance.transform.localRotation=Quaternion.Euler(0,building.Yaw,0);
            instance.isStatic=true;
            // Preserve the architecture atlas. Legacy Model() would overwrite it with the 32x1 material.
            foreach(var volume in building.Model.Volumes)
            { var body=instance.AddComponent<BoxCollider>();body.center=volume.BodyBounds.center;body.size=volume.BodyBounds.size; }
        }
        var paths=TownResidentialRoutes.Build(block,root.transform);
        block.InitializeWalkways(paths);
        var ground=new TownMeshBuilder();
        var area=block.Bounds;
        ground.Ground(area.center.x,area.center.y,area.width,area.height,.07f,TownColor.Grass);
        foreach(var path in paths)
        {
            var side=Vector3.Cross(Vector3.up,(path.To-path.From).normalized)*path.Width/2;
            Vector3 a=path.From+Vector3.up*.145f,b=path.To+Vector3.up*.145f;
            ground.Quad(b-side,b+side,a+side,a-side,TownColor.Pavement);
            Line(ground,a-side,b-side,.045f);Line(ground,a+side,b+side,.045f);
        }
        // Route markers are editor-authored coordinates, not interactive objects or runtime scripts.
        var routeRoot=new GameObject("Walkways").transform;routeRoot.SetParent(parent,false);
        foreach(var path in paths)
        {
            var route=new GameObject(path.Id).transform;route.SetParent(routeRoot,false);
            var from=new GameObject("From").transform;from.SetParent(route,false);from.localPosition=path.From;
            var to=new GameObject("To").transform;to.SetParent(route,false);to.localPosition=path.To;
        }
        var surface=new GameObject("Residential ground and walkways",typeof(MeshFilter),typeof(MeshRenderer));
        surface.transform.SetParent(parent,false);surface.isStatic=true;
        var mesh=ground.Build(block.Id+"_Ground");mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();
        surface.GetComponent<MeshFilter>().sharedMesh=SaveMesh(mesh);
        surface.GetComponent<MeshRenderer>().sharedMaterial=groundMaterial;
        PlaceGreenery(block,parent);
    }

    private void PlaceGreenery(TownResidentialBlock block,Transform parent)
    {
        var treePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/TreeRound.prefab");
        if(treePrefab==null)
        {
            string path=AssetDatabase.FindAssets("TreeRound t:Prefab",new[]{"Assets/Art/TownKit/Prefabs"})
                .Select(AssetDatabase.GUIDToAssetPath).Single(p=>Path.GetFileNameWithoutExtension(p)=="TreeRound");
            treePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        var treeMesh=treePrefab.GetComponent<MeshFilter>().sharedMesh;
        var sources=new List<CombineInstance>();
        var bodies=block.Buildings.SelectMany(b=>b.WorldBodies).ToArray();
        int count=0;
        // Modest groups in genuine green gaps. Courtyard equipment is deliberately left for stage four.
        for(float z=block.Bounds.yMin+6;z<block.Bounds.yMax-5;z+=9)
            for(float x=block.Bounds.xMin+6;x<block.Bounds.xMax-5;x+=10)
            {
                var point=new Vector3(x,0,z);
                if(bodies.Any(b=>DistanceXZ(point,b)<4)||block.Walkways.Any(p=>DistanceToPath(point,p)<3.3f))continue;
                if(root.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled&&!c.isTrigger&&c.bounds.max.y>.5f&&DistanceXZ(point,ColliderBounds(c))<1))continue;
                float scale=block.Id.StartsWith("River",StringComparison.Ordinal)?.7f:.6f;
                sources.Add(new CombineInstance {mesh=treeMesh,transform=Matrix4x4.TRS(point,Quaternion.Euler(0,count*47,0),Vector3.one*scale)});
                var trunk=new GameObject("Residential tree trunk "+count++);trunk.transform.SetParent(parent,false);trunk.transform.localPosition=point;
                var collider=trunk.AddComponent<CapsuleCollider>();collider.center=Vector3.up*.9f;collider.height=1.8f;collider.radius=.2f;
            }
        if(sources.Count==0)return;
        var mesh=new Mesh {name=block.Id+"_Trees",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(sources.ToArray(),true,true);
        mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();
        var trees=new GameObject("Residential green groups",typeof(MeshFilter),typeof(MeshRenderer));trees.transform.SetParent(parent,false);trees.isStatic=true;
        trees.GetComponent<MeshFilter>().sharedMesh=SaveMesh(mesh);trees.GetComponent<MeshRenderer>().sharedMaterial=groundMaterial;
    }

    internal static Bounds ColliderBounds(Collider collider)
    {
        if(collider is BoxCollider box)
        {
            var matrix=collider.transform.localToWorldMatrix;var local=new Bounds(box.center,box.size);
            var result=new Bounds(matrix.MultiplyPoint3x4(local.min),Vector3.zero);
            for(int i=1;i<8;i++) result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3((i&1)==0?local.min.x:local.max.x,
                (i&2)==0?local.min.y:local.max.y,(i&4)==0?local.min.z:local.max.z)));
            return result;
        }
        if(collider is MeshCollider mesh&&mesh.sharedMesh!=null)
        {
            var bounds=mesh.sharedMesh.bounds;var matrix=collider.transform.localToWorldMatrix;
            var result=new Bounds(matrix.MultiplyPoint3x4(bounds.min),Vector3.zero);
            for(int i=1;i<8;i++)result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3((i&1)==0?bounds.min.x:bounds.max.x,
                (i&2)==0?bounds.min.y:bounds.max.y,(i&4)==0?bounds.min.z:bounds.max.z)));
            return result;
        }
        if(collider is CapsuleCollider capsule)
        {
            var scale=collider.transform.lossyScale;float radius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
            return new Bounds(collider.transform.TransformPoint(capsule.center),new Vector3(radius*2,capsule.height*Mathf.Abs(scale.y),radius*2));
        }
        throw new InvalidOperationException("Unsupported residential collision shape: "+collider.GetType().Name);
    }
    private static void Line(TownMeshBuilder mesh,Vector3 a,Vector3 b,float width)
    { var side=Vector3.Cross(Vector3.up,(b-a).normalized)*width/2;a.y=b.y=.147f;mesh.Quad(b-side,b+side,a+side,a-side,TownColor.Stone); }
    private static Mesh SaveMesh(Mesh mesh)
    {
        string path=TownResidentialPlan.AssetRoot+"/Meshes/"+mesh.name+".asset";
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null) {AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,saved);EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);return saved;
    }
    internal static bool Inside(Rect area,Vector3 point) => point.x>=area.xMin&&point.x<=area.xMax&&point.z>=area.yMin&&point.z<=area.yMax;
    private static bool TouchesInterior(Rect area,Vector3 a,Vector3 b,Vector3 c)
        => Mathf.Max(a.x,b.x,c.x)>area.xMin+.2f&&Mathf.Min(a.x,b.x,c.x)<area.xMax-.2f
            &&Mathf.Max(a.z,b.z,c.z)>area.yMin+.2f&&Mathf.Min(a.z,b.z,c.z)<area.yMax-.2f;
    internal static float DistanceXZ(Vector3 point,Bounds body)
        => new Vector2(Mathf.Max(body.min.x-point.x,0,point.x-body.max.x),Mathf.Max(body.min.z-point.z,0,point.z-body.max.z)).magnitude;
    internal static float DistanceToPath(Vector3 point,TownResidentialWalkway path)
    { var delta=path.To-path.From;float t=Mathf.Clamp01(Vector3.Dot(point-path.From,delta)/delta.sqrMagnitude);return Vector3.Distance(point,path.From+delta*t); }
}
