using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Owns the district's native models and palette without altering the older town's assets.</summary>
internal sealed class TownForestArt
{
    internal const string Root="Assets/Art/TownForestQuarter";
    private readonly Dictionary<string,GameObject> models=new();
    private TownStaticTypography type;
    internal Material Surface{get;private set;}
    internal void Build()
    {
        foreach(string dir in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(Root+"/"+dir);
        AssetDatabase.Refresh();
        var palette=new Texture2D(32,1,TextureFormat.RGBA32,false){name="Forest palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        palette.SetPixels32(TownForestModels.Palette());palette.Apply();Save(palette,"Materials/Palette.asset");
        var surface=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat")){name="Forest brick timber slate",enableInstancing=true};
        surface.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/Palette.asset"));surface.SetFloat("_PaletteIndex",-1);
        Save(surface,"Materials/Surface.mat");Surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        type=new TownStaticTypography(Root,"CAFE LOCAL MARKET COMMUNITY HUB FOREST QUARTER WOODLAND TRAIL PARCELS BUS COFFEE OPEN 0123456789");
        foreach(var recipe in TownForestModels.Create())
        {
            var go=new GameObject(recipe.Id){isStatic=true};
            try
            {
                Part(go.transform,recipe.Id,recipe.Draft.Mesh);
                foreach(var solid in recipe.Draft.Solids)Box(go.transform,solid.Id,solid.Center,solid.Size);
                switch(recipe.Id)
                {
                    case "CafeCorner":
                    case "MarketCorner":
                        string label=recipe.Id=="CafeCorner"?"CAFE":"LOCAL MARKET";
                        type.Add(go.transform,recipe.Id+"FrontLabel",label,new Vector3(0,3.45f,6.61f),10,.42f);
                        type.Add(go.transform,recipe.Id+"RearLabel",label,new Vector3(0,3.45f,-6.61f),10,.42f,180);break;
                    case "CommunityPavilion":type.Add(go.transform,"CommunityLabel","COMMUNITY HUB",new Vector3(0,3.5f,4.91f),5.5f,.35f);break;
                    case "ParcelLocker":type.Add(go.transform,"ParcelLabel","PARCELS",new Vector3(0,2.45f,.415f),2.6f,.26f);break;
                    case "DistrictMarker":type.Add(go.transform,"DistrictLabel","FOREST QUARTER",new Vector3(0,2.25f,.16f),5,.38f);break;
                    case "TrailMarker":type.Add(go.transform,"TrailLabel","WOODLAND TRAIL",new Vector3(0,2.25f,.16f),2.45f,.26f);break;
                    case "CafeMenu":type.Add(go.transform,"CafeMenuLabel","COFFEE",new Vector3(0,1,.14f),.65f,.15f);type.Add(go.transform,"CafeMenuOpen","OPEN",new Vector3(0,.65f,.14f),.5f,.14f);break;
                    case "BusShelter":type.Add(go.transform,"BusLabel","FOREST QUARTER",new Vector3(0,3.15f,-.535f),5.4f,.28f);break;
                }
                string path=Root+"/Prefabs/"+recipe.Id+".prefab";PrefabUtility.SaveAsPrefabAsset(go,path);models.Add(recipe.Id,AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            finally{Object.DestroyImmediate(go);}
        }
        AssetDatabase.SaveAssets();
    }
    internal Transform Place(Transform parent,string id,Vector3 p,float yaw=0,float scale=1)
    {
        if(!models.TryGetValue(id,out var prefab))
        {
            prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TownGatewayPlacement.Root+"/Prefabs/"+id+".prefab");
            if(prefab==null)throw new InvalidOperationException("Unknown district model "+id);
        }
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        go.name=id;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;return go.transform;
    }
    internal Transform Part(Transform parent,string id,TownMeshBuilder draft,bool physical=false)
    {
        var mesh=draft.Build(id);mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();Save(mesh,"Meshes/"+id+".asset");
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+id+".asset");var node=Child(parent,id);
        node.gameObject.AddComponent<MeshFilter>().sharedMesh=saved;node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Surface;
        if(physical)node.gameObject.AddComponent<MeshCollider>().sharedMesh=saved;return node;
    }
    internal void Combine(Transform group,string id)
    {
        var filters=group.GetComponentsInChildren<MeshFilter>();int count=0;
        foreach(var materialGroup in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var mesh=new Mesh{name=id+"Batch"+count,indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(materialGroup.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=group.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
            mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path="Meshes/"+id+"Batch"+count+".asset";Save(mesh,path);
            var node=Child(group,id+" visual batch "+count++);node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/"+path);
            var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=materialGroup.Key;
            if(materialGroup.Key.shader.name.Contains("Lettering"))renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        foreach(var f in filters){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
    }
    internal static Transform Child(Transform parent,string id)=>TownBakeryPlacement.Child(parent,id,Vector3.zero);
    internal static void Box(Transform parent,string id,Vector3 p,Vector3 size)
    {var node=Child(parent,id);var c=node.gameObject.AddComponent<BoxCollider>();c.center=p;c.size=size;}
    internal static void Save(Object asset,string relative)=>TownBakeryPlacement.SaveAsset(asset,Root+"/"+relative);
}
