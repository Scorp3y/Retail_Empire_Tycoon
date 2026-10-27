using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Captures the authored urban shelter without running or saving gameplay.</summary>
public static class MobilityStopReview
{
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            var scene = EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            var stop = Object.FindObjectsOfType<Transform>(true)
                .Single(item => item.name == "05_BusStop");
            Directory.CreateDirectory("Docs/MobilityUpdate");
            TownFloristBatch.Render(scene, "Docs/MobilityUpdate/UrbanBusStop.png",
                stop.position + new Vector3(-10, 9, -12), stop.position + Vector3.up);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
