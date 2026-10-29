using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class StarterFrontageBatch
{
    public const string Report="Library/StarterFrontageQA",Pictures="Docs/StarterFrontage";
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
            EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
            var starter=Object.FindObjectOfType<StarterStoreInitialization>(true);var blueprint=starter.blueprint;
            if(blueprint.initialState.starterSiteVersion<StarterForecourtRevision.Version)
            {
                var previous=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(blueprint.initialState));
                var after=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(previous));
                foreach(var placed in after.placedObjects.Where(item=>item.itemId!="parking_01"))placed.z-=2;
                foreach(var tile in after.floorTiles.Where(tile=>tile.itemId!="asphalt_01"))tile.z-=2;
                foreach(var stock in after.shelfStocks)stock.anchorZ-=2;
                var bays=after.placedObjects.Where(item=>item.itemId=="parking_01").ToArray();
                bays.Single(item=>item.playerParking).x=25;
                int index=0;foreach(var bay in bays.Where(item=>!item.playerParking))bay.x=index++==0?11:39;
                foreach(var bay in bays){bay.z=-32;bay.rotated=false;bay.facing=2;}
                after.floorTiles.RemoveAll(tile=>tile.floor==0 && tile.itemId=="asphalt_01");
                var asphalt=new HashSet<Vector2Int>();
                void Rectangle(int minX,int maxX,int minZ,int maxZ)
                {for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)asphalt.Add(new Vector2Int(x,z));}
                Rectangle(-5,51,-40,-12);Rectangle(-3,11,-69,-41);Rectangle(-5,2,-11,20);
                var indoor=after.floorTiles.Where(tile=>tile.floor==0).Select(tile=>new Vector2Int(tile.x,tile.z)).ToHashSet();
                after.floorTiles.AddRange(asphalt.Where(cell=>!indoor.Contains(cell)).OrderBy(cell=>cell.x).ThenBy(cell=>cell.y)
                    .Select(cell=>new FloorTileSaveData{itemId="asphalt_01",x=cell.x,z=cell.y}));
                after.starterSiteVersion=StarterForecourtRevision.Version;
                blueprint.ForecourtRevision.Configure(previous,after,new RectInt(3,-9,36,30),new Vector2Int(0,-2));
                blueprint.initialState=after;blueprint.cameraFocus+=Vector3.back*(2*starter.building.grid.cellSize);
                blueprint.pickupPosition=RetailEmpireTycoon.BuildSystem.BuildPlacementPose.Position(starter.building.grid,
                    starter.catalog.GetById("parking_01"),new Vector3Int(25,0,-32),false,2);
                File.WriteAllText(Report+"/previous.json",JsonUtility.ToJson(previous));EditorUtility.SetDirty(blueprint);AssetDatabase.SaveAssets();
            }
            File.WriteAllLines(Report+"/outputs.txt",new[]{AssetDatabase.GetAssetPath(blueprint)});
            File.WriteAllText(Report+"/build.txt","PASS starter 2 cells closer to sidewalk; complete forecourt, straight connected driveway, clean warehouse strip, three evenly spaced bays.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    public static void Record()
    {
        var files=File.ReadAllLines(Report+"/outputs.txt").Concat(Directory.GetFiles("Assets/Project/Scripts","*.cs",SearchOption.AllDirectories))
            .Concat(AssetDatabase.GetDependencies(new[]{CityUpdateSetup.GamePath,CityUpdateSetup.CityPath},true).Where(path=>path.StartsWith("Assets/") && File.Exists(path)))
            .Concat(new[]{"Assets/Project/Editor/StarterFrontageBatch.cs","Assets/Project/Editor/ConstructionWorkspacePlay.cs","Assets/Project/Editor/MultiStoreyValidation.cs"}).Distinct().OrderBy(path=>path);
        File.WriteAllLines(Report+"/play-inputs.sha256",files.Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
    }
}
