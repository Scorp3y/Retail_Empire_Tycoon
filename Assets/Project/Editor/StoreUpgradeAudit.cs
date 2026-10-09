using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.Territory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StoreUpgradeAudit
{
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Project/Scenes/Game.unity");var output=new StringBuilder();
        foreach(var root in scene.GetRootGameObjects())
        {
            var renderers=root.GetComponentsInChildren<MeshRenderer>(true);output.AppendLine($"ROOT {root.name} position={root.transform.position} renderers={renderers.Length}");
            foreach(var child in root.transform.Cast<Transform>().Take(20)) output.AppendLine($"  {child.name} position={child.position} scale={child.lossyScale}");
        }
        foreach(var zone in Object.FindObjectsOfType<TerritoryZone>(true))
        {
            var visual=new SerializedObject(zone).FindProperty("_visual").objectReferenceValue;
            var line=visual!=null?new SerializedObject(visual).FindProperty("_border").objectReferenceValue as LineRenderer:null;
            if(line==null){output.AppendLine("ZONE "+zone.Id+" missing border");continue;}
            var points=new Vector3[line.positionCount];line.GetPositions(points);
            if(!line.useWorldSpace) points=points.Select(line.transform.TransformPoint).ToArray();
            output.AppendLine($"ZONE {zone.Id} price={zone.Price} position={zone.transform.position} points={string.Join(";",points.Select(p=>p.ToString()))}");
        }
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var area=prefab.GetComponentInChildren<StoreBuildArea>(true);if(area==null)continue;
            var instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            foreach(var box in instance.GetComponentInChildren<StoreBuildArea>(true).areaRects) output.AppendLine($"AREA {path} min={box.bounds.min} max={box.bounds.max}");
            Object.DestroyImmediate(instance);
        }
        Directory.CreateDirectory("Library/StoreUpgradeQA");File.WriteAllText("Library/StoreUpgradeQA/audit.txt",output.ToString());EditorApplication.Exit(0);
    }
}
