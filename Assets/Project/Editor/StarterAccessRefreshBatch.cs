using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>New-game economy/stock, narrow escalators and a scoped frontage trim; never loads a player save.</summary>
public static class StarterAccessRefreshBatch
{
    public const string Root="Assets/Art/StarterAccessRefresh",Report="Library/StarterAccessRefreshQA",Pictures="Docs/StarterAccessRefresh";
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
            var starter=Object.FindObjectOfType<StarterStoreInitialization>(true);
            var ids=new[]{"stairs_01","elevator_01","escalator_01","escalator_down_01"};
            starter.blueprint.ConstructionStock.ConfigureTransports(ids.Select(id=>AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Resources/MultiStorey/"+id+".asset")));
            starter.blueprint.initialState.playerMoney=GameData.StartingCapital;EditorUtility.SetDirty(starter.blueprint);
            foreach(var money in Object.FindObjectsOfType<MoneyController>(true)){money.SetMoney(GameData.StartingCapital);EditorUtility.SetDirty(money);}
            foreach(var legacy in Object.FindObjectsOfType<GameManager>(true)){legacy.playerMoney=GameData.StartingCapital;EditorUtility.SetDirty(legacy);}
            float left=starter.blueprint.initialState.floorTiles.Where(tile=>tile.floor==0&&tile.itemId!="asphalt_01").Min(tile=>tile.x)*starter.building.grid.cellSize+starter.building.grid.origin.x;
            var town=Object.FindObjectOfType<TownWorldPlaces>(true);float cut=town.layout.MapShopPoint(new Vector3(left,0,0)).x;
            var layout=town.layout;EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Narrow("escalator_01");Narrow("escalator_down_01");
            TrimPromenade(layout.cityShopOrigin,cut);
            scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            var city=Object.FindObjectOfType<CityWorld>(true);
            foreach(Transform child in city.unload.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
            city.unload.gameObject.SetActive(false);city.unload.requiresRearDocking=true;city.unload.displayName="Store unloading gate";
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            var outputs=Directory.GetFiles(Root,"*",SearchOption.AllDirectories).Append(Root+".meta")
                .Concat(new[]{CityUpdateSetup.GamePath,CityUpdateSetup.CityPath,TownWorldAssembler.PrefabPath,"Assets/Prefabs/StarterStore/StarterStore.asset"})
                .Concat(new[]{"escalator_01","escalator_down_01"}.SelectMany(id=>new[]{"Assets/Resources/MultiStorey/"+id+".asset","Assets/Art/ShopUi/Items/"+id+".png","Assets/Art/ShopUi/Items/"+id+".png.meta",AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Resources/MultiStorey/"+id+".asset").prefab)})).Distinct();
            File.WriteAllLines(Report+"/outputs.txt",outputs.Select(path=>path.Replace('\\','/')));
            File.WriteAllText(Report+"/build.txt","PASS new-game 300000 balance, four starter transports, escalators narrowed by another 20%, scoped left sidewalk overhang removed, City unloading marker removed.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    private static void Narrow(string id)
    {
        var item=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Resources/MultiStorey/"+id+".asset");
        string path=AssetDatabase.GetAssetPath(item.prefab);var go=PrefabUtility.LoadPrefabContents(path);
        try
        {
            // Absolute scale makes this authoring pass repeatable: previous revision used 0.8.
            go.transform.localScale=new Vector3(.64f,1,1);item.footprint=new Vector2Int(4,10);
            item.placementBounds=CityUpdateSetup.Measure(go);item.placementAlignmentOffset=new Vector3(0,0,item.placementBounds.center.z);
            if(item.placementBounds.size.x>4*ShopProportions.CellSize)throw new InvalidOperationException("Escalator does not fit its four-cell width.");
            PrefabUtility.SaveAsPrefabAsset(go,path);item.icon=ShopUiPreviews.Render(item.prefab,id,205);EditorUtility.SetDirty(item);
        }
        finally{PrefabUtility.UnloadPrefabContents(go);}
        AssetDatabase.SaveAssets();
    }
    private static void TrimPromenade(Vector3 origin,float cut)
    {
        var town=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try
        {
            if(town.transform.Find("Player entrance sidewalk trim")!=null)return;
            float x=origin.x,z=origin.z;
            if(cut<=x||cut>=x+8)throw new InvalidOperationException("Unexpected starter left-wall boundary: "+cut);
            bool Point(Vector3 p)=>Mathf.Abs(p.y-.075f)<.0002f&&p.x>=x-.0001f&&p.x<=x+30.0001f&&p.z>=z-3.0001f&&p.z<=z+.0001f;
            int total=0,index=0;Material surface=null;
            foreach(var filter in town.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null)continue;
                var bounds=filter.sharedMesh.bounds;
                var center=filter.transform.TransformPoint(bounds.center);var extent=Vector3.Scale(bounds.extents,filter.transform.lossyScale);
                if(center.x+extent.x<x||center.x-extent.x>x+30||center.z+extent.z<z-3||center.z-extent.z>z)continue;
                var mesh=TownBakeryPlacement.CopyWithoutTriangles(filter.sharedMesh,filter.transform,(a,b,c)=>Point(a)&&Point(b)&&Point(c),out int removed);
                if(mesh==null)continue;
                total+=removed;surface=filter.GetComponent<MeshRenderer>().sharedMaterial;
                string path=Root+"/PromenadeTrim"+(index++)+".asset";TownBakeryPlacement.SaveAsset(mesh,path);filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }
            if(total!=2)throw new InvalidOperationException("Expected exactly two original promenade triangles, found "+total);
            var draft=new TownMeshBuilder();draft.Ground((cut+x+30)*.5f,z-1.5f,x+30-cut,3,.075f,TownColor.Pavement);
            var replacement=draft.Build("Store entrance promenade without left overhang");replacement.uv2=replacement.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();
            TownBakeryPlacement.SaveAsset(replacement,Root+"/Promenade.asset");
            var node=new GameObject("Player entrance sidewalk trim",typeof(MeshFilter),typeof(MeshRenderer));node.transform.SetParent(town.transform,false);
            node.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Promenade.asset");node.GetComponent<MeshRenderer>().sharedMaterial=surface;
            PrefabUtility.SaveAsPrefabAsset(town,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            File.WriteAllText(Report+"/frontage.txt",$"PASS removed exactly two original paving triangles and rebuilt only X {cut}..{x+30}, Z {z-3}..{z}. Left overhang {cut-x} town metres removed. Other shared meshes untouched; lamp, planter and bench retained.\n");
        }
        finally{PrefabUtility.UnloadPrefabContents(town);}
    }
    public static void Record()
    {
        var files=File.ReadAllLines(Report+"/outputs.txt").Concat(Directory.GetFiles("Assets/Project/Scripts","*.cs",SearchOption.AllDirectories))
            .Concat(AssetDatabase.GetDependencies(new[]{CityUpdateSetup.GamePath,CityUpdateSetup.CityPath},true).Where(path=>path.StartsWith("Assets/")&&File.Exists(path)))
            .Concat(new[]{"Assets/Project/Editor/StarterAccessRefreshBatch.cs","Assets/Project/Editor/StarterAccessRefreshPlay.cs"}).Distinct().OrderBy(path=>path);
        File.WriteAllLines(Report+"/inputs.sha256",files.Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
    }
}
