using UnityEngine;

public sealed class TerritoryZone : MonoBehaviour
{
    [Header("Config")]
    public TerritoryId Id;
    public int Price;

    [Header("Visual")]
    [SerializeField] private TerritoryVisual _visual;

    [Header("World UI")]
    [SerializeField] private GameObject _priceTagRoot;
    [SerializeField] private TMPro.TextMeshProUGUI _priceText;
    [SerializeField] private GameObject _lockIconRoot;

    private StoreProgression _progression;

    public void Bind(StoreProgression progression)
    {
        _progression = progression;
        RefreshView();
    }

    public void RefreshView()
    {
        if (_progression == null) return;

        bool purchased = _progression.State.IsPurchased(Id);
        bool available = _progression.IsTerritoryAvailable(Id);

        var state = purchased
            ? TerritoryVisual.TerritoryViewState.Purchased
            : (available ? TerritoryVisual.TerritoryViewState.Available : TerritoryVisual.TerritoryViewState.Locked);

        if (_visual != null) _visual.SetState(state);
        if (_priceText != null) _priceText.text = Price.ToString();
        if (_priceTagRoot != null) _priceTagRoot.SetActive(available && !purchased);
        if (_lockIconRoot != null) _lockIconRoot.SetActive(!available && !purchased);

        if (purchased)
        {
            if (_priceTagRoot != null) _priceTagRoot.SetActive(false);
            if (_lockIconRoot != null) _lockIconRoot.SetActive(false);
        }
    }

    public bool CanPurchase()
    {
        if (_progression == null) return false;
        return _progression.IsTerritoryAvailable(Id);
    }

    public void SetHover(bool on)
    {
        if (_visual != null) _visual.SetHover(on);
    }
}
