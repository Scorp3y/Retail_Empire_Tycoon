using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Imports and measures the style study in the disposable QA project; never saves the city scene.</summary>
public static class PinebrookKitReview
{
    const string Art="Assets/Art/PinebrookStudy";
    const string Output="ArtSource/Pinebrook/Review";
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Output);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var atlasPath=Art+"/PinebrookPalette.png";
            var atlasImporter=(TextureImporter)AssetImporter.GetAtPath(atlasPath);
            atlasImporter.mipmapEnabled=false;atlasImporter.filterMode=FilterMode.Point;atlasImporter.textureCompression=TextureImporterCompression.Uncompressed;atlasImporter.SaveAndReimport();
            var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Shared.mat");
            if(material==null){material=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/TownForestQuarter/Materials/Surface.mat")){name="Pinebrook Study Shared",enableInstancing=true};AssetDatabase.CreateAsset(material,Art+"/Shared.mat");}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath));material.SetFloat("_Smoothness",.15f);material.SetFloat("_PaletteIndex",-1);
            var measurements=new System.Collections.Generic.List<string>();
            foreach(var file in Directory.GetFiles(Art,"*.fbx"))
            {
                var path=file.Replace('\\','/');var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale=1;importer.bakeAxisConversion=true;importer.importCameras=false;importer.importLights=false;importer.SaveAndReimport();
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var instance=Object.Instantiate(model);
                var renderers=instance.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;
                foreach(var renderer in renderers){bounds.Encapsulate(renderer.bounds);renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();}
                int triangles=instance.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
                if(instance.transform.localScale!=Vector3.one||Quaternion.Angle(instance.transform.rotation,Quaternion.identity)>.01f)throw new InvalidOperationException("Unexpected root transform: "+model.name);
                if(Mathf.Abs(bounds.min.y)>.05f)throw new InvalidOperationException("Bottom pivot is not seated: "+model.name+" "+bounds.min);
                if(model.name=="Pinebrook_Hero_LOD0"&&(triangles>8000||bounds.size.x<22||bounds.size.x>24||bounds.size.y<11||bounds.size.y>14))throw new InvalidOperationException("Hero scale/budget failed: "+bounds.size+" / "+triangles);
                measurements.Add(model.name+" | "+triangles+" triangles | size XYZ m "+bounds.size.ToString("F3")+" | bottom Y "+bounds.min.y.ToString("F4"));Object.DestroyImmediate(instance);
            }
            File.WriteAllLines(Output+"/ImportValidation.txt",measurements);
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            var cafe=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownForestQuarter/Prefabs/CafeCorner.prefab"));cafe.transform.position=new Vector3(1284,.12f,0);
            var hero=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Pinebrook_Hero_LOD0.fbx"));hero.transform.position=new Vector3(1316,.12f,0);
            foreach(var renderer in hero.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Temporary study floor";ground.transform.position=new Vector3(1300,-.16f,0);ground.transform.localScale=new Vector3(110,.3f,70);var floorMaterial=new Material(material);floorMaterial.SetTexture("_BaseMap",Texture2D.whiteTexture);floorMaterial.SetColor("_BaseColor",new Color(.58f,.65f,.46f));ground.GetComponent<MeshRenderer>().sharedMaterial=floorMaterial;
            TownFloristBatch.Render(scene,Output+"/UnityComparison.png",new Vector3(1340,27,-58),new Vector3(1300,5,0),23);
            File.WriteAllText(Output+"/Result.txt","PASS: all FBX models imported at unit scale with identity root rotation, bottom-centred vertical origins; triangle counts checked. City lighting reused; scene not saved.\n");
            Debug.Log("PINEBROOK IMPORT REVIEW PASS");EditorApplication.Exit(0);
        }
        catch(Exception error){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/Result.txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
