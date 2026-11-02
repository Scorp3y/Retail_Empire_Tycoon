using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real scene, directed GPS, pointer input and UI acceptance in the disposable project only.</summary>
[InitializeOnLoad]
public static class NavigationUpdateQA
{
    private const string Active = "NavigationUpdate.QA";
    private const string Report = "Library/NavigationUpdateQA";
    private const string Pictures = "Docs/NavigationUpdate";
    private static IEnumerator checks;
    private static DateTime deadline;
    private static string runtimeError;
    private static readonly StringBuilder results = new();
    static NavigationUpdateQA()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, trace, type) =>
        { if (SessionState.GetBool(Active, false) && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)) runtimeError ??= message; };
    }
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor(); Directory.CreateDirectory(Report); Directory.CreateDirectory(Pictures);
            File.WriteAllText(Report + "/result.txt", "RUNNING\n");
            string save = Path.Combine(Application.persistentDataPath, "gameData.json");
            SessionState.SetString("NavigationUpdate.Save", File.Exists(save) ? PinebrookDistrictBuild.Hash(save) : "missing");
            EditorSceneManager.OpenScene(CityUpdateSetup.GamePath); ShopGameplaySandbox.Open();
            foreach (var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true)) Object.DestroyImmediate(bootstrap);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (checks == null) { checks = Check(); deadline = DateTime.UtcNow.AddMinutes(10); }
            Require(DateTime.UtcNow < deadline, "Navigation acceptance timed out.");
            Require(runtimeError == null, "Unexpected runtime error: " + runtimeError);
            if (!checks.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static IEnumerator Check()
    {
        for (int i = 0; i < 20; i++) yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true) == null, "Player save writer present.");
        var trip = Object.FindObjectOfType<CityTrip>(); trip.orders.save = null;
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();var fresh=starter.CreateNewGame();
        Object.FindObjectOfType<StoreProgression>().ApplySaveData(fresh.territory);
        starter.building.inventory.ApplySaveData(fresh.buildInventory,starter.catalog);
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;starter.ConfigureSite(true);spawner.Spawn(StoreLevelId.Lvl1);starter.ApplyNewGameBuildings(fresh);
        for(int i=0;i<4;i++)yield return null;
        Require(trip.TownMap != null && trip.TownMap.MiniMap == null, "Store scene must not create a radar.");
        var store = trip.TownMap; var map = store.Data; var navigation = store.Navigation;
        File.WriteAllLines(Report + "/places.txt",map.Points.Select(p=>p.Id+" | "+p.Name+" | "+p.Position));
        Require(map.Points.Count(p => p.Checkpoint != null && p.Checkpoint.kind != CheckpointKind.Unload) == 7, "Missing supplier/showroom map destinations.");
        Require(map.Points.Any(p => p.Symbol == TownMapSymbol.Fuel) && map.Points.Any(p => p.Symbol == TownMapSymbol.Train), "Orientation landmarks missing.");
        TestProjection(map); TestRoutes(map); TestDisconnected();
        var produce = trip.orders.productCatalog.Products.First(p => p.StorageType == ProductStorageType.Produce);
        trip.orders.Ledger.Order(produce.Id, DeliveryItemKind.Product, produce.BoxAmount);
        var farmers = map.Points.Single(p => p.Checkpoint != null && p.Checkpoint.supplier == SupplierKind.Farmers);
        Require(map.ReadyUnits(farmers) == produce.BoxAmount && map.ReadyUnits(map.Home) == 0, "Supplier order badge uses the wrong inventory.");
        trip.controls.CloseWindows(); store.Toggle(); Require(store.IsOpen && trip.controls.WorldMapOpen, "Store map did not block gameplay controls.");
        navigation.Tick(map.Home.Position, Vector3.forward, 0, 0); navigation.Select(farmers);
        for (int i = 0; i < 4; i++) yield return null;
        Capture("StoreMap"); store.Close(); Require(!trip.controls.WorldMapOpen, "Store map leaked its input lock.");
        Require(trip.controls.Key(RetailEmpireTycoon.StoreOperations.ShopAction.Territory) == KeyCode.M, "Territory shortcut was changed.");
        float radarReady=Time.unscaledTime+.2f;while(Time.unscaledTime<radarReady)yield return null;Capture("StoreRadar");
        results.AppendLine("PASS: seven suppliers/showroom, real fuel/station landmarks, ready-order badge from the paid ledger, store radar and modal input restoration.");
        var driver=trip.GetComponent<DriverDelivery>();Require(driver!=null,"Existing employee driver is missing.");
        DriverMapView.Open(driver,trip.uiRoot);for(int i=0;i<3;i++)yield return null;
        var tracking=trip.uiRoot.GetComponentInChildren<DriverMapView>();var trackingMap=tracking.GetComponentInChildren<CityMapGraphic>();
        Require(Vector3.Distance(trackingMap.Data.Home.Position,map.Home.Position)<.01f,"Driver tracking uses an outdated parking marker.");
        var trackingBackdrop=tracking.transform.parent.gameObject;tracking.Close();Require(!trackingBackdrop.activeSelf,"Driver map left an input-blocking backdrop behind.");
        yield return null;
        trip.Begin(); double until = EditorApplication.timeSinceStartup + 120;
        while (trip.Transitioning && EditorApplication.timeSinceStartup < until) yield return null;
        Require(trip.InCity && !trip.Transitioning, "City transition failed.");
        var city = Object.FindObjectOfType<CityWorld>(); Require(city.Map != null, "Driving radar not installed.");
        var hud = city.Map; var cityFarm = hud.Data.Points.Single(p => p.Checkpoint != null && p.Checkpoint.supplier == SupplierKind.Farmers);
        Require(hud.Navigation.Destination?.Id==farmers.Id,"The planned store-world destination was lost on entering City.");
        Require(Vector3.Distance(map.Home.Position, hud.Data.Home.Position) < .25f, "Home marker differs between scaled store and native City.");
        Require(hud.Data.Points.Any(p=>p.Symbol==TownMapSymbol.Unload),"Actual store gate is missing from the map.");
        TestRoutes(hud.Data);
        var driverRoute=new DriverRoadRoute(hud.Data.Roads);
        int longLane=Enumerable.Range(0,hud.Data.Roads.Lanes.Count).First(i=>hud.Data.Roads.Lanes[i].Path.Length>40);
        var deliveryStop=driverRoute.NearestOnLane(longLane,hud.Data.Roads.Lanes[longLane].Path.Sample(hud.Data.Roads.Lanes[longLane].Path.Length,out _));
        Require(hud.Data.Roads.Lanes[longLane].Path.Length-deliveryStop.Distance>=9.99f,"Delivery driver lost its stop-before-turn safety margin.");
        hud.Tick(); hud.Toggle(); Require(hud.IsOpen && !city.pickup.InputEnabled, "Map did not disable vehicle input.");
        Require(Vector3.Distance(hud.Window.Graphic.PlayerPosition,hud.Data.ToNative(city.pickup.transform.position))<.01f,"Map did not initialise the player marker on its opening frame.");
        hud.Navigation.Select(cityFarm); hud.Window.Refresh();
        var gate=hud.Data.Points.First(p=>p.Symbol==TownMapSymbol.Unload);hud.Navigation.Select(gate);hud.Window.Refresh();
        Require(hud.Navigation.State==TownNavigationState.Following,"No route to the real unloading gate.");
        var body=hud.Data.StoreFootprint.Value;var routeToGate=hud.Navigation.PlannedRoute;
        for(int i=1;i<routeToGate.Count;i++)
        {
            int steps=Mathf.CeilToInt(Vector3.Distance(routeToGate[i-1],routeToGate[i])*2);
            for(int step=0;step<=steps;step++){var p=Vector3.Lerp(routeToGate[i-1],routeToGate[i],steps>0?(float)step/steps:0);Require(!body.Contains(new Vector2(p.x,p.z)),"GPS cuts through the store on the way to its gate.");}
        }
        hud.Window.Graphic.Projection.ZoomAt(4,Vector2.zero,hud.Window.Graphic.rectTransform.rect);hud.Window.Graphic.Projection.Follow(new Vector3(body.center.x,0,body.center.y-10));hud.Window.Refresh();
        for(int i=0;i<3;i++)yield return null;Capture("UnloadingRoute");hud.Window.Graphic.Projection.Reset();hud.Navigation.Select(cityFarm);hud.Window.Refresh();
        results.AppendLine("PASS: real unloading route stays outside the store footprint; default employee driver retains a 10 m stop margin before turning.");
        for (int i = 0; i < 4; i++) yield return null;
        var graphic = hud.Window.Graphic;
        var pointer = graphic.GetComponent<TownMapInput>();
        pointer.OnScroll(new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, graphic.rectTransform.position), scrollDelta = new Vector2(0, 1) });
        Require(graphic.Projection.Zoom > 1, "Map wheel zoom failed.");
        var nativeRoad=hud.Data.Roads.Lanes[0].Path.Sample(20,out _);
        var clickPosition=RectTransformUtility.WorldToScreenPoint(null,graphic.rectTransform.TransformPoint(graphic.Project(nativeRoad)));
        var click=new PointerEventData(EventSystem.current){position=clickPosition,button=PointerEventData.InputButton.Left};
        pointer.OnPointerDown(click);pointer.OnPointerClick(click);Require(hud.Navigation.Destination?.Id=="waypoint","Road-click waypoint failed.");
        click.button=PointerEventData.InputButton.Right;pointer.OnPointerDown(click);pointer.OnPointerClick(click);Require(hud.Navigation.Destination==null,"Right-click did not clear GPS.");
        hud.Navigation.Select(cityFarm);
        graphic.Projection.Reset(); hud.Window.Refresh(); Capture("WorldMap");
        Require(graphic.LastVertexCount<65000,"Full map exceeded Unity UI vertex budget.");
        File.WriteAllText(Report+"/geometry.txt","Full map vertices: "+graphic.LastVertexCount+"; visual road samples: "+hud.Data.Streets.Sum(r=>r.Points.Length)+"; original driving samples: "+hud.Data.Roads.Lanes.Sum(l=>l.points.Length)+".\n");
        hud.Window.SetFilter(TownMapFilter.ReadyOrders);
        Require(graphic.Visible(cityFarm) && !graphic.Visible(hud.Data.Points.Single(p => p.Checkpoint != null && p.Checkpoint.supplier == SupplierKind.Construction)), "Ready-order filter failed.");
        Capture("ReadyOrders"); hud.Window.SetFilter(TownMapFilter.All);
        hud.Close(); Require(city.pickup.InputEnabled, "Map close did not restore driving input.");
        hud.Tick(); radarReady=Time.unscaledTime+.45f;while(Time.unscaledTime<radarReady)yield return null;Capture("DrivingRadar");
        Require(hud.MiniMap.Graphic.GetComponentInParent<Mask>() != null, "Radar has no circular stencil mask.");
        TestProgress(hud.Data);
        hud.Toggle(); hud.Window.Graphic.Projection.ZoomAt(2, Vector2.zero, hud.Window.Graphic.rectTransform.rect); hud.Window.Refresh();
        Capture("ZoomedMap", 1280, 720); hud.Close();
        trip.Return();until=EditorApplication.timeSinceStartup+120;
        while(trip.Transitioning&&EditorApplication.timeSinceStartup<until)yield return null;
        Require(!trip.InCity&&!trip.Transitioning&&store.Navigation.Destination?.Id==farmers.Id,"GPS target was lost on returning to the store.");
        results.AppendLine("PASS: real Game→City transition, shared native home position, full map and circular driving radar, wheel zoom, order filter and vehicle input restoration.");
        string save = Path.Combine(Application.persistentDataPath, "gameData.json");
        Require(SessionState.GetString("NavigationUpdate.Save", "") == (File.Exists(save) ? PinebrookDistrictBuild.Hash(save) : "missing"), "Player save changed.");
    }
    private static void TestProjection(TownMapData map)
    {
        var projection = new TownMapProjection(map.Bounds); var view = new Rect(-500, -310, 1000, 620);
        foreach (var point in map.Points) Require(view.Contains(projection.Project(point.Position, view)), "Destination excluded from world overview: " + point.Name);
        var at = new Vector3(-78, 0, 357); var round = projection.Unproject(projection.Project(at, view), view);
        Require(Vector3.Distance(at, round) < .001f, "Map projection round-trip failed.");
        float x = Vector2.Distance(projection.Project(at, view), projection.Project(at + Vector3.right * 10, view));
        float z = Vector2.Distance(projection.Project(at, view), projection.Project(at + Vector3.forward * 10, view));
        Require(Mathf.Abs(x - z) < .001f, "World map stretches native geometry.");
        projection.LocalDiameter = 200; projection.Follow(at); projection.Heading = 90;
        Require(Vector2.Distance(projection.Project(at, view), view.center) < .001f && projection.Project(at + Vector3.right * 10, view).y > view.center.y, "Heading-up radar projection incorrect.");
        projection.LocalDiameter=0;projection.Reset();projection.ZoomAt(100,view.center,view);Require(projection.Zoom==16,"Maximum zoom not enforced.");
        projection.Pan(new Vector2(100000,100000),view);var edge=projection.Unproject(view.min,view);Require(edge.x>=map.Bounds.xMin-.01f&&edge.z>=map.Bounds.yMin-.01f,"Pan escaped map bounds.");
        projection.ZoomAt(.00001f,view.center,view);Require(projection.Zoom==1,"Minimum zoom not enforced.");
        results.AppendLine("PASS: full-world POI fit, isotropic metre scale, projection round-trip and heading-up radar at 90 degrees.");
    }
    private static void TestRoutes(TownMapData map)
    {
        int count = 0; var planner = new DriverRoadRoute(map.Roads, true);
        var destinations = map.Points.Where(p => p.Checkpoint != null || p == map.Home).ToArray();
        foreach (var a in destinations) foreach (var b in destinations)
        {
            if (a == b) continue;
            Require(planner.Plan(a.Access[0], b.Access[0]), "Disconnected GPS: " + a.Name + " to " + b.Name);
            for (int i = 1; i < planner.Lanes.Count; i++)
            {
                var incoming = map.Roads.Lanes[planner.Lanes[i - 1]]; var outgoing = map.Roads.Lanes[planner.Lanes[i]];
                Require(incoming.to == outgoing.from, "Route jumps between roads.");
                Require(incoming.from != outgoing.to || map.Roads.Departures(incoming.to).Count == 1, "Illegal street U-turn in GPS.");
            }
            Require(planner.MapPoints().Length > 0 && (Vector3.Distance(a.Access[0],b.Access[0])<.1f||planner.MapPoints().Length>1), "GPS produced an empty route: "+a.Name+" to "+b.Name); count++;
        }
        results.AppendLine("PASS: " + count + " supplier/home pairs connected by directed public lanes, with no immediate street U-turns.");
    }
    private static void TestProgress(TownMapData map)
    {
        var navigation = new TownNavigation(map); var target = map.Points.Single(p => p.Checkpoint != null && p.Checkpoint.supplier == SupplierKind.Farmers);
        navigation.Tick(map.Home.Position, Vector3.forward, 0, 0); navigation.Select(target);
        Require(navigation.State == TownNavigationState.Following, "No supplier GPS route.");
        float original = navigation.RemainingMetres; var points = navigation.PlannedRoute.ToArray(); float time = 0;
        foreach (var p in points.Take(Math.Min(28, points.Length - 1))) { time += .1f; navigation.Tick(p, Vector3.zero, 0, time); }
        Require(navigation.RemainingMetres < original - 10, "Route distance did not decrease with progress.");
        int replans = navigation.Replans; var wrong = new Vector3(136, 0, 560);
        navigation.Tick(wrong, Vector3.forward, 4, time + 3); navigation.Tick(wrong, Vector3.forward, 4, time + 5);
        Require(navigation.Replans > replans, "GPS failed to recalculate off-route.");
        navigation.Tick(target.Position, Vector3.zero, 0, time + 6); Require(navigation.State == TownNavigationState.Arrived, "Arrival detection failed.");
        navigation.Clear(); Require(navigation.Route.Count == 0 && navigation.Destination == null, "Clear route retained stale guidance.");
        results.AppendLine("PASS: decreasing remaining road distance, throttled off-route replanning, arrival and clear-route behaviour.");
    }
    private static void TestDisconnected()
    {
        var network = ScriptableObject.CreateInstance<TrafficRoadNetwork>();
        try
        {
            var nodes = new[] { new TrafficRoadNetwork.Junction { position = Vector3.zero }, new TrafficRoadNetwork.Junction { position = Vector3.forward * 8 }, new TrafficRoadNetwork.Junction { position = Vector3.right * 100 }, new TrafficRoadNetwork.Junction { position = new Vector3(100, 0, 8) } };
            TrafficRoadNetwork.Lane Lane(int a, int b)
            {
                var forward=(nodes[b].position-nodes[a].position).normalized;var side=Vector3.Cross(Vector3.up,forward);
                return new TrafficRoadNetwork.Lane { from = a, to = b, points = new[] { nodes[a].position+forward+side,nodes[b].position-forward+side } };
            }
            network.Configure(nodes, new[] { Lane(0, 1), Lane(1, 0), Lane(2, 3), Lane(3, 2) });
            var route = new DriverRoadRoute(network, true);
            Require(!route.Plan(new Vector3(0, 0, 2), new Vector3(100, 0, 5)), "Disconnected graph returned a false route.");
            Require(route.Nearest(new Vector3(0, 0, 4)).Position.z > 3.9f, "Short road nearest-point projection failed.");
        }
        finally { Object.DestroyImmediate(network); }
    }
    private static void Capture(string name, int width = 1920, int height = 1080)
    {
        var camera = Camera.main; var target = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
        var canvases = Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var cameras = canvases.Select(c => c.worldCamera).ToArray(); var planes = canvases.Select(c => c.planeDistance).ToArray();
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            target.Create(); camera.targetTexture = target;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .6f; }
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes(Pictures + "/" + name + ".png", texture.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = planes[i]; }
            camera.targetTexture = oldTarget; RenderTexture.active = previous; target.Release(); Object.Destroy(target); Object.Destroy(texture); Canvas.ForceUpdateCanvases();
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false); Directory.CreateDirectory(Report);
        File.WriteAllText(Report + "/result.txt", (error == null ? "PASS:\n" : "FAILED:\n") + results + (error == null ? "Player save unchanged.\n" : error.ToString()));
        if (error != null) Debug.LogException(error); EditorApplication.Exit(error == null ? 0 : 1);
    }
}
