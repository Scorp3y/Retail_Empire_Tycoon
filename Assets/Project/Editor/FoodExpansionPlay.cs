using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.SaveSystem;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Paid delivery regression plus actual restock, save and shopper cycles for every new food.</summary>
[InitializeOnLoad]
public static class FoodExpansionPlay
{
    private const string Pending = "FoodExpansion.Play";
    private static IEnumerator checks;
    private static double deadline;
    private static readonly StringBuilder trace = new StringBuilder();
    static FoodExpansionPlay() { EditorApplication.update += Tick; }

    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory(FoodExpansionIntegration.Report);
        Directory.CreateDirectory(AssortmentUpdateBatch.Report);
        File.WriteAllText(FoodExpansionIntegration.Report + "/play.txt", "RUNNING\n");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorUtility.UnloadUnusedAssetsImmediate(); GC.Collect();
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath); ShopGameplaySandbox.Open();
        foreach (var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true)) Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (checks == null) { checks = Check(); deadline = EditorApplication.timeSinceStartup + 1200; }
            Require(EditorApplication.timeSinceStartup < deadline, "Food integration checks timed out.");
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Debug.LogException(error); Finish(error); }
    }

    private static IEnumerator Check()
    {
        var regression = AssortmentUpdatePlay.Check();
        while (regression.MoveNext()) yield return regression.Current;
        File.Copy(AssortmentUpdateBatch.Report + "/details.txt", FoodExpansionIntegration.Report + "/delivery-and-fit.txt", true);
        var starter = Object.FindObjectOfType<StarterStoreInitialization>();
        var store = Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();
        var inventory = Object.FindObjectOfType<ProductInventory>();
        var productSave = Object.FindObjectOfType<ProductSaveService>();
        var work = Object.FindObjectOfType<WorkMinigame>();
        var pack = StoreAssortmentPack.Load();
        Require(pack.Products.Count == 240 && FoodExpansionIntegration.Entries.Length == 96, "Incomplete expanded pack.");
        foreach (var group in FoodExpansionIntegration.Entries.GroupBy(e => FixtureNumber((ProductStorageType)Enum.Parse(typeof(ProductStorageType), e.storage))))
        {
            var fixture = pack.Fixtures.Single(f => f.id.StartsWith("shelf_fixture_" + group.Key.ToString("D2") + "_"));
            var placed = PlaceFixture(starter, fixture);
            var shelf = placed.GetComponent<PlacedShelfStock>();
            foreach (var entry in group)
            {
                foreach (var other in Object.FindObjectsOfType<PlacedShelfStock>()) other.ClearStock();
                var product = starter.cityTrip.orders.productCatalog.GetById("product_" + entry.key);
                inventory.Clear(); inventory.Add(product, 10);
                Require(work.BeginRestock(shelf, product) && work.CompleteStep(0), "Cannot begin restock: " + product.Id);
                work.Cancel();
                Require(shelf.IsEmpty && inventory.GetCount(product) == 10, "Restock cancellation consumed goods.");
                Require(work.BeginRestock(shelf, product), "Cannot restart restock.");
                for (int step = 0; step < 5; step++) Require(work.CompleteStep(step), "Restock gesture failed.");
                int expected = Mathf.Min(10, shelf.CapacityFor(product));
                Require(!work.IsActive && shelf.CurrentProduct == product && shelf.CurrentAmount == expected, "Restock did not commit correct quantity.");
                var snapshot = productSave.BuildShelfSaveData();
                shelf.ClearStock(); productSave.ApplyShelfSaveData(snapshot);
                Require(shelf.CurrentProduct == product && shelf.CurrentAmount == expected, "New product ID did not survive stock save.");
                yield return null;
                Physics.SyncTransforms(); starter.building.NotifyLayoutChanged(); store.Advance(.05f); store.Advance(.05f);
                store.Balance.thiefChance = 0; store.SetOpen(true);
                Require(store.TrySpawnCustomer(), "Customer cannot reach " + product.Id + ": " + store.Notice);
                var visit = store.Visits.Single(); store.SetOpen(false);
                Require(visit.Product == product, "Customer selected wrong food.");
                for (int frame = 0; frame < 2000 && visit.Stage != VisitStage.Waiting && !visit.IsFinished; frame++)
                { store.Advance(.05f); if (frame % 20 == 0) yield return null; }
                Require(visit.Stage == VisitStage.Waiting, "Customer could not browse and queue: " + product.Id);
                int wallet = store.Money; int amount = shelf.CurrentAmount;
                Require(store.ClaimCheckout() == visit && store.CompleteCheckout(visit), "Checkout rejected " + product.Id);
                Require(store.Money == wallet + visit.Quantity * product.SellPrice && shelf.CurrentAmount == amount - visit.Quantity, "Sale quantity/value mismatch.");
                Require(!store.CompleteCheckout(visit), "Checkout paid twice.");
                for (int frame = 0; frame < 2000 && !visit.IsFinished; frame++)
                { store.Advance(.05f); if (frame % 20 == 0) yield return null; }
                Require(visit.IsFinished, "Shopper cannot leave: " + product.Id);
                trace.AppendLine("PASS " + product.Id + ": fitted real rack, five-gesture restock, cancellation, stock save, customer path/browse/queue, payment once, exit.");
                File.WriteAllText(FoodExpansionIntegration.Report + "/customers.txt", trace.ToString());
            }
            starter.building.grid.Release(placed.occupiedCells);
            Object.DestroyImmediate(placed.gameObject); Physics.SyncTransforms(); starter.building.NotifyLayoutChanged();
        }
    }

    private static int FixtureNumber(ProductStorageType type)
    {
        if (type == ProductStorageType.Produce) return 6;
        if (type == ProductStorageType.Bakery) return 5;
        if (type == ProductStorageType.Dairy || type == ProductStorageType.Meat || type == ProductStorageType.ReadyMeals) return 7;
        return 1;
    }

    private static PlacedObject PlaceFixture(StarterStoreInitialization starter, BuildItemData fixture)
    {
        var building = starter.building; building.inventory.Add(fixture, 1); building.EnterBuildMode(fixture);
        bool placed = false;
        foreach (var tile in starter.floors.BuildSaveData().Where(t => t.floor == 0 && t.itemId != "asphalt_01"))
            if (building.TryPlaceAt(new Vector3Int(tile.x, 0, tile.z))) { placed = true; break; }
        building.ExitBuildMode(); Require(placed, "Cannot install test equipment: " + fixture.id);
        return Object.FindObjectsOfType<PlacedObject>().Single(p => p.item == fixture);
    }

    private static void Finish(Exception error)
    {
        SessionState.SetBool(Pending, false); Time.timeScale = 1; checks = null;
        File.WriteAllText(FoodExpansionIntegration.Report + "/play.txt", error == null
            ? "PASS: 240-product paid UI order/delivery/cargo/warehouse/save/fit regression, 20 placeable fixtures, and 96 real new-food restock/customer/checkout/exit cycles. Player saves untouched.\n"
            : "FAIL\n" + error + "\n" + trace);
        EditorApplication.isPlaying = false; EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static void Require(bool condition, string message) => FoodExpansionIntegration.Require(condition, message);
}
