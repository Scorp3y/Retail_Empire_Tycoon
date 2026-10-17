using System;
using System.IO;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Save-free real Rigidbody reverse approach to the scenic dairy bay, without inventing a supplier contract.</summary>
[InitializeOnLoad]
public static class TownDairyPlayValidation
{
    private const string Active="Dairy.Play.Active",Phase="Dairy.Play.Phase",Report="Library/TownDairyQA";
    private static float nextStep;private static DateTime deadline;private static Vector3 startingPoint;
    static TownDairyPlayValidation(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        try{TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);File.WriteAllText(Report+"/play.txt","RUNNING\n");File.WriteAllText(Report+"/play-input.sha256",TownDairyBatch.PrefabHash());EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);SessionState.SetInt(Phase,0);SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();}
        catch(Exception e){Finish(e.ToString());}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying)return;
        try
        {
            var city=UnityEngine.Object.FindObjectOfType<CityWorld>();if(city==null)throw new InvalidOperationException("Native City world missing.");var car=city.pickup;int phase=SessionState.GetInt(Phase,0);
            if(phase==0){if(Time.frameCount<15)return;deadline=DateTime.UtcNow.AddSeconds(45);car.ReadKeyboard=false;car.InputEnabled=true;car.Stop();car.Body.position=TownDairyBatch.World(23,.25f,23);car.Body.rotation=TownDairyBatch.Rotation;startingPoint=car.Body.position;nextStep=Time.fixedTime+6*Time.fixedDeltaTime;SessionState.SetInt(Phase,1);return;}
            if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Dairy reverse drive timed out at "+car.Body.position);
            if(Time.fixedTime<nextStep)return;
            if(phase==1){car.SetInput(-.45f,0,false);SessionState.SetInt(Phase,2);return;}
            var bay=TownDairyBatch.World(23,0,4.5f);var flat=car.Body.position-bay;flat.y=0;
            if(phase==2){if(flat.magnitude>.6f)return;car.Stop();nextStep=Time.fixedTime+5*Time.fixedDeltaTime;SessionState.SetInt(Phase,3);return;}
            car.Stop();float travelled=Vector2.Distance(new Vector2(startingPoint.x,startingPoint.z),new Vector2(car.Body.position.x,car.Body.position.z));
            if(travelled<17||flat.magnitude>1||Vector3.Angle(car.transform.forward,TownDairyBatch.Rotation*Vector3.forward)>3)throw new InvalidOperationException("Native dairy pickup did not complete its aligned reverse approach.");
            Finish(null,"PASS: actual native pickup reversed "+travelled.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" m to scenic dairy dispatch bay and stopped aligned, using real FixedUpdate/Rigidbody physics. No supplier/order/processing gameplay added, no player save changed.\n");
        }
        catch(Exception e){Finish(e.ToString());}
    }
    private static void Finish(string error,string success=null){SessionState.SetBool(Active,false);Directory.CreateDirectory(Report);File.WriteAllText(Report+"/play.txt",error==null?success:"FAILED\n"+error);EditorApplication.Exit(error==null?0:1);}
}
