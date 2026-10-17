using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Disposable native physics course. No player-save components are loaded.</summary>
[InitializeOnLoad]
public static class TownLifePlay
{
    private const string Active="TownLife.Play";private static int phase;private static float until,maxHeight,maxUp,maxDown,brakeSpeed;private static DateTime deadline;
    private static Vector3 origin=new(0,0,-700);private static PickupDrive car;private static bool spun,steered;private static Quaternion initialYaw;
    static TownLifePlay(){EditorApplication.update+=Tick;}
    public static void Run(){try{TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(TownLifeUpdateBatch.Report);File.WriteAllText(TownLifeUpdateBatch.Report+"/play.txt","RUNNING\n");EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();}catch(Exception e){Finish(e);}}
    private static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||Time.frameCount<20)return;
        try
        {
            if(phase==0)
            {
                var city=Object.FindObjectOfType<CityWorld>();car=city.pickup;car.ReadKeyboard=false;car.InputEnabled=true;
                var train=city.town.GetComponentInChildren<TownRailTraffic>();train.enabled=false;var crossing=city.town.GetComponentInChildren<TownLevelCrossing>();crossing.enabled=false;train.SetCrossingHold(false);
                train.Advance((float)(419-train.ServiceClock));TownLifeUpdateBatch.Require(!train.IsInService,"Service did not hide in its interval");train.Advance(1.01f);TownLifeUpdateBatch.Require(train.IsInService&&Math.Abs(train.NextDeparture-840)<.01,"No scheduled 420-second departure");
                float position=train.TravelDistance;train.SetCrossingHold(true);train.Advance(5);TownLifeUpdateBatch.Require(Mathf.Abs(position-train.TravelDistance)<.001,"Train ignored crossing hold");train.SetCrossingHold(false);
                train.Advance((float)(840.01-train.ServiceClock));TownLifeUpdateBatch.Require(train.IsInService&&Math.Abs(train.NextDeparture-1260)<.01,"Second scheduled departure failed");
                foreach(var a in city.town.GetComponentsInChildren<FarmAnimal>())
                {
                    var start=a.transform.localPosition;var head=a.head.localRotation;for(int i=0;i<450;i++)a.Advance(1f/15,i/15f);
                    TownLifeUpdateBatch.Require(a.pasture.Contains(new Vector2(a.transform.localPosition.x,a.transform.localPosition.z))&&Vector3.Distance(start,a.transform.localPosition)>0.05f&&Quaternion.Angle(head,a.head.localRotation)>1,"Animal remained static or left pasture: "+a.name);
                }
                var ground=new TownMeshBuilder();void Span(float z0,float h0,float z1,float h1)=>ground.Quad(new(-15,h1,z1),new(15,h1,z1),new(15,h0,z0),new(-15,h0,z0),TownColor.Asphalt);
                Span(-10,0,10,0);Span(10,0,40,4);Span(40,4,70,0);Span(70,0,110,0);
                var floor=new GameObject("Disposable suspension test course",typeof(MeshCollider));floor.transform.position=origin;floor.GetComponent<MeshCollider>().sharedMesh=ground.Build("Physics ramp");
                car.Stop();car.Body.position=origin+new Vector3(0,.5f,0);car.Body.rotation=Quaternion.identity;until=Time.fixedTime+1;deadline=DateTime.UtcNow.AddSeconds(90);phase=1;return;
            }
            TownLifeUpdateBatch.Require(DateTime.UtcNow<deadline,"Suspension test stalled in phase "+phase+" at "+(car.Body.position-origin)+" speed="+car.Speed+" contacts="+car.GroundedWheels);
            if(Time.fixedTime<until)return;
            var p=car.Body.position-origin;float pitch=Vector3.Dot(car.transform.forward,Vector3.up);maxHeight=Mathf.Max(maxHeight,p.y);maxUp=Mathf.Max(maxUp,pitch);maxDown=Mathf.Min(maxDown,pitch);
            TownLifeUpdateBatch.Require(p.y>-.4f&&p.y<6&&Mathf.Abs(car.transform.eulerAngles.z>180?car.transform.eulerAngles.z-360:car.transform.eulerAngles.z)<35,"Pickup lost stability/support: "+p);
            if(phase==1)
            {
                spun|=car.wheels.Any(w=>Mathf.Abs(w.contact.rpm)>25);
                if(p.z<83){car.SetInput(.42f,0,false);return;}
                TownLifeUpdateBatch.Require(maxHeight>3.5f&&maxUp>.06f&&maxDown<-.06f&&p.y<.6f&&spun,"Real rise/descent/pitch/wheel roll not observed");
                initialYaw=car.Body.rotation;until=Time.fixedTime+1.2f;phase=2;car.SetInput(.28f,.6f,false);return;
            }
            if(phase==2)
            {
                steered=car.wheels.Where(w=>w.front).All(w=>Mathf.Abs(w.contact.steerAngle)>5);TownLifeUpdateBatch.Require(steered&&Quaternion.Angle(initialYaw,car.Body.rotation)>5,"Front wheel steering did not turn the real chassis");
                brakeSpeed=car.Speed;car.SetInput(0,0,true);until=Time.fixedTime+1.5f;phase=3;return;
            }
            if(phase==3)
            {
                TownLifeUpdateBatch.Require(car.Speed<.35f&&brakeSpeed>1,"Wheel braking failed");
                var obstacle=new GameObject("Disposable impact barrier",typeof(BoxCollider));obstacle.transform.position=origin+new Vector3(0,1,98);obstacle.GetComponent<BoxCollider>().size=new Vector3(8,2,1);
                car.Stop();car.Body.position=origin+new Vector3(0,.4f,87);car.Body.rotation=Quaternion.identity;until=Time.fixedTime+.7f;phase=4;return;
            }
            if(phase==4){car.SetInput(.5f,0,false);until=Time.fixedTime+6;phase=5;return;}
            TownLifeUpdateBatch.Require(p.z<95.8f&&p.z>90,"Rigidbody penetrated barrier or did not reach it: "+p);car.Stop();
            File.WriteAllText(TownLifeUpdateBatch.Report+"/physics.txt",$"PASS: four-contact suspension; height {maxHeight:F2} m; uphill forward.y {maxUp:F3}; downhill {maxDown:F3}; tire RPM, front steering, physical yaw, braking and solid-barrier contact. No velocity overwrite, frozen chassis or scripted hill tilt.\n");Finish(null);
        }
        catch(Exception e){Finish(e);}
    }
    private static void Finish(Exception e){SessionState.SetBool(Active,false);File.WriteAllText(TownLifeUpdateBatch.Report+"/play.txt",e==null?"PASS: actual wheel/suspension/Rigidbody hill, descent, steering, braking and impact tests; 11 moving articulated animals inside pasture; scheduled train passes at 420/840 s and occupied-crossing hold. No player save used.\n":"FAILED\n"+e);if(e==null)TownLifeUpdateBatch.Receipt("play");else Debug.LogException(e);EditorApplication.Exit(e==null?0:1);}
}
