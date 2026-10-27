using System;
using System.Collections;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Parking;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class MobilityCityReview
{
    private const string Pending="MobilityCityReview.Pending";
    private static IEnumerator checks;
    private static double deadline;
    static MobilityCityReview(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(MobilityUpdateBatch.Report);
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
        foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        File.WriteAllText(MobilityUpdateBatch.Report+"/city.txt","RUNNING\n");SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(checks==null){deadline=EditorApplication.timeSinceStartup+180;checks=Check();}
            Require(EditorApplication.timeSinceStartup<deadline,"Game-to-city review timed out.");
            if(checks.MoveNext())return;Finish(null);
        }
        catch(Exception error){Debug.LogException(error);Finish(error);}
    }
    private static IEnumerator Check()
    {
        for(int i=0;i<4;i++)yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Save writer present.");
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();starter.cityTrip.orders.save=null;
        var data=starter.CreateNewGame(5000);Object.FindObjectOfType<StoreProgression>().ApplySaveData(data.territory);
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;starter.ConfigureSite(true);spawner.Spawn(StoreLevelId.Lvl1);starter.ApplyNewGameBuildings(data);
        for(int i=0;i<6;i++)yield return null;
        Object.FindObjectOfType<ParkingTraffic>().enabled=false;starter.cityTrip.Begin();
        while(!starter.cityTrip.InCity||starter.cityTrip.Transitioning)yield return null;
        for(int i=0;i<6;i++)yield return null;
        var world=Object.FindObjectOfType<CityWorld>();Require(world!=null,"Actual trip failed to enter City.");world.pickup.ReadKeyboard=false;world.pickup.InputEnabled=false;
        var roof=world.facadeRoot.Find("Closed store roof");
        float carHeight=CityUpdateSetup.Measure(world.pickup.gameObject).size.y;
        if(roof!=null)TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/MobilityUpdate/CityScaleProbe.png",world.pickup.transform.position+new Vector3(-8,5,-10),roof.position+Vector3.down);
        Require(roof!=null && roof.position.y>carHeight*1.35f,"Store still appears as low as the full-size player pickup: roof="+(roof!=null?roof.position.ToString():"missing")+" carHeight="+carHeight);
        Require(world.home.transform.childCount==0,"Hatched return/loading zone remains under the personal parking.");
        var personal=world.facadeRoot.GetComponentsInChildren<MeshRenderer>().Where(renderer=>renderer.name.StartsWith("Shop exterior"))
            .Count(renderer=>{var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);var color=block.GetColor("_BaseColor");return color.g>.6f && color.b<.15f;});
        Require(personal>=3,"Yellow normal-bay paint was not copied into the city.");
        Directory.CreateDirectory("Docs/MobilityUpdate");
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/MobilityUpdate/CityStore.png",world.pickup.transform.position+new Vector3(-8,5,-10),roof.position+Vector3.down);
        var population=world.town.GetComponentInChildren<TownPedestrianTraffic>();population.enabled=false;
        population.SetObserver(null);population.Simulate(.1f,world.town.transform.TransformPoint(new Vector3(-110,0,-139)),true);
        foreach(var person in population.ActivePedestrians)
        {
            var appearance=person.Root.GetComponentInChildren<ShopperAppearance>();
            Require(Mathf.Abs(appearance.RestBounds.size.y*appearance.transform.localScale.y-ShopperDimensions.StreetHeight)<.01f,"Street NPC retained miniature height.");
        }
        var walker=population.ActivePedestrians.OrderBy(person=>Vector3.Distance(person.LocalPosition,new Vector3(-110,0,-139))).First();
        var at=walker.Root.transform.position;
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/MobilityUpdate/StreetScale.png",at+new Vector3(4,2.5f,-6),at+Vector3.up*.7f);
        var stop=world.town.GetComponentsInChildren<Transform>(true).First(transform=>transform.name=="BusShelter" && transform.parent.name!="BusShelter");
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/MobilityUpdate/BusStop.png",stop.position+new Vector3(9,8,-10),stop.position+Vector3.up);
        var urbanStop=world.town.GetComponentsInChildren<Transform>(true).Single(transform=>transform.name=="05_BusStop");
        Physics.SyncTransforms();
        var apron=Physics.RaycastAll(new Vector3(90,2,-155),Vector3.down,3)
            .FirstOrDefault(hit=>hit.collider.name=="Connected bus platforms");
        Require(apron.collider!=null && Mathf.Abs(apron.point.y-.065f)<.002f,"Grey yard remains exposed around the urban shelter.");
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/MobilityUpdate/UrbanBusStop.png",urbanStop.position+new Vector3(-10,9,-12),urbanStop.position+Vector3.up);
        var traffic=world.town.GetComponentInChildren<TownRoadTraffic>();traffic.enabled=false;
        var crossing=world.town.GetComponentInChildren<TownLevelCrossing>();crossing.enabled=false;var train=crossing.train;train.enabled=false;train.SetCrossingHold(false);
        for(int i=0;i<1000 && train.TravelDistance<325;i++)train.Advance(.1f);
        crossing.Advance(4);Require(crossing.IsClosed,"Corrected barriers failed to close before train arrival.");
        foreach(var arm in crossing.arms)
        {
            bool south=arm.parent.position.z<228;var pivot=arm.parent;
            Require(Mathf.Abs(pivot.position.x-(south?-19.2f:-28.8f))<.05f,"Barrier pivot is still on the outgoing side.");
            var signal=crossing.transform.Cast<Transform>().Single(transform=>transform.name=="RailSignal" && (transform.position.z<228)==south);
            Require(Vector3.Dot(signal.forward,south?Vector3.back:Vector3.forward)>.99f,"Rail signal faces away from approaching vehicles.");
            var bounds=CityUpdateSetup.Measure(arm.gameObject);float lane=south?-22.2f:-25.8f;
            Require(bounds.min.x<lane && bounds.max.x>lane,"Closed barrier misses the incoming road lane.");
        }
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/MobilityUpdate/CrossingClosed.png",new Vector3(-10,12,202),new Vector3(-24,3,228));
        for(int i=0;i<1000 && crossing.IsWarning;i++){train.Advance(.1f);crossing.Advance(.1f);}
        crossing.Advance(4);
        Require(!crossing.IsWarning && crossing.arms.All(arm=>Quaternion.Angle(arm.localRotation,Quaternion.Euler(0,0,90))<.1f),"Corrected barriers failed to reopen after the train.");
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/MobilityUpdate/CrossingOpen.png",new Vector3(-10,12,202),new Vector3(-24,3,228));
        File.WriteAllText(MobilityUpdateBatch.Report+"/city-details.txt","PASS actual Game -> City transition without SaveManager: taller facade, full-size player car, copied yellow normal parking lines, no home hatch; 1.7 m street actors; incoming-side barriers/lights, lane coverage, closure and reopening.\n");
        starter.cityTrip.Return();while(starter.cityTrip.Transitioning)yield return null;
        Require(!starter.cityTrip.InCity,"Return to Game failed.");
    }
    private static void Require(bool condition,string message)=>MobilityUpdateBatch.Require(condition,message);
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Pending,false);
        File.WriteAllText(MobilityUpdateBatch.Report+"/city.txt",error==null?"PASS actual shop/city/return, scale, parking paint and corrected crossing lifecycle.\n":"FAIL "+error);
        if(error==null)MobilityUpdateBatch.Record("city");
        EditorApplication.Exit(error==null?0:1);
    }
}
