using System;
using System.Collections;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Parking;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class MobilityUpdatePlay
{
    private const string Pending="MobilityUpdate.Play";
    private static IEnumerator checks;
    private static double deadline;
    static MobilityUpdatePlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(MobilityUpdateBatch.Report);
        File.WriteAllText(MobilityUpdateBatch.Report+"/play.txt","RUNNING\n");
        EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
        foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(checks==null){deadline=EditorApplication.timeSinceStartup+180;checks=Check();}
            Require(EditorApplication.timeSinceStartup<deadline,"Mobility gameplay check timed out.");
            if(checks.MoveNext())return;Finish(null);
        }
        catch(Exception error){Debug.LogException(error);Finish(error);}
    }
    private static IEnumerator Check()
    {
        for(int i=0;i<4;i++)yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Player save writer present.");
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();starter.cityTrip.orders.save=null;
        var data=starter.CreateNewGame(5000);Object.FindObjectOfType<StoreProgression>().ApplySaveData(data.territory);
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;starter.ConfigureSite(true);spawner.Spawn(StoreLevelId.Lvl1);starter.ApplyNewGameBuildings(data);
        for(int i=0;i<4;i++)yield return null;
        var parking=Object.FindObjectOfType<ParkingTraffic>();parking.enabled=false;
        var shop=Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();shop.SetOpen(false);
        var bays=Object.FindObjectsOfType<ParkingBay>().Where(bay=>bay.GetComponentInParent<BuildPreview>()==null).ToArray();
        Require(bays.Length==3 && bays.All(bay=>bay.width>1.2f && bay.length>2.5f),"Default parking retained miniature bay geometry.");
        var fleet=Resources.Load<ParkingVisitorFleet>("Parking/Visitors");Require(fleet!=null && fleet.Vehicles.Count==14 && fleet.Vehicles.All(car=>ParkingVisitorFleet.IsAllowed(car.DesignId)),"Forbidden vehicle in parking roster.");
        int cursor=0;Require(fleet.NextFitting(bays[0],ref cursor)!=null,"No passenger car fits a current bay.");
        foreach(var placed in Object.FindObjectsOfType<PlacedObject>().Where(p=>p.item!=null && p.item.isDoorway))
            Require(Mathf.Abs(placed.item.placementBounds.size.x-placed.item.footprint.x*starter.building.grid.cellSize)<.002f,"Door frame does not fill the grid opening.");
        foreach(var bay in bays)
        {
            var renderer=bay.GetComponentInChildren<MeshRenderer>();var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
            var color=block.GetColor("_BaseColor");bool personal=bay.GetComponent<PlacedObject>().playerParking;
            Require(personal?color.g>.6f&&color.b<.15f:color.b>.7f,"Personal/customer line colours are wrong.");
        }
        Directory.CreateDirectory("Docs/MobilityUpdate");
        var focus=starter.blueprint.cameraFocus;
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/MobilityUpdate/StoreJoints.png",focus+new Vector3(-7,10,-10),focus);
        parking.arrivalInterval=.05f;parking.parkingStaySeconds=20;
        GameObject parked=null;float closest=float.MaxValue;float nearestAngle=0;
        for(int step=0;step<15000;step++)
        {
            parking.Advance(.02f);Physics.SyncTransforms();
            foreach(var visitor in parking.VisitorCars)
            {
                var vehicle=visitor.GetComponent<TrafficVehicle>();Require(vehicle!=null&&ParkingVisitorFleet.IsAllowed(vehicle.DesignId),"Legacy/truck visitor entered the store.");
                Require(Mathf.Abs(visitor.transform.lossyScale.x-.5f)<.001f,"Visitor car uses miniature scene scale.");
                foreach(var bay in bays.Where(b=>!b.GetComponent<PlacedObject>().playerParking))
                {
                    float gap=Vector3.Distance(visitor.transform.position,bay.ParkPosition);
                    if(gap<closest){closest=gap;nearestAngle=Quaternion.Angle(visitor.transform.rotation,bay.ParkRotation);}
                    if(Vector3.Distance(visitor.transform.position,bay.ParkPosition)<.04f && Quaternion.Angle(visitor.transform.rotation,bay.ParkRotation)<1)
                    {Require(bay.Fits(vehicle.Width*.5f,vehicle.Length*.5f),"Vehicle overhangs the parking paint.");parked=visitor;}
                }
            }
            if(parked!=null)break;
            if(step%250==0)yield return null;
        }
        if(parked==null)
        {
            File.WriteAllText(MobilityUpdateBatch.Report+"/parking-state.txt",parking.MovementState+" closest="+closest+" angle="+nearestAngle);
            TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/MobilityUpdate/ParkingFailure.png",starter.blueprint.cameraFocus+new Vector3(-6,12,-15),starter.blueprint.cameraFocus);
        }
        Require(parked!=null,"A full-size visitor did not reach a bay. Cars="+parking.VisitorCars.Count+" / "+parking.MovementState);
        Time.timeScale=1;
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/MobilityUpdate/Parking.png",parked.transform.position+new Vector3(5,5,-6),parked.transform.position);
        var old=parked;parking.parkingStaySeconds=0;
        for(int step=0;step<15000 && old!=null;step++){parking.Advance(.02f);Physics.SyncTransforms();if(step%250==0)yield return null;}
        Require(old==null,"Parked visitor failed to leave and release the bay.");Time.timeScale=1;parking.enabled=false;
        QueueRegression();
        File.WriteAllText(MobilityUpdateBatch.Report+"/parking.txt","PASS 14 eligible passenger/light designs, no legacy mini cars/trucks; real arrival into sized bay, correct final pose, wheel animation and departure; white visitor/yellow personal lines.\n");
    }
    private static void QueueRegression()
    {
        var original=Object.FindObjectOfType<TownRoadTraffic>();original.enabled=false;
        var serialized=new SerializedObject(original);var list=serialized.FindProperty("fleet");
        var fleet=Enumerable.Range(0,list.arraySize).Select(i=>(TrafficVehicle)list.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        var signal=(TownJunctionSignals)serialized.FindProperty("signals").GetArrayElementAtIndex(0).objectReferenceValue;
        var graph=ScriptableObject.CreateInstance<TrafficRoadNetwork>();graph.Configure(
            new[]{new TrafficRoadNetwork.Junction{position=Vector3.zero},new TrafficRoadNetwork.Junction{position=Vector3.right*100,signalIndex=0}},
            new[]{new TrafficRoadNetwork.Lane{from=0,to=1,points=new[]{new Vector3(5.5f,0,-1.8f),new Vector3(94.5f,0,-1.8f)}},new TrafficRoadNetwork.Lane{from=1,to=0,points=new[]{new Vector3(94.5f,0,1.8f),new Vector3(5.5f,0,1.8f)}}});
        var root=new GameObject("Disposable green queue regression");root.transform.position=Vector3.back*900;
        var harness=root.AddComponent<TownRoadTraffic>();harness.enabled=false;harness.Configure(graph,fleet,new[]{signal},null,2);
        var eye=root.transform.TransformPoint(Vector3.right*50);double green=30-signal.PhaseOffset;
        harness.Simulate(.02f,green,eye,false);var follower=harness.ActiveCars[0];var leader=harness.ActiveCars[1];
        float leadAt=graph.Lanes[0].Path.Length-7-leader.Length*.5f;
        leader.Place(graph,0,leadAt,1);float followAt=leadAt-(follower.Length+leader.Length)*.5f-2;
        follower.Place(graph,0,followAt-2,1);follower.Advance(3,2);
        harness.Simulate(.02f,green,eye,false);
        Require(follower.ReservedJunction<0 && leader.ReservedJunction==1,"A moving follower stole its stopped leader's green reservation.");
        for(int i=0;i<900;i++)harness.Simulate(.02f,green,eye,false);
        Require(leader.CompletedTurns>0 && follower.CompletedTurns>0,"A green two-car queue remained deadlocked.");
        File.WriteAllText(MobilityUpdateBatch.Report+"/queue.txt","PASS exact stopped-leader/moving-follower regression: rear car cannot acquire/retain a blocking reservation; both cars clear green.\n");
        Object.DestroyImmediate(root);Object.DestroyImmediate(graph);
    }
    private static void Require(bool condition,string message)=>MobilityUpdateBatch.Require(condition,message);
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Pending,false);Time.timeScale=1;
        File.WriteAllText(MobilityUpdateBatch.Report+"/play.txt",error==null?"PASS real parking arrival/pose/departure and green queue reservation regression, without player save access.\n":"FAIL "+error);
        if(error==null)MobilityUpdateBatch.Record("play");
        EditorApplication.Exit(error==null?0:1);
    }
}
