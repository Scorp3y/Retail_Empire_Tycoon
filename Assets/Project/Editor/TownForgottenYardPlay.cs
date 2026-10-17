using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.SaveSystem;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Exercises the actual store and purchase UI, with save components removed before Play.</summary>
[InitializeOnLoad]
public static class TownForgottenYardPlay
{
    private const string Active="ForgottenYard.Play.Active";
    private const string DetailMode="ForgottenYard.Play.Details";
    private static string Report=>SessionState.GetBool(DetailMode,false)?TownYardStoryBatch.Report:TownForgottenYardBatch.Report;
    private static string Pictures=>SessionState.GetBool(DetailMode,false)?TownYardStoryBatch.Pictures:TownForgottenYardBatch.Pictures;
    private static int phase,frame;
    private static float next;
    private static DateTime deadline;
    private static StarterStoreInitialization initializer;
    private static GameData data;
    private static GameData restoredSnapshot;
    private static TerritoryId purchased;
    private static int beforeMoney,price;
    private static string beforeBuilding;
    private static int sales;
    static TownForgottenYardPlay(){EditorApplication.update+=Tick;}
    public static void Run()=>Begin(false);
    public static void RunDetails()=>Begin(true);
    private static void Begin(bool details)
    {
        try
        {
            SessionState.SetBool(DetailMode,details);
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);
            File.WriteAllText(Report+"/play.txt","RUNNING\n");
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
            Directory.CreateDirectory("Assets/Art/TownForgottenYardFixture");AssetDatabase.Refresh();
            EditorSceneManager.SaveScene(scene,"Assets/Art/TownForgottenYardFixture/Play.unity",true);
            scene=EditorSceneManager.OpenScene("Assets/Art/TownForgottenYardFixture/Play.unity");
            foreach(var c in Object.FindObjectsOfType<SaveManager>(true))Object.DestroyImmediate(c);
            foreach(var c in Object.FindObjectsOfType<TutorialController>(true))
            {
                if(c.tutorialChoiceUI!=null)c.tutorialChoiceUI.SetActive(false);
                if(c.tutorialUI!=null)c.tutorialUI.SetActive(false);
                if(c.characterRoot!=null)c.characterRoot.SetActive(false);Object.DestroyImmediate(c);
            }
            foreach(var c in Object.FindObjectsOfType<TutorialDialogue>(true))Object.DestroyImmediate(c);
            foreach(var c in Object.FindObjectsOfType<ButtonFadeIn>(true))Object.DestroyImmediate(c);
            foreach(var c in Object.FindObjectsOfType<AvatarSpawn>(true))c.enabled=false;
            foreach(var c in Object.FindObjectsOfType<ScreenFader>(true))c.SetInstant(0);
            PlayerSettings.companyName="RetailEmpireDisposableQA";PlayerSettings.productName="ForgottenYardFixture";
            EditorSceneManager.SaveScene(scene);SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception error){Finish(error);}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||Time.frameCount<25)return;
        try
        {
            if(initializer==null)
            {
                deadline=DateTime.UtcNow.AddMinutes(4);
                Require(Object.FindObjectOfType<SaveManager>(true)==null,"QA must never access player saves.");
                initializer=Object.FindObjectOfType<StarterStoreInitialization>();data=initializer.CreateNewGame(6000);
                var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;
                initializer.land.progression.ApplySaveData(data.territory);initializer.ConfigureSite(true);spawner.Spawn(StoreLevelId.Lvl1);
                initializer.ApplyNewGameBuildings(data);frame=Time.frameCount+4;return;
            }
            Require(DateTime.UtcNow<deadline,"Runtime store/purchase validation timed out, phase "+phase);
            if(Time.frameCount<frame||Time.realtimeSinceStartup<next)return;
            var progression=initializer.land.progression;
            var money=Object.FindObjectOfType<DeliveryOrders>().money;
            var operations=Object.FindObjectOfType<StoreOperations>();
            var products=Object.FindObjectOfType<ProductSaveService>();
            if(phase==0)
            {
                products.ApplyWarehouseSaveData(data.productInventory);products.ApplyShelfSaveData(data.shelfStocks);Physics.SyncTransforms();
                Require(Object.FindObjectsOfType<PlacedObject>().Length==data.placedObjects.Count,"Missing or duplicate starter pieces.");
                Require(initializer.floors.BuildSaveData().Count==data.floorTiles.Count,"Floor snapshot not restored.");
                var shelves=Object.FindObjectsOfType<PlacedShelfStock>().Where(s=>s.GetComponent<PlacedObject>()!=null).ToArray();
                Require(shelves.Length==3&&shelves.All(s=>s.CurrentAmount>0),"Tiny store must have three stocked shelves.");
                foreach(var placed in Object.FindObjectsOfType<PlacedObject>())
                {
                    if(placed.item.isWall)Require(new Rule_WallJoint().Evaluate(new PlacementRequest(placed.item,placed.anchorCell,placed.rotated,placed.facing,placed)).ok,"Invalid wall connection.");
                    if(placed.item.category==RetailEmpireTycoon.Core.BuildCategory.Shelf)Require(new Rule_ShelfApproach(initializer.building.grid,initializer.building.territory).Evaluate(new PlacementRequest(placed.item,placed.anchorCell,placed.rotated,placed.facing,placed)).ok,"Blocked shelf "+placed.item.id);
                    if(placed.item.isParkingSpace)Require(new RetailEmpireTycoon.Parking.Rule_ParkingSurface(initializer.building.grid,initializer.floors).Evaluate(new PlacementRequest(placed.item,placed.anchorCell,placed.rotated,placed.facing,placed)).ok,"Parking or its access lacks asphalt.");
                }
                money.SetMoney(6000);operations.ApplySaveData(data.shopOperations);operations.Advance(3);
                Require(operations.CheckoutObject!=null,"No accessible checkout: "+operations.Notice);
                var staffDoor=Object.FindObjectsOfType<PlacedObject>().Where(p=>p.item.isDoorway).OrderBy(p=>p.anchorCell.x).First();
                Require(operations.Navigation.TryPath(operations.EntrancePosition,staffDoor.transform.position+Vector3.left*.6f,out _),"Warehouse doorway unreachable.");
                foreach(var shelf in shelves)Require(operations.TryShelfApproach(shelf,operations.EntrancePosition,out _),"Customer cannot reach "+shelf.name);
                RenderSettings.fog=false;
                var scene=EditorSceneManager.GetActiveScene();
                TownFloristBatch.Render(scene,Pictures+"/StarterStore.png",initializer.blueprint.cameraFocus+new Vector3(-9,13,-15),initializer.blueprint.cameraFocus,8);
                TownFloristBatch.Render(scene,Pictures+"/SiteInGame.png",new Vector3(85,72,-66),new Vector3(31,0,16),45);
                TownFloristBatch.Render(scene,Pictures+"/OldPlayground.png",new Vector3(7,13,5),new Vector3(7,0,16),11);
                operations.enabled=false;operations.SetOpen(true);Require(operations.TrySpawnCustomer(),"No real customer can enter the small store.");
                phase=1;return;
            }
            if(phase==1)
            {
                for(int i=0;i<20;i++)
                {
                    operations.Advance(.1f);
                    var visit=operations.ClaimCheckout();
                    if(visit!=null)
                    {
                        int balance=money.Money,stock=visit.Shelf.CurrentAmount;
                        Require(operations.CompleteCheckout(visit),"Native checkout failed.");
                        Require(money.Money==balance+visit.Quantity*visit.Product.SellPrice&&visit.Shelf.CurrentAmount==stock-visit.Quantity,"Checkout payment/stock mismatch.");
                        sales++;break;
                    }
                }
                if(sales==0)return;
                operations.SetOpen(false);money.SetMoney(6000);
                var rules=progression.LandRules;var parcel=rules.Parcels.First(p=>progression.IsTerritoryAvailable(p.Id));purchased=parcel.Id;price=parcel.Price;beforeMoney=money.Money;
                beforeBuilding=JsonUtility.ToJson(new GameData{placedObjects=initializer.building.BuildPlacedSaveData(),floorTiles=initializer.floors.BuildSaveData()});
                var model=Object.FindObjectOfType<TownExpansionScenery>().plots.Single(p=>p.territory==purchased).model;
                Require(model.activeSelf,"Unowned plot has no scenery.");
                var zone=Object.FindObjectsOfType<TerritoryZone>().Single(z=>z.Id==purchased);zone.SetPurchaseMode(true);
                Object.FindObjectOfType<TerritoryPurchaseController>().RequestPurchase(purchased,0); // The catalogue, not caller input, owns the price.
                ((Button)ShopUiSetup.Read(Object.FindObjectOfType<ConfirmPurchaseUI>(),"_yes")).onClick.Invoke();
                next=Time.realtimeSinceStartup+3;phase=2;return;
            }
            if(phase==2)
            {
                Require(progression.State.IsPurchased(purchased)&&money.Money==beforeMoney-price,"Real UI purchase failed or charged an incorrect price.");
                var scenery=Object.FindObjectOfType<TownExpansionScenery>();
                Require(!scenery.plots.Single(p=>p.territory==purchased).model.activeSelf,"Buying land did not clear its old scenery.");
                Require(scenery.plots.Where(p=>p.territory!=purchased).All(p=>p.model.activeSelf),"Purchase cleared neighbouring scenery.");
                var bounds=progression.LandRules.Parcels.Single(p=>p.Id==purchased).Bounds;
                var cell=initializer.land.grid.WorldToCell(new Vector3(bounds.center.x,0,bounds.center.y));
                Require(initializer.land.territory.IsCellPurchased(cell),"Purchased land not buildable.");
                var buildings=JsonUtility.ToJson(new GameData{placedObjects=initializer.building.BuildPlacedSaveData(),floorTiles=initializer.floors.BuildSaveData()});
                Require(buildings==beforeBuilding,"Purchasing land changed the existing store.");
                var snapshot=new GameData{usesModularStore=true,territory=progression.BuildSaveData(),placedObjects=initializer.building.BuildPlacedSaveData(),floorTiles=initializer.floors.BuildSaveData(),shelfStocks=products.BuildShelfSaveData(),productInventory=products.BuildWarehouseSaveData()};
                restoredSnapshot=JsonUtility.FromJson<GameData>(JsonUtility.ToJson(snapshot));
                progression.ApplySaveData(restoredSnapshot.territory);initializer.ApplyNewGameBuildings(restoredSnapshot);
                frame=Time.frameCount+3;phase=3;return;
            }
            if(phase==3)
            {
                products.ApplyWarehouseSaveData(restoredSnapshot.productInventory);products.ApplyShelfSaveData(restoredSnapshot.shelfStocks);
                string StockKey(ShelfStockSaveEntry entry)=>entry.buildItemId+":"+entry.anchorX+":"+entry.anchorZ+":"+entry.facing+":"+entry.productId+":"+entry.amount;
                Require(products.BuildShelfSaveData().Select(StockKey).OrderBy(k=>k).SequenceEqual(restoredSnapshot.shelfStocks.Select(StockKey).OrderBy(k=>k)),"Shelf stock changed during JSON restore.");
                Require(progression.State.IsPurchased(purchased)&&Object.FindObjectsOfType<PlacedObject>().Length==data.placedObjects.Count,"Purchase/building JSON roundtrip failed.");
                // Insufficient funds and remote locked land must not mutate ownership.
                money.SetMoney(0);var rules=progression.LandRules;var nextParcel=rules.Parcels.First(p=>progression.IsTerritoryAvailable(p.Id));
                Object.FindObjectOfType<TerritoryPurchaseController>().RequestPurchase(nextParcel.Id,nextParcel.Price);
                ((Button)ShopUiSetup.Read(Object.FindObjectOfType<ConfirmPurchaseUI>(),"_yes")).onClick.Invoke();
                purchased=nextParcel.Id;next=Time.realtimeSinceStartup+3;phase=4;return;
            }
            if(phase==4)
            {
                Require(!progression.State.IsPurchased(purchased)&&money.Money==0,"Unaffordable purchase changed money or land.");
                File.WriteAllText(Report+"/play.txt","PASS: real new-game build/floor/stock loading; valid thick walls, shelf and parking access; actual customer path and paid checkout; actual purchase confirmation charges catalogue price, clears only the purchased scenery, enables build rights and preserves store; JSON restore; insufficient funds rejected. Player save components absent.\n");
                if(SessionState.GetBool(DetailMode,false))TownYardStoryBatch.Receipt("play");else TownForgottenYardBatch.Receipt("play");Finish(null);
            }
        }
        catch(Exception error){Finish(error);}
    }
    private static void Require(bool value,string message)=>TownForgottenYardBatch.Require(value,message);
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);
        if(error!=null){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/play.txt","FAILED\n"+error);Debug.LogException(error);}
        EditorApplication.Exit(error==null?0:1);
    }
}
