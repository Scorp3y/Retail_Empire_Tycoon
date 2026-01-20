using System.Collections.Generic;
using UnityEngine;

public sealed class TerritoryPurchaseModeManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private StoreProgression _progression;
    [SerializeField] private CameraModeController _cameraMode;
    [SerializeField] private List<TerritoryZone> _zones = new();

    public bool IsActive { get; private set; }
    private void Awake()
    {
        if (_progression == null) _progression = FindObjectOfType<StoreProgression>();
        if (_cameraMode == null) _cameraMode = FindObjectOfType<CameraModeController>();

        foreach (var z in _zones)
            z.Bind(_progression);

        SetActive(false);
    }

    public void Enter()
    {
        SetActive(true);
        _cameraMode?.EnterPurchaseMode();
    }

    public void Exit()
    {
        _cameraMode?.ExitPurchaseMode();
        SetActive(false);
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
}
