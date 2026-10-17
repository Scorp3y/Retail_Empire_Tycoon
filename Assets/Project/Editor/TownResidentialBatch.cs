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

/// <summary>Stage-three authoring/acceptance in the disposable Unity clone only.</summary>
[InitializeOnLoad]
public static class TownResidentialBatch
{
    private const string Report="Library/TownResidentialQA";
    private const string Running="RetailEmpire.Residential.Running";
    private const string Phase="RetailEmpire.Residential.Phase";
    private const string Deadline="RetailEmpire.Residential.Deadline";
    static TownResidentialBatch(){EditorApplication.update+=Tick;}

    public static void Generate()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);
            File.WriteAllText(Report+"/result.txt","RUNNING\n");
            var plan=TownResidentialPlan.Create();
            var layout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>(TownWorldSetup.LayoutPath);
            TownWorldPlan.Validate(layout);
            foreach(var block in plan.Blocks)
                if(block.Bounds.Overlaps(layout.reservedShopPlot))throw new InvalidOperationException("Residential parcel occupies store reserve: "+block.Id);
            var before=TownPilotScopePreservation.Capture(plan.AllowedAreas);
            string farm=TownFarmPreservation.Capture();var suppliers=TownSupplierPreservation.Capture();
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                if(root.transform.Find(TownResidentialPlan.HierarchyRoot)!=null)
                    throw new InvalidOperationException("Generation must start from the immutable original town, not from a previous candidate.");
                new TownResidentialPlacement(plan,root).Build();
                PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
            TownResidentialValidation.ValidateSaved(plan);
            var after=TownPilotScopePreservation.Capture(plan.AllowedAreas);before.RequireUnchanged(after);
            if(TownFarmPreservation.Capture()!=farm)throw new InvalidOperationException("Protected farm changed.");
            var current=TownSupplierPreservation.Capture();
            foreach(var pair in suppliers)
                if(!current.TryGetValue(pair.Key,out var value)||value!=pair.Value)throw new InvalidOperationException("Protected supplier changed: "+pair.Key);
            File.WriteAllText(Report+"/protection.txt","PASS: geometry/materials/collision outside the "+plan.Blocks.Count+" parcels plus two-metre pavement seams unchanged.\n"+
                "Before: "+before+"\nAfter: "+after+"\nFarm: "+farm+"\nSuppliers:\n"+string.Join("\n",suppliers)+"\n");
            File.WriteAllText(Report+"/plan.json",JsonUtility.ToJson(new PlanReport {blocks=plan.Blocks.Select(b=>new BlockReport {
                id=b.Id,district=b.District,scheme=b.Scheme,bounds=b.Bounds,buildingCount=b.Buildings.Count,
                footprint=b.Buildings.Sum(p=>p.Model.FootprintArea),families=b.Buildings.Select(p=>p.Family).Distinct().ToArray(),
                variants=b.Buildings.Select(p=>p.ModelId).Distinct().ToArray(),heights=b.Buildings.SelectMany(p=>p.Model.Volumes.Select(v=>v.BodyBounds.size.y)).Distinct().OrderBy(v=>v).ToArray(),
                placements=b.Buildings.Select(p=>new PlacementReport{id=p.Id,model=p.ModelId,position=p.Position,yaw=p.Yaw}).ToArray(),paths=b.Walkways.Count
            }).ToArray()},true));
            File.WriteAllText(Report+"/result.txt","TOWN RESIDENTIAL PASSED: saved placement, full-width paths, native architecture/materials, protected areas.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/result.txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }

    public static void ValidatePlayMode()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            var plan=TownResidentialPlan.Create();
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try{TownResidentialRoutes.ReadSaved(plan,root.transform);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            TownResidentialValidation.ValidateSaved(plan);
            Directory.CreateDirectory("Library/TownWorldQA");Directory.CreateDirectory("Library/CityUpdateQA");
            File.WriteAllText("Library/TownWorldQA/integration.txt","RUNNING\n");
            File.WriteAllText("Library/CityUpdateQA/integration.txt","RUNNING\n");
            File.WriteAllText(Report+"/gameplay.txt","RUNNING\n");
            EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
            SessionState.SetBool(Running,true);SessionState.SetInt(Phase,0);
            SessionState.SetString(Deadline,DateTime.UtcNow.AddMinutes(10).ToString("O"));EditorApplication.EnterPlaymode();
        }
        catch(Exception error){Debug.LogException(error);Finish(1,error.ToString());}
    }

    private static void Tick()
    {
        if(!Application.isBatchMode||!SessionState.GetBool(Running,false))return;
        try
        {
            if(DateTime.UtcNow>DateTime.Parse(SessionState.GetString(Deadline,""),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind))
                throw new TimeoutException("Residential gameplay acceptance timed out.");
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
                trip.StartCoroutine(Guard(ResidentialDrive(trip)));
            }
        }
        catch(Exception error){Debug.LogException(error);Finish(1,error.ToString());}
    }

    private static IEnumerator ResidentialDrive(CityTrip trip)
    {
        trip.Begin();float deadline=Time.realtimeSinceStartup+90;
        while((!trip.InCity||trip.Transitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
        if(!trip.InCity||trip.Transitioning)throw new InvalidOperationException("Residential drive departure failed.");
        var city=Object.FindObjectOfType<CityWorld>();
        if(city==null)throw new InvalidOperationException("Live City is missing.");
        yield return TownResidentialValidation.Check(city,TownResidentialPlan.Create());
        trip.Return();deadline=Time.realtimeSinceStartup+90;
        while((trip.InCity||trip.Transitioning)&&Time.realtimeSinceStartup<deadline)yield return null;
        if(trip.InCity||trip.Transitioning)throw new InvalidOperationException("Residential drive return failed.");
        Finish(0,"PASS original shop/customer/supplier/delivery/parking regressions plus residential physical access and real native pickup drives.\n"+
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
    [Serializable]private sealed class PlanReport{public BlockReport[] blocks;}
    [Serializable]private sealed class BlockReport{public string id,district,scheme;public Rect bounds;public int buildingCount,paths;public float footprint;public string[] families,variants;public float[] heights;public PlacementReport[] placements;}
    [Serializable]private sealed class PlacementReport{public string id,model;public Vector3 position;public float yaw;}
}
