using System;
using System.Collections;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RetailEmpireTycoon.City
{
    /// <summary>The city view owns driving and proximity interactions, not stock or purchase rules.</summary>
    public sealed class CityWorld : MonoBehaviour
    {
        public PickupDrive pickup;
        public PickupCamera chaseCamera;
        public DeliveryCheckpoint depot, unload, home;
        public GameObject cargoBoxPrefab;
        public Transform cargoRoot;
        public Transform facadeRoot;
        public Vector3 shopCenter = new Vector3(-55,0,-50);
        public float shopScale = 6;
        public TownWorldPlaces town;
        public bool useFixedShopOrigin;
        public Vector3 sourceShopOrigin;
        public Material shopRoofMaterial;
        private CityTrip trip;
        private DeliveryOrders orders;
        private ShopUi ui;
        private RectTransform canvasRoot, panel;
        private TMP_Text status;
        private Button interact;
        private DeliveryCheckpoint nearest;
        private int page;
        private bool buying;
        private DeliveryCheckpoint activeSupplier;
        private DeliveryCheckpoint[] checkpoints;
        private CityMapHud map;

        public void Enter(CityTrip trip, DeliveryOrders orders, Scene shopScene)
        {
            this.trip = trip; this.orders = orders;
            checkpoints=(town!=null?town.suppliers:Array.Empty<DeliveryCheckpoint>()).Concat(new[]{depot,unload,home}).Where(c=>c!=null).Distinct().ToArray();
            orders.Changed += RefreshCargo;
            ui = new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            var canvas = new GameObject("City HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 20;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            canvasRoot = (RectTransform)canvas.transform;
            // Loading owns a temporary input system. City needs its own after that scene unloads.
            var events=new GameObject("City input");events.SetActive(false);
            events.transform.SetParent(transform,false);
            events.AddComponent<EventSystem>();events.AddComponent<StandaloneInputModule>();
            StartCoroutine(EnableCityInput(events.GetComponent<EventSystem>()));
            var header = ui.Rect("Trip status",canvasRoot,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-24),new Vector2(580,118));
            ui.Panel(header,ShopUiTheme.Paper); status = ui.Label(header,"",new Vector2(18,-12),new Vector2(544,94),19);
            interact = ui.Button(canvasRoot,"",new Vector2(740,-976),new Vector2(440,60),Interact);
            var buttonRect = (RectTransform)interact.transform; buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.5f,0);
            buttonRect.pivot = new Vector2(.5f,0); buttonRect.anchoredPosition = new Vector2(0,24);
            SyncFacade(shopScene); RefreshCargo();
            if(town!=null)map=new CityMapHud(ui,canvasRoot,town,pickup,home,chaseCamera,()=>panel==null&&!trip.Transitioning);
        }
        private IEnumerator EnableCityInput(EventSystem events)
        {
            while(trip.Transitioning) yield return null;
            events.gameObject.SetActive(true);
        }
        private void Update()
        {
            if (trip == null || trip.Transitioning || orders == null) return;
            map?.Tick();
            if(map!=null&&map.IsOpen){interact.gameObject.SetActive(false);return;}
            nearest = checkpoints.Where(c=>c.CanUse(pickup)).OrderBy(c=>Vector3.Distance(c.transform.position,pickup.transform.position)).FirstOrDefault();
            interact.gameObject.SetActive(nearest != null && panel == null);
            if (nearest != null)
                interact.GetComponentInChildren<TMP_Text>().text = nearest.kind == CheckpointKind.Depot ? "E — "+SupplierRouting.Name(nearest.supplier)
                    : nearest.kind == CheckpointKind.Showroom ? "E — Осмотреть автомобили"
                    : nearest.kind == CheckpointKind.Unload ? "E — Разгрузить пикап" : "E — Вернуться в магазин";
            status.text = $"Пикап: {orders.PickupWeightKg:0.#} / {orders.pickupCapacityKg:0} кг • {orders.PickupVolumeM3:0.##} / {orders.pickupCapacityM3:0.#} м³\n"
                + $"Магазин: {Vector3.Distance(pickup.transform.position,home.transform.position):0} м • M — карта районов\n"
                + "WASD — ехать · Пробел — тормоз · Мышь — камера · V — вид сзади · Esc — курсор\n" + (orders.NoticeIsError ? orders.Notice : "");
            if (panel == null && Input.GetKeyDown(KeyCode.E)) Interact();
            if (panel != null && Input.GetKeyDown(KeyCode.Escape)) ClosePanel();
        }
        public void Interact()
        {
            if (nearest == null || !nearest.CanUse(pickup) || panel != null || trip.Transitioning) return;
            pickup.Stop(); pickup.InputEnabled = false; page = 0;
            chaseCamera.SetInteractionBlocked(true);
            if (nearest.kind == CheckpointKind.Depot) {activeSupplier=nearest;buying=false;ShowDepot();}
            else if(nearest.kind==CheckpointKind.Showroom)ShowShowroom();
            else if (nearest.kind == CheckpointKind.Unload)
            {
                orders.Unload(); pickup.InputEnabled = true;chaseCamera.SetInteractionBlocked(false);
            }
            else
            {
                panel = ui.Modal("Возвращение",canvasRoot,new Vector2(500,220));
                ui.Heading(panel,"Вернуться в магазин?","Return to the store?",new Vector2(22,-18),new Vector2(454,45));
                ui.Label(panel,"Груз останется в пикапе. Для доставки используйте ворота разгрузки.",new Vector2(22,-72),new Vector2(454,60));
                ui.Button(panel,"Отмена","Cancel",new Vector2(22,-156),new Vector2(208,42),ClosePanel);
                ui.Button(panel,"Вернуться","Return",new Vector2(260,-156),new Vector2(218,42),()=>{ ClosePanel(); trip.Return(); });
            }
        }
        private void ShowDepot()
        {
            if (panel != null) ui.CloseModal(panel);
            panel = ui.Modal("Поставщик",canvasRoot,new Vector2(760,660));
            ui.Heading(panel,SupplierRouting.Name(activeSupplier.supplier),SupplierRouting.Name(activeSupplier.supplier),new Vector2(24,-18),new Vector2(660,44));
            ui.Button(panel,"×",new Vector2(694,-14),new Vector2(42,42),ClosePanel);
            ui.Button(panel,"Ассортимент",new Vector2(24,-73),new Vector2(340,40),()=>{buying=true;page=0;ShowDepot();});
            ui.Button(panel,"Мои заказы",new Vector2(386,-73),new Vector2(340,40),()=>{buying=false;page=0;ShowDepot();});
            float awaiting=orders.Ledger.Entries.Where(e=>e.location==DeliveryLocation.Depot&&orders.SupplierFor(e)==activeSupplier.supplier).Sum(e=>orders.UnitWeight(e)*e.quantity);
            ui.Label(panel,$"У поставщика: {awaiting:0.#} кг • Пикап: {orders.PickupWeightKg:0.#} / {orders.pickupCapacityKg:0} кг\nОбъём: {orders.PickupVolumeM3:0.##} / {orders.pickupCapacityM3:0.#} м³. Заказ нужно загрузить отдельно.",
                new Vector2(24,-121),new Vector2(712,58),18);
            if(buying) {ShowAssortment();return;}
            var entries = orders.Ledger.Entries.Where(e=>e.location==DeliveryLocation.Depot&&orders.SupplierFor(e)==activeSupplier.supplier).ToArray();
            int pages = Mathf.Max(1,Mathf.CeilToInt(entries.Length / 6f)); page = Mathf.Clamp(page,0,pages-1);
            if (entries.Length == 0) ui.Label(panel,"Здесь нет ваших заказов. Откройте ассортимент или выберите другого поставщика на карте.",new Vector2(24,-194),new Vector2(712,70));
            var current = entries.Skip(page*6).Take(6).ToArray();
            for (int i = 0; i < current.Length; i++)
            {
                var entry = current[i]; int package = Mathf.Min(orders.PackageQuantity(entry),entry.quantity);
                float y = -193 - i*54;
                ui.Label(panel,$"{orders.ItemName(entry)} ×{entry.quantity}\nУпаковка: {package} шт. • {orders.UnitWeight(entry)*package:0.#} кг",new Vector2(24,y),new Vector2(510,48),17);
                ui.Button(panel,"Загрузить","Load",new Vector2(558,y),new Vector2(176,40),()=>{ orders.LoadFrom(entry,activeSupplier.supplier); ShowDepot(); });
            }
            SupplierFooter(pages);
        }
        private void ShowAssortment()
        {
            var products=orders.productCatalog.Products.Where(p=>p!=null&&SupplierRouting.ForProduct(p.StorageType)==activeSupplier.supplier).ToArray();
            var buildings=orders.buildCatalog.Items.Where(b=>b!=null&&!b.hiddenFromShop&&SupplierRouting.ForBuilding(b)==activeSupplier.supplier).ToArray();
            int total=products.Length+buildings.Length,pages=Mathf.Max(1,Mathf.CeilToInt(total/6f));page=Mathf.Clamp(page,0,pages-1);
            for(int row=0;row<6&&page*6+row<total;row++)
            {
                int index=page*6+row;float y=-193-row*54;
                if(index<products.Length)
                {
                    var product=products[index];
                    ui.Label(panel,$"{ShopText.Item(product)} • упаковка {product.BoxAmount} шт.\n${product.BuyPrice} • {product.UnitWeightKg*product.BoxAmount:0.#} кг",new Vector2(24,y),new Vector2(510,48),17);
                    ui.Button(panel,"Заказать",new Vector2(558,y),new Vector2(176,40),()=>{orders.Order(product);ShowDepot();});
                }
                else
                {
                    var building=buildings[index-products.Length];
                    ui.Label(panel,$"{ShopText.Item(building)}\n${building.price} • {building.weightKg:0.#} кг",new Vector2(24,y),new Vector2(510,48),17);
                    var button=ui.Button(panel,"Заказать",new Vector2(558,y),new Vector2(176,40),()=>{orders.Order(building);ShowDepot();});
                    button.interactable=orders.money.CanSpend(building.price);
                }
            }
            SupplierFooter(pages);
        }
        private void SupplierFooter(int pages)
        {
            ui.Label(panel,orders.Notice??"Покупка резервирует заказ здесь; загрузка выдаёт его в кузов.",new Vector2(24,-525),new Vector2(712,52),17);
            ui.Button(panel,"←",new Vector2(24,-599),new Vector2(100,42),()=>{page--;ShowDepot();});
            ui.Label(panel,$"{page+1} / {pages}",new Vector2(330,-603),new Vector2(100,35),19);
            ui.Button(panel,"→",new Vector2(634,-599),new Vector2(100,42),()=>{page++;ShowDepot();});
        }
        private void ShowShowroom()
        {
            panel=ui.Modal("Автосалон",canvasRoot,new Vector2(630,370));
            ui.Heading(panel,"Автосалон — осмотр", "Vehicle showroom",new Vector2(24,-18),new Vector2(560,44));
            ui.Label(panel,"Автомобили выставлены на площадке: объезжайте их и рассматривайте камерой из своей машины.\n\nВаш пикап уже куплен и выбран. Грузоподъёмность: 350 кг, кузов: 2 м³. Покупка новых машин здесь пока не предусмотрена.",new Vector2(24,-89),new Vector2(582,200),20);
            ui.Button(panel,"Продолжить осмотр",new Vector2(24,-302),new Vector2(582,44),ClosePanel);
        }
        private void ClosePanel() { ui.CloseModal(panel); panel = null; pickup.InputEnabled = true;chaseCamera.SetInteractionBlocked(false); }
        private void RefreshCargo()
        {
            if (orders == null || cargoRoot == null) return;
            for (int i = cargoRoot.childCount-1;i>=0;i--) { cargoRoot.GetChild(i).gameObject.SetActive(false); Destroy(cargoRoot.GetChild(i).gameObject); }
            int count = Mathf.Clamp(Mathf.CeilToInt(orders.PickupVolumeM3/.18f),0,12);
            for (int i=0;i<count;i++)
            {
                var box=Instantiate(cargoBoxPrefab,cargoRoot); box.transform.localPosition = new Vector3((i%2-.5f)*.65f,(i/6)*.48f,(i/2%3)*.55f);
                box.transform.localRotation=Quaternion.identity;
            }
        }
        private void SyncFacade(Scene shopScene)
        {
            if(town!=null)
            {
                var progression=shopScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<StoreProgression>(true)).FirstOrDefault();
                if(progression!=null)town.GetComponentInChildren<TownExpansionScenery>()?.ApplyState(progression.State);
            }
            var walls = shopScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<StoreWallOccluder>(true)).ToArray();
            var area=shopScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RetailEmpireTycoon.Territory.StoreBuildArea>(true)).FirstOrDefault();
            Bounds bounds = walls.Length>0 ? walls[0].WorldBounds : area!=null && area.areaRects.Count>0
                ? area.areaRects[0].bounds : new Bounds(Vector3.zero,new Vector3(3,.8f,3));
            foreach(var wall in walls.Skip(1)) bounds.Encapsulate(wall.WorldBounds);
            Vector3 originalCenter = useFixedShopOrigin?sourceShopOrigin:new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            float scale=useFixedShopOrigin?shopScale:Mathf.Min(shopScale,28f/Mathf.Max(bounds.size.x,bounds.size.z));
            Vector3 Map(Vector3 point) => shopCenter + (point-originalCenter)*scale;
            var filters = walls.SelectMany(w=>w.GetComponentsInChildren<MeshFilter>(true)).Distinct().ToArray();
            foreach(var filter in filters)
            {
                var source = filter.GetComponent<MeshRenderer>(); if(source==null || filter.sharedMesh==null) continue;
                var copy = new GameObject("Store facade "+filter.name,typeof(MeshFilter),typeof(MeshRenderer));
                copy.transform.SetParent(facadeRoot,false); copy.transform.position=Map(filter.transform.position);
                copy.transform.rotation=filter.transform.rotation; copy.transform.localScale=filter.transform.lossyScale*scale;
                copy.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh; copy.GetComponent<MeshRenderer>().sharedMaterials=source.sharedMaterials;
            }
            // The facade is visual-only. A solid core seals all doors and prevents entering the shop in the city.
            var core = new GameObject("Shop interior is closed",typeof(BoxCollider)); core.transform.SetParent(facadeRoot,false);
            core.transform.position=Map(bounds.center); var collider=core.GetComponent<BoxCollider>(); collider.size=bounds.size*scale;
            collider.size=new Vector3(collider.size.x,Mathf.Max(3,collider.size.y),collider.size.z);
            if(shopRoofMaterial!=null)
            {
                var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.name="Closed store roof";roof.transform.SetParent(facadeRoot,false);
                roof.transform.position=Map(new Vector3(bounds.center.x,bounds.max.y,bounds.center.z))+Vector3.up*.08f;
                roof.transform.localScale=new Vector3(bounds.size.x*scale+.2f,.16f,bounds.size.z*scale+.2f);
                roof.GetComponent<MeshRenderer>().sharedMaterial=shopRoofMaterial;
            }
            // The return point is the very same parking spot, transformed into city scale, not an unrelated spawn.
            if(trip.parkedPickup!=null)
            {
                var position=Map(trip.parkedPickup.transform.position);position.y=.04f;
                home.transform.position=position;pickup.Body.position=position+Vector3.up*.08f;
                pickup.Body.rotation=trip.parkedPickup.transform.rotation;pickup.Stop();
            }
            foreach(var floors in shopScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FloorPainter>(true)))
                foreach(var filter in floors.AsphaltSurfaces) CopyExterior(filter,Map,scale);
            foreach(var barrier in shopScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TerritoryConstructionBarrier>(true)))
                foreach(var filter in barrier.GetComponentsInChildren<MeshFilter>(true))
                    if(trip.WasShopObjectActive(filter.gameObject))CopyExterior(filter,Map,scale);
            foreach(var parking in shopScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlacedObject>(true)).Where(p=>p.item!=null&&p.item.isParkingSpace&&p.GetComponentInParent<BuildPreview>()==null))
                foreach(var filter in parking.GetComponentsInChildren<MeshFilter>(true)) CopyExterior(filter,Map,scale);
            var gates = shopScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlacedObject>(true)).Where(p=>p.item!=null&&p.item.isUnloadingGate).ToArray();
            if(gates.Length>0)
            {
                var gate=gates[0]; Vector3 gatePoint=Map(gate.transform.position);
                Vector3 direction=gatePoint-Map(bounds.center);direction.y=0;
                if(direction.sqrMagnitude<.1f) direction=Vector3.forward;
                direction.Normalize();
                // Move the usable loading area outside the sealed shop hull, even if a gate faces inward.
                float extent=Mathf.Abs(direction.x)*collider.size.x*.5f+Mathf.Abs(direction.z)*collider.size.z*.5f;
                unload.transform.position=Map(bounds.center)+direction*(extent+6);
                unload.transform.position=new Vector3(unload.transform.position.x,.05f,unload.transform.position.z);
                foreach(var filter in gate.GetComponentsInChildren<MeshFilter>(true)) CopyExterior(filter,Map,scale);
            }
        }
        private void CopyExterior(MeshFilter filter,Func<Vector3,Vector3> map,float scale)
        {
            var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||filter.sharedMesh==null)return;
            var copy=new GameObject("Shop exterior "+filter.name,typeof(MeshFilter),typeof(MeshRenderer));copy.transform.SetParent(facadeRoot,false);
            copy.transform.position=map(filter.transform.position);copy.transform.rotation=filter.transform.rotation;copy.transform.localScale=filter.transform.lossyScale*scale;
            copy.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;copy.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
            copy.GetComponent<MeshRenderer>().shadowCastingMode=renderer.shadowCastingMode;
        }
        private void OnDestroy() { if(orders!=null) orders.Changed-=RefreshCargo; }
    }
}
