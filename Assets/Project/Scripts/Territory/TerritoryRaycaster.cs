using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TerritoryRaycaster : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera _cam;
    [SerializeField] private TerritoryPurchaseModeManager _mode;
    [SerializeField] private TerritoryPurchaseController _purchase;

    [Header("Raycast")]
    [SerializeField] private LayerMask _territoryMask;
    [SerializeField] private float _maxDistance = 2000f;
    [SerializeField] private bool _blockWhenPointerOverUI = true;

    private void Awake()
    {
        if (_cam == null) _cam = Camera.main;
        if (_mode == null) _mode = FindObjectOfType<TerritoryPurchaseModeManager>(true);
        if (_purchase == null) _purchase = FindObjectOfType<TerritoryPurchaseController>(true);
    }

    private void Update()
    {
        if (_mode == null || !_mode.IsActive) return;
        if (!Input.GetMouseButtonDown(0)) return;

        if (_blockWhenPointerOverUI &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _territoryMask, QueryTriggerInteraction.Collide))
            return;

        var zone = hit.collider.GetComponentInParent<TerritoryZone>();
        if (zone == null) return;

        if (!zone.CanPurchase())
            return;

        _purchase.RequestPurchase(zone.Id, zone.Price);
    }
}
