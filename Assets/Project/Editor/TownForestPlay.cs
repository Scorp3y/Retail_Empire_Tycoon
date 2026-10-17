using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Drives the real arcade pickup through the gateway and a continuous complete district loop.</summary>
[InitializeOnLoad]
public static class TownForestPlay
{
    private const string Active="Forest.Play.Active";
    private static Vector3[] route;
    private static int target;
    private static float settled;
    private static double startTime;
    private static float travelled;
    private static Vector3 previous;
    private static DateTime deadline;
    static TownForestPlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(TownForestBatch.Report);
            File.WriteAllText(TownForestBatch.Report+"/play.txt","RUNNING\n");
            EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception error){Finish(error);}
    }
    private static Vector3[] Route()
    {
        var points=new List<Vector3>();
        void Line(float x0,float z0,float x1,float z1)
        {var a=new Vector3(x0,0,z0);var b=new Vector3(x1,0,z1);int n=Mathf.CeilToInt(Vector3.Distance(a,b)/4);for(int i=1;i<=n;i++)points.Add(Vector3.Lerp(a,b,i/(float)n));}
        void Arc(float x,float z,float a,float b)
        {for(int i=1;i<=12;i++){float angle=Mathf.Lerp(a,b,i/12f)*Mathf.Deg2Rad;points.Add(new Vector3(x+Mathf.Cos(angle)*12,0,z+Mathf.Sin(angle)*12));}}
        Line(-22,282,-22,420);Arc(-34,420,0,90);Line(-34,432,-110,432);
        Arc(-110,420,90,180);Line(-122,420,-122,320);Arc(-110,320,180,270);
        Line(-110,308,62,308);Arc(62,320,270,360);Line(74,320,74,420);
        Arc(62,420,0,90);Line(62,432,-10,432);return points.ToArray();
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||Time.frameCount<20)return;
        try
        {
            var city=Object.FindObjectOfType<CityWorld>();TownForestBatch.Require(city!=null&&city.pickup!=null,"Live CityWorld missing.");var car=city.pickup;
            if(route==null)
            {
                route=Route();target=0;travelled=0;car.ReadKeyboard=false;car.InputEnabled=true;car.Stop();
                previous=new Vector3(-22,.5f,282);car.Body.position=previous;car.Body.rotation=Quaternion.identity;
                settled=Time.fixedTime+.6f;startTime=Time.timeAsDouble;deadline=DateTime.UtcNow.AddSeconds(420);return;
            }
            if(Time.fixedTime<settled)return;
            if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Continuous pickup route timed out at "+car.Body.position+" target="+target);
            var p=car.Body.position;TownForestBatch.Require(p.y>-.3f&&p.y<.8f,"Pickup lost physical floor at "+p);
            TownForestBatch.Require(p.z<310||TownForestPlan.OnRoad(p,.2f),"Pickup left the carriageway at "+p);
            travelled+=Vector3.ProjectOnPlane(p-previous,Vector3.up).magnitude;previous=p;
            foreach(var signal in Object.FindObjectsOfType<TownJunctionSignals>())TownForestBatch.Require(signal.LampsMatchState,"Existing live signals lost bindings.");
            while(target<route.Length-1&&Vector3.ProjectOnPlane(route[target]-p,Vector3.up).magnitude<4)target++;
            if(target==route.Length-1&&Vector3.ProjectOnPlane(route[target]-p,Vector3.up).magnitude<1.6f)
            {
                car.Stop();float planned=Vector3.Distance(new Vector3(-22,0,282),route[0])+route.Skip(1).Select((b,i)=>Vector3.Distance(route[i],b)).Sum();
                TownForestBatch.Require(travelled>=planned*.97f&&travelled<=planned*1.08f,"Continuous route distance differs from its actual authored polyline: "+travelled+" / "+planned);
                File.WriteAllText(TownForestBatch.Report+"/drive.txt","PASS: actual PickupDrive/Rigidbody continuous drive "+travelled.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" m in "+(Time.timeAsDouble-startTime).ToString("F1")+" simulation seconds. Gateway, both main junctions, all four outer corners, full loop; no teleport after initial test pose. Existing player scene/save not written.\n");
                TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),TownForestBatch.Pictures+"/LivePickup.png",p+new Vector3(-13,9,-16),p+Vector3.forward*2);
                Finish(null);return;
            }
            float angle=Vector3.SignedAngle(car.transform.forward,Vector3.ProjectOnPlane(route[target]-p,Vector3.up),Vector3.up);
            car.SetInput(.35f,Mathf.Clamp(angle/22,-1,1),false);
        }
        catch(Exception error){Finish(error);}
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);
        if(error==null)TownForestBatch.Receipt("play","PASS: native continuous pickup route completed; no loss of road support or departure from carriageway, existing traffic lamp bindings remain valid.\n");
        else{File.WriteAllText(TownForestBatch.Report+"/play.txt","FAILED\n"+error);Debug.LogException(error);}
        EditorApplication.Exit(error==null?0:1);
    }
}
