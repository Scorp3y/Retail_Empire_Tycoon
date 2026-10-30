using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Parking;
using RetailEmpireTycoon.StoreOperations;
using UnityEngine;

namespace RetailEmpireTycoon.Logistics
{
    public enum DriverStage { Idle, WalkingToCar, Boarding, LeavingParking, JoiningRoad, Driving, SupplierAccess, Loading, ReturningParking, Unloading, Exiting }
    [Serializable] public sealed class DriverJourneySaveData
    {
        public bool active,returning;
        public SupplierKind supplier;
        public Vector3 position;
        public Quaternion rotation=Quaternion.identity;
        public DriverStage stage;
        public Vector3[] accessPath=Array.Empty<Vector3>();
        public float accessDistance;
    }

    /// <summary>Owns one hired driver's use of the selected player vehicle. Paid cargo remains in DeliveryOrders.
    /// Road travel is handed to the same traffic controller as civilian cars; no timed off-map delivery.</summary>
    [DefaultExecutionOrder(-70)]
    public sealed class DriverDelivery:MonoBehaviour
    {
        public CityTrip trip;
        public StoreOperations.StoreOperations shop;
        public DeliveryOrders orders;
        public TownWorldPlaces town;
        public TownRoadTraffic traffic;
        public ParkingTraffic parking;
        public GameObject starterVehicleModel;
        private ShopCharacter employee;
        private TrafficVehicle car;
        private DriverRoadRoute route;
        private DriverJourneySaveData resume;
        private TrafficPolyline privatePath;
        private Vector3[] privateSamples=Array.Empty<Vector3>();
        private float privateDistance,actionTime,retry;
        private int routeIndex;
        private bool returning,loadedAtStop;
        private Vector3 privateEnd,privateFinalHeading;
        private float privateFinalLength;
        private SupplierLoadingCourt Court=>town.GetComponentsInChildren<SupplierLoadingCourt>().FirstOrDefault(c=>c.checkpoint!=null&&c.checkpoint.supplier==supplier);
        private SupplierKind supplier;
        private Vector3 seatedFrom;
        public DriverStage Stage {get;private set;}
        public bool IsAway=>Stage!=DriverStage.Idle;
        public string Status {get;private set;}="Hire a driver to collect paid orders.";
        public string AccessObstacle {get;private set;}
        public Vector3 NativePosition=>car!=null?traffic.transform.InverseTransformPoint(car.transform.position):ParkPosition;
        public IReadOnlyList<Vector3> MapRoute=>route?.MapPoints()??Array.Empty<Vector3>();
        public ShopCharacter Employee=>employee;
        public TrafficVehicle Vehicle=>car;
        public Vector3 ToNative(Vector3 world)=>traffic.transform.InverseTransformPoint(world);
        public void OpenMap(Transform canvas)=>DriverMapView.Open(this,canvas);
        private Vector3 ParkPosition=>traffic.transform.InverseTransformPoint(trip.parkedPickup.transform.position);
        private Vector3 StreetEntry=>traffic.transform.InverseTransformPoint(parking.roadEntry.position);
        private Vector3 Driveway=>traffic.transform.InverseTransformPoint(parking.drivewayEntry.position);
        private Vector3 ParkingApproach
        {
            get
            {
                var forward=traffic.transform.InverseTransformDirection(trip.parkedPickup.transform.forward);
                if(Vector3.Dot(forward,Driveway-ParkPosition)<0)forward=-forward;
                return ParkPosition+forward*5;
            }
        }
        private void Start()
        {
            shop.Driver=this;route=new DriverRoadRoute(traffic.Network);
            // This is a selectable parked display, not the City physics chassis.
            foreach(var wheel in trip.parkedPickup.GetComponentsInChildren<WheelCollider>(true))wheel.enabled=false;
        }
        private void FixedUpdate()=>Advance(Time.fixedDeltaTime);
        public void Advance(float seconds)
        {
            if(seconds<=0 || trip.Transitioning || trip.InCity || !shop.ReadyForStaff)return;
            route??=new DriverRoadRoute(traffic.Network);
            if(resume!=null){var saved=resume;resume=null;Resume(saved);}
            bool hired=shop.Staff.Count(StaffRole.Driver)>0;
            if(employee==null && hired)CreateEmployee();
            if(!hired && !IsAway){if(employee!=null)Destroy(employee.gameObject);employee=null;return;}
            if(Stage==DriverStage.Idle)
            {
                retry-=seconds;
                if(retry>0)return;retry=2;
                if(!hired || !shop.IsOpen || !shop.StaffWorking || parking.grid==null || trip.parkedPickup==null)return;
                if(orders.PickupWeightKg>0){Status="Unload the player's cargo before dispatching the driver.";return;}
                if(orders.ShopWeightKg>=orders.shopCapacityKg){Status="Warehouse full. Free space before dispatch.";return;}
                if(!ChooseSupplier())return;
                if(!route.Plan(StreetEntry,SupplierStreet())){Status="No road route to this supplier.";return;}
                var door=trip.parkedPickup.transform.position-trip.parkedPickup.transform.right*.85f;
                if(!employee.MoveTo(door)){Status="The driver cannot reach the vehicle. Clear the walkway.";return;}
                trip.Fleet.AssignDriver(true);SetStage(DriverStage.WalkingToCar,"Walking to the vehicle");
            }
            else if(Stage==DriverStage.WalkingToCar)
            {
                employee.Advance(seconds);
                if(employee.Arrived){seatedFrom=employee.transform.position;employee.Stop();employee.SetBrowsing(true);SetStage(DriverStage.Boarding,"Boarding the vehicle");}
                else if(employee.IsBlocked){trip.Fleet.AssignDriver(false);SetStage(DriverStage.Idle,"Vehicle walkway blocked. Waiting.");}
            }
            else if(Stage==DriverStage.Boarding)
            {
                actionTime+=seconds;
                var seat=trip.parkedPickup.transform.position+Vector3.up*.25f;
                employee.transform.position=Vector3.Lerp(seatedFrom,seat,Mathf.Clamp01(actionTime/1.2f));
                if(actionTime<1.2f)return;
                employee.gameObject.SetActive(false);CreateCar(ParkPosition,trip.parkedPickup.transform.rotation);
                returning=false;BeginPrivate(new[]{ParkPosition,ParkingApproach,Driveway,route.Start.Position},DriverStage.LeavingParking,"Leaving the private parking bay");
            }
            else if(Stage==DriverStage.LeavingParking || Stage==DriverStage.SupplierAccess || Stage==DriverStage.ReturningParking)
            {
                if(!AdvancePrivate(seconds))return;
                if(Stage==DriverStage.LeavingParking)SetStage(DriverStage.JoiningRoad,"Waiting for a safe gap in traffic");
                else if(Stage==DriverStage.SupplierAccess){loadedAtStop=false;SetStage(DriverStage.Loading,"Loading at "+SupplierRouting.Name(supplier));}
                else SetStage(DriverStage.Unloading,"Unloading into the store warehouse");
            }
            else if(Stage==DriverStage.JoiningRoad)
            {
                if(!traffic.TryJoinTraffic(car,route.Start.Lane,route.Start.Distance,true))return;
                routeIndex=0;SetStage(DriverStage.Driving,returning?"Returning to the store":"Driving to "+SupplierRouting.Name(supplier));GuideRoad();
            }
            else if(Stage==DriverStage.Driving)
            {
                GuideRoad();
                if(car.IsTurning || routeIndex!=route.Lanes.Count-1 || car.Distance<route.End.Distance-.1f)return;
                traffic.WithdrawParkingCar(car);
                var end=returning?ParkPosition:SupplierPosition();
                if(returning)BeginPrivate(new[]{NativePosition,Driveway,ParkingApproach,end},DriverStage.ReturningParking,"Returning to the private parking bay");
                else BeginPrivate(SupplierApproach(false),DriverStage.SupplierAccess,"Entering "+SupplierRouting.Name(supplier));
            }
            else if(Stage==DriverStage.Loading)
            {
                actionTime+=seconds;if(actionTime<1.5f)return;actionTime=0;
                var entry=orders.Ledger.Entries.FirstOrDefault(e=>e.location==DeliveryLocation.Depot && orders.SupplierFor(e)==supplier && Fits(e));
                if(entry!=null){if(orders.LoadFrom(entry,supplier)){loadedAtStop=true;return;}Status=orders.Notice;return;}
                if(!loadedAtStop && orders.PickupWeightKg==0){Status="No package fits the selected vehicle. Returning empty.";}
                returning=true;
                if(!route.Plan(SupplierStreet(),StreetEntry,Court!=null?Court.streetAccess-Court.entrance:(Vector3?)null)){Status="No return road route. Cargo is safe in the vehicle.";return;}
                BeginPrivate(SupplierApproach(true),DriverStage.LeavingParking,"Leaving the supplier");
            }
            else if(Stage==DriverStage.Unloading)
            {
                actionTime+=seconds;if(actionTime<2)return;actionTime=0;
                if(orders.PickupWeightKg>0 && !orders.Unload()){Status=orders.Notice;return;}
                if(orders.PickupWeightKg>0){Status="Warehouse full. Remaining cargo stays in the delivery vehicle.";return;}
                SetStage(DriverStage.Exiting,"Delivery complete. Driver leaving the vehicle");
                if(employee!=null){employee.SetBrowsing(false);employee.transform.position=car.transform.position+Vector3.up*.2f;employee.gameObject.SetActive(true);seatedFrom=employee.transform.position;}
            }
            else if(Stage==DriverStage.Exiting)
            {
                actionTime+=seconds;
                var outside=trip.parkedPickup.transform.position-trip.parkedPickup.transform.right*.85f;
                if(employee!=null)employee.transform.position=Vector3.Lerp(seatedFrom,outside,Mathf.Clamp01(actionTime/1.2f));
                if(actionTime<1.2f)return;
                traffic.WithdrawParkingCar(car);Destroy(car.gameObject);car=null;
                trip.parkedPickup.SetActive(true);trip.Fleet.AssignDriver(false);returning=false;
                SetStage(DriverStage.Idle,"Delivery complete. Waiting for the next paid order.");orders.save?.SaveGame();
            }
        }
        private bool Fits(DeliveryEntry entry)
        {
            int count=Math.Min(orders.PackageQuantity(entry),entry.quantity);
            return new CargoCapacity(orders.pickupCapacityKg,orders.pickupCapacityM3).Fits(orders.PickupWeightKg,orders.PickupVolumeM3,orders.UnitWeight(entry)*count,orders.UnitVolume(entry)*count);
        }
        private bool ChooseSupplier()
        {
            bool tooLong=false;
            foreach(var entry in orders.Ledger.Entries)
                if(entry.location==DeliveryLocation.Depot && Fits(entry) && town.suppliers.Any(point=>point.supplier==orders.SupplierFor(entry) && point.kind==CheckpointKind.Depot))
                {
                    supplier=orders.SupplierFor(entry);
                    if(Court!=null && (trip.Fleet.Selected?.length??4.4f)>Court.designVehicleLength){tooLong=true;continue;}
                    return true;
                }
            Status=tooLong?"This supplier's yard requires a shorter vehicle. Select a smaller vehicle.":"Waiting for paid orders that fit this vehicle.";
            return false;
        }
        private Vector3 SupplierPosition()=>traffic.transform.InverseTransformPoint(town.suppliers.First(p=>p.kind==CheckpointKind.Depot && p.supplier==supplier).transform.position);
        private Vector3 SupplierStreet()=>Court!=null?Court.streetAccess:SupplierPosition();
        private IEnumerable<Vector3> SupplierApproach(bool leaving)
        {
            var points=new List<Vector3>();var court=Court;
            if(court!=null)
            {
                var inward=(court.entrance-court.streetAccess).normalized;
                // The rural turnaround shares its entrance with public two-way traffic.
                // Compact city courts have one authored service track between roadside furniture.
                float offset=supplier==SupplierKind.Farmers?1.6f:0;
                var side=Vector3.Cross(Vector3.up,inward)*(leaving?-offset:offset);
                points.Add(court.streetAccess+side);points.Add(court.entrance+side);points.AddRange(court.approachWaypoints);points.Add(court.staging);
            }
            points.Add(SupplierPosition());if(leaving)points.Reverse();
            points.Insert(0,NativePosition);if(leaving)points.Add(route.Start.Position);return points;
        }
        private void GuideRoad()
        {
            if(car.IsTurning)return;
            if(routeIndex+1<route.Lanes.Count && car.LaneIndex==route.Lanes[routeIndex+1])routeIndex++;
            if(routeIndex+1<route.Lanes.Count)car.ChooseDeparture(route.Lanes[routeIndex+1]);
        }
        private void SetStage(DriverStage stage,string status){Stage=stage;Status=status;actionTime=0;}
        private void BeginPrivate(IEnumerable<Vector3> points,DriverStage stage,string status)
        {
            var clean=new List<Vector3>();
            foreach(var p in points){var point=p;point.y=.02f;if(clean.Count==0 || Vector3.Distance(clean[clean.Count-1],point)>.02f)clean.Add(point);}
            if(clean.Count<2)clean.Add(clean[0]+Vector3.forward*.03f);
            privateEnd=clean[clean.Count-1];privateFinalLength=Vector3.Distance(privateEnd,clean[clean.Count-2]);
            privateFinalHeading=stage==DriverStage.SupplierAccess?traffic.transform.InverseTransformDirection(town.suppliers.First(p=>p.kind==CheckpointKind.Depot&&p.supplier==supplier).transform.forward)
                :stage==DriverStage.ReturningParking?traffic.transform.InverseTransformDirection(trip.parkedPickup.transform.forward):Vector3.zero;
            privateSamples=clean.ToArray();privatePath=new TrafficPolyline(privateSamples);privateDistance=0;
            car.transform.SetParent(traffic.transform,true);car.enabled=false;SetStage(stage,status);
            if(stage==DriverStage.LeavingParking)traffic.RequestPrivateMerge(car,route.Start.Lane,route.Start.Distance);
        }
        private bool AdvancePrivate(float seconds)
        {
            if(Stage==DriverStage.LeavingParking && privatePath.Length-privateDistance<car.Length+4)
                traffic.RequestPrivateMerge(car,route.Start.Lane,route.Start.Distance);
            float step=Math.Min(2.5f*seconds,privatePath.Length-privateDistance);
            var current=privatePath.Sample(privateDistance,out _);var next=privatePath.Sample(privateDistance+step,out var direction);
            var world=traffic.transform.TransformPoint(current);var forward=traffic.transform.TransformDirection(direction);
            float scale=traffic.transform.lossyScale.x;
            foreach(var hit in Physics.BoxCastAll(world+Vector3.up*.45f,new Vector3(car.Width*.42f,.35f,.08f)*scale,forward,Quaternion.LookRotation(forward),car.Length*.5f*scale+step*scale,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.collider.transform.IsChildOf(car.transform) || hit.normal.y>.65f || hit.collider.bounds.max.y<world.y+.12f)continue;
                AccessObstacle=hit.collider.name+" at "+ToNative(hit.point);Status="Delivery access blocked. Clear the driveway.";return false;
            }
            privateDistance+=step;car.transform.position=traffic.transform.TransformPoint(next);
            bool reverse=Vector3.Dot(car.transform.forward,forward)<-.2f;
            if(privateFinalHeading!=Vector3.zero && privatePath.Length-privateDistance<privateFinalLength+.01f)reverse=Vector3.Dot(traffic.transform.TransformDirection(privateFinalHeading),forward)<0;
            var facing=reverse?-forward:forward;
            car.transform.rotation=Quaternion.RotateTowards(car.transform.rotation,Quaternion.LookRotation(facing),100*seconds);
            car.AnimateParkingWheels(reverse?-step:step,Vector3.SignedAngle(car.transform.forward,facing,Vector3.up),seconds);
            return privateDistance>=privatePath.Length-.001f;
        }
        private void CreateEmployee()
        {
            var root=new GameObject("Driver employee");root.transform.SetParent(transform,false);root.transform.position=shop.EntrancePosition;
            var model=Instantiate(shop.ModelFor(StaffRole.Driver),root.transform);model.GetComponent<ShopperAppearance>()?.FitHeight(ShopperDimensions.ShopHeight);
            employee=root.AddComponent<ShopCharacter>();employee.Initialize(shop.Navigation,.8f);
        }
        private void CreateCar(Vector3 native,Quaternion rotation)
        {
            var selected=trip.Fleet.Selected;
            var root=new GameObject("Player vehicle — staff delivery");root.transform.SetParent(traffic.transform,false);
            root.transform.position=traffic.transform.TransformPoint(native);root.transform.rotation=rotation;
            var visual=Instantiate(selected!=null?selected.model:starterVehicleModel,root.transform);
            var bindings=visual.GetComponent<PlayerVehicleModel>();
            var wheels=bindings!=null?bindings.wheels.Select(w=>new TrafficVehicle.Wheel{visual=w.pivot,front=w.front}).ToArray():visual.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Wheel")).Select(t=>new TrafficVehicle.Wheel{visual=t,front=t.localPosition.z>0}).ToArray();
            float length=selected!=null?selected.length:4.4f,width=selected!=null?selected.width:1.9f;
            car=root.AddComponent<TrafficVehicle>();car.Configure("player_delivery",length,width,length*.62f,.33f,wheels);car.enabled=false;
            var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,.8f,0);collider.size=new Vector3(width,1.4f,length);
            trip.parkedPickup.SetActive(false);
        }
        public DriverJourneySaveData Capture()=>new DriverJourneySaveData
        {
            active=IsAway,returning=returning,supplier=supplier,position=NativePosition,stage=Stage,
            rotation=Quaternion.Inverse(traffic.transform.rotation)*(car!=null?car.transform.rotation:trip.parkedPickup.transform.rotation),
            accessPath=(Vector3[])privateSamples.Clone(),accessDistance=privateDistance
        };
        public void Restore(DriverJourneySaveData saved)
        {
            if(car!=null){traffic.WithdrawParkingCar(car);car.gameObject.SetActive(false);Destroy(car.gameObject);car=null;}
            if(employee!=null){employee.gameObject.SetActive(false);Destroy(employee.gameObject);}employee=null;
            Stage=DriverStage.Idle;trip.Fleet.AssignDriver(false);trip.parkedPickup.SetActive(true);
            resume=saved!=null && saved.active?saved:null;
        }
        private void Resume(DriverJourneySaveData saved)
        {
            if(employee==null)CreateEmployee();employee.gameObject.SetActive(false);
            returning=saved.returning;supplier=saved.supplier;
            if(saved.stage==DriverStage.WalkingToCar||saved.stage==DriverStage.Boarding)
            {
                employee.gameObject.SetActive(true);employee.transform.position=trip.parkedPickup.transform.position-trip.parkedPickup.transform.right*.85f;
                SetStage(DriverStage.Idle,"Ready to resume collection from the private bay.");return;
            }
            if(saved.stage!=DriverStage.Driving && saved.stage!=DriverStage.JoiningRoad && saved.stage!=DriverStage.Idle)
            {
                CreateCar(saved.position,traffic.transform.rotation*saved.rotation);trip.Fleet.AssignDriver(true);
                if(saved.stage==DriverStage.LeavingParking||saved.stage==DriverStage.SupplierAccess||saved.stage==DriverStage.ReturningParking)
                {
                    if(saved.accessPath==null||saved.accessPath.Length<2)throw new InvalidOperationException("Saved delivery access route is incomplete. Cargo was preserved.");
                    if(saved.stage==DriverStage.LeavingParking)
                    {
                        bool connected=returning?route.Plan(SupplierStreet(),StreetEntry,Court!=null?Court.streetAccess-Court.entrance:(Vector3?)null):route.Plan(StreetEntry,SupplierStreet());
                        if(!connected)throw new InvalidOperationException("Saved private exit has no public road connection. Cargo was preserved.");
                    }
                    BeginPrivate(saved.accessPath,saved.stage,"Resuming the saved delivery access");privateDistance=Mathf.Clamp(saved.accessDistance,0,privatePath.Length);
                }
                else
                {
                    SetStage(saved.stage,"Resuming the saved delivery operation");loadedAtStop=orders.PickupWeightKg>0;
                    if(saved.stage==DriverStage.Exiting){employee.gameObject.SetActive(true);employee.transform.position=car.transform.position;seatedFrom=employee.transform.position;}
                }
                return;
            }
            var goal=returning?StreetEntry:SupplierStreet();
            if(!route.Plan(saved.position,goal))throw new InvalidOperationException("Saved delivery has no connected road route. Paid cargo was preserved.");
            CreateCar(route.Start.Position,traffic.transform.rotation*Quaternion.LookRotation(traffic.Network.Lanes[route.Start.Lane].Path.Sample(route.Start.Distance+1,out var facing)-route.Start.Position));
            trip.Fleet.AssignDriver(true);SetStage(DriverStage.JoiningRoad,"Resuming the saved delivery journey");
        }
        private void OnDestroy(){if(traffic!=null && car!=null)traffic.WithdrawParkingCar(car);if(shop!=null && shop.Driver==this)shop.Driver=null;}
    }
}
