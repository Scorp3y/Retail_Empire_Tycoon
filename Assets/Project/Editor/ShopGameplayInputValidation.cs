using System;
using System.Collections;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Verifies the real UI event route and scaled drop target, after camera interpolation finishes.</summary>
public static class ShopGameplayInputValidation
{
    [MenuItem("Retail Empire/Shop/Validate work UI input %#F10")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != ShopGameplaySandbox.Path)
            throw new InvalidOperationException("Requires sandbox Play Mode.");
        var work = UnityEngine.Object.FindObjectOfType<WorkMinigame>();
        if (work.IsActive) work.Cancel();
        work.StartCoroutine(Check(work));
    }
    private static IEnumerator Check(WorkMinigame work)
    {
        var warehouse = UnityEngine.Object.FindObjectOfType<ProductInventory>();
        var product = AssetDatabase.LoadAssetAtPath<ProductItemData>("Assets/Prefabs/Product/SodaCola_Product.asset");
        var shelf = UnityEngine.Object.FindObjectsOfType<PlacedShelfStock>().First(s => s.IsProductAllowed(product));
        var warehouseSnapshot = warehouse.BuildSaveData(); var assigned = shelf.AssignedProduct; int amount = shelf.CurrentAmount;
        shelf.ClearStock(); warehouse.Add(product, 5);
        if (!work.BeginRestock(shelf, product)) throw new InvalidOperationException("Cannot start UI input check.");
        yield return new WaitForSecondsRealtime(0.7f);
        string report;
        try
        {
            var tray = GameObject.Find("Product tray"); var target = GameObject.Find("Work target 0");
            var source = (RectTransform)tray.transform;
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = source.TransformPoint(source.rect.center) };
            var rays = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, rays);
            if (rays.Count == 0 || rays[0].gameObject != tray) throw new InvalidOperationException("Product tray is not the top UI hit.");
            ExecuteEvents.Execute(tray, data, ExecuteEvents.beginDragHandler);
            data.position = target.transform.position;
            ExecuteEvents.Execute(tray, data, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(tray, data, ExecuteEvents.endDragHandler);
            if (work.CompletedSteps != 1 || !shelf.IsEmpty) throw new InvalidOperationException("Real drag event route did not complete exactly one uncommitted placement.");
            report = "PASS topmost tray raycast, begin/drag/end UI events, scaled target hit, one step only, no early warehouse transfer. Screen " + Screen.width + "x" + Screen.height;
        }
        catch (Exception error) { report = "FAIL " + error; Debug.LogException(error); }
        finally
        {
            work.Cancel(); shelf.SetStockFromSave(assigned, amount);
            warehouse.ApplySaveData(warehouseSnapshot, UnityEngine.Object.FindObjectOfType<ProductCatalog>());
        }
        Directory.CreateDirectory("Library/ShopGameplayQA"); File.WriteAllText("Library/ShopGameplayQA/input.txt", report);
    }
}
