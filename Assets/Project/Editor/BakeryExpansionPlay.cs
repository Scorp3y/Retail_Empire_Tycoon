using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.SaveSystem;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Existing assortment regression plus real player restocking and shopper purchases of each new bakery product.</summary>
[InitializeOnLoad]
public static class BakeryExpansionPlay
{
    private const string Pending = "BakeryExpansion.Play";
    private static IEnumerator checks;
    private static double deadline;
    private static readonly StringBuilder trace = new StringBuilder();
    static BakeryExpansionPlay() { EditorApplication.update += Tick; }

    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor(); Directory.CreateDirectory(BakeryExpansionBatch.Report);
        Directory.CreateDirectory(AssortmentUpdateBatch.Report);
        File.WriteAllText(BakeryExpansionBatch.Report + "/play.txt", "RUNNING\n");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorUtility.UnloadUnusedAssetsImmediate(); GC.Collect();
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath); ShopGameplaySandbox.Open();
        foreach (var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true)) Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (checks == null) { checks = Check(); deadline = EditorApplication.timeSinceStartup + 700; }
            Require(EditorApplication.timeSinceStartup < deadline, "Bakery QA timed out.");
            if (checks.MoveNext()) return; BakeryExpansionBatch.Record(); Finish(null);
        }
        catch (Exception error) { Debug.LogException(error); Finish(error); }
    }
    public static IEnumerator Check()
    {
        // This suite uses its own save-free scene prepared above. Keep the full
        // delivery/storage/fixture regression as the shared catalogue grows.
        var assortment = AssortmentUpdatePlay.Check();
        while (assortment.MoveNext()) yield return assortment.Current;
        File.Copy(AssortmentUpdateBatch.Report + "/details.txt", BakeryExpansionBatch.Report + "/assortment.txt", true);
        var products = CheckShelfProducts(BakeryExpansionBatch.Keys, "shelf_fresh_01", SupplierKind.Bakery, trace);
        while (products.MoveNext()) yield return products.Current;
        File.WriteAllText(BakeryExpansionBatch.Report + "/bakery.txt", trace.ToString());
    }

    public static IEnumerator CheckShelfProducts(string[] keys, string shelfId, SupplierKind supplier, StringBuilder log)
    {
        var starter = Object.FindObjectOfType<StarterStoreInitialization>(); var store = Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();
        var inventory = Object.FindObjectOfType<ProductInventory>(); var productSave = Object.FindObjectOfType<ProductSaveService>();
        var work = Object.FindObjectOfType<WorkMinigame>();
        var shelf = Object.FindObjectsOfType<PlacedShelfStock>().Single(s => s.GetComponent<PlacedObject>()?.item?.id == shelfId);
        var pack = StoreAssortmentPack.Load();
        Require(pack.Products.Count >= 54 && pack.Fixtures.Count >= 4, "Earlier assortment waves must remain present.");
        foreach (var key in keys)
        {
            foreach (var other in Object.FindObjectsOfType<PlacedShelfStock>()) other.ClearStock();
            var product = starter.cityTrip.orders.productCatalog.GetById("product_" + key);
            Require(product != null && SupplierRouting.ForProduct(product.StorageType) == supplier, "Supplier is incorrect for " + key);
            inventory.Clear(); inventory.Add(product, 10);
            Require(work.BeginRestock(shelf, product) && work.CompleteStep(0), "Player cannot begin bakery restocking.");
            work.Cancel(); Require(shelf.IsEmpty && inventory.GetCount(product) == 10, "Cancelled restocking consumed bakery goods.");
            Require(work.BeginRestock(shelf, product), "Player cannot restart bakery restocking.");
            for (int step = 0; step < 5; step++) Require(work.CompleteStep(step), "Bakery restocking gesture failed.");
            Require(!work.IsActive && shelf.CurrentProduct == product && shelf.CurrentAmount == 10, "Restocking did not commit bakery goods.");
            var saved = productSave.BuildShelfSaveData();
            shelf.ClearStock(); productSave.ApplyShelfSaveData(saved);
            Require(shelf.CurrentProduct == product && shelf.CurrentAmount == 10, "Saved bakery stock failed to restore.");
            yield return null;
            Physics.SyncTransforms(); starter.building.NotifyLayoutChanged(); store.Advance(.05f); store.Advance(.05f);
            store.Balance.thiefChance = 0; store.SetOpen(true);
            Require(store.TrySpawnCustomer(), "Shopper cannot reach bakery rack: " + store.Notice);
            var visit = store.Visits.Single(); store.SetOpen(false);
            Require(visit.Product == product, "Shopper selected the wrong product.");
            for (int frame = 0; frame < 2000 && visit.Stage != VisitStage.Waiting && !visit.IsFinished; frame++)
            { store.Advance(.05f); if (frame % 20 == 0) yield return null; }
            Require(visit.Stage == VisitStage.Waiting, "Shopper did not browse and queue for " + key);
            int wallet = store.Money; int amount = shelf.CurrentAmount;
            Require(store.ClaimCheckout() == visit && store.CompleteCheckout(visit), "Bakery checkout failed.");
            Require(store.Money == wallet + visit.Quantity * product.SellPrice && shelf.CurrentAmount == amount - visit.Quantity, "Bakery checkout quantities/value differ.");
            Require(!store.CompleteCheckout(visit), "Bakery purchase paid twice.");
            for (int frame = 0; frame < 2000 && !visit.IsFinished; frame++) { store.Advance(.05f); if (frame % 20 == 0) yield return null; }
            Require(visit.IsFinished, "Bakery shopper could not leave.");
            log.AppendLine("PASS " + product.Id + ": " + supplier + "; real rack; five-gesture restock; cancel without loss; saved stock; shopper route/browse/queue; correct checkout once; exit.");
        }
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Pending, false); Time.timeScale = 1; checks = null;
        File.WriteAllText(BakeryExpansionBatch.Report + "/play.txt", error == null
            ? $"PASS full {StoreAssortmentPack.Load().Products.Count}-product delivery/save/stock regression, four existing fixtures and six real bakery restocking/shopper checkout cycles. Player saves untouched.\n"
            : "FAIL " + error + "\n" + trace);
        EditorApplication.isPlaying = false; EditorApplication.Exit(error == null ? 0 : 1);
    }
    private static void Require(bool condition, string message) => BakeryExpansionBatch.Require(condition, message);
}
