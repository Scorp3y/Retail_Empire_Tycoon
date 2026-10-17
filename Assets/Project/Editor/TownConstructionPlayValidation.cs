using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Real shop-to-city driving, paid construction order, pickup UI loading and stock delivery in a save-free sandbox.</summary>
[InitializeOnLoad]
public static class TownConstructionPlayValidation
{
    private const string Running="Construction.Play.Running",Phase="Construction.Play.Phase",Folder="Library/TownConstructionQA",ItemKey="Construction.Play.Item",StockKey="Construction.Play.Stock";
    private static DateTime deadline;
    private static float nextFixedTime,approachDistance;
    private static Vector3 startingPoint;
    static TownConstructionPlayValidation(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/play.txt","RUNNING\n");
            using(var stream=File.OpenRead(TownWorldAssembler.PrefabPath))using(var hash=SHA256.Create())File.WriteAllText(Folder+"/play-input.sha256",BitConverter.ToString(hash.ComputeHash(stream)).Replace("-",""));
            EditorSceneManager.OpenScene("Assets/Project/Scenes/Game.unity");ShopGameplaySandbox.Open();SessionState.SetInt(Phase,-2);SessionState.SetBool(Running,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Finish(e.ToString());}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Running,false)||!EditorApplication.isPlaying)return;
        try
        {
            int phase=SessionState.GetInt(Phase,0);
            if(phase==-2)
            {
                if(Time.frameCount<15)return;Require(Object.FindObjectOfType<SaveManager>(true)==null,"Player save writer present.");
                var trip=Object.FindObjectOfType<CityTrip>();Require(trip!=null&&trip.orders!=null,"Actual trip/order dependencies missing.");trip.orders.save=null;
                var orders=trip.orders;var item=orders.buildCatalog.Items.FirstOrDefault(i=>i!=null&&!i.hiddenFromShop&&SupplierRouting.ForBuilding(i)==SupplierKind.Construction&&i.weightKg<=orders.pickupCapacityKg&&i.cargoVolumeM3<=orders.pickupCapacityM3);
                Require(item!=null,"No real catalogue construction fits the pickup.");SessionState.SetString(ItemKey,item.id);SessionState.SetInt(StockKey,orders.buildings.GetCount(item));
                orders.money.SetMoney(Mathf.Max(orders.money.Money,item.price+1000));Require(orders.Order(item),"Real construction order failed: "+orders.Notice);
                Require(orders.buildings.GetCount(item)==SessionState.GetInt(StockKey,0),"Paid construction granted before pickup.");
                deadline=DateTime.UtcNow.AddSeconds(90);trip.Begin();SessionState.SetInt(Phase,-1);return;
            }
            if(phase==-1)
            {
                var trip=Object.FindObjectOfType<CityTrip>();if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Shop-to-city transition timed out.");
                if(trip==null||!trip.InCity||trip.Transitioning)return;Require(Object.FindObjectOfType<SaveManager>(true)==null,"Save writer appeared during city trip.");SessionState.SetInt(Phase,0);return;
            }
            var city=Object.FindObjectOfType<CityWorld>();Require(city!=null,"Live City world missing.");var car=city.pickup;var bay=city.town.suppliers.Single(c=>c.supplier==SupplierKind.Construction);
            if(phase==0)
            {
                deadline=DateTime.UtcNow.AddSeconds(60);car.ReadKeyboard=false;car.InputEnabled=true;car.Stop();car.Body.position=TownConstructionPlacement.Bay+Vector3.up*.12f;car.Body.rotation=Quaternion.identity;Await(1,25);return;
            }
            if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Construction drive timed out in phase "+phase+" at "+car.Body.position);
            if(Time.fixedTime<nextFixedTime)return;
            if(phase==1)
            {
                Require(!bay.CanUse(car)&&bay.UnavailableReason(car).Contains("задом"),"Wrong-way test did not reach a stopped, incorrectly oriented pickup: reason="+bay.UnavailableReason(car)+", position="+car.Body.position+", speed="+car.Speed+", heading="+car.transform.forward+", bay="+bay.transform.position+"/"+bay.transform.forward);
                car.Stop();car.Body.position=TownConstructionPlacement.Street+Vector3.up*.12f;car.Body.rotation=Quaternion.identity;startingPoint=car.Body.position;Await(2,25);return;
            }
            if(phase==2)
            {
                if(FlatDistance(car.Body.position,TownConstructionPlacement.Staging)>.55f){car.SetInput(.45f,0,false);return;}
                car.Stop();approachDistance=FlatDistance(startingPoint,car.Body.position);car.Body.rotation=Quaternion.Euler(0,180,0);startingPoint=car.Body.position;Await(3,5);return;
            }
            if(phase==3)
            {
                if(bay.DistanceTo(car)>.55f){car.SetInput(-.45f,0,false);return;}car.Stop();Await(4,3);return;
            }
            Require(bay.CanUse(car),"Correct stopped rear docking rejected: "+bay.UnavailableReason(car));
            float reverse=FlatDistance(startingPoint,car.Body.position);Require(approachDistance>9&&reverse>10,"Actual street/reverse approaches were not completed.");
            car.Body.velocity=Vector3.forward;Require(!bay.CanUse(car)&&bay.UnavailableReason(car).Contains("Остановите"),"Moving collection accepted.");car.Stop();
            var delivery=Object.FindObjectOfType<CityTrip>().orders;var entry=delivery.Ledger.Entries.Single(e=>e.itemId==SessionState.GetString(ItemKey,"")&&e.location==DeliveryLocation.Depot);
            Require(!delivery.LoadFrom(entry,SupplierKind.Equipment),"Construction order loaded from wrong supplier.");
            city.Interact();Require(!car.InputEnabled&&Object.FindObjectsOfType<TMP_Text>().Any(t=>t.text.Contains(SupplierRouting.Name(SupplierKind.Construction))),"Actual Construction supplier modal did not open.");
            var load=Object.FindObjectsOfType<Button>().Single(b=>b.isActiveAndEnabled&&b.GetComponentInChildren<TMP_Text>()!=null&&(b.GetComponentInChildren<TMP_Text>().text=="Загрузить"||b.GetComponentInChildren<TMP_Text>().text=="Load"));load.onClick.Invoke();
            var loadedItem=delivery.buildCatalog.GetById(SessionState.GetString(ItemKey,""));int before=SessionState.GetInt(StockKey,0);
            Require(delivery.Ledger.Count(loadedItem.id,DeliveryItemKind.Building,DeliveryLocation.Pickup)==1&&delivery.buildings.GetCount(loadedItem)==before,"UI pickup did not reserve construction in cargo without granting stock.");
            city.SendMessage("ClosePanel",SendMessageOptions.RequireReceiver);Require(car.InputEnabled,"Supplier modal did not restore driving.");
            Require(delivery.Unload()&&delivery.buildings.GetCount(loadedItem)==before+1&&delivery.Ledger.Count(loadedItem.id,DeliveryItemKind.Building,DeliveryLocation.Pickup)==0,"Existing cargo-to-stock construction delivery failed.");
            Finish(null,"PASS: actual pickup drove "+approachDistance.ToString("F2")+" m from street and reversed "+reverse.ToString("F2")+" m to Construction pickup. Wrong-way/moving docking and wrong supplier rejected; real catalogue construction ordered, supplier modal opened, actual Load button moved the item into cargo, stock granted only after Unload. No player-save writes or synthetic physics.\n");
        }
        catch(Exception e){Finish(e.ToString());}
    }
    private static void Await(int phase,int steps){nextFixedTime=Time.fixedTime+steps*Time.fixedDeltaTime;SessionState.SetInt(Phase,phase);}
    private static float FlatDistance(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
    private static void Require(bool condition,string error){if(!condition)throw new InvalidOperationException(error);}
    private static void Finish(string error,string success=null){SessionState.SetBool(Running,false);Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/play.txt",error==null?success:"FAILED\n"+error);EditorApplication.Exit(error==null?0:1);}
}
