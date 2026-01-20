using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PurchaseModeManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera _camera;
    [SerializeField] private CameraModeController _cameraMode;
    [SerializeField] private StoreProgression _progression;
    [SerializeField] private StoreLevelManager _levelManager;

    [Header("UI")]
    [SerializeField] private ConfirmPurchaseUI _confirmUI;
    [SerializeField] private ScreenFader _fader;

    [Header("Territories")]
    [SerializeField] private List<TerritoryZone> _zones = new();

    [Header("Input")]
    [SerializeField] private LayerMask _territoryMask;

    private bool _active;
    private TerritoryZone _hovered;

    private void Reset()
    {
        _camera = Camera.main;
    }

    private void Awake()
    {
        foreach (var z in _zones)
            z.Bind(_progression);

        _progression.OnChanged += RefreshAll;
        RefreshAll();

        _levelManager.SwitchTo(_progression.State.CurrentLevel);
    }

    private void OnDestroy()
    {
        if (_progression != null)
            _progression.OnChanged -= RefreshAll;
    }

    public void EnterPurchaseMode()
    {
        if (_active) return;
        _active = true;

        _cameraMode.EnterPurchaseMode();
        RefreshAll();
    }

    public void ExitPurchaseMode()
    {
        if (!_active) return;
        _active = false;

        ClearHover();
        _confirmUI.HideInstant();
        _cameraMode.ExitPurchaseMode();
    }

    private void Update()
    {
        if (!_active) return;

        UpdateHover();

        if (Input.GetMouseButtonDown(0))
            TryClick();

        if (Input.GetKeyDown(KeyCode.Escape))
            ExitPurchaseMode();
    }

    private void UpdateHover()
    {
        Ray r = _camera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(r, out var hit, 500f, _territoryMask))
        {
            var zone = hit.collider.GetComponentInParent<TerritoryZone>();
            if (zone != _hovered)
            {
                ClearHover();
                _hovered = zone;
                if (_hovered != null) _hovered.SetHover(true);
            }
        }
        else
        {
            ClearHover();
        }
    }

    private void ClearHover()
    {
        if (_hovered != null) _hovered.SetHover(false);
        _hovered = null;
    }

    private void TryClick()
    {
        if (_hovered == null) return;
        if (!_hovered.CanPurchase()) return;

        int price = _hovered.Price;

        _confirmUI.Show(
            $"Уверены ли вы купить эту территорию за {price}?",
            onYes: () => StartCoroutine(PurchaseRoutine(_hovered)),
            onNo: () => _confirmUI.HideInstant()
        );
    }

    private IEnumerator PurchaseRoutine(TerritoryZone zone)
    {
        _confirmUI.HideInstant();

        if (_fader != null) yield return _fader.FadeOut();

        _progression.MarkPurchased(zone.Id);
        _levelManager.SwitchTo(_progression.State.CurrentLevel);

        RefreshAll();
        RefreshAll();
        if (_fader != null) yield return _fader.FadeIn();
    }

    private void RefreshAll()
    {
        foreach (var z in _zones)
            z.RefreshView();
    }
}
