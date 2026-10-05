using System.Collections.Generic;
using UnityEngine;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Territory;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.EventSystems;

namespace RetailEmpireTycoon.BuildSystem
{
    [DisallowMultipleComponent]
    [MovedFrom(false, "MyShopGame.BuildSystem", null, "BuildController")]
    public sealed class BuildController : MonoBehaviour
    {
        [Header("Refs")]
        public Camera worldCamera;
        public GridSystem grid;
        public BuildInventory inventory;
        public TerritoryManager territory;
        public BuildPreview preview;
        public event System.Action<BuildItemData> OnPlacedSuccessfully;
        public BuildGridOverlay gridOverlay;
        public FloorPainter floorPainter;
        [SerializeField] private RetailEmpireTycoon.StoreOperations.GameplayControls gameplayControls;

        [Header("State")]
        public BuildMode mode = BuildMode.Normal;

        private BuildItemData _selected;
        private bool _rotated;
        private int _facing;

        private PlacementValidator _validator;

        public bool TryGetPlacementFocus(out Vector3 focus)
        {
            focus = default;
            if (mode != BuildMode.Build || !TryGetMouseCell(out var cell, out _)) return false;
            focus = grid.CellToWorld(cell);
            return true;
        }

        private void Awake()
        {
            worldCamera ??= Camera.main; 

            grid ??= GetComponent<GridSystem>();
            inventory ??= GetComponent<BuildInventory>();
            territory ??= GetComponent<TerritoryManager>();
            preview ??= GetComponentInChildren<BuildPreview>(true);
            gridOverlay ??= FindObjectOfType<BuildGridOverlay>(true);
            floorPainter ??= FindObjectOfType<FloorPainter>(true);

            var rules = new List<IPlacementRule>
            {
                new Rule_InsidePurchasedArea(territory, grid),
                new Rule_NoOverlap(grid, grid),
                new Rule_Accessibility(grid, grid),
            };

            _validator = new PlacementValidator(rules); 
        }


        private bool _isPaintingFloor;
        private Vector3Int _floorStartCell;
        private List<Vector3Int> _floorPreviewCells = new();

        private void Update()
        {
            if (mode != BuildMode.Build) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                floorPainter?.ClearPreview();
                ExitBuildMode();
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                preview?.SetVisible(false);
                if (_isPaintingFloor && Input.GetMouseButtonUp(0))
                {
                    _isPaintingFloor = false;
                    _floorPreviewCells.Clear();
                    floorPainter?.ClearPreview();
                }
                return;
            }

            if (_selected != null && _selected.placementKind == PlacementKind.Floor)
            {
                HandleFloorPaintMode();
                return;
            }

            HandleRotate();
            UpdatePreview();

            if (Input.GetMouseButtonDown(0))
                TryPlace();
        }

        public void EnterBuildMode(BuildItemData item)
        {
            if (item == null) return;
            _isPaintingFloor = false;
            _floorPreviewCells.Clear();
            floorPainter?.ClearPreview();

            mode = BuildMode.Build;
            _selected = item;
            _rotated = false;
            _facing = 0;

            if (item.placementKind == PlacementKind.Floor)
                preview?.Clear();
            else
                preview?.SetItem(item);

            gridOverlay?.Show();
        }

        public void ExitBuildMode()
        {
            mode = BuildMode.Normal;
            _selected = null;
            _isPaintingFloor = false;
            _floorPreviewCells.Clear();
            floorPainter?.ClearPreview();
            preview?.Clear();
            gridOverlay?.Hide();
        }

        private void HandleRotate()
        {
            if (_selected == null) return;
            if (!_selected.allowRotation) return;

            bool clockwise = gameplayControls != null ? gameplayControls.Pressed(RetailEmpireTycoon.StoreOperations.ShopAction.RotateClockwise) : Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.E);
            bool counterclockwise = gameplayControls != null ? gameplayControls.Pressed(RetailEmpireTycoon.StoreOperations.ShopAction.RotateCounterclockwise) : Input.GetKeyDown(KeyCode.Q);
            var rot = counterclockwise ? -1 : clockwise ? 1 : 0;
            if (rot == 0) return;
            RotateSelected(rot);
        }

        public void RotateSelected(int quarterTurns)
        {
            if (_selected == null || !_selected.allowRotation) return;
            _facing = (_facing + quarterTurns) % 4;
            if (_facing < 0) _facing += 4;

            _rotated = _facing % 2 != 0;
        }

        private void UpdatePreview()
        {
            if (_selected == null) return;
            if (grid == null) return;
            if (_validator == null) return;

            if (!TryGetMouseCell(out var cell, out _))
            {
                preview?.SetVisible(false);
                return;
            }

            var worldPos = BuildPlacementPose.Position(grid, _selected, cell, _rotated, _facing);
            var rot = Quaternion.Euler(0f, _facing * 90f, 0f);

            var req = new PlacementRequest(_selected, cell, _rotated, _facing);
            var res = CanPlaceAt(cell);

            preview?.SetPose(worldPos, rot);
            preview?.ShowPlacement(grid, req, res);
        }

        /// <summary>The same result drives the ghost and the final placement, including stock availability.</summary>
        public PlacementResult CanPlaceAt(Vector3Int cell)
        {
            if (_selected == null || _selected.prefab == null || grid == null || _validator == null)
                return PlacementResult.Fail(PlaceFailReason.RuleFailed, "Предмет не готов к установке");
            if (inventory == null || inventory.GetCount(_selected) < 1)
                return PlacementResult.Fail(PlaceFailReason.InsufficientInventory, "Нет предмета на складе");
            if (territory == null || !territory.IsCellPurchased(cell))
                return PlacementResult.Fail(PlaceFailReason.NotPurchased, "За пределами доступной площади");
            return _validator.CanPlace(new PlacementRequest(_selected, cell, _rotated, _facing));
        }

        private void TryPlace()
        {
            if (_selected == null) return;
            if (grid == null) return;
            if (_validator == null) return;

            if (!TryGetMouseCell(out var cell, out _))
                return;

            if (_selected.placementKind == PlacementKind.Floor)
            {
                if (floorPainter != null)
                {
                    var cells = new List<Vector3Int> { cell };

                    if (floorPainter.AreCellsValid(cells) && inventory != null && inventory.GetCount(_selected) >= 1)
                    {
                        floorPainter.PaintCells(cells, _selected);
                        inventory.TryConsume(_selected, 1);

                        if (inventory.GetCount(_selected) <= 0)
                            ExitBuildMode();
                    }
                }

                return;
            }

            TryPlaceAt(cell);
        }

        public bool TryPlaceAt(Vector3Int cell)
        {
            if (mode != BuildMode.Build || _selected == null || _selected.placementKind == PlacementKind.Floor) return false;
            var req = new PlacementRequest(_selected, cell, _rotated, _facing);
            var res = CanPlaceAt(cell);
            if (!res.ok) return false;

            if (territory != null && !territory.IsCellPurchased(cell))
                return false;

            if (!SpawnPlaced(req)) return false;
            OnPlacedSuccessfully?.Invoke(req.item);
            return true;
        }

        private bool SpawnPlaced(PlacementRequest req)
        {
            if (req.item == null || req.item.prefab == null || inventory == null) return false;

            var worldPos = BuildPlacementPose.Position(grid, req.item, req.anchorCell, req.rotated, req.facing);
            var rot = Quaternion.Euler(0f, req.facing * 90f, 0f);

            var go = Instantiate(req.item.prefab, worldPos, rot);
            if (!inventory.TryConsume(req.item, 1))
            {
                go.SetActive(false);
                Destroy(go);
                return false;
            }
            var placed = go.GetComponent<PlacedObject>() ?? go.AddComponent<PlacedObject>();

            placed.item = req.item;
            placed.anchorCell = req.anchorCell;
            placed.rotated = req.rotated;
            placed.facing = req.facing;

            var cells = new List<Vector3Int>(grid.GetFootprintCells(req.anchorCell, req.item.footprint, req.rotated, req.item.pivotOffset));
            placed.occupiedCells = cells;

            grid.Occupy(cells);

            if (inventory == null || inventory.GetCount(req.item) <= 0)
            {
                ExitBuildMode();
            }
            return true;
        }

        private bool TryGetMouseCell(out Vector3Int cell, out Vector3 hitPos)
        {
            cell = default;
            hitPos = default;

            if (worldCamera == null) return false;
            if (grid == null) return false;

            var ray = worldCamera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, grid.origin);
            if (!plane.Raycast(ray, out float distance) || distance > 2000f)
                return false;

            hitPos = ray.GetPoint(distance);
            cell = grid.WorldToCell(hitPos);
            return true;
        }

        private void HandleFloorPaintMode()
        {
            if (_selected == null || floorPainter == null || inventory == null) return;
            if (!_isPaintingFloor)
            {
                if (TryGetMouseCell(out var hoverCell, out _))
                {
                    var cells = new List<Vector3Int> { hoverCell };

                    bool validArea = floorPainter.AreCellsValid(cells);
                    bool enoughItems = inventory.GetCount(_selected) >= 1;

                    floorPainter.ShowPreview(cells, validArea && enoughItems);
                }
            }

            if (_selected == null || floorPainter == null || inventory == null)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                if (!TryGetMouseCell(out _floorStartCell, out _))
                    return;

                _isPaintingFloor = true;
            }

            if (_isPaintingFloor && Input.GetMouseButton(0))
            {
                if (!TryGetMouseCell(out var currentCell, out _))
                    return;

                _floorPreviewCells = floorPainter.GetRectCells(_floorStartCell, currentCell);

                bool validArea = floorPainter.AreCellsValid(_floorPreviewCells);
                bool enoughItems = inventory.GetCount(_selected) >= _floorPreviewCells.Count;

                floorPainter.ShowPreview(_floorPreviewCells, validArea && enoughItems);
            }

            if (_isPaintingFloor && Input.GetMouseButtonUp(0))
            {
                _isPaintingFloor = false;

                if (_floorPreviewCells == null || _floorPreviewCells.Count == 0)
                {
                    floorPainter.ClearPreview();
                    return;
                }

                bool validArea = floorPainter.AreCellsValid(_floorPreviewCells);
                bool enoughItems = inventory.GetCount(_selected) >= _floorPreviewCells.Count;

                if (validArea && enoughItems)
                {
                    floorPainter.PaintCells(_floorPreviewCells, _selected);
                    inventory.TryConsume(_selected, _floorPreviewCells.Count);

                    if (inventory.GetCount(_selected) <= 0)
                        ExitBuildMode();
                }

                floorPainter.ClearPreview();
            }
        }

        public List<PlacedBuildSaveData> BuildPlacedSaveData()
        {
            var result = new List<PlacedBuildSaveData>();

            var placedObjects = FindObjectsOfType<PlacedObject>(true);

            foreach (var placed in placedObjects)
            {
                if (placed == null || placed.item == null || placed.GetComponentInParent<BuildPreview>() != null)
                    continue;

                result.Add(new PlacedBuildSaveData
                {
                    itemId = placed.item.id,
                    x = placed.anchorCell.x,
                    z = placed.anchorCell.z,
                    rotated = placed.rotated,
                    facing = placed.facing
                });
            }

            return result;
        }

        public void ApplyPlacedSaveData(List<PlacedBuildSaveData> data, BuildItemCatalog catalog)
        {
            ClearPlacedObjects();

            if (grid != null)
                grid.ClearAll();

            if (data == null || catalog == null)
                return;

            foreach (var d in data)
            {
                var item = catalog.GetById(d.itemId);
                if (item == null || item.prefab == null)
                    continue;

                var cell = new Vector3Int(d.x, 0, d.z);
                var req = new PlacementRequest(item, cell, d.rotated, d.facing);

                SpawnPlacedFromSave(req);
            }
        }

        private void ClearPlacedObjects()
        {
            var placedObjects = FindObjectsOfType<PlacedObject>(true);

            foreach (var placed in placedObjects)
            {
                if (placed != null && placed.GetComponentInParent<BuildPreview>() == null)
                    Destroy(placed.gameObject);
            }
        }

        private void SpawnPlacedFromSave(PlacementRequest req)
        {
            if (req.item == null) return;
            if (req.item.prefab == null) return;
            if (grid == null) return;

            var worldPos = BuildPlacementPose.Position(grid, req.item, req.anchorCell, req.rotated, req.facing);
            var rot = Quaternion.Euler(0f, req.facing * 90f, 0f);

            var go = Instantiate(req.item.prefab, worldPos, rot);
            var placed = go.GetComponent<PlacedObject>() ?? go.AddComponent<PlacedObject>();

            placed.item = req.item;
            placed.anchorCell = req.anchorCell;
            placed.rotated = req.rotated;
            placed.facing = req.facing;

            var cells = new List<Vector3Int>(
                grid.GetFootprintCells(req.anchorCell, req.item.footprint, req.rotated, req.item.pivotOffset)
            );

            placed.occupiedCells = cells;
            grid.Occupy(cells);
        }
    }


}
