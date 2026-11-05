using System;
using System.Collections;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class WorkshopTuningValidation
{
    private const string Pending = "WorkshopTuning.Validation";
    private const string Report = "Docs/WorkshopTuning";
    private static IEnumerator check;
    private static double deadline;
    static WorkshopTuningValidation() => EditorApplication.update += Tick;

    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory(Report);
        File.WriteAllText(Report + "/Result.txt", "RUNNING");
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
        ShopGameplaySandbox.Open();
        foreach (var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true)) Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || Time.frameCount < 8) return;
        try
        {
            if (check == null) { check = Verify(); deadline = EditorApplication.timeSinceStartup + 190; }
            Require(EditorApplication.timeSinceStartup < deadline, "Workshop validation timed out.");
            if (check.MoveNext()) return;
            SessionState.SetBool(Pending, false);
            File.AppendAllText(Report + "/Result.txt", "\nPASS\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            SessionState.SetBool(Pending, false);
            File.AppendAllText(Report + "/Result.txt", "\nFAIL\n" + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static IEnumerator Verify()
    {
        Require(Object.FindObjectOfType<SaveManager>(true) == null, "Player save is not isolated.");
        var starter = Object.FindObjectOfType<StarterStoreInitialization>();
        var trip = starter.cityTrip;
        var orders = trip.orders;
        orders.save = null;
        var data = starter.CreateNewGame(50000);
        Object.FindObjectOfType<StoreProgression>().ApplySaveData(data.territory);
        starter.building.inventory.ApplySaveData(data.buildInventory, starter.catalog);
        var spawner = Object.FindObjectOfType<StorePrefabSpawner>();
        spawner.UsesModularStore = true;
        starter.ConfigureSite(true);
        spawner.Spawn(StoreLevelId.Lvl1);
        starter.ApplyNewGameBuildings(data);
        Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>().enabled = false;
        Object.FindObjectOfType<RetailEmpireTycoon.Parking.ParkingTraffic>().enabled = false;
        orders.money.SetMoney(1000000);
        var fleet = trip.Fleet;
        fleet.Restore(null);
        Require(fleet.SelectedTuning.engine == 0 && orders.pickupCapacityKg == 350, "Legacy save did not retain stock behavior.");
        var shopRail = Object.FindObjectOfType<TownRailTraffic>();
        shopRail.Advance(45);
        double shopClock = shopRail.ServiceClock;
        trip.Begin();
        while (trip.Transitioning || !trip.InCity) yield return null;
        var city = Object.FindObjectOfType<CityWorld>();
        var cityRail = city.town.GetComponentInChildren<TownRailTraffic>();
        Require(cityRail.ServiceClock >= shopClock && cityRail.ServiceClock - shopClock < 5,
            "Entering City restarted the shared train schedule.");
        var car = city.pickup;
        car.ReadKeyboard = false;
        city.chaseCamera.ReadMouse = false;
        if (data.frontageDecorGranted)
        {
            var lamp = starter.catalog.GetById("frontage_street_lamp").prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            var lamps = city.facadeRoot.GetComponentsInChildren<MeshFilter>().Where(filter => filter.sharedMesh == lamp).ToArray();
            Require(lamps.Length == 2 && lamps.All(filter =>
                Mathf.Abs(filter.GetComponent<MeshRenderer>().bounds.size.y - 2.75f) < .02f),
                "The City facade did not copy the editable street lamps at their authored scale.");
            Directory.CreateDirectory("Docs/TownFlowUpdate");
            var centre = (lamps[0].transform.position + lamps[1].transform.position) * .5f;
            TownFloristBatch.Render(city.gameObject.scene, "Docs/TownFlowUpdate/CityDecor.png",
                centre + new Vector3(-17, 12, -16), centre + Vector3.forward * 4);
        }
        var fuelCheck = VerifyFuel(city, fleet, orders.money);
        while (fuelCheck.MoveNext()) yield return fuelCheck.Current;
        var door = city.town.GetComponentsInChildren<GarageDoor>().Single(d => d.name == "Door_2");
        Require(city.Map.Data.Points.Any(p => p.Id == "auto_works" && p.Access.Length == 3), "Workshop map marker missing.");
        car.Body.position = door.transform.TransformPoint(0, .05f, 11);
        car.Body.rotation = Quaternion.LookRotation(-door.transform.forward);
        Physics.SyncTransforms();
        Require(!door.CanService(car), "Workshop can be opened from the yard.");
        car.Body.position = door.transform.TransformPoint(0, .05f, -6.25f);
        car.Stop();
        Physics.SyncTransforms();
        for (int i = 0; i < 6; i++) yield return null;
        Require(door.CanService(car), "Parked vehicle does not fit the workshop interaction.");
        city.Interact();
        Require(!car.InputEnabled && ButtonNamed("Change view") != null, "Normal workshop interaction did not lock driving or show the UI.");
        for (float until = Time.realtimeSinceStartup + .65f; Time.realtimeSinceStartup < until;) yield return null;
        var front = city.chaseCamera.transform.position;
        TakeScreenshot("01-Engine");

        int before = orders.money.Money;
        ButtonNamed("Buy upgrade").onClick.Invoke();
        Require(fleet.SelectedTuning.engine == 1 && orders.money.Money == before - 900 && car.topSpeed > 14 && car.acceleration > 5,
            "Engine purchase did not apply to physical driving.");
        ButtonNamed("Handling").onClick.Invoke();
        ButtonNamed("Buy upgrade").onClick.Invoke();
        Require(fleet.SelectedTuning.handling == 1, "Handling level was not purchased.");
        ButtonNamed("Cargo capacity").onClick.Invoke();
        ButtonNamed("Buy upgrade").onClick.Invoke();
        Require(Mathf.Abs(orders.pickupCapacityKg - 402.5f) < .02f && Mathf.Abs(orders.pickupCapacityM3 - 2.3f) < .02f,
            "Cargo upgrade did not change real loading limits.");
        for (float until = Time.realtimeSinceStartup + .65f; Time.realtimeSinceStartup < until;) yield return null;
        Require(Vector3.Distance(front, city.chaseCamera.transform.position) > 1, "The camera did not move for the cargo category.");
        ButtonNamed("Respray").onClick.Invoke();
        SliderNamed("Paint R").value = 50;
        SliderNamed("Paint G").value = 163;
        SliderNamed("Paint B").value = 182;
        var custom = new Color32(50, 163, 182, 255);
        Require(!fleet.SelectedTuning.painted && PaintMatches(car, custom), "Starter RGB preview mutated the save or did not color its body.");
        for (float until = Time.realtimeSinceStartup + .65f; Time.realtimeSinceStartup < until;) yield return null;
        TakeScreenshot("02-RGB");
        ButtonNamed("Exit").onClick.Invoke();
        Require(car.InputEnabled && !fleet.SelectedTuning.painted && !PaintMatches(car, custom),
            "Workshop exit did not restore controls and factory paint.");
        car.Stop();
        city.Interact();
        ButtonNamed("Respray").onClick.Invoke();
        SliderNamed("Paint R").value = custom.r;
        SliderNamed("Paint G").value = custom.g;
        SliderNamed("Paint B").value = custom.b;
        before = orders.money.Money;
        ButtonNamed("Apply paint").onClick.Invoke();
        Require(orders.money.Money == before - 450 && fleet.SelectedTuning.Paint.Value.Equals(custom), "Paint was not committed at the listed price.");
        ButtonNamed("Exit").onClick.Invoke();
        car.SetInput(0, 1, true);
        for (float until = Time.fixedTime + .7f; Time.fixedTime < until;) yield return null;
        Require(car.wheels.Any(wheel => wheel.front && wheel.contact.steerAngle > 42.1f),
            "Steering upgrade did not reach the wheels.");
        car.Stop();
        var saved = JsonUtility.FromJson<VehicleFleetSaveData>(JsonUtility.ToJson(fleet.Capture()));
        fleet.Restore(saved);
        Require(PaintMatches(car, custom) && fleet.SelectedTuning.engine == 1 && fleet.SelectedTuning.handling == 1 &&
            fleet.SelectedTuning.cargo == 1, "Vehicle tuning did not survive save JSON or model rebuild.");
        var truck = fleet.Catalog.Last();
        Require(fleet.BuyAndSelect(truck.id) && fleet.SelectedTuning.engine == 0 && !fleet.SelectedTuning.painted,
            "Tuning leaked to a different vehicle.");
        car.Body.position = door.transform.TransformPoint(0, .10f, -6.85f);
        Physics.SyncTransforms();
        for (int i = 0; i < 6; i++) yield return null;
        Require(door.CanService(car), "Heavy truck cannot open the workshop.");
        city.Interact();
        ButtonNamed("Respray").onClick.Invoke();
        SliderNamed("Paint R").value = 112;
        SliderNamed("Paint G").value = 84;
        SliderNamed("Paint B").value = 178;
        for (float until = Time.realtimeSinceStartup + .65f; Time.realtimeSinceStartup < until;) yield return null;
        TakeScreenshot("03-Truck");
        ButtonNamed("Apply paint").onClick.Invoke();
        ButtonNamed("Exit").onClick.Invoke();
        Require(fleet.SelectedTuning.Paint.Value.Equals(new Color32(112,84,178,255)), "Heavy paint did not commit.");
        Require(fleet.Select(PlayerVehicleFleet.StarterId) && PaintMatches(car, custom) && car.topSpeed > 14,
            "Selecting the starter did not restore its own upgrades.");
        orders.money.SetMoney(0);
        Require(!fleet.BuyUpgrade(WorkshopCategory.Engine) && fleet.SelectedTuning.engine == 1, "Insufficient funds still allowed an upgrade.");
        double cityClock = cityRail.ServiceClock;
        trip.Return();
        while (trip.Transitioning || trip.InCity) yield return null;
        Require(shopRail.ServiceClock >= cityClock && shopRail.ServiceClock - cityClock < 5,
            "Returning to the shop restarted the shared train schedule.");
        File.AppendAllText(Report + "/Result.txt", "\nTrain schedule continued in both directions across the scene transitions.\n");
        File.AppendAllText(Report + "/Result.txt", "\nReal Game to City, map point, bay check, menu and camera, engine, handling, cargo, RGB preview/cancel/purchase, insufficient funds, JSON and per-vehicle upgrades checked.\n");
    }

    private static IEnumerator VerifyFuel(CityWorld city, PlayerVehicleFleet fleet, RetailEmpireTycoon.Economy.MoneyController wallet)
    {
        var car = city.pickup;
        var station = city.Map.Data.Points.Single(p => p.Id == "fuel");
        Require(fleet.FuelLitres == PlayerVehicleFleet.TankLitres && fleet.RefillPrice == 0, "An old save must start with a full tank.");
        city.Map.Navigation.Select(station);
        city.Map.Navigation.Tick(city.Map.Data.ToNative(car.transform.position), car.transform.forward, 0, Time.unscaledTime);
        Require(station.Category == TownMapCategory.Services && station.Access.Length == 4 &&
            city.Map.Navigation.State == TownNavigationState.Following, "The gas station does not route through its entrance.");
        var low = fleet.Capture();
        low.fuel.Add(new VehicleFuelSaveEntry {id = fleet.SelectedId, litres = 10});
        fleet.Restore(JsonUtility.FromJson<VehicleFleetSaveData>(JsonUtility.ToJson(low)));
        int balance = wallet.Money;
        var start = station.Position + Vector3.back * 11.5f;
        var transform = city.town.transform;
        car.Body.position = transform.TransformPoint(start + Vector3.up * .2f);
        car.Body.rotation = transform.rotation;
        car.Stop();
        Physics.SyncTransforms();
        for (float until = Time.fixedTime + .6f; Time.fixedTime < until;) yield return null;
        city.Interact();
        Require(car.InputEnabled && fleet.FuelLitres == 10 && wallet.Money == balance, "Refuelling was allowed away from the gas station.");
        car.SetInput(.42f, 0, false);
        float timeout = Time.fixedTime + 9;
        while (city.Map.Data.ToNative(car.transform.position).z < station.Position.z - 1 && Time.fixedTime < timeout)
            yield return null;
        var reached = city.Map.Data.ToNative(car.transform.position);
        Require(Vector3.Distance(reached, station.Position) < 2 && fleet.FuelLitres < 10,
            $"Pickup reached {reached}, body {car.Body.position}, target {station.Position}, {car.Speed:F2} m/s, {car.GroundedWheels} grounded wheels, {fleet.FuelLitres:F3} L.");
        city.Interact();
        Require(fleet.FuelLitres < 10 && wallet.Money == balance, "Fuel purchase was allowed while moving.");
        car.Stop();
        wallet.SetMoney(0);
        city.Interact();
        Require(fleet.FuelLitres < 10 && wallet.Money == 0, "An unaffordable refill changed the tank.");
        wallet.SetMoney(balance);
        int price = fleet.RefillPrice;
        city.Interact();
        Require(fleet.FuelLitres == PlayerVehicleFleet.TankLitres && wallet.Money == balance - price, "A parked refill did not commit once.");
        city.Interact();
        Require(wallet.Money == balance - price, "Pressing refuel on a full tank charged again.");
        yield return null;
        var hud = city.Map.MiniMap.Root.Find("Fuel and balance");
        Require(hud != null && hud.GetComponentsInChildren<TMPro.TMP_Text>().Any(label => label.text == "55 / 55 L") &&
            hud.GetComponentsInChildren<TMPro.TMP_Text>().Any(label => label.text == $"${wallet.Money:N0}"), "The driving HUD did not display current fuel and balance.");
        TakeScreenshot("04-Fuel");
        wallet.SetMoney(balance);
        File.WriteAllText(Report + "/Fuel.txt", "PASS: legacy full tank, JSON fuel, gas station GPS, physical approach and fuel consumption, stopped-only refill, funds and duplicate protection, live HUD.\n");
    }

    private static Button ButtonNamed(string text) => Object.FindObjectsOfType<Button>()
        .Single(button => button.name == text || button.name.StartsWith(text + " ", StringComparison.Ordinal));
    private static Slider SliderNamed(string text) => Object.FindObjectsOfType<Slider>().Single(slider => slider.name == text);

    private static bool PaintMatches(PickupDrive car, Color32 color)
    {
        var renderer = car.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == VehiclePaint.StarterRenderer);
        if (renderer.sharedMaterials.Length != 2) return false;
        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties, 1);
        return ((Color32)properties.GetColor("_BaseColor")).Equals(color);
    }

    private static void TakeScreenshot(string name)
    {
        var camera = Camera.main;
        var canvases = Object.FindObjectsOfType<Canvas>()
            .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var previous = RenderTexture.active;
        var target = new RenderTexture(1600, 900, 24);
        var texture = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
        try
        {
            target.Create();
            camera.targetTexture = target;
            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .6f;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Report + "/" + name + ".png", texture.EncodeToPNG());
        }
        finally
        {
            foreach (var canvas in canvases) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(texture);
        }
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}

