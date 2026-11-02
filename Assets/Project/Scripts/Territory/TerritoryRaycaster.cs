using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public sealed class TerritoryRaycaster : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera _cam;
    [SerializeField] private TerritoryPurchaseModeManager _mode;
    [SerializeField] private TerritoryPurchaseController _purchase;

    [Header("Raycast")]
    [SerializeField] private float _maxDistance = 2000f;
    private readonly List<RaycastResult> uiHits = new();
    private ConfirmPurchaseUI confirmation;

    private void Awake()
    {
        if (_cam == null) _cam = Camera.main;
        if (_mode == null) _mode = FindObjectOfType<TerritoryPurchaseModeManager>(true);
        if (_purchase == null) _purchase = FindObjectOfType<TerritoryPurchaseController>(true);
        confirmation = FindObjectOfType<ConfirmPurchaseUI>(true);
    }

    private void Update()
    {
        if (_mode == null || !_mode.IsActive) return;
        if (!Input.GetMouseButtonDown(0)) return;

        TrySelectAtScreenPosition(Input.mousePosition);
    }

    /// <summary>Select land only for an unobstructed world click. UI always owns its clicks,
    /// regardless of legacy scene overrides or EventSystem update ordering.</summary>
    public bool TrySelectAtScreenPosition(Vector2 position)
    {
        if (_mode == null || !_mode.IsActive || _purchase != null && _purchase.IsBusy
            || confirmation != null && confirmation.IsOpen) return false;
        if (EventSystem.current != null)
        {
            uiHits.Clear();
            var pointer = new PointerEventData(EventSystem.current) { position = position };
            EventSystem.current.RaycastAll(pointer, uiHits);
            foreach (var hit in uiHits)
                if (hit.module is UnityEngine.UI.GraphicRaycaster) return false;
        }

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;

        Ray ray = _cam.ScreenPointToRay(position);

        // Land selection remains possible through decorative foliage and does not
        // require invisible colliders during ordinary play.
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray,out float distance) || distance > _maxDistance || _mode.Rules == null) return false;
        var point=ray.GetPoint(distance);
        foreach(var parcel in _mode.Rules.Parcels)
            if(parcel.Bounds.Contains(new Vector2(point.x,point.z))) {_mode.Select(parcel.Id);return true;}
        return false;
    }
}
