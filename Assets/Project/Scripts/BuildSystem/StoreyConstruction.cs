using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.StoreOperations;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Owns floor/ceiling selection and upper-storey cutaway. Lower-storey objects stay visible.</summary>
    public sealed class StoreyConstruction : MonoBehaviour
    {
        public int ActiveFloor {get;private set;}
        public bool PaintCeiling {get;private set;}
        public bool EraseSurface {get;private set;}
        public int SurfaceLayer=>ActiveFloor+(building!=null && building.mode==BuildMode.Build
            && building.SelectedItem!=null && building.SelectedItem.placementKind==PlacementKind.Floor && PaintCeiling?1:0);
        private BuildController building;
        private MainCamera cameraInput;
        private readonly Dictionary<Renderer,bool> hidden=new();
        private float nextRefresh;
        private int overlayLayer=-1;

        public void Initialize(BuildController owner)
        {building=owner;cameraInput=owner.worldCamera!=null?owner.worldCamera.GetComponent<MainCamera>():null;}
        public void SelectFloor(int floor)
        {
            if(floor<0 || floor>GridSystem.TopFloor || building==null)return;
            building.GetComponent<BuildEditingController>()?.CancelSelection();
            building.CancelFloorStroke();ActiveFloor=floor;
            cameraInput?.SetConstructionElevation(building.grid.FloorElevation(floor));
            UpdateOverlay();RefreshVisibility();
        }
        public void SetCeiling(bool ceiling)
        {PaintCeiling=ceiling;building.CancelFloorStroke();UpdateOverlay();RefreshVisibility();}
        public void ToggleRemoval()
        {
            if(building.SelectedItem==null || building.SelectedItem.placementKind!=PlacementKind.Floor)
            {
                var item=building.floorPainter.FirstSurface;if(item==null)return;
                building.EnterBuildMode(item);EraseSurface=true;
            }
            else EraseSurface=!EraseSurface;
            building.CancelFloorStroke();
        }
        public void SetPaintMode(){EraseSurface=false;}
        private void Update()
        {
            if(building==null)return;
            var focused=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            bool typing=focused!=null && focused.GetComponent<TMP_InputField>()!=null;
            if(building.mode!=BuildMode.Normal && !typing)
            {
                if(Input.GetKeyDown(KeyCode.UpArrow))SelectFloor(ActiveFloor+1);
                if(Input.GetKeyDown(KeyCode.DownArrow))SelectFloor(ActiveFloor-1);
            }
            if(overlayLayer!=SurfaceLayer)UpdateOverlay();
            if(Time.unscaledTime>=nextRefresh){RefreshVisibility();nextRefresh=Time.unscaledTime+.25f;}
        }
        private void UpdateOverlay()
        {
            overlayLayer=SurfaceLayer;
            if(building.gridOverlay==null)return;
            building.gridOverlay.ActiveFloor=SurfaceLayer;
            if(building.mode!=BuildMode.Normal)building.gridOverlay.Show();
        }
        public void RefreshVisibility()
        {
            foreach(var pair in hidden)if(pair.Key!=null)pair.Key.enabled=pair.Value;
            hidden.Clear();building.floorPainter?.SetVisibleThroughLayer(SurfaceLayer);
            foreach(var placed in FindObjectsOfType<PlacedObject>().Where(p=>p.GetComponentInParent<BuildPreview>()==null && p.anchorCell.y>ActiveFloor))
                foreach(var renderer in placed.GetComponentsInChildren<Renderer>())
                {hidden[renderer]=renderer.enabled;renderer.enabled=false;}
            foreach(var character in FindObjectsOfType<ShopCharacter>().Where(c=>building.grid.WorldToCell(c.transform.position).y>ActiveFloor))
                foreach(var renderer in character.GetComponentsInChildren<Renderer>())
                {hidden[renderer]=renderer.enabled;renderer.enabled=false;}
        }
        private void OnDisable()
        {foreach(var pair in hidden)if(pair.Key!=null)pair.Key.enabled=pair.Value;hidden.Clear();}
    }
}
