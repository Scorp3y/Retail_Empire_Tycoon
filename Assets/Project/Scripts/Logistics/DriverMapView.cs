using RetailEmpireTycoon.City;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEngine;

namespace RetailEmpireTycoon.Logistics
{
    /// <summary>A live view of the same vehicle travelling in the Game world, never a simulated marker timer.</summary>
    public sealed class DriverMapView:MonoBehaviour
    {
        private DriverDelivery delivery;
        private CityMapGraphic map;
        private TMP_Text status;
        public static void Open(DriverDelivery driver,Transform canvas)
        {
            var existing=canvas.GetComponentInChildren<DriverMapView>();if(existing!=null)return;
            var ui=new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            var panel=ui.Modal("Driver tracking",canvas,new Vector2(960,730));
            var view=panel.gameObject.AddComponent<DriverMapView>();view.delivery=driver;
            ui.Heading(panel,"Driver tracking","Driver tracking",new Vector2(24,-18),new Vector2(820,45));
            ui.Button(panel,"×",new Vector2(890,-18),new Vector2(44,44),()=>Destroy(panel.gameObject));
            var area=ui.Rect("Live driver map",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-85),new Vector2(912,548));
            view.map=area.gameObject.AddComponent<CityMapGraphic>();view.map.town=driver.town;view.map.driver=driver;view.map.raycastTarget=false;
            view.status=ui.Label(panel,"",new Vector2(24,-647),new Vector2(912,56),18);
            ui.Button(panel,"Watch vehicle",new Vector2(720,-18),new Vector2(156,44),()=>{DriverFollowView.Open(driver,canvas);Destroy(panel.gameObject);});
        }
        private void Update()
        {
            if(delivery==null){Destroy(gameObject);return;}
            map.SetVerticesDirty();status.text=delivery.Status+"\nBlue — driver · Purple — road route · Yellow — suppliers · Red — private parking";
            if(Input.GetKeyDown(KeyCode.Escape))Destroy(gameObject);
        }
    }
}
