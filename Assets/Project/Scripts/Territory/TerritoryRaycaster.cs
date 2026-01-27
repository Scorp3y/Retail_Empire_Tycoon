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

        int terrLayer = LayerMask.NameToLayer("Territory");
        Debug.Log($"[TerritoryRaycaster] Territory layer index={terrLayer}, maskValue={_territoryMask.value}", this);

        if (terrLayer == -1)
            Debug.LogError("[TerritoryRaycaster] Слоя 'Territory' НЕ существует. Layers -> Add 'Territory'.", this);

        if (_territoryMask.value == 0)
            Debug.LogWarning("[TerritoryRaycaster] TerritoryMask = Nothing. В инспекторе выбери слой Territory.", this);
    }

    private void Update()
    {
        // Работает только в режиме покупки
        if (_mode == null || !_mode.IsActive) return;

        if (!Input.GetMouseButtonDown(0)) return;

        // Если курсор над UI — можно блокировать клик
        if (_blockWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("[TerritoryRaycaster] Click ignored (pointer over UI)");
            return;
        }

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        // 1) Debug: во что попали вообще без маски (чтобы видеть Terrain / Floor / и т.д.)
        if (Physics.Raycast(ray, out RaycastHit anyHit, _maxDistance, ~0, QueryTriggerInteraction.Collide))
        {
            Debug.Log($"[ANY HIT] {anyHit.collider.name} layer={LayerMask.LayerToName(anyHit.collider.gameObject.layer)} dist={anyHit.distance}");
        }
        else
        {
            Debug.Log("[ANY HIT] nothing");
        }

        // 2) Основной Raycast: только по Territory
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _territoryMask, QueryTriggerInteraction.Collide))
        {
            Debug.Log("[TERR HIT] none (mask)");
            return;
        }

        Debug.Log($"[TERR HIT] {hit.collider.name} layer={LayerMask.LayerToName(hit.collider.gameObject.layer)} dist={hit.distance}");

        var zone = hit.collider.GetComponentInParent<TerritoryZone>();
        if (zone == null)
        {
            Debug.LogWarning($"[TerritoryRaycaster] Hit {hit.collider.name}, but no TerritoryZone in parents.", hit.collider);
            return;
        }

        Debug.Log($"[TerritoryRaycaster] Zone FOUND id={zone.Id} price={zone.Price}");
        _purchase.RequestPurchase(zone.Id, zone.Price);
    }
}
