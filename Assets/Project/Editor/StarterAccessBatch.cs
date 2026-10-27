using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class StarterAccessBatch
{
    public const string Report="Library/StarterAccessQA";
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
            var starter=Object.FindObjectOfType<StarterStoreInitialization>(true);var blueprint=starter.blueprint;
            if(blueprint.initialState.starterSiteVersion<StarterSiteRevision.Version)
            {
                var previous=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(blueprint.initialState));
                var shift=new Vector2Int(8,8);
                var cells=previous.placedObjects.Where(item=>item.itemId!="parking_01").SelectMany(placed=>
                {
                    var item=starter.catalog.GetById(placed.itemId);
                    return starter.building.grid.GetFootprintCells(new Vector3Int(placed.x,placed.floor,placed.z),item.footprint,placed.rotated,item.pivotOffset);
                }).ToArray();
                var bounds=new RectInt(cells.Min(cell=>cell.x),cells.Min(cell=>cell.z),cells.Max(cell=>cell.x)-cells.Min(cell=>cell.x)+1,cells.Max(cell=>cell.z)-cells.Min(cell=>cell.z)+1);
                var strip=new List<FloorTileSaveData>();
                for(int x=bounds.xMin;x<bounds.xMin+shift.x;x++)
                for(int z=bounds.yMin-2;z<bounds.yMax+shift.y+2;z++)strip.Add(new FloorTileSaveData{itemId="asphalt_01",x=x,z=z});
                blueprint.SiteRevision.Configure(previous,shift,bounds,strip);
                if(!blueprint.SiteRevision.TryApply(blueprint.initialState,starter.building.grid,starter.catalog))throw new InvalidOperationException("New starter relocation conflicts with its parking.");
                var delta=new Vector2(shift.x,shift.y)*starter.building.grid.cellSize;
                blueprint.cameraFocus+=new Vector3(delta.x,0,delta.y);
                var old=starter.land.initialBounds;var moved=new Rect(old.position+delta,old.size);
                var corridor=new Rect(bounds.xMin*starter.building.grid.cellSize,(bounds.yMin-2)*starter.building.grid.cellSize,
                    shift.x*starter.building.grid.cellSize,(bounds.height+shift.y+4)*starter.building.grid.cellSize);
                starter.land.initialBounds=Rect.MinMaxRect(Mathf.Min(old.xMin,moved.xMin,corridor.xMin),Mathf.Min(old.yMin,moved.yMin,corridor.yMin),
                    Mathf.Max(old.xMax,moved.xMax,corridor.xMax),Mathf.Max(old.yMax,moved.yMax,corridor.yMax));
                EditorUtility.SetDirty(blueprint);
                File.WriteAllText(Report+"/layout.txt","PASS starter moves "+delta.x+" right and "+delta.y+" back; "+shift.x+"-cell left asphalt strip; street entry and all parking anchors retained. Previous envelope="+bounds+"\n");
                File.WriteAllText(Report+"/previous-starter.json",JsonUtility.ToJson(previous));
            }
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllLines(Report+"/outputs.txt",new[]{AssetDatabase.GetAssetPath(blueprint),CityUpdateSetup.GamePath});
            File.WriteAllText(Report+"/build.txt","PASS relocated default starter, connected asphalt access, preserved street and parking; no player save read or written.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    public static void Record(string stage)
    {
        var files=File.ReadAllLines(Report+"/outputs.txt").Concat(Directory.GetFiles("Assets/Project/Scripts","*.cs",SearchOption.AllDirectories))
            .Concat(AssetDatabase.GetDependencies(new[]{CityUpdateSetup.GamePath,CityUpdateSetup.CityPath},true).Where(path=>path.StartsWith("Assets/",StringComparison.Ordinal)&&File.Exists(path)))
            .Concat(new[]{"Assets/Project/Editor/StarterAccessBatch.cs","Assets/Project/Editor/MultiStoreyValidation.cs","Assets/Project/Editor/ConstructionWorkspacePlay.cs"})
            .Distinct().OrderBy(path=>path,StringComparer.Ordinal);
        File.WriteAllLines(Report+"/"+stage+"-inputs.sha256",files.Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
    }
}
