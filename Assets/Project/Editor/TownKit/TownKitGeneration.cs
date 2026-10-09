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
    /// <summary>Generates model assets only, in an isolated editor. Never loads or saves gameplay scenes.</summary>
    public static class TownKitGeneration
    {
        public const string Root="Assets/Art/TownKit";
        private const string Report="Library/TownKitQA";

        [Serializable] private sealed class Entry
        {
            public string id,category,name,prefab,obj,preview;
            public int triangles,vertices;
            public Vector3 size;
        }
        [Serializable] private sealed class Manifest
        {
            public string generator="RetailEmpireTycoon.TownKit.v1";
            public string units="1 Unity unit = 1 metre; Y up; front +Z; grounded pivot";
            public string scope="Visual assets only; no scenes, runtime logic, vehicle controllers or purchase catalogues changed.";
            public List<Entry> assets=new();
        }

        public static void GenerateBatch()
        {
            try
            {
                if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').EndsWith("/Library/CityUpdateQA/IsolatedProject/Assets",StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Generate only in the isolated QA project.");
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
            var definitions=TownAssetCatalogue.Create();
            if(definitions.Select(d=>d.Id).Distinct().Count()!=definitions.Count)throw new InvalidOperationException("Duplicate model ID.");
            if(definitions.Any(d=>d.Id.Contains("Terminal")||d.Id.Contains("SupplierDirection")))throw new InvalidOperationException("Excluded prop was reintroduced.");
            foreach(string directory in new[]{"Materials","Meshes","Models","Prefabs","Previews"})Directory.CreateDirectory(Root+"/"+directory);
            AssetDatabase.Refresh();var material=CreatePalette();TownModelExport.Material(Root+"/Models/TownKit.mtl");
            var manifest=new Manifest();var images=new List<Texture2D>();var categoryImages=new Dictionary<string,List<Texture2D>>();
            try
            {
                foreach(var definition in definitions)
                {
                    foreach(string directory in new[]{"Meshes","Models","Prefabs"})Directory.CreateDirectory(Root+"/"+directory+"/"+definition.Category);
                    var mesh=definition.Build().Build(definition.Id);
                    GroundMesh(mesh);ValidateMesh(mesh,definition.Id);
                    string meshPath=$"{Root}/Meshes/{definition.Category}/{definition.Id}.asset";
                    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(existing!=null)
                    {
                        // Mesh setters update the GPU buffers as well as persistent geometry.
                        // CopySerialized alone can leave cached prefab previews on the old shape.
                        existing.Clear();existing.indexFormat=mesh.indexFormat;
                        existing.vertices=mesh.vertices;existing.normals=mesh.normals;
                        existing.uv=mesh.uv;existing.triangles=mesh.triangles;existing.RecalculateBounds();
                        EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);mesh=existing;
                    }
                    else AssetDatabase.CreateAsset(mesh,meshPath);
                    var root=new GameObject(definition.Id,typeof(MeshFilter),typeof(MeshRenderer));GameObject prefab;
                    string prefabPath=$"{Root}/Prefabs/{definition.Category}/{definition.Id}.prefab";
                    try
                    {
                        root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterial=material;
                        prefab=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
                    }
                    finally {Object.DestroyImmediate(root);}
                    string objPath=$"{Root}/Models/{definition.Category}/{definition.Id}.obj";
                    TownModelExport.Obj(mesh,objPath);
                    var image=TownModelPreviews.Render(prefab);images.Add(image);
                    if(!categoryImages.TryGetValue(definition.Category,out var group))categoryImages.Add(definition.Category,group=new List<Texture2D>());
                    group.Add(image);
                    string previewPath=$"{Root}/Previews/{definition.Id}.png";File.WriteAllBytes(previewPath,image.EncodeToPNG());
                    manifest.assets.Add(new Entry {id=definition.Id,category=definition.Category,name=definition.DisplayName,
                        prefab=prefabPath.Substring(Root.Length+1),obj=objPath.Substring(Root.Length+1),preview=previewPath.Substring(Root.Length+1),
                        triangles=mesh.triangles.Length/3,vertices=mesh.vertexCount,size=mesh.bounds.size});
                    Debug.Log($"TownKit: {definition.Id}, {mesh.triangles.Length/3} triangles");
                }
                foreach(var group in categoryImages)TownModelPreviews.Sheet(group.Value,4,Root+"/Previews/"+group.Key+".png");
                TownModelPreviews.Sheet(images,8,Root+"/Previews/AllModels.png");
                File.WriteAllText(Root+"/manifest.json",JsonUtility.ToJson(manifest,true));
                WriteGallery(manifest);AssetDatabase.Refresh();ConfigureImports(manifest);AssetDatabase.SaveAssets();
                ValidateSavedAssets(manifest,material);
                File.WriteAllText(Report+"/result.txt",$"TOWN KIT PASSED: {manifest.assets.Count} models; prefab, mesh, OBJ and rendered preview for every entry.\n"+
                    $"Total triangles: {manifest.assets.Sum(a=>a.triangles)}. Largest model: {manifest.assets.Max(a=>a.triangles)} triangles. One renderer/material per prefab.\n"+
                    "No scene save, gameplay scripts, inventory/catalogue changes or player-save access.\n");
            }
            finally {foreach(var image in images)Object.DestroyImmediate(image);}
        }

        private static Material CreatePalette()
        {
            const string texturePath=Root+"/Materials/TownPalette.png",materialPath=Root+"/Materials/TownSurface.mat";
            var texture=new Texture2D(TownPalette.Colors.Length*8,8,TextureFormat.RGBA32,false);
            try
            {
                var pixels=new Color32[texture.width*texture.height];
                for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)pixels[y*texture.width+x]=TownPalette.Colors[x/8];
                texture.SetPixels32(pixels);texture.Apply();File.WriteAllBytes(texturePath,texture.EncodeToPNG());
            }
            finally {Object.DestroyImmediate(texture);}
            AssetDatabase.ImportAsset(texturePath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;
            importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null)
            {
                var shader=Shader.Find("Universal Render Pipeline/Lit");
                if(shader==null)throw new InvalidOperationException("The project's URP Lit shader is unavailable.");
                material=new Material(shader){name="TownSurface"};AssetDatabase.CreateAsset(material,materialPath);
            }
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",0);material.SetFloat("_Metallic",0);
            EditorUtility.SetDirty(material);return material;
        }

        private static void GroundMesh(Mesh mesh)
        {
            float bottom=mesh.bounds.min.y;if(Mathf.Abs(bottom)<.000001f)return;
            var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i].y-=bottom;
            mesh.vertices=vertices;mesh.RecalculateBounds();
        }

        private static void ValidateMesh(Mesh mesh,string id)
        {
            if(mesh.vertexCount==0||mesh.subMeshCount!=1||mesh.triangles.Length/3>12000)throw new InvalidOperationException("Invalid or excessive geometry: "+id);
            if(mesh.normals.Length!=mesh.vertexCount||mesh.uv.Length!=mesh.vertexCount)throw new InvalidOperationException("Missing normals/UV: "+id);
            if(Mathf.Abs(mesh.bounds.min.y)>.0001f)throw new InvalidOperationException("Ungrounded model: "+id);
            foreach(var vertex in mesh.vertices)if(float.IsNaN(vertex.x)||float.IsInfinity(vertex.x)||float.IsNaN(vertex.y)||float.IsInfinity(vertex.y)||float.IsNaN(vertex.z)||float.IsInfinity(vertex.z))throw new InvalidOperationException("Non-finite vertex: "+id);
            var vertices=mesh.vertices;var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3)
                if(Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]).sqrMagnitude<.000000000001f)throw new InvalidOperationException("Degenerate triangle: "+id);
        }

        private static void ConfigureImports(Manifest manifest)
        {
            foreach(var entry in manifest.assets)
            {
                var objImporter=(ModelImporter)AssetImporter.GetAtPath(Root+"/"+entry.obj);
                objImporter.materialImportMode=ModelImporterMaterialImportMode.None;objImporter.isReadable=false;objImporter.SaveAndReimport();
            }
        }

        private static void ValidateSavedAssets(Manifest manifest,Material material)
        {
            foreach(var entry in manifest.assets)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+entry.prefab);
                if(prefab==null||prefab.GetComponentsInChildren<Renderer>().Length!=1||prefab.GetComponent<MeshRenderer>().sharedMaterial!=material
                    ||prefab.GetComponentsInChildren<MonoBehaviour>(true).Length!=0||prefab.GetComponentsInChildren<Collider>(true).Length!=0)
                    throw new InvalidOperationException("Prefab is missing or contains gameplay components: "+entry.id);
                if(!File.Exists(Root+"/"+entry.obj)||!File.Exists(Root+"/"+entry.preview))throw new InvalidOperationException("Model deliverable is missing: "+entry.id);
                var savedMesh=prefab.GetComponent<MeshFilter>().sharedMesh;
                ValidateMesh(savedMesh,entry.id);
                if(savedMesh.triangles.Length/3!=entry.triangles||(savedMesh.bounds.size-entry.size).sqrMagnitude>.000001f)
                    throw new InvalidOperationException("Saved prefab geometry is stale: "+entry.id);
                var imported=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+entry.obj);
                if(imported==null||imported.GetComponentInChildren<MeshFilter>().sharedMesh.triangles.Length/3!=entry.triangles)
                    throw new InvalidOperationException("OBJ roundtrip changes geometry: "+entry.id);
            }
        }

        private static void WriteGallery(Manifest manifest)
        {
            var html=new StringBuilder("<!doctype html><html lang=\"ru\"><meta charset=\"utf-8\"><title>TownKit — реальные 3D-модели</title><style>body{margin:32px;background:#f4f2e9;color:#233e34;font:16px system-ui}section{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:16px}figure{margin:0;background:#fff;padding:12px}img{width:100%}small{display:block;color:#58685e}h2{margin-top:40px}</style><h1>TownKit — реальные 3D-модели</h1><p>Набор визуальных моделей. Масштаб: 1 единица = 1 метр. Передняя сторона +Z. На игровые сцены не добавлены.</p>");
            foreach(var group in manifest.assets.GroupBy(a=>a.category))
            {
                html.Append("<h2>").Append(group.Key).Append("</h2><section>");
                foreach(var item in group)html.Append($"<figure><img src=\"{item.preview}\" alt=\"{item.name}\"><figcaption>{item.name}<small>{item.id} · {item.triangles} треугольников · {item.size.x:0.##} × {item.size.y:0.##} × {item.size.z:0.##} м</small><a href=\"{item.obj}\">OBJ</a></figcaption></figure>");
                html.Append("</section>");
            }
            html.Append("</html>");File.WriteAllText(Root+"/index.html",html.ToString());
        }
    }
}
