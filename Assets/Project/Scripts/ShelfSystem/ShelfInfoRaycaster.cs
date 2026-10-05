using UnityEngine;
using UnityEngine.EventSystems;
using RetailEmpireTycoon.BuildSystem;

namespace RetailEmpireTycoon.Shelves
{
    [DisallowMultipleComponent]
    public sealed class ShelfInfoRaycaster : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private ShelfInfoWindow infoWindow;
        [SerializeField] private ProductAssignMode assignMode;
        [SerializeField] private BuildController buildController;
        [SerializeField] private RetailEmpireTycoon.StoreOperations.WorkMinigame workMinigame;
        private Vector2 _pressPosition;
        private bool _pendingClick;
        private bool _dragged;

        [Header("Raycast")]
        [SerializeField] private LayerMask shelfMask;
        [SerializeField, Min(1f)] private float maxDistance = 2000f;
        [SerializeField] private bool blockWhenPointerOverUI = true;

        private void Awake()
        {
            FindMissingRefs();
        }

        private void Update()
        {
            if (workMinigame != null && workMinigame.IsActive) { HideWindow(); return; }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HideWindow();
                return;
            }

            if (buildController != null && buildController.mode == BuildMode.Build)
            {
                _pendingClick = false;
                HideWindow();
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                _pressPosition = Input.mousePosition;
                _pendingClick = !IsPointerBlockedByUI();
                _dragged = false;
            }
            if (_pendingClick && ((Vector2)Input.mousePosition - _pressPosition).sqrMagnitude > 36f)
                _dragged = true;
            if (!Input.GetMouseButtonUp(1)) return;
            bool isClick = _pendingClick && !_dragged;
            _pendingClick = false;
            if (!isClick) return;

            if (assignMode != null && assignMode.IsActive)
                return;

            TryShowShelfInfo();
        }

        private void TryShowShelfInfo()
        {
            if (IsPointerBlockedByUI())
                return;

            if (!TryFindShelfUnderMouse(out var shelf))
            {
                HideWindow();
                return;
            }

            infoWindow.Show(shelf, Input.mousePosition);
        }

        private bool TryFindShelfUnderMouse(out PlacedShelfStock shelf)
        {
            shelf = null;

            if (worldCamera == null)
                worldCamera = Camera.main;

            if (worldCamera == null)
                return false;

            Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
            int mask = shelfMask.value == 0 ? ~0 : shelfMask.value;

            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, mask, QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
                return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                shelf = hit.collider.GetComponentInParent<PlacedShelfStock>();

                if (shelf != null)
                    return true;
            }

            return false;
        }

        private bool IsPointerBlockedByUI()
        {
            return blockWhenPointerOverUI
                && EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject();
        }

        private void HideWindow()
        {
            if (infoWindow != null)
                infoWindow.Hide();
        }

        private void FindMissingRefs()
        {
            if (worldCamera == null)
                worldCamera = Camera.main;

            if (infoWindow == null)
                infoWindow = FindObjectOfType<ShelfInfoWindow>(true);

            if (assignMode == null)
                assignMode = FindObjectOfType<ProductAssignMode>(true);
            if (buildController == null)
                buildController = FindObjectOfType<BuildController>(true);
        }
    }
}
