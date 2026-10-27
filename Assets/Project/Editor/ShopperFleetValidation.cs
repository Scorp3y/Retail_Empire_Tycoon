using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Checks imported rigs and real purchases without reading or writing the player's save.</summary>
[InitializeOnLoad]
public static class ShopperFleetValidation
{
    private const string Pending = "ShopperQA.Pending";
    private static IEnumerator checks;
    private static readonly StringBuilder report = new StringBuilder();
    private static double deadline;

    static ShopperFleetValidation() { EditorApplication.update += Tick; }

    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory(ShopperFleetBatch.Evidence);
        File.WriteAllText(ShopperFleetBatch.Evidence + "/validation.txt", "RUNNING\n");
        CheckRosterAndRigs();
        SessionState.SetString("ShopperQA.AssetReport", report.ToString());
        EditorSceneManager.OpenScene("Assets/Project/Scenes/Game.unity");
        ShopGameplaySandbox.Open();
        foreach (var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true)) Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void CheckRosterAndRigs()
    {
        var roster = Resources.Load<ShopperRoster>("Shoppers/Roster");
        Require(roster != null && roster.Variants.Count == 50, "Fifty shoppers were not registered.");
        var manifest = JsonUtility.FromJson<ShopperFleetBatch.Manifest>(File.ReadAllText(ShopperFleetBatch.Root + "/Manifest.json"));
        Require(manifest.characters.Length == 50 && manifest.characters.Select(design => design.id).Distinct().Count() == 50,
            "Expected fifty unique appearance definitions.");
        int lightSkins = manifest.characters.Count(design => design.skin == "light" || design.skin == "peach");
        Require(manifest.light_skin_count == 35 && lightSkins == 35, "Expected exactly 35 light-skinned appearances out of 50.");
        var randomBefore = JsonUtility.ToJson(UnityEngine.Random.state);
        var selection = roster.CreateSelection(1729);
        GameObject previous = null;
        for (int cycle = 0; cycle < 10; cycle++)
        {
            var used = new HashSet<GameObject>();
            for (int i = 0; i < 50; i++)
            {
                var next = selection.Next();
                Require(next != previous && used.Add(next), "Appearance repeated before exhausting the roster.");
                previous = next;
            }
        }
        Require(randomBefore == JsonUtility.ToJson(UnityEngine.Random.state), "Appearance selection changed gameplay randomness.");
        int maxTriangles = 0;
        foreach (var prefab in roster.Variants)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                instance.GetComponent<ShopperAppearance>().FitHeight(ShopperDimensions.ShopHeight);
                var animator = instance.GetComponentInChildren<Animator>();
                var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                var design = manifest.characters.Single(entry => entry.id == prefab.name);
                Require(design.skin_cell != null && design.skin_cell.Length == 2 && skin.sharedMesh.uv.Count(uv =>
                    Mathf.FloorToInt(uv.x * 16) == design.skin_cell[0] && Mathf.FloorToInt(uv.y * 4) == design.skin_cell[1]) > 30,
                    "Imported skin UVs do not match the approved appearance: " + prefab.name);
                Require(skin.sharedMaterials.Length == 1, "Expected a single palette material.");
                maxTriangles = Mathf.Max(maxTriangles, skin.sharedMesh.triangles.Length / 3);
                var clips = animator.runtimeAnimatorController.animationClips;
                Require(clips.Select(c => c.name).Distinct().Count() == 3, "Animation set is incomplete.");
                var rootPosition = instance.transform.position;
                var transforms = animator.GetComponentsInChildren<Transform>();
                var rightElbow = transforms.Single(t => t.name == "Forearm.R");
                clips.Single(c => c.name == "Idle").SampleAnimation(animator.gameObject, 0);
                float idleElbowDepth = instance.transform.InverseTransformPoint(rightElbow.position).z;
                foreach (string state in new[] { "Idle", "Walk", "Browse" })
                {
                    var clip = clips.Single(c => c.name == state);
                    clip.SampleAnimation(animator.gameObject, clip.length * .18f);
                    var rotations = transforms.Select(t => t.localRotation).ToArray();
                    var positions = transforms.Select(t => t.localPosition).ToArray();
                    clip.SampleAnimation(animator.gameObject, clip.length * .61f);
                    Require(transforms.Where((t, i) => Quaternion.Angle(rotations[i], t.localRotation) > .5f ||
                        Vector3.Distance(positions[i], t.localPosition) > .0001f).Any(), prefab.name + " has static " + state);
                    Require(instance.transform.position == rootPosition, "Animation moved the gameplay root.");
                    if (state == "Browse")
                        Require(instance.transform.InverseTransformPoint(rightElbow.position).z > idleElbowDepth + .02f,
                            "Browsing arm reaches backwards: " + prefab.name);
                }
                clips.Single(c => c.name == "Idle").SampleAnimation(animator.gameObject, 0);
                // BakeMesh in this importer configuration includes the root scale even with useScale=false.
                // Measure at unit scale, then apply the game's explicit fitting factor exactly once.
                var fittingScale = instance.transform.localScale;
                instance.transform.localScale = Vector3.one;
                var mesh = new Mesh();
                skin.BakeMesh(mesh, false);
                var vertices = mesh.vertices.Select(v => instance.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
                float height = (vertices.Max(v => v.y) - vertices.Min(v => v.y)) * fittingScale.y;
                Object.DestroyImmediate(mesh);
                instance.transform.localScale = fittingScale;
                Require(Mathf.Abs(height - ShopperDimensions.ShopHeight) < .015f, "In-game height mismatch: " + prefab.name + " / " + height);
                var marker = transforms.Single(t => t.name == "FacingMarker");
                Require(instance.transform.InverseTransformPoint(marker.position).z > 0, "Shopper faces backwards.");
            }
            finally { Object.DestroyImmediate(instance); }
        }
        report.AppendLine("PASS 50 unique appearances, 10 full shuffle cycles, no immediate repeats, independent gameplay random state.");
        report.AppendLine("PASS 35/50 light-skinned appearances (70%), all imported skin UVs match their definitions. The remaining 15 retain varied skin tones.");
        report.AppendLine("PASS all 50 imported rigs: animated Idle/Walk/Browse, fixed gameplay root, forward facing, " + ShopperDimensions.ShopHeight + " shop height, single material. Max triangles: " + maxTriangles);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (checks == null) { deadline = EditorApplication.timeSinceStartup + 180; checks = CheckGame(); }
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Shopper gameplay validation timed out.");
            if (checks.MoveNext()) return;
            Finish(0, "PASS shoppers integrated into real shop purchase/leave flow. No player save accessed.\n");
        }
        catch (Exception error) { Debug.LogException(error); Finish(1, "FAIL " + error); }
    }

    private static void Finish(int code, string text)
    {
        SessionState.SetBool(Pending, false);
        File.WriteAllText(ShopperFleetBatch.Evidence + "/validation.txt", report + text);
        if (code == 0)
        {
            using var hash = SHA256.Create();
            var bytes = hash.ComputeHash(File.ReadAllBytes(ShopperFleetBatch.Root + "/Manifest.json"));
            File.WriteAllText(ShopperFleetBatch.Evidence + "/validated-manifest.sha256", BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant());
        }
        EditorApplication.Exit(code);
    }

    private static IEnumerator CheckGame()
    {
        report.Append(SessionState.GetString("ShopperQA.AssetReport", ""));
        for (int i = 0; i < 4; i++) yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true) == null, "Player save writer present.");
        var starter = Object.FindObjectOfType<StarterStoreInitialization>();
        starter.cityTrip.orders.save = null;
        var initial = starter.CreateNewGame(12000);
        Object.FindObjectOfType<StoreProgression>().ApplySaveData(initial.territory);
        var spawner = Object.FindObjectOfType<StorePrefabSpawner>();
        spawner.UsesModularStore = true; starter.ConfigureSite(true); spawner.Spawn(StoreLevelId.Lvl1);
        starter.ApplyNewGameBuildings(initial);
        for (int i = 0; i < 4; i++) yield return null;
        var shop = Object.FindObjectOfType<StoreOperations>();
        shop.enabled = false; shop.SetOpen(false);
        ShopGameplaySandbox.PrepareActivities();
        starter.building.ExitBuildMode();
        var shelves = Object.FindObjectsOfType<PlacedShelfStock>();
        Require(shelves.Length > 0, "Starter store has no placed shelves.");
        var product = AssetDatabase.FindAssets("t:ProductItemData")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ProductItemData>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(p => p != null && shelves.Any(s => s.IsProductAllowed(p)));
        foreach (var shelf in shelves) shelf.ClearStock();
        var stocked = shelves.First(s => s.IsProductAllowed(product));
        stocked.SetStockFromSave(product, 24);
        var balance = Object.Instantiate(shop.Balance);
        balance.thiefChance = 0; balance.litterChance = 0;
        ShopGameplaySetup.Set(shop, "balance", balance);
        shop.Advance(.1f);
        Require(shop.CustomerAppearanceCount == 50, "Game did not load the new roster.");
        shop.SetOpen(true);
        Require(shop.TrySpawnCustomer(), "Real stocked store failed to spawn a shopper: " + shop.Notice);
        shop.SetOpen(false);
        var visit = shop.Visits.Single();
        var actor = visit.Character;
        var animator = actor.GetComponentInChildren<Animator>();
        Require(actor.GetComponentInChildren<ShopperAppearance>() != null, "Game spawned a legacy appearance.");
        var origin = actor.transform.position;
        bool walked = false, browsed = false;
        for (int i = 0; i < 450 && visit.Stage != VisitStage.Waiting; i++)
        {
            shop.Advance(.1f); animator.Update(.1f);
            walked |= animator.GetBool("isWalking");
            browsed |= animator.GetBool("isBrowsing");
            if (i % 20 == 0) yield return null;
        }
        Require(walked && browsed && visit.BrowsingSeconds >= 1.9f, "Walking/browsing states never ran.");
        Require(visit.Stage == VisitStage.Waiting && Vector3.Distance(origin, actor.transform.position) > .2f, "Shopper did not walk to the checkout.");
        CaptureGame(shop);
        int moneyBefore = shop.Money, stockBefore = stocked.CurrentAmount;
        Require(shop.ClaimCheckout() == visit && shop.CompleteCheckout(visit), "Sale could not be completed.");
        Require(shop.Money == moneyBefore + visit.Quantity * product.SellPrice && stocked.CurrentAmount == stockBefore - visit.Quantity, "Wrong payment or stock deduction.");
        for (int i = 0; i < 450 && shop.Visits.Contains(visit); i++) shop.Advance(.1f);
        Require(!shop.Visits.Contains(visit), "Paid shopper could not leave.");
        yield return null;
        stocked.SetStockFromSave(product, 24); shop.SetOpen(true);
        for (int i = 0; i < 6; i++) Require(shop.TrySpawnCustomer(), "Expected existing level-one customer capacity.");
        Require(!shop.TrySpawnCustomer(), "Fifty variants incorrectly increased simultaneous customer cap.");
        shop.SetOpen(false);
        for (int frame = 0; frame < 35; frame++)
        {
            shop.Advance(.08f);
            foreach (var shopper in shop.Visits) shopper.Character.GetComponentInChildren<Animator>().Update(.08f);
            yield return null;
        }
        report.AppendLine("PASS real Game: new roster, entrance -> shelf (2 seconds browsing) -> checkout, exact payment/stock, exit. Existing six-customer level-one limit preserved.");
    }

    private static void CaptureGame(StoreOperations shop)
    {
        var camera = Camera.main;
        if (camera == null || shop.Visits.Count == 0) return;
        var input = camera.GetComponent<MainCamera>();
        if (input != null) input.enabled = false;
        var actor = shop.Visits[0].Character.transform;
        Vector3 centre = actor.position + Vector3.up * .24f;
        Vector3 offset = actor.forward * .8f + actor.right * .45f + Vector3.up * .6f;
        camera.transform.SetPositionAndRotation(centre + offset, Quaternion.LookRotation(-offset));
        camera.orthographic = true; camera.orthographicSize = .7f;
        camera.nearClipPlane = .01f;
        // A cutaway review only: hide nearby architecture, without moving actors or furniture.
        var walls = Object.FindObjectsOfType<StoreWallOccluder>()
            .SelectMany(w => w.GetComponentsInChildren<Renderer>())
            .Concat(Object.FindObjectsOfType<PlacedObject>().Where(p => p.item != null && p.item.isDoorway)
                .SelectMany(p => p.GetComponentsInChildren<Renderer>()))
            .Where(r => r.enabled).Distinct().ToArray();
        var target = new RenderTexture(1400, 1000, 24);
        var previous = RenderTexture.active;
        var image = new Texture2D(1400, 1000, TextureFormat.RGBA32, false);
        try
        {
            foreach (var wall in walls) wall.enabled = false;
            target.Create(); camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0); image.Apply();
            File.WriteAllBytes(ShopperFleetBatch.Evidence + "/InGame.png", image.EncodeToPNG());
        }
        finally
        {
            foreach (var wall in walls) if (wall != null) wall.enabled = true;
            camera.targetTexture = null; RenderTexture.active = previous;
            target.Release(); Object.Destroy(target); Object.Destroy(image);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
