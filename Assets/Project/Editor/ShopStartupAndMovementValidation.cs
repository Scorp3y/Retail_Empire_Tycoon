using System;
using System.IO;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ShopStartupAndMovementValidation
{
    [MenuItem("Retail Empire/Shop/Validate startup and movement in sandbox %#F3")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != ShopGameplaySandbox.Path)
            throw new InvalidOperationException("Requires isolated sandbox Play Mode.");
        var wallet = UnityEngine.Object.FindObjectOfType<MoneyController>();
        var manager = UnityEngine.Object.FindObjectOfType<GameManager>();
        var view = UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.UI.MoneyView>(true);
        var navigation = UnityEngine.Object.FindObjectOfType<StoreOperations>().Navigation;
        var grid = UnityEngine.Object.FindObjectOfType<GridSystem>();
        int original = wallet.Money; bool viewEnabled = view.enabled; bool managerEnabled = manager.enabled;
        var root = new GameObject("Movement QA actors and obstacles");
        try
        {
            wallet.SetMoney(26040);
            Require(manager.moneyText.text == "$26040" && manager.playerMoney == 26040, "Late startup wallet change not reflected.");
            view.enabled = false; manager.enabled = false; wallet.SetMoney(90000);
            manager.enabled = true; view.enabled = true;
            Require(manager.moneyText.text == "$90000" && manager.playerMoney == 90000, "Re-enabled HUD shows stale balance.");
            manager.SpendMoney(300); Require(manager.moneyText.text == "$89700", "Legacy purchase did not update HUD.");
            manager.AddMoney(10300); Require(manager.moneyText.text == "$100000", "Legacy sale did not update HUD.");
            // Separate the geometry probes from the player's layout, but use the real navigation/controller.
            Vector3 start = grid.CellToWorld(new Vector3Int(-1000, 0, -1000)); start.y = StoreNavigation.FloorHeight;
            Vector3 end = start + Vector3.right * grid.cellSize * 12;
            var curb = Cube(root, start + Vector3.right * grid.cellSize * 5 + Vector3.up * .02f, new Vector3(.05f, .04f, .5f));
            Physics.SyncTransforms();
            Require(navigation.CanTraverse(start, end), "Low decorative seam blocks NPC body.");
            UnityEngine.Object.DestroyImmediate(curb);
            var post = Cube(root, (start + end) * .5f + Vector3.up * .24f, new Vector3(.01f, .48f, .01f));
            Physics.SyncTransforms();
            Require(!navigation.CanTraverse(start, end), "Thin full-height solid obstacle is ignored.");
            Require(navigation.TryPath(start, end, out var detour, true), "NPC cannot go around small post.");
            Vector3 previous = start;
            foreach (var point in detour) { Require(navigation.CanTraverse(previous, point), "Detour clips obstacle."); previous = point; }
            UnityEngine.Object.DestroyImmediate(post); Physics.SyncTransforms();
            var a = Actor(root, start, navigation); var b = Actor(root, start, navigation);
            Require(a.MoveTo(end) && b.MoveTo(end), "Open route rejected.");
            for (int i = 0; i < 10; i++) a.Advance(.1f);
            for (int i = 0; i < 60; i++) b.Advance(1f / 60);
            Require(Vector3.Distance(a.transform.position, b.transform.position) < .01f, "Movement speed depends on frame rate.");
            Require(Mathf.Abs(Vector3.Distance(start, a.transform.position) - .65f) < .01f, "Movement drops distance at cell boundaries.");
            var obstruction = Cube(root, a.transform.position + Vector3.up * .24f, new Vector3(.6f, .48f, .6f));
            Physics.SyncTransforms(); a.Advance(.5f); a.Advance(.5f);
            Require(!a.Arrived && a.IsBlocked, "Failed replanning is incorrectly treated as arrival.");
            UnityEngine.Object.DestroyImmediate(obstruction); Physics.SyncTransforms();
            for (int i = 0; i < 60 && !a.Arrived; i++) a.Advance(.1f);
            Require(a.Arrived && !a.IsBlocked, "NPC did not recover after obstacle removal.");
            a.Stop(); Require(!a.HasDestination && a.Arrived, "Stopping leaves an active movement job.");
            Directory.CreateDirectory("Library/ShopGameplayQA");
            File.WriteAllText("Library/ShopGameplayQA/startup-movement.txt", "PASS late startup balance, hidden/re-enabled HUD, legacy money operations; low seam clearance, thin-post detour, equal 10/60 Hz speed, blocked-not-arrived, automatic recovery, explicit stop.");
            Debug.Log("Startup and movement validation passed. Player save untouched.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root); wallet.SetMoney(original); manager.enabled = managerEnabled; view.enabled = viewEnabled;
        }
    }
    private static GameObject Cube(GameObject root, Vector3 position, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.SetParent(root.transform);
        go.transform.position = position; go.transform.localScale = size; return go;
    }
    private static ShopCharacter Actor(GameObject root, Vector3 position, StoreNavigation navigation)
    {
        var go = new GameObject("QA walker"); go.transform.SetParent(root.transform); go.transform.position = position;
        var actor = go.AddComponent<ShopCharacter>(); actor.Initialize(navigation); return actor;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
