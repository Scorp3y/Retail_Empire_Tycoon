using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Native City/Game Play checks without loading or writing the player's store save.</summary>
[InitializeOnLoad]
public static class StreetLifeValidation
{
    private const string Pending="StreetLifeQA.Pending";
    private static IEnumerator checks;
    private static double deadline;
    static StreetLifeValidation(){EditorApplication.update+=Tick;}
    public static void City()=>Begin(false);
    public static void Shop()=>Begin(true);
    private static void Begin(bool shop)
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(StreetLifeBatch.Report);
        File.WriteAllText(StreetLifeBatch.Report+(shop?"/shop.txt":"/city.txt"),"RUNNING\n");
        EditorSceneManager.OpenScene(shop?CityUpdateSetup.GamePath:CityUpdateSetup.CityPath);
        if(shop)
        {
            ShopGameplaySandbox.Open();
            foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        SessionState.SetBool("StreetLifeQA.Shop",shop);SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        try
        {
            if(checks==null){deadline=EditorApplication.timeSinceStartup+180;checks=Check();}
            Require(EditorApplication.timeSinceStartup<deadline,"Street life validation timed out.");
            if(checks.MoveNext())return;
            Finish(null);
        }
        catch(Exception error){Debug.LogException(error);Finish(error);}
    }
    private static IEnumerator Check()
    {
        for(int i=0;i<20;i++)yield return null;
        bool shop=SessionState.GetBool("StreetLifeQA.Shop",false);
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Save writer present in disposable scene.");
        var population=Object.FindObjectOfType<TownPedestrianTraffic>();
        var traffic=Object.FindObjectOfType<TownRoadTraffic>();
        Require(population!=null && traffic!=null,"Saved scene is missing pedestrians or vehicles.");
        var city=Object.FindObjectOfType<CityWorld>();
        if(city!=null){city.pickup.ReadKeyboard=false;city.pickup.InputEnabled=false;}
        var eye=new GameObject("Disposable street observer").transform;
        eye.position=population.transform.TransformPoint(new Vector3(-110,0,-139));population.SetObserver(eye);
        float began=Time.time;
        while(Time.time-began<2)yield return null;
        Require(population.ActivePedestrians.Count>0,"Automatic street Update did not spawn anybody.");
        population.enabled=false;traffic.enabled=false;
        var seen=new HashSet<int>();
        foreach(var district in new[]{new Vector3(-110,0,-139),new Vector3(168,0,48),new Vector3(-120,0,170),new Vector3(180,0,-144)})
        {
            population.SetObserver(null);
            for(int frame=0;frame<300;frame++)
            {
                population.Simulate(.1f,population.transform.TransformPoint(district),true);
                foreach(var person in population.ActivePedestrians)
                {
                    seen.Add(person.Appearance);
                    Require(person.Appearance>=1 && person.Appearance<=50 && !person.Animator.GetBool("isBrowsing"),"Street actor has the wrong appearance or shopping animation.");
                    var path=population.Routes.Routes[person.RouteIndex].Path;var point=path.Sample(person.Distance,out _);
                    Require(Vector3.Distance(person.LocalPosition,point)<.36f,"Pedestrian left the checked sidewalk.");
                    Require(person.Distance>=0 && person.Distance<=path.Length,"Pedestrian walked beyond a junction endpoint.");
                }
                Require(population.PoolCount<=32 && population.ActivePedestrians.Count<=32,"Unbounded pedestrian population.");
            }
            Require(population.ActivePedestrians.Count>=4 && population.ActivePedestrians.Sum(person=>person.Travelled)>8,"No actual walking in district: "+district);
            yield return null;
        }
        Require(seen.Count==50,"Not all fifty appearances participated in streamed street visits: "+seen.Count);
        var remote=population.transform.TransformPoint(new Vector3(3000,0,3000));population.Simulate(.1f,remote,false);
        Require(population.ActivePedestrians.Count==0,"Distant city actors kept running outside the observer neighbourhood.");
        long steps=population.SimulatedSteps;for(int i=0;i<10;i++)population.Simulate(.1f,remote,false);
        Require(population.SimulatedSteps==steps,"Map-wide pedestrian updates detected.");
        population.Simulate(.1f,population.transform.TransformPoint(new Vector3(-110,0,-139)),true);
        Require(population.ActivePedestrians.Count>0 && population.PoolCount<=32,"Population failed to resume after returning.");
        // A parked vehicle in front of a walker is an obstacle, not something to walk through.
        var actor=population.ActivePedestrians.First(person=>person.Animator.GetBool("isWalking") && person.Distance>1 && person.Distance<population.Routes.Routes[person.RouteIndex].Path.Length-1);
        var direction=actor.Root.transform.forward;
        var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Disposable pedestrian obstacle";
        float scale=population.transform.lossyScale.x;obstacle.transform.position=actor.Root.transform.position+direction*.45f*scale+Vector3.up*.4f*scale;
        obstacle.transform.localScale=new Vector3(.3f,.8f,.3f)*scale;Physics.SyncTransforms();float before=actor.Travelled;
        for(int i=0;i<5;i++)population.Simulate(.1f,population.transform.TransformPoint(new Vector3(-110,0,-139)),true);
        Require(actor.Travelled-before<.08f,"Pedestrian walked into a solid obstacle.");Object.DestroyImmediate(obstacle);
        StreetLifeBatch.ValidateSignals(population.GetComponentInParent<TownWorldPlaces>(),traffic);
        SignalRules(traffic);
        Directory.CreateDirectory("Docs/StreetLife");
        var focus=population.ActivePedestrians.OrderBy(person=>Vector3.Distance(person.LocalPosition,new Vector3(-110,0,-139))).First().Root.transform.position;
        TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),"Docs/StreetLife/"+(shop?"ShopStreet":"CityStreet")+".png",focus+new Vector3(3,2.5f,-5)*scale,focus+Vector3.up*.45f*scale);
        File.WriteAllText(StreetLifeBatch.Report+"/"+(shop?"shop-details":"city-details")+".txt",
            "PASS automatic Play Update; four sidewalk neighbourhoods, fifty appearances, <=32 pooled/active; checked-route confinement, obstacle stop, distant unload and return; native scale="+scale+".\n"
            +"PASS six saved signal bindings match visible lamps; four directions stop at red/amber, green releases; green-to-red reservation at stop line revoked; cars already inside clear junction. No player save accessed.\n");
    }
    private static void SignalRules(TownRoadTraffic original)
    {
        var serialized=new SerializedObject(original);var designs=serialized.FindProperty("fleet");
        var fleet=Enumerable.Range(0,designs.arraySize).Select(i=>(TrafficVehicle)designs.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        var signals=serialized.FindProperty("signals");var signal=(TownJunctionSignals)signals.GetArrayElementAtIndex(0).objectReferenceValue;
        foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.right,Vector3.left})
        {
            var graph=ScriptableObject.CreateInstance<TrafficRoadNetwork>();var right=Vector3.Cross(Vector3.up,direction)*1.8f;
            graph.Configure(new[]{new TrafficRoadNetwork.Junction{position=Vector3.zero,signalIndex=0},new TrafficRoadNetwork.Junction{position=direction*100,signalIndex=0}},
                new[]{new TrafficRoadNetwork.Lane{from=0,to=1,points=new[]{direction*5.5f+right,direction*94.5f+right}},
                    new TrafficRoadNetwork.Lane{from=1,to=0,points=new[]{direction*94.5f-right,direction*5.5f-right}}});
            var root=new GameObject("Disposable stop-line test");root.transform.position=new Vector3(0,0,-900);root.transform.localScale=original.transform.lossyScale;
            var harness=root.AddComponent<TownRoadTraffic>();harness.enabled=false;harness.Configure(graph,fleet,new[]{signal},null,1);
            var eye=root.transform.TransformPoint(direction*50);
            bool ns=Mathf.Abs(direction.z)>.5f;double red=(ns?30:0)-signal.PhaseOffset,green=(ns?0:30)-signal.PhaseOffset;
            for(int i=0;i<2200;i++)harness.Simulate(.02f,red,eye,false);
            var car=harness.ActiveCars.Single();
            Require(car.Speed<.001f && !car.IsTurning && Mathf.Abs(car.Remaining-7-car.Length*.5f)<.01f,"A car crossed the red stop line.");
            float held=car.Travelled;for(int i=0;i<100;i++)harness.Simulate(.02f,red,eye,false);
            Require(Mathf.Abs(car.Travelled-held)<.001f,"Stopped car crept through red.");
            int lane=car.LaneIndex,next=graph.Departures(graph.Lanes[lane].to)[0];
            car.Place(graph,lane,graph.Lanes[lane].Path.Length-7-car.Length*.5f-.02f,next);
            harness.Simulate(.02f,green,eye,false);Require(car.ReservedJunction>=0,"Green did not reserve the crossing.");
            // Regression: the old 5 cm exemption kept an unused green reservation on red.
            for(int i=0;i<400;i++)harness.Simulate(.02f,red,eye,false);
            Require(car.ReservedJunction<0 && !car.IsTurning && car.Remaining>=7+car.Length*.5f-.001f && car.Speed<.001f,"Green-to-red change leaked a reservation across the stop line.");
            foreach(double phase in ns?new[]{20d,23,25,46,51,63}:new[]{0d,20,25,46,51,63})
                for(int i=0;i<5;i++)harness.Simulate(.02f,phase-signal.PhaseOffset,eye,false);
            Require(car.Speed<.001f,"Red/amber or pedestrian phase released a car.");
            for(int i=0;i<900;i++)harness.Simulate(.02f,green,eye,false);
            Require(car.CompletedTurns>0 && car.Travelled>held+5,"Car did not resume on green.");
            car.Place(graph,lane,graph.Lanes[lane].Path.Length-7-car.Length*.5f+.5f,next);
            car.ReservedJunction=graph.Lanes[lane].to;float entered=car.Travelled;
            for(int i=0;i<100;i++)harness.Simulate(.02f,red,eye,false);
            Require(car.Travelled>entered+1,"A car already past the line was stranded inside the junction.");
            Object.DestroyImmediate(root);Object.DestroyImmediate(graph);
        }
    }
    private static void Require(bool condition,string message)=>StreetLifeBatch.Require(condition,message);
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Pending,false);bool shop=SessionState.GetBool("StreetLifeQA.Shop",false);
        File.WriteAllText(StreetLifeBatch.Report+(shop?"/shop.txt":"/city.txt"),error==null?"PASS street pedestrians and traffic signal rules in actual "+(shop?"Game":"City")+" Play Mode.\n":"FAIL "+error);
        if(error==null)File.WriteAllLines(StreetLifeBatch.Report+(shop?"/shop-inputs.sha256":"/city-inputs.sha256"),
            StreetLifeBatch.Inputs().Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
        EditorApplication.Exit(error==null?0:1);
    }
}
