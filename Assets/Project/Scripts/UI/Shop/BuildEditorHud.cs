using System;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.StoreOperations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>One construction workspace: owned-item placement, editing, private parking and floor tools.</summary>
    public sealed class BuildEditorHud : MonoBehaviour
    {
        public BuildEditingController editor;
        public Transform uiRoot;
        public Transform warehouseWindow;
        public bool IsOpen {get;private set;}
        private BuildController building=>editor.building;
        private ShopUi ui;
        private RectTransform toolbar,catalog,content;
        private TMP_Text title,notice,floorLabel,catalogCount;
        private Button left,right,remove,cancel,down,up,buildTab,editTab,floorBrush,ceilingBrush,eraseBrush;
        private bool showCatalog=true;
        private BuildCategory? category;
        public Button Launcher {get;set;}

        private void Start()
        {
            ui=new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            toolbar=ui.Rect("Construction workspace",uiRoot,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,16),new Vector2(1000,156));
            ui.Panel(toolbar,ShopUiTheme.Paper);ui.Border(toolbar);
            toolbar.gameObject.AddComponent<ShopPanelFit>().MaximumSize=new Vector2(1000,156);
            ui.Icon(toolbar,ShopIcon.Hammer,new Vector2(-470,51),new Vector2(25,25));
            ui.Heading(toolbar,"Construction","Construction",new Vector2(51,-12),new Vector2(175,32),22);
            buildTab=Action(toolbar,ShopIcon.Equipment,"Build",new Vector2(230,-10),112,OpenCatalog);
            editTab=Action(toolbar,ShopIcon.Move,"Move",new Vector2(350,-10),112,OpenEditing);
            ui.Label(toolbar,"Click an object to move it",new Vector2(480,-19),new Vector2(180,24),12).color=ShopUiTheme.Muted;
            down=ui.IconButton(toolbar,ShopIcon.ArrowDown,new Vector2(670,-10),38,()=>building.Storeys.SelectFloor(building.Storeys.ActiveFloor-1));
            down.name="Floor down";
            floorLabel=ui.Label(toolbar,"Floor 1",new Vector2(714,-13),new Vector2(96,30),20);floorLabel.alignment=TextAlignmentOptions.Center;
            up=ui.IconButton(toolbar,ShopIcon.ArrowUp,new Vector2(816,-10),38,()=>building.Storeys.SelectFloor(building.Storeys.ActiveFloor+1));up.name="Floor up";
            Action(toolbar,ShopIcon.Check,"Done",new Vector2(866,-10),118,Close);
            title=ui.Label(toolbar,"",new Vector2(18,-54),new Vector2(590,25),18);
            notice=ui.Label(toolbar,"",new Vector2(18,-81),new Vector2(958,22),15);notice.color=ShopUiTheme.Muted;
            left=Action(toolbar,ShopIcon.RotateLeft,"Rotate left",new Vector2(16,-112),138,()=>Rotate(-1));
            right=Action(toolbar,ShopIcon.RotateRight,"Rotate right",new Vector2(162,-112),142,()=>Rotate(1));
            remove=Action(toolbar,ShopIcon.Warehouse,"To storage",new Vector2(312,-112),148,()=>editor.DeleteSelected());
            cancel=Action(toolbar,ShopIcon.Close,"Cancel",new Vector2(468,-112),120,CancelTool);
            ui.Label(toolbar,"Click to select / place · R / Q rotate",new Vector2(620,-120),new Vector2(364,22),13).color=ShopUiTheme.Muted;
            CreateCatalog();
            building.inventory.Changed+=RefreshCatalog;
            toolbar.gameObject.SetActive(false);catalog.gameObject.SetActive(false);
        }
        private Button Action(Transform parent,ShopIcon icon,string text,Vector2 position,float width,Action action)
        {
            var button=ui.Button(parent,text,position,new Vector2(width,34),action);
            button.image.color=ShopUiTheme.Line;
            var label=button.GetComponentInChildren<TMP_Text>();label.rectTransform.anchoredPosition=new Vector2(36,-2);label.rectTransform.sizeDelta=new Vector2(width-42,30);label.fontSize=16;
            ui.Icon(button.transform,icon,new Vector2(-width*.5f+20,0),new Vector2(20,20));
            return button;
        }
        private void CreateCatalog()
        {
            catalog=ui.Rect("Construction storage catalog",uiRoot,new Vector2(0,1),new Vector2(0,1),new Vector2(16,-112),new Vector2(286,430));
            ui.Panel(catalog,ShopUiTheme.Paper);ui.Border(catalog);
            ui.Heading(catalog,"Build from storage","Build from storage",new Vector2(14,-12),new Vector2(258,28),21);
            catalogCount=ui.Label(catalog,"",new Vector2(14,-44),new Vector2(258,23),14);catalogCount.color=ShopUiTheme.Muted;
            var filters=new[]{("All",(BuildCategory?)null),("Fixtures",(BuildCategory?)BuildCategory.Shelf),("Building",(BuildCategory?)BuildCategory.Structures),("Decor",(BuildCategory?)BuildCategory.Decoration)};
            for(int i=0;i<filters.Length;i++)
            {
                var filter=filters[i];
                var button=ui.Button(catalog,filter.Item1,new Vector2(12+i*67,-74),new Vector2(63,28),()=>{category=filter.Item2;content.anchoredPosition=Vector2.zero;RefreshCatalog();});
                button.GetComponentInChildren<TMP_Text>().fontSize=12;button.image.color=ShopUiTheme.Line;
            }
            var viewport=ui.Rect("Owned items viewport",catalog,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-112),new Vector2(262,238));
            ui.Panel(viewport,new Color(1,1,1,.01f));viewport.gameObject.AddComponent<RectMask2D>();
            content=ui.Rect("Owned construction items",viewport,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(262,238));
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.scrollSensitivity=28;scroll.movementType=ScrollRect.MovementType.Clamped;
            floorBrush=Action(catalog,ShopIcon.Floor,"Floor",new Vector2(12,-362),82,()=>UseSurface(false));
            ceilingBrush=Action(catalog,ShopIcon.Ceiling,"Ceiling",new Vector2(101,-362),86,()=>UseSurface(true));
            eraseBrush=Action(catalog,ShopIcon.Erase,"Erase",new Vector2(194,-362),80,()=>building.Storeys.ToggleRemoval());
            foreach(var button in new[]{floorBrush,ceilingBrush,eraseBrush})
            {
                var label=button.GetComponentInChildren<TMP_Text>();label.fontSize=12;label.rectTransform.anchoredPosition=new Vector2(26,-2);label.rectTransform.sizeDelta=new Vector2(((RectTransform)button.transform).sizeDelta.x-30,30);
                var icon=button.GetComponentInChildren<ShopIconGraphic>();icon.rectTransform.sizeDelta=new Vector2(16,16);icon.rectTransform.anchoredPosition+=Vector2.left*4;
            }
            ui.Label(catalog,"Up / Down arrows change floors.",new Vector2(14,-402),new Vector2(258,22),12).color=ShopUiTheme.Muted;
        }
        public void Toggle(){if(IsOpen)Close();else Open();}
        public void Open()
        {
            if(IsOpen || toolbar==null)return;
            editor.controls?.CloseWindows();editor.Begin();IsOpen=true;showCatalog=true;content.anchoredPosition=Vector2.zero;RefreshCatalog();
        }
        public void Close()
        {
            if(!IsOpen)return;
            IsOpen=false;editor.Finish();building.ExitBuildMode();building.Storeys.SetCeiling(false);building.Storeys.SelectFloor(0);
            toolbar.gameObject.SetActive(false);catalog.gameObject.SetActive(false);
        }
        public void OpenCatalog(){if(!IsOpen)Open();editor.Begin();showCatalog=true;RefreshCatalog();}
        public void OpenEditing(){editor.Begin();showCatalog=false;}
        public void BuildItem(BuildItemData item)
        {
            if(item==null || building.inventory.GetCount(item)<=0)return;
            if(!IsOpen)Open();
            building.Storeys.SetPaintMode();building.EnterBuildMode(item);showCatalog=true;
        }
        private void UseSurface(bool ceiling)
        {
            var item=building.SelectedItem;
            if(item==null || item.placementKind!=PlacementKind.Floor)
                item=building.inventory.Entries.FirstOrDefault(entry=>entry.item!=null && entry.count>0 && entry.item.placementKind==PlacementKind.Floor && !entry.item.isAsphalt)?.item;
            if(item==null)return;
            BuildItem(item);building.Storeys.SetCeiling(ceiling);
        }
        private void Rotate(int turns){if(editor.Selected!=null)editor.Rotate(turns);else building.RotateSelected(turns);}
        private void CancelTool(){if(editor.IsActive)editor.CancelSelection();else{building.ExitBuildMode();editor.Begin();}}
        private void RefreshCatalog()
        {
            if(content==null || !IsOpen)return;
            foreach(Transform row in content){row.gameObject.SetActive(false);Destroy(row.gameObject);}
            var entries=building.inventory.Entries.Where(entry=>entry.item!=null && entry.count>0 && (!category.HasValue || entry.item.category==category.Value)).OrderBy(entry=>ShopText.Item(entry.item)).ToArray();
            catalogCount.text=entries.Length+" owned item types · select to place";
            content.sizeDelta=new Vector2(262,Mathf.Max(238,entries.Length*64));
            for(int i=0;i<entries.Length;i++)
            {
                var entry=entries[i];var item=entry.item;
                var button=ui.Button(content,"",new Vector2(0,-i*64),new Vector2(258,58),()=>BuildItem(item));button.name="Build "+item.id;button.image.color=ShopUiTheme.Line;
                var imageRect=ui.Rect("Item icon",button.transform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(6,0),new Vector2(46,46));
                var image=imageRect.gameObject.AddComponent<Image>();image.sprite=item.icon;image.preserveAspect=true;image.raycastTarget=false;
                ui.Label(button.transform,ShopText.Item(item),new Vector2(62,-8),new Vector2(188,25),15);
                ui.Label(button.transform,"Owned ×"+entry.count,new Vector2(62,-33),new Vector2(188,18),12).color=ShopUiTheme.Muted;
            }
            if(entries.Length==0)ui.Label(content,"No owned items in this category. Order supplies from the store catalog.",new Vector2(10,-14),new Vector2(240,100),16);
        }
        private void LateUpdate()
        {
            if(toolbar==null)return;
            toolbar.gameObject.SetActive(IsOpen);catalog.gameObject.SetActive(IsOpen && showCatalog);
            if(!IsOpen)return;
            if(building.mode==BuildMode.Normal)editor.Begin();
            bool selected=editor.Selected!=null;
            var item=selected?editor.Selected.item:building.SelectedItem;
            title.text=item!=null?ShopText.Item(item)+(building.Storeys.EraseSurface?" · erase tiles":""):"Select an owned item, or click an object to move it";
            notice.text=selected?(!editor.CandidateResult.ok?BuildPreview.FailureText(editor.CandidateResult):editor.Notice)
                :building.HasPlacementPreview&&!building.PreviewResult.ok?BuildPreview.FailureText(building.PreviewResult)
                :building.mode==BuildMode.Build?"Click to place · Esc cancels the current tool":"Choose Build or Move · click any parking bay to relocate it";
            if(item!=null && item.storeyConnection!=StoreyConnectionKind.None && (selected?editor.CandidateResult.ok:building.HasPlacementPreview&&building.PreviewResult.ok))
            {
                int tiles=selected?editor.OpeningTileCount:building.PreviewOpeningTileCount;
                notice.text=tiles>0?$"Place to return {tiles} ceiling tiles to storage · keep both landings clear"
                    :"Place now · add Floor 2 tiles at the marked upper exit to connect it";
            }
            left.interactable=right.interactable=item!=null && item.allowRotation;
            remove.interactable=selected;cancel.interactable=selected || building.mode==BuildMode.Build;
            down.interactable=building.Storeys.ActiveFloor>0;up.interactable=building.Storeys.ActiveFloor<GridSystem.TopFloor;
            floorLabel.text="Floor "+(building.Storeys.ActiveFloor+1);
            buildTab.image.color=showCatalog?ShopUiTheme.Green:ShopUiTheme.Line;
            editTab.image.color=!showCatalog && !selected?ShopUiTheme.Green:ShopUiTheme.Line;
            floorBrush.image.color=!building.Storeys.PaintCeiling?ShopUiTheme.Green:ShopUiTheme.Line;
            ceilingBrush.image.color=building.Storeys.PaintCeiling?ShopUiTheme.Green:ShopUiTheme.Line;
            eraseBrush.image.color=building.Storeys.EraseSurface?ShopUiTheme.Green:ShopUiTheme.Line;
        }
        private void OnDestroy()
        {
            if(editor!=null && editor.building!=null && building.inventory!=null)building.inventory.Changed-=RefreshCatalog;
            if(toolbar!=null)Destroy(toolbar.gameObject);if(catalog!=null)Destroy(catalog.gameObject);
        }
    }
}
