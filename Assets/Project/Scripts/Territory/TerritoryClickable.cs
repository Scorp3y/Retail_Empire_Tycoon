using UnityEngine;

public sealed class TerritoryClickable : MonoBehaviour
{
    [SerializeField] private TerritoryZone _zone;
    [SerializeField] private TerritoryPurchaseController _purchase;

    private void Awake()
    {
        if (_zone == null) _zone = GetComponent<TerritoryZone>();
        if (_purchase == null) _purchase = FindObjectOfType<TerritoryPurchaseController>();
    }
    private void OnMouseDown()
    {
        if (_zone == null || _purchase == null) return;
        _purchase.RequestPurchase(_zone.Id, _zone.Price);
    }
}
