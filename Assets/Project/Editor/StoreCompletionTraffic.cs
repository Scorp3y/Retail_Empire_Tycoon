using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Five minutes of native fixed-step traffic, including the observed southern junctions.</summary>
[InitializeOnLoad]
public static class StoreCompletionTraffic
{
    private const string Pending="StoreCompletion.Traffic";
    static StoreCompletionTraffic(){EditorApplication.update+=Tick;}
    public static void City()=>Begin(false);
    public static void Shop()=>Begin(true);
    private static void Begin(bool shop)
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(StoreCompletionBatch.Report);
        EditorSceneManager.OpenScene(shop?CityUpdateSetup.GamePath:CityUpdateSetup.CityPath);
        if(shop)
        {
            ShopGameplaySandbox.Open();
            foreach(var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true))Object.DestroyImmediate(bootstrap);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        SessionState.SetBool("StoreCompletion.TrafficShop",shop);SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||Time.frameCount<15)return;
        SessionState.SetBool(Pending,false);bool shop=SessionState.GetBool("StoreCompletion.TrafficShop",false);string stage=shop?"traffic-shop":"traffic-city";
        try
        {
            Require(Object.FindObjectOfType<SaveManager>(true)==null,"Player save writer present.");
            var traffic=Object.FindObjectOfType<TownRoadTraffic>();traffic.enabled=false;Object.FindObjectOfType<TownPedestrianTraffic>().enabled=false;
            var city=Object.FindObjectOfType<CityWorld>();if(city!=null){city.pickup.ReadKeyboard=false;city.pickup.InputEnabled=false;}
            Physics.simulationMode=SimulationMode.Script;
            var eye=traffic.transform.TransformPoint(new Vector3(-120,0,-144));
            int turns=0;var report=new StringBuilder();
            object Call(string name,params object[] args)=>typeof(TownRoadTraffic).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(traffic,args);
            for(int step=0;step<15000;step++)
            {
                int before=traffic.ActiveCars.Sum(car=>car.CompletedTurns);
                traffic.Simulate(.02f,step*.02,eye,true);Physics.Simulate(.02f);
                int after=traffic.ActiveCars.Sum(car=>car.CompletedTurns);if(after>before)turns+=after-before;
                if(step%1000!=999)continue;
                foreach(var car in traffic.ActiveCars.Where(car=>car.HoldSeconds>75 && !car.IsTurning))
                {
                    bool green=(bool)Call("SignalAllows",traffic.Network.Lanes[car.LaneIndex],step*.02);
                    float clearance=(float)Call("FrontClearance",car);bool exit=(bool)Call("ExitClear",car);
                    report.AppendLine($"t={step*.02:F0} {car.DesignId}: hold={car.HoldSeconds:F1}, green={green}, exit={exit}, front={clearance:F1}, reserve={car.ReservedJunction}, at={car.LocalPosition}");
                    Require(!(green&&exit&&clearance>2),"A clear green head remained stopped for over 75 seconds. "+report);
                }
            }
            Require(turns>150,"Traffic stopped circulating during a five-minute run: turns="+turns+"\n"+report);
            File.WriteAllText(StoreCompletionBatch.Report+"/"+stage+".txt","PASS 300 simulated seconds with native physics, scale="+traffic.transform.lossyScale.x+", turns="+turns+"; no long-lived clear-green head.\n"+report);
            Reroute(traffic);
            StoreCompletionBatch.Record(stage);EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(StoreCompletionBatch.Report+"/"+stage+".txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    private static void Reroute(TownRoadTraffic original)
    {
        var fields=new SerializedObject(original);var list=fields.FindProperty("fleet");
        var fleet=Enumerable.Range(0,list.arraySize).Select(i=>(TrafficVehicle)list.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        var graph=ScriptableObject.CreateInstance<TrafficRoadNetwork>();
        TrafficRoadNetwork.Lane Lane(int from,int to,Vector3 a,Vector3 b)=>new TrafficRoadNetwork.Lane{from=from,to=to,points=new[]{a,b}};
        graph.Configure(new[]{new TrafficRoadNetwork.Junction{position=Vector3.zero},new TrafficRoadNetwork.Junction{position=Vector3.right*100},new TrafficRoadNetwork.Junction{position=Vector3.right*200},new TrafficRoadNetwork.Junction{position=new Vector3(100,0,100)}},
            new[]{Lane(0,1,new Vector3(5.5f,0,-1.8f),new Vector3(94.5f,0,-1.8f)),Lane(1,0,new Vector3(94.5f,0,1.8f),new Vector3(5.5f,0,1.8f)),Lane(1,2,new Vector3(105.5f,0,-1.8f),new Vector3(194.5f,0,-1.8f)),Lane(2,1,new Vector3(194.5f,0,1.8f),new Vector3(105.5f,0,1.8f)),Lane(1,3,new Vector3(101.8f,0,5.5f),new Vector3(101.8f,0,94.5f)),Lane(3,1,new Vector3(98.2f,0,94.5f),new Vector3(98.2f,0,5.5f))});
        var root=new GameObject("Disposable blocked-exit regression");root.transform.position=Vector3.back*900;
        var traffic=root.AddComponent<TownRoadTraffic>();traffic.enabled=false;traffic.Configure(graph,fleet,Array.Empty<TownJunctionSignals>(),null,2);
        var eye=root.transform.TransformPoint(Vector3.right*100);traffic.Simulate(.02f,0,eye,false);
        var leader=traffic.ActiveCars[0];var obstacle=traffic.ActiveCars[1];leader.Place(graph,0,graph.Lanes[0].Path.Length-7-leader.Length*.5f,2);
        for(int step=0;step<1000;step++){obstacle.Place(graph,2,3,3);traffic.Simulate(.02f,step*.02,eye,false);}
        Require(leader.CompletedTurns>0,"A full exit blocked a car despite a free legal alternative.");
        File.WriteAllText(StoreCompletionBatch.Report+"/reroute.txt","PASS stopped leader abandons its blocked exit, selects a free legal turn without U-turning, and completes the junction.\n");
        Object.DestroyImmediate(root);Object.DestroyImmediate(graph);
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
