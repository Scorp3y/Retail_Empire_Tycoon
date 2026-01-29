using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TerritoryPurchaseModeManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private StoreProgression _progression;
    [SerializeField] private CameraModeController _cameraMode;
    [SerializeField] private List<TerritoryZone> _zones = new();

    [Header("UI")]
    [SerializeField] private GameObject _enterButton;
    [SerializeField] private GameObject _backButton;

    public bool IsActive { get; private set; }

    private void Awake()
    {
        if (_progression == null) _progression = StoreProgression.Instance ?? FindObjectOfType<StoreProgression>(true);
        if (_cameraMode == null) _cameraMode = FindObjectOfType<CameraModeController>();

        foreach (var z in _zones)
            if (z != null) z.Bind(_progression);

        SetActive(false);
        RefreshButtons();

        if (_progression != null)
            _progression.OnChanged += RefreshZones;
    }

    private void OnDestroy()
    {
        if (_progression != null)
            _progression.OnChanged -= RefreshZones;
    }

    private void RefreshZones()
    {
        foreach (var z in _zones)
            if (z != null) z.RefreshView();
    }

    public void Enter()
    {
        Debug.Log("[PurchaseMode] ENTER");
        SetActive(true);
        RefreshButtons();
        _cameraMode?.EnterPurchaseMode();
    }


    public void Exit()
    {
        _cameraMode?.ExitPurchaseMode();
        SetActive(false);
        RefreshButtons();
    }

    private void SetActive(bool active)
    {
        IsActive = active;

        foreach (var z in _zones)
        {
            if (z == null) continue;
            z.SetPurchaseMode(active);
            z.RefreshView();
        }
    }

    private void RefreshButtons()
    {
        if (_enterButton != null) _enterButton.SetActive(!IsActive);
        if (_backButton != null) _backButton.SetActive(IsActive);
    }
}
