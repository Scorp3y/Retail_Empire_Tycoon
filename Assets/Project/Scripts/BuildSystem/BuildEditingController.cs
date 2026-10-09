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
            controls?.CloseWindows();
            CancelSelection();
            building.ExitBuildMode();
            building.mode = BuildMode.Edit;
            building.gridOverlay?.Show();
            Notice = "Выберите установленный объект.";
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
                placementDefinition.placementAlignmentOffset = Vector3.zero;
                var scale = placed.transform.localScale;
                placementDefinition.placementBounds = new Bounds(Vector3.Scale(placed.item.placementBounds.center, scale), Vector3.Scale(placed.item.placementBounds.size, scale));
            }
            candidateCell = placed.anchorCell;
            candidateFacing = placed.facing;
            selectedRenderers = placed.GetComponentsInChildren<Renderer>();
            hiddenStates = selectedRenderers.Select(r => r.forceRenderingOff).ToArray();
            foreach (var renderer in selectedRenderers) renderer.forceRenderingOff = true;
            building.preview.SetItem(placed.item);
            building.preview.SetModelScale(placed.transform.localScale);
            PreviewAt(candidateCell);
            Notice = "Выберите новое место и подтвердите. Esc отменяет выбор.";
            return true;
        }

        public PlacementResult PreviewAt(Vector3Int cell)
        {
            if (Selected == null) return PlacementResult.Fail(PlaceFailReason.RuleFailed, "Сначала выберите объект.");
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
            var cells = building.grid.GetFootprintCells(candidateCell, placementDefinition.footprint, request.rotated, placementDefinition.pivotOffset).ToList();
            building.grid.Release(Selected.occupiedCells);
            Selected.transform.SetPositionAndRotation(BuildPlacementPose.Position(building.grid, placementDefinition, candidateCell, request.rotated, candidateFacing), Quaternion.Euler(0, candidateFacing * 90, 0));
            Selected.anchorCell = candidateCell;
            Selected.facing = candidateFacing;
            Selected.rotated = request.rotated;
            Selected.occupiedCells = cells;
            building.grid.Occupy(cells);
            building.NotifyLayoutChanged();
            CancelSelection();
            Notice = "Изменения применены. Можно выбрать следующий объект.";
            return true;
        }

        public bool DeleteSelected()
        {
            if (!IsActive || Selected == null) return false;
            if (Selected.playerParking)
            { Notice = "Личную парковку можно переместить или повернуть, но нельзя убрать вместе с местом возврата пикапа."; return false; }
            if (Selected.item.isUnloadingGate && FindObjectsOfType<PlacedObject>().Count(p => p.item != null && p.item.isUnloadingGate) <= 1)
            { Notice = "Последние разгрузочные ворота обязательны. Их можно переместить."; return false; }
            var shelf = Selected.GetComponent<PlacedShelfStock>();
            float stockWeight = shelf != null && shelf.AssignedProduct != null ? shelf.CurrentAmount * shelf.AssignedProduct.UnitWeightKg : 0;
            if (deliveries != null && deliveries.ShopWeightKg + stockWeight + Selected.item.weightKg > deliveries.shopCapacityKg)
            { Notice = "На складе недостаточно места для возврата предмета и товара."; return false; }
            if (shelf != null && shelf.AssignedProduct != null && shelf.CurrentAmount > 0)
            {
                if (deliveries == null) { Notice = "Склад недоступен — товар нельзя безопасно вернуть."; return false; }
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
            Notice = "Предмет и остаток товара возвращены на склад.";
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
            Notice = "Выберите установленный объект.";
        }

        public void Finish()
        {
            if (!IsActive && Selected == null) return;
            CancelSelection();
            building.ExitBuildMode();
        }

        private void Update()
        {
            if (!IsActive || !ReadPointerInput) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Selected != null) { CancelSelection(); Notice = "Выбор отменён."; }
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
                PlacedObject clicked = null;
                if (Physics.Raycast(ray, out var hit, 1000)) clicked = hit.collider.GetComponentInParent<PlacedObject>();
                if (clicked == null)
                {
                    var ground = new Plane(Vector3.up, building.grid.origin);
                    if (ground.Raycast(ray, out float groundDistance))
                    {
                        var cell = building.grid.WorldToCell(ray.GetPoint(groundDistance));
                        clicked = FindObjectsOfType<PlacedObject>().FirstOrDefault(p => p.occupiedCells.Contains(cell));
                    }
                }
                Select(clicked);
                return;
            }
            var plane = new Plane(Vector3.up, building.grid.origin);
            if (!plane.Raycast(ray, out float distance)) return;
            PreviewAt(building.grid.WorldToCell(ray.GetPoint(distance)));
            if (Input.GetMouseButtonDown(0)) Confirm();
        }

        private void OnDisable() => Finish();
    }
}
