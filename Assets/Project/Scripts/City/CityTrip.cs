using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace RetailEmpireTycoon.City
{
    /// <summary>Keeps the shop scene resident but inactive during a trip. The same stock and employees resume on return.</summary>
    public sealed class CityTrip : MonoBehaviour
    {
        public DeliveryOrders orders;
        public GameObject parkedPickup;
        public Transform uiRoot;
        public GameplayControls controls;
        public WorkMinigame work;
        public string cityScene = "City";
        public string loadingScene = "Loading";
        public bool Transitioning { get; private set; }
        public bool InCity { get; private set; }
        private readonly Dictionary<GameObject,bool> shopRoots = new Dictionary<GameObject,bool>();
        private Scene shopScene;
        private float shopTimeScale;
        private RectTransform confirmation;
        private ShopUi ui;
        private bool trafficEnabled;
        public bool WasShopObjectActive(GameObject instance)
        {
            if(instance==null)return false;
            var current=instance.transform;
            while(current.parent!=null)
            {
                if(!current.gameObject.activeSelf)return false;
                current=current.parent;
            }
            return shopRoots.TryGetValue(current.gameObject,out bool active)?active:current.gameObject.activeSelf;
        }
        private void Awake() { shopScene = gameObject.scene; ui = new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme")); }
        private void Update()
        {
            if (InCity || Transitioning || parkedPickup == null || confirmation != null) return;
            if (controls != null && (controls.LegacyWindowVisible || controls.IsConstructionActive)) return;
            if (work != null && work.IsActive) return;
            if (!Input.GetMouseButtonDown(0) || Camera.main == null || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit, 1000)
                && (hit.transform == parkedPickup.transform || hit.transform.IsChildOf(parkedPickup.transform))) ConfirmDeparture();
        }
        public void ConfirmDeparture()
        {
            if (Transitioning || InCity || confirmation != null) return;
            confirmation = ui.Modal("Поездка в город", uiRoot, new Vector2(480,240));
            ui.Heading(confirmation,"Поехать в город?","Drive to the city?",new Vector2(22,-18),new Vector2(430,40),26);
            ui.Copy(confirmation,"Посетите поставщиков в разных районах и заберите свои заказы. Магазин будет приостановлен до возвращения.",
                "Visit district suppliers and collect your orders. The store pauses until you return.",new Vector2(22,-68),new Vector2(430,80));
            ui.Button(confirmation,"Отмена","Cancel",new Vector2(22,-177),new Vector2(200,40),CloseConfirmation);
            ui.Button(confirmation,"Поехать","Drive",new Vector2(250,-177),new Vector2(208,40),Begin);
        }
        private void CloseConfirmation() { ui.CloseModal(confirmation); confirmation = null; }
        public void Begin()
        {
            if (InCity || Transitioning) return;
            if (!Application.CanStreamedLevelBeLoaded(cityScene) || !Application.CanStreamedLevelBeLoaded(loadingScene))
            { Debug.LogError("City and Loading scenes must be enabled in Build Settings.",this); return; }
            // Save before suspending the scene; a failed save must not strand the player in transit.
            orders.save?.SaveGame(); CloseConfirmation(); controls?.CloseWindows();
            shopTimeScale = Time.timeScale; Time.timeScale = 1;
            shopRoots.Clear(); foreach (var root in shopScene.GetRootGameObjects())
            {
                if (root == gameObject) continue;
                shopRoots[root] = root.activeSelf; root.SetActive(false);
            }
            var traffic=GetComponent<RetailEmpireTycoon.Parking.ParkingTraffic>();
            if(traffic!=null) {trafficEnabled=traffic.enabled;traffic.enabled=false;}
            Transitioning = true; StartCoroutine(GuardTransition(Travel(true)));
        }
        public void Return()
        {
            if (!InCity || Transitioning) return;
            orders.save?.SaveGame(); Transitioning = true; StartCoroutine(GuardTransition(Travel(false)));
        }
        private IEnumerator GuardTransition(IEnumerator transition)
        {
            while(true)
            {
                bool more; object next;
                try { more=transition.MoveNext();next=more?transition.Current:null; }
                catch(Exception error) {Debug.LogException(error,this);StartCoroutine(Recover());yield break;}
                if(!more) yield break;
                yield return next;
            }
        }
        private IEnumerator Recover()
        {
            var city=SceneManager.GetSceneByName(cityScene);
            if(city.isLoaded) {var remove=SceneManager.UnloadSceneAsync(city);while(!remove.isDone) yield return null;}
            var loading=SceneManager.GetSceneByName(loadingScene);
            if(loading.isLoaded) {var remove=SceneManager.UnloadSceneAsync(loading);while(!remove.isDone) yield return null;}
            SceneManager.SetActiveScene(shopScene);
            foreach(var pair in shopRoots) if(pair.Key!=null) pair.Key.SetActive(pair.Value);
            InCity=false;Transitioning=false;Time.timeScale=shopTimeScale;
            var traffic=GetComponent<RetailEmpireTycoon.Parking.ParkingTraffic>();if(traffic!=null) traffic.enabled=trafficEnabled;
            Debug.LogError("Поездка не удалась. Магазин восстановлен, оплаченные заказы и груз сохранены.",this);
        }
        private IEnumerator Travel(bool outbound)
        {
            var worldScene = SceneManager.GetSceneByName(cityScene);
            if (!outbound)
                foreach (var root in worldScene.GetRootGameObjects()) root.SetActive(false);
            var loadingOperation = SceneManager.LoadSceneAsync(loadingScene,LoadSceneMode.Additive);
            while (!loadingOperation.isDone) yield return null;
            var loading = SceneManager.GetSceneByName(loadingScene);
            var loader = loading.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LoadingManager>(true)).First();
            float start = Time.realtimeSinceStartup;
            if (outbound)
            {
                var operation = SceneManager.LoadSceneAsync(cityScene,LoadSceneMode.Additive);
                operation.allowSceneActivation = false;
                while (operation.progress < .9f || Time.realtimeSinceStartup - start < .8f)
                { loader.SetProgress(Mathf.Clamp01(operation.progress / .9f)); yield return null; }
                operation.allowSceneActivation = true; while (!operation.isDone) yield return null;
                worldScene = SceneManager.GetSceneByName(cityScene); SceneManager.SetActiveScene(worldScene);
                var city = worldScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CityWorld>(true)).Single();
                city.Enter(this,orders,shopScene); InCity = true;
            }
            else
            {
                var operation = SceneManager.UnloadSceneAsync(worldScene);
                while (!operation.isDone || Time.realtimeSinceStartup - start < .6f)
                { loader.SetProgress(operation.progress); yield return null; }
                SceneManager.SetActiveScene(shopScene);
                foreach (var pair in shopRoots) if (pair.Key != null) pair.Key.SetActive(pair.Value);
                InCity = false; Time.timeScale = shopTimeScale;
                var traffic=GetComponent<RetailEmpireTycoon.Parking.ParkingTraffic>();if(traffic!=null) traffic.enabled=trafficEnabled;
            }
            loader.SetProgress(1); yield return null;
            var unload = SceneManager.UnloadSceneAsync(loading); while (!unload.isDone) yield return null;
            Transitioning = false;
        }
    }
}
