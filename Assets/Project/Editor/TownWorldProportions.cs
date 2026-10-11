using System;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Repeatable asset corrections; old player construction keeps its saved geometry version.</summary>
internal static class TownWorldProportions
{
    public static void Apply(GridSystem grid,StarterStoreBlueprint starter)
    {
        ConfigureWall("Assets/Prefabs/Structures/Wall.asset",grid);
        ConfigureWall("Assets/Prefabs/Structures/Wall_Corner.asset",grid);
        var door=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Structures/Door.asset");
        ResizePrefab(door.prefab,model=>
        {
            var bounds=CityUpdateSetup.Measure(model);
            model.transform.localScale=new Vector3(model.transform.localScale.x,model.transform.localScale.y,
                model.transform.localScale.z*grid.cellSize*2/bounds.size.z);
        });
        RefreshBounds(door);
        ConfigureStarterWalls(starter);
        // Absolute length keeps vehicle proportions stable when either scene is regenerated.
        ResizePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/City/Pickup.prefab"),model=>
        {
            var bounds=CityUpdateSetup.Measure(model);
            model.transform.localScale*=4.4f/Mathf.Max(bounds.size.x,bounds.size.z);
            model.transform.Find("Cargo bed").localPosition=new Vector3(0,1,-1.85f);
        });
        ConfigureParking();
        foreach(string id in new[]{"decor_plant","decor_bench","decor_bin","decor_lamp","decor_planter","decor_sign"})
        {
            var item=AssetDatabase.LoadAssetAtPath<BuildItemData>(StoreUpgradeSetup.Folder+"/"+id+".asset");
            if(item.decorationProportionsVersion>=2)continue;
            bool enlarged=id=="decor_bench"||id=="decor_bin"||id=="decor_planter";
            float scale=item.decorationProportionsVersion==1?(enlarged?1.2f:1):id=="decor_lamp"?1/1.3f:enlarged?.6f:.5f;
            // Keep the existing floor-height offset; shrinking the root would sink decor into shop flooring.
            ResizePrefab(item.prefab,model=>
            {
                foreach(Transform child in model.transform)
                {
                    child.localPosition=Vector3.up*.096f+(child.localPosition-Vector3.up*.096f)*scale;
                    child.localScale*=scale;
                }
                var collider=model.GetComponent<BoxCollider>();var bounds=CityUpdateSetup.Measure(model);
                collider.center=model.transform.InverseTransformPoint(bounds.center);
                collider.size=bounds.size;
            });
            RefreshBounds(item);
            item.decorationProportionsVersion=2;EditorUtility.SetDirty(item);
        }
        if(starter.proportionsVersion>=2)return;
        var pickup=Object.FindObjectOfType<RetailEmpireTycoon.City.CityTrip>(true).parkedPickup;
        // Version zero already has the requested original size; version one was reduced.
        if(starter.proportionsVersion==1)pickup.transform.localScale*=1.3f;
        starter.proportionsVersion=2;EditorUtility.SetDirty(starter);
    }

    private static void ConfigureParking()
    {
        var item=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/City/parking_01.asset");
        ResizePrefab(item.prefab,model=>
        {
            var bounds=CityUpdateSetup.Measure(model);
            float factor=(.618f*1.3f)/bounds.size.x;
            foreach(Transform child in model.transform)
            {
                child.localPosition=new Vector3(child.localPosition.x*factor,child.localPosition.y,child.localPosition.z*factor);
                child.localScale=Vector3.Scale(child.localScale,new Vector3(factor,1,factor));
            }
            model.GetComponent<RetailEmpireTycoon.Parking.ParkingBay>().dimensionsScale=1.3f;
        });
        // Round up to complete grid cells: paint and vehicle clearance must remain inside occupancy.
        item.footprint=new Vector2Int(6,11);RefreshBounds(item);
    }

    public static void ConfigureWall(string path,GridSystem grid)
    {
        var item=AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
        if(item.wallGeometryVersion>=3)return;
        string previous=StoreUpgradeSetup.Folder+"/"+item.id+"_v2.prefab";
        if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(item.prefab),previous))throw new InvalidOperationException("Cannot preserve historical wall prefab: "+path);
        item.previousWallPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(previous);
        item.previousWallBounds=item.placementBounds;item.previousWallAlignmentOffset=item.placementAlignmentOffset;
        bool corner=item.id=="wallcorner_01";
        var root=new GameObject(item.id);
        try
        {
            // A thick corner is a solid mitre with the same authored wall profile, not overlapping thin L-shaped faces.
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Structures/Wall.prefab"));
            PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            model.transform.SetParent(root.transform,false);
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            foreach(var marker in model.GetComponentsInChildren<StoreWallOccluder>(true))Object.DestroyImmediate(marker);
            var bounds=CityUpdateSetup.Measure(model);
            model.transform.localScale=Vector3.Scale(model.transform.localScale,new Vector3(grid.cellSize*(corner?2:1)/bounds.size.x,1,grid.cellSize*2/bounds.size.z));
            bounds=CityUpdateSetup.Measure(model);
            var body=root.AddComponent<BoxCollider>();body.center=bounds.center;body.size=bounds.size;
            root.AddComponent<StoreWallOccluder>();
            item.prefab=PrefabUtility.SaveAsPrefabAsset(root,StoreUpgradeSetup.Folder+"/"+item.id+".prefab");
            item.footprint=new Vector2Int(corner?2:1,2);item.pivotOffset=Vector2Int.zero;
            item.placementBounds=bounds;item.placementAlignmentOffset=Vector3.zero;
            item.wallCellSize=grid.cellSize;item.wallGeometryVersion=3;EditorUtility.SetDirty(item);
        }
        finally {Object.DestroyImmediate(root);}
    }

    private static void ConfigureStarterWalls(StarterStoreBlueprint starter)
    {
        var state=starter.initialState;
        if(!state.placedObjects.Any(p=>p.itemId=="wall_01"&&p.wallModuleVersion<3))return;
        starter.previousStarterStructure=state.placedObjects.Where(IsStructure).Select(p=>JsonUtility.FromJson<PlacedBuildSaveData>(JsonUtility.ToJson(p))).ToList();
        var corner=state.placedObjects.Where(p=>p.itemId=="wallcorner_01").OrderBy(p=>p.x).ThenBy(p=>p.z).First();
        int originX=corner.x,originZ=corner.z;
        state.placedObjects.RemoveAll(IsStructure);
        void Add(string id,int x,int z,int facing=0)=>state.placedObjects.Add(new PlacedBuildSaveData
        {itemId=id,x=originX+x,z=originZ+z,facing=facing,rotated=facing%2!=0,wallModuleVersion=3});
        for(int x=2;x<88;x++)
        {
            if(x<54||x>=60)Add("wall_01",x,0);
            Add("wall_01",x,70);
        }
        for(int z=2;z<70;z++)
        {
            if(z<56||z>=62)Add("wall_01",0,z,1);
            Add("wall_01",88,z,1);
            if(z<12||z>=18)Add("wall_01",18,z,1);
        }
        Add("wallcorner_01",0,0);Add("wallcorner_01",88,0,3);Add("wallcorner_01",88,70,2);Add("wallcorner_01",0,70,1);
        Add("door_01",56,0);Add("door_01",18,14,1);Add("unloading_gate_01",0,56,1);
        EditorUtility.SetDirty(starter);
    }
    private static bool IsStructure(PlacedBuildSaveData placed)=>placed.itemId=="wall_01"||placed.itemId=="wallcorner_01"||placed.itemId=="door_01"||placed.itemId=="unloading_gate_01";
    private static void ResizePrefab(GameObject prefab,Action<GameObject> change)
    {
        string path=AssetDatabase.GetAssetPath(prefab);var model=PrefabUtility.LoadPrefabContents(path);
        try {change(model);PrefabUtility.SaveAsPrefabAsset(model,path);}
        finally {PrefabUtility.UnloadPrefabContents(model);}
    }
    private static void RefreshBounds(BuildItemData item)
    {
        var model=Object.Instantiate(item.prefab);
        try {item.placementBounds=CityUpdateSetup.Measure(model);EditorUtility.SetDirty(item);}
        finally {Object.DestroyImmediate(model);}
    }
}
