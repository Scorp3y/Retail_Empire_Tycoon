using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEngine;

namespace RetailEmpireTycoon.Logistics
{
    public sealed class DeliveryNotice : MonoBehaviour
    {
        public DeliveryOrders orders;
        public Transform uiRoot;
        private TMP_Text label;
        private float expires;
        private void Start()
        {
            var ui=new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            var rect=ui.Rect("Delivery notice",uiRoot,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,20),new Vector2(760,84));
            ui.Panel(rect,ShopUiTheme.Paper);label=ui.Label(rect,"",new Vector2(18,-10),new Vector2(724,64),18);
            rect.gameObject.SetActive(false);orders.Changed+=Refresh;
        }
        private void Refresh()
        {
            if(label==null) return;
            label.text=orders.Notice;label.transform.parent.gameObject.SetActive(orders.NoticeIsError&&!string.IsNullOrEmpty(orders.Notice));expires=Time.unscaledTime+7;
        }
        private void Update() {if(label!=null&&Time.unscaledTime>expires) label.transform.parent.gameObject.SetActive(false);}
        private void OnDestroy() { if(orders!=null) orders.Changed-=Refresh; }
    }
}
