using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Edits the existing instance: preview/cancel never mutate its pose, stock or occupancy.</summary>
    [RequireComponent(typeof(BuildController))]
    public sealed class BuildEditingController : MonoBehaviour
    {
        public BuildController building;
        public GameplayControls controls;
        public DeliveryOrders deliveries;
        public ProductAssignMode productAssignment;
        public WorkMinigame work;
        public PlacedObject Selected { get; private set; }
        public bool IsActive => building != null && building.mode == BuildMode.Edit;
        [NonSerialized] public bool ReadPointerInput = true;
        public string Notice { get; private set; }
        public PlacementResult CandidateResult { get; private set; }
        private BuildItemData placementDefinition;
        private Vector3Int candidateCell;
        private int candidateFacing;
        private bool hasCandidate;
        private Renderer[] selectedRenderers = Array.Empty<Renderer>();
        private bool[] hiddenStates = Array.Empty<bool>();

        public void Begin()
        {
            if (work != null && work.IsActive) work.Cancel();
            productAssignment?.Cancel();
            controls?.CloseWindows(false);
            CancelSelection();
            building.ExitBuildMode();
            building.mode = BuildMode.Edit;
            building.gridOverlay?.Show();
            Notice = "Click an installed object to select it.";
        }
        public bool Select(PlacedObject placed)
        {
            if (!IsActive || placed == null || placed.item == null || placed.GetComponentInParent<BuildPreview>() != null) return false;
            CancelSelection();
            Selected = placed;
            placementDefinition = Instantiate(placed.item);
            placementDefinition.name = placed.item.name + " edit geometry";
            if (placed.item.isWall && placed.wallModuleVersion < BuildItemData.CurrentWallModuleVersion)
            {
                placementDefinition.footprint = placed.item.WallFootprint(placed.wallModuleVersion);
                placementDefinition.pivotOffset = placed.item.WallPivot(placed.wallModuleVersion);
                placementDefinition.prefab = placed.item.WallPrefab(placed.wallModuleVersion);
                placementDefinition.placementAlignmentOffset = placed.wallModuleVersion==2 ? placed.item.previousWallAlignmentOffset : Vector3.zero;
                var sourceScale=placementDefinition.prefab.transform.localScale;
                var scale = new Vector3(placed.transform.localScale.x/sourceScale.x,placed.transform.localScale.y/sourceScale.y,placed.transform.localScale.z/sourceScale.z);
                var bounds = placed.item.WallBounds(placed.wallModuleVersion);
                placementDefinition.placementBounds = new Bounds(Vector3.Scale(bounds.center, scale), Vector3.Scale(bounds.size, scale));
            }
            candidateCell = placed.anchorCell;
            candidateFacing = placed.facing;
            selectedRenderers = placed.GetComponentsInChildren<Renderer>();
            hiddenStates = selectedRenderers.Select(r => r.forceRenderingOff).ToArray();
            foreach (var renderer in selectedRenderers) renderer.forceRenderingOff = true;
            building.preview.SetItem(placementDefinition);
            building.preview.SetModelScale(placed.transform.localScale);
            PreviewAt(candidateCell);
            Notice = "Click a new position to move this object. Esc cancels selection.";
            return true;
        }

        public PlacementResult PreviewAt(Vector3Int cell)
        {
            if (Selected == null) return PlacementResult.Fail(PlaceFailReason.RuleFailed, "Select an object first.");
            candidateCell = cell;
            hasCandidate = true;
            var request = Request();
            CandidateResult = building.ValidateExistingPlacement(request);
            building.preview.SetPose(BuildPlacementPose.Position(building.grid, placementDefinition, cell, request.rotated, candidateFacing), Quaternion.Euler(0, candidateFacing * 90, 0));
            building.preview.ShowPlacement(building.grid, request, CandidateResult);
            return CandidateResult;
        }

        private PlacementRequest Request() => new PlacementRequest(placementDefinition, candidateCell, candidateFacing % 2 != 0, candidateFacing, Selected);

        public void Rotate(int quarterTurns)
        {
            if (Selected == null || !Selected.item.allowRotation) return;
            candidateFacing = ((candidateFacing + quarterTurns) % 4 + 4) % 4;
            PreviewAt(candidateCell);
        }

        public bool Confirm()
        {
            if (!IsActive || Selected == null || !hasCandidate) return false;
            var request = Request();
            CandidateResult = building.ValidateExistingPlacement(request);
            if (!CandidateResult.ok) { Notice = BuildPreview.FailureText(CandidateResult); return false; }
            var cells = StoreyLink.OccupiedCells(building.grid, placementDefinition, candidateCell, request.rotated).ToList();
            building.grid.Release(Selected.occupiedCells);
            if(Selected.item.isParkingSpace && Selected.parkingModuleVersion<Selected.item.parkingGeometryVersion)
                Selected.GetComponent<RetailEmpireTycoon.Parking.ParkingBay>()?.Resize(Selected.item.placementBounds.size.x,Selected.item.placementBounds.size.z,Selected.item.previousParkingBounds);
            Selected.transform.SetPositionAndRotation(BuildPlacementPose.Position(building.grid, placementDefinition, candidateCell, request.rotated, candidateFacing), Quaternion.Euler(0, candidateFacing * 90, 0));
            Selected.anchorCell = candidateCell;
            Selected.facing = candidateFacing;
            Selected.rotated = request.rotated;
            Selected.occupiedCells = cells;
            if(Selected.item.isParkingSpace)Selected.parkingModuleVersion=Selected.item.parkingGeometryVersion;
            building.grid.Occupy(cells);
            building.NotifyLayoutChanged();
            CancelSelection();
            Notice = "Changes applied. Select another object.";
            return true;
        }

        public bool DeleteSelected()
        {
            if (!IsActive || Selected == null) return false;
            var parkingTraffic=FindObjectOfType<RetailEmpireTycoon.Parking.ParkingTraffic>();
            if(Selected.item.isParkingSpace && parkingTraffic!=null && !parkingTraffic.CanEdit(Selected.GetComponent<RetailEmpireTycoon.Parking.ParkingBay>()))
            {Notice="Wait until this parking bay is free.";return false;}
            if (Selected.playerParking)
            { Notice = "Personal parking can be moved or rotated, but not removed: it is the pickup return point."; return false; }
            if (Selected.item.isUnloadingGate && FindObjectsOfType<PlacedObject>().Count(p => p.item != null && p.item.isUnloadingGate) <= 1)
            { Notice = "The last unloading gate is required. You can move it instead."; return false; }
            var shelf = Selected.GetComponent<PlacedShelfStock>();
            float stockWeight = shelf != null && shelf.AssignedProduct != null ? shelf.CurrentAmount * shelf.AssignedProduct.UnitWeightKg : 0;
            if (deliveries != null && deliveries.ShopWeightKg + stockWeight + Selected.item.weightKg > deliveries.shopCapacityKg)
            { Notice = "There is not enough warehouse space for this item and its stock."; return false; }
            if (shelf != null && shelf.AssignedProduct != null && shelf.CurrentAmount > 0)
            {
                if (deliveries == null) { Notice = "The warehouse is unavailable. Goods cannot be safely returned."; return false; }
                deliveries.products.Add(shelf.AssignedProduct, shelf.CurrentAmount);
                shelf.ClearStock();
            }
            var removed = Selected;
            building.inventory.Add(removed.item, 1);
            building.grid.Release(removed.occupiedCells);
            CancelSelection();
            removed.gameObject.SetActive(false);
            Destroy(removed.gameObject);
            building.NotifyLayoutChanged();
            Notice = "The item and remaining goods were returned to storage.";
            return true;
        }

        public void CancelSelection()
        {
            for (int i = 0; i < selectedRenderers.Length; i++)
                if (selectedRenderers[i] != null) selectedRenderers[i].forceRenderingOff = hiddenStates[i];
            selectedRenderers = Array.Empty<Renderer>();
            hiddenStates = Array.Empty<bool>();
            Selected = null;
            if (placementDefinition != null) Destroy(placementDefinition);
            placementDefinition = null;
            hasCandidate = false;
            building?.preview?.Clear();
            Notice = "Click an installed object to select it.";
        }

        public void Finish()
        {
            if (!IsActive && Selected == null) return;
            CancelSelection();
            building.ExitBuildMode();
        }

        /// <summary>Uses the same picking as a real edit-mode click. Flat parking paint is picked by its occupied cells.</summary>
        public bool SelectAtScreenPoint(Vector2 screenPoint)
        {
            if(!IsActive || building.worldCamera==null)return false;
            var ray=building.worldCamera.ScreenPointToRay(screenPoint);
            int floor=building.Storeys.ActiveFloor;
            var plane=new Plane(Vector3.up,Vector3.up*building.grid.FloorElevation(floor));
            bool onPlane=plane.Raycast(ray,out float distance);
            var cell=onPlane?building.grid.WorldToCell(ray.GetPoint(distance)):default;cell.y=floor;
            var placedObjects=FindObjectsOfType<PlacedObject>().Where(placed=>placed.anchorCell.y==floor
                && placed.GetComponentInParent<BuildPreview>()==null).ToArray();
            // Parking paint has no solid collider. A wall farther along the ray must not steal its click.
            var parking=onPlane?placedObjects.FirstOrDefault(placed=>placed.item!=null && placed.item.isParkingSpace && placed.occupiedCells.Contains(cell)):null;
            if(parking!=null)return Select(parking);
            foreach(var hit in Physics.RaycastAll(ray,1000).OrderBy(hit=>hit.distance))
            {
                var placed=hit.collider.GetComponentInParent<PlacedObject>();
                if(placed!=null && placedObjects.Contains(placed))return Select(placed);
            }
            return onPlane && Select(placedObjects.FirstOrDefault(placed=>placed.occupiedCells.Contains(cell)));
        }

        private void Update()
        {
            if (!IsActive || !ReadPointerInput) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Selected != null) { CancelSelection(); Notice = "Selection cancelled."; }
                else Finish();
                return;
            }
            if (Selected != null)
            {
                if (controls.Pressed(ShopAction.RotateClockwise)) Rotate(1);
                if (controls.Pressed(ShopAction.RotateCounterclockwise)) Rotate(-1);
                if (Input.GetKeyDown(KeyCode.Delete)) { DeleteSelected(); return; }
            }
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            var ray = building.worldCamera.ScreenPointToRay(Input.mousePosition);
            if (Selected == null)
            {
                if (!Input.GetMouseButtonDown(0)) return;
                SelectAtScreenPoint(Input.mousePosition);
                return;
            }
            var plane = new Plane(Vector3.up, Vector3.up * building.grid.FloorElevation(building.Storeys.ActiveFloor));
            if (!plane.Raycast(ray, out float distance)) return;
            PreviewAt(building.grid.WorldToCell(ray.GetPoint(distance)));
            if (Input.GetMouseButtonDown(0)) Confirm();
        }

        private void OnDisable() => Finish();
    }
}
