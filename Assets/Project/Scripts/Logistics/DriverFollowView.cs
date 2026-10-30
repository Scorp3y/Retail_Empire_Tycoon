using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEngine;

namespace RetailEmpireTycoon.Logistics
{
    /// <summary>Temporarily observes the real employee/vehicle beyond the store camera's construction bounds.</summary>
    public sealed class DriverFollowView:MonoBehaviour
    {
        private DriverDelivery driver;
        private Camera view;
        private MainCamera normal;
        private bool wasEnabled;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private TMP_Text status;
        public static void Open(DriverDelivery delivery,Transform canvas)
        {
            if(canvas.GetComponentInChildren<DriverFollowView>()!=null)return;
            delivery.trip.controls?.CloseWindows();
            var ui=new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            var strip=ui.Rect("Watch driver",canvas,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-76),new Vector2(700,68));ui.Panel(strip,ShopUiTheme.Paper);
            var follow=strip.gameObject.AddComponent<DriverFollowView>();follow.driver=delivery;follow.view=Camera.main;
            follow.normal=follow.view.GetComponent<MainCamera>();follow.wasEnabled=follow.normal!=null&&follow.normal.enabled;
            follow.savedPosition=follow.view.transform.position;follow.savedRotation=follow.view.transform.rotation;
            if(follow.normal!=null)follow.normal.enabled=false;
            var target=delivery.Vehicle!=null?delivery.Vehicle.transform:delivery.Employee!=null?delivery.Employee.transform:delivery.trip.parkedPickup.transform;
            var focus=target.position+Vector3.up*.55f;
            follow.view.transform.position=focus-Quaternion.Euler(28,target.eulerAngles.y,0)*Vector3.forward*6.5f;
            follow.view.transform.rotation=Quaternion.LookRotation(focus-follow.view.transform.position);
            follow.status=ui.Label(strip,"",new Vector2(16,-10),new Vector2(450,50),18);
            ui.Button(strip,"Back to store",new Vector2(490,-14),new Vector2(192,40),()=>Destroy(strip.gameObject));
        }
        private void LateUpdate()
        {
            if(driver==null || driver.trip.InCity || driver.trip.controls!=null&&driver.trip.controls.IsConstructionActive || Input.GetKeyDown(KeyCode.Escape)){Destroy(gameObject);return;}
            var target=driver.Vehicle!=null?driver.Vehicle.transform:driver.Employee!=null?driver.Employee.transform:driver.trip.parkedPickup.transform;
            var rotation=Quaternion.Euler(28,target.eulerAngles.y,0);
            var focus=target.position+Vector3.up*.55f;
            var position=focus-rotation*Vector3.forward*6.5f;
            float blend=1-Mathf.Exp(-5*Time.unscaledDeltaTime);
            view.transform.position=Vector3.Lerp(view.transform.position,position,blend);view.transform.rotation=Quaternion.Slerp(view.transform.rotation,Quaternion.LookRotation(focus-view.transform.position),blend);
            status.text="Driver · "+driver.Status;
        }
        private void OnDestroy()
        {
            if(view==null)return;view.transform.SetPositionAndRotation(savedPosition,savedRotation);
            if(normal!=null)normal.enabled=wasEnabled;
        }
    }
}
