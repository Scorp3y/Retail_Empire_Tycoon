using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.StoreOperations;
using TMPro;
using UnityEngine;

namespace RetailEmpireTycoon.UI.Shop
{
    public sealed class ConstructionGuide : MonoBehaviour
    {
        public BuildController building;
        public GameplayControls controls;
        public ShopOperationsHud operationsHud;
        public Transform uiRoot;
        private TMP_Text label;
        private void Start()
        {
            var ui=new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            var rect=ui.Rect("Construction guidance",uiRoot,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,14),new Vector2(730,74));
            ui.Panel(rect,ShopUiTheme.Paper);
            label=ui.Label(rect,"",new Vector2(16,-9),new Vector2(698,58),17);
            label.raycastTarget=false;
            rect.gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            if(label==null)return;
            bool hint=operationsHud!=null&&operationsHud.HintsVisible;
            bool error=building.HasPlacementPreview&&!building.PreviewResult.ok;
            bool visible=building.mode==BuildMode.Build&&(hint||error);
            label.transform.parent.gameObject.SetActive(visible);
            if(!visible)return;
            string rotation=controls.Key(ShopAction.RotateClockwise)+" / "+controls.Key(ShopAction.RotateCounterclockwise);
            string text=hint?ShopText.Get($"ЛКМ — поставить · {rotation} — повернуть · Esc — отменить",$"Click — place · {rotation} — rotate · Esc — cancel"):"";
            if(error)text+="\n"+BuildPreview.FailureText(building.PreviewResult);
            else if(hint&&building.SelectedItem!=null&&building.SelectedItem.category==BuildCategory.Shelf)
                text+="\n"+ShopText.Get("Голубые стрелки — подход покупателей. Оставьте его свободным.","Blue arrows show customer access. Keep this aisle clear.");
            else if(hint&&building.SelectedItem!=null&&building.SelectedItem.placementKind==PlacementKind.Floor)
                text+="\n"+ShopText.Get($"Плиток нужно: {building.FloorStrokeCellCount}. На складе: {building.inventory.GetCount(building.SelectedItem)}.",$"Tiles required: {building.FloorStrokeCellCount}. In storage: {building.inventory.GetCount(building.SelectedItem)}.");
            label.text=text.Trim();
        }
    }
}
