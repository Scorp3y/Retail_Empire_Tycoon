using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RetailEmpireTycoon.Territory;

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
    public TerritoryId? Selected { get; private set; }
    public TerritorySubdivisionRules Rules => _progression != null ? _progression.LandRules : null;
    private TerritoryLandOverlay overlay;
    private TerritoryPurchasePanel panel;
    private TerritoryPurchaseController purchase;
    private string feedback;
    private int displayedBalance = -1;

    private void Awake()
    {
        if (_progression == null) _progression = StoreProgression.Instance ?? FindObjectOfType<StoreProgression>(true);
        if (_cameraMode == null) _cameraMode = FindObjectOfType<CameraModeController>();

        if (_zones == null || _zones.Count == 0)
            _zones = new List<TerritoryZone>(FindObjectsOfType<TerritoryZone>(true));

        foreach (var z in _zones)
            if (z != null) z.Bind(_progression);

        SetActive(false);
        RefreshButtons();

        if (_progression != null)
            _progression.OnChanged += RefreshZones;
    }

    private void OnDestroy()
    {
        overlay?.Dispose();panel?.Dispose();
        if (_progression != null)
            _progression.OnChanged -= RefreshZones;
    }

    private void RefreshZones()
    {
        foreach (var z in _zones)
            if (z != null) z.RefreshView();
        RefreshPresentation();
    }

    public void Enter()
    {
        if (IsActive || Rules == null) return;
        if(overlay==null) overlay=new TerritoryLandOverlay();
        if(panel==null) panel=new TerritoryPurchasePanel(Exit,BuySelected,()=>Browse(-1),()=>Browse(1));
        purchase=FindObjectOfType<TerritoryPurchaseController>(true);
        Selected=null;feedback=null;
        SetActive(true);
        RefreshButtons();
        _cameraMode?.EnterPurchaseMode();
        RefreshPresentation();
    }


    public void Exit()
    {
        if(!IsActive || purchase != null && purchase.IsBusy)return;
        FindObjectOfType<ConfirmPurchaseUI>(true)?.HideInstant();
        _cameraMode?.ExitPurchaseMode();
        SetActive(false);
        RefreshButtons();
    }

    private void SetActive(bool active)
    {
        IsActive = active;
        if(overlay!=null)overlay.Root.SetActive(active);
        panel?.SetVisible(active);

        foreach (var z in _zones)
        {
            if (z == null) continue;
            z.SetPurchaseMode(active);
            z.RefreshView();
        }
    }

    public void Select(TerritoryId id)
    {
        if(!IsActive||!Rules.Contains(id))return;
        Selected=id;feedback=null;RefreshPresentation();
    }
    public void PurchaseCompleted()
    {
        feedback="Land purchased. Ready to build.";
        RefreshZones();_cameraMode?.RefreshPurchaseView();
    }
    public void BuySelected()
    {
        if(!IsActive||!Selected.HasValue||!_progression.IsTerritoryAvailable(Selected.Value))return;
        int price=Rules.PriceOf(Selected.Value);
        if(GameManager.Instance==null||GameManager.Instance.playerMoney<price)return;
        purchase?.RequestPurchase(Selected.Value,price);
    }
    private void Browse(int direction)
    {
        int index=-1;
        for(int i=0;i<Rules.Parcels.Count;i++)if(Selected==Rules.Parcels[i].Id)index=i;
        index=(index+direction+Rules.Parcels.Count)%Rules.Parcels.Count;
        Select(Rules.Parcels[index].Id);
    }
    private void RefreshPresentation()
    {
        if(!IsActive||Rules==null)return;
        overlay?.Refresh(Rules,_progression,Selected);
        displayedBalance=GameManager.Instance!=null?GameManager.Instance.playerMoney:0;
        panel?.Refresh(Rules,_progression,Selected,feedback);
    }
    private void Update()
    {
        if(!IsActive)return;
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            var confirmation=FindObjectOfType<ConfirmPurchaseUI>(true);
            if(confirmation!=null&&confirmation.IsOpen)confirmation.HideInstant();else Exit();
            return;
        }
        // Balance can change while the store remains open.
        int balance=GameManager.Instance!=null?GameManager.Instance.playerMoney:0;
        if(balance==displayedBalance)return;
        displayedBalance=balance;
        panel?.Refresh(Rules,_progression,Selected,feedback);
    }

    private void RefreshButtons()
    {
        if (_enterButton != null) _enterButton.SetActive(!IsActive);
        if (_backButton != null) _backButton.SetActive(false);
    }
}
