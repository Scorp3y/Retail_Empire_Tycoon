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

/// <summary>Stage-six exact detail repair and acceptance in the disposable Unity clone only.</summary>
[InitializeOnLoad]
public static class TownSeamBatch
{
    private const string Report="Library/TownSeamQA";
    private const string Running="RetailEmpire.Seam.Running";
    private const string Phase="RetailEmpire.Seam.Phase";
    private const string Deadline="RetailEmpire.Seam.Deadline";
    static TownSeamBatch(){EditorApplication.update+=Tick;}

    public static void Generate()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            Directory.CreateDirectory(Report);
            File.WriteAllText(Report + "/result.txt", "RUNNING\n");
            var root = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            TownSeamPlan plan;
            TownCourtyardValidation.ResidentialSnapshot architecture;
            TownPilotScopePreservation.Snapshot outside;
            string farm = TownFarmPreservation.Capture();
            var suppliers = TownSupplierPreservation.Capture();
            try
            {
                plan = new TownSeamPlan(root);
                architecture = TownCourtyardValidation.CaptureResidentialSnapshot(root);
                File.WriteAllText(Report+"/audit.txt","PASS original saved triangle audit\n"+string.Join("\n",plan.Audit));
                File.WriteAllText(Report+"/plan.json",JsonUtility.ToJson(new TownSeamReport{replacements=plan.Replacements.ToArray()},true));
                File.WriteAllText(Report+"/audit.json",JsonUtility.ToJson(new TownSeamReport{replacements=plan.Replacements.ToArray()},true));
                TownPilotScopePreservation.ValidateMultiAreaContracts();
                outside = TownPilotScopePreservation.Capture(plan.AllowedAreas);
                new TownSeamPlacement(plan, root).Build();
                architecture.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));
                PrefabUtility.SaveAsPrefabAsset(root, TownWorldAssembler.PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            TownSeamValidation.ValidateSaved(plan.Replacements);
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
            File.WriteAllText(Report + "/result.txt", "TOWN SEAM REPAIRS PASSED: actual detail contacts, exact native geometry and protected previous stages.\n");
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
            try { TownSeamValidation.ValidateSaved(TownSeamValidation.ReadPlan()); ValidatePreviousStages(); }
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
                throw new TimeoutException("Seam gameplay acceptance timed out.");
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
                trip.StartCoroutine(Guard(SeamDrive(trip)));
            }
        }
        catch(Exception error){Debug.LogException(error);Finish(1,error.ToString());}
    }

    private static IEnumerator SeamDrive(CityTrip trip)
    {
        trip.Begin();float deadline=Time.realtimeSinceStartup+90;
        while((!trip.InCity||trip.Transitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
        if(!trip.InCity||trip.Transitioning)throw new InvalidOperationException("Residential drive departure failed.");
        var city=Object.FindObjectOfType<CityWorld>();
        if(city==null)throw new InvalidOperationException("Live City is missing.");
        var town = city.town;
        if (town == null) throw new InvalidOperationException("Live courtyard world is missing.");
        yield return TownSeamValidation.Check(city,TownSeamValidation.ReadPlan());
        yield return TownSpecialValidation.Check(city,TownSpecialPlan.Create());
        yield return TownCourtyardValidation.Check(city, TownCourtyardPlan.Create(town.gameObject));
        yield return TownResidentialValidation.Check(city, TownResidentialPlan.Create());
        foreach(var pair in new[] {
            ("Library/TownWorldQA/integration.txt","original-town-integration.txt"),
            ("Library/CityUpdateQA/integration.txt","original-city-integration.txt"),
            ("Library/TownCourtyardQA/runtime.txt","courtyards-runtime.txt"),
            ("Library/TownResidentialQA/runtime.txt","residential-runtime.txt"),
            ("Library/TownSpecialQA/runtime.txt","industrial-runtime.txt")
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
        try { TownCourtyardValidation.ValidateSaved(TownCourtyardPlan.Create(town)); TownSpecialValidation.ValidateSaved(TownSpecialPlan.Create()); }
        finally { PrefabUtility.UnloadPrefabContents(town); }
    }
    public static void Audit()
    {
        GameObject town=null;
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);
            town=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);var plan=new TownSeamPlan(town);
            File.WriteAllText(Report+"/audit.txt","PASS read-only saved geometry audit\n"+string.Join("\n",plan.Audit));
            File.WriteAllText(Report+"/audit.json",JsonUtility.ToJson(new TownSeamReport{replacements=plan.Replacements.ToArray()},true));
            EditorApplication.Exit(0);
        }
        catch(Exception error){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/audit.txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
        finally{if(town!=null)PrefabUtility.UnloadPrefabContents(town);}
    }
}
