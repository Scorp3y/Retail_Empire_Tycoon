using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>English landscaping brief: dense mature mixed forest matching the existing northern woodland, predominantly tall conifers with broad overlapping deciduous crowns; preserve the marked area and all existing access.</summary>
internal sealed class TownWesternGreenery
{
    internal const string RootName="Western district greenery";
    internal static readonly Rect Area=new(-390,-184,168,361);
    private readonly GameObject town;
    private readonly TownWorldLayout layout;
    private readonly List<Vector2> treePositions=new();
    private readonly Dictionary<string,GameObject> models=new();
    private readonly Dictionary<string,float> crownRadii=new();
    private readonly System.Random variation=new(14102026);
    private Bounds[] obstacles;
    private Transform root;
    private int trees,bushes,meadows;

    internal TownWesternGreenery(GameObject town)
    {
        this.town=town;
        layout=town.GetComponent<TownWorldPlaces>().layout;
    }

    internal void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(TownGroceryPlacement.SiteName)==null)throw new InvalidOperationException("Install the grocery depot before landscaping its district.");
        // Sample real authored trees from the forest in the reference, including the town's 1.3 scale multiplier.
        var reference=town.GetComponentsInChildren<CapsuleCollider>(true)
            .Where(c=>c.transform.parent!=null&&c.transform.parent.name=="Forest and river corridor"&&c.transform.position.z>247&&c.transform.position.x<454&&(c.name=="Pine"||c.name=="TreeWide"))
            .Select(c=>new ForestTree(c.name,c.transform.lossyScale.x)).ToArray();
        if(reference.Length<30)throw new InvalidOperationException("Cannot identify enough actual trees in the reference northern forest.");
        var previous=town.transform.Find(RootName);
        if(previous!=null)Object.DestroyImmediate(previous.gameObject);
        obstacles=town.GetComponentsInChildren<Collider>(true)
            .Where(c=>!c.isTrigger).Select(TownResidentialPlacement.ColliderBounds).Where(b=>b.max.y>.25f).ToArray();
        root=TownBakeryPlacement.Child(town.transform,RootName,Vector3.zero);
        foreach(string id in new[]{"TreeWide","Pine","BushRound","BushWide","GrassClump"})
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/"+id+".prefab");
            if(model==null)throw new InvalidOperationException("Existing city vegetation is missing: "+id);
            models.Add(id,model);
            crownRadii.Add(id,model.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(f.transform.TransformPoint)).Max(v=>new Vector2(v.x,v.z).magnitude));
        }
        var canopy=new List<(Vector2 position,float radius)>();
        float totalScale=0;int pines=0;
        // One continuous irregular forest, rather than small isolated groups surrounded by lawn.
        // Crowns may overlap as in the reference; only trunks need physical separation.
        for(float z=Area.yMin+4;z<Area.yMax-4;z+=10)
            for(float x=Area.xMin+4;x<Area.xMax-4;x+=9)
        {
            var p=new Vector2(x+Range(-3.4f,3.4f),z+Range(-3.4f,3.4f));
            var sample=reference[variation.Next(reference.Length)];
            float radius=crownRadii[sample.Id]*sample.Scale+.1f;
            if(!Clear(p,radius)||treePositions.Any(other=>Vector2.Distance(p,other)<5.2f))continue;
            var tree=Place(root,sample.Id,p,sample.Scale,0);
            float trunkHeight=sample.Id=="Pine"?3:2.52f;
            var trunk=tree.gameObject.AddComponent<CapsuleCollider>();trunk.center=Vector3.up*(trunkHeight/2);trunk.height=trunkHeight;trunk.radius=.20f;
            treePositions.Add(p);canopy.Add((p,radius));trees++;totalScale+=sample.Scale;if(sample.Id=="Pine")pines++;
            if(variation.Next(4)==0)
            {
                var bush=p+new Vector2(2,-2);
                if(Clear(bush,2.3f)){Place(root,variation.Next(2)==0?"BushWide":"BushRound",bush,1.2f,.05f);bushes++;}
            }
            if(variation.Next(5)==0)
            {
                var grass=p+new Vector2(-2.5f,1.5f);
                if(Clear(grass,1.2f)){Place(root,"GrassClump",grass,1.4f,.02f);meadows++;}
            }
        }
        int connectedCrowns=canopy.Count(a=>canopy.Any(b=>a.position!=b.position&&Vector2.Distance(a.position,b.position)<a.radius+b.radius));
        float crownConnection=(float)connectedCrowns/trees;
        if(trees<400||totalScale/trees<2.0f||crownConnection<.70f)throw new InvalidOperationException("Forest does not match mature woodland density/scale: "+trees+" trees; mean scale "+totalScale/trees+"; connected crowns "+crownConnection);
        CombineByGrove();
        File.WriteAllText(TownGroceryBatch.Report+"/greenery.txt","PASS: dense mature forest: "+trees+" trees ("+pines+" pines), "+bushes+" understory shrubs, "+meadows+" grass tufts. Sampled "+reference.Length+" actual northern-forest trees; reference mean scale "+reference.Average(t=>t.Scale).ToString("F2")+", planted mean scale "+(totalScale/trees).ToString("F2")+"; "+(crownConnection*100).ToString("F1")+"% of crown envelopes have neighbours. Native forest palette; marked area and base/road/rail access retained.\n");
        AssetDatabase.SaveAssets();
    }

    private bool Clear(Vector2 p,float radius)
    {
        if(!Area.Contains(p-new Vector2(radius,radius))||!Area.Contains(p+new Vector2(radius,radius)))return false;
        var site=TownGroceryPlacement.Site;
        float buffer=Mathf.Max(6,radius+2);
        if(new Rect(site.xMin-buffer,site.yMin-buffer,site.width+buffer*2,site.height+buffer*2).Contains(p))return false;
        foreach(var tile in layout.roads)
        {
            var road=tile.Position(layout.roadTileSize);
            float clearance=layout.roadTileSize/2+radius+2.5f;
            if(Mathf.Abs(p.x-road.x)<clearance&&Mathf.Abs(p.y-road.z)<clearance)return false;
        }
        if(NearPath(p,layout.railway,10+radius)||NearPath(p,layout.freightSiding,8+radius)||NearPath(p,layout.countryRoad,8+radius))return false;
        foreach(var b in obstacles)
        {
            // Invisible world limits must not intersect trunks; crowns may overhang that invisible plane.
            float clearance=b.size.y>=3.9f&&(b.size.x>300||b.size.z>300)?.6f:radius+.4f;
            if(p.x>b.min.x-clearance&&p.x<b.max.x+clearance&&p.y>b.min.z-clearance&&p.y<b.max.z+clearance)return false;
        }
        return true;
    }

    private static bool NearPath(Vector2 p,TownTransportPath path,float clearance)
    {
        for(int i=1;i<path.points.Length;i++)
        {
            var a=new Vector2(path.points[i-1].x,path.points[i-1].z);var b=new Vector2(path.points[i].x,path.points[i].z);
            var delta=b-a;float t=delta.sqrMagnitude<.001f?0:Mathf.Clamp01(Vector2.Dot(p-a,delta)/delta.sqrMagnitude);
            if(Vector2.Distance(p,a+delta*t)<clearance)return true;
        }
        return false;
    }

    private float Range(float min,float max)=>Mathf.Lerp(min,max,(float)variation.NextDouble());
    private Transform Place(Transform parent,string id,Vector2 p,float scale,float height)
    {
        var item=(GameObject)PrefabUtility.InstantiatePrefab(models[id]);
        PrefabUtility.UnpackPrefabInstance(item,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        item.transform.SetParent(parent,false);item.transform.localPosition=new Vector3(p.x,height,p.y);
        item.transform.localRotation=Quaternion.Euler(0,Range(0,360),0);item.transform.localScale=Vector3.one*scale;
        foreach(var renderer in item.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
        item.isStatic=true;return item.transform;
    }

    private void CombineByGrove()
    {
        // Small spatial batches preserve off-screen culling instead of one giant always-visible woodland mesh.
        var groups=root.GetComponentsInChildren<MeshFilter>().GroupBy(f=>new Vector2Int(Mathf.FloorToInt(f.transform.position.x/48),Mathf.FloorToInt(f.transform.position.z/48))).ToArray();
        string folder=TownGroceryPlacement.Root+"/Meshes";Directory.CreateDirectory(folder);
        foreach(var group in groups)
        {
            var filters=group.ToArray();
            var material=filters[0].GetComponent<MeshRenderer>().sharedMaterial;
            if(filters.Any(f=>f.GetComponent<MeshRenderer>().sharedMaterial!=material))throw new InvalidOperationException("Native greenery must share the city vegetation material.");
            var mesh=new Mesh{name="WesternGreenery_"+group.Key.x+"_"+group.Key.y,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.CombineMeshes(filters.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
            mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path=folder+"/"+mesh.name+".asset";
            TownBakeryPlacement.SaveAsset(mesh,path);
            var batch=TownBakeryPlacement.Child(root,"Greenery batch "+group.Key,Vector3.zero);batch.gameObject.isStatic=true;
            batch.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);batch.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            foreach(var filter in filters){Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);}
        }
    }
    private readonly struct ForestTree
    {
        internal readonly string Id;
        internal readonly float Scale;
        internal ForestTree(string id,float scale){Id=id;Scale=scale;}
    }
}
