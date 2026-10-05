using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Territory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Behavior checks on isolated instances, without reading or writing the player's save.</summary>
public static class CameraBuildExperienceValidation
{
    [MenuItem("Retail Empire/Building/Validate camera and placement")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Requires Edit Mode.");
        var original = SceneManager.GetActiveScene();
        if (original.isDirty) throw new InvalidOperationException("Save the scene before validation.");
        Directory.CreateDirectory("Library/CameraBuildQA");
        var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(temporary);
        BuildItemData item = null;
        try
        {
            Camera camera = new GameObject("QA camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 8f, -10f);
            camera.transform.LookAt(Vector3.zero);
            camera.pixelRect = new Rect(0f, 0f, 1100f, 850f);
            var input = camera.gameObject.AddComponent<MainCamera>();
            input.SendMessage("Awake");
            Vector3 anchor = new Vector3(2f, 0f, 1f);
            Vector3 before = camera.WorldToScreenPoint(anchor);
            input.ZoomAtScreenPoint(0.25f, before);
            Settle(input);
            Require(Vector2.Distance(before, camera.WorldToScreenPoint(anchor)) < 1f, "Cursor zoom loses its anchor.");
            input.Orbit(90f, 1000f);
            Settle(input);
            Require(camera.transform.position.y >= input.minY, "Camera went below its height limit.");
            input.ToggleTopView();
            Settle(input);
            Require(input.IsTopView && camera.transform.forward.y < -0.99f, "Top view failed.");
            input.Orbit(15f, 0f);
            Settle(input);
            Require(input.IsTopView && camera.transform.forward.y < -0.99f, "Rotating top view changed its tilt.");
            input.ToggleTopView();
            input.Pan(new Vector3(1000f, 0f, -1000f));
            Settle(input);
            Require(input.FocusPoint.x <= input.maxX + 0.01f && input.FocusPoint.z >= input.minZ - 0.01f, "Camera focus escaped bounds.");
            input.ResetView();
            Settle(input);
            Require(Vector3.Distance(camera.transform.position, new Vector3(0f, 8f, -10f)) < 0.01f, "Home did not restore the initial view.");
            input.enabled = false;
            camera.transform.position = new Vector3(3f, 9f, -8f);
            camera.transform.LookAt(new Vector3(3f, 0f, 0f));
            Vector3 restored = camera.transform.position;
            input.SendMessage("OnEnable");
            input.Advance(1f / 60f);
            Require(Vector3.Distance(restored, camera.transform.position) < 0.01f, "Re-enabling input caused a jump.");

            var root = new GameObject("QA building");
            var grid = root.AddComponent<GridSystem>();
            CheckModelAlignment(grid);
            var inventory = root.AddComponent<BuildInventory>();
            var territory = root.AddComponent<TerritoryManager>();
            territory.AddPurchasedRect(new Vector3Int(-3, 0, -3), new Vector3Int(3, 0, 3));
            var preview = new GameObject("QA preview").AddComponent<BuildPreview>();
            CameraBuildExperienceSetup.ConfigurePreview(preview);
            var build = root.AddComponent<BuildController>();
            build.grid = grid;
            build.inventory = inventory;
            build.territory = territory;
            build.preview = preview;
            build.worldCamera = camera;
            build.SendMessage("Awake");
            // Do not let optional lookups touch components in the real Game scene.
            build.gridOverlay = null;
            build.floorPainter = null;
            item = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Shelf/WallGoodsShelf_BuildItem.asset"));
            item.id = "qa_placement_only";
            item.footprint = new Vector2Int(2, 1);
            item.pivotOffset = Vector2Int.zero;
            item.ruleFlags = PlacementRuleFlags.InsidePurchasedArea | PlacementRuleFlags.NoOverlap;
            item.alignModelToFootprint = false; // Synthetic two-cell scenario, not the actual model calibration.
            inventory.Add(item, 2);
            build.EnterBuildMode(item);
            Require(inventory.GetCount(item) == 2, "Ghost consumed inventory.");
            Require(preview.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Ghost has active physics.");
            Require(preview.GetComponentsInChildren<Behaviour>(true).Where(b => b != preview).All(b => !b.enabled), "Ghost runs gameplay behavior.");
            Require(!temporary.GetRootGameObjects().Any(o => o.name == "Placement feedback"), "Removed hint is still created.");
            Require(build.CanPlaceAt(new Vector3Int(4, 0, 0)).reason == PlaceFailReason.NotPurchased, "Unpurchased placement accepted.");
            Require(!build.TryPlaceAt(new Vector3Int(4, 0, 0)) && inventory.GetCount(item) == 2, "Rejected placement consumed stock.");
            int placedCount = 0;
            build.OnPlacedSuccessfully += _ => placedCount++;
            Require(build.TryPlaceAt(Vector3Int.zero), "Valid placement rejected.");
            Require(inventory.GetCount(item) == 1 && placedCount == 1, "Placement did not consume exactly one item.");
            Require(build.CanPlaceAt(Vector3Int.zero).reason == PlaceFailReason.Overlap, "Overlap not detected.");
            Require(!build.TryPlaceAt(Vector3Int.zero) && inventory.GetCount(item) == 1, "Overlap consumed stock.");
            build.RotateSelected(1);
            Vector3Int rotatedCell = new Vector3Int(-2, 0, 0);
            var request = new PlacementRequest(item, rotatedCell, true, 1);
            preview.SetPose(grid.CellToWorld(rotatedCell), Quaternion.Euler(0f, 90f, 0f));
            preview.ShowPlacement(grid, request, build.CanPlaceAt(rotatedCell));
            var footprint = preview.transform.Find("Placement footprint").GetComponent<MeshFilter>().sharedMesh;
            Require(footprint.vertexCount == 8, "Footprint lost rotated cells.");
            Require(build.TryPlaceAt(rotatedCell), "Rotated placement rejected.");
            Require(inventory.GetCount(item) == 0 && placedCount == 2 && build.mode == BuildMode.Normal, "Last item did not end build mode.");
            build.EnterBuildMode(item);
            Require(build.CanPlaceAt(new Vector3Int(0, 0, -2)).reason == PlaceFailReason.InsufficientInventory, "Empty stock accepted.");
            Require(build.BuildPlacedSaveData().Count(d => d.itemId == item.id) == 2, "Preview leaked into placement saves.");
            build.ExitBuildMode();
            CheckWalls();
            File.WriteAllText("Library/CameraBuildQA/validation.txt", "PASS: all 8 shop models fit their calibrated cells at all 4 rotations; front/rear access and pivot offsets; no placement HUD; cursor-anchored zoom, orbit and height limits, top view, focus bounds, Home, pose resynchronization; ghost isolation, footprint, territory and overlap rejection, exact stock consumption, rotation, empty stock, save exclusion; wall cutaway and collider preservation. Edit Mode isolated scene; player save untouched.");
            Debug.Log("Camera/build experience validation: PASS.");
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/CameraBuildQA/validation.txt", "FAIL: " + error);
            Debug.LogException(error);
        }
        finally
        {
            EditorSceneManager.CloseScene(temporary, true);
            SceneManager.SetActiveScene(original);
            if (item != null) UnityEngine.Object.DestroyImmediate(item);
        }
    }

    private static void CheckWalls()
    {
        Vector3 camera = new Vector3(0f, 6f, -6f);
        Require(StoreWallVisibility.ShouldHide(new Bounds(new Vector3(0f, 1f, -2f), new Vector3(4f, 2f, 0.2f)), camera, Vector3.zero), "Near wall remains visible.");
        Require(!StoreWallVisibility.ShouldHide(new Bounds(new Vector3(0f, 1f, 2f), new Vector3(4f, 2f, 0.2f)), camera, Vector3.zero), "Far wall hidden.");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var marker = wall.AddComponent<StoreWallOccluder>();
        marker.SetHidden(true);
        Require(wall.GetComponent<Renderer>().forceRenderingOff && wall.GetComponent<Collider>().enabled, "Cutaway changed physics.");
        marker.SetHidden(false);
        Require(!wall.GetComponent<Renderer>().forceRenderingOff, "Wall was not restored.");
    }

    private static void CheckModelAlignment(GridSystem grid)
    {
        float previousSize = grid.cellSize;
        grid.cellSize = 0.1667f;
        Vector3Int anchor = new Vector3Int(-2, 0, -3);
        foreach (string path in BuildModelAlignmentSetup.ItemPaths)
        {
            var model = AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
            Require(model.alignModelToFootprint, "Uncalibrated item: " + path);
            Bounds measured = BuildModelAlignmentSetup.Measure(model.prefab);
            Require(Vector3.Distance(measured.center, model.placementBounds.center) < 0.0001f && Vector3.Distance(measured.size, model.placementBounds.size) < 0.0001f, "Stale model bounds: " + path);
            for (int facing = 0; facing < 4; facing++)
            {
                bool rotated = facing % 2 != 0;
                var cells = grid.GetFootprintCells(anchor, model.footprint, rotated, model.pivotOffset).ToArray();
                Vector3 minimum = grid.CellToWorld(new Vector3Int(cells.Min(c => c.x), 0, cells.Min(c => c.z))) - new Vector3(grid.cellSize / 2f, 0f, grid.cellSize / 2f);
                Vector3 maximum = grid.CellToWorld(new Vector3Int(cells.Max(c => c.x), 0, cells.Max(c => c.z))) + new Vector3(grid.cellSize / 2f, 0f, grid.cellSize / 2f);
                Vector3 position = BuildPlacementPose.Position(grid, model, anchor, rotated, facing);
                Quaternion rotation = Quaternion.Euler(0f, facing * 90f, 0f);
                for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = position + rotation * (measured.center + Vector3.Scale(measured.extents, new Vector3(x, 0f, z)));
                    Require(corner.x >= minimum.x - 0.0001f && corner.x <= maximum.x + 0.0001f && corner.z >= minimum.z - 0.0001f && corner.z <= maximum.z + 0.0001f, model.name + " outside footprint, facing=" + facing);
                }
            }
        }
        var doubleShelf = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Shelf/DoubleSidedShelfBuildItem.asset"));
        try
        {
            doubleShelf.footprint = new Vector2Int(2, 2);
            doubleShelf.pivotOffset = new Vector2Int(1, 1);
            var rule = new Rule_Accessibility(grid, grid);
            var request = new PlacementRequest(doubleShelf, Vector3Int.zero, false, 0);
            Require(rule.Evaluate(request).ok, "Empty double shelf sides rejected.");
            grid.Occupy(new[] { new Vector3Int(1, 0, 3), new Vector3Int(2, 0, 3) });
            Require(!rule.Evaluate(request).ok, "Double shelf blocked front accepted.");
            grid.ClearAll();
            grid.Occupy(new[] { new Vector3Int(1, 0, 0), new Vector3Int(2, 0, 0) });
            Require(!rule.Evaluate(request).ok, "Double shelf blocked rear accepted.");
            doubleShelf.twoSidedAccess = false;
            Require(rule.Evaluate(request).ok, "Single-sided shelf checked the back instead of its front.");
            doubleShelf.frontFacing = 2;
            Require(!rule.Evaluate(request).ok, "Configured front direction ignored.");
        }
        finally
        {
            grid.ClearAll();
            grid.cellSize = previousSize;
            UnityEngine.Object.DestroyImmediate(doubleShelf);
        }
    }

    private static void Settle(MainCamera camera) { for (int i = 0; i < 120; i++) camera.Advance(1f / 60f); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
