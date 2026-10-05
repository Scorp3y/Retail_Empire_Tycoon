using System;
using System.Collections.Generic;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.Shelves;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RetailEmpireTycoon.UI.Shop;

namespace RetailEmpireTycoon.StoreOperations
{
    public enum WorkKind { Restock, Checkout, Cleaning }

    /// <summary>Five placement gestures are illustrative; only successful completion transfers actual warehouse goods.</summary>
    public sealed class WorkMinigame : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private MainCamera cameraInput;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Canvas[] legacyCanvases;
        [SerializeField] private ProductInventory warehouse;
        [SerializeField] private StoreOperations shop;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Sprite panelSprite, buttonSprite;
        private ShopUi _ui;
        private readonly LegacyUiVisibility _legacyUi = new LegacyUiVisibility();
        private void Awake() { _ui = new ShopUi(font, panelSprite, buttonSprite); }
        private RectTransform _panel, _tray;
        private TMP_Text _caption;
        private readonly List<RectTransform> _targets = new List<RectTransform>();
        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<Transform> _workSlots = new List<Transform>();
        private readonly List<GameObject> _visuals = new List<GameObject>();
        private readonly HashSet<int> _completed = new HashSet<int>();
        private Func<bool> _commit;
        private Action _cancelled;
        private ProductItemData _product;
        private WorkKind _kind;
        private int _required;
        private Vector3 _savedPosition, _closePosition;
        private Quaternion _savedRotation, _closeRotation;
        private bool _inputEnabled;
        private bool _holdingProduct;
        private int _pickedFrame;
        private Vector2 _previousPointer;
        private float _sweep, _transition;
        private ShopDirtSpot _dirt;
        private GameObject _mop;
        public bool IsActive => _panel != null;
        public PlacedShelfStock ActiveShelf { get; private set; }
        public int CompletedSteps => _completed.Count;

        public bool BeginRestock(PlacedShelfStock shelf, ProductItemData product)
        {
            if (IsActive || shelf == null || !shelf.CanAccept(product) || warehouse.GetCount(product) <= 0) return false;
            var display = shelf.GetComponent<ShelfProductDisplay>();
            var slots = display != null ? display.GetWorkSlots(5) : new List<Transform>();
            if (slots.Count < 5) { Debug.LogWarning("Shelf has no work slots: " + shelf.name, shelf); return false; }
            ActiveShelf = shelf; _product = product;
            _workSlots.AddRange(slots);
            foreach (var slot in slots) _positions.Add(slot.position);
            Vector3 focus = display.WorkAreaBounds.center + Vector3.up * .04f;
            StartWork(WorkKind.Restock, 5, focus, shelf.transform.forward, () => shelf != null && shelf.RefillFromInventory(warehouse, product), null);
            return true;
        }
        public bool BeginCheckout()
        {
            if (IsActive) return false;
            var visit = shop.ClaimCheckout(); if (visit == null) return false;
            _product = visit.Product;
            for (int i = 0; i < visit.Quantity; i++) _positions.Add(shop.CheckoutPosition + Vector3.up * 0.16f);
            StartWork(WorkKind.Checkout, visit.Quantity, shop.CheckoutPosition + Vector3.up * 0.18f, -Vector3.forward,
                () => shop.CompleteCheckout(visit), visit.Release); return true;
        }
        public bool BeginCleaning(ShopDirtSpot dirt)
        {
            if (IsActive || dirt == null || !dirt.TryClaim()) return false;
            _dirt = dirt;
            for (int i = 0; i < 5; i++) _positions.Add(dirt.transform.position + new Vector3((i % 3 - 1) * 0.09f, 0.02f, (i / 3) * 0.08f));
            StartWork(WorkKind.Cleaning, 5, dirt.transform.position, -Vector3.forward, () => { if (dirt == null) return false; shop.Clean(dirt); return true; }, dirt.Release);
            _mop = new GameObject("Low-poly player mop");
            MopPart(new Vector3(0, 0.01f, 0), new Vector3(0.2f, 0.035f, 0.07f));
            MopPart(new Vector3(0, 0.2f, 0), new Vector3(0.015f, 0.4f, 0.015f));
            return true;
        }
        private void MopPart(Vector3 position, Vector3 size)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.transform.SetParent(_mop.transform);
            part.transform.localPosition = position; part.transform.localScale = size; part.GetComponent<Collider>().enabled = false;
        }
        private void StartWork(WorkKind kind, int required, Vector3 focus, Vector3 forward, Func<bool> commit, Action cancel)
        {
            _kind = kind; _required = required; _commit = commit; _cancelled = cancel; _transition = 0;
            _savedPosition = worldCamera.transform.position; _savedRotation = worldCamera.transform.rotation;
            _inputEnabled = cameraInput != null && cameraInput.enabled; if (cameraInput != null) cameraInput.enabled = false;
            _legacyUi.Hide(legacyCanvases, canvas);
            Vector3 side = forward.normalized;
            if (kind == WorkKind.Restock && ActiveShelf != null && ActiveShelf.ShelfType == ShelfStorageType.DoubleSided
                && Vector3.Dot(_savedPosition - focus, side) < 0) side = -side;
            _closePosition = focus + side * 1.15f + Vector3.up * (kind == WorkKind.Cleaning ? 1.25f : 0.7f);
            if (kind == WorkKind.Restock) _closePosition = FindShelfView(focus, side);
            _closeRotation = Quaternion.LookRotation(focus - _closePosition);
            _panel = _ui.Rect("Work activity", canvas.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(590, 100));
            _ui.Panel(_panel, ShopUiTheme.Paper); _ui.Border(_panel);
            _caption = _ui.Label(_panel, "", new Vector2(16, -6), new Vector2(450, 30), 21); _caption.fontStyle = FontStyles.Bold;
            string instruction = kind == WorkKind.Cleaning ? ShopText.Get("Зажми ЛКМ и убери выделенное пятно", "Hold LMB and mop the highlighted spot") : ShopText.Get("Перетащи товар или поставь двумя кликами", "Drag the product or place with two clicks");
            _ui.Label(_panel, instruction, new Vector2(90, -45), new Vector2(350, 46), 17);
            _ui.Button(_panel, "Отмена", "Cancel", new Vector2(465, -49), new Vector2(110, 38), Cancel).image.color = ShopUiTheme.Line;
            _tray = _ui.Rect("Product tray", _panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -48), new Vector2(60, 55));
            var icon = _ui.Panel(_tray, Color.white); icon.sprite = _product != null ? _product.Icon : null;
            icon.type = Image.Type.Simple; icon.preserveAspect = true;
            _tray.gameObject.AddComponent<WorkProductDrag>().Initialize(pointer => DropProduct(pointer), PickProduct);
            _tray.gameObject.SetActive(kind != WorkKind.Cleaning);
            for (int i = 0; i < required; i++)
            {
                var target = _ui.Rect("Work target " + i, canvas.transform, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(kind == WorkKind.Cleaning ? 80 : 65, kind == WorkKind.Cleaning ? 55 : 65));
                _ui.Panel(target, new Color(0.35f, 0.85f, 0.18f, 0.35f)).raycastTarget = false;
                _ui.Label(target, kind == WorkKind.Checkout ? "СКАН" : (i + 1).ToString(), new Vector2(0, 0), target.sizeDelta, 16).alignment = TextAlignmentOptions.Center;
                target.gameObject.SetActive(false); _targets.Add(target);
            }
            RefreshCaption();
        }
        private Vector3 FindShelfView(Vector3 focus, Vector3 preferredSide)
        {
            var area = ActiveShelf.GetComponent<ShelfProductDisplay>().WorkAreaBounds;
            float distance = Mathf.Max(.8f, area.extents.magnitude * 2);
            Vector3 best = focus + preferredSide * distance + Vector3.up * .35f;
            float bestScore = float.MaxValue;
            // Sample both faces: imported model pivots don't reliably describe their serving face.
            foreach (float angle in new[] { 0f, 25f, -25f, 180f, 155f, 205f })
            {
                Vector3 side = Quaternion.AngleAxis(angle, Vector3.up) * preferredSide;
                Vector3 candidate = focus + side * distance + Vector3.up * .35f;
                float score = Mathf.Abs(Mathf.DeltaAngle(0, angle)) * .001f;
                foreach (var point in _positions)
                {
                    var direction = point - candidate;
                    foreach (var hit in Physics.RaycastAll(candidate, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.transform.IsChildOf(ActiveShelf.transform)) continue;
                        score += 10;
                    }
                }
                if (score >= bestScore) continue;
                bestScore = score; best = candidate;
            }
            return best;
        }
        private void LateUpdate()
        {
            if (!IsActive) return;
            if (Input.GetKeyDown(KeyCode.Escape) || ActiveShelf == null && _kind == WorkKind.Restock || _kind == WorkKind.Cleaning && _dirt == null) { Cancel(); return; }
            _transition = Mathf.Clamp01(_transition + Time.unscaledDeltaTime * 2.5f);
            worldCamera.transform.SetPositionAndRotation(Vector3.Lerp(_savedPosition, _closePosition, Mathf.SmoothStep(0, 1, _transition)), Quaternion.Slerp(_savedRotation, _closeRotation, Mathf.SmoothStep(0, 1, _transition)));
            int next = NextTarget();
            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i]; target.gameObject.SetActive(i == next && _transition >= 1);
                Vector3 screen = worldCamera.WorldToScreenPoint(_positions[i]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, null, out var local);
                target.localPosition = local;
            }
            if (_transition < 1) return;
            Vector2 pointer = Input.mousePosition;
            if (_holdingProduct)
            {
                _tray.position = pointer;
                if (Time.frameCount > _pickedFrame && Input.GetMouseButtonDown(0)) DropProduct(pointer);
            }
            if (_kind == WorkKind.Cleaning)
            {
                var plane = new Plane(Vector3.up, _positions[0]);
                if (_mop != null && plane.Raycast(worldCamera.ScreenPointToRay(pointer), out float distance)) _mop.transform.position = worldCamera.ScreenPointToRay(pointer).GetPoint(distance);
                if (Input.GetMouseButton(0) && RectTransformUtility.RectangleContainsScreenPoint(_targets[next], pointer))
                {
                    _sweep += Mathf.Min(40, Vector2.Distance(pointer, _previousPointer));
                    if (_sweep >= 70) { _sweep = 0; CompleteStep(next); }
                }
            }
            _previousPointer = pointer;
        }
        private void PickProduct()
        {
            if (!IsActive || _transition < 1) return;
            _holdingProduct = true; _pickedFrame = Time.frameCount;
            _tray.GetComponent<Image>().raycastTarget = false;
        }
        private bool DropProduct(Vector2 pointer)
        {
            if (!IsActive || _transition < 1 || _kind == WorkKind.Cleaning) return false;
            int next = NextTarget();
            if (!RectTransformUtility.RectangleContainsScreenPoint(_targets[next], pointer)) return false;
            _holdingProduct = false;
            _tray.anchoredPosition = new Vector2(16, -48);
            _tray.GetComponent<Image>().raycastTarget = true;
            return CompleteStep(next);
        }
        private int NextTarget() { for (int i = 0; i < _required; i++) if (!_completed.Contains(i)) return i; return _required - 1; }
        public bool CompleteStep(int index)
        {
            if (!IsActive || index != NextTarget() || index < 0 || index >= _required || !_completed.Add(index)) return false;
            if (_kind == WorkKind.Restock && _product.ShelfDisplayPrefab != null)
            {
                var model = ActiveShelf.GetComponent<ShelfProductDisplay>().CreateWorkProduct(_product, _workSlots[index]);
                if (model != null) _visuals.Add(model);
            }
            if (_completed.Count >= _required)
            {
                bool success = _commit != null && _commit();
                if (!success) { Debug.LogWarning("Work could not commit: target or stock changed."); _cancelled?.Invoke(); }
                End();
            }
            else RefreshCaption();
            return true;
        }
        private void RefreshCaption()
        {
            string activity = _kind == WorkKind.Restock ? ShopText.Get("Выкладка", "Restocking")
                : _kind == WorkKind.Checkout ? ShopText.Get("Касса", "Checkout") : ShopText.Get("Уборка", "Cleaning");
            _caption.text = activity + "  " + _completed.Count + " / " + _required;
        }
        public void Cancel() { if (!IsActive) return; _cancelled?.Invoke(); End(); }
        private void End()
        {
            _panel.gameObject.SetActive(false); Destroy(_panel.gameObject); _panel = null;
            foreach (var target in _targets) if (target != null) Destroy(target.gameObject); _targets.Clear(); _positions.Clear(); _workSlots.Clear();
            foreach (var model in _visuals) if (model != null) Destroy(model); _visuals.Clear();
            if (_mop != null) Destroy(_mop); _mop = null; _dirt = null;
            _completed.Clear(); _product = null; ActiveShelf = null; _holdingProduct = false; _sweep = 0; _commit = null; _cancelled = null;
            if (worldCamera != null) worldCamera.transform.SetPositionAndRotation(_savedPosition, _savedRotation);
            if (cameraInput != null) cameraInput.enabled = _inputEnabled;
            _legacyUi.Restore();
        }
        private void OnDisable() { Cancel(); }
    }
}
