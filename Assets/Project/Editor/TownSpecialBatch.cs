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

/// <summary>Stage-five industrial authoring and acceptance in the disposable Unity clone only.</summary>
[InitializeOnLoad]
public static class TownSpecialBatch
{
    private const string Report="Library/TownSpecialQA";
    private const string Running="RetailEmpire.Special.Running";
    private const string Phase="RetailEmpire.Special.Phase";
    private const string Deadline="RetailEmpire.Special.Deadline";
    static TownSpecialBatch(){EditorApplication.update+=Tick;}

    public static void Generate()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            Directory.CreateDirectory(Report);
            File.WriteAllText(Report + "/result.txt", "RUNNING\n");
            var root = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            TownSpecialPlan plan;
            TownCourtyardValidation.ResidentialSnapshot architecture;
            TownPilotScopePreservation.Snapshot outside;
            string farm = TownFarmPreservation.Capture();
            var suppliers = TownSupplierPreservation.Capture();
            try
            {
                plan = TownSpecialPlan.Create();
                architecture = TownCourtyardValidation.CaptureResidentialSnapshot(root);
                outside = TownPilotScopePreservation.Capture(plan.AllowedAreas);
                new TownSpecialPlacement(plan, root).Build();
                architecture.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));
                PrefabUtility.SaveAsPrefabAsset(root, TownWorldAssembler.PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            TownSpecialValidation.ValidateSaved(plan);
            ValidatePreviousStages();
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
            File.WriteAllText(Report + "/plan.json", JsonUtility.ToJson(new SpecialReport {
                sites = plan.Sites.Select(site => new SpecialSiteReport {
                    id = site.Id, bounds = site.Bounds, rootPath = site.RootPath,
                    models = site.Props.Select(p => p.Model.Id).ToArray(), routes = site.Routes.Count
                }).ToArray()
            }, true));
            File.WriteAllText(Report + "/result.txt", "TOWN SPECIAL SITES PASSED: native industrial complexes, actual coverage, clear physical access and protected previous stages.\n");
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
            try { TownSpecialValidation.ValidateSaved(TownSpecialPlan.Create()); ValidatePreviousStages(); }
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

    /// <summary>Read-only isolated gate diagnostic; never substitutes for the full Play acceptance.</summary>
    public static void DiagnoseGate()
    {
        TownWorldSafety.RequireIsolatedEditor();
        EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        SessionState.SetBool(Running, true); SessionState.SetInt(Phase, 10);
        SessionState.SetString(Deadline, DateTime.UtcNow.AddMinutes(3).ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if(!Application.isBatchMode||!SessionState.GetBool(Running,false))return;
        try
        {
            if(DateTime.UtcNow>DateTime.Parse(SessionState.GetString(Deadline,""),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind))
                throw new TimeoutException("Industrial gameplay acceptance timed out.");
            if(!EditorApplication.isPlaying)return;
            int phase=SessionState.GetInt(Phase,0);
            if(phase==10)
            {
                if(Time.frameCount<8)return;
                var city=Object.FindObjectOfType<CityWorld>();
                if(city==null)throw new InvalidOperationException("Diagnostic City is missing.");
                SessionState.SetInt(Phase,11);city.StartCoroutine(Guard(GateDiagnostic(city)));return;
            }
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
                trip.StartCoroutine(Guard(SpecialDrive(trip)));
            }
        }
        catch(Exception error){Debug.LogException(error);Finish(1,error.ToString());}
    }

    private static IEnumerator GateDiagnostic(CityWorld city)
    {
        yield return TownSpecialValidation.Check(city,TownSpecialPlan.Create());
        Finish(0,"DIAGNOSTIC ONLY: gates passed; full gameplay acceptance must still run.\n");
    }

    private static IEnumerator SpecialDrive(CityTrip trip)
    {
        trip.Begin();float deadline=Time.realtimeSinceStartup+90;
        while((!trip.InCity||trip.Transitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
        if(!trip.InCity||trip.Transitioning)throw new InvalidOperationException("Residential drive departure failed.");
        var city=Object.FindObjectOfType<CityWorld>();
        if(city==null)throw new InvalidOperationException("Live City is missing.");
        var town = city.town;
        if (town == null) throw new InvalidOperationException("Live courtyard world is missing.");
        var plan = TownSpecialPlan.Create();
        yield return TownSpecialValidation.Check(city, plan);
        yield return TownCourtyardValidation.Check(city, TownCourtyardPlan.Create(town.gameObject));
        yield return TownResidentialValidation.Check(city, TownResidentialPlan.Create());
        foreach(var pair in new[] {
            ("Library/TownWorldQA/integration.txt","original-town-integration.txt"),
            ("Library/CityUpdateQA/integration.txt","original-city-integration.txt"),
            ("Library/TownCourtyardQA/runtime.txt","courtyards-runtime.txt"),
            ("Library/TownResidentialQA/runtime.txt","residential-runtime.txt")
        }) File.Copy(pair.Item1,Report+"/"+pair.Item2,true);
        trip.Return();deadline=Time.realtimeSinceStartup+90;
        while((trip.InCity||trip.Transitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
        if(trip.InCity||trip.Transitioning)throw new InvalidOperationException("Residential drive return failed.");
        Finish(0,"PASS original shop/customer/supplier/delivery/parking regressions plus industrial gates, courtyard and residential physical access and real native pickup drives.\n"+
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
    private static void ValidatePreviousStages()
    {
        var town = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try { TownCourtyardValidation.ValidateSaved(TownCourtyardPlan.Create(town)); }
        finally { PrefabUtility.UnloadPrefabContents(town); }
    }
    [Serializable] private sealed class SpecialReport { public SpecialSiteReport[] sites; }
    [Serializable] private sealed class SpecialSiteReport { public string id, rootPath; public Rect bounds; public string[] models; public int routes; }
}

