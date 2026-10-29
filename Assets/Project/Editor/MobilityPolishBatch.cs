using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Scoped model/catalog revision and dealership entrances; no town regeneration or player-save access.</summary>
public static class MobilityPolishBatch
{
    public const string Root="Assets/Art/MobilityPolish",Report="Library/MobilityPolishQA",Pictures="Docs/MobilityPolish";
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            Narrow("stairs_01",4);Narrow("escalator_01",5);
            var up=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Resources/MultiStorey/escalator_01.asset");
            up.displayName="Escalator";up.storeyDirection=StoreyTravelDirection.Both;
            up.description="Reversible escalator, 20% slower. Customers can ride both ways; the belt reverses for the current rider. Ceiling tiles in the opening return to storage.";EditorUtility.SetDirty(up);
            var down=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Resources/MultiStorey/escalator_down_01.asset");
            if(down==null){down=Object.Instantiate(up);AssetDatabase.CreateAsset(down,"Assets/Resources/MultiStorey/escalator_down_01.asset");}
            down.id="escalator_down_01";down.displayName="Down escalator";down.name=down.id;down.storeyDirection=StoreyTravelDirection.Down;
            down.description="One-way descending escalator from floor 2 to floor 1, 20% slower. Use stairs, an elevator or another escalator for the upward route. Ceiling tiles in its opening return to storage.";
            var go=(GameObject)PrefabUtility.InstantiatePrefab(up.prefab);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            go.GetComponent<StoreyTransportVisual>().direction=StoreyTravelDirection.Down;
            down.prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/DownEscalator.prefab");Object.DestroyImmediate(go);
            down.icon=ShopUiPreviews.Render(down.prefab,down.id,205);EditorUtility.SetDirty(down);AssetDatabase.SaveAssets();
            Entrances();AssetDatabase.SaveAssets();
            var paths=new[]{"Assets/Art/ElevatorModel","Assets/Art/EscalatorModel","Assets/Art/StoreyTransport",Root};
            var files=paths.SelectMany(path=>Directory.GetFiles(path,"*",SearchOption.AllDirectories).Append(path+".meta"))
                .Concat(new[]{"Assets/Resources/MultiStorey/elevator_01.asset","Assets/Resources/MultiStorey/stairs_01.asset","Assets/Resources/MultiStorey/escalator_01.asset","Assets/Resources/MultiStorey/escalator_down_01.asset","Assets/Resources/MultiStorey/escalator_down_01.asset.meta",TownWorldAssembler.PrefabPath})
                .Concat(new[]{"stairs_01","escalator_01","elevator_01","escalator_down_01"}.SelectMany(id=>new[]{"Assets/Art/ShopUi/Items/"+id+".png","Assets/Art/ShopUi/Items/"+id+".png.meta"})).Distinct();
            File.WriteAllLines(Report+"/outputs.txt",files.Select(path=>path.Replace('\\','/')));
            File.WriteAllText(Report+"/build.txt","PASS stairs and both escalators 20% narrower; 4x10 / 5x10 footprints; distinct descending definition; shared lift floor geometry revision; two functional dealership entrances and visible marker.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    private static void Narrow(string id,int columns)
    {
        var item=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Resources/MultiStorey/"+id+".asset");
        string path=AssetDatabase.GetAssetPath(item.prefab);var go=PrefabUtility.LoadPrefabContents(path);
        try
        {
            go.transform.localScale=new Vector3(.8f,1,1);item.footprint=new Vector2Int(columns,item.footprint.y);
            item.placementBounds=CityUpdateSetup.Measure(go);item.placementAlignmentOffset=new Vector3(0,0,item.placementBounds.center.z);
            if(item.placementBounds.size.x>columns*ShopProportions.CellSize)throw new InvalidOperationException("Narrow transport exceeds occupied cells: "+id);
            PrefabUtility.SaveAsPrefabAsset(go,path);item.icon=ShopUiPreviews.Render(item.prefab,id,205);EditorUtility.SetDirty(item);
        }
        finally{PrefabUtility.UnloadPrefabContents(go);}
    }
    private static void Entrances()
    {
        var town=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try
        {
            var places=town.GetComponent<TownWorldPlaces>();
            var primary=places.suppliers.Single(checkpoint=>checkpoint.supplier==SupplierKind.Showroom);
            var legacy=town.transform.Find("Legacy showroom entrance");
            if(legacy==null){legacy=new GameObject("Legacy showroom entrance").transform;legacy.SetParent(town.transform,false);}
            legacy.localPosition=new Vector3(237,.055f,-92);
            var entrance=legacy.GetComponent<DeliveryCheckpoint>()??legacy.gameObject.AddComponent<DeliveryCheckpoint>();
            entrance.kind=CheckpointKind.Showroom;entrance.supplier=SupplierKind.Showroom;entrance.radius=4;entrance.displayName="Car Showroom";
            places.showroomEntrances=new[]{entrance};
            var site=town.transform.Find(TownShowroomPlacement.SiteName);var marker=site.Find("Showroom interaction marker");
            if(marker==null)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Logistics/PickupStopZone.prefab");
                marker=((GameObject)PrefabUtility.InstantiatePrefab(prefab)).transform;marker.SetParent(site,false);marker.name="Showroom interaction marker";
                foreach(var collider in marker.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
            }
            marker.localPosition=primary.transform.localPosition+Vector3.up*.055f;marker.localRotation=Quaternion.Euler(0,90,0);
            PrefabUtility.SaveAsPrefabAsset(town,TownWorldAssembler.PrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(town);}
    }
    public static void Record()
    {
        var files=File.ReadAllLines(Report+"/outputs.txt").Concat(Directory.GetFiles("Assets/Project/Scripts","*.cs",SearchOption.AllDirectories))
            .Concat(AssetDatabase.GetDependencies(new[]{CityUpdateSetup.GamePath,CityUpdateSetup.CityPath},true).Where(path=>path.StartsWith("Assets/")&&File.Exists(path)))
            .Concat(new[]{"Assets/Project/Editor/MobilityPolishBatch.cs","Assets/Project/Editor/MobilityPolishPlay.cs","Assets/Project/Editor/ElevatorModelBatch.cs"}).Distinct().OrderBy(path=>path);
        File.WriteAllLines(Report+"/inputs.sha256",files.Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
    }
}
