using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TownWorldSurvey
{
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            var scene=EditorSceneManager.OpenScene("Assets/Project/Scenes/Game.unity");
            var report=new StringBuilder();
            foreach(var root in scene.GetRootGameObjects())
            {
                report.AppendLine($"ROOT {root.name} pos={root.transform.position} scale={root.transform.lossyScale} renderers={root.GetComponentsInChildren<Renderer>(true).Length}");
                if(root.name!="World")continue;
                foreach(Transform child in root.transform)
                {
                    var renderers=child.GetComponentsInChildren<Renderer>(true);
                    var bounds=renderers.Length>0?renderers[0].bounds:new Bounds(child.position,Vector3.zero);
                    foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
                    report.AppendLine($"  {child.name}: pos={child.position}, bounds={bounds}, scripts={string.Join(",",child.GetComponentsInChildren<MonoBehaviour>(true).Where(s=>s!=null).Select(s=>s.GetType().Name).Distinct())}");
                }
            }
            Directory.CreateDirectory("Library/TownWorldQA");
            File.WriteAllText("Library/TownWorldQA/survey.txt",report.ToString());
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
}

internal static class TownWorldSafety
{
    public static void RequireIsolatedEditor()
    {
        if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').EndsWith("/Library/CityUpdateQA/IsolatedProject/Assets",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("World generation and QA require the isolated Unity project.");
    }
}
