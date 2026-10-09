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
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory("Library/TownWorldQA");
            File.WriteAllText("Library/TownWorldQA/integration.txt","RUNNING\n");
            TownWorldSetup.Generate();TownWorldValidation.ValidateAssets();
            EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);ShopGameplaySandbox.Open();
            SessionState.SetBool(Running,true);SessionState.SetBool(Started,false);
            SessionState.SetBool(Regression,false);
            SessionState.SetString(Deadline,DateTime.UtcNow.AddMinutes(8).ToString("O"));EditorApplication.EnterPlaymode();
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
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
