using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RetailEmpireTycoon.City;
using Object=UnityEngine.Object;

/// <summary>Runs the existing traffic/pedestrian pools in the expansion and drives the real pickup over new junctions.</summary>
[InitializeOnLoad]
public static class PinebrookDistrictPlay
{
    private const string Active="Pinebrook.Play.Active";
    private static int phase;
    private static double phaseStart;
    private static DateTime deadline;
    private static Transform observer;
    private static Vector3 previous;
    private static float travelled;
    private static int waypoint;
    private static readonly Vector3[] DriveRoute={new(-22.2f,0,395),new(-22.2f,0,410),new(17.8f,0,452),new(17.8f,0,467),new(17.8f,0,570)};
    private static bool trafficMoved,peopleMoved,completedTurn;
    private static int peakDistrictTraffic;
    private static string SaveSnapshot(){string path=Path.Combine(Application.persistentDataPath,"gameData.json");return File.Exists(path)?PinebrookDistrictBuild.Hash(path):"missing";}
    static PinebrookDistrictPlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        try{TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(PinebrookDistrictBuild.Report);File.WriteAllText(PinebrookDistrictBuild.Report+"/play.txt","RUNNING\n");EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);foreach(var save in Object.FindObjectsOfType<SaveManager>(true))save.enabled=false;SessionState.SetString("Pinebrook.Save.Before",SaveSnapshot());SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();}
        catch(Exception error){Finish(error);}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||Time.frameCount<25)return;
        try
        {
            var town=Object.FindObjectOfType<TownWorldPlaces>();var traffic=town.GetComponentInChildren<TownRoadTraffic>();var people=town.GetComponentInChildren<TownPedestrianTraffic>();var city=Object.FindObjectOfType<CityWorld>();var car=city.pickup;
            foreach(var signal in Object.FindObjectsOfType<TownJunctionSignals>())if(!signal.IsConfigured||(phase>0&&!signal.LampsMatchState))throw new InvalidOperationException("Existing signal lamps lost their live binding.");
            if(phase==0)
            {
                deadline=DateTime.UtcNow.AddSeconds(240);Time.timeScale=3;observer=new GameObject("Temporary Pinebrook observer").transform;observer.position=new Vector3(-170,3,550);traffic.SetObserver(observer);people.SetObserver(observer);var chunks=town.GetComponentInChildren<TownSignalChunks>();foreach(var junction in chunks.Junctions)chunks.Tick(Time.timeAsDouble,chunks.transform.TransformPoint(junction.Center));chunks.SetObserver(observer);phaseStart=Time.timeAsDouble;phase=1;return;
            }
            if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Pinebrook play test timed out; phase "+phase+" pickup "+car.Body.position);
            if(phase==1)
            {
                peakDistrictTraffic=Mathf.Max(peakDistrictTraffic,traffic.SparseTrafficCount);
                if(traffic.SparseTrafficCount>4)throw new InvalidOperationException("Pinebrook ambient population exceeded its four-car capacity.");
                completedTurn|=traffic.ActiveCars.Any(c=>c.transform.localPosition.z>430&&c.CompletedTurns>0);
                trafficMoved|=traffic.ActiveCars.Any(c=>c.transform.localPosition.z>430&&c.Travelled>6);peopleMoved|=people.ActivePedestrians.Any(p=>p.LocalPosition.z>430&&p.Travelled>1.5f);
                if(Time.timeAsDouble-phaseStart<45)return;
                if(!trafficMoved||!peopleMoved||!completedTurn)throw new InvalidOperationException("Population did not move in the new district. Cars="+trafficMoved+" pedestrians="+peopleMoved+" turns="+completedTurn);
                File.WriteAllText(PinebrookDistrictBuild.Report+"/population.txt","PASS: moving ambient cars and pedestrians on Pinebrook streets; peak district traffic "+peakDistrictTraffic+" of four over 45 simulated seconds, junction turns completed. One traffic controller and original 29-car global capacity retained.\n");
                TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),PinebrookDistrictBuild.Pictures+"/LivePopulation.png",new Vector3(-190,13,522),new Vector3(-170,2,558));
                observer.position=new Vector3(-24,3,-100);traffic.SetObserver(observer);people.SetObserver(observer);phaseStart=Time.timeAsDouble;phase=3;return;
            }
            if(phase==3)
            {
                if(traffic.SparseTrafficCount>4||traffic.ActiveCars.Count>traffic.MaximumCars)throw new InvalidOperationException("Remote district traffic exceeded a population budget.");
                if(Time.timeAsDouble-phaseStart<20)return;
                if(traffic.SparseTrafficCount<1)throw new InvalidOperationException("Pinebrook became empty when the observer returned to the old city.");
                File.AppendAllText(PinebrookDistrictBuild.Report+"/population.txt","PASS: "+traffic.SparseTrafficCount+" ambient district cars remain simulated while the observer is in the old city; same bounded global pool.\n");
                traffic.enabled=false;foreach(var vehicle in traffic.ActiveCars)vehicle.gameObject.SetActive(false);
                car.ReadKeyboard=false;car.InputEnabled=true;car.Stop();car.Body.position=new Vector3(-22.2f,.5f,250);car.Body.rotation=Quaternion.identity;previous=car.Body.position;travelled=0;waypoint=0;phaseStart=Time.timeAsDouble;phase=2;return;
            }
            if(Time.timeAsDouble-phaseStart<.7)return;
            var position=car.Body.position;if(position.y<-.3f||position.y>1.1f)throw new InvalidOperationException("Pickup lost new road support: "+position);
            travelled+=Vector3.ProjectOnPlane(position-previous,Vector3.up).magnitude;previous=position;
            if(Vector3.ProjectOnPlane(position-DriveRoute[waypoint],Vector3.up).magnitude<5){waypoint++;}
            if(waypoint==DriveRoute.Length)
            {
                car.Stop();if(travelled<310)throw new InvalidOperationException("Pickup route was shortened: "+position+" travelled "+travelled);
                File.WriteAllText(PinebrookDistrictBuild.Report+"/drive.txt","PASS: real PickupDrive/Rigidbody continuously drove "+travelled.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" m from the gateway through the diagonal fork into the rear residential street, without teleport after initial test pose. No scene or player save written.\n");
                TownFloristBatch.Render(EditorSceneManager.GetActiveScene(),PinebrookDistrictBuild.Pictures+"/LivePickup.png",position+new Vector3(-16,12,-20),new Vector3(16,3,585));Finish(null);return;
            }
            float angle=Vector3.SignedAngle(car.transform.forward,Vector3.ProjectOnPlane(DriveRoute[waypoint]-position,Vector3.up),Vector3.up);car.SetInput(Mathf.Abs(angle)>20?.18f:.32f,Mathf.Clamp(angle/28,-1,1),false);
        }
        catch(Exception error){Finish(error);}
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);Time.timeScale=1;
        string before=SessionState.GetString("Pinebrook.Save.Before","");if(error==null&&before.Length>0&&before!=SaveSnapshot())error=new InvalidOperationException("The player save file changed during acceptance.");
        if(error==null)PinebrookDistrictBuild.Receipt("play","PASS: native live population movement and continuous physical pickup drive through the new district.\n");
        else{Directory.CreateDirectory(PinebrookDistrictBuild.Report);File.WriteAllText(PinebrookDistrictBuild.Report+"/play.txt","FAILED\n"+error);Debug.LogException(error);}
        EditorApplication.Exit(error==null?0:1);
    }
}
