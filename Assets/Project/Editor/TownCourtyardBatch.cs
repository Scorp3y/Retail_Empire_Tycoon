using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Stage-four courtyard authoring and acceptance in the disposable Unity clone only.</summary>
[InitializeOnLoad]
public static class TownCourtyardBatch
{
    private const string Report="Library/TownCourtyardQA";
    private const string Running="RetailEmpire.Courtyard.Running";
    private const string Phase="RetailEmpire.Courtyard.Phase";
    private const string Deadline="RetailEmpire.Courtyard.Deadline";
    static TownCourtyardBatch(){EditorApplication.update+=Tick;}

    public static void Generate()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            Directory.CreateDirectory(Report);
            File.WriteAllText(Report + "/result.txt", "RUNNING\n");
            var root = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            TownCourtyardPlan plan;
            TownCourtyardValidation.ResidentialSnapshot architecture;
            TownPilotScopePreservation.Snapshot outside;
            string farm = TownFarmPreservation.Capture();
            var suppliers = TownSupplierPreservation.Capture();
            try
            {
                plan = TownCourtyardPlan.Create(root);
                architecture = TownCourtyardValidation.CaptureResidentialSnapshot(root);
                outside = TownPilotScopePreservation.Capture(plan.AllowedAreas);
                new TownCourtyardPlacement(plan, root).Build();
                architecture.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));
                PrefabUtility.SaveAsPrefabAsset(root, TownWorldAssembler.PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            TownCourtyardValidation.ValidateSaved(plan);
            var saved = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try { architecture.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(saved)); }
            finally { PrefabUtility.UnloadPrefabContents(saved); }
            var currentOutside = TownPilotScopePreservation.Capture(plan.AllowedAreas);
            outside.RequireUnchanged(currentOutside);
            if (TownFarmPreservation.Capture() != farm) throw new InvalidOperationException("Protected farm changed.");
            var current = TownSupplierPreservation.Capture();
            foreach (var pair in suppliers)
                if (!current.TryGetValue(pair.Key, out var value) || value != pair.Value)
                    throw new InvalidOperationException("Protected supplier changed: " + pair.Key);
            File.WriteAllText(Report + "/protection.txt",
                "PASS: outside parcel geometry/materials/collision, original 62 buildings and 299 door routes unchanged.\n" +
                "Residential: " + architecture + "\nBefore: " + outside + "\nAfter: " + currentOutside +
                "\nFarm: " + farm + "\nSuppliers:\n" + string.Join("\n", suppliers));
            File.WriteAllText(Report + "/plan.json", JsonUtility.ToJson(new CourtyardReport {
                blocks = plan.Blocks.Select(b => new CourtyardBlockReport {
                    id = b.Id, trees = b.Trees.Count, connections = b.Paths.Count,
                    zones = b.Zones.Select(z => new CourtyardZoneReport {
                        id = z.Id, kind = z.Kind.ToString(), bounds = z.Bounds, entrance = z.Entrance,
                        models = z.Props.Select(p => p.ModelId).ToArray(), slots = z.Slots.Count
                    }).ToArray()
                }).ToArray()
            }, true));
            File.WriteAllText(Report + "/result.txt", "TOWN COURTYARDS PASSED: native templates, finished functional zones, clear physical passages and protected world.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Report);
            File.WriteAllText(Report + "/result.txt", "FAILED\n" + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    public static void ValidatePlayMode()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            var root = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try { TownCourtyardValidation.ValidateSaved(TownCourtyardPlan.Create(root)); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Directory.CreateDirectory("Library/TownWorldQA");
            Directory.CreateDirectory("Library/CityUpdateQA");
            File.WriteAllText("Library/TownWorldQA/integration.txt", "RUNNING\n");
            File.WriteAllText("Library/CityUpdateQA/integration.txt", "RUNNING\n");
            File.WriteAllText(Report + "/gameplay.txt", "RUNNING\n");
            File.WriteAllText(Report + "/runtime.txt", "PENDING\n");
            EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
            ShopGameplaySandbox.Open();
            SessionState.SetBool(Running, true); SessionState.SetInt(Phase, 0);
            SessionState.SetString(Deadline, DateTime.UtcNow.AddMinutes(12).ToString("O"));
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Debug.LogException(error); Finish(1, error.ToString()); }
    }

    private static void Tick()
    {
        if(!Application.isBatchMode||!SessionState.GetBool(Running,false))return;
        try
        {
            if(DateTime.UtcNow>DateTime.Parse(SessionState.GetString(Deadline,""),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind))
                throw new TimeoutException("Courtyard gameplay acceptance timed out.");
            if(!EditorApplication.isPlaying)return;
            int phase=SessionState.GetInt(Phase,0);
            if(phase==0)
            {if(Time.frameCount<8)return;SessionState.SetInt(Phase,1);TownWorldValidation.Run();return;}
            if(phase==1)
            {
                string result=File.ReadAllText("Library/TownWorldQA/integration.txt");
                if(result.StartsWith("FAILED"))throw new InvalidOperationException(result);
                if(!result.StartsWith("TOWN WORLD PASSED"))return;
                SessionState.SetInt(Phase,2);CityUpdateValidation.Run();return;
            }
            if(phase==2)
            {
                string result=File.ReadAllText("Library/CityUpdateQA/integration.txt");
                if(result.Contains("FAILED"))throw new InvalidOperationException(result);
                if(!result.Contains("CITY DELIVERY INTEGRATION PASSED"))return;
                SessionState.SetInt(Phase,3);
                var trip=Object.FindObjectOfType<CityTrip>();
                if(trip==null)throw new InvalidOperationException("Sandbox trip disappeared.");
                trip.StartCoroutine(Guard(CourtyardDrive(trip)));
            }
        }
        catch(Exception error){Debug.LogException(error);Finish(1,error.ToString());}
    }

    private static IEnumerator CourtyardDrive(CityTrip trip)
    {
        trip.Begin();float deadline=Time.realtimeSinceStartup+90;
        while((!trip.InCity||trip.Transitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
        if(!trip.InCity||trip.Transitioning)throw new InvalidOperationException("Residential drive departure failed.");
        var city=Object.FindObjectOfType<CityWorld>();
        if(city==null)throw new InvalidOperationException("Live City is missing.");
        var town = city.town;
        if (town == null) throw new InvalidOperationException("Live courtyard world is missing.");
        var plan = TownCourtyardPlan.Create(town.gameObject);
        yield return TownCourtyardValidation.Check(city, plan);
        yield return TownResidentialValidation.Check(city, TownResidentialPlan.Create());
        trip.Return();deadline=Time.realtimeSinceStartup+90;
        while((trip.InCity||trip.Transitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
        if(trip.InCity||trip.Transitioning)throw new InvalidOperationException("Residential drive return failed.");
        Finish(0,"PASS original shop/customer/supplier/delivery/parking regressions plus courtyard and residential physical access and real native pickup drives.\n"+
            File.ReadAllText(Report+"/runtime.txt"));
    }

    private static IEnumerator Guard(IEnumerator routine)
    {
        var stack=new Stack<IEnumerator>();stack.Push(routine);
        while(stack.Count>0)
        {
            bool moved=false;object current=null;Exception failure=null;
            try{moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
            catch(Exception error){failure=error;}
            if(failure!=null)
            {while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();Debug.LogException(failure);Finish(1,failure.ToString());yield break;}
            if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
            if(current is IEnumerator nested&&!(current is CustomYieldInstruction))stack.Push(nested);else yield return current;
        }
    }
    private static void Finish(int code,string result)
    {SessionState.SetBool(Running,false);Directory.CreateDirectory(Report);File.WriteAllText(Report+"/gameplay.txt",code==0?result:"FAILED\n"+result);EditorApplication.Exit(code);}
    [Serializable] private sealed class CourtyardReport { public CourtyardBlockReport[] blocks; }
    [Serializable] private sealed class CourtyardBlockReport { public string id; public int trees, connections; public CourtyardZoneReport[] zones; }
    [Serializable] private sealed class CourtyardZoneReport { public string id, kind; public Rect bounds; public Vector3 entrance; public string[] models; public int slots; }

}


