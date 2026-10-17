using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Actual LateUpdate lamp transitions and a Rigidbody crossing of the new shop entrance.
/// Only the isolated City scene is used; no save components or player save files are involved.</summary>
[InitializeOnLoad]
public static class TownSiteRevisionPlay
{
    private const string Active="SiteRevision.Play.Active";
    private static int phase;private static double until;private static DateTime deadline;
    private static TownSignalChunks stream;private static TownJunctionSignals watched,distant;
    private static JunctionSignalState cached,last;private static int changes;private static Vector3 start,end,direction;
    static TownSiteRevisionPlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(TownSiteRevisionBatch.Report);File.WriteAllText(TownSiteRevisionBatch.Report+"/signals-play.txt","RUNNING\n");
            EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Finish(e);}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||Time.frameCount<20)return;
        try
        {
            var city=Object.FindObjectOfType<CityWorld>();var car=city.pickup;
            if(phase==0)
            {
                stream=city.town.GetComponent<TownSignalChunks>();car.ReadKeyboard=false;car.InputEnabled=true;car.Stop();car.Body.position=new Vector3(-120,1,-144);stream.SetObserver(car.transform);
                until=Time.realtimeSinceStartupAsDouble+1;deadline=DateTime.UtcNow.AddSeconds(50);phase=1;return;
            }
            if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Runtime site regression timed out in phase "+phase+" at "+car.Body.position);
            if(phase==1)
            {
                if(Time.realtimeSinceStartupAsDouble<until)return;
                watched=stream.Junctions.First(j=>stream.IsActive(j.Signal)).Signal;distant=stream.Junctions.Last(j=>!stream.IsActive(j.Signal)).Signal;cached=distant.Current;last=watched.Current;
                if(stream.ActiveJunctionCount>=6)throw new InvalidOperationException("Runtime tick updates all signals.");
                Time.timeScale=8;until=Time.timeAsDouble+TownJunctionSignals.CycleSeconds+2;phase=2;return;
            }
            if(phase==2)
            {
                car.Stop();if(!watched.LampsMatchState||!distant.Current.Equals(cached))throw new InvalidOperationException("Lamp bindings or inactive-chunk cache changed incorrectly.");
                if(!watched.Current.Equals(last)){changes++;last=watched.Current;}
                if(Time.timeAsDouble<until)return;
                if(changes<6)throw new InvalidOperationException("Actual LateUpdate did not animate the safe cycle: "+changes);
                Time.timeScale=1;var a=TownSiteRevisionPlacement.ShopEntry;direction=Vector3.ProjectOnPlane(a.To-a.From,Vector3.up).normalized;start=a.From-direction*2.3f;end=a.To+direction*2;
                car.Stop();car.Body.position=start+Vector3.up*.35f;car.Body.rotation=Quaternion.LookRotation(direction);until=Time.fixedTime+.6;deadline=DateTime.UtcNow.AddSeconds(40);phase=3;return;
            }
            if(Time.fixedTime<until)return;
            var offset=Vector3.ProjectOnPlane(car.Body.position-start,Vector3.up);
            if(car.Body.position.y<-.3f||car.Body.position.y>1.3f||Vector3.ProjectOnPlane(offset,direction).magnitude>1)throw new InvalidOperationException("New entrance lost floor or lateral clearance: "+car.Body.position);
            if(Vector3.Dot(car.Body.position-end,direction)<-.1f){car.SetInput(.28f,0,false);return;}
            car.Stop();File.WriteAllText(TownSiteRevisionBatch.Report+"/shop-drive.txt","PASS: actual pickup FixedUpdate/Rigidbody crossed "+Vector3.Dot(offset,direction).ToString("F2")+" m from city road through new straight lowered entrance into forecourt.\n");Finish(null);
        }
        catch(Exception e){Finish(e);}
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);Time.timeScale=1;
        File.WriteAllText(TownSiteRevisionBatch.Report+"/signals-play.txt",error==null?"PASS: actual chunk coordinator LateUpdate produced "+changes+" lamp-state transitions through a complete cycle; distant junction state remained cached. New straight entrance driven by real pickup Rigidbody.\n":"FAILED\n"+error);
        if(error==null)File.WriteAllText(TownSiteRevisionBatch.Report+"/signals-input.sha256",TownForgottenYardBatch.Hash(TownWorldAssembler.PrefabPath));else Debug.LogException(error);
        EditorApplication.Exit(error==null?0:1);
    }
}
