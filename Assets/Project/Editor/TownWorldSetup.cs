using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
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
    {
        TownWorldSafety.RequireIsolatedEditor();
        var game=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
        var land=Object.FindObjectOfType<TerritoryPlotLayout>(true);
        var starter=AssetDatabase.LoadAssetAtPath<StarterStoreBlueprint>(StarterStoreSetup.BlueprintPath);
        if(land==null||starter==null)throw new InvalidOperationException("Store land and starter blueprint must exist.");
        Directory.CreateDirectory(TownWorldAssembler.Folder);AssetDatabase.Refresh();
        var layout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>(LayoutPath);
        if(layout==null){layout=ScriptableObject.CreateInstance<TownWorldLayout>();AssetDatabase.CreateAsset(layout,LayoutPath);}
        TownWorldPlan.Configure(layout,land,starter);EditorUtility.SetDirty(layout);
        var town=new TownWorldAssembler(layout).Build();AssetDatabase.SaveAssets();
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
        ConfigurePedestrians(scene,layout);
        var min=layout.sourceShopOrigin+(new Vector3(layout.cityBounds.xMin,0,layout.cityBounds.yMin)-layout.cityShopOrigin)/layout.shopToCityScale;
        var max=layout.sourceShopOrigin+(new Vector3(layout.cityBounds.xMax,0,layout.cityBounds.yMax)-layout.cityShopOrigin)/layout.shopToCityScale;
        camera.minX=min.x+3;camera.maxX=max.x-3;camera.minZ=min.z+3;camera.maxZ=max.z-3;
        camera.maxY=90;camera.maxZoomDistance=130;camera.GetComponent<Camera>().farClipPlane=650;
        var greenery=instance.GetComponentInChildren<TownExpansionScenery>();
        greenery.progression=Object.FindObjectOfType<StoreProgression>();greenery.Refresh();
        EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save the town in Game.");
    }

    private static void ConfigurePedestrians(Scene scene,TownWorldLayout layout)
    {
        const string name="Town pedestrian promenade";
        foreach(var old in scene.GetRootGameObjects().Where(r=>r.name==name))Object.DestroyImmediate(old);
        var route=new GameObject(name);SceneManager.MoveGameObjectToScene(route,scene);
        Vector3[] points={new Vector3(-111,0,-137),new Vector3(-83,0,-137),new Vector3(-58,0,-137),new Vector3(-14,0,-137)};
        var stops=new Transform[points.Length];
        for(int i=0;i<points.Length;i++)
        {
            var stop=new GameObject("Sidewalk stop "+i).transform;stop.SetParent(route.transform,false);
            stop.position=layout.sourceShopOrigin+(points[i]-layout.cityShopOrigin)/layout.shopToCityScale;
            stops[i]=stop;
        }
        foreach(var spawn in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<AvatarSpawn>(true)))
        {
            spawn.spawnPoint=stops[0];spawn.waypoints=stops.Skip(1).ToArray();EditorUtility.SetDirty(spawn);
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
        var camera=new GameObject("Third person driving camera",typeof(Camera),typeof(AudioListener),typeof(PickupCamera));
        camera.tag="MainCamera";camera.transform.SetParent(root.transform,false);camera.GetComponent<Camera>().farClipPlane=850;
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
