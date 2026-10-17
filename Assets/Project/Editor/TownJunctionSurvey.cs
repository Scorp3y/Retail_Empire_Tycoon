using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Inventories saved road modules and legacy equipment, without rebuilding the town.</summary>
public static class TownJunctionSurvey
{
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            Directory.CreateDirectory("Library/TownJunctionQA");
            var town=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var names=new[]{"RoadCross","RoadTee","TrafficSignal","YieldSign"};
                File.WriteAllLines("Library/TownJunctionQA/survey.txt",town.GetComponentsInChildren<Transform>(true)
                    .Where(t=>names.Contains(t.name)).OrderBy(t=>t.position.z).ThenBy(t=>t.position.x)
                    .Select(t=>t.name+" | "+t.position.ToString("F3")+" | yaw="+t.eulerAngles.y+" | scale="+t.lossyScale+" | parent="+t.parent.name));
            }
            finally{PrefabUtility.UnloadPrefabContents(town);}
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
