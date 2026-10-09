using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Runs migrations/tests only in the disposable local QA clone.</summary>
[InitializeOnLoad]
public static class StoreUpgradeBatch
{
    private const string State="RetailEmpire.StoreUpgradeQA.Stage";
    private const string Deadline="RetailEmpire.StoreUpgradeQA.Deadline";
    private const string Starter="RetailEmpire.StoreUpgradeQA.Starter";
    static StoreUpgradeBatch(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        RunMode(false);
    }
    public static void RunCorrections()
    {
        RunMode(true);
    }
    public static void RunStarter()
    {
        SessionState.SetBool(Starter,true);
        RunMode(true);
    }
    private static void RunMode(bool correctionsOnly)
    {
        try
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Library/CityUpdateQA/IsolatedProject/Assets",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use only the isolated Unity QA project.");
            Directory.CreateDirectory("Library/StoreUpgradeQA");
            File.Delete("Library/StoreUpgradeQA/integration.txt");File.Delete("Library/CityUpdateQA/integration.txt");
            EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
            if (correctionsOnly) StoreUpgradeSetup.ApplyCatalogueCorrections();
            else StoreUpgradeSetup.Apply();
            if(SessionState.GetBool(Starter,false)) {File.Delete("Library/StoreUpgradeQA/starter.txt");StarterStoreSetup.Apply();}
            CityUpdateValidation.ValidateAssets();
            StoreUpgradeValidation.ValidateAssets();
            ShopGameplaySandbox.Open();
            SessionState.SetInt(State,1);SessionState.SetString(Deadline,DateTime.UtcNow.AddMinutes(5).ToString("O"));
            EditorApplication.EnterPlaymode();
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static void Tick()
    {
        int stage=SessionState.GetInt(State,0);
        if(stage==0 || !Application.isBatchMode)return;
        try
        {
            if(DateTime.UtcNow>DateTime.Parse(SessionState.GetString(Deadline,"")))throw new TimeoutException("Store QA timed out.");
            if(!EditorApplication.isPlaying || Time.frameCount<8)return;
            if(stage==1){SessionState.SetInt(State,2);StoreUpgradeValidation.Run();return;}
            if(stage==2)
            {
                string path="Library/StoreUpgradeQA/integration.txt";
                if(!File.Exists(path))return;
                string report=File.ReadAllText(path);
                if(report.Contains("FAILED:")){Finish(1);return;}
                if(!report.Contains("STORE UPGRADE PASSED"))return;
                SessionState.SetInt(State,3);CityUpdateValidation.Run();return;
            }
            if(stage==4)
            {
                string starterPath="Library/StoreUpgradeQA/starter.txt";
                if(!File.Exists(starterPath))return;
                string starterReport=File.ReadAllText(starterPath);
                if(starterReport.Contains("FAILED:"))Finish(1);
                else if(starterReport.Contains("STARTER STORE PASSED"))
                {
                    File.Delete("Library/CityUpdateQA/integration.txt");
                    SessionState.SetInt(State,5);CityUpdateValidation.Run();
                }
                return;
            }
            string cityPath="Library/CityUpdateQA/integration.txt";
            if(!File.Exists(cityPath))return;
            string cityReport=File.ReadAllText(cityPath);
            if(cityReport.Contains("FAILED:"))Finish(1);
            else if(cityReport.Contains("CITY DELIVERY INTEGRATION PASSED"))
            {
                if(stage==3&&SessionState.GetBool(Starter,false)){SessionState.SetInt(State,4);StarterStoreValidation.Run();}
                else Finish(0);
            }
        }
        catch(Exception error){Debug.LogException(error);Finish(1);}
    }
    private static void Finish(int code){SessionState.SetInt(State,0);EditorApplication.Exit(code);}
}
