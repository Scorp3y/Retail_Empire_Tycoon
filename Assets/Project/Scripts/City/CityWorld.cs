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
        private PlayerVehicleFleet vehicleFleet;
        private readonly System.Collections.Generic.List<(DeliveryCheckpoint Point,Vector3 Gate,Quaternion Rotation,float Depth)> unloadingGates=new();
        private Vector3 storeEnvelopeCenter;

        public void Enter(CityTrip trip, DeliveryOrders orders, Scene shopScene)
        {
            this.trip = trip; this.orders = orders;
            vehicleFleet=trip.Fleet;
            vehicleFleet.Changed += ApplySelectedVehicle;
            ApplySelectedVehicle();
            if(town!=null)
            {
                town.GetComponent<TownSignalChunks>()?.SetObserver(pickup.transform);
                town.GetComponentInChildren<TownRoadTraffic>()?.SetObserver(pickup.transform);
                town.GetComponentInChildren<TownPedestrianTraffic>()?.SetObserver(pickup.transform);
            }
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
            checkpoints=(town!=null?town.suppliers.Concat(town.showroomEntrances??Array.Empty<DeliveryCheckpoint>()):Array.Empty<DeliveryCheckpoint>())
                .Concat(new[]{depot,home}).Concat(unloadingGates.Select(gate=>gate.Point)).Where(c=>c!=null).Distinct().ToArray();
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
            nearest = checkpoints.Where(c=>c.CanUse(pickup)).OrderBy(c=>c.DistanceTo(pickup)).FirstOrDefault();
            var nearby=checkpoints.Where(c=>c.DistanceTo(pickup)<14).OrderBy(c=>c.DistanceTo(pickup)).FirstOrDefault();
            string dockingHint=nearby!=null&&!nearby.CanUse(pickup)?nearby.UnavailableReason(pickup):string.Empty;
            interact.gameObject.SetActive(nearest != null && panel == null);
            if (nearest != null)
                interact.GetComponentInChildren<TMP_Text>().text = nearest.kind == CheckpointKind.Depot ? "E — "+SupplierRouting.Name(nearest.supplier)
                    : nearest.kind == CheckpointKind.Showroom ? "E — View vehicles"
                    : nearest.kind == CheckpointKind.Unload ? "E — Unload vehicle" : "E — Return to store";
            status.text = $"{trip.Fleet.SelectedName}: {orders.PickupWeightKg:0.#} / {orders.pickupCapacityKg:0} kg • {orders.PickupVolumeM3:0.##} / {orders.pickupCapacityM3:0.#} m³\n"
                + $"Store: {Vector3.Distance(pickup.transform.position,home.transform.position):0} m • M — town map\n"
                + "WASD — drive · Space — brake · Mouse — camera · V — rear view · Esc — cursor\n"
                + (GetComponent<CityRiverRecovery>()?.ShowRecoveryNotice==true?"The pickup was recovered to the riverbank. Cargo preserved.":orders.NoticeIsError?orders.Notice:dockingHint);
            if (panel == null && Input.GetKeyDown(KeyCode.E)) Interact();
            if (panel != null && Input.GetKeyDown(KeyCode.Escape)) ClosePanel();
        }
        public void Interact()
        {
            // Resolve from the current pose: button/input can arrive before the next world Update.
            if(checkpoints!=null)nearest=checkpoints.Where(c=>c.CanUse(pickup)).OrderBy(c=>c.DistanceTo(pickup)).FirstOrDefault();
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
                panel = ui.Modal("Return to store",canvasRoot,new Vector2(500,220));
                ui.Heading(panel,"Return to the store?","Return to the store?",new Vector2(22,-18),new Vector2(454,45));
                ui.Label(panel,"Cargo will remain in the pickup. Use the unloading gate to deliver it.",new Vector2(22,-72),new Vector2(454,60));
                ui.Button(panel,"Cancel","Cancel",new Vector2(22,-156),new Vector2(208,42),ClosePanel);
                ui.Button(panel,"Return","Return",new Vector2(260,-156),new Vector2(218,42),()=>{ ClosePanel(); trip.Return(); });
            }
        }
        private void ShowDepot()
        {
            if (panel != null) ui.CloseModal(panel);
            panel = ui.Modal("Supplier",canvasRoot,new Vector2(760,660));
            ui.Heading(panel,SupplierRouting.Name(activeSupplier.supplier),SupplierRouting.Name(activeSupplier.supplier),new Vector2(24,-18),new Vector2(660,44));
            ui.Button(panel,"×",new Vector2(694,-14),new Vector2(42,42),ClosePanel);
            ui.Button(panel,"Browse supplies",new Vector2(24,-73),new Vector2(340,40),()=>{buying=true;page=0;ShowDepot();});
            ui.Button(panel,"My orders",new Vector2(386,-73),new Vector2(340,40),()=>{buying=false;page=0;ShowDepot();});
            float awaiting=orders.Ledger.Entries.Where(e=>e.location==DeliveryLocation.Depot&&orders.SupplierFor(e)==activeSupplier.supplier).Sum(e=>orders.UnitWeight(e)*e.quantity);
            ui.Label(panel,$"At supplier: {awaiting:0.#} kg • Pickup: {orders.PickupWeightKg:0.#} / {orders.pickupCapacityKg:0} kg\nVolume: {orders.PickupVolumeM3:0.##} / {orders.pickupCapacityM3:0.#} m³. Load each order separately.",
                new Vector2(24,-121),new Vector2(712,58),18);
            if(buying) {ShowAssortment();return;}
            var entries = orders.Ledger.Entries.Where(e=>e.location==DeliveryLocation.Depot&&orders.SupplierFor(e)==activeSupplier.supplier).ToArray();
            int pages = Mathf.Max(1,Mathf.CeilToInt(entries.Length / 6f)); page = Mathf.Clamp(page,0,pages-1);
            if (entries.Length == 0) ui.Label(panel,"You have no orders here. Browse supplies or select another supplier on the map.",new Vector2(24,-194),new Vector2(712,70));
            var current = entries.Skip(page*6).Take(6).ToArray();
            for (int i = 0; i < current.Length; i++)
            {
                var entry = current[i]; int package = Mathf.Min(orders.PackageQuantity(entry),entry.quantity);
                float y = -193 - i*54;
                ui.Label(panel,$"{orders.ItemName(entry)} ×{entry.quantity}\nPackage: {package} units • {orders.UnitWeight(entry)*package:0.#} kg",new Vector2(24,y),new Vector2(510,48),17);
                ui.Button(panel,"Load","Load",new Vector2(558,y),new Vector2(176,40),()=>{ orders.LoadFrom(entry,activeSupplier.supplier); ShowDepot(); });
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
                    ui.Label(panel,$"{ShopText.Item(product)} • {product.BoxAmount} units per package\n${product.BuyPrice} • {product.UnitWeightKg*product.BoxAmount:0.#} kg",new Vector2(24,y),new Vector2(510,48),17);
                    ui.Button(panel,"Order",new Vector2(558,y),new Vector2(176,40),()=>{orders.Order(product);ShowDepot();});
                }
                else
                {
                    var building=buildings[index-products.Length];
                    ui.Label(panel,$"{ShopText.Item(building)}\n${building.price} • {building.weightKg:0.#} kg",new Vector2(24,y),new Vector2(510,48),17);
                    var button=ui.Button(panel,"Order",new Vector2(558,y),new Vector2(176,40),()=>{orders.Order(building);ShowDepot();});
                    button.interactable=orders.money.CanSpend(building.price);
                }
            }
            SupplierFooter(pages);
        }
        private void SupplierFooter(int pages)
        {
            ui.Label(panel,orders.Notice??"An order reserves supplies here. Load it into the pickup to collect it.",new Vector2(24,-525),new Vector2(712,52),17);
            ui.Button(panel,"←",new Vector2(24,-599),new Vector2(100,42),()=>{page--;ShowDepot();});
            ui.Label(panel,$"{page+1} / {pages}",new Vector2(330,-603),new Vector2(100,35),19);
            ui.Button(panel,"→",new Vector2(634,-599),new Vector2(100,42),()=>{page++;ShowDepot();});
        }
        private void ShowShowroom()
        {
            if (panel != null) ui.CloseModal(panel);
            var fleet = trip.Fleet;
            panel=ui.Modal("Vehicle showroom",canvasRoot,new Vector2(880,790));
            ui.Heading(panel,"Vehicle showroom", "Vehicle showroom",new Vector2(24,-18),new Vector2(730,44));
            ui.Button(panel,"×",new Vector2(812,-18),new Vector2(42,42),ClosePanel);
            ui.Label(panel,$"Balance: ${orders.money.Money:N0} · Selected: {fleet.SelectedName}\nUnload before exchanging vehicles. Owned vehicles remain in your garage.",new Vector2(24,-73),new Vector2(832,56),18);
            VehicleRow(null,0);
            for (int i=0;i<fleet.Catalog.Count;i++) VehicleRow(fleet.Catalog[i],i+1);
            ui.Label(panel,fleet.Notice??"Inspect all five upgrades here. Each tier carries more weight and volume.",new Vector2(24,-718),new Vector2(832,50),18);
        }
        private void VehicleRow(PlayerVehicleDefinition definition,int index)
        {
            var fleet=trip.Fleet;string id=definition!=null?definition.id:PlayerVehicleFleet.StarterId;
            float y=-146-index*92;
            if(definition!=null&&definition.icon!=null)
            {
                var rect=ui.Rect("Vehicle preview",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(24,y),new Vector2(116,82));
                var preview=rect.gameObject.AddComponent<Image>();preview.sprite=definition.icon;preview.preserveAspect=true;
            }
            string name=definition!=null?definition.displayName:"Delivery pickup";
            float kg=definition!=null?definition.payloadKg:350, volume=definition!=null?definition.cargoM3:2;
            ui.Label(panel,$"{name}\n{kg:0} kg · {volume:0} m³",new Vector2(156,y-6),new Vector2(420,74),21);
            bool selected=fleet.SelectedId==id, owned=fleet.Owns(id);
            string label=selected?"Selected":owned?"Select":$"Buy · ${definition.price:N0}";
            var button=ui.Button(panel,label,new Vector2(606,y-15),new Vector2(246,48),()=>
            {
                if(owned)fleet.Select(id);else fleet.BuyAndSelect(id);
                ShowShowroom();
            });
            button.interactable=!selected&&(owned||orders.money.CanSpend(definition.price));
        }
        private void ApplySelectedVehicle()
        {
            pickup.SetVehicle(trip.Fleet.Selected);
            chaseCamera.distance=Mathf.Max(8,(trip.Fleet.Selected!=null?trip.Fleet.Selected.length:4.4f)*1.15f);
            RefreshCargo();
            RefreshUnloadingZones();
        }
        private void OnDestroy()
        {
            if(vehicleFleet!=null)vehicleFleet.Changed-=ApplySelectedVehicle;
            if(orders!=null)orders.Changed-=RefreshCargo;
        }
        private void ClosePanel() { ui.CloseModal(panel); panel = null; pickup.InputEnabled = true;chaseCamera.SetInteractionBlocked(false); }
        private void RefreshCargo()
        {
            if (orders == null || cargoRoot == null) return;
            for (int i = cargoRoot.childCount-1;i>=0;i--) { cargoRoot.GetChild(i).gameObject.SetActive(false); Destroy(cargoRoot.GetChild(i).gameObject); }
            // Enclosed cargo vehicles keep their load inside the body; only the starting open pickup shows crates.
            if(trip.Fleet.Selected!=null)return;
            int count = Mathf.Clamp(Mathf.CeilToInt(orders.PickupVolumeM3/.18f),0,12);
            for (int i=0;i<count;i++)
            {
                var box=Instantiate(cargoBoxPrefab,cargoRoot); box.transform.localPosition = new Vector3((i%2-.5f)*.65f,(i/6)*.48f,(i/2%3)*.5f);
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
            var walls = shopScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<StoreWallOccluder>(true))
                .Where(w=>trip.WasShopObjectActive(w.gameObject)).ToArray();
            var area=shopScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RetailEmpireTycoon.Territory.StoreBuildArea>(true)).FirstOrDefault();
            Bounds bounds = walls.Length>0 ? walls[0].WorldBounds : area!=null && area.areaRects.Count>0
                ? area.areaRects[0].bounds : new Bounds(Vector3.zero,new Vector3(3,.8f,3));
            foreach(var wall in walls.Skip(1)) bounds.Encapsulate(wall.WorldBounds);
            Vector3 originalCenter = useFixedShopOrigin?sourceShopOrigin:new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            // Both views must use the authored town mapping. Auto-fitting a growing shop to 28 m shrank it.
            var layout=town!=null?town.layout:null;
            float scale=layout!=null?layout.shopToCityScale:shopScale;
            Vector3 geometryScale=layout!=null?layout.ShopVisualScale:Vector3.one*scale;
            if(layout!=null){originalCenter=layout.sourceShopOrigin;shopCenter=layout.cityShopOrigin;}
            Vector3 Map(Vector3 point) => shopCenter + Vector3.Scale(point-originalCenter,geometryScale);
            var doorFilters=shopScene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<PlacedObject>(true))
                .Where(placed=>placed.item!=null && placed.item.isDoorway && !placed.item.isUnloadingGate && placed.GetComponentInParent<BuildPreview>()==null)
                .SelectMany(placed=>placed.GetComponentsInChildren<MeshFilter>(true));
            var contents=shopScene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<PlacedObject>(true))
                .Where(placed=>placed.item!=null && !placed.item.isWall && !placed.item.isDoorway && !placed.item.isUnloadingGate
                    && !placed.item.isParkingSpace && placed.GetComponentInParent<BuildPreview>()==null)
                .SelectMany(placed=>placed.GetComponentsInChildren<MeshFilter>(true))
                .Where(filter=>trip.WasShopObjectActive(filter.gameObject) && !filter.name.StartsWith("Highlight_Tube_",StringComparison.Ordinal)
                    && filter.GetComponentInParent<ArrowBounce>()==null);
            var filters = walls.SelectMany(w=>w.GetComponentsInChildren<MeshFilter>(true)).Concat(doorFilters).Concat(contents).Distinct().ToArray();
            foreach(var filter in filters)
            {
                var source = filter.GetComponent<MeshRenderer>(); if(source==null || filter.sharedMesh==null) continue;
                var copy = new GameObject("Store facade "+filter.name,typeof(MeshFilter),typeof(MeshRenderer));
                copy.transform.SetParent(facadeRoot,false); copy.transform.position=Map(filter.transform.position);
                copy.transform.rotation=filter.transform.rotation; copy.transform.localScale=Vector3.Scale(filter.transform.lossyScale,geometryScale);
                copy.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh; copy.GetComponent<MeshRenderer>().sharedMaterials=source.sharedMaterials;
            }
            // The facade is visual-only. A solid core seals all doors and prevents entering the shop in the city.
            var core = new GameObject("Shop interior is closed",typeof(BoxCollider)); core.transform.SetParent(facadeRoot,false);
            core.transform.position=Map(bounds.center); var collider=core.GetComponent<BoxCollider>(); collider.size=Vector3.Scale(bounds.size,geometryScale);
            collider.size=new Vector3(collider.size.x,Mathf.Max(3,collider.size.y),collider.size.z);
            // Only player-built surfaces are copied below. A free automatic roof would contradict the saved construction.
            // The return point is the very same parking spot, transformed into city scale, not an unrelated spawn.
            if(trip.parkedPickup!=null)
            {
                var position=Map(trip.parkedPickup.transform.position);position.y=.04f;
                home.transform.position=position;pickup.Body.position=position+Vector3.up*.08f;
                pickup.Body.rotation=trip.parkedPickup.transform.rotation;pickup.Stop();
            }
            foreach(var floors in shopScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FloorPainter>(true)))
                foreach(var filter in floors.Surfaces) CopyExterior(filter,Map,geometryScale);
            foreach(var barrier in shopScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TerritoryConstructionBarrier>(true)))
                foreach(var filter in barrier.GetComponentsInChildren<MeshFilter>(true))
                    if(trip.WasShopObjectActive(filter.gameObject))CopyExterior(filter,Map,geometryScale);
            foreach(var parking in shopScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlacedObject>(true)).Where(p=>p.item!=null&&p.item.isParkingSpace&&p.GetComponentInParent<BuildPreview>()==null))
                foreach(var filter in parking.GetComponentsInChildren<MeshFilter>(true)) CopyExterior(filter,Map,geometryScale);
            var gates = shopScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlacedObject>(true))
                .Where(p=>p.item!=null&&p.item.isUnloadingGate&&p.anchorCell.y==0&&p.GetComponentInParent<BuildPreview>()==null&&trip.WasShopObjectActive(p.gameObject)).ToArray();
            storeEnvelopeCenter=Map(bounds.center);
            unloadingGates.Clear();unload.gameObject.SetActive(gates.Length>0);
            // Older City scenes may still contain the yellow loading slab. Interaction needs no visible platform.
            foreach(var renderer in unload.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            for(int index=0;index<gates.Length;index++)
            {
                var gate=gates[index];
                var point=index==0?unload:new GameObject("Invisible store unloading zone "+(index+1)).AddComponent<DeliveryCheckpoint>();
                if(index>0)point.transform.SetParent(transform,false);
                point.kind=CheckpointKind.Unload;point.radius=2.5f;point.requiresRearDocking=true;point.displayName="Store unloading gate";
                unloadingGates.Add((point,Map(gate.transform.position),gate.transform.rotation,gate.item.placementBounds.size.z*geometryScale.z));
                foreach(var filter in gate.GetComponentsInChildren<MeshFilter>(true)) CopyExterior(filter,Map,geometryScale);
            }
            RefreshUnloadingZones();
        }
        /// <summary>Positions the vehicle centre so its rear faces the gate with clearance; the gate's normal defines the approach.</summary>
        public static (Vector3 Position,Quaternion Rotation) UnloadingPose(Vector3 gate,Quaternion rotation,Vector3 shopCenter,float gateDepth,float vehicleLength)
        {
            var normal=Vector3.ProjectOnPlane(rotation*Vector3.forward,Vector3.up).normalized;
            var outside=gate-shopCenter;outside.y=0;
            if(Vector3.Dot(normal,outside)<0)normal=-normal;
            var position=gate+normal*(gateDepth*.5f+vehicleLength*.5f+.65f);position.y=.05f;
            return (position,Quaternion.LookRotation(normal));
        }
        private void RefreshUnloadingZones()
        {
            if(pickup==null)return;
            var body=pickup.GetComponent<BoxCollider>();float length=body!=null?body.size.z*pickup.transform.lossyScale.z:4.4f;
            foreach(var gate in unloadingGates)
            {
                var pose=UnloadingPose(gate.Gate,gate.Rotation,storeEnvelopeCenter,gate.Depth,length);
                gate.Point.transform.SetPositionAndRotation(pose.Position,pose.Rotation);
            }
        }
        private void CopyExterior(MeshFilter filter,Func<Vector3,Vector3> map,Vector3 scale)
        {
            var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||filter.sharedMesh==null)return;
            var copy=new GameObject("Shop exterior "+filter.name,typeof(MeshFilter),typeof(MeshRenderer));copy.transform.SetParent(facadeRoot,false);
            copy.transform.position=map(filter.transform.position);copy.transform.rotation=filter.transform.rotation;copy.transform.localScale=Vector3.Scale(filter.transform.lossyScale,scale);
            copy.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;copy.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
            var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);copy.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
            copy.GetComponent<MeshRenderer>().shadowCastingMode=renderer.shadowCastingMode;
        }
    }
}
