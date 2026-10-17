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
using Object=UnityEngine.Object;

/// <summary>Actual Rigidbody driving and the existing showroom modal, without modifying player saves.</summary>
[InitializeOnLoad]
public static class TownShowroomPlayValidation
{
    private const string Running="Showroom.Play.Running",Phase="Showroom.Play.Phase";
    private const string Folder="Library/TownShowroomQA";
    private static DateTime deadline;
    private static float nextFixedTime,publicDistance;
    private static Vector3 startingPoint;
    static TownShowroomPlayValidation(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/play.txt","RUNNING\n");
            using(var stream=File.OpenRead(TownWorldAssembler.PrefabPath))using(var hash=SHA256.Create())File.WriteAllText(Folder+"/play-input.sha256",BitConverter.ToString(hash.ComputeHash(stream)).Replace("-",""));
            EditorSceneManager.OpenScene("Assets/Project/Scenes/Game.unity");ShopGameplaySandbox.Open();
            SessionState.SetInt(Phase,-2);SessionState.SetBool(Running,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception error){Finish(error.ToString());}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Running,false)||!EditorApplication.isPlaying)return;
        try
        {
            int phase=SessionState.GetInt(Phase,0);
            if(phase==-2)
            {
                if(Time.frameCount<15)return;
                Require(Object.FindObjectOfType<SaveManager>(true)==null,"Player save writer present in showroom test.");
                var trip=Object.FindObjectOfType<CityTrip>();Require(trip!=null&&trip.orders!=null,"Actual shop trip dependencies missing.");
                trip.orders.save=null;deadline=DateTime.UtcNow.AddSeconds(90);trip.Begin();SessionState.SetInt(Phase,-1);return;
            }
            if(phase==-1)
            {
                var trip=Object.FindObjectOfType<CityTrip>();
                if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Actual shop-to-city transition timed out.");
                if(trip==null||!trip.InCity||trip.Transitioning)return;
                Require(Object.FindObjectOfType<SaveManager>(true)==null,"Save writer appeared during city transition.");
                SessionState.SetInt(Phase,0);return;
            }
            var city=Object.FindObjectOfType<CityWorld>();if(city==null)throw new InvalidOperationException("Live city missing.");
            var car=city.pickup;var checkpoint=city.town.suppliers.Single(c=>c.supplier==SupplierKind.Showroom);
            if(phase==0)
            {
                if(Time.frameCount<15)return;
                deadline=DateTime.UtcNow.AddSeconds(50);car.ReadKeyboard=false;car.InputEnabled=true;car.Stop();
                car.Body.position=TownShowroomPlacement.PublicStreet+Vector3.up*.12f;car.Body.rotation=Quaternion.Euler(0,90,0);
                startingPoint=car.Body.position;Await(1,5);return;
            }
            if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Showroom drive timed out in phase "+phase+" at "+car.Body.position);
            // FixedUpdate time, not rendered frames, determines when interpolation and stop state have settled.
            if(Time.fixedTime<nextFixedTime)return;
            if(phase==1)
            {
                if(checkpoint.DistanceTo(car)>.55f){car.SetInput(.45f,0,false);return;}
                car.Stop();publicDistance=FlatDistance(startingPoint,car.Body.position);Await(2,3);return;
            }
            if(phase==2)
            {
                Require(publicDistance>12,"Pickup did not drive from the public street to the showroom.");
                Require(checkpoint.kind==CheckpointKind.Showroom&&!checkpoint.requiresRearDocking&&checkpoint.CanUse(car),"Stopped public inspection rejected.");
                car.Body.velocity=Vector3.right;Require(!checkpoint.CanUse(car),"Moving inspection incorrectly accepted.");car.Stop();
                car.Body.rotation*=Quaternion.Euler(0,180,0);Await(3,3);return;
            }
            if(phase==3)
            {
                Require(checkpoint.CanUse(car),"Existing any-heading showroom inspection contract changed.");
                city.Interact();
                Require(!car.InputEnabled&&Object.FindObjectsOfType<TMP_Text>().Any(t=>t.text.Contains("Автосалон — осмотр")||t.text.Contains("Vehicle showroom")),"Actual showroom inspection modal did not open.");
                city.SendMessage("ClosePanel",SendMessageOptions.RequireReceiver);
                Require(car.InputEnabled,"Closing showroom modal did not restore driving.");
                car.Stop();car.Body.position=TownShowroomPlacement.ServiceStaging+Vector3.up*.12f;car.Body.rotation=Quaternion.Euler(0,270,0);
                startingPoint=car.Body.position;Await(4,5);return;
            }
            if(phase==4)
            {
                if(FlatDistance(car.Body.position,TownShowroomPlacement.ServiceBay)>.55f){car.SetInput(-.45f,0,false);return;}
                car.Stop();Await(5,3);return;
            }
            car.Stop();float rearDistance=FlatDistance(startingPoint,car.Body.position);
            Require(rearDistance>9,"Actual service-yard reverse approach failed.");
            Require(FlatDistance(car.Body.position,TownShowroomPlacement.ServiceBay)<.6f,"Pickup missed service preparation bay.");
            Finish(null,"PASS: actual native pickup drove "+publicDistance.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" m to public Showroom inspection; stopped interaction accepted in either heading, moving interaction rejected. Existing inspection modal opened and closed and driving restored.\n"+
                "Actual pickup reversed "+rearDistance.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" m into the decorative rear preparation bay. No new depot, purchasing mechanic, synthetic physics simulation or player-save writes.\n");
        }
        catch(Exception error){Finish(error.ToString());}
    }
    private static void Await(int phase,int fixedSteps){nextFixedTime=Time.fixedTime+fixedSteps*Time.fixedDeltaTime;SessionState.SetInt(Phase,phase);}
    private static float FlatDistance(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
    private static void Require(bool condition,string error){if(!condition)throw new InvalidOperationException(error);}
    private static void Finish(string error,string success=null){SessionState.SetBool(Running,false);File.WriteAllText(Folder+"/play.txt",error==null?success:"FAILED\n"+error);EditorApplication.Exit(error==null?0:1);}
}
