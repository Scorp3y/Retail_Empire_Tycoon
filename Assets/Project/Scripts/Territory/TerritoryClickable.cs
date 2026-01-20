using UnityEngine;

public sealed class TerritoryClickable : MonoBehaviour
{
    [SerializeField] private TerritoryZone _zone;
    [SerializeField] private TerritoryPurchaseController _purchase;
    [SerializeField] private TerritoryPurchaseModeManager _mode;

    private void Awake()
    {
        if (_zone == null) _zone = GetComponent<TerritoryZone>();
        if (_purchase == null) _purchase = FindObjectOfType<TerritoryPurchaseController>();
        if (_mode == null) _mode = FindObjectOfType<TerritoryPurchaseModeManager>();
    }

    private void OnMouseDown()
    {
        if (_mode == null || !_mode.IsActive) return;
        if (_zone == null || _purchase == null) return;
        if (!_zone.CanPurchase()) return;

        _purchase.RequestPurchase(_zone.Id, _zone.Price);
    }

    private void OnMouseEnter()
    {
        if (_mode == null || !_mode.IsActive) return;
        if (_zone == null) return;
        _zone.SetHover(true);
    }

    private void OnMouseExit()
    {
        if (_zone == null) return;
        _zone.SetHover(false);
    }
}
