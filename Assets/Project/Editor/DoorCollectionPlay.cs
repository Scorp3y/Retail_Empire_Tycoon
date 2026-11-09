using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static class DoorCollectionPlay
{
    public const string Report = "Library/DoorCollectionQA";

    public static IEnumerable<object> Verify(StarterStoreInitialization starter, Action<string> capture)
    {
        var styles = DoorCollectionDesigns.All;
        var items = styles.Select(style => starter.catalog.GetById(style.Id)).ToArray();
        Require(Resources.LoadAll<BuildItemData>("DoorCollection").Length == 24 && items.All(item => item != null),
            "The catalog must contain all 24 requested doors.");
        var controls = Object.FindObjectOfType<GameplayControls>();
        var shop = Object.FindObjectOfType<ShopWindow>(true);
        controls.RequestShop();
        shop.OpenCategory(BuildCategory.Structures);
        yield return null;
        shop.GetComponentsInChildren<Button>().Single(button => button.name == "Doors & Gates").onClick.Invoke();
        yield return null;
        var cards = shop.GetComponentsInChildren<ShopItemCard>();
        Require(items.All(item => cards.Any(card => card.nameText.text == item.displayName && card.icon.sprite == item.icon &&
            item.icon != null && card.buyButton.interactable)), "A new door is missing its purchasable card or icon.");
        capture("DoorStore");
        shop.GetComponentsInChildren<Button>().Single(button => button.name == "Revolving").onClick.Invoke();
        yield return null;
        Require(shop.GetComponentsInChildren<ShopItemCard>().Length == 2, "The revolving-door filter contains the wrong entries.");
        controls.HandleEscape();
        yield return null;

        var build = starter.building;
        var orders = starter.cityTrip.orders;
        int budget = orders.money.Money;
        orders.money.SetMoney(0);
        Require(!orders.Order(items[0]), "An unaffordable door order was accepted.");
        orders.money.SetMoney(budget);
        var testPlot = new Vector3Int(9600, 0, 9600);
        build.territory.AddPurchasedRect(testPlot, testPlot + new Vector3Int(270, 0, 380));
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            int stock = build.inventory.GetCount(item);
            Require(orders.Order(item) && orders.money.Money == budget - items.Take(i + 1).Sum(door => door.price),
                "Wrong order charge for " + item.id);
            var package = orders.Ledger.Entries.Single(entry => entry.itemId == item.id && entry.location == DeliveryLocation.Depot);
            Require(orders.LoadFrom(package, SupplierKind.Construction) && orders.Unload() &&
                build.inventory.GetCount(item) == stock + 1, "Cannot deliver " + item.id);
            build.EnterBuildMode(item);
            build.RotateSelected(i % 4);
            var anchor = testPlot + new Vector3Int(10 + i % 4 * 58, 0, 20 + i / 4 * 58);
            var check = build.CanPlaceAt(anchor);
            Require(check.ok && build.TryPlaceAt(anchor), $"Cannot install {item.id} at {anchor}: {check.message}");
            build.ExitBuildMode();
        }
        var saved = build.BuildPlacedSaveData();
        var roundTrip = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(new GameData { placedObjects = saved }));
        build.ApplyPlacedSaveData(roundTrip.placedObjects, starter.catalog);
        yield return null;
        var doors = Object.FindObjectsOfType<PlacedObject>().Where(placed => items.Contains(placed.item))
            .OrderBy(placed => placed.item.id, StringComparer.Ordinal).ToArray();
        Require(doors.Length == 24 && doors.Where((door, i) => door.facing != i % 4).Count() == 0, "Door position or facing was lost.");
        foreach (var step in VerifyFrameFinishes(starter, doors[0])) yield return step;
        doors = Object.FindObjectsOfType<PlacedObject>().Where(placed => items.Contains(placed.item))
            .OrderBy(placed => placed.item.id, StringComparer.Ordinal).ToArray();
        Require(saved.Where(placed => placed.itemId is "door_01" or "unloading_gate_01").All(placed =>
            build.BuildPlacedSaveData().Any(entry => entry.itemId == placed.itemId && entry.x == placed.x && entry.z == placed.z)),
            "A legacy opening moved or disappeared.");
        var installedEntrance = doors.First(placed => placed.item.id == "Door_17_AutoDouble");
        CaptureInstalled(starter, installedEntrance.item, capture);

        var navigation = new StoreNavigation(build.grid, build.territory);
        navigation.RefreshDoorways(Object.FindObjectsOfType<PlacedObject>());
        var sectional = doors.First(placed => placed.item.id == "Door_28_Sectional");
        var overhead = build.grid.WorldToCell(sectional.transform.position) + Vector3Int.up;
        Require(!starter.floors.AreCellsValid(new() { overhead }, starter.catalog.GetById("floor_cream")),
            "The construction brush accepted a ceiling inside the gate travel.");
        foreach (var installed in doors)
        {
            VerifyPedestrian(installed, navigation);
            yield return null;
        }
        foreach (var step in VerifyVehicles(doors.Where(placed => placed.GetComponent<CollectionDoor>().AllowsVehicles)))
            yield return step;

        var editor = build.GetComponent<BuildEditingController>();
        editor.Begin();
        Require(editor.Select(doors[0]), "Could not select the installed door for moving.");
        editor.Rotate(1);
        Require(editor.PreviewAt(doors[0].anchorCell + Vector3Int.right * 9).ok && editor.Confirm(),
            "Could not move and rotate an installed door.");
        int stockBeforeRemoval = build.inventory.GetCount(items[0]);
        Require(editor.Select(doors[0]) && editor.DeleteSelected() && build.inventory.GetCount(items[0]) == stockBeforeRemoval + 1,
            "The moved door was not returned to storage.");
        editor.Finish();

        Debug.Log("Door Collection QA passed: 24 cards, 24 paid and delivered placements, saved facings, move/delete, pedestrian routes and ten vehicle passes.");
    }

    private static IEnumerable<object> VerifyFrameFinishes(StarterStoreInitialization starter, PlacedObject oakDoor)
    {
        var build = starter.building;
        var door = oakDoor.GetComponent<DoorFrameFinish>();
        var brick = starter.catalog.GetById("wall_finish_13");
        var plaster = starter.catalog.GetById("wall_finish_02");
        Require(door != null && door.Choice == DoorFinish.Auto && door.Resolved == DoorFinish.Auto,
            "A freestanding door must retain the authored frame under Auto.");
        var doorAnchor = oakDoor.anchorCell;
        var oakLeaf = oakDoor.transform.Find("Leaf_L").GetComponentInChildren<MeshRenderer>();
        var leafMaterials = oakLeaf.sharedMaterials;
        PlacedObject BuildWall(BuildItemData item, Vector3Int at)
        {
            build.inventory.Add(item, 1);
            build.EnterBuildMode(item);
            Require(build.TryPlaceAt(at), "Could not install a neighboring wall for the frame test.");
            build.ExitBuildMode();
            return Object.FindObjectsOfType<PlacedObject>().Single(placed => placed.item == item && placed.anchorCell == at);
        }
        var left = BuildWall(brick, doorAnchor + Vector3Int.left * 2);
        Require(door.Resolved == DoorFinish.Graphite, "Brick did not select graphite for the frame.");
        var right = BuildWall(plaster, doorAnchor + Vector3Int.right * oakDoor.item.footprint.x);
        Require(door.Resolved == DoorFinish.Graphite, "Mixed neighbors must fall back to graphite.");
        var editor = build.GetComponent<BuildEditingController>();
        editor.Begin();
        Require(editor.Select(left) && editor.DeleteSelected(), "Could not remove the neighboring brick.");
        Require(door.Resolved == DoorFinish.Cream, "Removing brick did not update the frame to match plaster.");

        var hud = Object.FindObjectOfType<BuildEditorHud>();
        hud.Open();
        hud.OpenEditing();
        Require(editor.Select(oakDoor), "Could not select the door for frame editing.");
        yield return null;
        hud.uiRoot.GetComponentsInChildren<Button>().Single(button => button.name == "Door frame finish").onClick.Invoke();
        hud.uiRoot.GetComponentsInChildren<Button>().Single(button => button.name == "Frame Bronze").onClick.Invoke();
        var bronze = AssetDatabase.LoadAssetAtPath<Material>(DoorCollectionBatch.Root + "/Materials/FrameBronze.mat");
        Require(door.Choice == DoorFinish.Bronze && door.Resolved == DoorFinish.Bronze &&
            oakDoor.transform.Find("Frame").GetComponentsInChildren<MeshRenderer>().SelectMany(renderer => renderer.sharedMaterials)
                .Contains(bronze) && oakLeaf.sharedMaterials.SequenceEqual(leafMaterials),
            "The menu must recolor the frame while leaving the leaf materials intact.");
        hud.Close();
        editor.Begin();
        Require(editor.Select(right) && editor.DeleteSelected(), "Could not remove the neighboring plaster.");
        editor.Finish();
        Require(door.Resolved == DoorFinish.Bronze, "A wall change must preserve a manual finish.");

        var snapshot = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(new GameData { placedObjects = build.BuildPlacedSaveData() }));
        build.ApplyPlacedSaveData(snapshot.placedObjects, starter.catalog);
        yield return null;
        var restored = Object.FindObjectsOfType<PlacedObject>().Single(placed => placed.item.id == "Door_01_OakSwing").GetComponent<DoorFrameFinish>();
        Require(restored.Choice == DoorFinish.Bronze && restored.Resolved == DoorFinish.Bronze,
            "The manual frame finish was lost on save/load.");
        Require(JsonUtility.FromJson<PlacedBuildSaveData>("{\"itemId\":\"door_01\"}").doorFrameFinish == DoorFinish.Auto,
            "Legacy saves must default to Auto.");
    }

    private static void CaptureInstalled(StarterStoreInitialization starter, BuildItemData item, Action<string> capture)
    {
        var build = starter.building;
        var candidates = build.territory.PurchasedRects.First();
        var cells = Enumerable.Range(candidates.min.x, candidates.max.x - candidates.min.x + 1)
            .SelectMany(x => Enumerable.Range(candidates.min.z, candidates.max.z - candidates.min.z + 1)
                .Select(z => new Vector3Int(x, 0, z)))
            .OrderBy(cell => (build.grid.CellToWorld(cell) - starter.blueprint.cameraFocus).sqrMagnitude);
        build.inventory.Add(item, 1);
        build.EnterBuildMode(item);
        foreach (var cell in cells)
        {
            if (!build.TryPlaceAt(cell)) continue;
            var placed = Object.FindObjectsOfType<PlacedObject>().Single(entry => entry.item == item && entry.anchorCell == cell);
            var target = placed.transform.position + Vector3.up * .65f;
            var camera = Camera.main;
            var originalPosition = camera.transform.position;
            var originalRotation = camera.transform.rotation;
            camera.transform.position = target + placed.transform.forward * 4 + Vector3.up * 2.3f + Vector3.left * .9f;
            camera.transform.LookAt(target);
            capture("DoorInstalled");
            camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
            break;
        }
        build.ExitBuildMode();
    }

    private static void VerifyPedestrian(PlacedObject placed, StoreNavigation navigation)
    {
        var door = placed.GetComponent<CollectionDoor>();
        var rotor = door.transform.Find("Rotor");
        var leaf = FirstMovingPart(door);
        var closedPosition = leaf.localPosition;
        var closedRotation = leaf.localRotation;
        var barrier = door.GetComponentsInChildren<BoxCollider>().First(collider => door.IsBarrier(collider));
        Vector3 middle = barrier.transform.TransformPoint(barrier.center);
        middle.y = placed.transform.position.y + .025f;
        var normal = barrier.transform.forward;
        var start = middle + normal * 1.27f;
        var end = middle - normal * (door.Mechanism == DoorMechanism.Revolve ? 3.05f : 1.27f);
        var avatar = new GameObject("Door route check");
        try
        {
            avatar.transform.position = start;
            var walker = avatar.AddComponent<ShopCharacter>();
            walker.Initialize(navigation, 1.45f);
            Physics.SyncTransforms();
            Require(walker.MoveTo(end), "No route through " + placed.item.id);
            bool opened = false, animated = false;
            for (int step = 0; step < 140 && !walker.Arrived; step++)
            {
                walker.Advance(.045f);
                door.Advance(.045f);
                opened |= door.IsOpen;
                animated |= PoseChanged(leaf, closedPosition, closedRotation);
            }
            Require(opened && animated && walker.Arrived, "Shopper did not animate, open and cross " + placed.item.id);
            avatar.transform.position = end + normal * -3f;
            for (int step = 0; step < 90; step++) door.Advance(.05f);
            Require(door.IsClosed && barrier.enabled, "Door failed to close: " + placed.item.id);
            if (rotor == null)
                Require(!PoseChanged(leaf, closedPosition, closedRotation), "The leaf did not return to its closed pose: " + placed.item.id);

            // The physical actor path tests the BoxCollider zones independently of StoreNavigation.
            var controller = avatar.AddComponent<CharacterController>();
            controller.radius = .10f; controller.height = .95f; controller.center = Vector3.up * .475f;
            controller.enabled = false;
            avatar.transform.position = start;
            controller.enabled = true;
            controller.Move(-normal * .002f);
            Physics.SyncTransforms();
            for (int step = 0; step < 45; step++) door.Advance(.05f);
            Require(door.IsOpen && !barrier.enabled, "Physical actor did not trigger " + placed.item.id);
            void PositionActor(Vector3 position)
            {
                controller.enabled = false;
                avatar.transform.position = position;
                controller.enabled = true;
                controller.Move(Vector3.zero);
                Physics.SyncTransforms();
            }
            PositionActor(middle);
            var rotorBefore = rotor != null ? rotor.localRotation : Quaternion.identity;
            for (int step = 0; step < 50; step++) door.Advance(.05f);
            Require(door.IsOpen, "The door closed while a physical actor occupied the aperture.");
            if (rotor != null)
                Require(Quaternion.Angle(rotorBefore, rotor.localRotation) > 12, "A revolving door stopped with an actor in the opening.");
            PositionActor(start);
            for (int step = 0; step < 65 && Vector3.Dot(avatar.transform.position - end, normal) > .07f; step++)
            {
                controller.Move(-normal * .073f);
                door.Advance(.03f);
            }
            Require(Vector3.Dot(avatar.transform.position - end, normal) < .15f,
                $"Physical actor blocked at {placed.item.id}: distance to target {Vector3.Dot(avatar.transform.position - end, normal):0.00}, open {door.IsOpen}.");
            PositionActor(end - normal * 5);
            for (int step = 0; step < 90; step++) door.Advance(.05f);
            if (rotor != null)
            {
                var stopped = rotor.localRotation;
                door.Advance(.15f);
                Require(Quaternion.Angle(stopped, rotor.localRotation) < .01f, "A revolving door failed to brake after departure.");
            }
        }
        finally { Object.Destroy(avatar); }
    }

    private static Transform FirstMovingPart(CollectionDoor door) => (Transform)new SerializedObject(door)
        .FindProperty("parts").GetArrayElementAtIndex(0).FindPropertyRelative("node").objectReferenceValue;

    private static bool PoseChanged(Transform leaf, Vector3 position, Quaternion rotation) =>
        Vector3.Distance(leaf.localPosition, position) > .001f || Quaternion.Angle(leaf.localRotation, rotation) > .1f;

    private static IEnumerable<object> VerifyVehicles(IEnumerable<PlacedObject> gates)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/City/Pickup.prefab");
        var heavy = AssetDatabase.LoadAssetAtPath<PlayerVehicleDefinition>("Assets/Resources/PlayerVehicles/heavy_box_truck.asset");
        var tick = typeof(PickupDrive).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var simulation = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        try
        {
            foreach (var installed in gates)
            {
                var origin = new Vector3(1300, 12, 1300);
                var gate = Object.Instantiate(installed.item.prefab, origin, Quaternion.identity).GetComponent<CollectionDoor>();
                gate.enabled = false;
                var style = DoorCollectionDesigns.All.Single(entry => entry.Id == installed.item.id);
                if (style.CompactGate)
                {
                    var opening = gate.GetComponentsInChildren<BoxCollider>().Single(collider => gate.IsBarrier(collider));
                    Require(installed.item.footprint.x == 14 && Mathf.Abs(opening.size.x - 3.6f) < .01f &&
                        Mathf.Abs(opening.size.y - 4f) < .01f, "Wrong compact gate dimensions: " + style.Id);
                }
                var leaf = FirstMovingPart(gate);
                var closedPosition = leaf.localPosition;
                var closedRotation = leaf.localRotation;
                var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
                road.transform.position = origin + Vector3.down * .25f;
                road.transform.localScale = new Vector3(12, .5f, 45);
                try
                {
                    foreach (var vehicle in new PlayerVehicleDefinition[] { null, heavy })
                    {
                        var drive = Object.Instantiate(prefab, origin + new Vector3(0, .1f, -8.5f), Quaternion.identity).GetComponent<PickupDrive>();
                        drive.enabled = false;
                        drive.ReadKeyboard = false;
                        drive.SetVehicle(vehicle);
                        Physics.SyncTransforms();
                        for (int step = 0; step < 85; step++) { tick.Invoke(drive, null); Physics.Simulate(.02f); }
                        bool opened = false, animated = false;
                        drive.SetInput(.17f, 0, false);
                        for (int step = 0; step < 770 && drive.Body.position.z < origin.z + 8.6f; step++)
                        {
                            gate.Advance(.02f);
                            tick.Invoke(drive, null);
                            Physics.Simulate(.02f);
                            opened |= gate.IsOpen;
                            animated |= PoseChanged(leaf, closedPosition, closedRotation);
                        }
                        Require(opened && animated && drive.Body.position.z >= origin.z + 8.6f,
                            $"{vehicle?.id ?? "pickup"} did not pass {installed.item.id}: z={drive.Body.position.z - origin.z:0.00}");
                        drive.gameObject.SetActive(false);
                        Object.Destroy(drive.gameObject);
                        for (int step = 0; step < 90; step++) gate.Advance(.05f);
                        Require(gate.IsClosed && !PoseChanged(leaf, closedPosition, closedRotation),
                            "Vehicle gate did not return to its closed pose: " + style.Id);
                        yield return null;
                    }
                }
                finally
                {
                    gate.gameObject.SetActive(false); road.SetActive(false);
                    Object.Destroy(gate.gameObject); Object.Destroy(road);
                }
            }
        }
        finally { Physics.simulationMode = simulation; }
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
