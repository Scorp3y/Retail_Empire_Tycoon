using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Retains the earlier assortment/bakery tests, then exercises each new fruit on the real produce rack.</summary>
[InitializeOnLoad]
public static class ProduceExpansionPlay
{
    private const string Pending = "ProduceExpansion.Play";
    private static IEnumerator checks;
    private static double deadline;
    private static readonly StringBuilder trace = new StringBuilder();
    static ProduceExpansionPlay() { EditorApplication.update += Tick; }
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();
        foreach (string path in new[] { ProduceExpansionBatch.Report, BakeryExpansionBatch.Report, AssortmentUpdateBatch.Report }) Directory.CreateDirectory(path);
        File.WriteAllText(ProduceExpansionBatch.Report + "/play.txt", "RUNNING\n");
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
            if (checks == null) { checks = Check(); deadline = EditorApplication.timeSinceStartup + 900; }
            AssortmentUpdateBatch.Require(EditorApplication.timeSinceStartup < deadline, "Produce checks timed out.");
            if (checks.MoveNext()) return; ProduceExpansionBatch.Record(); Finish(null);
        }
        catch (Exception error) { Debug.LogException(error); Finish(error); }
    }
    private static IEnumerator Check()
    {
        var previous = BakeryExpansionPlay.Check(); while (previous.MoveNext()) yield return previous.Current;
        var pack = StoreAssortmentPack.Load();
        AssortmentUpdateBatch.Require(pack.Products.Count >= 60 && ProduceExpansionBatch.Keys.All(key => pack.Products.Any(p => p.Id == "product_" + key)), "New produce is missing.");
        var produce = BakeryExpansionPlay.CheckShelfProducts(ProduceExpansionBatch.Keys, "shelf_produce_01", SupplierKind.Farmers, trace);
        while (produce.MoveNext()) yield return produce.Current;
        File.WriteAllText(ProduceExpansionBatch.Report + "/produce.txt", trace.ToString());
        File.Copy(AssortmentUpdateBatch.Report + "/details.txt", ProduceExpansionBatch.Report + "/assortment.txt", true);
        File.Copy(BakeryExpansionBatch.Report + "/bakery.txt", ProduceExpansionBatch.Report + "/bakery.txt", true);
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Pending, false); Time.timeScale = 1; checks = null;
        File.WriteAllText(ProduceExpansionBatch.Report + "/play.txt", error == null
            ? "PASS 60-product order/delivery/warehouse/save/stock regression; four fixtures; six bakery and six produce player-restocking/real-shopper checkout cycles. Player saves untouched.\n"
            : "FAIL " + error + "\n" + trace);
        EditorApplication.isPlaying = false; EditorApplication.Exit(error == null ? 0 : 1);
    }
}
