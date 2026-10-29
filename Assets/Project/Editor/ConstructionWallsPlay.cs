using System;
using System.Collections;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ConstructionWallsPlay
{
    private const string Pending="ConstructionWalls.Pending",Report="Library/ConstructionWallsQA";
    private static IEnumerator checks;
    private static double deadline;
    static ConstructionWallsPlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
        foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());SessionState.SetBool(Pending,true);
        File.WriteAllText(Report+"/result.txt","RUNNING\n");EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(checks==null){deadline=EditorApplication.timeSinceStartup+90;checks=Check();}
            Require(EditorApplication.timeSinceStartup<deadline,"Wall visibility test timed out.");
            if(checks.MoveNext())return;Finish(null);
        }
        catch(Exception error){Debug.LogException(error);Finish(error);}
    }
    private static IEnumerator Check()
    {
        for(int i=0;i<4;i++)yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Player save writer present.");
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();starter.cityTrip.orders.save=null;
        var data=starter.CreateNewGame(50000);Object.FindObjectOfType<StoreProgression>().ApplySaveData(data.territory);
        starter.building.inventory.ApplySaveData(data.buildInventory,starter.catalog);
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;starter.ConfigureSite(true);spawner.Spawn(StoreLevelId.Lvl1);starter.ApplyNewGameBuildings(data);
        var shop=Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();shop.SetOpen(false);shop.enabled=false;
        yield return null;yield return null;
        var build=starter.building;var editor=build.GetComponent<BuildEditingController>();editor.ReadPointerInput=false;
        var visibility=Object.FindObjectOfType<StoreWallVisibility>();Require(visibility!=null,"Wall visibility component missing.");
        var walls=Object.FindObjectsOfType<StoreWallOccluder>().Where(wall=>wall.GetComponentInParent<BuildPreview>()==null).ToArray();
        Require(walls.Length>20,"Real starter walls were not loaded.");
        var renderers=walls.SelectMany(wall=>wall.GetComponentsInChildren<Renderer>()).ToArray();
        var colliders=walls.SelectMany(wall=>wall.GetComponentsInChildren<Collider>()).ToArray();
        bool[] colliderStates=colliders.Select(collider=>collider.enabled).ToArray();
        var originalData=JsonUtility.ToJson(new GameData{placedObjects=build.BuildPlacedSaveData(),floorTiles=starter.floors.BuildSaveData()});
        void AllVisible(){visibility.SendMessage("LateUpdate");Require(renderers.All(renderer=>renderer.enabled && !renderer.forceRenderingOff),"Camera or construction hides an installed wall.");}
        foreach(string id in new[]{"elevator_01","stairs_01","floor_cream","wall_01","shelf_fresh_01"})
        {
            build.Storeys.SelectFloor(0);build.EnterBuildMode(starter.catalog.GetById(id));
            for(int angle=0;angle<4;angle++)
            {
                var offset=Quaternion.Euler(0,angle*90,0)*new Vector3(-8,12,-14);
                build.worldCamera.transform.SetPositionAndRotation(starter.blueprint.cameraFocus+offset,Quaternion.LookRotation(-offset));
                yield return null;AllVisible();
            }
        }
        build.Storeys.SelectFloor(1);yield return null;AllVisible();build.Storeys.SelectFloor(0);yield return null;AllVisible();
        editor.Begin();editor.Select(Object.FindObjectsOfType<PlacedObject>().First(placed=>placed.item.id=="shelf_fresh_01"));yield return null;AllVisible();editor.CancelSelection();
        var selected=walls.First().GetComponentInParent<PlacedObject>();editor.Select(selected);yield return null;visibility.SendMessage("LateUpdate");
        Require(walls.Where(wall=>wall.GetComponentInParent<PlacedObject>()!=selected).SelectMany(wall=>wall.GetComponentsInChildren<Renderer>()).All(renderer=>!renderer.forceRenderingOff),"Moving one wall hides its neighbors.");
        editor.CancelSelection();yield return null;AllVisible();editor.Finish();
        visibility.enabled=false;AllVisible();visibility.enabled=true;AllVisible();
        Require(colliders.Select((collider,index)=>collider.enabled==colliderStates[index]).All(same=>same),"Construction changed wall physics.");
        Require(originalData==JsonUtility.ToJson(new GameData{placedObjects=build.BuildPlacedSaveData(),floorTiles=starter.floors.BuildSaveData()}),"Visibility changed construction or flooring data.");
        var item=starter.catalog.GetById("elevator_01");build.EnterBuildMode(item);
        var anchor=starter.floors.BuildSaveData().Where(tile=>tile.floor==0 && tile.itemId!="asphalt_01").Select(tile=>new Vector3Int(tile.x,0,tile.z)).First(cell=>build.CanPlaceAt(cell).ok);
        var request=new PlacementRequest(item,anchor,false,0);build.preview.SetPose(BuildPlacementPose.Position(build.grid,item,anchor,false,0),Quaternion.identity);build.preview.ShowPlacement(build.grid,request,build.CanPlaceAt(anchor));
        AllVisible();Directory.CreateDirectory("Docs/ConstructionWalls");
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/ConstructionWalls/ElevatorPreview.png",starter.blueprint.cameraFocus+new Vector3(-9,12,-14),starter.blueprint.cameraFocus);
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Pending,false);File.WriteAllText(Report+"/result.txt",error==null?"PASS all installed starter walls visible while placing elevator, stairs, floors, walls and shelves at four camera angles; floor switches and furniture movement preserve walls; cancelling wall movement restores it; physics and saved layout unchanged. No player save used.\n":"FAIL "+error);
        if(error==null)
        {
            var files=Directory.GetFiles("Assets/Project/Scripts","*.cs",SearchOption.AllDirectories).Concat(new[]{"Assets/Project/Editor/ConstructionWallsPlay.cs","Assets/Project/Editor/CameraBuildExperienceSetup.cs","Assets/Project/Editor/CameraBuildSandbox.cs","Assets/Project/Editor/CameraBuildExperienceValidation.cs","Assets/Project/Editor/ShopGameplayValidation.cs"});
            File.WriteAllLines(Report+"/tested-sources.sha256",files.Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
        }
        EditorApplication.Exit(error==null?0:1);
    }
}
