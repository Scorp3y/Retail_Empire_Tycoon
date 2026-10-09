using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Territory;
using RetailEmpireTycoon.UI.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Repeatable store upgrade, preserving item IDs, old save coordinates and the player save.</summary>
public static class StoreUpgradeSetup
{
    public const string Folder = "Assets/Prefabs/StoreUpgrade";
    [MenuItem("Retail Empire/Store/Apply wall and catalogue corrections")]
    public static void ApplyCatalogueCorrections()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != CityUpdateSetup.GamePath)
            throw new InvalidOperationException("Open the saved Game scene in Edit Mode.");
        var grid = Object.FindObjectOfType<GridSystem>(true);
        ConfigureWall("Assets/Prefabs/Structures/Wall.asset", grid);
        ConfigureWall("Assets/Prefabs/Structures/Wall_Corner.asset", grid);
        RetireLegacyFloor();
        ConfigurePickupIcon();
        foreach (string path in new[] { "Assets/Prefabs/Structures/Wall.asset", "Assets/Prefabs/Structures/Wall_Corner.asset" })
        {
            var item = AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
            item.icon = ShopUiPreviews.Render(item.prefab, item.id, 25);
            EditorUtility.SetDirty(item);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Wall and catalogue corrections applied without rebuilding the world or opening player saves.");
    }

    private static void RetireLegacyFloor()
    {
        var retiredFloor = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Structures/Floor.asset");
        retiredFloor.hiddenFromShop = true;
        EditorUtility.SetDirty(retiredFloor);
    }

    private static void ConfigurePickupIcon()
    {
        var shop = Object.FindObjectOfType<ShopWindow>(true);
        ShopUiSetup.Set(shop, "pickupIcon", ShopUiPreviews.Render(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/City/Pickup.prefab"), "vehicle_pickup", 325));
    }

    [MenuItem("Retail Empire/Store/Apply construction and land upgrade")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != CityUpdateSetup.GamePath)
            throw new InvalidOperationException("Open the saved Game scene in Edit Mode.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        ShopAssortmentSetup.Apply();
        var grid = Object.FindObjectOfType<GridSystem>(true);
        ConfigureWall("Assets/Prefabs/Structures/Wall.asset", grid);
        ConfigureWall("Assets/Prefabs/Structures/Wall_Corner.asset", grid);
        RetireLegacyFloor();
        CreateFloorStyles();
        CreateDecorations();
        ConfigureTerritories(grid);
        ConfigurePickup();
        var preview = Object.FindObjectOfType<BuildPreview>(true);
        ShopUiSetup.Set(preview, "accessMaterial", AccessMaterial());
        ShopUiSetup.RebuildShopDepartments();
        var services=GameObject.Find("City and delivery services");
        var guide=services.GetComponent<ConstructionGuide>()??services.AddComponent<ConstructionGuide>();
        guide.building=Object.FindObjectOfType<BuildController>(true);
        guide.controls=Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.GameplayControls>(true);
        guide.operationsHud=Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.ShopOperationsHud>(true);
        guide.uiRoot=services.GetComponent<CityTrip>().uiRoot;
        ConfigurePickupIcon();
        ShopUiPreviews.Generate();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        TerritoryBarrierSetup.Configure();
        Debug.Log("Store upgrade applied without opening player saves.");
    }

    private static Material Material(string name, Color color)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", .12f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material AccessMaterial()
    {
        var material = Material("BuyerApproach", new Color(.03f, .65f, .95f));
        material.shader=Shader.Find("Universal Render Pipeline/Unlit");
        material.SetColor("_BaseColor",new Color(.03f,.65f,.95f));
        material.SetFloat("_Cull", 0);
        return material;
    }

    private static GameObject Part(Transform root, string name, PrimitiveType shape, Vector3 position, Vector3 size, Material material)
    {
        var part = GameObject.CreatePrimitive(shape);
        part.name = name;
        part.transform.SetParent(root, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        return part;
    }

    private static void ConfigureWall(string path, GridSystem grid)
    {
        var item = AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
        var root = new GameObject(item.id);
        try
        {
            // Keep the authored mesh/materials and height; only modularise its horizontal dimensions.
            string sourcePath = path.Replace(".asset", ".prefab");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException("Missing original wall: " + sourcePath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.transform.SetParent(root.transform, false);
            foreach (var marker in model.GetComponentsInChildren<StoreWallOccluder>(true)) Object.DestroyImmediate(marker);
            foreach (var partCollider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(partCollider);
            var bounds = CityUpdateSetup.Measure(model);
            item.legacyWallDepth = Mathf.Min(bounds.size.z, grid.cellSize * 2);
            float blockSize = BuildItemData.WallBlockSizeInCells * grid.cellSize;
            var straightSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Structures/Wall.prefab");
            var straight = Object.Instantiate(straightSource);
            float horizontalScale;
            try
            {
                horizontalScale = blockSize / CityUpdateSetup.Measure(straight).size.x * straight.transform.localScale.x;
            }
            finally { Object.DestroyImmediate(straight); }
            // The original corner's two connection faces share the straight wall's local profile.
            // Preserve that profile: independently stretching their bounding boxes breaks the joint.
            model.transform.localScale = new Vector3(horizontalScale, model.transform.localScale.y, horizontalScale);
            bounds = CityUpdateSetup.Measure(model);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
            root.AddComponent<StoreWallOccluder>();
            item.prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + item.id + ".prefab");
            item.footprint = Vector2Int.one * BuildItemData.WallBlockSizeInCells;
            item.pivotOffset = Vector2Int.zero;
            item.placementBounds = bounds;
            item.wallCellSize = grid.cellSize;
            item.placementAlignmentOffset = new Vector3(bounds.center.x, 0, bounds.center.z);
            item.isWall = true;
            item.alignModelToFootprint = true;
            item.ruleFlags = PlacementRuleFlags.InsidePurchasedArea | PlacementRuleFlags.NoOverlap;
            EditorUtility.SetDirty(item);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static BuildItemData Item(string id, string name, int price, Vector2Int footprint, GameObject prefab)
    {
        string path = Folder + "/" + id + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
        if (item == null) { item = ScriptableObject.CreateInstance<BuildItemData>(); AssetDatabase.CreateAsset(item,path); }
        item.id = id; item.displayName = name; item.price = price; item.footprint = footprint;
        item.prefab = prefab;
        item.ruleFlags = PlacementRuleFlags.InsidePurchasedArea | PlacementRuleFlags.NoOverlap;
        var model = Object.Instantiate(prefab);
        try
        {
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            item.placementBounds = CityUpdateSetup.Measure(model);
            item.alignModelToFootprint = true;
        }
        finally { Object.DestroyImmediate(model); }
        foreach (var shop in Object.FindObjectsOfType<ShopWindow>(true)) Append(shop,"buildCatalog",item);
        foreach (var catalog in Object.FindObjectsOfType<BuildItemCatalog>(true)) Append(catalog,"items",item);
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void CreateFloorStyles()
    {
        string[] ids = { "floor_cream", "floor_graphite", "floor_checker", "floor_wood", "floor_concrete", "floor_terrazzo" };
        string[] names = { "Cream tile", "Graphite tile", "Checkerboard", "Wooden floor", "Concrete", "Terrazzo" };
        var floor = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Structures/Floor.asset");
        for (int style = 0; style < ids.Length; style++)
        {
            var texture = new Texture2D(128,128,TextureFormat.RGBA32,true) { name = ids[style], filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Repeat };
            for (int y=0;y<128;y++) for (int x=0;x<128;x++) texture.SetPixel(x,y,FloorColor(style,x,y));
            texture.Apply();
            string texturePath = Folder + "/" + ids[style] + "_Pattern.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (existing == null) AssetDatabase.CreateAsset(texture,texturePath);
            else { EditorUtility.CopySerialized(texture,existing); Object.DestroyImmediate(texture); texture=existing; }
            var material = Material(ids[style],Color.white);
            material.SetTexture("_BaseMap",texture);
            var model = new GameObject(ids[style]);
            try
            {
                // The unit root is scaled by FloorPainter; child pattern survives inventory placement and save reload.
                Part(model.transform,"Surface",PrimitiveType.Cube,Vector3.zero,new Vector3(1,.025f,1),material);
                var prefab = PrefabUtility.SaveAsPrefabAsset(model,Folder + "/" + ids[style] + ".prefab");
                var item = Item(ids[style],names[style], style==3?15:style==5?18:10,Vector2Int.one,prefab);
                item.placementKind=PlacementKind.Floor; item.category=BuildCategory.Structures;
                item.floorMaterial=material; item.weightKg=2; item.cargoVolumeM3=.01f;
                item.previewValidMaterial=floor.previewValidMaterial; item.previewInvalidMaterial=floor.previewInvalidMaterial;
            }
            finally { Object.DestroyImmediate(model); }
        }
    }

    private static Color FloorColor(int style,int x,int y)
    {
        bool grout=x%64<2 || y%64<2;
        switch(style)
        {
            case 0: return grout ? new Color(.53f,.55f,.48f) : new Color(.87f,.86f,.73f);
            case 1: return grout ? new Color(.5f,.52f,.5f) : new Color(.23f,.27f,.28f);
            case 2: return grout ? new Color(.53f,.53f,.49f) : (x/64+y/64)%2==0 ? new Color(.88f,.87f,.78f) : new Color(.23f,.28f,.28f);
            case 3:
                if (y%32<2 || (x+(y/32%2)*64)%128<2) return new Color(.27f,.17f,.09f);
                float grain=Mathf.Sin(x*.17f+y*.08f)*.035f;
                return new Color(.58f+grain,.36f+grain,.18f+grain);
            case 4:
                float noise=((x*73+y*31)%29)/290f;
                return new Color(.5f+noise,.52f+noise,.51f+noise);
            default:
                int seed=(x/3*73+y/3*31)%23;
                return seed<3 ? new Color(.32f,.46f,.41f) : seed<5 ? new Color(.66f,.47f,.34f) : new Color(.85f,.84f,.73f);
        }
    }

    private static void CreateDecorations()
    {
        var wood=Material("DecorWood",new Color(.5f,.29f,.12f));
        var green=Material("DecorLeaf",new Color(.22f,.48f,.12f));
        var clay=Material("DecorPot",new Color(.73f,.38f,.21f));
        var metal=Material("DecorMetal",new Color(.17f,.24f,.23f));
        var cream=Material("DecorCream",new Color(.92f,.87f,.64f));
        string[] ids={"decor_plant","decor_bench","decor_bin","decor_lamp","decor_planter","decor_sign"};
        string[] names={"Potted plant","Wooden bench","Waste bin","Floor lamp","Flower planter","Market sign"};
        int[] prices={45,90,35,80,65,110}; int[] beauty={3,4,1,4,5,6};
        for(int i=0;i<ids.Length;i++)
        {
            var root=new GameObject(ids[i]);
            try
            {
                if(i==0 || i==4)
                {
                    Part(root.transform,"Planter",PrimitiveType.Cylinder,new Vector3(0,.1f,0),new Vector3(i==4?.46f:.25f,.1f,.25f),clay);
                    Part(root.transform,"Stem",PrimitiveType.Cylinder,new Vector3(0,.3f,0),new Vector3(.025f,.14f,.025f),wood);
                    for(int n=0;n<5;n++)
                    {
                        float angle=n*Mathf.PI*.4f;
                        Part(root.transform,"Leaf",PrimitiveType.Sphere,new Vector3(Mathf.Sin(angle)*.065f,.37f+n*.014f,Mathf.Cos(angle)*.065f),new Vector3(.14f,.23f,.10f),green).transform.localRotation=Quaternion.Euler(25,angle*Mathf.Rad2Deg,25);
                    }
                    if(i==4) for(int n=0;n<7;n++) Part(root.transform,"Flower",PrimitiveType.Sphere,new Vector3((n-3)*.055f,.25f,.035f),new Vector3(.055f,.055f,.055f),n%2==0?cream:clay);
                }
                else if(i==1)
                {
                    for(int n=0;n<3;n++) Part(root.transform,"Seat slat",PrimitiveType.Cube,new Vector3(0,.23f,(n-1)*.075f),new Vector3(.62f,.045f,.065f),wood);
                    for(int n=0;n<2;n++)
                    {
                        Part(root.transform,"Back slat",PrimitiveType.Cube,new Vector3(0,.32f+n*.08f,-.11f),new Vector3(.62f,.06f,.025f),wood);
                        Part(root.transform,"Leg",PrimitiveType.Cube,new Vector3(n==0?-.23f:.23f,.12f,0),new Vector3(.035f,.24f,.25f),metal);
                    }
                }
                else if(i==2)
                {
                    Part(root.transform,"Bin",PrimitiveType.Cylinder,new Vector3(0,.19f,0),new Vector3(.22f,.19f,.22f),metal);
                    Part(root.transform,"Lid",PrimitiveType.Cylinder,new Vector3(0,.39f,0),new Vector3(.25f,.025f,.25f),green);
                }
                else if(i==3)
                {
                    Part(root.transform,"Foot",PrimitiveType.Cylinder,new Vector3(0,.02f,0),new Vector3(.24f,.02f,.24f),metal);
                    Part(root.transform,"Pole",PrimitiveType.Cylinder,new Vector3(0,.36f,0),new Vector3(.025f,.34f,.025f),metal);
                    Part(root.transform,"Shade",PrimitiveType.Cylinder,new Vector3(0,.73f,0),new Vector3(.28f,.09f,.28f),cream);
                }
                else
                {
                    Part(root.transform,"Sign panel",PrimitiveType.Cube,new Vector3(0,.44f,0),new Vector3(.57f,.35f,.05f),wood);
                    Part(root.transform,"Sign face",PrimitiveType.Cube,new Vector3(0,.44f,.032f),new Vector3(.52f,.30f,.02f),cream);
                    for(int n=0;n<3;n++) Part(root.transform,"Graphic leaf",PrimitiveType.Sphere,new Vector3((n-1)*.14f,.45f,.05f),new Vector3(.095f,.16f,.02f),green);
                    for(int n=0;n<2;n++) Part(root.transform,"Sign stand",PrimitiveType.Cube,new Vector3(n==0?-.22f:.22f,.14f,0),new Vector3(.025f,.28f,.12f),metal);
                }
                // Imported shop flooring is at 0.096; decor should stand on it rather than sink into it.
                foreach(Transform part in root.transform) part.localPosition+=Vector3.up*.096f;
                var bounds=CityUpdateSetup.Measure(root);
                var collider=root.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+ids[i]+".prefab");
                var item=Item(ids[i],names[i],prices[i],new Vector2Int(i==1||i==4||i==5?4:2,2),prefab);
                item.category=BuildCategory.Decoration;item.beautyPoints=beauty[i];item.weightKg=i==1?15:5;item.cargoVolumeM3=i==1?.25f:.08f;
                EditorUtility.SetDirty(item);
            }
            finally {Object.DestroyImmediate(root);}
        }
    }

    private static void ConfigurePickup()
    {
        var pickup=GameObject.Find("Player pickup");
        // Shop units are much smaller than city metres. Match the actual parked neighbouring cars, not either prefab's authoring scale.
        var cars=GameObject.Find("Cars");
        var neighbour=cars!=null ? cars.GetComponentsInChildren<Renderer>().Where(r=>Mathf.Max(r.bounds.size.x,r.bounds.size.z)>.9f&&Mathf.Max(r.bounds.size.x,r.bounds.size.z)<4).OrderBy(r=>(r.bounds.center-pickup.transform.position).sqrMagnitude).FirstOrDefault() : null;
        float size=neighbour!=null?Mathf.Max(neighbour.bounds.size.x,neighbour.bounds.size.z):1.22f;
        var bounds=CityUpdateSetup.Measure(pickup);
        pickup.transform.localScale*=size/Mathf.Max(bounds.size.x,bounds.size.z);
        bounds=CityUpdateSetup.Measure(pickup);
        pickup.transform.position-=Vector3.up*(bounds.min.y-.025f);
    }

    private static void ConfigureTerritories(GridSystem grid)
    {
        var manager=Object.FindObjectOfType<TerritoryManager>(true);
        var layout=manager.GetComponent<TerritoryPlotLayout>()??manager.gameObject.AddComponent<TerritoryPlotLayout>();
        bool migrateLandscape=layout.layoutVersion<1;
        layout.grid=grid;layout.territory=manager;layout.progression=Object.FindObjectOfType<StoreProgression>(true);
        manager.plotLayout=layout;
        // Grid-aligned shared edges eliminate the narrow unbuildable strips between old plot colliders.
        Vector3Int origin=grid.WorldToCell(new Vector3(-.76f,0,-2.8f));
        Vector3 start=grid.CellToWorld(origin)-new Vector3(grid.cellSize*.5f,0,grid.cellSize*.5f);
        float baseW=90*grid.cellSize, baseD=72*grid.cellSize, rearD=126*grid.cellSize, eastW=132*grid.cellSize;
        layout.initialBounds=new Rect(start.x,start.z,baseW,baseD);
        layout.plots.Clear();
        Rect[] bounds={
            new Rect(start.x+baseW,start.z,baseW,baseD),
            new Rect(start.x,start.z+baseD,baseW,rearD),
            new Rect(start.x+baseW,start.z+baseD,baseW,rearD),
            new Rect(start.x+2*baseW,start.z+baseD, eastW,rearD),
            new Rect(start.x+2*baseW,start.z,eastW,baseD),
            new Rect(start.x+2*baseW+eastW,start.z,eastW,baseD),
            new Rect(start.x+2*baseW+eastW,start.z+baseD,eastW,rearD),
            new Rect(start.x,start.z+baseD+rearD,2*baseW,baseD),
            new Rect(start.x+2*baseW,start.z+baseD+rearD,2*eastW,baseD)
        };
        var ids=(TerritoryId[])Enum.GetValues(typeof(TerritoryId));
        var zones=Object.FindObjectsOfType<TerritoryZone>(true).ToDictionary(z=>z.Id);
        for(int i=0;i<ids.Length;i++)
        {
            var rect=bounds[i];
            layout.plots.Add(new TerritoryPlotLayout.Plot{id=ids[i],bounds=rect});
            if(!zones.TryGetValue(ids[i],out var zone))
            {
                zone=new GameObject("Territory "+ids[i]).AddComponent<TerritoryZone>();zone.Id=ids[i];zone.Price=6500+(i-5)*1500;
            }
            ConfigureZone(zone,rect);
            Append(Object.FindObjectOfType<TerritoryPurchaseModeManager>(true),"_zones",zone);
        }
        Rect full=new Rect(start.x,start.z,2*baseW+2*eastW,2*baseD+rearD);
        ClearExpandedLand(full,migrateLandscape);
        ConfigureLandscapePrefabs(layout);
        layout.layoutVersion=1;
        var camera=Object.FindObjectOfType<MainCamera>(true);
        camera.maxX=full.xMax+5;camera.minX=full.xMin-5;camera.maxZ=full.yMax+5;camera.minZ=full.yMin-5;
        camera.maxZoomDistance=140;camera.maxY=120;
        var mode=Object.FindObjectOfType<CameraModeController>(true);
        ShopUiSetup.Set(mode,"_plotLayout",layout);
        var serialized=new SerializedObject(mode);
        var pose=serialized.FindProperty("_purchasePose");
        var target=new Vector3(full.center.x,0,full.center.y);
        var rotation=Quaternion.Euler(65,0,0);
        pose.FindPropertyRelative("Position").vector3Value=target-rotation*Vector3.forward*95;
        pose.FindPropertyRelative("Euler").vector3Value=rotation.eulerAngles;
        pose.FindPropertyRelative("Fov").floatValue=60;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureZone(TerritoryZone zone,Rect rect)
    {
        var prefabRoot=PrefabUtility.GetOutermostPrefabInstanceRoot(zone.gameObject);
        if(prefabRoot!=null) PrefabUtility.UnpackPrefabInstance(prefabRoot,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        // Replace only the authored presentation/click plane. The zone identity and existing scene references remain intact.
        for(int i=zone.transform.childCount-1;i>=0;i--) Object.DestroyImmediate(zone.transform.GetChild(i).gameObject);
        foreach(var old in zone.GetComponents<Collider>()) Object.DestroyImmediate(old);
        foreach(var old in zone.GetComponents<LineRenderer>()) Object.DestroyImmediate(old);
        zone.transform.SetPositionAndRotation(new Vector3(rect.center.x,0,rect.center.y),Quaternion.identity);zone.transform.localScale=Vector3.one;
        var visual=zone.GetComponent<TerritoryVisual>()??zone.gameObject.AddComponent<TerritoryVisual>();
        var lineObject=new GameObject("Plot outline");lineObject.transform.SetParent(zone.transform,false);
        var line=lineObject.AddComponent<LineRenderer>();line.useWorldSpace=true;line.loop=true;line.positionCount=4;
        line.SetPositions(new[]{new Vector3(rect.xMin,.1f,rect.yMin),new Vector3(rect.xMax,.1f,rect.yMin),new Vector3(rect.xMax,.1f,rect.yMax),new Vector3(rect.xMin,.1f,rect.yMax)});
        line.sharedMaterial=Material("PlotBorder",new Color(.43f,.69f,.23f));line.startWidth=line.endWidth=.07f;line.enabled=false;
        ShopUiSetup.Set(visual,"_border",line);ShopUiSetup.Set(visual,"_fillRenderer",null);ShopUiSetup.Set(visual,"_fence",null);ShopUiSetup.Set(visual,"_constructionBarrier",null);ShopUiSetup.Set(visual,"_hoverRoot",zone.transform);
        ShopUiSetup.Set(zone,"_visual",visual);
        var click=zone.GetComponent<TerritoryClickPlane>()??zone.gameObject.AddComponent<TerritoryClickPlane>();
        var clickSettings=new SerializedObject(click);
        clickSettings.FindProperty("_sizeXZ").vector2Value=new Vector2(rect.width,rect.height);
        clickSettings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ClearExpandedLand(Rect plot,bool moveEnvironment)
    {
        var world=GameObject.Find("World").transform;
        // Keep the entrance, original parking, pedestrians and foreground streets unchanged.
        var forest=world.Find("Forest");
        if(moveEnvironment&&forest!=null) forest.position=new Vector3(plot.xMax+5,forest.position.y,plot.yMax+5);
        var houses=world.Find("House Objects");
        if(moveEnvironment&&houses!=null) foreach(Transform house in houses)
        {
            if(house.position.z>8.2f) house.position+=Vector3.forward*(plot.yMax-8.2f+3);
            if(house.position.x>17f) house.position+=Vector3.right*(plot.xMax-17f+3);
        }
        if(moveEnvironment) foreach(string groupName in new[]{"Drive Road","Sidewalk"})
        {
            var group=world.Find(groupName);if(group==null)continue;
            foreach(var renderer in group.GetComponentsInChildren<Renderer>())
            {
                var bounds=renderer.bounds;
                if(bounds.center.z>8.2f&&bounds.center.x>plot.xMin&&bounds.center.x<plot.xMax)
                    renderer.transform.position+=Vector3.forward*(plot.yMax-8.2f+3);
            }
        }
        if(moveEnvironment)
        {
            // Move waypoint objects once, even when several traffic spawners share the same route.
            var waypoints=Object.FindObjectsOfType<CarSpawner>(true).SelectMany(s=>(s.waypoints??Array.Empty<Transform>()).Concat(new[]{s.spawnPoint})).Where(t=>t!=null).Distinct().ToArray();
            foreach(var point in waypoints)
                if(point.position.z>8.2f&&point.position.x>plot.xMin&&point.position.x<plot.xMax)
                    point.position+=Vector3.forward*(plot.yMax-8.2f+3);
        }
        var ground=world.Find("Expanded plot ground");
        if(ground==null) ground=Part(world,"Expanded plot ground",PrimitiveType.Cube,new Vector3(plot.center.x,.028f,plot.center.y),new Vector3(plot.width+.2f,.04f,plot.height+.2f),Material("PlotGrass",new Color(.42f,.62f,.38f))).transform;
        // Above the old grass, below store flooring. This removes the colour seam across newly enlarged plots.
        ground.position=new Vector3(plot.center.x,.028f,plot.center.y);ground.localScale=new Vector3(plot.width+.2f,.04f,plot.height+.2f);
        int layer=LayerMask.NameToLayer("Ground");
        ground.gameObject.layer=layer>=0?layer:0;
        if(ground.GetComponent<Collider>()==null) ground.gameObject.AddComponent<BoxCollider>();
        var backdrop=world.Find("Extended world ground");
        if(backdrop==null) Part(world,"Extended world ground",PrimitiveType.Cube,new Vector3(plot.center.x,-.055f,plot.center.y),new Vector3(260,.04f,220),Material("PlotGrass",new Color(.42f,.62f,.38f)));
    }

    private static void ConfigureLandscapePrefabs(TerritoryPlotLayout layout)
    {
        var spawner=new SerializedObject(Object.FindObjectOfType<StorePrefabSpawner>(true));
        var stages=spawner.FindProperty("prefabs");
        for(int i=0;i<stages.arraySize;i++)
        {
            var stage=stages.GetArrayElementAtIndex(i);
            var level=(StoreLevelId)stage.FindPropertyRelative("level").enumValueIndex;
            string path=AssetDatabase.GetAssetPath(stage.FindPropertyRelative("prefab").objectReferenceValue);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var area=root.GetComponentInChildren<StoreBuildArea>(true);
                if(area==null||area.landscapeLayoutVersion>=1)continue;
                var nature=root.transform.Find("Trees - Unpurchased territory");
                var remaining=layout.plots.Where(p=>(int)p.id<5&&!PurchasedAtStage(level,p.id)).ToArray();
                if(nature!=null&&remaining.Length>0) foreach(Transform prop in nature)
                {
                    if(prop.GetComponentInChildren<Renderer>(true)==null)continue;
                    var bounds=CityUpdateSetup.Measure(prop.gameObject);
                    var center=bounds.center;
                    var target=new Vector2(layout.initialBounds.xMin+(center.x+.76f)*3,layout.initialBounds.yMin+(center.z+2.8f)*3);
                    var plot=remaining.OrderBy(p=>DistanceToRect(target,p.bounds)).First().bounds;
                    target.x=Mathf.Clamp(target.x,plot.xMin+bounds.extents.x+.15f,plot.xMax-bounds.extents.x-.15f);
                    target.y=Mathf.Clamp(target.y,plot.yMin+bounds.extents.z+.15f,plot.yMax-bounds.extents.z-.15f);
                    prop.position+=new Vector3(target.x-center.x,0,target.y-center.z);
                }
                area.landscapeLayoutVersion=1;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
    }

    private static float DistanceToRect(Vector2 point,Rect rect)
    {
        var closest=new Vector2(Mathf.Clamp(point.x,rect.xMin,rect.xMax),Mathf.Clamp(point.y,rect.yMin,rect.yMax));
        return (closest-point).sqrMagnitude;
    }

    private static bool PurchasedAtStage(StoreLevelId level,TerritoryId id)
    {
        if(level==StoreLevelId.Lvl6)return true;
        if(level==StoreLevelId.Lvl1)return false;
        if(id==TerritoryId.Purple)return true;
        bool later=level==StoreLevelId.Lvl4||level==StoreLevelId.Lvl5_1||level==StoreLevelId.Lvl5_2;
        if(id==TerritoryId.Red)return later||level==StoreLevelId.Lvl3_1;
        if(id==TerritoryId.Green)return later||level==StoreLevelId.Lvl3_2;
        if(id==TerritoryId.Pink)return level==StoreLevelId.Lvl5_1;
        return id==TerritoryId.Yellow&&level==StoreLevelId.Lvl5_2;
    }

    private static void Append(Object target,string field,Object value)
    {
        var serialized=new SerializedObject(target);var list=serialized.FindProperty(field);
        for(int i=0;i<list.arraySize;i++) if(list.GetArrayElementAtIndex(i).objectReferenceValue==value)return;
        list.arraySize++;list.GetArrayElementAtIndex(list.arraySize-1).objectReferenceValue=value;serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
