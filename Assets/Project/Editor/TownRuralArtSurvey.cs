using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Read-only inventory of available source meshes before the local art replacement.</summary>
public static class TownRuralArtSurvey
{
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory("Library/TownRuralSurvey");
            var paths=new[]{"Assets/Models/Car/Models/FBX format/tractor.fbx","Assets/Models/lowpoly models/Pandazole Farm Ranch Pack/Prefabs/Prop_Wheelbarrow.prefab","Assets/Models/lowpoly models/Pandazole Farm Ranch Pack/Prefabs/Prop_BigFarmingTool_01.prefab","Assets/Models/lowpoly models/Pandazole Farm Ranch Pack/Prefabs/food_Carrot.prefab","Assets/Models/lowpoly models/Pandazole Farm Ranch Pack/Prefabs/Env_Wheat.prefab"};
            var lines=paths.SelectMany(path=>
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(source==null)throw new InvalidOperationException("Missing "+path);
                return new[]{path}.Concat(source.GetComponentsInChildren<MeshFilter>(true).Select(f=>"MESH "+f.name+" "+f.sharedMesh.bounds+" matrix="+f.transform.localToWorldMatrix+" vertices="+f.sharedMesh.vertexCount))
                    .Concat(source.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials.Select(m=>"MAT "+m.name+" shader="+m.shader.name+" texture="+AssetDatabase.GetAssetPath(m.mainTexture))));
            });File.WriteAllLines("Library/TownRuralSurvey/assets.txt",lines);
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
