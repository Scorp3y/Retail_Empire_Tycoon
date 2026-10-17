using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Actual native pickup drives: docking, entire fuel loop, workshop corridor, curved farm access and fields.</summary>
[InitializeOnLoad]
public static class TownRuralPolishPlay
{
    private const string Active="RuralPolish.Play.Active",Case="RuralPolish.Play.Case",Phase="RuralPolish.Play.Phase";
    private static Vector3[] route;private static int target;private static DateTime deadline;private static float settled;private static Vector3 start;private static float travelled;private static Vector3 previous;
    private static readonly string[] Names={"farm rear docking","complete fuel circulation","workshop service corridor","curved farm access","field machinery headland"};
    static TownRuralPolishPlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(TownRuralPolishBatch.Report);File.WriteAllText(TownRuralPolishBatch.Report+"/play.txt","RUNNING\n");File.WriteAllText(TownRuralPolishBatch.Report+"/drives.txt","");EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);SessionState.SetInt(Case,0);SessionState.SetInt(Phase,0);SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Finish(e);}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||Time.frameCount<15)return;
        try
        {
            var city=Object.FindObjectOfType<CityWorld>();TownRuralPolishBatch.Require(city!=null,"CityWorld missing.");var car=city.pickup;int index=SessionState.GetInt(Case,0),phase=SessionState.GetInt(Phase,0);
            if(index>=Names.Length){Finish(null);return;}
            if(phase==0)
            {
                route=Route(index,city.town);target=1;travelled=0;start=route[0];previous=start;car.ReadKeyboard=false;car.InputEnabled=true;car.Stop();car.Body.position=start+Vector3.up*.4f;
                car.Body.rotation=Quaternion.LookRotation(index==0?Vector3.back:(route[1]-route[0]).normalized);settled=Time.fixedTime+.5f;deadline=DateTime.UtcNow.AddSeconds(220);SessionState.SetInt(Phase,1);return;
            }
            if(Time.fixedTime<settled)return;
            if(DateTime.UtcNow>deadline)throw new InvalidOperationException(Names[index]+" timed out at "+car.Body.position+" velocity="+car.Body.velocity+" target="+route[target]);
            var flat=car.Body.position-route[target];flat.y=0;travelled+=Vector3.ProjectOnPlane(car.Body.position-previous,Vector3.up).magnitude;previous=car.Body.position;
            TownRuralPolishBatch.Require(car.Body.position.y>-.5f&&car.Body.position.y<3,"Pickup lost its physical floor during "+Names[index]);
            if(index==0)
            {
                car.SetInput(-.45f,0,false);if(car.Body.position.z<304.4f)return;
            }
            else
            {
                if(flat.magnitude<.9f&&target<route.Length-1){target++;flat=car.Body.position-route[target];flat.y=0;}
                if(flat.magnitude>.9f)
                {
                    float error=Vector3.SignedAngle(car.transform.forward,-flat,Vector3.up);float steer=Mathf.Clamp(error/24,-1,1);float throttle=Mathf.Abs(error)>40?.17f:index==4?.32f:.23f;car.SetInput(throttle,steer,false);return;
                }
                if(target<route.Length-1)return;
            }
            car.Stop();if(index==0){var checkpoint=city.town.suppliers.Single(c=>c.supplier==SupplierKind.Farmers);TownRuralPolishBatch.Require(checkpoint.CanUse(car),"Farm pickup interaction rejected: "+checkpoint.UnavailableReason(car));}
            File.AppendAllText(TownRuralPolishBatch.Report+"/drives.txt",Names[index]+": actual Rigidbody drive "+travelled.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" m; end="+car.Body.position+"\n");SessionState.SetInt(Case,index+1);SessionState.SetInt(Phase,0);
        }
        catch(Exception e){Finish(e);}
    }
    private static Vector3[] Route(int index,TownWorldPlaces town)
    {
        Vector3 P(float x,float z)=>new Vector3(x,0,z);
        if(index==0)return new[]{P(560,289),P(560,305)};
        if(index==1)return new[]{P(228,-138),P(228,-128),P(228.3f,-126.5f),P(229.2f,-125.2f),P(230.5f,-124.3f),P(232,-124),P(234,-124),P(235.5f,-123.7f),P(236.8f,-122.8f),P(237.7f,-121.5f),P(238,-120),P(238,-108),P(237.7f,-106.5f),P(236.8f,-105.2f),P(235.5f,-104.3f),P(234,-104),P(228,-104),P(226.5f,-104.3f),P(225.2f,-105.2f),P(224.3f,-106.5f),P(224,-108),P(224,-120),P(223.7f,-121.5f),P(222.8f,-122.8f),P(221.5f,-123.7f),P(220,-124),P(208,-124),P(206.5f,-124.3f),P(205.2f,-125.2f),P(204.3f,-126.5f),P(204,-128),P(204,-138)};
        if(index==2)return new[]{P(584,299),P(606,299),P(610,300),P(614,303),P(617,308),P(617,335)};
        if(index==3)return town.layout.countryRoad.points.Where(p=>p.z>=244&&p.z<=278).Concat(new[]{P(550,285),P(550,289)}).ToArray();
        return new[]{P(538,375),P(538,432)};
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);
        if(error==null)TownRuralPolishBatch.Receipt("play","PASS: five real native pickup Rigidbody drives, including rear collection interaction, complete fuel loop, workshop corridor, original curved farm road and field headland. No player save modified.\n");
        else{File.WriteAllText(TownRuralPolishBatch.Report+"/play.txt","FAILED\n"+error);Debug.LogException(error);}
        EditorApplication.Exit(error==null?0:1);
    }
}
