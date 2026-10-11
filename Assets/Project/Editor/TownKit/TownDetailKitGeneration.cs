using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>Asset-only first stage. Placement is a separate, explicitly invoked world build.</summary>
    public static class TownDetailKitGeneration
    {
        public const string Root="Assets/Art/TownDetails";
        private const string Report="Library/TownDetailsQA";
        [Serializable] private sealed class Entry
        {
            public string id,label,category,prefab,obj,preview;
            public int triangles;
            public Vector3 size;
        }
        [Serializable] private sealed class Manifest
        {
            public string units="Metres; Y up; front +Z; grounded pivot. Visual assets, not gameplay services.";
            public List<Entry> assets=new List<Entry>();
        }

        public static void GenerateBatch()
        {
            try
            {
                if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').EndsWith("/Library/CityUpdateQA/IsolatedProject/Assets",StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Detail generation requires the isolated QA project.");
                Generate();EditorApplication.Exit(0);
            }
            catch(Exception error)
            {
                Directory.CreateDirectory(Report);File.WriteAllText(Report+"/result.txt","FAILED: "+error);
                Debug.LogException(error);EditorApplication.Exit(1);
            }
        }

        private static void Generate()
        {
            Directory.CreateDirectory(Report);
            foreach(string folder in new[]{"Materials","Meshes","Models","Prefabs","Previews"})Directory.CreateDirectory(Root+"/"+folder);
            AssetDatabase.Refresh();
            const string texture=Root+"/Materials/TownPalette.png";
            if(!File.Exists(texture)&&!AssetDatabase.CopyAsset(TownKitGeneration.Root+"/Materials/TownPalette.png",texture))
                throw new InvalidOperationException("Cannot export the shared palette texture.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(TownKitGeneration.Root+"/Materials/TownSurface.mat");
            if(material==null)throw new InvalidOperationException("Shared low-poly material is missing.");
            TownModelExport.Material(Root+"/Models/TownKit.mtl");
            var definitions=TownDetailModels.Create();
            if(definitions.Select(d=>d.Id).Distinct().Count()!=definitions.Count)throw new InvalidOperationException("Duplicate detail model ID.");
            var manifest=new Manifest();var images=new List<Texture2D>();var groups=new Dictionary<string,List<Texture2D>>();
            try
            {
                foreach(var definition in definitions)
                {
                    foreach(string folder in new[]{"Meshes","Models","Prefabs"})Directory.CreateDirectory(Root+"/"+folder+"/"+definition.Category);
                    var mesh=definition.Build().Build(definition.Id);
                    var vertices=mesh.vertices;float bottom=mesh.bounds.min.y;
                    for(int i=0;i<vertices.Length;i++)vertices[i].y-=bottom;
                    mesh.vertices=vertices;mesh.RecalculateBounds();Validate(mesh,definition.Id);
                    string meshPath=$"{Root}/Meshes/{definition.Category}/{definition.Id}.asset";
                    var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(saved==null)AssetDatabase.CreateAsset(mesh,meshPath);
                    else
                    {
                        saved.Clear();saved.indexFormat=mesh.indexFormat;saved.vertices=mesh.vertices;saved.normals=mesh.normals;
                        saved.uv=mesh.uv;saved.triangles=mesh.triangles;saved.RecalculateBounds();EditorUtility.SetDirty(saved);
                        Object.DestroyImmediate(mesh);mesh=saved;
                    }
                    string prefabPath=$"{Root}/Prefabs/{definition.Category}/{definition.Id}.prefab";
                    var instance=new GameObject(definition.Id,typeof(MeshFilter),typeof(MeshRenderer));GameObject prefab;
                    try
                    {
                        instance.GetComponent<MeshFilter>().sharedMesh=mesh;instance.GetComponent<MeshRenderer>().sharedMaterial=material;
                        prefab=PrefabUtility.SaveAsPrefabAsset(instance,prefabPath);
                    }
                    finally {Object.DestroyImmediate(instance);}
                    string objPath=$"{Root}/Models/{definition.Category}/{definition.Id}.obj";
                    TownModelExport.Obj(mesh,objPath);
                    var preview=TownModelPreviews.Render(prefab);images.Add(preview);
                    if(!groups.TryGetValue(definition.Category,out var group))groups.Add(definition.Category,group=new List<Texture2D>());
                    group.Add(preview);
                    string previewPath=Root+"/Previews/"+definition.Id+".png";File.WriteAllBytes(previewPath,preview.EncodeToPNG());
                    if(definition.Category=="Buildings")
                    {
                        var rear=TownModelPreviews.Render(prefab,180);images.Add(rear);
                        if(!groups.TryGetValue("BuildingsRear",out var rearGroup))groups.Add("BuildingsRear",rearGroup=new List<Texture2D>());
                        rearGroup.Add(rear);File.WriteAllBytes(Root+"/Previews/"+definition.Id+"_Rear.png",rear.EncodeToPNG());
                    }
                    manifest.assets.Add(new Entry{id=definition.Id,label=definition.DisplayName,category=definition.Category,
                        triangles=mesh.triangles.Length/3,size=mesh.bounds.size,prefab=prefabPath.Substring(Root.Length+1),
                        obj=objPath.Substring(Root.Length+1),preview=previewPath.Substring(Root.Length+1)});
                }
                foreach(var group in groups)TownModelPreviews.Sheet(group.Value,4,Root+"/Previews/"+group.Key+".png");
                TownModelPreviews.Sheet(images,6,Root+"/Previews/AllModels.png");
                File.WriteAllText(Root+"/manifest.json",JsonUtility.ToJson(manifest,true));WriteGallery(manifest);
                AssetDatabase.Refresh();AssetDatabase.SaveAssets();
                foreach(var entry in manifest.assets)
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+entry.prefab);
                    Validate(prefab.GetComponent<MeshFilter>().sharedMesh,entry.id);
                    if(prefab.GetComponentsInChildren<Renderer>().Length!=1||prefab.GetComponentsInChildren<Collider>().Length!=0
                        ||prefab.GetComponentsInChildren<MonoBehaviour>().Length!=0)throw new InvalidOperationException("Detail prefab is not purely visual: "+entry.id);
                    var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/"+entry.obj);
                    importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
                    var imported=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+entry.obj);
                    if(imported==null||imported.GetComponentInChildren<MeshFilter>().sharedMesh.triangles.Length/3!=entry.triangles)
                        throw new InvalidOperationException("OBJ round-trip mismatch: "+entry.id);
                }
                File.WriteAllText(Report+"/result.txt",$"TOWN DETAILS PASSED: {manifest.assets.Count} grounded models; {manifest.assets.Sum(a=>a.triangles)} triangles total.\n"+
                    "Every prefab has one mesh/material; finite vertices, normals, UV, OBJ round-trip and non-empty 3D render verified.\nAsset generation only: no scene or supplier changes.\n");
            }
            finally {foreach(var image in images)Object.DestroyImmediate(image);}
        }

        private static void Validate(Mesh mesh,string id)
        {
            if(mesh==null||mesh.vertexCount==0||mesh.triangles.Length/3>12000||mesh.subMeshCount!=1
                ||mesh.normals.Length!=mesh.vertexCount||mesh.uv.Length!=mesh.vertexCount||Mathf.Abs(mesh.bounds.min.y)>.0001f)
                throw new InvalidOperationException("Invalid detail mesh: "+id);
            foreach(var vertex in mesh.vertices)
                if(float.IsNaN(vertex.x)||float.IsInfinity(vertex.x)||float.IsNaN(vertex.y)||float.IsInfinity(vertex.y)||float.IsNaN(vertex.z)||float.IsInfinity(vertex.z))
                    throw new InvalidOperationException("Non-finite detail vertex: "+id);
            var vertices=mesh.vertices;var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3)
                if(Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]).sqrMagnitude<1e-12f)
                    throw new InvalidOperationException("Degenerate detail triangle: "+id);
        }

        private static void WriteGallery(Manifest manifest)
        {
            var html=new StringBuilder("<!doctype html><html lang=\"ru\"><meta charset=\"utf-8\"><title>Городские модели</title><style>body{margin:32px;font:16px system-ui;background:#f4f2e9;color:#233e34}section{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:16px}figure{margin:0;background:white;padding:12px}img{width:100%}small{display:block}</style><h1>Объекты для живого города</h1><p>Настоящие 3D-модели Unity и OBJ. Единицы — метры. Пункты выдачи в этот набор не входят.</p>");
            foreach(var group in manifest.assets.GroupBy(a=>a.category))
            {
                html.Append("<h2>").Append(group.Key).Append("</h2><section>");
                foreach(var item in group)html.Append($"<figure><img src=\"{item.preview}\" alt=\"{item.label}\"><figcaption>{item.label}<small>{item.triangles} треугольников; {item.size.x:0.##} × {item.size.y:0.##} × {item.size.z:0.##} м</small><a href=\"{item.obj}\">OBJ</a></figcaption></figure>");
                html.Append("</section>");
            }
            File.WriteAllText(Root+"/index.html",html.Append("</html>").ToString());
        }
    }
}
