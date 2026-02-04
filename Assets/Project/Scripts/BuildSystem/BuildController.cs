using System.Collections.Generic;
using UnityEngine;
using MyShopGame.Core;
using MyShopGame.Territory;

namespace MyShopGame.BuildSystem
{
    [DisallowMultipleComponent]
    public sealed class BuildController : MonoBehaviour
    {
        [Header("Refs")]
        public Camera worldCamera;
        public GridSystem grid;
        public BuildInventory inventory;
        public TerritoryManager territory;
        public BuildPreview preview;

        [Header("State")]
        public BuildMode mode = BuildMode.Normal;

        private BuildItemData _selected;
        private bool _rotated;
        private int _facing;

        private PlacementValidator _validator;

        private void Awake()
        {
            worldCamera ??= Camera.main;

            var rules = new List<IPlacementRule>
            {
                new Rule_InsidePurchasedArea(territory, grid),
                new Rule_NoOverlap(grid, grid),
                new Rule_Accessibility(grid, grid),
            };

            _validator = new PlacementValidator(rules);
        }

        private void Update()
        {
            if (mode != BuildMode.Build)
                return;

            HandleRotate();
            UpdatePreview();

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                ExitBuildMode();

            if (Input.GetMouseButtonDown(0))
                TryPlace();
        }

        public void EnterBuildMode(BuildItemData item)
        {
            if (item == null)
                return;

            mode = BuildMode.Build;
            _selected = item;
            _rotated = false;
            _facing = 0;

            preview?.SetItem(item);
        }

        public void ExitBuildMode()
        {
            mode = BuildMode.Normal;
            _selected = null;
            preview?.Clear();
        }

        private void HandleRotate()
        {
            if (_selected == null || !_selected.allowRotation)
                return;

            var rot = Input.GetKeyDown(KeyCode.Q) ? -1 : Input.GetKeyDown(KeyCode.E) ? 1 : 0;
            if (rot == 0)
                return;

            _facing = (_facing + rot) % 4;
            if (_facing < 0)
                _facing += 4;

            _rotated = _facing % 2 != 0;
        }

        private void UpdatePreview()
        {
            if (!TryGetMouseCell(out var cell, out var hitPos))
                return;

            var worldPos = grid.CellToWorld(cell);
            var rot = Quaternion.Euler(0f, _facing * 90f, 0f);

            var req = new PlacementRequest(_selected, cell, _rotated, _facing);
            var res = _validator.CanPlace(req);

            preview?.SetPose(worldPos, rot);
            preview?.SetValid(res.ok);
        }

        private void TryPlace()
        {
            if (!TryGetMouseCell(out var cell, out _))
                return;

            var req = new PlacementRequest(_selected, cell, _rotated, _facing);
            var res = _validator.CanPlace(req);

            if (!res.ok)
                return;

            if (!inventory.TryConsume(_selected, 1))
                return;

            SpawnPlaced(req);
        }

        private void SpawnPlaced(PlacementRequest req)
        {
            var worldPos = grid.CellToWorld(req.anchorCell);
            var rot = Quaternion.Euler(0f, req.facing * 90f, 0f);

            var go = Instantiate(req.item.prefab, worldPos, rot);
            var placed = go.GetComponent<PlacedObject>() ?? go.AddComponent<PlacedObject>();

            placed.item = req.item;
            placed.anchorCell = req.anchorCell;
            placed.rotated = req.rotated;
            placed.facing = req.facing;

            var cells = new List<Vector3Int>(grid.GetFootprintCells(req.anchorCell, req.item.footprint, req.rotated));
            placed.occupiedCells = cells;

            grid.Occupy(cells);
        }

        private bool TryGetMouseCell(out Vector3Int cell, out Vector3 hitPos)
        {
            cell = default;
            hitPos = default;

            if (worldCamera == null)
                return false;

            var ray = worldCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 1000f))
                return false;

            hitPos = hit.point;
            cell = grid.WorldToCell(hit.point);
            return true;
        }
    }
}
