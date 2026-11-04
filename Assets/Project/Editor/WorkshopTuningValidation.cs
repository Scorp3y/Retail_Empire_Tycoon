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
        trip.Begin();
        while (trip.Transitioning || !trip.InCity) yield return null;
        var city = Object.FindObjectOfType<CityWorld>();
        var car = city.pickup;
        car.ReadKeyboard = false;
        city.chaseCamera.ReadMouse = false;
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
        File.AppendAllText(Report + "/Result.txt", "\nReal Game to City, map point, bay check, menu and camera, engine, handling, cargo, RGB preview/cancel/purchase, insufficient funds, JSON and per-vehicle upgrades checked.\n");
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

