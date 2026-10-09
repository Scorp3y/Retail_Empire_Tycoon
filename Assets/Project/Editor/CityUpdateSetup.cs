using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.Parking;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit, repeatable asset/scene migration. Does not read or write the player's save.</summary>
public static class CityUpdateSetup
{
    public const string GamePath="Assets/Project/Scenes/Game.unity";
    public const string CityPath="Assets/Project/Scenes/City.unity";
    private const string Folder="Assets/Prefabs/City";
    private const string Town="Assets/Models/lowpoly models/Pandazole City Town Pack/Prefabs/";

    [MenuItem("Retail Empire/City/Apply city, deliveries, bakery and parking")]
    public static void Apply()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path!=GamePath)
            throw new InvalidOperationException("Apply to the saved Game scene in Edit Mode.");
        ValidateSources(); Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        ShopAssortmentSetup.Apply();
        ConfigureWeights();
        var pickup=PickupPrefab(); var box=BoxPrefab(); var rack=StorageRackPrefab(box); var gate=GatePrefab(); var bay=BayPrefab();
        var asphalt=BuildItem("asphalt_01","Асфальт",8,Vector2Int.one,Object.FindObjectOfType<FloorPainter>(true).floorTilePrefab);
        asphalt.placementKind=PlacementKind.Floor; asphalt.floorMaterial=Mat("Asphalt",new Color(.16f,.19f,.19f)); asphalt.isAsphalt=true;
        asphalt.weightKg=2; asphalt.cargoVolumeM3=.012f;
        var parking=BuildItem("parking_01","Парковка",40,new Vector2Int(4,8),bay); parking.isParkingSpace=true;
        parking.ruleFlags=PlacementRuleFlags.InsidePurchasedArea|PlacementRuleFlags.NoOverlap; parking.weightKg=1; parking.cargoVolumeM3=.02f;
        var unloading=BuildItem("unloading_gate_01","Ворота разгрузки",180,new Vector2Int(6,2),gate); unloading.isUnloadingGate=true;
        unloading.weightKg=70; unloading.cargoVolumeM3=.5f;
        var storage=BuildItem("storage_rack_01","Складской стеллаж",100,new Vector2Int(6,3),rack); storage.weightKg=45; storage.cargoVolumeM3=.5f;
        foreach(var item in new[]{asphalt,parking,unloading,storage})
        {
            foreach(var shop in Object.FindObjectsOfType<ShopWindow>(true)) Append(shop,"buildCatalog",item);
            foreach(var catalog in Object.FindObjectsOfType<BuildItemCatalog>(true)) Append(catalog,"items",item);
            EditorUtility.SetDirty(item);
        }
        var services=GameObject.Find("City and delivery services")??new GameObject("City and delivery services");
        var orders=services.GetComponent<DeliveryOrders>()??services.AddComponent<DeliveryOrders>();
        orders.money=Object.FindObjectOfType<MoneyController>(true); orders.buildings=Object.FindObjectOfType<BuildInventory>(true);
        orders.products=Object.FindObjectOfType<ProductInventory>(true); orders.buildCatalog=Object.FindObjectOfType<BuildItemCatalog>(true);
        orders.productCatalog=Object.FindObjectOfType<ProductCatalog>(true); orders.save=Object.FindObjectOfType<SaveManager>(true);
        var trip=services.GetComponent<CityTrip>()??services.AddComponent<CityTrip>(); trip.orders=orders;
        trip.controls=Object.FindObjectOfType<GameplayControls>(true); trip.work=Object.FindObjectOfType<WorkMinigame>(true);
        trip.uiRoot=((Canvas)ShopUiSetup.Read(Object.FindObjectOfType<ShopOperationsHud>(true),"canvas")).transform;
        var parked=GameObject.Find("Player pickup");
        if(parked==null)
        {
            parked=(GameObject)PrefabUtility.InstantiatePrefab(pickup);
            parked.name="Player pickup"; parked.transform.localScale=Vector3.one*.18f;
            var lots=GameObject.Find("Parking Spaces");
            var reference=lots!=null ? lots.transform.GetComponentsInChildren<Renderer>().FirstOrDefault() : null;
            parked.transform.position=reference!=null ? reference.bounds.center+Vector3.up*.04f : new Vector3(-2,.04f,-1);
            parked.transform.position=new Vector3(parked.transform.position.x,.025f,parked.transform.position.z);
            Object.DestroyImmediate(parked.GetComponent<PickupDrive>()); Object.DestroyImmediate(parked.GetComponent<Rigidbody>());
        }
        trip.parkedPickup=parked;
        foreach(var shop in Object.FindObjectsOfType<ShopWindow>(true)) ShopUiSetup.Set(shop,"deliveryOrders",orders);
        var traffic=services.GetComponent<ParkingTraffic>()??services.AddComponent<ParkingTraffic>();
        traffic.grid=Object.FindObjectOfType<GridSystem>(true); traffic.asphalt=Object.FindObjectOfType<FloorPainter>(true);
        traffic.carPrefab=ParkingCarPrefab();
        var entry=services.transform.Find("Parking road entry");
        if(entry==null) {entry=new GameObject("Parking road entry").transform;entry.SetParent(services.transform,false);entry.position=parked.transform.position+Vector3.forward*1.5f;}
        traffic.roadEntry=entry;
        var notice=services.GetComponent<DeliveryNotice>()??services.AddComponent<DeliveryNotice>(); notice.orders=orders; notice.uiRoot=trip.uiRoot;
        ConfigureStorageStatus(orders);
        AssetDatabase.SaveAssets(); ShopUiPreviews.Generate();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        BuildCityScene(pickup,box,rack,gate);
        var scenes=EditorBuildSettings.scenes.ToList();
        if(!scenes.Any(s=>s.path==CityPath)) scenes.Add(new EditorBuildSettingsScene(CityPath,true));
        else scenes.First(s=>s.path==CityPath).enabled=true;
        EditorBuildSettings.scenes=scenes.ToArray(); AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(GamePath);
        Debug.Log("City update applied. Game save untouched. Orders now await depot pickup; six selected bakery models registered.");
    }
    private static void ValidateSources()
    {
        foreach(string name in ShopBakeryAssortment.Models)
            Require("Assets/Yodah_the_Cat/Stylized bread serving/Prefabs/"+name+".prefab");
        Require(Town+"Env_ResidentBuilding_01.prefab"); Require(Town+"Env_CommercialBuilding_01.prefab");
        Require("Assets/Models/Island/Low Poly Atmospheric Locations Pack/Prefabs/Vehicles/car SUVs pickups lights red.prefab");
    }
    private static void ConfigureStorageStatus(DeliveryOrders orders)
    {
        var inventory=Object.FindObjectOfType<RetailEmpireTycoon.UI.Windows.BuildInventoryWindow>(true);
        var status=inventory.GetComponent<ShopStorageStatus>()??inventory.gameObject.AddComponent<ShopStorageStatus>();status.orders=orders;
        if(status.label==null)
        {
            var ui=new RetailEmpireTycoon.StoreOperations.ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            status.label=ui.Label(inventory.transform,"",Vector2.zero,new Vector2(580,26),16);
            var rect=status.label.rectTransform;rect.name="Storage weight";rect.anchorMin=Vector2.zero;rect.anchorMax=new Vector2(1,0);
            rect.pivot=new Vector2(.5f,0);rect.sizeDelta=new Vector2(-36,26);rect.anchoredPosition=new Vector2(0,12);
        }
        var host=((GameObject)ShopUiSetup.Read(inventory,"mainCategoriesPanel")).GetComponent<RectTransform>();
        host.offsetMin=new Vector2(host.offsetMin.x,48);
        var empty=(GameObject)ShopUiSetup.Read(inventory,"emptyLabel");
        empty.GetComponent<ShopLocalizedLabel>()?.Set("Пока пусто. Закажите товар и доставьте его со склада в городе.","Nothing here yet. Order stock and collect it from the city depot.");
    }
    private static void Require(string path)
    { if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null) throw new InvalidOperationException("Missing installed model: "+path); }
    private static void ConfigureWeights()
    {
        foreach(var guid in AssetDatabase.FindAssets("t:ProductItemData",new[]{"Assets/Prefabs"}))
        {
            var item=AssetDatabase.LoadAssetAtPath<ProductItemData>(AssetDatabase.GUIDToAssetPath(guid));
            float weight=item.Id=="product_milk"?1:item.StorageType==ProductStorageType.Bakery?.35f:item.Id=="product_eggs"?.06f:.5f;
            var data=new SerializedObject(item);data.FindProperty("unitWeightKg").floatValue=weight;
            data.FindProperty("unitVolumeM3").floatValue=item.StorageType==ProductStorageType.Bakery?.015f:.01f;data.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var guid in AssetDatabase.FindAssets("t:BuildItemData",new[]{"Assets/Prefabs"}))
        {
            var item=AssetDatabase.LoadAssetAtPath<BuildItemData>(AssetDatabase.GUIDToAssetPath(guid));
            item.weightKg=item.placementKind==PlacementKind.Floor?2:item.category==BuildCategory.Shelf?60:35;
            item.cargoVolumeM3=item.placementKind==PlacementKind.Floor?.012f:item.category==BuildCategory.Shelf?.6f:.3f;
            EditorUtility.SetDirty(item);
        }
    }
    private static BuildItemData FindBuild(string id) => AssetDatabase.FindAssets("t:BuildItemData",new[]{"Assets/Prefabs"})
        .Select(g=>AssetDatabase.LoadAssetAtPath<BuildItemData>(AssetDatabase.GUIDToAssetPath(g))).First(i=>i.id==id);
    private static BuildItemData BuildItem(string id,string name,int price,Vector2Int footprint,GameObject prefab)
    {
        string path=Folder+"/"+id+".asset"; var item=AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
        if(item==null) {item=ScriptableObject.CreateInstance<BuildItemData>();AssetDatabase.CreateAsset(item,path);}
        item.id=id;item.displayName=name;item.category=BuildCategory.Structures;item.price=price;item.footprint=footprint;item.prefab=prefab;
        item.ruleFlags=PlacementRuleFlags.InsidePurchasedArea|PlacementRuleFlags.NoOverlap|PlacementRuleFlags.RequireAccessibility;
        item.previewValidMaterial=FindBuild("shelf_fresh_01").previewValidMaterial;item.previewInvalidMaterial=FindBuild("shelf_fresh_01").previewInvalidMaterial;
        if(prefab==null) throw new InvalidOperationException("Build item has no visual prefab: "+id);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try {item.placementBounds=Measure(model);item.alignModelToFootprint=true;}
        finally {Object.DestroyImmediate(model);}
        return item;
    }
    private static GameObject PickupPrefab()
    {
        var root=new GameObject("Pickup");
        try
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Island/Low Poly Atmospheric Locations Pack/Prefabs/Vehicles/car SUVs pickups lights red.prefab");
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            model.transform.SetParent(root.transform,false);Clean(model); Normalize(model,4.4f);
            var bounds=Measure(model);var body=root.AddComponent<BoxCollider>();
            body.size=new Vector3(Mathf.Min(1.9f,bounds.size.x),Mathf.Min(1.6f,bounds.size.y),Mathf.Min(4.3f,bounds.size.z));
            body.center=new Vector3(bounds.center.x,body.size.y*.5f,bounds.center.z);
            string contactPath=Folder+"/VehicleContact.physicMaterial";
            var contact=AssetDatabase.LoadAssetAtPath<PhysicMaterial>(contactPath);
            if(contact==null) {contact=new PhysicMaterial("Vehicle contact");AssetDatabase.CreateAsset(contact,contactPath);}
            contact.dynamicFriction=0;contact.staticFriction=0;contact.frictionCombine=PhysicMaterialCombine.Minimum;
            EditorUtility.SetDirty(contact);body.sharedMaterial=contact;
            root.AddComponent<Rigidbody>();root.AddComponent<PickupDrive>();
            var cargo=new GameObject("Cargo bed").transform;cargo.SetParent(root.transform,false);cargo.localPosition=new Vector3(0,1,-1.5f);
            return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Pickup.prefab");
        }
        finally {Object.DestroyImmediate(root);}
    }
    private static GameObject ParkingCarPrefab()
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/sedan.prefab"));
        try {PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);Clean(root);Normalize(root,.65f);
            var bounds=Measure(root);var collider=root.AddComponent<BoxCollider>();
            collider.center=root.transform.InverseTransformPoint(bounds.center);
            var scale=root.transform.lossyScale;
            collider.size=Vector3.Scale(bounds.size,new Vector3(1/scale.x,1/scale.y,1/scale.z));
            return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/ParkingCar.prefab");}
        finally {Object.DestroyImmediate(root);}
    }
    private static GameObject BoxPrefab()
    {
        var root=new GameObject("Shipment box");
        try
        {
            Cube(root.transform,"Cardboard",new Vector3(0,.23f,0),new Vector3(.6f,.46f,.5f),Mat("Cardboard",new Color(.59f,.41f,.22f)),false);
            Cube(root.transform,"Packing tape",new Vector3(0,.465f,0),new Vector3(.08f,.008f,.5f),Mat("Tape",new Color(.85f,.73f,.5f)),false);
            return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/ShipmentBox.prefab");
        }
        finally {Object.DestroyImmediate(root);}
    }
    private static GameObject StorageRackPrefab(GameObject box)
    {
        var root=new GameObject("Storage rack with boxes");
        try
        {
            var steel=Mat("Steel",new Color(.20f,.29f,.28f));var shelf=Mat("Plywood",new Color(.52f,.39f,.24f));
            foreach(float x in new[]{-.45f,.45f}) foreach(float z in new[]{-.20f,.20f}) Cube(root.transform,"Frame",new Vector3(x,.48f,z),new Vector3(.025f,.96f,.025f),steel,false);
            for(int tier=0;tier<3;tier++)
            {
                float y=.08f+tier*.30f;Cube(root.transform,"Deck",new Vector3(0,y,0),new Vector3(.90f,.025f,.44f),shelf,false);
                for(int column=0;column<3;column++) {var crate=(GameObject)PrefabUtility.InstantiatePrefab(box);crate.transform.SetParent(root.transform,false);crate.transform.localScale=Vector3.one*.40f;crate.transform.localPosition=new Vector3(-.28f+column*.28f,y+.014f,0);}
            }
            var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,.48f,0);collider.size=new Vector3(.94f,.96f,.48f);
            root.AddComponent<PlacedObject>();return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/StorageRack.prefab");
        }
        finally {Object.DestroyImmediate(root);}
    }
    private static GameObject GatePrefab()
    {
        var root=new GameObject("Unloading gate");
        try
        {
            var green=Mat("WarehouseGreen",new Color(.18f,.30f,.25f));var metal=Mat("DoorSteel",new Color(.58f,.64f,.59f));
            foreach(float x in new[]{-.48f,.48f}) Cube(root.transform,"Door frame",new Vector3(x,.42f,0),new Vector3(.04f,.84f,.12f),green,false);
            Cube(root.transform,"Header",new Vector3(0,.84f,0),new Vector3(1,.05f,.12f),green,false);
            for(int i=0;i<10;i++) Cube(root.transform,"Roller slat",new Vector3(0,.04f+i*.08f,0),new Vector3(.92f,.072f,.055f),metal,false);
            Cube(root.transform,"Dock apron",new Vector3(0,.005f,.27f),new Vector3(1,.01f,.45f),Mat("DockYellow",new Color(.94f,.68f,.08f)),false);
            var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,.42f,0);collider.size=new Vector3(1,.87f,.12f);
            root.AddComponent<PlacedObject>();return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/UnloadingGate.prefab");
        }
        finally {Object.DestroyImmediate(root);}
    }
    private static GameObject BayPrefab()
    {
        var root=new GameObject("Parking bay");
        try
        {
            var paint=Mat("ParkingPaint",new Color(.96f,.93f,.75f));
            foreach(float x in new[]{-.30f,.30f}) Cube(root.transform,"Parking line",new Vector3(x,.014f,0),new Vector3(.018f,.004f,1.28f),paint,false);
            Cube(root.transform,"Back line",new Vector3(0,.014f,-.62f),new Vector3(.61f,.004f,.018f),paint,false);
            root.AddComponent<ParkingBay>();root.AddComponent<PlacedObject>();return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/ParkingBay.prefab");
        }
        finally {Object.DestroyImmediate(root);}
    }
    private static void BuildCityScene(GameObject pickup,GameObject box,GameObject rack,GameObject gate)
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("City");var world=root.AddComponent<CityWorld>();
        var ground=Mat("Grass",new Color(.31f,.48f,.31f));var asphalt=Mat("Asphalt",new Color(.16f,.19f,.19f));var sidewalk=Mat("Sidewalk",new Color(.65f,.66f,.59f));
        Cube(root.transform,"City ground",new Vector3(0,-.2f,0),new Vector3(240,.4f,240),ground,true);
        foreach(float coordinate in new[]{-75f,-35f,5f,45f,85f})
        {
            Cube(root.transform,"North south road",new Vector3(coordinate,.015f,0),new Vector3(10,.025f,224),asphalt,false);
            Cube(root.transform,"East west road",new Vector3(0,.016f,coordinate),new Vector3(224,.025f,10),asphalt,false);
            foreach(float side in new[]{-6f,6f})
            {
                Cube(root.transform,"Sidewalk",new Vector3(coordinate+side,.045f,0),new Vector3(2,.09f,224),sidewalk,false);
                Cube(root.transform,"Sidewalk",new Vector3(0,.045f,coordinate+side),new Vector3(224,.09f,2),sidewalk,false);
            }
            for(int position=-108;position<=108;position+=8)
            {
                if(new[]{-75f,-35f,5f,45f,85f}.Any(c=>Mathf.Abs(position-c)<7)) continue;
                Cube(root.transform,"Lane dash",new Vector3(coordinate,.035f,position),new Vector3(.15f,.008f,3),Mat("WhiteRoad",Color.white),false);
                Cube(root.transform,"Lane dash",new Vector3(position,.035f,coordinate),new Vector3(3,.008f,.15f),Mat("WhiteRoad",Color.white),false);
            }
        }
        // Low ramps instead of kerbs across intersections: the pickup must be able to turn without catching on seams.
        var lots=new[]{-55f,-15f,25f,65f}; int index=0;
        foreach(float x in lots) foreach(float z in lots)
        {
            if(x==-55 && z==-55 || x==65 && z==65) continue;
            string[] buildingNames={"Env_ResidentBuilding_01","Env_CommercialBuilding_02","Env_ResidentBuilding_03","Env_CompanyBuilding_02","Env_Motel_01","Env_CommercialBuilding_04","Env_ResidentBuilding_05","Env_CompanyBuilding_04"};
            string source=Town+buildingNames[index++%buildingNames.Length]+".prefab";
            var building=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
            PrefabUtility.UnpackPrefabInstance(building,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);building.transform.SetParent(root.transform,false);
            Clean(building);Normalize(building,20);building.transform.position=new Vector3(x,0,z);var bounds=Measure(building);
            var collider=building.AddComponent<BoxCollider>();collider.center=building.transform.InverseTransformPoint(bounds.center);collider.size=Vector3.Scale(bounds.size,new Vector3(1/building.transform.lossyScale.x,1/building.transform.lossyScale.y,1/building.transform.lossyScale.z));
            for(int corner=0;corner<2;corner++)
            {
                CityProp(root.transform,"Prop_Tree_01",new Vector3(x+(corner==0?-12:12),0,z+12),6);
                CityProp(root.transform,"Prop_Bush_01",new Vector3(x+(corner==0?-12:12),0,z-12),1.4f);
            }
        }
        // Forecourt connects home and loading doors directly to the southern road.
        Cube(root.transform,"Store forecourt",new Vector3(-55,.03f,-25),new Vector3(28,.06f,18),asphalt,false);
        world.facadeRoot=new GameObject("Synchronized store exterior").transform;world.facadeRoot.SetParent(root.transform,false);
        world.home=Checkpoint(root.transform,"Возврат в магазин",new Vector3(-53,.04f,-24),CheckpointKind.Return);
        world.unload=Checkpoint(root.transform,"Разгрузка магазина",new Vector3(-66,.04f,-24),CheckpointKind.Unload);
        var dock=(GameObject)PrefabUtility.InstantiatePrefab(gate);dock.name="Shop unloading doors";dock.transform.SetParent(world.facadeRoot,false);
        dock.transform.localScale=Vector3.one*6;dock.transform.position=new Vector3(-66,0,-33);
        Object.DestroyImmediate(dock.GetComponent<PlacedObject>());
        var depot=new GameObject("City distribution warehouse").transform;depot.SetParent(root.transform,false);
        var metal=Mat("WarehouseWalls",new Color(.54f,.59f,.55f));
        Cube(depot,"Warehouse floor",new Vector3(65,.01f,65),new Vector3(28,.04f,28),sidewalk,false);
        Cube(depot,"Warehouse roof",new Vector3(65,9,65),new Vector3(29,.35f,29),metal,true);
        Cube(depot,"Warehouse rear",new Vector3(65,4.5f,79),new Vector3(28,9,.3f),metal,true);
        foreach(float x in new[]{51f,79f}) Cube(depot,"Warehouse side",new Vector3(x,4.5f,65),new Vector3(.3f,9,28),metal,true);
        foreach(float x in new[]{55f,75f}) Cube(depot,"Warehouse entrance side",new Vector3(x,4.5f,51),new Vector3(8,9,.3f),metal,true);
        Cube(depot,"Entrance header",new Vector3(65,8,51),new Vector3(12,2,.3f),metal,true);
        Cube(depot,"Depot approach",new Vector3(65,.025f,44),new Vector3(12,.05f,16),asphalt,false);
        world.depot=Checkpoint(root.transform,"Городской склад",new Vector3(65,.04f,62),CheckpointKind.Depot);
        foreach(float x in new[]{54f,76f}) for(int z=57;z<=74;z+=8)
        {
            var shelving=(GameObject)PrefabUtility.InstantiatePrefab(rack);shelving.transform.SetParent(depot,false);shelving.transform.position=new Vector3(x,0,z);shelving.transform.localScale=Vector3.one*5;
            Object.DestroyImmediate(shelving.GetComponent<PlacedObject>());
        }
        Sign(depot,"ГОРОДСКОЙ СКЛАД",new Vector3(65,7,50.7f),Quaternion.Euler(0,180,0),new Vector2(18,2));
        foreach(float x in new[]{57f,73f})
        {
            var lamp=new GameObject("Warehouse overhead light",typeof(Light));lamp.transform.SetParent(depot,false);lamp.transform.position=new Vector3(x,7,64);
            var source=lamp.GetComponent<Light>();source.type=LightType.Point;source.range=24;source.intensity=4;source.color=new Color(1,.94f,.8f);
        }
        Sign(root.transform,"МАГАЗИН",new Vector3(-55,4.6f,-34),Quaternion.identity,new Vector2(18,2));
        foreach(var edge in new[]{new Vector3(-118,1,0),new Vector3(118,1,0),new Vector3(0,1,-118),new Vector3(0,1,118)})
            Cube(root.transform,"Outer barrier",edge,edge.x==0?new Vector3(240,2,.5f):new Vector3(.5f,2,240),metal,true);
        var vehicle=(GameObject)PrefabUtility.InstantiatePrefab(pickup);vehicle.transform.SetParent(root.transform,false);vehicle.transform.position=world.home.transform.position+Vector3.up*.06f;
        world.pickup=vehicle.GetComponent<PickupDrive>();world.cargoRoot=vehicle.transform.Find("Cargo bed");world.cargoBoxPrefab=box;
        var camera=new GameObject("City third person camera",typeof(Camera),typeof(AudioListener),typeof(PickupCamera));camera.tag="MainCamera";camera.transform.SetParent(root.transform,false);
        camera.transform.position=vehicle.transform.position+new Vector3(0,4,-8);camera.GetComponent<Camera>().farClipPlane=400;
        world.chaseCamera=camera.GetComponent<PickupCamera>();world.chaseCamera.target=vehicle.transform;
        var sun=new GameObject("City sun",typeof(Light));sun.transform.SetParent(root.transform,false);sun.transform.rotation=Quaternion.Euler(48,-28,0);
        var light=sun.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.shadows=LightShadows.Soft;
        var fillRoot=new GameObject("City soft fill",typeof(Light));fillRoot.transform.SetParent(root.transform,false);fillRoot.transform.rotation=Quaternion.Euler(40,140,0);
        var fill=fillRoot.GetComponent<Light>();fill.type=LightType.Directional;fill.intensity=.4f;fill.shadows=LightShadows.None;
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.60f,.66f,.65f);RenderSettings.sun=light;
        RenderSettings.fog=true;RenderSettings.fogColor=new Color(.71f,.80f,.78f);RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=140;RenderSettings.fogEndDistance=270;
        EditorSceneManager.SaveScene(scene,CityPath);
    }
    private static DeliveryCheckpoint Checkpoint(Transform parent,string name,Vector3 position,CheckpointKind kind)
    {
        var root=new GameObject(name);root.transform.SetParent(parent,false);root.transform.position=position;var checkpoint=root.AddComponent<DeliveryCheckpoint>();checkpoint.kind=kind;checkpoint.radius=5;
        var yellow=Mat("CheckpointYellow",new Color(.97f,.74f,.12f));
        for(int i=0;i<20;i++)
        {
            float angle=i*Mathf.PI*2/20; var marker=Cube(root.transform,"Checkpoint ring",new Vector3(Mathf.Sin(angle)*4.6f,.03f,Mathf.Cos(angle)*4.6f),new Vector3(.5f,.06f,1.1f),yellow,false);
            marker.transform.localRotation=Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);
        }
        Sign(root.transform,name,new Vector3(0,5,0),Quaternion.identity,new Vector2(10,1));
        root.GetComponentInChildren<TMPro.TextMeshPro>().fontSize=6;return checkpoint;
    }
    private static void CityProp(Transform parent,string name,Vector3 position,float size)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Town+name+".prefab");
        if(source==null) throw new InvalidOperationException("Missing city decoration: "+name);
        var prop=(GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(prop,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        prop.transform.SetParent(parent,false);Clean(prop);Normalize(prop,size);prop.transform.position=position;
    }
    private static void Sign(Transform parent,string text,Vector3 position,Quaternion rotation,Vector2 size)
    {
        var root=new GameObject(text,typeof(TMPro.TextMeshPro));root.transform.SetParent(parent,false);root.transform.localPosition=position;root.transform.localRotation=rotation;
        var label=root.GetComponent<TMPro.TextMeshPro>();label.font=Resources.Load<ShopUiTheme>("ShopUi/Theme").regularFont;label.text=text;label.fontSize=12;label.alignment=TMPro.TextAlignmentOptions.Center;label.color=new Color(.94f,.91f,.76f);
        root.AddComponent<LocalizedFontControl>().excludeFromFontChange=true;
        label.rectTransform.sizeDelta=size;
    }
    private static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool collision)
    {
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name=name;cube.transform.SetParent(parent,false);cube.transform.localPosition=position;cube.transform.localScale=size;
        cube.GetComponent<Renderer>().sharedMaterial=material;if(!collision) Object.DestroyImmediate(cube.GetComponent<Collider>());return cube;
    }
    private static Material Mat(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) {material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.1f);EditorUtility.SetDirty(material);return material;
    }
    internal static void Clean(GameObject model)
    {
        foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
        foreach(var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach(var body in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
        foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>
            {
                if(source==null) throw new InvalidOperationException("Missing city source material.");
                if(source.shader.name.StartsWith("Universal Render Pipeline/")) return source;
                string name="Source_"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
                var target=Mat(name,source.HasProperty("_Color")?source.GetColor("_Color"):Color.white);
                if(source.HasProperty("_MainTex")) target.SetTexture("_BaseMap",source.GetTexture("_MainTex"));return target;
            }).ToArray();
        }
    }
    internal static void Normalize(GameObject model,float size)
    {
        var bounds=Measure(model);float maximum=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
        if(maximum<.0001f) throw new InvalidOperationException("Empty city model.");
        model.transform.localScale*=size/maximum;bounds=Measure(model);
        model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
    }
    internal static Bounds Measure(GameObject model)
    {
        var renderers=model.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0) throw new InvalidOperationException("No model renderer.");
        var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);return bounds;
    }
    private static void Append(Object target,string field,Object value)
    {
        var data=new SerializedObject(target);var array=data.FindProperty(field);
        for(int i=0;i<array.arraySize;i++) if(array.GetArrayElementAtIndex(i).objectReferenceValue==value) return;
        array.arraySize++;array.GetArrayElementAtIndex(array.arraySize-1).objectReferenceValue=value;data.ApplyModifiedPropertiesWithoutUndo();
    }
}
