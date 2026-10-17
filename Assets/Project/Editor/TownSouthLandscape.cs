using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>
/// English landscape brief: extend the native ground southwards, retaining the river section;
/// plant mature mixed woodland matching the northern forest, with an irregular edge and small glades.
/// </summary>
internal sealed class TownSouthLandscape
{
    internal const string Root="Assets/Art/TownSouthLandscape",Site="Southern woodland extension";
    internal const string EastRoot=Root+"/EastBank",EastSite="Eastern riverbank woodland";
    internal const string LayoutPath="Assets/Prefabs/TownWorld/TownLayout.asset";
    internal static readonly Rect OriginalBounds=new(-288,-216,960,720);
    internal static readonly Rect ExpandedBounds=new(-288,-504,960,1008);
    internal static readonly Rect Forest=new(-282,-492,564,286);
    internal static readonly Rect EastForest=new(328,-612,456,852);
    internal static readonly Rect[] Permission={new(-410,-626,1204,428),new(-289,-506,2,1012),new(671,-506,2,1012)};
    private readonly GameObject town;
    private readonly TownWorldLayout layout;
    private readonly System.Random random=new(15102026);
    private readonly Dictionary<string,GameObject> models=new();
    private readonly Dictionary<string,float> radii=new();
    private readonly List<Vector2> trunks=new();
    private readonly Material material;
    private Bounds[] obstacles;
    private Transform site;
    private Rect plantingArea=Forest;
    private string outputRoot=Root;
    private bool eastBank;
    internal TownSouthLandscape(GameObject town)
    {
        this.town=town;layout=town.GetComponent<TownWorldPlaces>().layout;
        material=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
        if(material==null)throw new InvalidOperationException("Native terrain/vegetation material is missing.");
    }
    internal void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(layout.cityBounds!=OriginalBounds||town.transform.Find(Site)!=null)
            throw new InvalidOperationException("Expected the recorded original southern boundary and no existing extension.");
        var reference=PrepareVegetation();
        Directory.CreateDirectory(outputRoot+"/Meshes");AssetDatabase.Refresh();
        site=TownBakeryPlacement.Child(town.transform,Site,Vector3.zero);
        ExtendGround();MoveWorldLimits();
        Plant(reference,TownSouthLandscapeBatch.Report+"/planting.txt");
    }
    internal void CompleteEastBank(string reportPath)
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(layout.cityBounds!=ExpandedBounds||town.transform.Find(Site)==null||town.transform.Find(EastSite)!=null)
            throw new InvalidOperationException("Expected the installed southern extension and no existing east-bank completion.");
        eastBank=true;plantingArea=EastForest;outputRoot=EastRoot;
        var reference=PrepareVegetation();
        Directory.CreateDirectory(outputRoot+"/Meshes");AssetDatabase.Refresh();
        site=TownBakeryPlacement.Child(town.transform,EastSite,Vector3.zero);
        // Complete only the missed vegetation: terrain, river and all world/map limits stay untouched.
        Plant(reference,reportPath);
    }
    private (string Id,float Scale)[] PrepareVegetation()
    {
        var reference=town.GetComponentsInChildren<CapsuleCollider>(true)
            .Where(c=>c.transform.parent!=null&&c.transform.parent.name=="Forest and river corridor"&&c.transform.position.z>247&&c.transform.position.x<454&&(c.name=="Pine"||c.name=="TreeWide"))
            .Select(c=>(Id:c.name,Scale:c.transform.lossyScale.x)).ToArray();
        if(reference.Length<30)throw new InvalidOperationException("Cannot sample the existing northern woodland.");
        obstacles=town.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger&&c.name!="World edge")
            .Select(TownResidentialPlacement.ColliderBounds).Where(b=>b.max.y>.25f).ToArray();
        trunks.AddRange(town.GetComponentsInChildren<CapsuleCollider>(true).Where(c=>c.name=="Pine"||c.name=="TreeWide"||c.name=="TreeRound")
            .Select(c=>new Vector2(c.transform.position.x,c.transform.position.z)));
        foreach(string id in new[]{"Pine","TreeWide","BushRound","BushWide","GrassClump"})
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/"+id+".prefab");
            if(model==null)throw new InvalidOperationException("Missing native vegetation: "+id);
            models.Add(id,model);radii.Add(id,model.GetComponent<MeshFilter>().sharedMesh.vertices.Max(p=>new Vector2(p.x,p.z).magnitude));
        }
        return reference;
    }
    private void Plant((string Id,float Scale)[] reference,string reportPath)
    {
        var grove=TownBakeryPlacement.Child(site,"Mixed woodland",Vector3.zero);
        int trees=0,pines=0,bushes=0,grass=0;float scaleSum=0;
        for(float z=plantingArea.yMin+6;z<plantingArea.yMax-6;z+=10.5f)
            for(float x=plantingArea.xMin+6;x<plantingArea.xMax-6;x+=10)
        {
            var p=new Vector2(x+Range(-3.4f,3.4f),z+Range(-3.4f,3.4f));
            var sample=reference[random.Next(reference.Length)];float radius=radii[sample.Id]*sample.Scale;
            if(!Clear(p,radius)||InGlade(p)||Mathf.PerlinNoise(p.x*.019f+11,p.y*.023f+7)<(eastBank?.20f:.26f)||trunks.Any(q=>Vector2.Distance(p,q)<5.2f))continue;
            var tree=Place(grove,sample.Id,p,sample.Scale);
            var trunk=tree.gameObject.AddComponent<CapsuleCollider>();trunk.center=Vector3.up*.9f;trunk.height=1.8f;trunk.radius=.2f;
            trunks.Add(p);trees++;scaleSum+=sample.Scale;if(sample.Id=="Pine")pines++;
            if(random.NextDouble()<.50)
            {
                var q=p+new Vector2(Range(1.8f,3.6f),Range(-3.4f,-1.8f));string id=random.Next(2)==0?"BushRound":"BushWide";float scale=Range(.9f,1.5f);
                if(Clear(q,radii[id]*scale)){Place(grove,id,q,scale);bushes++;}
            }
            if(random.NextDouble()<.60)
            {
                var q=p+new Vector2(Range(-3.8f,-1.7f),Range(1.5f,3.8f));float scale=Range(1.2f,1.8f);
                if(Clear(q,radii["GrassClump"]*scale)){Place(grove,"GrassClump",q,scale);grass++;}
            }
        }
        TownForestBatch.Require(trees>=1000&&pines>trees*.5f&&scaleSum/trees>2,"Woodland is too sparse or unlike its native reference.");
        CombineGroves(grove);AssetDatabase.SaveAssets();
        File.WriteAllText(reportPath,"PASS: "+trees+" mature trees ("+pines+" pines, "+(trees-pines)+" broadleaf), "+bushes+" shrubs, "+grass+" grass tufts. Mean tree scale "+(scaleSum/trees).ToString("F2")+"; sampled "+reference.Length+" actual northern trees. Irregular edge, native undergrowth, no old objects removed. Forest "+plantingArea+".\n");
    }
    private void ExtendGround()
    {
        var ground=new TownMeshBuilder();var water=new TownMeshBuilder();
        float left=OriginalBounds.xMin-120,right=OriginalBounds.xMax+120;
        // Continue the exact native cross-section only beyond its old edge: no overlapping flat plane over the river.
        for(float z=ExpandedBounds.yMin-120;z<OriginalBounds.yMin-120;z+=12)
        {
            float next=z+12,a=layout.RiverCenter(z),b=layout.RiverCenter(next);
            ground.Quad(new Vector3(left,0,next),new Vector3(b-18,0,next),new Vector3(a-18,0,z),new Vector3(left,0,z),TownColor.Grass);
            ground.Quad(new Vector3(b+18,0,next),new Vector3(right,0,next),new Vector3(right,0,z),new Vector3(a+18,0,z),TownColor.Grass);
            ground.Quad(new Vector3(b-18,0,next),new Vector3(b-10,-4.5f,next),new Vector3(a-10,-4.5f,z),new Vector3(a-18,0,z),TownColor.Sand);
            ground.Quad(new Vector3(b+10,-4.5f,next),new Vector3(b+18,0,next),new Vector3(a+18,0,z),new Vector3(a+10,-4.5f,z),TownColor.Sand);
            ground.Quad(new Vector3(b-10,-4.5f,next),new Vector3(b+10,-4.5f,next),new Vector3(a+10,-4.5f,z),new Vector3(a-10,-4.5f,z),TownColor.Sand);
            water.Quad(new Vector3(b-12,-3.05f,next),new Vector3(b+12,-3.05f,next),new Vector3(a+12,-3.05f,z),new Vector3(a-12,-3.05f,z),TownColor.Glass);
            water.Arc(new Vector3(a+Mathf.Sin(z*.47f)*7,-3.025f,z+3),.8f,.86f,10,120,TownColor.White,5);
        }
        Part(site,"Extended river-cut ground",ground,true);Part(site,"Extended river water",water,false);
    }
    private void MoveWorldLimits()
    {
        var edges=town.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name=="World edge").ToArray();
        TownForestBatch.Require(edges.Length==4,"Expected exactly four original world boundaries.");
        int changed=0;
        foreach(var edge in edges)
        {
            if(edge.size.x<2)
            {
                edge.center=new Vector3(edge.center.x,edge.center.y,ExpandedBounds.center.y);
                edge.size=new Vector3(edge.size.x,edge.size.y,ExpandedBounds.height);changed++;
            }
            else if(Mathf.Abs(edge.center.z-OriginalBounds.yMin)<.01f)
            {edge.center=new Vector3(edge.center.x,edge.center.y,ExpandedBounds.yMin);changed++;}
        }
        TownForestBatch.Require(changed==3,"Unexpected original world-boundary configuration.");
        string before=JsonUtility.ToJson(layout);layout.cityBounds=ExpandedBounds;
        var copy=Object.Instantiate(layout);copy.name=layout.name;
        try{copy.cityBounds=OriginalBounds;TownForestBatch.Require(JsonUtility.ToJson(copy)==before,"Non-boundary layout data changed.");}
        finally{Object.DestroyImmediate(copy);}
        EditorUtility.SetDirty(layout);
    }
    private bool Clear(Vector2 p,float radius)
    {
        if(!plantingArea.Contains(p-Vector2.one*radius)||!plantingArea.Contains(p+Vector2.one*radius))return false;
        if(eastBank)
        {
            // North of the new southern ground only the previously bare outer rim needs infill.
            // Leave the established woodland, dairy grounds and their access entirely as authored.
            if(p.y+radius>-202&&p.x-radius<layout.cityBounds.xMax+6)return false;
            float river=Mathf.Max(layout.RiverCenter(p.y-radius),Mathf.Max(layout.RiverCenter(p.y),layout.RiverCenter(p.y+radius)));
            if(p.x-radius<river+25)return false;
            // Keep trunks clear of the existing invisible world limits; foliage may overhang those planes.
            if(Mathf.Abs(p.x-layout.cityBounds.xMax)<1.6f||Mathf.Abs(p.y-layout.cityBounds.yMin)<1.6f)return false;
        }
        foreach(var b in obstacles)
            if(p.x>b.min.x-radius-.5f&&p.x<b.max.x+radius+.5f&&p.y>b.min.z-radius-.5f&&p.y<b.max.z+radius+.5f)return false;
        return true;
    }
    private static bool InGlade(Vector2 p)
    {
        var a=p-new Vector2(-112,-352);var b=p-new Vector2(116,-426);
        return a.x*a.x/(18*18)+a.y*a.y/(13*13)<1||b.x*b.x/(15*15)+b.y*b.y/(19*19)<1;
    }
    private Transform Place(Transform parent,string id,Vector2 p,float scale)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(models[id]);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        go.name=id;go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(p.x,0,p.y);
        go.transform.localScale=Vector3.one*scale;go.transform.localRotation=Quaternion.Euler(0,Range(0,360),0);go.isStatic=true;
        foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=material;
        return go.transform;
    }
    private void CombineGroves(Transform grove)
    {
        // Local 48 m cells let Unity cull distant/off-screen parts instead of rendering one enormous forest mesh.
        foreach(var group in grove.GetComponentsInChildren<MeshFilter>().GroupBy(f=>new Vector2Int(Mathf.FloorToInt(f.transform.position.x/48),Mathf.FloorToInt(f.transform.position.z/48))).ToArray())
        {
            var filters=group.ToArray();var mesh=new Mesh{name="Grove_"+group.Key.x+"_"+group.Key.y,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.CombineMeshes(filters.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=grove.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
            SavePart(grove,mesh.name,mesh,false);
            foreach(var filter in filters){Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);}
        }
    }
    private void Part(Transform parent,string name,TownMeshBuilder draft,bool physical)=>SavePart(parent,name,draft.Build(name),physical);
    private void SavePart(Transform parent,string name,Mesh mesh,bool physical)
    {
        mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path=outputRoot+"/Meshes/"+name.Replace(' ','_')+".asset";
        TownBakeryPlacement.SaveAsset(mesh,path);var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var node=TownBakeryPlacement.Child(parent,name,Vector3.zero);node.gameObject.isStatic=true;
        node.gameObject.AddComponent<MeshFilter>().sharedMesh=saved;node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
        if(physical)node.gameObject.AddComponent<MeshCollider>().sharedMesh=saved;
    }
    private float Range(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
}
