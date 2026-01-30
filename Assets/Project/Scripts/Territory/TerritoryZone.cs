using UnityEngine;

public sealed class TerritoryZone : MonoBehaviour
{
    public TerritoryId Id;
    public int Price;

    [SerializeField] private TerritoryVisual _visual;

    private StoreProgression _progression;
    private bool _purchaseMode;


    public void Bind(StoreProgression progression)
    {
        _progression = progression;
        RefreshView();
    }

    public void SetPurchaseMode(bool enabled)
    {
        _purchaseMode = enabled;

        if (_visual != null)
            _visual.SetVisible(enabled);

        if (!enabled) SetHover(false);
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

        // После покупки: нельзя снова кликнуть/купить эту территорию.
        // Также убираем визуальную рамку (LineRenderer) через TerritoryVisual.
        if (purchased)
        {
            // Выключаем все коллайдеры на зоне (и дочерних объектах), чтобы raycast больше не попадал.
            foreach (var c in GetComponentsInChildren<Collider>(true))
                c.enabled = false;
        }
    }

    public bool CanPurchase()
    {
        if (!_purchaseMode) return false;
        if (_progression == null) return false;
        return _progression.IsTerritoryAvailable(Id);
    }

    public void SetHover(bool on)
    {
        if (_visual != null) _visual.SetHover(on);
    }

}

