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

[InitializeOnLoad]
public static class MobilityDiagnostics
{
    private const string Pending="MobilityProbe.Pending";
    static MobilityDiagnostics(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory("Library/MobilityUpdateQA");
        EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||Time.frameCount<15)return;
        SessionState.SetBool(Pending,false);
        try
        {
            Require(Object.FindObjectOfType<SaveManager>()==null,"Save writer in city probe.");
            var traffic=Object.FindObjectOfType<TownRoadTraffic>();traffic.enabled=false;
            Object.FindObjectOfType<TownPedestrianTraffic>().enabled=false;
            Physics.simulationMode=SimulationMode.Script;
            var eye=traffic.transform.TransformPoint(new Vector3(-120,0,-144));
            double clock=0;
            for(int i=0;i<15000;i++){clock=i*.02;traffic.Simulate(.02f,clock,eye,true);Physics.Simulate(.02f);}
            object Call(string name,params object[] arguments)=>typeof(TownRoadTraffic).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(traffic,arguments);
            var text=new StringBuilder();
            foreach(var car in traffic.ActiveCars.Where(car=>car.Speed<.1f))
            {
                var lane=traffic.Network.Lanes[car.LaneIndex];
                text.AppendLine($"{car.DesignId} lane={car.LaneIndex} to={traffic.Network.Junctions[lane.to].position} at={car.LocalPosition} hold={car.HoldSeconds:F1} remaining={car.Remaining:F2} turn={car.IsTurning} reserve={car.ReservedJunction} green={Call("SignalAllows",lane,clock)} front={Call("FrontClearance",car)} exit={Call("ExitClear",car)} obstacle={Call("ObstacleClearance",car,35f)}");
                foreach(var other in traffic.ActiveCars.Where(other=>other!=car && Vector3.Distance(other.LocalPosition,car.LocalPosition)<18))
                    text.AppendLine($"  near {other.DesignId} lane={other.LaneIndex} at={other.LocalPosition} forward={other.Forward} turning={other.IsTurning} reserved={other.ReservedJunction}");
            }
            File.WriteAllText("Library/MobilityUpdateQA/traffic-probe.txt",text.ToString());EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText("Library/MobilityUpdateQA/probe-failure.txt",error.ToString());EditorApplication.Exit(1);}
    }
    private static void Require(bool okay,string message){if(!okay)throw new InvalidOperationException(message);}
}
