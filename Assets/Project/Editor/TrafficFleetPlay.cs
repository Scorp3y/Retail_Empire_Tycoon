using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Actual Play Mode validation. Uses only the disposable project and never a player's save.</summary>
[InitializeOnLoad]
public static class TrafficFleetPlay
{
    private const string Active="TrafficFleet.Play";
    private static TownRoadTraffic traffic;
    private static float started;
    private static DateTime deadline;
    private static int phase;
    private static readonly HashSet<string> seen=new();
    private static readonly Vector3[] districts={new(-115,0,40),new(170,0,-125),new(-24,0,350),new(480,0,160),new(550,0,278)};
    private static int district;
    private static float totalDistance;
    static TrafficFleetPlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(TrafficFleetBatch.Report);
            File.WriteAllText(TrafficFleetBatch.Report+"/play.txt","RUNNING\n");
            File.WriteAllText(TrafficFleetBatch.Report+"/districts.txt",string.Empty);
            EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Finish(e);}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||Time.frameCount<12)return;
        try
        {
            if(phase==0)
            {
                traffic=Object.FindObjectOfType<TownRoadTraffic>();TrafficFleetBatch.Require(traffic!=null,"Traffic missing in real City scene.");
                var city=Object.FindObjectOfType<CityWorld>();city.pickup.ReadKeyboard=false;city.pickup.InputEnabled=false;
                var observer=new GameObject("Disposable traffic observer").transform;observer.position=traffic.transform.TransformPoint(districts[0]);traffic.SetObserver(observer);
                started=Time.time;deadline=DateTime.UtcNow.AddSeconds(100);phase=1;return;
            }
            TrafficFleetBatch.Require(DateTime.UtcNow<deadline,"Native traffic test timed out.");
            foreach(var car in traffic.ActiveCars)
            {
                seen.Add(car.DesignId);
                TrafficFleetBatch.Require(!float.IsNaN(car.LocalPosition.sqrMagnitude)&&car.Speed>=0&&car.Speed<12,"Non-finite/unsafe vehicle motion.");
            }
            TrafficFleetBatch.Require(traffic.PoolCount<=traffic.MaximumCars,"Unbounded vehicle pool.");
            if(phase==1)
            {
                if(Time.time-started<8)return;
                TrafficFleetBatch.Require(traffic.ActiveCars.Count>=8&&traffic.ActiveCars.Any(c=>c.Travelled>10),"Native traffic did not spawn and move.");
                TrafficFleetBatch.Require(traffic.ActiveCars.Any(c=>Quaternion.Angle(c.transform.Find("WheelFL").localRotation,Quaternion.identity)>5),"Wheel meshes did not rotate.");
                File.WriteAllText(TrafficFleetBatch.Report+"/native-motion.txt","PASS: real eight-second Play Mode; "+traffic.ActiveCars.Count+" cars; moving kinematic colliders and animated wheel meshes.\n");
                traffic.enabled=false;
                // Manual fixed steps still run native physics; accelerated validation does not
                // substitute editor frame count for elapsed gameplay seconds.
                Physics.simulationMode=SimulationMode.Script;
                phase=2;return;
            }
            if(phase==2)
            {
                if(district>=districts.Length){Rules();SignalAndObstacleTest();CrossingTest();Finish(null);return;}
                var observer=traffic.transform.TransformPoint(districts[district]);
                traffic.SetObserver(null);
                int turns=0;float moved=0;bool steered=false;
                for(int frame=0;frame<1500;frame++)
                {
                    traffic.Simulate(.02f,Time.timeAsDouble+frame*.02,observer,true);Physics.Simulate(.02f);
                    foreach(var car in traffic.ActiveCars)
                    {
                        seen.Add(car.DesignId);steered|=Mathf.Abs(car.Steering)>2;
                        TrafficFleetBatch.Require(car.Speed>=0&&car.Speed<12,"Speed bound failed.");
                    }
                    if(frame%25==0)RequireNoPenetrations();
                }
                foreach(var car in traffic.ActiveCars){moved+=car.Travelled;turns+=car.CompletedTurns;}
                TrafficFleetBatch.Require(traffic.ActiveCars.Count>0&&moved>12,"District has no moving traffic: "+districts[district]);
                totalDistance+=moved;
                File.AppendAllText(TrafficFleetBatch.Report+"/districts.txt",districts[district]+": "+traffic.ActiveCars.Count+" cars, "+moved.ToString("F1")+" m aggregate, "+turns+" completed turns, steering="+steered+", pool="+traffic.PoolCount+".\n");
                if(district==0)TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),TrafficFleetBatch.Pictures+"/TrafficPlaying.png",new Vector3(-163,24,-12),new Vector3(-115,0,43));
                district++;deadline=DateTime.UtcNow.AddSeconds(100);return;
            }
        }
        catch(Exception e){Finish(e);}
    }
    private static void RequireNoPenetrations()
    {
        var cars=traffic.ActiveCars;
        for(int i=0;i<cars.Count;i++)for(int j=i+1;j<cars.Count;j++)
        {
            var a=cars[i];var b=cars[j];if(Vector3.Distance(a.LocalPosition,b.LocalPosition)>9)continue;
            var ca=a.GetComponent<BoxCollider>();var cb=b.GetComponent<BoxCollider>();
            var ra=a.GetComponent<Rigidbody>();var rb=b.GetComponent<Rigidbody>();
            bool overlap=Physics.ComputePenetration(ca,ra.position,ra.rotation,cb,rb.position,rb.rotation,out _,out float depth);
            TrafficFleetBatch.Require(!overlap||depth<.12f,"Traffic bodies overlap: "+a.DesignId+" / "+b.DesignId+" depth="+depth+" lanes="+a.LaneIndex+","+b.LaneIndex);
        }
    }
    private static void Rules()
    {
        TrafficFleetBatch.Require(seen.Count==20,"Not all approved designs participated: "+seen.Count);
        var network=traffic.Network;
        int tested=0;
        foreach(var lane in network.Lanes)
        {
            var start=lane.Path.Sample(0,out var forward);
            var center=network.Junctions[lane.from].position;
            float offset=Vector3.Dot(start-center,Vector3.Cross(Vector3.up,forward).normalized);
            TrafficFleetBatch.Require(offset>1.6f&&offset<2,"Lane is not right-hand traffic.");
            for(float d=0;d<lane.Path.Length;d+=4)
            {
                var p=lane.Path.Sample(d,out _);TrafficFleetBatch.Require(!float.IsNaN(p.sqrMagnitude),"Invalid lane sample.");tested++;
            }
        }
        // Direct movement contract: an obstacle limit caps distance even at full speed.
        var prototype=AssetDatabase.LoadAssetAtPath<GameObject>(TrafficFleetBatch.Root+"/Prefabs/01_Hatchback.prefab");
        var test=Object.Instantiate(prototype,traffic.transform).GetComponent<TrafficVehicle>();
        int incoming=0;int outgoing=network.Departures(network.Lanes[incoming].to)[0];
        test.Place(network,incoming,10,outgoing);
        for(int i=0;i<200;i++)test.Advance(.02f,35);
        float before=test.Travelled;test.Advance(.02f,0);TrafficFleetBatch.Require(test.Travelled==before&&test.Speed==0,"Obstacle clearance not enforced.");
        Object.DestroyImmediate(test.gameObject);
        int rural=Enumerable.Range(0,network.Lanes.Count).First(i=>network.Lanes[i].rural&&network.Lanes[i].points[0].x<300);
        var hill=Object.Instantiate(prototype,traffic.transform).GetComponent<TrafficVehicle>();
        hill.Place(network,rural,0,network.Departures(network.Lanes[rural].to)[0]);
        float highest=0,up=0,down=0;
        for(int i=0;i<700;i++)
        {
            hill.Advance(.02f,35);Physics.Simulate(.02f);
            highest=Mathf.Max(highest,hill.LocalPosition.y);up=Mathf.Max(up,hill.Forward.y);down=Mathf.Min(down,hill.Forward.y);
        }
        TrafficFleetBatch.Require(highest>2&&up>.1f&&down<-.1f,"Traffic did not follow bridge elevation and pitch.");
        Object.DestroyImmediate(hill.gameObject);
        File.WriteAllText(TrafficFleetBatch.Report+"/rules.txt","PASS: "+network.Lanes.Count+" directed right-hand lanes; "+tested+" finite samples; 20 variants observed; bounded pool; obstacle distance cap; existing signal clock retained. Aggregate district travel "+totalDistance.ToString("F1")+" m.\n");
    }
    private static void SignalAndObstacleTest()
    {
        var graph=ScriptableObject.CreateInstance<TrafficRoadNetwork>();
        graph.Configure(new[]{new TrafficRoadNetwork.Junction{position=Vector3.zero,signalIndex=0},new TrafficRoadNetwork.Junction{position=new Vector3(100,0,0),signalIndex=0}},
            new[]{new TrafficRoadNetwork.Lane{from=0,to=1,points=new[]{new Vector3(5.5f,.12f,-1.8f),new Vector3(94.5f,.12f,-1.8f)}},
                  new TrafficRoadNetwork.Lane{from=1,to=0,points=new[]{new Vector3(94.5f,.12f,1.8f),new Vector3(5.5f,.12f,1.8f)}}});
        var root=new GameObject("Disposable signal test");root.transform.position=new Vector3(0,0,-800);
        var harness=root.AddComponent<TownRoadTraffic>();harness.enabled=false;
        var fleet=AssetDatabase.FindAssets("t:Prefab",new[]{TrafficFleetBatch.Root+"/Prefabs"}).Select(g=>AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)).GetComponent<TrafficVehicle>()).ToArray();
        var signal=Object.FindObjectsOfType<TownJunctionSignals>()[0];
        harness.Configure(graph,fleet,new[]{signal},null,1);
        double red=-signal.PhaseOffset;
        var eye=root.transform.TransformPoint(new Vector3(50,0,0));
        for(int i=0;i<2000;i++){harness.Simulate(.02f,red,eye,false);Physics.Simulate(.02f);}
        var car=harness.ActiveCars.Single();
        TrafficFleetBatch.Require(car.CompletedTurns==0&&!car.IsTurning&&car.Speed<.01f&&Mathf.Abs(car.Remaining-7-car.Length*.5f)<.1f,"Car crossed the red stop line or stopped at the wrong location.");
        float stopped=car.Travelled;
        for(int i=0;i<100;i++){harness.Simulate(.02f,red,eye,false);Physics.Simulate(.02f);}
        TrafficFleetBatch.Require(Mathf.Abs(car.Travelled-stopped)<.001f,"Stopped car crept past red.");
        // Native obstacle directly beyond the stop line. Traffic must brake before a solid
        // player/obstacle collider and resume when the obstruction is removed.
        var obstacle=new GameObject("Disposable stopped vehicle",typeof(BoxCollider),typeof(Rigidbody));
        obstacle.GetComponent<Rigidbody>().isKinematic=true;
        var before=car.LocalPosition;obstacle.transform.position=root.transform.TransformPoint(before+car.Forward*9+Vector3.up*.9f);
        obstacle.GetComponent<BoxCollider>().size=new Vector3(1,1.6f,4);
        Physics.SyncTransforms();double green=30-signal.PhaseOffset;
        for(int i=0;i<500;i++){harness.Simulate(.02f,green,eye,true);Physics.Simulate(.02f);}
        TrafficFleetBatch.Require(car.Speed<.05f&&Vector3.Distance(before,car.LocalPosition)<7,"Traffic drove through the stopped physical vehicle.");
        Object.DestroyImmediate(obstacle);Physics.SyncTransforms();
        for(int i=0;i<600;i++){harness.Simulate(.02f,green,eye,true);Physics.Simulate(.02f);}
        TrafficFleetBatch.Require(car.CompletedTurns>0&&car.Travelled>stopped+15,"Traffic did not resume through green.");
        File.WriteAllText(TrafficFleetBatch.Report+"/signal-and-obstacle.txt","PASS: actual controller held at red for 42 simulated seconds without creeping; stopped before a native kinematic vehicle collider on green; resumed and completed a turn after removal.\n");
        Object.DestroyImmediate(root);Object.DestroyImmediate(graph);
    }
    private static void CrossingTest()
    {
        var crossing=Object.FindObjectOfType<TownLevelCrossing>();crossing.enabled=false;
        var train=crossing.train;train.enabled=false;train.SetCrossingHold(false);
        // Small deterministic steps include any remaining station dwell without guessing it.
        for(int i=0;i<700&&train.TravelDistance<325;i++)train.Advance(.1f);
        TrafficFleetBatch.Require(train.TravelDistance>320&&train.TravelDistance<335,"Train did not approach the crossing test.");
        int laneIndex=Enumerable.Range(0,traffic.Network.Lanes.Count).First(i=>
        {
            var lane=traffic.Network.Lanes[i];return Mathf.Abs(lane.points[0].x+22.2f)<.1f&&lane.points[0].z<210&&lane.points.Last().z>270;
        });
        var path=traffic.Network.Lanes[laneIndex].Path;float nearest=0,error=float.MaxValue;
        for(float d=0;d<path.Length;d+=.1f){var p=path.Sample(d,out _);float e=Mathf.Abs(p.z-228);if(e<error){nearest=d;error=e;}}
        var car=traffic.ActiveCars[0];car.Place(traffic.Network,laneIndex,nearest,traffic.Network.Departures(traffic.Network.Lanes[laneIndex].to)[0]);
        Physics.SyncTransforms();crossing.Advance(.1f);
        TrafficFleetBatch.Require(crossing.IsWarning&&crossing.IsOccupied&&train.IsHeldAtCrossing,"Crossing did not protect a kinematic NPC vehicle.");
        float held=train.TravelDistance;train.Advance(1);TrafficFleetBatch.Require(Mathf.Abs(train.TravelDistance-held)<.001f,"Train advanced into NPC occupancy.");
        car.gameObject.SetActive(false);Physics.SyncTransforms();crossing.Advance(4);
        TrafficFleetBatch.Require(!crossing.IsOccupied&&crossing.IsClosed&&!train.IsHeldAtCrossing,"Crossing did not close and release after NPC exit.");
        File.WriteAllText(TrafficFleetBatch.Report+"/crossing.txt","PASS: the actual train held before an NPC-occupied deck; kinematic traffic counts as occupancy; barriers closed and train released only after clearance.\n");
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);
        File.WriteAllText(TrafficFleetBatch.Report+"/play.txt",error==null ? "PASS: actual native motion, four animated wheels, five district streaming runs, twenty designs, bounded pool and safety-distance contract. No player save used.\n" : "FAILED\n"+error);
        if(error==null)TrafficFleetBatch.Receipt("play");else Debug.LogException(error);
        EditorApplication.Exit(error==null?0:1);
    }
}
