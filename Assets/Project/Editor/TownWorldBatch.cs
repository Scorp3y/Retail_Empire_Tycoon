using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TownWorldBatch
{
    private const string Running="RetailEmpire.TownWorld.Running";
    private const string Started="RetailEmpire.TownWorld.Started";
    private const string Deadline="RetailEmpire.TownWorld.Deadline";
    private const string Regression="RetailEmpire.TownWorld.Regression";
    static TownWorldBatch(){EditorApplication.update+=Tick;}
    public static void Run() => Run(false);

    /// <summary>Stage-one acceptance: the saved world outside the pilot must remain identical.</summary>
    public static void RunPilot() => Run(true);

    private static void Run(bool preserveOutsidePilot)
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory("Library/TownWorldQA");
            File.WriteAllText("Library/TownWorldQA/integration.txt","RUNNING\n");
            GenerateAndValidate(TownhousePilotVariant.FiveBuildings,preserveOutsidePilot);
            EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
            SessionState.SetBool(Running,true);SessionState.SetBool(Started,false);
            SessionState.SetBool(Regression,false);
            SessionState.SetString(Deadline,DateTime.UtcNow.AddMinutes(8).ToString("O"));EditorApplication.EnterPlaymode();
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }

    internal static void GenerateAndValidate(TownhousePilotVariant variant,bool preserveOutsidePilot)
    {
            TownWorldSafety.RequireIsolatedEditor();
            Directory.CreateDirectory("Library/TownWorldQA");
            TownPilotScopePreservation.Snapshot protectedWorld=null;
            if(preserveOutsidePilot)
            {
                File.WriteAllText("Library/TownWorldQA/pilot-scope-preservation.txt","RUNNING\n");
                protectedWorld=TownPilotScopePreservation.Capture();
            }
            EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
            var parked=UnityEngine.Object.FindObjectOfType<RetailEmpireTycoon.City.CityTrip>(true).parkedPickup;
            var oldBounds=CityUpdateSetup.Measure(parked);
            var starter=AssetDatabase.LoadAssetAtPath<RetailEmpireTycoon.BuildSystem.StarterStoreBlueprint>(StarterStoreSetup.BlueprintPath);
            float expectedShopPickupLength=Mathf.Max(oldBounds.size.x,oldBounds.size.z)*(starter.proportionsVersion==1?1.3f:1);
            string previousFarm=TownFarmPreservation.Capture();
            var previousSuppliers=TownSupplierPreservation.Capture();
            TownWorldSetup.Generate(variant);
            if(protectedWorld!=null)
            {
                var currentWorld=TownPilotScopePreservation.Capture();
                protectedWorld.RequireUnchanged(currentWorld);
                File.WriteAllText("Library/TownWorldQA/pilot-scope-preservation.txt",
                    "PASS unchanged saved geometry, materials and colliders outside the pilot plus two-metre pavement seams\n"+
                    "Before: "+protectedWorld+"\nAfter: "+currentWorld+"\n");
            }
            if(TownFarmPreservation.Capture()!=previousFarm)throw new InvalidOperationException("Farm geometry or collection route changed during city relocation.");
            File.WriteAllText("Library/TownWorldQA/farm-preservation.txt","PASS unchanged farm geometry, colliders, collection checkpoint and yard route\n"+previousFarm);
            var suppliers=TownSupplierPreservation.Capture();
            foreach(var previous in previousSuppliers)
                if(!suppliers.TryGetValue(previous.Key,out var current)||current!=previous.Value)
                    throw new InvalidOperationException("Protected supplier geometry, checkpoint or access changed: "+previous.Key);
            File.WriteAllText("Library/TownWorldQA/supplier-preservation.txt","PASS all six urban supplier/showroom plots, geometry, colliders, checkpoints and access routes unchanged\n"+
                string.Join("\n",previousSuppliers));
            TownWorldValidation.ValidateAssets(expectedShopPickupLength);
    }
    private static void Tick()
    {
        if(!Application.isBatchMode||!SessionState.GetBool(Running,false))return;
        try
        {
            if(DateTime.UtcNow>DateTime.Parse(SessionState.GetString(Deadline,"")))throw new TimeoutException("Town world QA timed out.");
            if(!EditorApplication.isPlaying)return;
            if(!SessionState.GetBool(Started,false))
            {
                if(Time.frameCount<8)return;
                SessionState.SetBool(Started,true);TownWorldValidation.Run();return;
            }
            if(SessionState.GetBool(Regression,false))
            {
                var regression=File.ReadAllText("Library/CityUpdateQA/integration.txt");
                if(regression.Contains("CITY DELIVERY INTEGRATION PASSED"))Finish(0);
                else if(regression.Contains("FAILED"))Finish(1);
                return;
            }
            var result=File.ReadAllText("Library/TownWorldQA/integration.txt");
            if(result.StartsWith("TOWN WORLD PASSED"))
            {
                SessionState.SetBool(Regression,true);CityUpdateValidation.Run();
            }
            else if(result.StartsWith("FAILED"))Finish(1);
        }
        catch(Exception error){Debug.LogException(error);Finish(1);}
    }
    private static void Finish(int code){SessionState.SetBool(Running,false);EditorApplication.Exit(code);}
}
