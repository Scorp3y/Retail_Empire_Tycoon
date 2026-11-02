using System;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.Territory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Purchase details use the authoritative parcel definition, never the legacy sign price.</summary>
public sealed class TerritoryPurchasePanel : IDisposable
{
    private readonly GameObject root;
    private readonly TMP_Text title, details, balance, notice, summary;
    private readonly Button buy;
    public TerritoryPurchasePanel(Action close, Action purchase, Action previous, Action next)
    {
        root = new GameObject("Territories UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=45;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        // Use the existing theme, fonts and rounded sprites from the rest of the HUD.
        var ui=new ShopUi(Resources.Load<RetailEmpireTycoon.UI.Shop.ShopUiTheme>("ShopUi/Theme"));
        var panel=ui.Rect("Land details",root.transform,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-24,0),new Vector2(330,480));
        ui.Panel(panel,ShopUi.Surface);ui.Border(panel);
        ui.Heading(panel,"Territories","Territories",new Vector2(20,-18),new Vector2(290,36),26);
        summary=ui.Label(panel,"",new Vector2(20,-60),new Vector2(290,45),16);
        title=ui.Label(panel,"Select a plot",new Vector2(20,-118),new Vector2(290,32),22);
        details=ui.Label(panel,"Click an outlined plot on the ground.",new Vector2(20,-160),new Vector2(290,112),18);
        balance=ui.Label(panel,"",new Vector2(20,-278),new Vector2(290,54),17);
        notice=ui.Label(panel,"",new Vector2(20,-335),new Vector2(290,40),16);
        buy=ui.Button(panel,"Buy land",new Vector2(20,-382),new Vector2(290,38),purchase);
        ui.Button(panel,"<",new Vector2(20,-430),new Vector2(44,32),previous);
        ui.Button(panel,">",new Vector2(72,-430),new Vector2(44,32),next);
        ui.Button(panel,"Done",new Vector2(130,-430),new Vector2(180,32),close);
        root.SetActive(false);
    }
    public void SetVisible(bool visible)=>root.SetActive(visible);
    public void Refresh(TerritorySubdivisionRules rules,StoreProgression progression,TerritoryId? selected,string feedback)
    {
        int count=0;float area=rules.InitialBounds.width*rules.InitialBounds.height;
        TerritorySubdivisionRules.Parcel chosen=null;
        foreach(var parcel in rules.Parcels)
        {
            if(progression.State.IsPurchased(parcel.Id)){count++;area+=parcel.Bounds.width*parcel.Bounds.height;}
            if(selected==parcel.Id)chosen=parcel;
        }
        summary.text=$"Owned land: {area:N0} m²\nExpansion plots: {count} / {rules.Parcels.Count}";
        int money=GameManager.Instance!=null?GameManager.Instance.playerMoney:0;
        buy.interactable=false;notice.text=feedback;
        if(chosen==null){title.text="Select a plot";details.text="Click an outlined plot, or browse with the arrows.";balance.text=$"Balance: ${money:N0}";return;}
        var bounds=chosen.Bounds;bool owned=progression.State.IsPurchased(chosen.Id);bool available=progression.IsTerritoryAvailable(chosen.Id);
        title.text=$"Expansion plot {(int)chosen.Id-99:00}";
        details.text=$"Size: {bounds.width:0.#} × {bounds.height:0.#} m\nArea: {bounds.width*bounds.height:0.#} m²\n\nPrice: ${chosen.Price:N0}";
        balance.text=$"Balance: ${money:N0}\nAfter purchase: ${(long)money-chosen.Price:N0}";
        if(owned) {notice.text="Already owned";balance.text=$"Balance: ${money:N0}";}
        else if(!available)notice.text="Buy a neighbouring plot first.";
        else if(money<chosen.Price)notice.text=$"Need ${chosen.Price-money:N0} more.";
        else buy.interactable=true;
    }
    public void Dispose()=>UnityEngine.Object.Destroy(root);
}
