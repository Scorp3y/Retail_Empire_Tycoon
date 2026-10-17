using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Short actual-physics acceptance for the new bread bay; never runs in the user's main editor.</summary>
[InitializeOnLoad]
public static class TownBakeryPlayValidation
{
    private const string Running="Bakery.Play.Running";
    private const string Phase="Bakery.Play.Phase";
    private static string Folder=>SessionState.GetString("Pickup.Play.Folder","Library/TownBakeryQA");
    private static string Report=>Folder+"/play.txt";
    private static SupplierKind Supplier=>(SupplierKind)SessionState.GetInt("Pickup.Play.Supplier",(int)SupplierKind.Bakery);
    private static float nextFixedTime;
    private static DateTime deadline;
    private static Vector3 startingPoint;
    static TownBakeryPlayValidation(){EditorApplication.update+=Tick;}
    public static void Run()
    {Start(SupplierKind.Bakery,"Library/TownBakeryQA");}
    public static void RunFlorist()
    {Start(SupplierKind.Decoration,"Library/TownFloristQA");}
    public static void RunQuarter()
    {Start(SupplierKind.Bakery,"Library/TownBakeryQuarterQA");}
    private static void Start(SupplierKind supplier,string folder)
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();SessionState.SetString("Pickup.Play.Folder",folder);SessionState.SetInt("Pickup.Play.Supplier",(int)supplier);
            Directory.CreateDirectory(Folder);File.WriteAllText(Report,"RUNNING\n");
            File.WriteAllText(Folder+"/play-input.sha256",PrefabHash());
            EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);SessionState.SetInt(Phase,0);SessionState.SetBool(Running,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception error){Finish(error.ToString());}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Running,false)||!EditorApplication.isPlaying)return;
        try
        {
            var city=Object.FindObjectOfType<CityWorld>();if(city==null)throw new InvalidOperationException("Live City world missing.");
            var car=city.pickup;var bay=city.town.suppliers.Single(c=>c.supplier==Supplier);
            var court=city.town.GetComponentsInChildren<SupplierLoadingCourt>().Single(c=>c.checkpoint==bay);
            int phase=SessionState.GetInt(Phase,0);
            if(phase==0)
            {
                if(Time.frameCount<15)return;deadline=DateTime.UtcNow.AddSeconds(40);
                car.ReadKeyboard=false;car.InputEnabled=true;car.Stop();car.Body.position=bay.transform.position+Vector3.up*.12f;car.Body.rotation=bay.transform.rotation*Quaternion.Euler(0,180,0);
                nextFixedTime=Time.fixedTime+3*Time.fixedDeltaTime;SessionState.SetInt(Phase,1);return;
            }
            if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Native bakery reverse drive timed out at "+car.Body.position);
            // Batch Mode can render several frames without a physics step. Rigidbody interpolation
            // must settle in real FixedUpdates before the checkpoint reads Transform pose.
            if(Time.fixedTime<nextFixedTime)return;
            if(phase==1)
            {
                car.Stop();string reason=bay.UnavailableReason(car);
                Require(!bay.CanUse(car)&&reason.Contains("задом"),"Wrong-way test failed: "+reason+"; transform="+car.transform.position+"; body="+car.Body.position+"; heading="+Vector3.Angle(car.transform.forward,bay.transform.forward));
                car.Body.position=court.staging+Vector3.up*.12f;car.Body.rotation=bay.transform.rotation;
                startingPoint=car.Body.position;nextFixedTime=Time.fixedTime+5*Time.fixedDeltaTime;SessionState.SetInt(Phase,2);return;
            }
            if(phase==2)
            {car.SetInput(-.45f,0,false);SessionState.SetInt(Phase,3);return;}
            if(phase==3)
            {
                if(bay.DistanceTo(car)>.55f)return;car.Stop();nextFixedTime=Time.fixedTime+3*Time.fixedDeltaTime;SessionState.SetInt(Phase,4);return;
            }
            car.Stop();Require(bay.CanUse(car),"Correct rear docking was rejected: "+bay.UnavailableReason(car));
            float distance=Vector2.Distance(new Vector2(startingPoint.x,startingPoint.z),new Vector2(car.Body.position.x,car.Body.position.z));
            Require(distance>8.5f,"The actual pickup did not complete the reverse approach.");
            car.Body.velocity=Vector3.forward;Require(!bay.CanUse(car)&&bay.UnavailableReason(car).Contains("Остановите"),"Moving bread collection was accepted.");car.Stop();
            Finish(null,"PASS: actual native pickup reversed "+distance.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" m to the "+Supplier+" bay. Wrong-way and moving collection rejected; correct stopped rear docking accepted.\nNo physics simulation or mesh-read shortcut used.\n");
        }
        catch(Exception error){Finish(error.ToString());}
    }
    private static void Require(bool condition,string error){if(!condition)throw new InvalidOperationException(error);}
    private static string PrefabHash()
    {using(var input=File.OpenRead(TownWorldAssembler.PrefabPath))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","");}
    private static void Finish(string error,string success=null)
    {SessionState.SetBool(Running,false);Directory.CreateDirectory(Folder);File.WriteAllText(Report,error==null?success:"FAILED\n"+error);EditorApplication.Exit(error==null?0:1);}
}
