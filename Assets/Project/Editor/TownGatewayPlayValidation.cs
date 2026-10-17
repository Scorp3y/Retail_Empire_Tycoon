using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Save-free actual pickup drives and train/occupied-crossing behaviour in the isolated editor.</summary>
[InitializeOnLoad]
public static class TownGatewayPlayValidation
{
    private const string Active="Gateway.Play.Active",Stage="Gateway.Play.Stage";
    private static DateTime deadline;
    private static float settleUntil;
    private static Vector3 start;
    private static readonly string[] Names={"farm reverse pickup","fuel through lane","bridge approaches","rail crossing approaches"};
    static TownGatewayPlayValidation(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(TownGatewayBatch.Report);File.WriteAllText(TownGatewayBatch.Report+"/play.txt","RUNNING\n");File.WriteAllText(TownGatewayBatch.Report+"/drives.txt","");
            EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);SessionState.SetInt(Stage,0);SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Finish(e);}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||Time.frameCount<15)return;
        try
        {
            var city=Object.FindObjectOfType<CityWorld>();TownGatewayBatch.Require(city!=null,"CityWorld missing.");var car=city.pickup;int stage=SessionState.GetInt(Stage,0);int index=stage/3;
            if(index>=Names.Length){CrossingBehaviour();Finish(null);return;}
            if(stage%3==0)
            {
                car.ReadKeyboard=false;car.InputEnabled=true;car.Stop();
                start=index==0?new Vector3(560,.4f,289):index==1?new Vector3(224,.4f,-125):index==2?new Vector3(299,.4f,-40):new Vector3(-24,.6f,200);
                car.Body.position=start;car.Body.rotation=Quaternion.Euler(0,index==0?180:index==2?90:0,0);settleUntil=Time.fixedTime+.5f;deadline=DateTime.UtcNow.AddSeconds(150);SessionState.SetInt(Stage,stage+1);return;
            }
            if(DateTime.UtcNow>deadline)
            {
                var obstacles=Physics.OverlapBox(car.Body.position+Vector3.up*.8f,new Vector3(1.3f,.9f,2.4f),car.Body.rotation,~0,QueryTriggerInteraction.Ignore).Where(c=>!c.transform.IsChildOf(car.transform)).Select(c=>c.transform.parent.name+"/"+c.name);
                throw new InvalidOperationException(Names[index]+" timed out at "+car.Body.position+" velocity="+car.Body.velocity+" fixedTime="+Time.fixedTime+" contacts="+string.Join(",",obstacles));
            }
            if(Time.fixedTime<settleUntil)return;
            if(stage%3==1){car.SetInput(index==0?-.45f:.55f,0,false);SessionState.SetInt(Stage,stage+1);return;}
            bool reached=index==0?car.Body.position.z>=304.4f:index==1?car.Body.position.z>=-102:index==2?car.Body.position.x>=360:car.Body.position.z>=269;
            TownGatewayBatch.Require(car.Body.position.y>-.6f&&car.Body.position.y<5,"Pickup fell or climbed an obstacle during "+Names[index]);
            TownGatewayBatch.Require(index==2?Mathf.Abs(car.Body.position.z+40)<1:Mathf.Abs(car.Body.position.x-start.x)<1,"Pickup deviated from lane during "+Names[index]);
            if(!reached)return;
            car.Stop();
            if(index==0){var farm=Object.FindObjectsOfType<DeliveryCheckpoint>().Single(c=>c.supplier==RetailEmpireTycoon.Logistics.SupplierKind.Farmers);TownGatewayBatch.Require(farm.CanUse(car),"Native farm loading interaction rejected the parked pickup: "+farm.UnavailableReason(car));}
            File.AppendAllText(TownGatewayBatch.Report+"/drives.txt",Names[index]+": "+Vector3.Distance(start,car.Body.position).ToString("F2")+" m; end="+car.Body.position+"\n");SessionState.SetInt(Stage,stage+1);
        }
        catch(Exception e){Finish(e);}
    }
    private static void CrossingBehaviour()
    {
        var crossing=Object.FindObjectOfType<TownLevelCrossing>();var train=crossing.train;crossing.enabled=false;train.enabled=false;
        var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="QA occupied crossing vehicle";blocker.transform.position=new Vector3(-24,3.3f,228);blocker.AddComponent<Rigidbody>().useGravity=false;Physics.SyncTransforms();
        bool held=false;for(int i=0;i<1500;i++){crossing.Advance(.1f);train.Advance(.1f);if(train.IsHeldAtCrossing){held=true;break;}}
        TownGatewayBatch.Require(held&&train.TravelDistance<crossing.trackDistance-10,"Occupied deck did not hold approaching train.");
        float distance=train.TravelDistance;for(int i=0;i<30;i++){crossing.Advance(.1f);train.Advance(.1f);}TownGatewayBatch.Require(Mathf.Abs(train.TravelDistance-distance)<.001f,"Train entered occupied crossing.");
        blocker.transform.position+=Vector3.forward*40;Physics.SyncTransforms();bool closed=false,passed=false;
        for(int i=0;i<400;i++){crossing.Advance(.1f);closed|=crossing.IsClosed;train.Advance(.1f);if(train.TravelDistance>crossing.trackDistance+train.cars.Length*train.carSpacing+15)passed=true;if(passed&&!crossing.IsWarning&&!crossing.IsClosed)break;}
        TownGatewayBatch.Require(closed&&passed&&!crossing.IsWarning,"Crossing did not close, release train and reopen after its tail.");
        crossing.Advance(4);TownGatewayBatch.Require(crossing.arms.All(a=>Quaternion.Angle(a.localRotation,Quaternion.Euler(0,0,90))<.1f),"Barrier arms failed to reopen.");Object.Destroy(blocker);
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);
        if(error==null)TownGatewayBatch.Receipt("play","PASS: four real native pickup Rigidbody drives (farm reverse, fuel lane, bridge and railway approach ramps), occupied-deck train hold, physical gate closure, tail clearance and reopening. No player save changed.\n");
        else{File.WriteAllText(TownGatewayBatch.Report+"/play.txt","FAILED\n"+error);Debug.LogException(error);}
        EditorApplication.Exit(error==null?0:1);
    }
}
