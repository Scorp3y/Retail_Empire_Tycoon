using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch QA entry point restricted to a disposable project clone, never the live player project.</summary>
[InitializeOnLoad]
public static class CityUpdateBatch
{
    private const string Running="RetailEmpire.CityQa.Running";
    private const string Started="RetailEmpire.CityQa.Started";
    private const string Deadline="RetailEmpire.CityQa.Deadline";
    static CityUpdateBatch() {EditorApplication.update+=Tick;}
    public static void Run()
    {
        try
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').EndsWith("/Library/CityUpdateQA/IsolatedProject/Assets",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Batch QA is allowed only in Library/CityUpdateQA/IsolatedProject.");
            EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);CityUpdateSetup.Apply();CityUpdateValidation.ValidateAssets();
            ShopGameplaySandbox.Open();
            SessionState.SetBool(Running,true);SessionState.SetBool(Started,false);
            SessionState.SetString(Deadline,DateTime.UtcNow.AddMinutes(4).ToString("O"));
            EditorApplication.EnterPlaymode();
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Running,false)||!Application.isBatchMode) return;
        try
        {
            if(DateTime.UtcNow>DateTime.Parse(SessionState.GetString(Deadline,""))) throw new TimeoutException("City QA timed out.");
            if(!EditorApplication.isPlaying) return;
            if(!SessionState.GetBool(Started,false))
            {
                if(Time.frameCount<8) return;
                SessionState.SetBool(Started,true);CityUpdateValidation.Run();return;
            }
            const string report="Library/CityUpdateQA/integration.txt";
            if(!File.Exists(report)) return;
            string text=File.ReadAllText(report);
            if(text.Contains("CITY DELIVERY INTEGRATION PASSED")) Finish(0);
            else if(text.Contains("FAILED:")) Finish(1);
        }
        catch(Exception error){Debug.LogException(error);Finish(1);}
    }
    private static void Finish(int code)
    {
        SessionState.SetBool(Running,false);EditorApplication.Exit(code);
    }
}
