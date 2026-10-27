using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Editor.TownKit;
using RetailEmpireTycoon.Parking;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Authors a private enlarged construction kit. Shared imported models and player saves are never edited.</summary>
public static class StoreCompletionBatch
{
    public const string Root="Assets/Art/StoreCompletion",Report="Library/StoreCompletionQA";
    public static void RepairApron()
    {
        TownWorldSafety.RequireIsolatedEditor();
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
        var starter=Object.FindObjectOfType<StarterStoreInitialization>(true);
        ShopProportions.ReflowParking(starter.blueprint.initialState,starter.building.grid,starter.catalog);
        EditorUtility.SetDirty(starter.blueprint);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);
        try
        {
            if(File.Exists(Report+"/authored.txt"))throw new InvalidOperationException("Already scaled; restore the baseline before authoring again.");
            Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(Root+"/Meshes");AssetDatabase.Refresh();
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
            var starter=Object.FindObjectOfType<StarterStoreInitialization>(true);
            var catalog=starter.catalog;
            var definitions=catalog.Items.Concat(Resources.LoadAll<BuildItemData>("MultiStorey")).Where(item=>item!=null).Distinct().ToArray();
            var outputs=new HashSet<string>();var copies=new Dictionary<string,GameObject>();
            GameObject Copy(GameObject original)
            {
                if(original==null)return null;
                string source=AssetDatabase.GetAssetPath(original);
                if(copies.TryGetValue(source,out var result))return result;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(original);
                try
                {
                    go.transform.localScale*=ShopProportions.Scale;
                    var bay=go.GetComponent<ParkingBay>();if(bay!=null){bay.width*=ShopProportions.Scale;bay.length*=ShopProportions.Scale;}
                    string path=Root+"/Prefabs/"+Path.GetFileNameWithoutExtension(source)+"_"+AssetDatabase.AssetPathToGUID(source).Substring(0,8)+".prefab";
                    result=PrefabUtility.SaveAsPrefabAsset(go,path);copies.Add(source,result);return result;
                }
                finally{Object.DestroyImmediate(go);}
            }
            foreach(var item in definitions)
            {
                item.prefab=Copy(item.prefab);item.previousWallPrefab=Copy(item.previousWallPrefab);item.previousParkingPrefab=Copy(item.previousParkingPrefab);
                item.placementBounds=Scaled(item.placementBounds);item.previousWallBounds=Scaled(item.previousWallBounds);item.previousParkingBounds=Scaled(item.previousParkingBounds);
                item.placementAlignmentOffset*=ShopProportions.Scale;item.previousWallAlignmentOffset*=ShopProportions.Scale;
                item.wallCellSize*=ShopProportions.Scale;item.legacyWallDepth*=ShopProportions.Scale;
                outputs.Add(AssetDatabase.GetAssetPath(item));EditorUtility.SetDirty(item);
            }
            var door=definitions.Single(item=>item.id=="door_01");door.prefab=Door();door.placementBounds=CityUpdateSetup.Measure(door.prefab);EditorUtility.SetDirty(door);
            starter.building.grid.cellSize=ShopProportions.CellSize;
            starter.floors.surfaceHeight*=ShopProportions.Scale;
            starter.land.initialBounds=Scaled(starter.land.initialBounds);
            foreach(var plot in starter.land.plots)plot.bounds=Scaled(plot.bounds);
            // The street and apron retain their physical location. Only interior cell spacing grows.
            ShopProportions.Upgrade(starter.blueprint.initialState,starter.building.grid,catalog);
            starter.blueprint.cameraFocus*=ShopProportions.Scale;
            EditorUtility.SetDirty(starter.blueprint);outputs.Add(AssetDatabase.GetAssetPath(starter.blueprint));
            ShopGameplaySetup.Set(starter.cityTrip.GetComponent<ParkingTraffic>(),"store",Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>(true));
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            outputs.Add(CityUpdateSetup.GamePath);
            File.WriteAllLines(Report+"/outputs.txt",outputs.OrderBy(path=>path,StringComparer.Ordinal));
            File.WriteAllText(Report+"/authored.txt","PASS enlarged private kit, 1.5x grid and storeys, preserved street/apron, revised double-leaf entrance. Items="+definitions.Length+".\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build-failure.txt",error.ToString());EditorApplication.Exit(1);}
    }
    private static Bounds Scaled(Bounds bounds)=>new Bounds(bounds.center*ShopProportions.Scale,bounds.size*ShopProportions.Scale);
    private static Rect Scaled(Rect rect)=>new Rect(rect.position*ShopProportions.Scale,rect.size*ShopProportions.Scale);
    private static GameObject Door()
    {
        var root=new GameObject("door_01");
        var material=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
        void Part(Transform parent,string name,TownMeshBuilder mesh)
        {
            string path=Root+"/Meshes/Door_"+name+".asset";TrafficFleetBatch.Save(mesh.Build(name),path);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);go.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        try
        {
            var frame=new TownMeshBuilder();
            foreach(float x in new[]{-.71f,.71f})
            {
                frame.Box(new Vector3(x,.75f,0),new Vector3(.08f,1.5f,.5f),TownColor.Stone);
                var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(x,.75f,0);collider.size=new Vector3(.08f,1.5f,.5f);
            }
            frame.Box(new Vector3(0,1.455f,0),new Vector3(1.5f,.09f,.5f),TownColor.Stone);
            frame.Box(new Vector3(0,.145f,0),new Vector3(1.34f,.025f,.5f),TownColor.Metal);
            Part(root.transform,"Frame",frame);
            foreach(int sign in new[]{-1,1})
            {
                var hinge=new GameObject(sign<0?"door-left":"door-right").transform;hinge.SetParent(root.transform,false);hinge.localPosition=new Vector3(sign*.67f,0,0);
                float center=-sign*.33f;var panel=new TownMeshBuilder();
                panel.Box(new Vector3(center,.77f,0),new Vector3(.65f,1.24f,.065f),TownColor.Frame);
                panel.Box(new Vector3(center,.91f,-.038f),new Vector3(.55f,.84f,.018f),TownColor.Glass);
                panel.Box(new Vector3(center,.30f,-.04f),new Vector3(.55f,.23f,.02f),TownColor.GreenStripe);
                panel.Box(new Vector3(center-sign*.21f,.78f,-.075f),new Vector3(.025f,.25f,.05f),TownColor.Metal);
                Part(hinge,sign<0?"LeftPanel":"RightPanel",panel);
            }
            root.AddComponent<StoreDoorway>();
            return PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/door_01.prefab");
        }
        finally{Object.DestroyImmediate(root);}
    }
    public static void Record(string name)
    {
        var files=File.ReadAllLines(Report+"/outputs.txt").Concat(Directory.GetFiles(Root,"*",SearchOption.AllDirectories))
            .Concat(AssetDatabase.GetDependencies(File.ReadAllLines(Report+"/outputs.txt"),true).Where(path=>path.StartsWith("Assets/",StringComparison.Ordinal)&&File.Exists(path)))
            .Concat(Directory.GetFiles("Assets/Project/Scripts","*.cs",SearchOption.AllDirectories))
            .Concat(new[]{CityUpdateSetup.CityPath,"Assets/Prefabs/TownWorld/TownEnvironment.prefab","Assets/Project/Editor/StoreCompletionBatch.cs","Assets/Project/Editor/StoreCompletionPlay.cs","Assets/Project/Editor/StoreCompletionTraffic.cs","Assets/Project/Editor/MultiStoreyValidation.cs"})
            .Distinct().OrderBy(path=>path,StringComparer.Ordinal);
        File.WriteAllLines(Report+"/"+name+"-inputs.sha256",files.Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
    }
}
