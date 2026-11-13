using System;
using System.Collections;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Parking;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ConstructionWorkspacePlay
{
    private const string Pending="ConstructionWorkspace.Pending";
    private static IEnumerator checks;
    private static double deadline;
    private static readonly System.Collections.Generic.HashSet<string> englishScreens=new();
    static ConstructionWorkspacePlay(){EditorApplication.update+=Tick;}
    private static bool StarterAccess=>SessionState.GetBool("ConstructionWorkspace.StarterAccess",false);
    private static bool Transport=>SessionState.GetBool("ConstructionWorkspace.Transport",false);
    private static bool Frontage=>SessionState.GetBool("ConstructionWorkspace.Frontage",false);
    private static bool Escalator=>SessionState.GetBool("ConstructionWorkspace.Escalator",false);
    private static bool Elevator=>SessionState.GetBool("ConstructionWorkspace.Elevator",false);
    private static bool Floors=>SessionState.GetBool("ConstructionWorkspace.Floors",false);
    private static bool Doors=>SessionState.GetBool("ConstructionWorkspace.Doors",false);
    private static bool Decor=>SessionState.GetBool("ConstructionWorkspace.Decor",false);
    private static string Report=>Decor?"Library/DecorCollectionQA":Doors?DoorCollectionPlay.Report:Floors?"Library/FloorCollectionQA":Elevator?ElevatorModelBatch.Report:Escalator?EscalatorModelBatch.Report:Frontage?StarterFrontageBatch.Report:Transport?StoreyTransportBatch.Report:StarterAccess?StarterAccessBatch.Report:ConstructionWorkspaceBatch.Report;
    private static string Documentation=>Doors?DoorCollectionBatch.Pictures:Floors?FloorCollectionBatch.Pictures:Elevator?ElevatorModelBatch.Pictures:Escalator?EscalatorModelBatch.Pictures:Frontage?StarterFrontageBatch.Pictures:Transport?StoreyTransportBatch.Pictures:StarterAccess?"Docs/StarterAccess":"Docs/ConstructionWorkspace";
    private static void RecordInputs(){if(Elevator)ElevatorModelBatch.Record();else if(Escalator)EscalatorModelBatch.Record();else if(Frontage)StarterFrontageBatch.Record();else if(Transport)StoreyTransportBatch.Record();else if(StarterAccess)StarterAccessBatch.Record("play");else ConstructionWorkspaceBatch.Record("play");}
    public static void Run()=>Begin(false);
    public static void RunStarterAccess()=>Begin(true);
    public static void RunTransport()=>Begin(false,true);
    public static void RunFrontage()=>Begin(false,true,true);
    public static void RunEscalator()=>Begin(false,true,false,true);
    public static void RunElevator()=>Begin(false,transport:true,elevator:true);
    public static void RunFloors()=>Begin(false, floors:true);
    public static void RunDoors()=>Begin(false, doors:true);
    public static void RunDecor()=>Begin(false, decor:true);
    private static void Begin(bool starterAccess,bool transport=false,bool frontage=false,bool escalator=false,bool elevator=false,bool floors=false,bool doors=false,bool decor=false)
    {
        SessionState.SetBool("ConstructionWorkspace.Floors",floors);
        SessionState.SetBool("ConstructionWorkspace.Doors",doors);
        SessionState.SetBool("ConstructionWorkspace.Decor",decor);
        TownWorldSafety.RequireIsolatedEditor();SessionState.SetBool("ConstructionWorkspace.StarterAccess",starterAccess);SessionState.SetBool("ConstructionWorkspace.Transport",transport);SessionState.SetBool("ConstructionWorkspace.Frontage",frontage);SessionState.SetBool("ConstructionWorkspace.Escalator",escalator);SessionState.SetBool("ConstructionWorkspace.Elevator",elevator);Directory.CreateDirectory(Report);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
        foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        File.WriteAllText(Report+"/play.txt","RUNNING\n");SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(checks==null){deadline=EditorApplication.timeSinceStartup+240;checks=Check();}
            Require(EditorApplication.timeSinceStartup<deadline,"Full parking/shop cycle timed out.");
            if(checks.MoveNext())return;Finish(null);
        }
        catch(Exception error){Debug.LogException(error);Finish(error);}
    }
    private static IEnumerator Check()
    {
        for(int i=0;i<4;i++)yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Player save writer present.");
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();starter.cityTrip.orders.save=null;
        if(StarterAccess)SiteRevision(starter);
        var data=starter.CreateNewGame();Object.FindObjectOfType<StoreProgression>().ApplySaveData(data.territory);
        StarterStock(starter,data);
        starter.building.inventory.ApplySaveData(data.buildInventory,starter.catalog);
        var orders=starter.cityTrip.orders;
        Require(data.buildInventory.All(entry=>starter.building.inventory.GetCount(starter.catalog.GetById(entry.itemId))==entry.count),
            "Starting quantities were lost when applying the warehouse inventory.");
        Require(orders.shopCapacityKg-orders.ShopWeightKg>=2*orders.pickupCapacityKg,
            "Starter stock must leave room for two full pickup loads.");
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;starter.ConfigureSite(true,data);spawner.Spawn(StoreLevelId.Lvl1);starter.ApplyNewGameBuildings(data);
        if (!Floors && !Doors && !Decor) VerifyStarterWallJoins(starter);
        var shop=Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();shop.enabled=false;shop.SetOpen(false);
        var parking=Object.FindObjectOfType<ParkingTraffic>();parking.enabled=false;
        var roads=Object.FindObjectOfType<RetailEmpireTycoon.City.TownRoadTraffic>();roads.enabled=false;
        foreach(var roadCar in roads.ActiveCars)roadCar.gameObject.SetActive(false);
        Require(roads.MaximumCars==RetailEmpireTycoon.City.TownRoadTraffic.DefaultCapacity,"Unexpected shared town traffic capacity.");
        for(int i=0;i<5;i++)yield return null;
        if (Decor)
        {
            foreach(var step in DecorCollectionPlay.Verify(starter))yield return step;
            yield break;
        }
        if (Doors)
        {
            foreach (var step in DoorCollectionPlay.Verify(starter, CaptureUi)) yield return step;
            yield break;
        }
        if (Floors)
        {
            foreach (var step in VerifyFloorCollection(starter)) yield return step;
            yield break;
        }
        if(Frontage){FrontageMigration(starter);foreach(var step in GroundOnlyTransports(starter))yield return step;}
        Physics.simulationMode=SimulationMode.Script;
        VerifyFrontageDecor(starter);
        foreach (var step in VerifyPauseAndStatistics(starter)) yield return step;
        Require(Mathf.Abs(starter.building.grid.cellSize-ShopProportions.CellSize)<.00001f && Mathf.Abs(GridSystem.StoreyHeight-1.65f)<.0001f,"Construction grid/storeys were not enlarged.");
        var bays=Object.FindObjectsOfType<ParkingBay>().Where(bay=>bay.GetComponentInParent<BuildPreview>()==null).ToArray();
        Require(bays.Length==3 && bays.All(bay=>Mathf.Abs(bay.width-1.943271f)<.005f && Mathf.Abs(bay.length-3.923667f)<.005f),"Parking must match the updated 1.1x kit.");
        foreach (var bay in bays)
        {
            var paint = bay.GetComponentsInChildren<MeshRenderer>().Select(r => r.bounds)
                .Aggregate((a,b) => { a.Encapsulate(b); return a; });
            var size = bay.transform.InverseTransformDirection(paint.size);
            Require(Mathf.Abs(Mathf.Abs(size.x)-bay.width*.855f)<.02f &&
                Mathf.Abs(Mathf.Abs(size.z)-bay.length*.60375f)<.02f,"Parking paint does not match the requested proportions.");
        }
        var editing=starter.building.GetComponent<BuildEditingController>();editing.ReadPointerInput=false;
        foreach(var bay in bays)
        {
            var placed=bay.GetComponent<PlacedObject>();editing.Begin();editing.Select(placed);
            var result=editing.PreviewAt(placed.anchorCell);
            Require(result.ok,"An existing asphalt bay cannot be placed: "+result.reason+" "+result.message+" anchor="+placed.anchorCell);
            Require(editing.Confirm(),"Preview and final parking relocation disagree.");editing.Finish();
        }
        var parkingItem=starter.catalog.GetById("parking_01");starter.building.inventory.Add(parkingItem,1);starter.building.EnterBuildMode(parkingItem);
        bool placedNew=false;
        foreach(var tile in starter.floors.BuildSaveData().Where(tile=>tile.itemId=="asphalt_01"))
        {
            var at=new Vector3Int(tile.x,0,tile.z);
            if(!starter.building.CanPlaceAt(at).ok)continue;
            Require(starter.building.TryPlaceAt(at),"Green parking preview refused placement.");placedNew=true;break;
        }
        Require(placedNew,"No free asphalt can accept a new full-size parking bay.");
        starter.building.EnterBuildMode(parkingItem);Require(!starter.building.CanPlaceAt(new Vector3Int(-800,0,-800)).ok,"Public/unowned terrain admitted parking.");starter.building.ExitBuildMode();
        var extraBay=Object.FindObjectsOfType<ParkingBay>().Single(bay=>bay.GetComponentInParent<BuildPreview>()==null && !bays.Contains(bay));
        var extraAnchor=extraBay.GetComponent<PlacedObject>().anchorCell;
        editing.Begin();editing.Select(extraBay.GetComponent<PlacedObject>());Require(editing.DeleteSelected(),"New test bay cannot be returned to storage.");editing.Finish();
        var personalItem=starter.catalog.GetById("parking_personal");
        Require(personalItem!=null && personalItem.isPersonalParking && !personalItem.hiddenFromShop,"The personal bay is missing from the build catalog.");
        Require(starter.building.inventory.GetCount(personalItem)==2,"Two personal parking bays must be available in starting storage.");
        starter.building.EnterBuildMode(personalItem);
        Require(starter.building.TryPlaceAt(extraAnchor),"The personal parking item cannot be placed on clear asphalt.");
        Require(starter.building.inventory.GetCount(personalItem)==1,"Placing a personal bay must consume one from storage.");
        var extraPersonal=Object.FindObjectsOfType<PlacedObject>().Single(p=>p.item==personalItem);
        Require(extraPersonal.playerParking && !extraPersonal.primaryPlayerParking &&
            starter.building.BuildPlacedSaveData().Any(p=>p.itemId==personalItem.id && p.playerParking),"A personal bay was not reserved on placement and saved.");
        editing.Begin();editing.Select(extraPersonal);Require(editing.DeleteSelected(),"A secondary personal bay cannot be returned to storage.");editing.Finish();
        foreach(var door in Object.FindObjectsOfType<PlacedObject>().Where(placed=>placed.item!=null&&placed.item.id=="door_01"))
        {
            Require(door.transform.Find("door-left")!=null && door.transform.Find("door-right")!=null,"New operable double door missing.");
            Require(Mathf.Abs(door.item.placementBounds.size.x-door.item.footprint.x*starter.building.grid.cellSize)<.003f,"Door does not fill enlarged opening.");
        }
        foreach(string id in new[]{"door_01","unloading_gate_01"})
        {
            var item=starter.catalog.GetById(id);
            var bounds=CityUpdateSetup.Measure(item.prefab);
            float cell=starter.building.grid.cellSize;
            Require(Vector3.Distance(bounds.size,new Vector3(6*cell,GridSystem.StoreyHeight,cell))<.003f &&
                item.footprint==new Vector2Int(6,1),"Opening does not match wall envelope: "+id+" "+bounds);
            var band=item.prefab.GetComponentsInChildren<MeshRenderer>(true).First(renderer=>renderer.name=="ContinuousTopBand");
            Require(band.sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(WallCollectionBatch.Root+"/Wallfinishes.mat"),"Opening uses a mismatched wall palette.");
            Require(item.WallFootprint(0)==new Vector2Int(6,2) && item.WallPrefab(0)!=item.prefab,
                "Saved openings must keep the previous footprint and prefab.");
        }
        foreach(var step in Workspace(starter))yield return step;
        Migration(starter);
        ShopGameplaySandbox.PrepareActivities();
        if(Transport)PrepareUpperShop(starter);
        if(Escalator)
        {
            var motor=Object.FindObjectsOfType<StoreyTransportVisual>().Single(visual=>visual.GetComponent<PlacedObject>()?.item?.id=="escalator_01");
            Require(motor.treadMarkers.Length==18 && motor.treadMarkers.All(step=>step.GetComponent<MeshFilter>()!=null),"New animated escalator steps are missing.");
            var before=motor.treadMarkers.Select(step=>step.localPosition).ToArray();float until=Time.realtimeSinceStartup+.2f;
            while(Time.realtimeSinceStartup<until)yield return null;
            Require(motor.treadMarkers.Where((step,index)=>Vector3.Distance(step.localPosition,before[index])>.001f).Any(),"Escalator animation is frozen.");
            File.WriteAllText(Report+"/animation.txt","PASS new shared step meshes visibly animate in actual Play Mode; same escalator ID and navigation component.\n");
        }
        var shelves=Object.FindObjectsOfType<PlacedShelfStock>();
        var product=AssetDatabase.FindAssets("t:ProductItemData").Select(guid=>AssetDatabase.LoadAssetAtPath<ProductItemData>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(item=>item!=null&&shelves.Any(shelf=>shelf.IsProductAllowed(item) && (!Transport || shelf.GetComponent<PlacedObject>().anchorCell.y==1)));
        foreach(var shelf in shelves)shelf.ClearStock();var stocked=shelves.First(shelf=>shelf.IsProductAllowed(product) && (!Transport || shelf.GetComponent<PlacedObject>().anchorCell.y==1));stocked.SetStockFromSave(product,24);
        var balance=Object.Instantiate(shop.Balance);balance.thiefChance=0;balance.litterChance=0;ShopGameplaySetup.Set(shop,"balance",balance);
        shop.Advance(.1f);shop.SetOpen(true);parking.arrivalInterval=9999;
        var visitorFleet=Resources.Load<ParkingVisitorFleet>("Parking/Visitors");
        var access=new ParkingStreetAccess(roads,parking.roadEntry,parking.drivewayEntry);
        var passingCar=Object.Instantiate(visitorFleet.Vehicles.OrderBy(c=>c.Width*c.Length).First());
        passingCar.transform.localScale=Vector3.one*visitorFleet.ModelScale;
        Require(access.PrepareLane() && access.TryPrepare(passingCar.Width*visitorFleet.ModelScale,
            passingCar.Length*visitorFleet.ModelScale,null),"No upstream position for the passing car.");
        // A script reload can keep the initialized flag while losing the runtime-only generator.
        typeof(RetailEmpireTycoon.City.TownRoadTraffic).GetField("random",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            .SetValue(roads,null);
        Require(roads.TryJoinTraffic(passingCar,access.SpawnLane,access.SpawnDistance),"Could not stage a regular car on the road.");
        access.GuideArrival(passingCar);
        Directory.CreateDirectory(Documentation);
        if(Frontage)TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/SiteAndStreet.png",starter.blueprint.cameraFocus+new Vector3(-12,23,-31),starter.blueprint.cameraFocus+Vector3.back*8);
        File.WriteAllText(Report+"/routing.txt","entry="+shop.EntrancePosition+" arrival="+shop.ArrivalPosition+" shelf="+stocked.transform.position+" from street="+shop.TryShelfApproach(stocked,shop.ArrivalPosition,out _)+"\n");
        var map=new System.Text.StringBuilder();var grid=starter.building.grid;
        for(int z=-24;z<=12;z++)
        {
            for(int x=10;x<=26;x++)map.Append(shop.Navigation.IsWalkable(new Vector3Int(x,0,z),true)?'.':'#');
            map.AppendLine(" z="+z);
        }
        foreach(var placed in Object.FindObjectsOfType<PlacedObject>().Where(placed=>placed.item!=null && (placed.item.isDoorway || placed.GetComponent<PlacedShelfStock>()!=null)))
            map.AppendLine(placed.item.id+" at="+placed.transform.position+" bounds="+placed.item.placementBounds+" facing="+placed.facing);
        foreach(var z in new[]{-4.5f,-4.125f,-3.75f,-2f,0f})
        {
            var point=new Vector3(4.875f,0,z);map.AppendLine("at="+point+" hits="+string.Join(",",Physics.OverlapCapsule(point+Vector3.up*.36f,point+Vector3.up*.81f,.1f).Select(collider=>collider.name+":"+collider.GetComponentInParent<PlacedObject>()?.item?.id)));
        }
        File.AppendAllText(Report+"/routing.txt",map.ToString());
        Require(shop.TryShelfApproach(stocked,shop.ArrivalPosition,out _),"Entrance to shelf route is blocked; see routing.txt.");
        ShopperVisit customer=null;GameObject car=null;bool paid=false,walked=false,streetArrival=false,streetDeparture=false,reachedUpper=false,parkingSteering=false;int beforeMoney=shop.Money,beforeStock=stocked.CurrentAmount;
        var lift=Elevator?Object.FindObjectsOfType<StoreyTransportVisual>().Single(motor=>motor.liftDoors!=null):null;
        bool lowerDoorSeen=false,upperDoorSeen=false,liftMoved=false;
        float trafficTime=0,originalTimeScale=Time.timeScale;
        if(Elevator)Time.timeScale=8;
        for(int step=0;step<(Elevator?120000:16000);step++)
        {
            // Door/cabin animation and shop patience must share one clock in the lift integration scenario.
            float seconds=Elevator?Mathf.Clamp(Time.deltaTime,.0001f,.1f):.05f;trafficTime+=seconds;
            parking.Advance(seconds);Physics.SyncTransforms();
            roads.Simulate(seconds,trafficTime,parking.roadEntry.position,true);Physics.Simulate(seconds);
            shop.Advance(seconds);
            if(lift!=null)
            {
                lowerDoorSeen|=lift.liftDoors.IsOpen(0);upperDoorSeen|=lift.liftDoors.IsOpen(1);liftMoved|=lift.platform.localPosition.y>.15f;
                Require(!(lift.liftDoors.IsOpen(0)&&lift.liftDoors.IsOpen(1)),"Both lift doors are open in the real store.");
                if(lift.liftDoors.IsOpen(0))Require(Mathf.Abs(lift.platform.localPosition.y)<.013f,"Ground lift door opens without cabin in real store.");
                if(lift.liftDoors.IsOpen(1))Require(Mathf.Abs(lift.platform.localPosition.y-lift.travelHeight)<.013f,"Upper lift door opens without cabin in real store.");
            }
            Require(shop.Visits.Count<=1,"A separate pedestrian arrival duplicated the parking buyer.");
            if(car==null && customer==null)
            {
                car=parking.VisitorCars.FirstOrDefault();
                if(car!=null)
                {
                    Require(Vector3.Distance(car.transform.position,parking.roadEntry.position)>6,"Visitor still spawns at the driveway.");
                    Require(car==passingCar.gameObject && parking.ArrivalState=="Diverted from city traffic","The customer did not use the existing road car.");
                    streetArrival=true;
                }
            }
            if(car!=null && !passingCar.enabled)parkingSteering|=Mathf.Abs(passingCar.Steering)>4;
            if(paid && car!=null && car.transform.position.z<parking.roadEntry.position.z-.5f && Mathf.Abs(car.transform.position.x-parking.roadEntry.position.x)>6)streetDeparture=true;
            if(customer==null)customer=shop.Visits.FirstOrDefault(visit=>visit.ArrivedByCar);
            if(customer!=null)
            {
                reachedUpper|=customer.Character!=null && customer.Character.transform.position.y>GridSystem.StoreyHeight;
                var animator=customer.Character!=null?customer.Character.GetComponentInChildren<Animator>():null;
                if(animator!=null){animator.Update(seconds);walked|=animator.GetBool("isWalking");}
                Require(customer.IsFinished || car!=null,"Driver's car left before the visit ended.");
                if(customer.Stage==VisitStage.Waiting && !paid)
                {
                    for(int wait=0;wait<600;wait++)parking.Advance(.05f);
                    Require(car!=null && !customer.IsFinished,"Car uses an unrelated parking timer rather than waiting for its buyer.");
                    TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/StoreAndParking.png",starter.blueprint.cameraFocus+new Vector3(-9,12,-16),starter.blueprint.cameraFocus+Vector3.back*3);
                    Require(shop.ClaimCheckout()==customer && shop.CompleteCheckout(customer),"Driver cannot buy through the real checkout.");paid=true;
                    Require(shop.SalesRevenue == customer.Quantity * product.SellPrice && shop.UnitsSold == customer.Quantity &&
                        shop.CompletedSales == 1 && shop.AverageReceipt == shop.SalesRevenue, "Real checkout did not update store statistics.");
                    var persisted = JsonUtility.FromJson<ShopOperationsSaveData>(JsonUtility.ToJson(shop.BuildSaveData()));
                    Require(persisted.salesRevenue == shop.SalesRevenue && persisted.unitsSold == shop.UnitsSold &&
                        persisted.trackedSales == 1, "Sales totals did not survive save serialization.");
                    Require(shop.Money==beforeMoney+customer.Quantity*product.SellPrice && stocked.CurrentAmount==beforeStock-customer.Quantity,"Payment or stock was applied incorrectly.");
                }
                if(paid && customer.IsFinished && parking.VisitorCars.Count==0)break;
            }
            if(step%500==0)
            {
                var lines=parking.MovementState+" arrival="+parking.ArrivalState+" notice="+shop.Notice+" stage="+customer?.Stage+"\n";
                foreach(var vehicle in parking.VisitorCars)
                    foreach(int side in new[]{-1,1})
                    {
                        var p=vehicle.transform.position+vehicle.transform.right*side*(vehicle.GetComponent<RetailEmpireTycoon.City.TrafficVehicle>().Width*.25f+.22f);p.y=.025f;
                        bool nearest=shop.Navigation.TryNearest(p,out var target,true,true);
                        lines+="car="+vehicle.transform.position+" door="+p+" nearest="+nearest+" at="+target+" streetpath="+shop.Navigation.TryPath(target,shop.ArrivalPosition,out _,true)+" shelfpath="+shop.TryShelfApproach(stocked,target,out _)+"\n";
                    }
                File.WriteAllText(Report+"/cycle-state.txt",lines);
                if(customer==null && parking.VisitorCars.Count>0)TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/RouteProbe.png",starter.blueprint.cameraFocus+new Vector3(-9,12,-16),starter.blueprint.cameraFocus+Vector3.back*3);
            }
            // Lift doors and cabin run on real frame time; let their animations advance between buyer steps.
            if(step%100==0 || Elevator)yield return null;
        }
        Time.timeScale=originalTimeScale;
        Require(customer!=null && walked && paid && customer.ReturnedToCar && streetArrival && streetDeparture && parkingSteering && parking.VisitorCars.Count==0,"Incomplete street -> car -> shop -> purchase -> same car -> street cycle. "+parking.MovementState+" notice="+shop.Notice+" visit="+customer?.Stage);
        Require(parking.VisitorCars.Count==0,"Finished visitor retained the parking lease.");
        Require(shop.TryHire(StaffRole.Cleaner), "Could not hire the payroll test employee.");
        int payroll = shop.Balance.Wage(StaffRole.Cleaner);
        shop.Advance(60);
        Require(shop.WagesPaid == payroll && shop.BuildSaveData().wagesPaid == payroll,
            "Actual payroll was not reflected in the statistics and save data.");
        if(Transport)Require(reachedUpper,"Parking buyer never physically reached its upstairs shelf.");
        if(Elevator)
        {
            Require(lowerDoorSeen&&upperDoorSeen&&liftMoved,"Real buyer skipped the lift cabin or a landing door pair.");
            File.WriteAllText(Report+"/doors.txt","PASS actual parking buyer boards the single cabin, rides upstairs, returns downstairs and passes both animated door pairs; doors only open with aligned cabin, never both stops at once.\n");
        }
        File.WriteAllText(Report+"/cycle.txt","PASS actual passenger car arrival, checkout and departure. Actual sale, units, average receipt and payroll reached the persistent statistics.\n");
        File.WriteAllText(Report+"/placement.txt","PASS 10-percent larger StorePolish kit, shelves and storeys; 1.94x3.92 bays; proportional grid clearance; move all initial bays plus place and remove a test bay on service-yard asphalt; reject public/unowned ground; matching-width operable double door; idempotent version-one and legacy save conversion.\n");
        float shopWallTop=Object.FindObjectsOfType<PlacedObject>().Where(placed=>placed.item!=null && placed.item.isWall)
            .SelectMany(placed=>placed.GetComponentsInChildren<MeshRenderer>()).Max(renderer=>renderer.bounds.max.y);
        var gate=Object.FindObjectsOfType<PlacedObject>().First(placed=>placed.item!=null && placed.item.isUnloadingGate);
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/Gate.png",gate.transform.position+new Vector3(-3,2.5f,-4),gate.transform.position+Vector3.up*.6f);
        starter.cityTrip.Begin();while(!starter.cityTrip.InCity || starter.cityTrip.Transitioning)yield return null;
        for(int i=0;i<6;i++)yield return null;
        var world=Object.FindObjectOfType<RetailEmpireTycoon.City.CityWorld>();world.pickup.ReadKeyboard=false;world.pickup.InputEnabled=false;
        var roof=world.facadeRoot.Find("Closed store roof");
        float expectedTop=world.town.layout.MapShopPoint(Vector3.up*shopWallTop).y;
        Require(roof==null,"City still grants an automatic roof.");
        Require(!world.facadeRoot.GetComponentsInChildren<MeshFilter>().Any(filter=>filter.name.Contains("Highlight_Tube_")),"City snapshot copies temporary editing highlights.");
        EnglishUi("city driving HUD");
        var cityMap=(RetailEmpireTycoon.City.CityMapHud)typeof(RetailEmpireTycoon.City.CityWorld).GetField("map",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(world);
        cityMap.Toggle();yield return null;EnglishUi("city map");cityMap.Toggle();
        var supplier=world.town.suppliers.First(point=>point.kind==RetailEmpireTycoon.City.CheckpointKind.Depot);
        typeof(RetailEmpireTycoon.City.CityWorld).GetField("activeSupplier",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(world,supplier);
        typeof(RetailEmpireTycoon.City.CityWorld).GetField("buying",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(world,true);
        typeof(RetailEmpireTycoon.City.CityWorld).GetMethod("ShowDepot",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(world,null);
        yield return null;EnglishUi("supplier assortment");
        typeof(RetailEmpireTycoon.City.CityWorld).GetMethod("ClosePanel",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(world,null);
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/CityStore.png",world.pickup.transform.position+new Vector3(-8,5,-10),world.town.layout.MapShopPoint(starter.blueprint.cameraFocus)+Vector3.up);
        starter.cityTrip.Return();while(starter.cityTrip.Transitioning)yield return null;
        Require(!starter.cityTrip.InCity,"Return from City failed.");
        File.WriteAllText(Report+"/city.txt","PASS actual Game -> City -> Game; no automatic roof; scaled wall top "+expectedTop+"; uniform world scale, player-built surfaces only.\n");
        if (Resources.Load<BuildItemData>("WallCollection/wall_finish_01") != null)
            foreach (var step in VerifyWallCollection(starter)) yield return step;
    }

    private static System.Collections.Generic.IEnumerable<object> VerifyPauseAndStatistics(StarterStoreInitialization starter)
    {
        var controls = Object.FindObjectOfType<GameplayControls>();
        var shopWindow = Object.FindObjectOfType<RetailEmpireTycoon.UI.Shop.ShopWindow>(true);
        var warehouse = Object.FindObjectOfType<RetailEmpireTycoon.UI.Windows.BuildInventoryWindow>(true);
        var hud = Object.FindObjectOfType<ShopOperationsHud>();
        var shop = Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();
        var settings = Object.FindObjectOfType<ControlPanel>(true);
        var settingsPanel = settings.settingPanel.GetComponent<RetailEmpireTycoon.UI.Shop.ShopSettingsPanel>();
        var canvas = Object.FindObjectOfType<RetailEmpireTycoon.UI.Shop.ShopHudPresenter>().canvas;
        var shortcuts=canvas.transform.Find("Action shortcuts").GetComponentsInChildren<TMPro.TMP_Text>(true);
        shortcuts[shortcuts.Length-1].text="";
        typeof(ShopOperationsHud).GetField("_shortcutLabels",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            .SetValue(hud,null);
        hud.SendMessage("RefreshShortcutText");
        Require(shortcuts.Length==17 && shortcuts[15].text=="Esc" && shortcuts[16].text=="Pause",
            "The shortcut HUD could not restore its labels after losing the runtime cache.");
        var actionRow = canvas.transform.Find("Game edge HUD/Shop action row");
        var rating = canvas.transform.Find("Shop rating");
        Require(actionRow != null && actionRow.gameObject.activeInHierarchy, "Store HUD actions are missing.");
        var buttons=actionRow.GetComponentsInChildren<Button>().Select(b=>(RectTransform)b.transform).OrderBy(r=>r.anchoredPosition.x).ToArray();
        Require(buttons.Length==6 && buttons.All(r=>r.sizeDelta==new Vector2(48,48)) &&
            buttons.Zip(buttons.Skip(1),(left,right)=>right.anchoredPosition.x-left.anchoredPosition.x).All(gap=>Mathf.Abs(gap-57)<.01f),
            "The HUD buttons must have equal size and spacing.");
        Canvas.ForceUpdateCanvases();CaptureUi("StarterHudUniform");
        var actions = actionRow.GetComponentsInChildren<Button>()
            .OrderBy(button => ((RectTransform)button.transform).anchoredPosition.x)
            .Select(button => button.name);
        Require(actions.SequenceEqual(new[] { "Open territories", "Open store statistics", "Open or close store",
            "Open construction", "Open store catalog", "Open warehouse" }), "The store HUD actions are out of order or include the map.");
        Require(rating.GetComponent<Graphic>() == null && rating.GetComponent<Outline>() == null &&
            rating.GetComponentInChildren<TMPro.TMP_Text>(true) == null && rating.childCount == 5,
            "The rating should show only its five stars.");
        Button ButtonNamed(string text) => canvas.GetComponentsInChildren<Button>()
            .First(button => button.GetComponentInChildren<TMPro.TMP_Text>()?.text == text);
        Require(Object.FindObjectOfType<SaveManager>(true) == null, "Player save is not isolated.");
        var saveObject = new GameObject("Disposable pause save fixture");
        var save = saveObject.AddComponent<SaveManager>();
        save.enabled = false; // Exercise the real writer without running its player-load or autosave lifecycle.
        const string path = "Library/ShopPauseQA/disposable-save.json";
        Directory.CreateDirectory("Library/ShopPauseQA");
        var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(SaveManager).GetField("saveFilePath", fields).SetValue(save, path);
        typeof(SaveManager).GetField("worldRestored", fields).SetValue(save, true);
        float originalTime = Time.timeScale;
        try
        {
            controls.RequestShop();
            controls.RequestWarehouse();
            controls.RequestPause();
            Require(shopWindow.gameObject.activeInHierarchy && !warehouse.gameObject.activeInHierarchy && !controls.PauseMenuVisible,
                "Store did not exclude warehouse and pause.");
            yield return null;
            Require(!actionRow.gameObject.activeInHierarchy, "HUD actions overlap the catalog.");
            CaptureUi("HudCatalog");
            controls.HandleEscape();
            yield return null;
            Require(actionRow.gameObject.activeInHierarchy, "HUD actions did not return after closing the catalog.");
            Require(!shopWindow.gameObject.activeInHierarchy && !controls.PauseMenuVisible, "Esc did not close just the catalog.");
            controls.RequestWarehouse();
            yield return null;
            Require(warehouse.gameObject.activeInHierarchy && !actionRow.gameObject.activeInHierarchy, "HUD actions overlap the warehouse.");
            controls.HandleEscape();
            Require(!warehouse.gameObject.activeInHierarchy && !controls.PauseMenuVisible, "Esc did not close just the warehouse.");
            controls.RequestConstruction();
            yield return null;
            Require(controls.IsConstructionActive && !actionRow.gameObject.activeInHierarchy, "HUD actions overlap construction.");
            CaptureUi("HudConstruction");
            controls.HandleEscape();
            yield return null;
            Require(!controls.IsConstructionActive && actionRow.gameObject.activeInHierarchy, "HUD actions did not return after construction.");
            var map = starter.cityTrip.TownMap;
            map.Toggle();
            Require(map.IsOpen, "Store map did not open.");
            controls.HandleEscape();
            Require(!map.IsOpen && !controls.PauseMenuVisible, "Esc did not close just the map.");

            Time.timeScale = .75f;
            controls.HandleEscape();
            float pausedAt = Time.time;
            yield return null; yield return null;
            Require(controls.PauseMenuVisible && Time.timeScale == 0 && Time.time == pausedAt, "Pause did not stop game time.");
            Require(!actionRow.gameObject.activeInHierarchy, "HUD actions overlap the pause menu.");
            CaptureUi("PauseMenu");
            ButtonNamed("Save game").onClick.Invoke();
            Require(File.Exists(path) && File.ReadAllText(path).Contains("\"shopOperations\"") &&
                canvas.GetComponentsInChildren<TMPro.TMP_Text>().Any(label => label.text == "Game saved."),
                "Pause save did not write and confirm the disposable save.");
            ButtonNamed("Settings").onClick.Invoke();
            controls.RequestShop();
            Require(settings.settingPanel.activeInHierarchy && !shopWindow.gameObject.activeInHierarchy &&
                !ButtonExists("Resume"), "Settings and another menu are shown together.");
            settingsPanel.controlsButton.onClick.Invoke();
            Require(hud.ControlsVisible && !settings.settingPanel.activeInHierarchy, "Controls did not replace Settings.");
            controls.Capture(ShopAction.Catalog);
            controls.HandleEscape();
            Require(hud.ControlsVisible && !controls.Capturing.HasValue, "Esc closed the bindings page instead of cancelling capture.");
            controls.HandleEscape();
            Require(settings.settingPanel.activeInHierarchy && !hud.ControlsVisible && Time.timeScale == 0, "Esc did not return to Settings.");
            controls.HandleEscape();
            Require(!settings.settingPanel.activeInHierarchy && ButtonExists("Resume") && Time.timeScale == 0, "Esc did not return to the paused menu.");
            ButtonNamed("Main menu").onClick.Invoke();
            Require(ButtonExists("Leave") && !ButtonExists("Resume"), "The leave confirmation did not replace the pause page.");
            controls.HandleEscape();
            controls.HandleEscape();
            Require(!controls.PauseMenuVisible && Mathf.Approximately(Time.timeScale, .75f), "Resume did not restore the prior time scale.");
            yield return null;
            yield return null;
            Require(rating.gameObject.activeInHierarchy && actionRow.gameObject.activeInHierarchy,
                "The rating and actions did not return after resuming.");
            CaptureUi("HudOverview");

            shop.ApplySaveData(new ShopOperationsSaveData {completedSales = 12});
            Require(shop.CompletedSales == 12 && shop.SalesRevenue == 0 && shop.AverageReceipt == 0, "A legacy save invented revenue.");
            shop.ApplySaveData(new ShopOperationsSaveData());
            Require(Mathf.Approximately(shop.Rating, shop.RatingFactors.Score), "Rating breakdown does not match the live score.");
            Canvas.ForceUpdateCanvases();
            var star = (RectTransform)rating.GetChild(2);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(canvas.GetComponent<GraphicRaycaster>().eventCamera,
                    star.TransformPoint(star.rect.center))
            };
            // The batch editor does not assign render depth to overlay graphics outside screenshot capture.
            var starGraphic = star.GetComponent<Graphic>();
            Require(starGraphic.isActiveAndEnabled && starGraphic.raycastTarget &&
                starGraphic.Raycast(pointer.position, canvas.GetComponent<GraphicRaycaster>().eventCamera), "The rating star cannot receive pointer input.");
            ExecuteEvents.ExecuteHierarchy(star.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Require(GameObject.Find("Rating factors") != null && GameObject.Find("Sales overview") == null, "The stars did not open the rating page.");
            Require(!actionRow.gameObject.activeInHierarchy, "HUD actions overlap the rating breakdown.");
            CaptureUi("RatingBreakdown");
            ButtonNamed("Overview").onClick.Invoke(); yield return null;
            Require(GameObject.Find("Sales overview") != null, "Statistics overview did not open.");
            CaptureUi("StoreStatistics");
            controls.HandleEscape();
            var storeButton = actionRow.Find("Open or close store").GetComponent<Button>();
            Require(string.IsNullOrEmpty(storeButton.GetComponentInChildren<TMPro.TMP_Text>()?.text) &&
                storeButton.GetComponent<RetailEmpireTycoon.UI.Shop.ShopLockAnimation>() != null,
                "The store status button should display only the animated lock.");
            storeButton.onClick.Invoke(); yield return null;
            var toast = canvas.transform.Find("Store status notice");
            Require(shop.IsOpen && toast.GetComponent<CanvasGroup>().alpha > 0 && toast.GetComponentInChildren<TMPro.TMP_Text>().text == "Store is open",
                "Opening the store did not display a temporary notice.");
            storeButton.onClick.Invoke(); yield return null;
            Require(!shop.IsOpen && toast.GetComponentInChildren<TMPro.TMP_Text>().text == "Store is closed", "Closing did not replace the notice.");
            File.WriteAllText("Library/ShopPauseQA/Result.txt",
                "PASS exclusive store, warehouse, map and pause; paused simulation; save; Settings, Controls, key capture and Escape stack; exit confirmation; live rating and overview; legacy stats; store state notices and icon-only lock; six ordered HUD actions hidden behind menus and construction; rating has stars only.\n");
        }
        finally
        {
            controls.CloseWindows();
            Time.timeScale = originalTime;
            Object.DestroyImmediate(saveObject);
        }
        bool ButtonExists(string text) => canvas.GetComponentsInChildren<Button>()
            .Any(button => button.GetComponentInChildren<TMPro.TMP_Text>()?.text == text);
    }

    private static System.Collections.Generic.IEnumerable<object> VerifyFloorCollection(StarterStoreInitialization starter)
    {
        var build = starter.building;
        var floors = starter.floors;
        var inventory = build.inventory;
        var orders = starter.cityTrip.orders;
        var styles = Resources.LoadAll<BuildItemData>("FloorCollection").OrderBy(item => item.id, StringComparer.Ordinal).ToArray();
        Require(styles.Length == 29 && styles.Select(item => item.id).Distinct().Count() == 29 &&
            styles.All(item => !item.id.StartsWith("Floor_29_", StringComparison.Ordinal)), "Invalid floor collection IDs.");
        var controls = Object.FindObjectOfType<GameplayControls>();
        var shop = Object.FindObjectOfType<RetailEmpireTycoon.UI.Shop.ShopWindow>(true);
        controls.RequestShop();
        shop.OpenCategory(BuildCategory.Structures);
        yield return null;
        shop.GetComponentsInChildren<Button>().Single(button => button.name == "Floors & Ceilings").onClick.Invoke();
        yield return null;
        var cards = shop.GetComponentsInChildren<RetailEmpireTycoon.UI.Shop.ShopItemCard>();
        Require(styles.All(item => cards.Any(card => card.nameText.text == item.displayName && card.icon.sprite == item.icon &&
            card.buyButton.interactable)), "Missing purchasable floor card or icon.");
        Require(cards[0].nameText.text == "Cream Ceramic" && cards.All(card => card.nameText.text != "Storage rack"),
            "The floor section must start with the new collection and contain only finishes.");
        CaptureUi("FloorStore");
        shop.GetComponentsInChildren<Button>().Single(button => button.name == "Wood").onClick.Invoke();
        yield return null;
        Require(shop.GetComponentsInChildren<RetailEmpireTycoon.UI.Shop.ShopItemCard>().Select(card => card.nameText.text)
            .SequenceEqual(styles.Where(item => item.floorFinishGroup == FloorFinishGroup.Wood).Select(item => item.displayName)),
            "The Wood filter did not isolate the new wooden finishes.");
        controls.HandleEscape();
        yield return null;

        var nav = new StoreNavigation(build.grid, build.territory);
        nav.RefreshDoorways(Object.FindObjectsOfType<PlacedObject>());
        var racks = Object.FindObjectsOfType<PlacedObject>()
            .Where(placed => placed.item != null && placed.item.id == "storage_rack_01")
            .OrderBy(placed => placed.anchorCell.z).ToArray();
        Require(racks.Length == 3 && racks.SelectMany(rack => rack.occupiedCells).Distinct().Count() ==
            racks.Sum(rack => rack.occupiedCells.Count), "The starter warehouse needs three separate racks.");
        var aisle = racks[1].anchorCell + Vector3Int.right * 4;
        var aisleStart = aisle + new Vector3Int(0, 0, -2);
        var aisleEnd = aisle + new Vector3Int(0, 0, 6);
        Require(nav.IsWalkable(aisle) && nav.TryPath(build.grid.CellToWorld(aisleStart) + Vector3.up * StoreNavigation.FloorHeight,
            build.grid.CellToWorld(aisleEnd) + Vector3.up * StoreNavigation.FloorHeight, out _),
            "The new rack blocks the warehouse aisle.");
        var legacy = floors.BuildSaveData();
        VerifyGroundFloorHeights(starter.catalog, styles[0]);
        var positions = legacy.Where(tile => tile.floor == 0 && tile.itemId != "asphalt_01")
            .Select(tile => new Vector3Int(tile.x, 0, tile.z))
            .Where(cell => nav.IsWalkable(cell) && floors.AreCellsValid(new() { cell }, styles[0]))
            .OrderBy(cell => (build.grid.CellToWorld(cell) - starter.blueprint.cameraFocus).sqrMagnitude)
            .Take(styles.Length).ToArray();
        Require(positions.Length == styles.Length, "Starter floor has too few free test cells.");
        Require(positions.All(cell => Mathf.Abs(floors.SurfaceElevation(cell) - floors.surfaceHeight) < .001f),
            "An indoor starter floor was lowered.");
        int balance = orders.money.Money;
        orders.money.SetMoney(0);
        Require(!orders.Order(styles[0]) && orders.money.Money == 0, "An unaffordable tile order was accepted.");
        orders.money.SetMoney(balance);
        for (int i = 0; i < styles.Length; i++)
        {
            var item = styles[i];
            Require(starter.catalog.GetById(item.id) == item && item.prefab.isStatic && item.icon != null &&
                item.floorMaterial.IsKeywordEnabled("_NORMALMAP"), "Invalid definition: " + item.id);
            int before = orders.money.Money;
            int stockBefore = inventory.GetCount(item);
            Require(orders.Order(item) && orders.money.Money == before - item.price, "Incorrect floor charge: " + item.id);
            var parcel = orders.Ledger.Entries.Single(entry => entry.itemId == item.id &&
                entry.location == RetailEmpireTycoon.Logistics.DeliveryLocation.Depot);
            Require(orders.LoadFrom(parcel, RetailEmpireTycoon.Logistics.SupplierKind.Construction) && orders.Unload(),
                "Floor delivery failed: " + item.id + " " + orders.Notice);
            build.EnterBuildMode(item);
            build.RotateSelected(i % 4);
            Require(build.TryPaintCells(new() { positions[i] }) && inventory.GetCount(item) == stockBefore,
                "Delivered floor could not be painted: " + item.id);
            build.ExitBuildMode();
            Require(nav.IsWalkable(positions[i]), "New finish blocked store navigation: " + item.id);
        }
        Require(orders.money.Money == balance - styles.Sum(style => style.price), "Incorrect total charged for collection.");
        Require(floors.Surfaces.Where(mesh => styles.Any(item => item.id == mesh.name)).All(mesh =>
            mesh.GetComponent<Collider>() == null && mesh.sharedMesh.tangents.Length == mesh.sharedMesh.vertexCount),
            "Ground finishes changed colliders or lack normal-map tangents.");
        var snapshot = new GameData { floorTiles = floors.BuildSaveData(), buildInventory = inventory.BuildSaveData() };
        var loaded = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(snapshot));
        floors.ApplySaveData(loaded.floorTiles, starter.catalog);
        inventory.ApplySaveData(loaded.buildInventory, starter.catalog);
        var restored = floors.BuildSaveData().ToDictionary(tile => new Vector3Int(tile.x, tile.floor, tile.z));
        for (int i = 0; i < styles.Length; i++)
            Require(restored[positions[i]].itemId == styles[i].id && restored[positions[i]].facing == i % 4,
                "Floor item or rotation lost in save/load: " + styles[i].id);
        var previous = JsonUtility.FromJson<FloorTileSaveData>("{\"itemId\":\"floor_cream\",\"x\":2,\"z\":3}");
        Require(previous.facing == 0 && legacy.Any(tile => tile.itemId == "asphalt_01") &&
            loaded.floorTiles.Any(tile => tile.itemId == "asphalt_01"), "Legacy floors changed.");

        build.EnterBuildMode(styles[0]);
        int replacementStock = inventory.GetCount(styles[0]);
        inventory.Add(styles[0], 1);
        Require(build.TryPaintCells(new() { positions[1] }), "Could not replace another finish.");
        build.ExitBuildMode();
        Require(floors.RemoveCells(new() { positions[1] }, inventory) && inventory.GetCount(styles[0]) == replacementStock + 1,
            "Erasing a floor did not return the replacement tile.");
        VerifyStripeRegion(starter.catalog, styles[18]);
        foreach (var step in DriveAcrossFloorCollection(styles)) yield return step;

        var accents = new[] { styles[3], styles[13], styles[18], styles[26] };
        var shopCells = legacy.Where(tile => tile.floor == 0 && tile.itemId != "asphalt_01")
            .Select(tile => new Vector3Int(tile.x, 0, tile.z)).ToHashSet();
        var center = shopCells.OrderByDescending(cell => floors.GetRectCells(cell, cell + new Vector3Int(7, 0, 7))
                .Count(at => shopCells.Contains(at) && nav.IsWalkable(at)))
            .First();
        for (int i = 0; i < accents.Length; i++)
        {
            var offset = new Vector3Int(i % 2 * 4, 0, i / 2 * 4);
            var patch = floors.GetRectCells(center + offset, center + offset + new Vector3Int(3, 0, 3));
            Require(patch.All(shopCells.Contains) && floors.AreCellsValid(patch, accents[i]), "Cannot place a contiguous screenshot swatch.");
            floors.PaintCells(patch, accents[i]);
        }
        var walkingStart = build.grid.CellToWorld(center + new Vector3Int(0, 0, 1)) + Vector3.up * StoreNavigation.FloorHeight;
        var walkingEnd = build.grid.CellToWorld(center + new Vector3Int(7, 0, 1)) + Vector3.up * StoreNavigation.FloorHeight;
        Require(nav.TryPath(walkingStart, walkingEnd, out _), "Customer navigation could not route across the floor junction.");
        yield return null;
        var camera = Camera.main;
        var target = build.grid.CellToWorld(center + new Vector3Int(3, 0, 3));
        camera.transform.position = target + new Vector3(-5.2f, 8, -6.7f);
        camera.transform.LookAt(target);
        CaptureUi("FloorGameplay");
        Debug.Log("Floor Collection QA passed: 29 cards, filters, purchase, supplier, placement, navigation, pickup drive, replacement, erase, rotation and save/load.");
    }

    private static System.Collections.Generic.IEnumerable<object> DriveAcrossFloorCollection(BuildItemData[] styles)
    {
        var root = new GameObject("Floor driving test");
        Vector3 origin = new Vector3(1000, 10, 1000);
        var floor = root.AddComponent<FloorPainter>();
        floor.grid = root.AddComponent<GridSystem>();
        floor.grid.origin = origin;
        floor.grid.cellSize = ShopProportions.CellSize;
        var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.transform.SetParent(root.transform);
        road.transform.position = origin + new Vector3(0, -.25f, 5);
        road.transform.localScale = new Vector3(12, .5f, 38);
        for (int i = 0; i < styles.Length; i++)
            floor.PaintCells(floor.GetRectCells(new Vector3Int(-6, 0, i * 2), new Vector3Int(5, 0, i * 2 + 1)), styles[i]);
        Physics.SyncTransforms();
        for (int i = 0; i < styles.Length; i++)
            Require(Physics.Raycast(origin + new Vector3(0, 2, (i * 2 + .5f) * floor.grid.cellSize), Vector3.down, out var hit, 3)
                && hit.collider.gameObject == road, "The finish added a ground collision: " + styles[i].id);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/City/Pickup.prefab");
        var truck = Object.Instantiate(model, origin + new Vector3(0, .1f, -3.5f), Quaternion.identity, root.transform)
            .GetComponent<RetailEmpireTycoon.City.PickupDrive>();
        truck.enabled = false;
        truck.ReadKeyboard = false;
        var tick = typeof(RetailEmpireTycoon.City.PickupDrive).GetMethod("FixedUpdate",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var previous = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        try
        {
            truck.Stop();
            Physics.SyncTransforms();
            for (int step = 0; step < 130; step++) { tick.Invoke(truck, null); Physics.Simulate(.02f); }
            float settled = truck.Body.position.y;
            Require(truck.GroundedWheels == truck.wheels.Length, "Pickup was not grounded before the test.");
            truck.SetInput(.52f, 0, false);
            float end = origin.z + styles.Length * 2 * floor.grid.cellSize + 2.5f;
            for (int step = 0; step < 360 && truck.Body.position.z < end; step++)
            {
                tick.Invoke(truck, null);
                Physics.Simulate(.02f);
                Require(Mathf.Abs(truck.Body.position.y - settled) < .12f && truck.GroundedWheels >= 3,
                    "Pickup bounced or lost wheel contact on the floor runway.");
                if (step % 60 == 0) yield return null;
            }
            Require(truck.Body.position.z >= end, "Pickup did not cross all 29 finishes.");
        }
        finally
        {
            Physics.simulationMode = previous;
            Object.Destroy(root);
        }
    }

    private static void VerifyStripeRegion(BuildItemCatalog catalog, BuildItemData stripe)
    {
        var root = new GameObject("Floor stripe test");
        var floor = root.AddComponent<FloorPainter>();
        floor.grid = root.AddComponent<GridSystem>();
        floor.grid.cellSize = ShopProportions.CellSize;
        var inner = new Vector3Int(15, 1, 4);
        var outer = new Vector3Int(16, 1, 4);
        floor.PaintCells(new() { inner, outer }, stripe, 1);
        var surfaces = floor.Surfaces.OrderBy(filter => filter.transform.parent.name, StringComparer.Ordinal).ToArray();
        Require(surfaces.Length == 2 && surfaces.All(mesh => mesh.GetComponent<MeshCollider>().sharedMesh == mesh.sharedMesh),
            "Upper floor stripe lacks its chunk collider.");
        Require(surfaces[0].sharedMesh.uv.Take(4).All(uv => uv.x < .5f) &&
            surfaces[1].sharedMesh.uv.Take(4).All(uv => uv.x > .5f), "Stripe shows on an interior chunk edge.");
        var inventory = root.AddComponent<BuildInventory>();
        Require(floor.RemoveCells(new() { outer }, inventory) && surfaces[0].sharedMesh.uv.Take(4).All(uv => uv.x > .5f),
            "The stripe did not reappear on the neighboring chunk after erasing.");
        var snapshot = floor.BuildSaveData();
        floor.ApplySaveData(snapshot, catalog);
        Require(floor.BuildSaveData().Single().facing == 1, "The stripe direction did not restore.");
        Object.Destroy(root);
    }

    private static void VerifyGroundFloorHeights(BuildItemCatalog catalog, BuildItemData finish)
    {
        var root = new GameObject("Floor height test");
        var floor = root.AddComponent<FloorPainter>();
        floor.grid = root.AddComponent<GridSystem>();
        floor.grid.origin = new Vector3(1000, 4, 1000);
        floor.grid.cellSize = ShopProportions.CellSize;
        var inside = new Vector3Int(2, 0, 2);
        var outside = new Vector3Int(3, 0, 2);
        floor.SetRaisedFloorFootprint(new RectInt(2, 2, 1, 1));
        floor.PaintCells(new() { inside, outside }, finish);
        float ground = floor.grid.origin.y + FloorPainter.GroundSurfaceLift;
        float raised = floor.grid.origin.y + floor.surfaceHeight;
        void CheckMesh()
        {
            var mesh = floor.Surfaces.Single().sharedMesh;
            Require(mesh.vertexCount == 8 && Mathf.Abs(mesh.bounds.min.y - ground) < .001f &&
                Mathf.Abs(mesh.bounds.max.y - raised) < .001f &&
                Mathf.Abs(floor.SurfaceElevation(outside) - ground) < .001f &&
                Mathf.Abs(floor.SurfaceElevation(inside) - raised) < .001f,
                "Exterior paving and the raised indoor floor do not match their respective heights.");
        }
        CheckMesh();
        floor.ApplySaveData(floor.BuildSaveData(), catalog);
        CheckMesh();
        floor.ShowPreview(new() { outside }, true);
        var preview = root.transform.Find("Floor brush preview").GetComponent<MeshFilter>().sharedMesh;
        Require(Mathf.Abs(preview.bounds.center.y - (ground + .005f)) < .001f,
            "The floor brush preview floats above outdoor paving.");
        Object.Destroy(root);
    }

    private static System.Collections.Generic.IEnumerable<object> VerifyWallCollection(StarterStoreInitialization starter)
    {
        var orders=starter.cityTrip.orders;
        var build=starter.building;
        var editor=build.GetComponent<BuildEditingController>();
        var shop=Object.FindObjectOfType<RetailEmpireTycoon.UI.Shop.ShopWindow>(true);
        var controls=Object.FindObjectOfType<GameplayControls>();
        var styles=Resources.LoadAll<BuildItemData>("WallCollection").Where(item=>!item.IsWallCorner).OrderBy(item=>item.id).ToArray();
        Require(styles.Length==30 && styles.Select(i=>i.id).Distinct().Count()==30,"The wall collection catalog is incomplete.");
        VerifyWallProfiles(starter,styles);
        controls.RequestShop();
        shop.OpenCategory(BuildCategory.Structures);
        yield return null;
        shop.GetComponentsInChildren<Button>().Single(button=>button.name=="Walls").onClick.Invoke();
        yield return null;
        var cards=shop.GetComponentsInChildren<RetailEmpireTycoon.UI.Shop.ShopItemCard>();
        Require(styles.All(item=>cards.Any(card=>card.nameText.text==item.displayName && card.icon.sprite==item.icon &&
            card.buyButton.interactable)),"A purchasable wall card, name or icon is missing from Construction → Walls.");
        Require(cards.All(card=>card.nameText.text!="Wall" && card.nameText.text!="Corner wall") &&
            cards.Any(card=>card.nameText.text=="Store Stripe Corner" && card.buyButton.interactable),
            "Legacy wall cards must be hidden and the new corner available.");
        CaptureUi("WallCollectionStore");
        controls.HandleEscape();
        yield return null;

        int money=orders.money.Money;
        var oldLedger=orders.Ledger.Capture();
        var oldLayout=build.BuildPlacedSaveData();
        var oldInventory=build.inventory.BuildSaveData();
        var floor=starter.floors.BuildSaveData().Where(t=>t.floor==0 && t.itemId!="asphalt_01")
            .Select(t=>new Vector3Int(t.x,0,t.z))
            .OrderBy(at=>(build.grid.CellToWorld(at)-starter.blueprint.cameraFocus).sqrMagnitude).ToArray();
        var positions=new System.Collections.Generic.List<Vector3Int>();
        foreach(var item in styles)
        {
            Require(starter.catalog.GetById(item.id)==item && item.isWall && item.prefab.isStatic,"Wall definition or save lookup mismatch for "+item.id);
            int balance=orders.money.Money;
            Require(orders.Order(item) && orders.money.Money==balance-item.price,"Wall order did not charge the expected amount.");
            var parcel=orders.Ledger.Entries.Single(e=>e.itemId==item.id && e.location==RetailEmpireTycoon.Logistics.DeliveryLocation.Depot);
            Require(orders.LoadFrom(parcel,RetailEmpireTycoon.Logistics.SupplierKind.Construction) && orders.Unload(),
                "The construction supplier could not deliver "+item.id+": "+orders.Notice);
            build.EnterBuildMode(item);
            var cell=floor.FirstOrDefault(at=>positions.All(p=>(p-at).sqrMagnitude>=9) && build.CanPlaceAt(at).ok);
            Require(build.TryPlaceAt(cell),"A delivered wall cannot be placed: "+item.id);
            build.ExitBuildMode();
            var wall=Object.FindObjectsOfType<PlacedObject>().Single(p=>p.item==item && p.anchorCell==cell);
            Require(wall.GetComponent<BoxCollider>()!=null && wall.GetComponent<MeshCollider>()==null &&
                wall.wallModuleVersion==BuildItemData.TwoCellWallVersion,"Unexpected wall collider or geometry version.");
            // Keep a few examples for the save round-trip and reuse the same aisle for the other tests.
            if(item==styles[0] || item==styles[12] || item==styles[24] || item==styles[29])
            {
                positions.Add(cell);
                continue;
            }
            editor.Begin();
            Require(editor.Select(wall) && editor.DeleteSelected(),"A placed wall could not be returned to storage.");
            editor.Finish();
            build.inventory.ApplySaveData(oldInventory,starter.catalog);
        }
        orders.money.SetMoney(0);
        Require(!orders.Order(styles[0]) && orders.money.Money==0,"A wall could be ordered without funds.");
        var kept=Object.FindObjectsOfType<PlacedObject>().Single(p=>p.item==styles[0] && p.anchorCell==positions[0]);
        editor.Begin();
        Require(editor.Select(kept),"Cannot select wall for move.");
        editor.Rotate(1);
        var movedTo=floor.First(at=>at!=positions[0] && positions.Skip(1).All(p=>(p-at).sqrMagnitude>=9) && editor.PreviewAt(at).ok);
        Require(editor.Confirm() && kept.facing==1 && kept.anchorCell==movedTo,"Wall movement or rotation failed.");
        editor.Finish();
        positions[0]=movedTo;
        var saved=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(new GameData{placedObjects=build.BuildPlacedSaveData()}));
        Require(saved.placedObjects.Count(p=>p.itemId.StartsWith("wall_finish_",StringComparison.Ordinal))==
            oldLayout.Count(p=>p.itemId.StartsWith("wall_finish_",StringComparison.Ordinal))+positions.Count,
            "A wall returned to storage was still included in a save taken in the same frame.");
        build.ApplyPlacedSaveData(saved.placedObjects,starter.catalog);
        yield return null;
        var restored=build.BuildPlacedSaveData();
        Require(restored.Count==saved.placedObjects.Count &&
            saved.placedObjects.Where(p=>p.itemId.StartsWith("wall_finish_",StringComparison.Ordinal))
                .All(p=>restored.Any(r=>r.itemId==p.itemId && r.x==p.x && r.z==p.z && r.facing==p.facing)),
            "Wall IDs, cells, or rotations were lost on save/load.");
        var examples=Object.FindObjectsOfType<PlacedObject>().Where(p=>p.item!=null &&
            p.item.id.StartsWith("wall_finish_",StringComparison.Ordinal) && positions.Contains(p.anchorCell)).ToArray();
        var focus=examples.Aggregate(Vector3.zero,(sum,p)=>sum+p.transform.position)/examples.Length+Vector3.up*.55f;
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/WallCollectionPlaced.png",
            focus+new Vector3(-2.4f,3f,-3.2f),focus);
        build.ApplyPlacedSaveData(oldLayout,starter.catalog);
        build.inventory.ApplySaveData(oldInventory,starter.catalog);
        orders.Ledger.Restore(oldLedger);
        orders.money.SetMoney(money);
        yield return null;
        File.WriteAllText(Report+"/wall-collection.txt","PASS 30 cards, paid supplier purchase/load/unload and placement, insufficient funds, removal, move/rotation and save/load with legacy walls.\n");
    }

    private static void VerifyStarterWallJoins(StarterStoreInitialization starter)
    {
        var placed=Object.FindObjectsOfType<PlacedObject>();
        var stripes=placed.Where(p=>p.item.id==BuildItemData.StoreStripeId).ToArray();
        var corners=placed.Where(p=>p.item.id==BuildItemData.StoreStripeCornerId).ToArray();
        Require(stripes.Length==62 && corners.Length==4 && placed.All(p=>p.item.id!="wall_01" && p.item.id!="wallcorner_01") &&
            stripes.Concat(corners).All(p=>p.wallModuleVersion==BuildItemData.TwoCellWallVersion),
            "New games must place the two-cell wall kit on the starter footprint.");
        var occupied=new System.Collections.Generic.HashSet<Vector3Int>();
        Require(placed.SelectMany(p=>p.occupiedCells).All(occupied.Add),"The new starter layout overlaps a wall, opening or fixture.");
        int west=corners.Min(p=>p.anchorCell.x),east=corners.Max(p=>p.anchorCell.x);
        int south=corners.Min(p=>p.anchorCell.z),north=corners.Max(p=>p.anchorCell.z);
        var floor=starter.floors.BuildSaveData();
        var town=Object.FindObjectOfType<RetailEmpireTycoon.City.TownWorldPlaces>();
        var sidewalk=town.transform.Find("Player entrance sidewalk trim").GetComponent<MeshFilter>();
        float worldEdge=sidewalk.transform.TransformPoint(sidewalk.sharedMesh.bounds.min).x;
        Require(Mathf.Abs(worldEdge-west*starter.building.grid.cellSize)<.002f,"The entrance sidewalk still extends into the left asphalt.");
        var blueprint=starter.blueprint.initialState;
        int oldWest=blueprint.placedObjects.Where(p=>p.itemId=="wallcorner_01").Min(p=>p.x)+1;
        Require(west==oldWest+StarterStoreBlueprint.RightSideShiftCells,"Starter store was not moved to the right edge.");
        var asphalt=floor.Where(tile=>tile.floor==0 && tile.itemId=="asphalt_01").Select(tile=>new Vector2Int(tile.x,tile.z)).ToHashSet();
        for(int x=oldWest-1;x<west;x++)
        for(int z=south-1;z<=north;z++)
            Require(asphalt.Contains(new Vector2Int(x,z)),"There is a hole in the new asphalt to the left of the starter shop.");
        Require(placed.Where(p=>p.item.id=="parking_01").All(p=>blueprint.placedObjects.Any(old=>old.itemId=="parking_01" && old.x==p.anchorCell.x && old.z==p.anchorCell.z)),
            "Moving the shop also moved the forecourt parking bays.");
        Require(floor.Where(tile=>tile.floor==0 && tile.itemId!="asphalt_01").All(tile=>
            tile.x>=west && tile.x<=east && tile.z>=south && tile.z<=north),"The starter floor extends past the exterior walls.");
        Require(floor.Any(tile=>tile.itemId=="floor_graphite" && tile.x==west && tile.z==south),"The border trim removed the floor under the walls.");
        var openings=placed.Where(p=>p.item.isDoorway || p.item.isUnloadingGate).ToArray();
        float cell=starter.building.grid.cellSize;
        Require(openings.Length==3 && openings.All(p=>p.wallModuleVersion==BuildItemData.TwoCellWallVersion && p.occupiedCells.Count==6),
            "The starter doorway or gate still uses its legacy placement geometry.");
        foreach(var opening in openings)
        foreach(int side in new[]{-1,1})
        {
            var expected=opening.transform.position+opening.transform.right*(4*cell*side);
            Require(stripes.Any(p=>(p.transform.position-expected).sqrMagnitude<.000001f),"Opening is not flush with the adjacent walls.");
        }
        var stems=stripes.Where(p=>p.GetComponent<BoxCollider>().size.x>2*starter.building.grid.cellSize+.02f).ToArray();
        Require(stems.Length==2,"The two starter T junctions have no wall extension.");
        Require(stripes.Count(p=>p.GetComponent<MeshFilter>().sharedMesh.triangles.Length<
            p.item.prefab.GetComponent<MeshFilter>().sharedMesh.triangles.Length)>50,
            "Internal wall caps were left on the joined starter segments.");
        foreach(var stem in stems)
        {
            var box=stem.GetComponent<BoxCollider>();
            Vector3 probe=stem.transform.TransformPoint(new Vector3(Mathf.Sign(box.center.x)*(box.size.x*.5f-.025f)+box.center.x,.85f,-.2f));
            Physics.SyncTransforms();
            Require(box.Raycast(new Ray(probe,stem.transform.forward),out _,.4f),"A starter T junction left a physical gap.");
        }
        Directory.CreateDirectory(Documentation);
        Vector3 focus=stems[0].transform.position+Vector3.up*.55f;
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/StarterWallJunction.png",focus+new Vector3(-2,3,-2.7f),focus);
        focus=stripes.Aggregate(Vector3.zero,(sum,p)=>sum+p.transform.position)/stripes.Length;
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/StarterFittedOpenings.png",focus+new Vector3(-10,12,-14),focus);
        var siteFocus=focus+new Vector3(-3.6f,0,-2.8f);
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/StarterRightSide.png",siteFocus+new Vector3(0,18,-12),siteFocus);
        foreach(var opening in openings.Where(p=>p.item.isUnloadingGate || p.anchorCell.z==south))
        {
            focus=opening.transform.position+Vector3.up*.75f;
            var position=focus-opening.transform.forward*2.8f+opening.transform.right*1.6f+Vector3.up*1.55f;
            TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+(opening.item.isUnloadingGate?"/StarterFittedGate.png":"/StarterFittedDoor.png"),position,focus);
        }
    }

    private static void VerifyWallProfiles(StarterStoreInitialization starter,BuildItemData[] styles)
    {
        float cell=starter.building.grid.cellSize;
        foreach(var item in styles)
        {
            var box=item.prefab.GetComponent<BoxCollider>();
            Require(item.footprint==new Vector2Int(2,1) && Mathf.Abs(box.size.x-2*cell)<.0001f &&
                Mathf.Abs(box.size.z-cell)<.0001f && item.WallFootprint(3)==new Vector2Int(1,2) &&
                item.WallPrefab(3)!=item.prefab && Mathf.Abs(item.WallPrefab(3).GetComponent<BoxCollider>().size.x-cell)<.0001f,
                item.id+" does not use two full cells or cannot restore its previous footprint.");
        }
        foreach(int index in new[]{0,1,2,3,15,16,17,20})
        {
            var item=styles[index];
            var mesh=item.prefab.GetComponent<MeshFilter>().sharedMesh;
            var vertices=mesh.vertices;
            float halfWidth=cell;
            Require(Mathf.Abs(mesh.bounds.min.x+halfWidth)<.0001f && Mathf.Abs(mesh.bounds.max.x-halfWidth)<.0001f,
                item.id+" does not reach both grid edges.");
            var left=vertices.Where(v=>Mathf.Abs(v.x+halfWidth)<.0001f).Select(v=>new Vector2(v.y,v.z)).ToHashSet();
            var right=vertices.Where(v=>Mathf.Abs(v.x-halfWidth)<.0001f).Select(v=>new Vector2(v.y,v.z)).ToHashSet();
            Require(left.SetEquals(right),item.id+" has different profiles at its meeting edges.");
            if(index!=20)continue;
            var uv=mesh.uv;
            var top=Enumerable.Range(0,vertices.Length).Where(i=>Mathf.Abs(vertices[i].y-mesh.bounds.max.y)<.0001f).ToArray();
            Require(top.Length>0 && top.All(i=>uv[i]==WallCollectionFinishes.Swatch(9)),"Corrugation is projected onto the steel top cap.");
        }
    }

    private static void VerifyFrontageDecor(StarterStoreInitialization starter)
    {
        if (!starter.CreateNewGame().frontageDecorGranted) return; // Older assembly stages predate this furniture.
        var frontage = Object.FindObjectsOfType<PlacedObject>().Where(item => item.item != null &&
            item.item.id.StartsWith("frontage_", StringComparison.Ordinal)).ToArray();
        Require(frontage.Length == 3 && frontage.All(p=>p.anchorCell.x>=16), "The trimmed sidewalk still has fixtures on its removed stretch.");
        var bin = frontage.Single(item => item.item.id == "frontage_bin");
        var anchor = bin.anchorCell;
        var item = bin.item;
        var editor = starter.building.GetComponent<BuildEditingController>();
        editor.ReadPointerInput = false;
        editor.Begin();
        Require(editor.Select(bin) && editor.DeleteSelected(), "Outdoor bin cannot be returned to construction storage.");
        editor.Finish();
        starter.building.EnterBuildMode(item);
        Require(starter.building.CanPlaceAt(anchor).ok && starter.building.TryPlaceAt(anchor), "Outdoor bin cannot be built back on its original spot.");
        starter.building.ExitBuildMode();

        var legacy = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(starter.blueprint.initialState));
        var shelf=legacy.placedObjects.First(p=>p.itemId=="shelf_fresh_01");
        int oldShelfX=shelf.x;
        legacy.shelfStocks.Add(new RetailEmpireTycoon.SaveSystem.ShelfStockSaveEntry{buildItemId=shelf.itemId,anchorX=shelf.x,anchorZ=shelf.z,productId="site-relocation-test",amount=9});
        starter.UpgradeUnmodifiedLayout(legacy);
        starter.UpgradeUnmodifiedLayout(legacy);
        var movedStock=legacy.shelfStocks.Single(stock=>stock.productId=="site-relocation-test");
        Require(shelf.x==oldShelfX+StarterStoreBlueprint.RightSideShiftCells && movedStock.anchorX==shelf.x &&
            movedStock.amount==9,"Starter relocation did not preserve the shelf stock or shifted it twice.");
        Require(legacy.placedObjects.Count(placed => placed.itemId.StartsWith("frontage_", StringComparison.Ordinal)) == 3 &&
            legacy.buildInventory.Where(entry=>entry.itemId.StartsWith("frontage_",StringComparison.Ordinal)).Sum(entry=>entry.count)==3,
            "The legacy frontage did not trim once and return the removed fixtures to storage.");
        Require(legacy.placedObjects.Count(p=>p.itemId==BuildItemData.StoreStripeId && p.wallModuleVersion==BuildItemData.TwoCellWallVersion)==62,
            "An untouched legacy shop did not migrate to the two-cell wall kit.");
        Require(legacy.placedObjects.Count(p=>(p.itemId=="door_01" || p.itemId=="unloading_gate_01") && p.wallModuleVersion==BuildItemData.TwoCellWallVersion)==3 &&
            legacy.floorTiles.Count(t=>t.itemId=="floor_graphite")<starter.blueprint.initialState.floorTiles.Count(t=>t.itemId=="floor_graphite"),
            "An untouched starter save did not receive the fitted openings and floor border.");
        var customized = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(starter.blueprint.initialState));
        customized.placedObjects.First(placed => placed.itemId == "wall_01").x += 20;
        starter.UpgradeUnmodifiedLayout(customized);
        Require(!customized.placedObjects.Any(placed => placed.itemId.StartsWith("frontage_", StringComparison.Ordinal)) &&
            customized.placedObjects.Any(placed=>placed.itemId=="wall_01") &&
            customized.buildInventory.Where(entry => entry.itemId.StartsWith("frontage_", StringComparison.Ordinal)).Sum(entry => entry.count) == 6,
            "A customized frontage received props outside the player's inventory.");
        Directory.CreateDirectory("Docs/TownFlowUpdate");
        var focus = starter.blueprint.cameraFocus + Vector3.back * 5;
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(), "Docs/TownFlowUpdate/StarterDecor.png", focus + new Vector3(-10, 7, -9), focus);
        File.WriteAllText("Docs/TownFlowUpdate/Decor.txt", "PASS three editable frontage props; bin removed and rebuilt; three fixtures on the trimmed sidewalk returned to storage once; custom layout received six pieces in storage.\n");
    }
    private static System.Collections.Generic.IEnumerable<object> GroundOnlyTransports(StarterStoreInitialization starter)
    {
        var build=starter.building;var floors=starter.floors;
        Require(!floors.BuildSaveData().Any(tile=>tile.floor>0),"Starter unexpectedly has an upper floor.");
        var anchors=floors.BuildSaveData().Where(tile=>tile.itemId!="asphalt_01").Select(tile=>new Vector3Int(tile.x,0,tile.z)).ToArray();
        var tile=starter.catalog.GetById("floor_cream");
        foreach(string id in new[]{"stairs_01","elevator_01","escalator_01"})
        {
            var item=starter.catalog.GetById(id);build.EnterBuildMode(item);
            var valid=anchors.Where(cell=>build.CanPlaceAt(cell).ok).ToArray();Require(valid.Length>0,"Cannot place "+id+" on the fresh starter's ground floor.");
            var anchor=valid[0];Require(build.TryPlaceAt(anchor),"Ground-only transport placement failed: "+id);
            var placed=Object.FindObjectsOfType<PlacedObject>().Single(p=>p.item==item && p.GetComponentInParent<BuildPreview>()==null);
            var link=new StoreyLink(build.grid,placed);Require(!link.IsUsable(floors),"Transport routes into empty upper air.");
            build.EnterBuildMode(tile);Require(build.TryPaintCells(new System.Collections.Generic.List<Vector3Int>{link.Upper}),"Cannot add landing after transport placement.");build.ExitBuildMode();
            Require(link.IsUsable(floors),"Upper floor does not activate the placed connection.");
            var navigation=new StoreNavigation(build.grid,build.territory);navigation.RefreshDoorways(Object.FindObjectsOfType<PlacedObject>());
            var lower=build.grid.CellToWorld(link.Lower)+Vector3.up*StoreNavigation.FloorOffset(0);
            var upper=build.grid.CellToWorld(link.Upper)+Vector3.up*StoreNavigation.FloorOffset(1);
            var actor=new GameObject("Fresh starter transport passenger").AddComponent<ShopCharacter>();actor.transform.position=lower;actor.Initialize(navigation);
            Require(actor.MoveTo(upper),"Upper landing is unreachable after activation: "+id);
            float until=Time.realtimeSinceStartup+25;while(!actor.Arrived && Time.realtimeSinceStartup<until){actor.Advance(.05f);yield return null;}
            Require(actor.Arrived,"Passenger stalled on fresh starter transport: "+id);Object.Destroy(actor.gameObject);yield return null;
            var editor=build.GetComponent<BuildEditingController>();editor.Begin();editor.Select(placed);Require(editor.DeleteSelected(),"Cannot return test connection to storage.");editor.Finish();yield return null;
            Require(floors.RemoveCells(new System.Collections.Generic.List<Vector3Int>{link.Upper},build.inventory),"Cannot return temporary landing tile.");
        }
        Require(!floors.BuildSaveData().Any(cell=>cell.floor>0),"Ground-only checks left upper-floor construction behind.");
        File.WriteAllText(Report+"/ground-only.txt","PASS all three transports placed in the fresh real starter with no upper floor; inactive until landing painted; real passenger ascends after activation; items and tile returned once.\n");
    }
    private static void FrontageMigration(StarterStoreInitialization starter)
    {
        var previous=JsonUtility.FromJson<GameData>(File.ReadAllText(Report+"/previous.json"));
        previous.playerMoney=24680;previous.buildInventory.Add(new BuildInventorySaveEntry{itemId="floor_wood",count=7});
        var shelf=previous.placedObjects.First(item=>item.itemId=="shelf_fresh_01");
        previous.shelfStocks.Clear();
        previous.shelfStocks.Add(new RetailEmpireTycoon.SaveSystem.ShelfStockSaveEntry{buildItemId=shelf.itemId,anchorX=shelf.x,anchorZ=shelf.z,productId="frontage-stock",amount=9});
        Require(starter.blueprint.ForecourtRevision.TryApply(previous,starter.building.grid,starter.catalog),"Untouched starter frontage did not upgrade.");
        Require(previous.playerMoney==24680 && previous.buildInventory.Single().count==7 && previous.shelfStocks.Single().anchorZ==shelf.z-2 && previous.shelfStocks.Single().amount==9,"Frontage migration changed money, inventory or stock.");
        var once=JsonUtility.ToJson(previous);Require(!starter.blueprint.ForecourtRevision.TryApply(previous,starter.building.grid,starter.catalog) && once==JsonUtility.ToJson(previous),"Frontage migration repeats.");
        var custom=JsonUtility.FromJson<GameData>(File.ReadAllText(Report+"/previous.json"));custom.placedObjects.First().x++;
        once=JsonUtility.ToJson(custom);Require(!starter.blueprint.ForecourtRevision.TryApply(custom,starter.building.grid,starter.catalog) && once==JsonUtility.ToJson(custom),"Custom construction or parking was replaced.");
        var ground=starter.blueprint.initialState.floorTiles.Where(tile=>tile.floor==0).ToArray();
        Require(ground.Select(tile=>(tile.x,tile.z)).Distinct().Count()==ground.Length,"Starter paving overlaps indoor floor.");
        var asphalt=ground.Where(tile=>tile.itemId=="asphalt_01").Select(tile=>new Vector2Int(tile.x,tile.z)).ToHashSet();
        var seen=new System.Collections.Generic.HashSet<Vector2Int>();var queue=new System.Collections.Generic.Queue<Vector2Int>();queue.Enqueue(asphalt.First());
        while(queue.Count>0){var at=queue.Dequeue();if(!seen.Add(at))continue;foreach(var d in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})if(asphalt.Contains(at+d)&&!seen.Contains(at+d))queue.Enqueue(at+d);}
        Require(seen.Count==asphalt.Count,"The driveway or warehouse strip is detached from the forecourt.");
        File.WriteAllText(Report+"/frontage.txt","PASS 0.55 m forward shift, aligned spaced bays, continuous asphalt from street to forecourt and warehouse; no duplicate ground layers; exact-template migration preserves custom sites, money, inventory and stock.\n");
    }
    private static void PrepareUpperShop(StarterStoreInitialization starter)
    {
        var build=starter.building;var tile=starter.catalog.GetById("floor_cream");
        var upper=starter.floors.BuildSaveData().Where(cell=>cell.floor==0 && cell.itemId!="asphalt_01")
            .Select(cell=>new Vector3Int(cell.x,1,cell.z)).Where(cell=>build.territory.IsCellPurchased(cell)).ToList();
        build.inventory.Add(tile,upper.Count);build.EnterBuildMode(tile);Require(build.TryPaintCells(upper),"Cannot prepare the buyer's second floor.");
        var stairs=starter.catalog.GetById(Elevator?"elevator_01":Escalator?"escalator_01":"stairs_01");build.Storeys.SelectFloor(0);build.EnterBuildMode(stairs);
        var anchors=upper.Select(cell=>new Vector3Int(cell.x,0,cell.z)).ToArray();bool installed=false;
        foreach(var cell in anchors)
            if(build.CanPlaceAt(cell).ok){Require(build.TryPlaceAt(cell),"Stair preview and placement disagree.");installed=true;break;}
        Require(installed,"No valid staircase position in starter shop.");
        var shelf=Object.FindObjectsOfType<PlacedShelfStock>().First().GetComponent<PlacedObject>();
        var editor=build.GetComponent<BuildEditingController>();editor.Begin();build.Storeys.SelectFloor(1);editor.Select(shelf);
        bool moved=false;foreach(var cell in upper)
            if(editor.PreviewAt(cell).ok){Require(editor.Confirm(),"Cannot move test shelf upstairs.");moved=true;break;}
        Require(moved,"No upper shelf position.");editor.Finish();build.Storeys.SelectFloor(0);
        File.WriteAllText(Report+"/upstairs.txt","PASS real starter "+stairs.id+" cuts its solid ceiling; real stock-bearing shelf moved upstairs for the parking buyer's purchase cycle.\n");
    }
    private static void StarterStock(StarterStoreInitialization starter,GameData data)
    {
        Require(data.playerMoney==420000,"New games should start with $420,000.");
        var starterFloors = Resources.LoadAll<BuildItemData>("FloorCollection");
        Require(starterFloors.Length == 29 && starterFloors.All(floor =>
            data.buildInventory.Single(entry => entry.itemId == floor.id).count ==
                (floor.id is "Floor_26_BrickPavers" or "Floor_01_CreamCeramic" ? 50000 : 50)),
            "Starting storage must contain 50,000 each of Brick Pavers and Cream Ceramic and 50 of each other collection floor.");
        var starterDoors = Resources.LoadAll<BuildItemData>("DoorCollection");
        Require(starterDoors.Length == 24 && starterDoors.All(door =>
            data.buildInventory.Single(entry => entry.itemId == door.id).count == 3), "Starting storage must contain three of each collection door.");
        var starterDecor = starter.catalog.Items.Where(item => item != null && item.category == BuildCategory.Decoration &&
            !item.hiddenFromShop && item.prefab != null).ToArray();
        Require(starterDecor.Length == 99 && starterDecor.All(item =>
            data.buildInventory.Single(entry => entry.itemId == item.id).count >= 3),
            "Starting storage must contain three of every purchasable decoration, including the legacy market sign.");
        int totalStock = data.buildInventory.Sum(entry => entry.count);
        var starterWalls=Resources.LoadAll<BuildItemData>("WallCollection").Where(item=>!item.IsWallCorner).ToArray();
        Require(starterWalls.Length==30 && starterWalls.All(wall=>data.buildInventory.Single(entry=>entry.itemId==wall.id).count==
            (wall.id==BuildItemData.StoreStripeId?30:10)),
            "Starting storage must contain ten modules of each wall style.");
        Require(data.buildInventory.Where(entry=>starter.catalog.GetById(entry.itemId).category==BuildCategory.Shelf).Count()==5,"Five different starter shelves required.");
        foreach(var pair in new[]{("floor_cream",20),("door_01",1),("unloading_gate_01",1),("parking_01",2)})
            Require(data.buildInventory.Single(entry=>entry.itemId==pair.Item1).count==pair.Item2,"Incorrect stock: "+pair.Item1);
        foreach(string id in new[]{"stairs_01","elevator_01","escalator_01","escalator_down_01"})Require(data.buildInventory.Single(entry=>entry.itemId==id).count==1,"Missing starter transport: "+id);
        Require(starter.blueprint.initialState.buildInventory.Count==0,"Grant mutated the reusable blueprint.");
        var reloaded=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(data));
        starter.UpgradeUnmodifiedLayout(reloaded);
        Require(reloaded.buildInventory.Sum(entry => entry.count) == totalStock, "Loading/upgrading grants the starter stock again.");
        var collectionIds = starterFloors.Concat(starterDoors).Concat(starterDecor).Select(item => item.id).ToHashSet();
        reloaded.buildInventory.RemoveAll(entry => collectionIds.Contains(entry.itemId));
        var extraRack = reloaded.placedObjects.Where(placed => placed.itemId == "storage_rack_01")
            .OrderBy(placed => placed.z).ElementAt(1);
        reloaded.placedObjects.Remove(extraRack);
        starter.UpgradeUnmodifiedLayout(reloaded);
        Require(reloaded.buildInventory.All(entry => !collectionIds.Contains(entry.itemId)) &&
            reloaded.placedObjects.Count(placed => placed.itemId == "storage_rack_01") == 2,
            "An existing layout received the new-game warehouse contents.");
        var stock=(StarterConstructionStock)typeof(StarterStoreBlueprint).GetField("constructionStock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(starter.blueprint);
        var choices=new System.Collections.Generic.HashSet<string>();
        for(int seed=1;seed<=12;seed++)
        {
            var game=new GameData();stock.AddTo(game,new System.Random(seed));
            choices.Add(string.Join(",",game.buildInventory.Where(entry=>starter.catalog.GetById(entry.itemId).category==BuildCategory.Shelf).Select(entry=>entry.itemId)));
        }
        Require(choices.Count>1,"Starter shelf selection is not random.");
        var layout=Object.FindObjectOfType<RetailEmpireTycoon.City.TownWorldPlaces>().layout;
        Require(layout.ShopVisualScale==Vector3.one*layout.shopToCityScale,"City shop is still stretched vertically.");
        File.WriteAllText(Report+"/stock.txt","PASS 50,000 Brick Pavers, 50,000 Cream Ceramic and three of all 99 purchasable decorations; existing-game upgrade grants nothing.\n");
    }
    private static System.Collections.Generic.IEnumerable<object> Workspace(StarterStoreInitialization starter)
    {
        var build=starter.building;var workspace=Object.FindObjectOfType<RetailEmpireTycoon.UI.Shop.BuildEditorHud>(true);
        Require(workspace.Launcher!=null,"Hammer launcher is not connected.");
        Require(!RetailEmpireTycoon.UI.Shop.ShopText.Russian,"An old language preference still selects Russian.");
        var russian=UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.Locales.First(locale=>locale.Identifier.Code=="ru");
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale=russian;yield return null;
        Require(UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code=="en","A legacy locale selector can still switch the build away from English.");
        workspace.Launcher.onClick.Invoke();yield return null;
        Require(workspace.IsOpen && workspace.editor.IsActive,"Hammer does not open construction editing.");
        Require(!workspace.warehouseWindow.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(button=>
            button.GetComponentInChildren<TMPro.TMP_Text>(true)?.text=="Edit store" || button.GetComponentInChildren<TMPro.TMP_Text>(true)?.text=="Private parking"),"Construction tools remain in the warehouse.");
        var inventory=Object.FindObjectOfType<RetailEmpireTycoon.UI.Windows.BuildInventoryWindow>(true);
        workspace.Close();inventory.gameObject.SetActive(true);inventory.ShowFurniture();yield return null;
        Require(inventory.GetComponentsInChildren<RetailEmpireTycoon.UI.Windows.BuildInventoryItemRow>(true).All(row=>!row.placeButton.gameObject.activeSelf),"Warehouse still places furniture directly.");
        inventory.Close();workspace.Open();yield return null;
        Require(!workspace.uiRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(button=>button.name=="Apply" || button.name=="Parking"),"Redundant Apply or Parking button remains.");
        Canvas.ForceUpdateCanvases();CaptureUi("Workspace_Floor1");EnglishUi("construction");
        var lower=Object.FindObjectsOfType<StoreWallOccluder>().SelectMany(wall=>wall.GetComponentsInChildren<Renderer>()).ToArray();
        workspace.uiRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button=>button.name=="Floor up").onClick.Invoke();yield return null;
        Require(build.Storeys.ActiveFloor==1,"Floor-up arrow failed.");
        var fixture=build.inventory.Entries.First(entry=>entry.item.category==BuildCategory.Shelf && entry.count>0).item;
        workspace.BuildItem(fixture);yield return null;yield return null;
        Require(lower.All(renderer=>renderer.enabled && !renderer.forceRenderingOff),"Building upstairs hides a ground-floor wall.");
        var overlay=build.gridOverlay;var vertices=overlay.GetComponent<MeshFilter>().sharedMesh.vertices.Select(vertex=>overlay.transform.TransformPoint(vertex)).ToArray();
        Require(vertices.All(vertex=>Mathf.Abs(vertex.y-GridSystem.StoreyHeight-.002f)<.002f),"Grid floats above an empty second-floor wall top.");
        var ground=string.Join(";",starter.floors.BuildSaveData().Where(tile=>tile.floor==0).Select(tile=>tile.itemId+":"+tile.x+":"+tile.z).OrderBy(key=>key));
        var placed=JsonUtility.ToJson(new GameData{placedObjects=build.BuildPlacedSaveData()});
        var floor=starter.catalog.GetById("floor_cream");workspace.BuildItem(floor);build.Storeys.SetCeiling(false);
        var catalogContent=workspace.uiRoot.GetComponentsInChildren<RectTransform>(true).Single(rect=>rect.name=="Owned construction items");catalogContent.anchoredPosition=new Vector2(0,64);
        var source=starter.floors.BuildSaveData().First(tile=>tile.floor==0 && tile.itemId!="asphalt_01");
        var upper=new Vector3Int(source.x,1,source.z);int stock=build.inventory.GetCount(floor);
        Require(build.TryPaintCells(new System.Collections.Generic.List<Vector3Int>{upper}),"Cannot paint an upper-floor tile through construction.");
        Require(build.inventory.GetCount(floor)==stock-1 && starter.floors.HasSurface(upper),"Painting does not consume exactly one owned tile.");
        Require(Mathf.Abs(catalogContent.anchoredPosition.y-64)<.1f,"Painting resets the owned-item catalog scroll position.");
        Require(starter.floors.Surfaces.Where(filter=>filter.transform.parent.name.Contains("layer 1")).All(filter=>Mathf.Abs(filter.GetComponent<Renderer>().bounds.max.y-GridSystem.StoreyHeight-FloorPainter.UpperSurfaceLift)<.002f),"Built upper floor does not use the wall-cap separation.");
        Require(ground==string.Join(";",starter.floors.BuildSaveData().Where(tile=>tile.floor==0).Select(tile=>tile.itemId+":"+tile.x+":"+tile.z).OrderBy(key=>key))
            && placed==JsonUtility.ToJson(new GameData{placedObjects=build.BuildPlacedSaveData()}),"Upper-floor building changed the ground-floor data.");
        yield return null;CaptureUi("Workspace_Floor2");EnglishUi("upper-floor construction");
        var wallCell=build.BuildPlacedSaveData().First(item=>item.floor==0 && item.itemId==BuildItemData.StoreStripeId);
        var overWall=new Vector3Int(wallCell.x,1,wallCell.z);
        Require(build.TryPaintCells(new System.Collections.Generic.List<Vector3Int>{overWall}),"Cannot build a ceiling over a wall.");
        Require(Mathf.Abs(starter.floors.SurfaceElevation(overWall)-GridSystem.StoreyHeight-FloorPainter.UpperSurfaceLift)<.0001f,"Upper surface still lies on the wall cap.");
        yield return null;CaptureUi("CeilingOverWall");
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),Documentation+"/WallSeparation.png",build.grid.CellToWorld(overWall)+new Vector3(-2,2,-3),build.grid.CellToWorld(overWall));
        var roundTrip=starter.floors.BuildSaveData();starter.floors.ApplySaveData(roundTrip,starter.catalog);
        Require(starter.floors.HasSurface(overWall) && starter.floors.HasSurface(upper),"Save/load lost ceiling tiles.");
        workspace.OpenEditing();build.Storeys.SelectFloor(0);
        var personal=Object.FindObjectsOfType<PlacedObject>().First(placed=>placed.playerParking);
        Require(workspace.editor.SelectAtScreenPoint(build.worldCamera.WorldToScreenPoint(personal.transform.position)),"Clicking personal parking does not select it.");yield return null;
        Require(workspace.editor.Selected==personal && build.Storeys.ActiveFloor==0,"Pointer did not select the personal parking instance.");
        Require(workspace.editor.PreviewAt(personal.anchorCell).ok && workspace.editor.Confirm(),"Clicked personal parking cannot be placed.");
        Require(workspace.editor.SelectAtScreenPoint(build.worldCamera.WorldToScreenPoint(personal.transform.position)),"Cannot pick personal parking a second time.");
        Require(!workspace.editor.DeleteSelected(),"Personal return parking can be deleted.");workspace.editor.CancelSelection();
        var visitor=Object.FindObjectsOfType<PlacedObject>().First(item=>item.item!=null && item.item.isParkingSpace && !item.playerParking);
        Require(workspace.editor.SelectAtScreenPoint(build.worldCamera.WorldToScreenPoint(visitor.transform.position)) && workspace.editor.Selected==visitor,"Pointer cannot select ordinary parking.");
        var visitors=Object.FindObjectsOfType<PlacedObject>().Where(item=>item.item!=null && item.item.isParkingSpace && !item.playerParking).ToArray();
        Require(visitors.Length==2,"The starter needs two visitor bays for the personal parking test.");
        foreach(var bay in visitors)
        {
            Require(workspace.editor.Select(bay),"Cannot select extra personal parking.");
            workspace.editor.TogglePersonalParking();
            Require(bay.playerParking,"The selected bay was not made personal.");
        }
        Require(workspace.editor.Select(visitor),"Cannot select the new return point.");
        workspace.editor.UseSelectedReturnBay();
        var savedBays=build.BuildPlacedSaveData().Where(p=>p.playerParking).ToArray();
        Require(savedBays.Length==3 && savedBays.Single(p=>p.primaryPlayerParking).x==visitor.anchorCell.x &&
            Vector3.Distance(starter.cityTrip.parkedPickup.transform.position, visitor.transform.position)<1,
            "The three personal bays or chosen return position were not saved.");
        Require(workspace.editor.Select(personal),"Cannot restore the original return point.");
        workspace.editor.UseSelectedReturnBay();
        foreach(var bay in visitors)
        {
            Require(workspace.editor.Select(bay),"Cannot reselect the visitor bay.");
            workspace.editor.TogglePersonalParking();
            Require(!bay.playerParking,"Cannot restore the visitor bay.");
        }
        workspace.Close();
        Require(!workspace.IsOpen && build.mode==BuildMode.Normal,"Construction close did not restore ordinary gameplay.");
        yield return null;CaptureUi("Overview");
        var shop=Object.FindObjectOfType<RetailEmpireTycoon.UI.Shop.ShopWindow>(true);shop.gameObject.SetActive(true);shop.ShowMainTabs();yield return null;EnglishUi("store catalog");shop.gameObject.SetActive(false);
        inventory.gameObject.SetActive(true);inventory.ShowProducts();yield return null;EnglishUi("warehouse");inventory.Close();
        var settings=Object.FindObjectOfType<ControlPanel>(true);settings.OpenSetting();yield return null;EnglishUi("settings");settings.ExitSetting();
        File.WriteAllText(Report+"/workspace.txt","PASS hammer launcher; no Apply/Parking buttons; pointer selects personal and visitor bays; personal bay remains protected from deletion; upper-floor and over-wall tiles persist after save/load, stay 5 mm above caps, preserve ground-floor data; English interface.\n");
    }
    private static void SiteRevision(StarterStoreInitialization starter)
    {
        var previous=JsonUtility.FromJson<GameData>(File.ReadAllText(Report+"/previous-starter.json"));
        GameData Clone(GameData value)=>JsonUtility.FromJson<GameData>(JsonUtility.ToJson(value));
        var converted=Clone(previous);converted.playerMoney=27182;
        converted.buildInventory.Add(new BuildInventorySaveEntry{itemId="wall_01",count=13});
        var shelf=previous.placedObjects.First(item=>starter.catalog.GetById(item.itemId).category==BuildCategory.Shelf);
        converted.shelfStocks.Clear();converted.shelfStocks.Add(new RetailEmpireTycoon.SaveSystem.ShelfStockSaveEntry{buildItemId=shelf.itemId,anchorX=shelf.x,anchorZ=shelf.z,amount=17,productId="site-stock"});
        var upper=previous.floorTiles.First(tile=>tile.itemId!="asphalt_01");
        converted.floorTiles.Add(new FloorTileSaveData{itemId=upper.itemId,x=upper.x,z=upper.z,floor=1});
        var revision=starter.blueprint.SiteRevision;var shift=revision.Shift;
        Require(shift==new Vector2Int(8,8) && revision.TryApply(converted,starter.building.grid,starter.catalog),"Unmodified starter cannot receive the site relocation.");
        Require(converted.playerMoney==27182 && converted.buildInventory.Single().count==13 && converted.shelfStocks.Single().amount==17
            && converted.shelfStocks.Single().anchorX==shelf.x+shift.x && converted.shelfStocks.Single().anchorZ==shelf.z+shift.y,"Relocation changed money, inventory or shelf stock.");
        Require(converted.floorTiles.Any(tile=>tile.floor==1 && tile.x==upper.x+shift.x && tile.z==upper.z+shift.y),"Upper player ceiling did not move with its building.");
        foreach(var parking in previous.placedObjects.Where(item=>item.itemId=="parking_01"))
            Require(converted.placedObjects.Any(item=>item.itemId==parking.itemId && item.x==parking.x && item.z==parking.z && item.playerParking==parking.playerParking),"Relocation moved an existing parking bay.");
        string once=JsonUtility.ToJson(converted);Require(!revision.TryApply(converted,starter.building.grid,starter.catalog) && once==JsonUtility.ToJson(converted),"Relocation runs twice.");
        var customized=Clone(previous);customized.placedObjects.First(item=>item.itemId=="wall_01").x++;
        string before=JsonUtility.ToJson(customized);Require(!revision.TryApply(customized,starter.building.grid,starter.catalog) && before==JsonUtility.ToJson(customized),"Migration overwrote a custom shop.");
        var conflict=Clone(previous);var bay=conflict.placedObjects.First(item=>item.itemId=="parking_01");bay.x=shelf.x+shift.x;bay.z=shelf.z+shift.y;
        before=JsonUtility.ToJson(conflict);Require(!revision.TryApply(conflict,starter.building.grid,starter.catalog) && before==JsonUtility.ToJson(conflict),"Rejected parking conflict partially mutated save data.");
        File.WriteAllText(Report+"/migration.txt","PASS exact starter moves once by (8,8); custom ground layouts and conflicting private parking opt out atomically; money, inventory, shelf stock, upper ceilings and existing parking survive. No player save accessed.\n");
    }
    private static void EnglishUi(string context)
    {
        var invalid=Object.FindObjectsOfType<TMPro.TMP_Text>().Where(text=>text.enabled && text.gameObject.activeInHierarchy && text.GetComponentInParent<Canvas>()!=null)
            .Where(text=>text.text.Any(character=>character>='А'&&character<='я'||character=='ё'||character=='Ё')).Select(text=>text.name+"="+text.text).ToArray();
        Require(invalid.Length==0,"Non-English UI in "+context+": "+string.Join("; ",invalid));
        englishScreens.Add(context);
    }
    private static void CaptureUi(string name)
    {
        Directory.CreateDirectory(Documentation);
        var camera=Camera.main;var target=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;var oldTarget=camera.targetTexture;
        var canvases=Object.FindObjectsOfType<Canvas>().Where(canvas=>canvas.isRootCanvas && canvas.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var cameras=canvases.Select(canvas=>canvas.worldCamera).ToArray();var planes=canvases.Select(canvas=>canvas.planeDistance).ToArray();
        var texture=new Texture2D(1600,1000,TextureFormat.RGBA32,false);
        try
        {
            target.Create();camera.targetTexture=target;
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.6f;}
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
            File.WriteAllBytes(Documentation+"/"+name+".png",texture.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
            camera.targetTexture=oldTarget;RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(texture);Canvas.ForceUpdateCanvases();
        }
    }
    private static void Migration(StarterStoreInitialization starter)
    {
        var previous=new GameData{usesModularStore=true,shopProportionsVersion=1,playerMoney=27182};
        for(int x=0;x<40;x++)for(int z=-50;z<-20;z++)previous.floorTiles.Add(new FloorTileSaveData{itemId="asphalt_01",x=x,z=z});
        previous.floorTiles.Add(new FloorTileSaveData{itemId="floor_cream",x=3,floor=1,z=4});
        string fixture=starter.catalog.Items.First(item=>item!=null && item.category==BuildCategory.Shelf).id;
        previous.placedObjects.Add(new PlacedBuildSaveData{itemId=fixture,x=5,floor=1,z=5});
        previous.placedObjects.Add(new PlacedBuildSaveData{itemId=fixture,x=10,z=-40});
        previous.shelfStocks.Add(new RetailEmpireTycoon.SaveSystem.ShelfStockSaveEntry{buildItemId=fixture,anchorX=10,anchorZ=-40,productId="fixture-stock",amount=17});
        previous.buildInventory.Add(new BuildInventorySaveEntry{itemId="wall_01",count=13});
        Require(ShopProportions.Upgrade(previous,starter.building.grid,starter.catalog),"Version-one save did not receive the ten-percent conversion.");
        var moved=previous.placedObjects.Single(item=>item.floor==0);
        Require(previous.shopProportionsVersion==2 && previous.playerMoney==27182 && previous.buildInventory.Single().count==13
            && previous.placedObjects.Single(item=>item.floor==1).x==5 && previous.floorTiles.Any(tile=>tile.floor==1 && tile.x==3 && tile.z==4),"Ten-percent conversion lost money, inventory or upper-storey addresses.");
        Require(previous.shelfStocks.Single().anchorX==moved.x && previous.shelfStocks.Single().anchorZ==moved.z && previous.shelfStocks.Single().amount==17,"Ten-percent conversion lost outdoor shelf stock.");
        string converted=JsonUtility.ToJson(previous);Require(!ShopProportions.Upgrade(previous,starter.building.grid,starter.catalog) && converted==JsonUtility.ToJson(previous),"Ten-percent conversion applied twice.");
        var old=new GameData{usesModularStore=true,playerMoney=31415};
        for(int x=0;x<12;x++)for(int z=-24;z<-12;z++)old.floorTiles.Add(new FloorTileSaveData{itemId="asphalt_01",x=x,z=z});
        var shelf=new PlacedBuildSaveData{itemId="wall_01",x=3,z=4,wallModuleVersion=3};old.placedObjects.Add(shelf);
        string shelfId=starter.catalog.Items.First(item=>item!=null && item.category==BuildCategory.Shelf).id;
        old.placedObjects.Add(new PlacedBuildSaveData{itemId=shelfId,x=4,z=-20});
        old.shelfStocks.Add(new RetailEmpireTycoon.SaveSystem.ShelfStockSaveEntry{buildItemId=shelfId,anchorX=4,anchorZ=-20,productId="migration-fixture",amount=17});
        Require(ShopProportions.Upgrade(old,starter.building.grid,starter.catalog),"Old proportions were not converted.");
        Require(shelf.x==3 && shelf.z==4 && old.playerMoney==31415,"Conversion altered interior addresses or money.");
        var outdoor=old.placedObjects.Single(placed=>placed.itemId==shelfId);
        Require(old.shelfStocks.Single().anchorX==outdoor.x && old.shelfStocks.Single().anchorZ==outdoor.z && old.shelfStocks.Single().amount==17,"Outdoor shelf lost its saved stock identity.");
        string once=JsonUtility.ToJson(old);Require(!ShopProportions.Upgrade(old,starter.building.grid,starter.catalog) && once==JsonUtility.ToJson(old),"Save conversion runs twice.");
        Require(old.floorTiles.All(tile=>(tile.z+.5f)*ShopProportions.CellSize>=-4.001f && (tile.z+.5f)*ShopProportions.CellSize<-2.0004f),"Asphalt moved away from the original street.");
        var impossible=new GameData{usesModularStore=true,playerMoney=777};impossible.placedObjects.Add(new PlacedBuildSaveData{itemId="parking_01",playerParking=true});
        string unchanged=JsonUtility.ToJson(impossible);bool rejected=false;
        try{ShopProportions.Upgrade(impossible,starter.building.grid,starter.catalog);}catch(InvalidOperationException){rejected=true;}
        Require(rejected && unchanged==JsonUtility.ToJson(impossible),"Failed migration changed the original save snapshot.");
        string path=Report+"/must-not-save.json";Require(!File.Exists(path),"Stale guard-test file exists.");
        var root=new GameObject("Inactive save guard check");root.SetActive(false);var save=root.AddComponent<SaveManager>();
        typeof(SaveManager).GetField("saveFilePath",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(save,path);
        save.SaveGame();Require(!File.Exists(path),"Failed/incomplete load allowed a save write.");Object.DestroyImmediate(root);
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Finish(Exception error)
    {
        if (Doors || Decor)
        {
            SessionState.SetBool(Pending, false);
            File.WriteAllText(Report + "/play.txt", error == null ? (Decor?"PASS decor catalog, purchase, mounts, move, storage and save/load.\n":
                "PASS door catalog, placement, persistence, pedestrian and vehicle checks.\n") : "FAIL " + error);
            EditorApplication.Exit(error == null ? 0 : 1);
            return;
        }
        if (Floors)
        {
            SessionState.SetBool(Pending, false);
            File.WriteAllText(Report + "/play.txt", error == null ? "PASS floor catalog, purchase, placement and persistence.\n" : "FAIL " + error);
            EditorApplication.Exit(error == null ? 0 : 1);
            return;
        }
        SessionState.SetBool(Pending,false);File.WriteAllText(Report+"/play.txt",error==null?"PASS full construction, parking and buyer lifecycle without accessing the player's save.\n":"FAIL "+error);
        if(error==null)File.WriteAllLines(Report+"/language.txt",new[]{"PASS English-only UI, legacy locale changes cannot switch this build away from English. Verified screens:"}.Concat(englishScreens.OrderBy(name=>name)));
        if(error==null)RecordInputs();EditorApplication.Exit(error==null?0:1);
    }
}


