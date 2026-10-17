using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Observes a complete live signal cycle and drives the actual pickup through nine junctions.
/// These are clearance drives, not an automatic road-rule driver or a player-input restriction.</summary>
[InitializeOnLoad]
public static class TownJunctionPlay
{
    private const string Active="Junction.Play.Active",Case="Junction.Play.Case",Phase="Junction.Play.Phase";
    private static Dictionary<TownJunctionSignals,HashSet<string>> observed;
    private static TownJunctionPlan.Junction[] routes;
    private static double cycleStart;
    private static DateTime deadline,routeDeadline;
    private static Vector3 start,end,forward,previous;
    private static float settled,travelled;

    static TownJunctionPlay(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(TownJunctionBatch.Report);
            File.WriteAllText(TownJunctionBatch.Report+"/play.txt","RUNNING\n");File.WriteAllText(TownJunctionBatch.Report+"/drives.txt","");
            EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);SessionState.SetInt(Case,0);SessionState.SetInt(Phase,0);
            SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception error){Finish(error);}
    }

    private static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||Time.frameCount<15)return;
        try
        {
            var city=Object.FindObjectOfType<CityWorld>();TownJunctionBatch.Require(city!=null,"CityWorld missing.");
            if(observed==null)
            {
                observed=Object.FindObjectsOfType<TownJunctionSignals>().ToDictionary(s=>s,_=>new HashSet<string>());
                TownJunctionBatch.Require(observed.Count==6,"Six live signal controllers required.");
                cycleStart=Time.timeAsDouble;deadline=DateTime.UtcNow.AddSeconds(240);
                var plan=TownJunctionPlan.Create(city.town.layout);
                var priorityCells=new[]{new Vector2Int(-10,-6),new(22,4),new(-2,4)};
                routes=plan.Where(j=>j.HasSignals).Concat(priorityCells.Select(c=>plan.Single(j=>j.Cell==c))).ToArray();
            }
            foreach(var entry in observed)
            {
                var signal=entry.Key;TownJunctionBatch.Require(signal.LampsMatchState,"Live lenses disagree with controller.");
                var state=signal.Current;
                TownJunctionBatch.Require(!state.PedestrianWalk||(state.NorthSouth==JunctionSignalAspect.Red&&state.EastWest==JunctionSignalAspect.Red),"Live pedestrian conflict.");
                entry.Value.Add(state.NorthSouth+"/"+state.EastWest+"/"+state.PedestrianWalk);
            }
            if(DateTime.UtcNow>deadline)throw new InvalidOperationException("Live signal/drive acceptance timed out.");
            int index=SessionState.GetInt(Case,0);var car=city.pickup;
            if(index>=routes.Length)
            {
                car.Stop();
                if(Time.timeAsDouble-cycleStart<61)return;
                TownJunctionBatch.Require(observed.Values.All(s=>s.Count==8),"A live signal cycle missed a state.");
                File.WriteAllLines(TownJunctionBatch.Report+"/runtime-phases.txt",observed.Select(p=>p.Key.name+": "+p.Value.Count+" distinct safe live states over "+(Time.timeAsDouble-cycleStart).ToString("F1")+" seconds"));
                Finish(null);return;
            }
            if(SessionState.GetInt(Phase,0)==0)
            {
                var j=routes[index];int approach=(j.Mask&5)==5?0:1;
                start=TownJunctionPlan.At(j,approach,19,2);end=TownJunctionPlan.At(j,approach,-19,2);forward=(end-start).normalized;
                car.ReadKeyboard=false;car.InputEnabled=true;car.Stop();car.Body.position=start+Vector3.up*.4f;car.Body.rotation=Quaternion.LookRotation(forward);
                previous=start;travelled=0;settled=Time.fixedTime+.5f;routeDeadline=DateTime.UtcNow.AddSeconds(40);
                SessionState.SetInt(Phase,1);return;
            }
            if(Time.fixedTime<settled)return;
            if(DateTime.UtcNow>routeDeadline)throw new InvalidOperationException("Pickup blocked at "+routes[index].Cell+" position="+car.Body.position+" velocity="+car.Body.velocity);
            TownJunctionBatch.Require(car.Body.position.y>-.3f&&car.Body.position.y<.7f,"Pickup lost road floor.");
            travelled+=Vector3.ProjectOnPlane(car.Body.position-previous,Vector3.up).magnitude;previous=car.Body.position;
            if(Vector3.Dot(car.Body.position-start,forward)<37.2f){car.SetInput(.35f,0,false);return;}
            car.Stop();File.AppendAllText(TownJunctionBatch.Report+"/drives.txt",routes[index].Cell+" "+(routes[index].HasSignals?"signal":"priority")+": actual Rigidbody drive "+travelled.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" m; end="+car.Body.position+"\n");
            SessionState.SetInt(Case,index+1);SessionState.SetInt(Phase,0);
        }
        catch(Exception error){Finish(error);}
    }

    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);
        if(error==null)TownJunctionBatch.Receipt("play","PASS: nine actual native pickup clearance drives; all six controllers observed through a complete live cycle, including exclusive pedestrian walk and clearance. No player save or traffic AI changed.\n");
        else{File.WriteAllText(TownJunctionBatch.Report+"/play.txt","FAILED\n"+error);Debug.LogException(error);}
        EditorApplication.Exit(error==null?0:1);
    }
}
