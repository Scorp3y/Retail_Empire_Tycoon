using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using RetailEmpireTycoon.Editor.TownKit;
using Object=UnityEngine.Object;

/// <summary>Creates native reusable assets from the approved Blender kit without rewriting existing town art.</summary>
internal sealed class PinebrookDistrictArt
{
    internal const string Root="Assets/Art/Pinebrook";
    [Serializable] internal sealed class Catalog {public Model[] models;}
    [Serializable] internal sealed class Model {public string name,kind;public int triangles,lod1_triangles,limit,floors;public float width,depth;public float[] size_m,entrance;}
    private readonly Dictionary<string,GameObject> prefabs=new();
    internal IReadOnlyDictionary<string,Model> Models{get;private set;}
    internal Material Surface{get;private set;}
    internal Material Ground{get;private set;}
    internal void LoadExisting()
    {
        Models=JsonUtility.FromJson<Catalog>(File.ReadAllText(Root+"/Models/Models.json")).models.ToDictionary(m=>m.name);
        Surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        var floor=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownForestRoadStyle.MaterialPath)){name="Pinebrook city-matched ground",enableInstancing=true};Save(floor,"Materials/Ground.mat");Ground=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Ground.mat");
        foreach(var model in Models.Values)prefabs[model.name]=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+model.name+".prefab");
        BuildSpecialProps();
    }
    internal void Build()
    {
        foreach(string folder in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(Root+"/"+folder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Models=JsonUtility.FromJson<Catalog>(File.ReadAllText(Root+"/Models/Models.json")).models.ToDictionary(m=>m.name);
        string atlasPath=Root+"/Models/PinebrookPalette.png";var texture=(TextureImporter)AssetImporter.GetAtPath(atlasPath);
        texture.mipmapEnabled=false;texture.filterMode=FilterMode.Point;texture.textureCompression=TextureImporterCompression.Uncompressed;texture.wrapMode=TextureWrapMode.Clamp;texture.SaveAndReimport();
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/TownForestQuarter/Materials/Surface.mat");
        var surface=new Material(source){name="Pinebrook shared cream timber teal",enableInstancing=true};surface.shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/PinebrookSurface.shader");if(surface.shader==null)throw new InvalidOperationException("Pinebrook vertex shading shader missing.");surface.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath));surface.SetFloat("_PaletteIndex",-1);surface.SetFloat("_Smoothness",.15f);Save(surface,"Materials/Surface.mat");Surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        // The crossing/whole-city palette has the accepted grey asphalt. The facade palette does not.
        var floor=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownForestRoadStyle.MaterialPath)){name="Pinebrook city-matched ground",enableInstancing=true};Save(floor,"Materials/Ground.mat");Ground=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Ground.mat");
        foreach(var model in Models.Values)
        {
            var go=new GameObject(model.name);go.isStatic=true;
            try
            {
                var levels=new List<LOD>();int count=model.kind=="building"?2:1;
                for(int level=0;level<count;level++)
                {
                    var mesh=ImportMesh(model.name+"_LOD"+level);int triangles=mesh.triangles.Length/3;
                    if(level==0&&(triangles!=model.triangles||triangles>model.limit))throw new InvalidOperationException("Triangle budget differs from Blender: "+model.name);
                    Save(mesh,"Meshes/"+model.name+"_LOD"+level+".asset");var node=Child(go.transform,"LOD"+level);
                    node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+model.name+"_LOD"+level+".asset");
                    var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=Surface;levels.Add(new LOD(level==0?.085f:.015f,new Renderer[]{renderer}));
                }
                if(model.name=="Garden_Fence"){var fence=go.AddComponent<BoxCollider>();fence.center=new Vector3(0,.6f,0);fence.size=new Vector3(3,1.2f,.14f);}
                if(model.name=="Footbridge"){var deck=go.AddComponent<BoxCollider>();deck.center=new Vector3(0,.3f,0);deck.size=new Vector3(2.7f,.25f,5.5f);foreach(float z in new[]{-3f,3f}){var ramp=go.AddComponent<BoxCollider>();ramp.center=new Vector3(0,.12f,z);ramp.size=new Vector3(2.7f,.24f,.7f);}}
                if(count==2){var lod=go.AddComponent<LODGroup>();lod.SetLODs(levels.ToArray());lod.RecalculateBounds();var box=go.AddComponent<BoxCollider>();box.center=new Vector3(0,model.floors*3.2f/2,0);box.size=new Vector3(model.width,model.floors*3.2f,model.depth);}
                string path=Root+"/Prefabs/"+model.name+".prefab";PrefabUtility.SaveAsPrefabAsset(go,path);prefabs[model.name]=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            finally{Object.DestroyImmediate(go);}
        }
        BuildSpecialProps();AssetDatabase.SaveAssets();
    }
    private static Mesh ImportMesh(string name)
    {
        string path=Root+"/Models/"+name+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);if(importer==null)throw new FileNotFoundException(path);
        importer.globalScale=1;importer.bakeAxisConversion=true;importer.importCameras=false;importer.importLights=false;importer.SaveAndReimport();
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
        mesh.CombineMeshes(source.GetComponentsInChildren<MeshFilter>().Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=source.transform.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();
        if(Mathf.Abs(mesh.bounds.min.y)>.01f||mesh.vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)))throw new InvalidOperationException("Invalid metre pivot or vertex: "+name);
        return mesh;
    }
    private void BuildSpecialProps()
    {
        var pond=new TownMeshBuilder();
        Vector3 Bank(int i,bool inner){float angle=i*Mathf.PI*2/32;return new Vector3(Mathf.Cos(angle)*(inner?7.8f:9),inner?.10f:.025f,Mathf.Sin(angle)*(inner?2.4f:3.3f));}
        for(int i=0;i<32;i++){pond.Quad(Bank(i,false),Bank(i,true),Bank(i+1,true),Bank(i+1,false),TownColor.Soil);var a=Bank(i,true);var b=Bank(i+1,true);a.y=b.y=.07f;pond.Triangle(new Vector3(0,.07f,0),b,a,TownColor.Glass);}
        PrefabFromDraft("Pond",pond,false);
        var parking=new TownMeshBuilder();parking.Ground(0,0,7,6,.125f,TownColor.Asphalt);for(int i=-1;i<=1;i++)parking.Ground(i*3,0,.08f,5.8f,.133f,TownColor.White);parking.Ground(0,2.9f,6,.08f,.133f,TownColor.White);PrefabFromDraft("ParkingPocket",parking,false);
    }
    private void PrefabFromDraft(string name,TownMeshBuilder draft,bool collision)
    {
        var go=new GameObject(name);try{Part(go.transform,name,draft,Ground,collision);string path=Root+"/Prefabs/"+name+".prefab";PrefabUtility.SaveAsPrefabAsset(go,path);prefabs[name]=AssetDatabase.LoadAssetAtPath<GameObject>(path);}finally{Object.DestroyImmediate(go);}
    }
    internal Transform Place(Transform parent,string id,Vector3 position,float yaw,float scale)
    {
        if(id.StartsWith("label:")){var label=Child(parent,id);label.localPosition=position;label.localRotation=Quaternion.Euler(0,yaw,0);return label;}
        if(!prefabs.TryGetValue(id,out var prefab))
        {
            string path=id.StartsWith("junction:")?"Assets/Art/TownJunctions/Prefabs/"+id.Substring(9)+".prefab":id.StartsWith("traffic:")?"Assets/Art/TrafficFleet/Prefabs/"+id.Substring(8)+".prefab":id.StartsWith("nature:")?"Assets/Art/TownKit/Prefabs/Nature/"+id.Substring(7)+".prefab":id.StartsWith("native:")?"Assets/Art/TownForestQuarter/Prefabs/"+id.Substring(7)+".prefab":null;
            if(path!=null)prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null&&id.StartsWith("native:"))prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownGatewayUpdate/Prefabs/"+id.Substring(7)+".prefab");
            if(prefab==null)throw new InvalidOperationException("Unknown district prefab "+id);
        }
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
        if(id.StartsWith("nature:")){foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=Ground;if(id=="nature:TreeRound"||id=="nature:TreeWide"||id=="nature:Pine"){var trunk=go.AddComponent<BoxCollider>();trunk.center=new Vector3(0,1.1f,0);trunk.size=new Vector3(.28f,2.2f,.28f);}}
        if(id.StartsWith("traffic:"))foreach(var behaviour in go.GetComponentsInChildren<MonoBehaviour>())Object.DestroyImmediate(behaviour);
        if(id=="native:BusShelter"){var old=go.transform.Find("BusLabel");if(old!=null)Object.DestroyImmediate(old.gameObject);}
        foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.isStatic=true;return go.transform;
    }
    internal Transform Part(Transform parent,string name,TownMeshBuilder draft,Material material,bool physical=false)
    {
        var mesh=draft.Build(name);mesh.uv2=mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();Save(mesh,"Meshes/"+name+".asset");var node=Child(parent,name);node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+name+".asset");node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;if(physical)node.gameObject.AddComponent<MeshCollider>().sharedMesh=node.GetComponent<MeshFilter>().sharedMesh;return node;
    }
    internal static Transform Child(Transform parent,string name){var go=new GameObject(name);go.isStatic=true;go.transform.SetParent(parent,false);return go.transform;}
    internal static void Save(Object asset,string path)=>TownBakeryPlacement.SaveAsset(asset,Root+"/"+path);
}
