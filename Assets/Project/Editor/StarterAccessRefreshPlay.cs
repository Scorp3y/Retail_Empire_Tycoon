using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Economy;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Exercises new/save restore in a workspace-only file, catalog stock and gate-derived unloading in real scenes.</summary>
[InitializeOnLoad]
public static class StarterAccessRefreshPlay
{
    private const string Pending="StarterAccessRefresh.Pending";
    private static IEnumerator checks;private static double deadline;
    static StarterAccessRefreshPlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(StarterAccessRefreshBatch.Report);Directory.CreateDirectory(StarterAccessRefreshBatch.Pictures);
        File.WriteAllText(StarterAccessRefreshBatch.Report+"/play.txt","RUNNING\n");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
        foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(checks==null){checks=Check();deadline=EditorApplication.timeSinceStartup+240;}
            Require(EditorApplication.timeSinceStartup<deadline,"Starter access checks timed out.");
            if(checks.MoveNext())return;StarterAccessRefreshBatch.Record();Finish(null);
        }
        catch(Exception error){Debug.LogException(error);Finish(error);}
    }
    private static IEnumerator Check()
    {
        for(int i=0;i<5;i++)yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Unexpected player save writer.");
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();var trip=starter.cityTrip;var orders=trip.orders;orders.save=null;
        var money=orders.money;Require(money.Money==300000,"Saved Game scene still starts with the old wallet.");
        // Awake only constructs the usual path. Override it before any lifecycle/load/save method runs.
        var owner=new GameObject("Workspace-only save lifecycle");var save=owner.AddComponent<SaveManager>();save.enabled=false;
        string path=Path.GetFullPath(StarterAccessRefreshBatch.Report+"/NewGame-"+Guid.NewGuid().ToString("N")+".json");
        typeof(SaveManager).GetField("saveFilePath",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(save,path);
        typeof(SaveManager).GetField("starterStore",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(save,starter);
        var operations=Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();operations.enabled=false;
        Object.FindObjectOfType<RetailEmpireTycoon.Parking.ParkingTraffic>().enabled=false;
        save.LoadGame();for(int i=0;i<5;i++)yield return null;
        Require(money.Money==300000,"New-game path did not apply 300000.");
        var stock=starter.building.inventory.BuildSaveData();Require(stock.Sum(entry=>entry.count)==53,"Incorrect new-game stock total.");
        foreach(string id in new[]{"stairs_01","elevator_01","escalator_01","escalator_down_01"})Require(stock.Single(entry=>entry.itemId==id).count==1,"Missing starter transport: "+id);
        money.SetMoney(12345);save.SaveGame();Require(File.Exists(path),"Workspace save was not written.");
        money.SetMoney(987);save.LoadGame();for(int i=0;i<5;i++)yield return null;
        Require(money.Money==12345&&starter.building.inventory.BuildSaveData().Sum(entry=>entry.count)==53,"Existing balance replaced or starter gifts duplicated.");
        Object.Destroy(owner);yield return null;orders.save=null;money.SetMoney(GameData.StartingCapital);
        File.WriteAllText(StarterAccessRefreshBatch.Report+"/economy.txt","PASS actual SaveManager no-file branch grants 300000 and 53 stock units including all four transports; reload from workspace-only JSON preserves 12345 and grants no duplicates. Player save path never opened.\n");
        var shop=Object.FindObjectOfType<ShopWindow>(true);shop.gameObject.SetActive(true);shop.OpenCategory_Structures();yield return null;
        var names=shop.GetComponentsInChildren<TMP_Text>().Select(label=>label.text).ToArray();
        foreach(string id in new[]{"stairs_01","elevator_01","escalator_01","escalator_down_01"})Require(names.Contains(ShopText.Item(starter.catalog.GetById(id))),"Transport missing from equipment shop: "+id);
        shop.Close();var workspace=Object.FindObjectOfType<BuildEditorHud>(true);workspace.Open();yield return null;
        Require(Object.FindObjectsOfType<TMP_Text>().Any(label=>label.text=="Down escalator"),"Down escalator missing from construction storage.");
        CaptureUi("StarterStorage");workspace.Close();for(int i=0;i<3;i++)yield return null;
        foreach(string id in new[]{"escalator_01","escalator_down_01"})
        {
            var item=starter.catalog.GetById(id);Require(item.footprint.x==4&&Mathf.Abs(item.placementBounds.size.x-1.0440512f)<.003f,"Escalator was not narrowed by an additional 20%.");
        }
        File.WriteAllText(StarterAccessRefreshBatch.Report+"/catalog.txt","PASS stairs, lift, reversible escalator and down escalator exist in actual equipment shop; Down escalator is owned in construction storage; both escalators now fit 4x10 and are 64% of original model width.\n");
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),StarterAccessRefreshBatch.Pictures+"/StarterSite.png",starter.blueprint.cameraFocus+new Vector3(-12,20,-22),starter.blueprint.cameraFocus+Vector3.back*2);
        CaptureUi("HammerHUD");
        var gate=Object.FindObjectsOfType<PlacedObject>().First(placed=>placed.item!=null&&placed.item.isUnloadingGate);
        var layout=Object.FindObjectOfType<TownWorldPlaces>().layout;
        Vector3 gatePosition=layout.MapShopPoint(gate.transform.position);Quaternion gateRotation=gate.transform.rotation;float gateDepth=gate.item.placementBounds.size.z*layout.shopToCityScale;
        for(int facing=0;facing<4;facing++)
        {
            Vector3 outward=Quaternion.Euler(0,facing*90,0)*Vector3.forward;Vector3 p=outward*10;
            var pose=CityWorld.UnloadingPose(p,Quaternion.LookRotation(-outward),Vector3.zero,1.1f,4.4f);
            Require(Vector3.Dot(pose.Rotation*Vector3.forward,outward)>.999f&&Vector3.Distance(new Vector3(pose.Position.x,0,pose.Position.z),p+outward*3.4f)<.001f,"Gate pose is incorrect in orientation "+facing);
        }
        trip.Begin();while(trip.Transitioning||!trip.InCity)yield return null;
        var city=Object.FindObjectOfType<CityWorld>();city.pickup.ReadKeyboard=false;city.chaseCamera.ReadMouse=false;
        Require(city.unload.gameObject.activeInHierarchy&&city.unload.requiresRearDocking,"Placed gate has no reverse-docking zone.");
        Require(city.unload.GetComponentsInChildren<Renderer>(true).Length==0,"Yellow loading platform still exists.");
        Vector3 delta=city.unload.transform.position-gatePosition;delta.y=0;
        Require(Vector3.Cross(delta,gateRotation*Vector3.forward).magnitude<.01f,"Loading zone is diagonal instead of in front of the gate.");
        var core=city.facadeRoot.GetComponentsInChildren<BoxCollider>().Single(collider=>collider.name=="Shop interior is closed");
        var poseExpected=CityWorld.UnloadingPose(gatePosition,gateRotation,core.transform.position,gateDepth,city.pickup.GetComponent<BoxCollider>().size.z);
        Require(Vector3.Distance(poseExpected.Position,city.unload.transform.position)<.001f,"Loading zone does not follow gate position.");
        float beforeDistance=delta.magnitude;Require(trip.Fleet.BuyAndSelect("heavy_box_truck"),"Cannot prepare heavy truck for dock test.");
        delta=city.unload.transform.position-gatePosition;delta.y=0;Require(delta.magnitude>beforeDistance+1,"Loading zone did not adjust for the longer vehicle.");
        city.pickup.Body.position=city.unload.transform.position;city.pickup.Body.rotation=city.unload.transform.rotation*Quaternion.Euler(0,180,0);city.pickup.Stop();
        Require(!city.unload.CanUse(city.pickup),"Wrong-way unloading accepted.");
        city.pickup.Body.rotation=city.unload.transform.rotation;Require(city.unload.CanUse(city.pickup),"Correct rear docking refused.");
        var floor=starter.catalog.GetById("floor_cream");int before=orders.buildings.GetCount(floor);
        orders.shopCapacityKg=Mathf.Max(orders.shopCapacityKg,orders.ShopWeightKg+floor.weightKg+1);
        orders.Ledger.Order(floor.id,DeliveryItemKind.Building,1);orders.Ledger.Transfer(floor.id,DeliveryItemKind.Building,DeliveryLocation.Depot,DeliveryLocation.Pickup,1);
        city.Interact();Require(orders.buildings.GetCount(floor)==before+1&&orders.PickupWeightKg==0,"Invisible zone did not deliver actual cargo.");
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),StarterAccessRefreshBatch.Pictures+"/InvisibleDock.png",gatePosition+new Vector3(-11,8,-10),gatePosition+Vector3.up);
        trip.Return();while(trip.Transitioning||trip.InCity)yield return null;
        var editing=starter.building.GetComponent<BuildEditingController>();editing.Begin();editing.Select(gate);Require(!editing.DeleteSelected(),"Last mandatory unloading gate can be deleted.");editing.Finish();
        // A damaged/older layout may have no active gate. Simulate it without bypassing normal deletion rules.
        gate.gameObject.SetActive(false);yield return null;
        trip.Begin();while(trip.Transitioning||!trip.InCity)yield return null;
        city=Object.FindObjectOfType<CityWorld>();Require(!city.unload.gameObject.activeInHierarchy&&!city.unload.CanUse(city.pickup),"Unloading remains active without a gate.");
        trip.Return();while(trip.Transitioning||trip.InCity)yield return null;
        File.WriteAllText(StarterAccessRefreshBatch.Report+"/dock.txt","PASS four gate orientations, exact gate-derived placement, invisible renderer-free zone, adjustment for heavy truck length, wrong-heading rejection, actual cargo delivery and disabled unloading after gate removal; real Game/City journeys.\n");
    }
    private static void CaptureUi(string name)
    {
        var camera=Camera.main;var target=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;var oldTarget=camera.targetTexture;
        var canvases=Object.FindObjectsOfType<Canvas>().Where(canvas=>canvas.isRootCanvas&&canvas.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var cameras=canvases.Select(canvas=>canvas.worldCamera).ToArray();var planes=canvases.Select(canvas=>canvas.planeDistance).ToArray();
        var texture=new Texture2D(1600,1000,TextureFormat.RGBA32,false);
        try
        {
            target.Create();camera.targetTexture=target;
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.6f;}
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();File.WriteAllBytes(StarterAccessRefreshBatch.Pictures+"/"+name+".png",texture.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
            camera.targetTexture=oldTarget;RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(texture);Canvas.ForceUpdateCanvases();
        }
    }
    private static void Finish(Exception error)
    {SessionState.SetBool(Pending,false);File.WriteAllText(StarterAccessRefreshBatch.Report+"/play.txt",error==null?"PASS starter capital, stock, catalog, narrow escalators and invisible gate docking.\n":"FAIL "+error);EditorApplication.Exit(error==null?0:1);}
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
