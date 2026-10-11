using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.Territory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class TownWorldSetup
{
    public const string LayoutPath=TownWorldAssembler.Folder+"/TownLayout.asset";

    public static void Generate()
        => Generate(TownhousePilotVariant.FiveBuildings);

    internal static void Generate(TownhousePilotVariant pilotVariant)
    {
        TownWorldSafety.RequireIsolatedEditor();
        var game=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
        var land=Object.FindObjectOfType<TerritoryPlotLayout>(true);
        var starter=AssetDatabase.LoadAssetAtPath<StarterStoreBlueprint>(StarterStoreSetup.BlueprintPath);
        if(land==null||starter==null)throw new InvalidOperationException("Store land and starter blueprint must exist.");
        TownWorldProportions.Apply(land.grid,starter);
        ConfigureStarterFrontage(starter,land);
        Directory.CreateDirectory(TownWorldAssembler.Folder);AssetDatabase.Refresh();
        var layout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>(LayoutPath);
        if(layout==null){layout=ScriptableObject.CreateInstance<TownWorldLayout>();AssetDatabase.CreateAsset(layout,LayoutPath);}
        TownWorldPlan.Configure(layout,land,starter);EditorUtility.SetDirty(layout);
        TownWorldArtDirection.ConfigureStoreAsphalt(layout);
        var town=new TownWorldAssembler(layout,pilotVariant).Build();AssetDatabase.SaveAssets();
        TownWorldArtDirection.GraphicsProfiles();
        ConfigureGame(game,town,layout);
        ConfigureCity(town,layout);AssetDatabase.SaveAssets();
    }

    private static void ConfigureGame(Scene scene,GameObject prefab,TownWorldLayout layout)
    {
        var world=scene.GetRootGameObjects().Single(r=>r.name=="World");
        // Keep referenced routes and territory services. Only superseded scenery is deactivated.
        string[] legacy={"Sidewalk","House Objects","Forest","Cars","Drive Road","Ground","Expanded plot ground","Extended world ground","Traffic Car"};
        foreach(string name in legacy)
        {
            var child=world.transform.Find(name);if(child!=null)child.gameObject.SetActive(false);
        }
        var previous=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="Shared town environment");
        if(previous!=null)Object.DestroyImmediate(previous);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.name="Shared town environment";
        instance.transform.localScale=Vector3.one/layout.shopToCityScale;
        instance.transform.position=layout.sourceShopOrigin-layout.cityShopOrigin/layout.shopToCityScale;
        var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MainCamera>(true)).Single();
        foreach(var old in scene.GetRootGameObjects().Where(r=>r.name=="Town atmosphere"))Object.DestroyImmediate(old);
        var sunlight=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>()).First(l=>l.type==LightType.Directional);
        TownWorldArtDirection.Apply(sunlight,camera.GetComponent<Camera>(),1/layout.shopToCityScale);
        var traffic=Object.FindObjectOfType<RetailEmpireTycoon.Parking.ParkingTraffic>(true);
        if(traffic!=null&&traffic.roadEntry!=null)
        {
            traffic.roadEntry.position=layout.sourceShopOrigin+(layout.StoreStreetEntry-layout.cityShopOrigin)/layout.shopToCityScale;
            traffic.roadEntry.rotation=Quaternion.identity;
            var approach=traffic.transform.Find("Store driveway entry");
            if(approach==null){approach=new GameObject("Store driveway entry").transform;approach.SetParent(traffic.transform,false);}
            approach.position=layout.sourceShopOrigin+(new Vector3(-24,0,-120)-layout.cityShopOrigin)/layout.shopToCityScale;
            traffic.drivewayEntry=approach;
        }
        RemoveDecorativePedestrians(scene);
        var min=layout.sourceShopOrigin+(new Vector3(layout.cityBounds.xMin,0,layout.cityBounds.yMin)-layout.cityShopOrigin)/layout.shopToCityScale;
        var max=layout.sourceShopOrigin+(new Vector3(layout.cityBounds.xMax,0,layout.cityBounds.yMax)-layout.cityShopOrigin)/layout.shopToCityScale;
        camera.minX=min.x+3;camera.maxX=max.x-3;camera.minZ=min.z+3;camera.maxZ=max.z-3;
        camera.maxY=90;camera.maxZoomDistance=130;camera.GetComponent<Camera>().farClipPlane=650;
        var greenery=instance.GetComponentInChildren<TownExpansionScenery>();
        greenery.progression=Object.FindObjectOfType<StoreProgression>();greenery.Refresh();
        EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save the town in Game.");
    }

    private static void ConfigureStarterFrontage(StarterStoreBlueprint starter,TerritoryPlotLayout land)
    {
        var grid=land.grid;
        var start=grid.WorldToCell(new Vector3(land.initialBounds.xMin+.001f,0,land.initialBounds.yMin+.001f));
        var state=starter.initialState;
        if(starter.legacyPlayerParking==null||string.IsNullOrEmpty(starter.legacyPlayerParking.itemId))
        {
            var old=state.placedObjects.SingleOrDefault(p=>p.itemId=="parking_01"&&p.playerParking&&p.x==start.x-16&&p.z==start.z+8&&p.facing==3);
            if(old!=null)starter.legacyPlayerParking=JsonUtility.FromJson<PlacedBuildSaveData>(JsonUtility.ToJson(old));
        }
        state.placedObjects.RemoveAll(p=>p.itemId=="parking_01");
        foreach(int x in new[]{20,36,52})state.placedObjects.Add(new PlacedBuildSaveData
        {
            itemId="parking_01",x=start.x+x,z=start.z-24,facing=2,rotated=false,
            wallModuleVersion=BuildItemData.CurrentWallModuleVersion,playerParking=x==20
        });
        state.floorTiles.RemoveAll(p=>p.itemId=="asphalt_01");
        void Asphalt(int xMin,int xMax,int zMin,int zMax)
        {
            for(int x=xMin;x<xMax;x++)for(int z=zMin;z<zMax;z++)
                state.floorTiles.Add(new FloorTileSaveData{itemId="asphalt_01",x=start.x+x,z=start.z+z});
        }
        Asphalt(-24,90,-48,0);Asphalt(-24,0,0,72);
        starter.serviceYard=new Rect(land.initialBounds.xMin-24*grid.cellSize,land.initialBounds.yMin-48*grid.cellSize,
            114*grid.cellSize,120*grid.cellSize);
        var parking=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/City/parking_01.asset");
        starter.pickupPosition=BuildPlacementPose.Position(grid,parking,start+new Vector3Int(20,0,-24),false,2);
        starter.pickupEuler=new Vector3(0,180,0);
        EditorUtility.SetDirty(starter);
    }

    private static void RemoveDecorativePedestrians(Scene scene)
    {
        const string name="Town pedestrian promenade";
        foreach(var old in scene.GetRootGameObjects().Where(r=>r.name==name))Object.DestroyImmediate(old);
        // AvatarSpawn only produces background passersby. StoreOperations owns actual customers.
        foreach(var spawn in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<AvatarSpawn>(true)))
        {
            spawn.enabled=false;
            spawn.spawnPoint=null;
            spawn.waypoints=Array.Empty<Transform>();
            EditorUtility.SetDirty(spawn);
        }
    }

    private static void ConfigureCity(GameObject prefab,TownWorldLayout layout)
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var oldWorld=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CityWorld>(true)).Single();
        var pickupPrefab=PrefabUtility.GetCorrespondingObjectFromSource(oldWorld.pickup.gameObject);
        var cargoBox=oldWorld.cargoBoxPrefab;
        if(pickupPrefab==null||cargoBox==null)throw new InvalidOperationException("Existing driving pickup/cargo models are missing.");
        foreach(var previous in scene.GetRootGameObjects())Object.DestroyImmediate(previous);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
        var places=instance.GetComponent<TownWorldPlaces>();
        var root=new GameObject("City driving and deliveries");SceneManager.MoveGameObjectToScene(root,scene);
        var world=root.AddComponent<CityWorld>();world.town=places;
        world.useFixedShopOrigin=true;world.sourceShopOrigin=layout.sourceShopOrigin;
        world.shopCenter=layout.cityShopOrigin;world.shopScale=layout.shopToCityScale;
        string roofPath=TownWorldAssembler.Folder+"/StoreRoof.mat";
        var roof=AssetDatabase.LoadAssetAtPath<Material>(roofPath);
        if(roof==null){roof=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(roof,roofPath);}
        roof.SetColor("_BaseColor",new Color(.44f,.65f,.56f));roof.SetFloat("_Smoothness",0);EditorUtility.SetDirty(roof);world.shopRoofMaterial=roof;
        world.depot=places.suppliers.Single(c=>c.supplier==SupplierKind.Wholesale);
        world.facadeRoot=new GameObject("Synchronized player store").transform;world.facadeRoot.SetParent(root.transform,false);
        world.home=Checkpoint(root.transform,"Мой магазин",layout.cityShopOrigin+new Vector3(-5,.05f,4),CheckpointKind.Return);
        world.unload=Checkpoint(root.transform,"Разгрузка магазина",layout.cityShopOrigin+new Vector3(-10,.05f,26),CheckpointKind.Unload);
        var vehicle=(GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab,scene);vehicle.name="Driving pickup";
        vehicle.transform.SetParent(root.transform,false);vehicle.transform.position=world.home.transform.position+Vector3.up*.12f;
        world.pickup=vehicle.GetComponent<PickupDrive>();world.cargoRoot=vehicle.transform.Find("Cargo bed");world.cargoBoxPrefab=cargoBox;
        var recovery=root.AddComponent<CityRiverRecovery>();recovery.pickup=world.pickup;recovery.town=places;
        var camera=new GameObject("Third person driving camera",typeof(Camera),typeof(AudioListener),typeof(PickupCamera));
        camera.tag="MainCamera";camera.transform.SetParent(root.transform,false);camera.GetComponent<Camera>().farClipPlane=1250;
        camera.transform.position=vehicle.transform.position+new Vector3(0,4,-8);
        world.chaseCamera=camera.GetComponent<PickupCamera>();world.chaseCamera.target=vehicle.transform;
        var sun=new GameObject("Town daylight",typeof(Light));sun.transform.SetParent(root.transform,false);sun.transform.rotation=Quaternion.Euler(48,-135,0);
        var light=sun.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.shadows=LightShadows.Soft;
        RenderSettings.sun=light;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        TownWorldArtDirection.Apply(light,camera.GetComponent<Camera>(),1);
        EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save City.");
    }

    private static DeliveryCheckpoint Checkpoint(Transform parent,string name,Vector3 position,CheckpointKind kind)
    {
        var instance=new GameObject(name);instance.transform.SetParent(parent,false);instance.transform.position=position;
        var checkpoint=instance.AddComponent<DeliveryCheckpoint>();checkpoint.kind=kind;checkpoint.radius=4;checkpoint.displayName=name;
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Logistics/PickupStopZone.prefab");
        var zone=(GameObject)PrefabUtility.InstantiatePrefab(source);zone.transform.SetParent(instance.transform,false);
        return checkpoint;
    }
}
