using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using RetailEmpireTycoon.Editor.TownKit;
using Object=UnityEngine.Object;

namespace RetailEmpireTycoon.Editor.ConceptTownKit
{
    /// <summary>Builds only draft assets in the isolated editor; never saves or modifies a gameplay scene.</summary>
    public static class ConceptGeneration
    {
        public const string Root="Assets/Art/ConceptTownKit";
        private const string Report="Library/ConceptTownKitQA";

        [Serializable] private sealed class Entry
        {
            public string id,name,prefab,obj,preview,rearPreview;
            public int triangles,vertices,materials;
            public Vector3 size;
        }
        [Serializable] private sealed class Manifest
        {
            public string generator="RetailEmpireTycoon.ConceptTownKit.v1";
            public string units="metres; Y up; facade +Z; grounded origin";
            public string scope="12 static visual asset groups, staged outside main Assets. No map placement, colliders, interiors or gameplay.";
            public List<Entry> assets=new List<Entry>();
        }

        public static void GenerateBatch()
        {
            try
            {
                if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').EndsWith("/Library/CityUpdateQA/IsolatedProject/Assets",StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Concept generation requires the isolated QA project.");
                Generate();EditorApplication.Exit(0);
            }
            catch(Exception error)
            {
                Directory.CreateDirectory(Report);File.WriteAllText(Report+"/result.txt","FAILED: "+error);
                Debug.LogException(error);EditorApplication.Exit(1);
            }
        }

        private static ConceptDefinition[] Catalogue()=>new[]
        {
            new ConceptDefinition("01_Apartment","Жилой дом",ConceptBuildings.Apartment),
            new ConceptDefinition("02_RetailRow","Торговый ряд",ConceptBuildings.RetailRow),
            new ConceptDefinition("03_Cafe","Кафе",ConceptBuildings.Cafe),
            new ConceptDefinition("04_FuelStation","Заправка",ConceptInfrastructure.FuelStation),
            new ConceptDefinition("05_BusStop","Остановка",ConceptInfrastructure.BusStop),
            new ConceptDefinition("06_Warehouse","Городской склад",ConceptBuildings.Warehouse),
            new ConceptDefinition("07_ConstructionSite","Стройплощадка",ConceptInfrastructure.ConstructionSite),
            new ConceptDefinition("08_RailwayStation","Железнодорожная станция",ConceptInfrastructure.RailwayStation),
            new ConceptDefinition("09_Bridge","Мост",ConceptInfrastructure.Bridge),
            new ConceptDefinition("10_Farmstead","Ферма: дом и амбар",ConceptFarm.Farmstead),
            new ConceptDefinition("11_Greenhouse","Теплица",ConceptFarm.Greenhouse),
            new ConceptDefinition("12_SilosAndGarage","Силос и гараж",ConceptFarm.SilosAndGarage)
        };

        private static void Generate()
        {
            Directory.CreateDirectory(Report);
            foreach(string folder in new[]{"Materials","Meshes","Models","Models/Objects","Prefabs","Previews"})
                Directory.CreateDirectory(Root+"/"+folder);
            AssetDatabase.Refresh();
            var materials=CreateMaterials();var manifest=new Manifest();var images=new List<Texture2D>();
            WriteMtl();
            var catalogue=Catalogue();
            if(catalogue.Length!=12||catalogue.Select(d=>d.Id).Distinct().Count()!=12)
                throw new InvalidOperationException("The approved catalogue must contain 12 distinct models.");
            try
            {
                foreach(var definition in catalogue)
                {
                    var geometry=definition.Build();var mesh=BuildMesh(geometry,definition.Id);
                    ValidateMesh(mesh,definition.Id);
                    string meshPath=Root+"/Meshes/"+definition.Id+".asset";
                    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(existing!=null)
                    {
                        existing.Clear();existing.indexFormat=mesh.indexFormat;existing.vertices=mesh.vertices;
                        existing.normals=mesh.normals;existing.uv=mesh.uv;existing.subMeshCount=mesh.subMeshCount;
                        for(int sub=0;sub<mesh.subMeshCount;sub++)existing.SetTriangles(mesh.GetTriangles(sub),sub);
                        existing.RecalculateBounds();EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);mesh=existing;
                    }
                    else AssetDatabase.CreateAsset(mesh,meshPath);
                    var root=new GameObject(definition.Id,typeof(MeshFilter),typeof(MeshRenderer));GameObject prefab;
                    string prefabPath=Root+"/Prefabs/"+definition.Id+".prefab";
                    try
                    {
                        root.GetComponent<MeshFilter>().sharedMesh=mesh;
                        root.GetComponent<MeshRenderer>().sharedMaterials=geometry.HasGlass?materials:new[]{materials[0]};
                        prefab=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
                    }
                    finally {Object.DestroyImmediate(root);}
                    string objPath=Root+"/Models/Objects/"+definition.Id+".obj";WriteObj(mesh,objPath);
                    var front=ConceptPreviews.Render(prefab,false);images.Add(front);
                    string previewPath=Root+"/Previews/"+definition.Id+".png";
                    File.WriteAllBytes(previewPath,front.EncodeToPNG());
                    string rearPath=Root+"/Previews/"+definition.Id+"_Rear.png";
                    var rear=ConceptPreviews.Render(prefab,true);
                    try {File.WriteAllBytes(rearPath,rear.EncodeToPNG());}
                    finally {Object.DestroyImmediate(rear);}
                    manifest.assets.Add(new Entry {id=definition.Id,name=definition.Name,prefab=Relative(prefabPath),obj=Relative(objPath),
                        preview=Relative(previewPath),rearPreview=Relative(rearPath),triangles=mesh.triangles.Length/3,
                        vertices=mesh.vertexCount,materials=mesh.subMeshCount,size=mesh.bounds.size});
                    Debug.Log("ConceptTownKit: "+definition.Id+" / "+mesh.triangles.Length/3+" triangles");
                }
                ConceptPreviews.Sheet(images,Root+"/Previews/AllModels.png");
                File.WriteAllText(Root+"/manifest.json",JsonUtility.ToJson(manifest,true));
                WriteGallery(manifest);AssetDatabase.Refresh();
                foreach(var entry in manifest.assets)
                {
                    var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/"+entry.obj);
                    importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
                }
                AssetDatabase.SaveAssets();ValidateSavedAssets(manifest);
                File.WriteAllText(Report+"/result.txt","CONCEPT KIT PASSED: 12 models, 24 Unity views, 12 OBJ roundtrips.\n"+
                    "Total triangles: "+manifest.assets.Sum(a=>a.triangles)+"; largest: "+manifest.assets.Max(a=>a.triangles)+".\n"+
                    "Finite geometry, normals, palette UV, grounded pivots, material slots and all saved references verified.\n"+
                    "No gameplay scene save, map placement or player-save access.\n");
                AssetDatabase.ExportPackage(Root,Report+"/ConceptTownKit.unitypackage",ExportPackageOptions.Recurse);
            }
            finally {foreach(var image in images)Object.DestroyImmediate(image);}
        }

        private static string Relative(string path)=>path.Substring(Root.Length+1);

        private static Mesh BuildMesh(ConceptGeometry geometry,string id)
        {
            var solid=geometry.Solid.Build(id);
            Mesh mesh=solid;
            if(geometry.HasGlass)
            {
                var glass=geometry.Glass.Build(id+"_Glass");
                try
                {
                    mesh=new Mesh {name=id,indexFormat=IndexFormat.UInt32};
                    mesh.CombineMeshes(new[]{new CombineInstance{mesh=solid,transform=Matrix4x4.identity},
                        new CombineInstance{mesh=glass,transform=Matrix4x4.identity}},false,true);
                }
                finally {Object.DestroyImmediate(solid);Object.DestroyImmediate(glass);}
            }
            mesh.RecalculateBounds();float bottom=mesh.bounds.min.y;
            var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i].y-=bottom;
            mesh.vertices=vertices;mesh.RecalculateBounds();return mesh;
        }

        private static Material[] CreateMaterials()
        {
            var colors=(Color32[])TownPalette.Colors.Clone();
            colors[(int)TownColor.Mint]=new Color32(50,157,157,255);
            colors[(int)TownColor.Cream]=new Color32(236,222,192,255);
            colors[(int)TownColor.Pavement]=new Color32(194,190,176,255);
            colors[(int)TownColor.GreenStripe]=new Color32(44,126,125,255);
            colors[(int)TownColor.Red]=new Color32(168,67,58,255);
            colors[(int)TownColor.Glass]=new Color32(154,194,201,255);
            colors[(int)TownColor.Metal]=new Color32(157,164,161,255);
            colors[(int)TownColor.Yellow]=new Color32(233,182,64,255);
            string texturePath=Root+"/Materials/ConceptPalette.png";
            var texture=new Texture2D(colors.Length*8,8,TextureFormat.RGBA32,false);
            try
            {
                var pixels=new Color32[texture.width*texture.height];
                for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)pixels[y*texture.width+x]=colors[x/8];
                texture.SetPixels32(pixels);texture.Apply();File.WriteAllBytes(texturePath,texture.EncodeToPNG());
            }
            finally {Object.DestroyImmediate(texture);}
            AssetDatabase.ImportAsset(texturePath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)throw new InvalidOperationException("URP/Lit is missing.");
            var palette=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var solid=CreateMaterial(shader,"ConceptSurface");solid.SetTexture("_BaseMap",palette);
            solid.SetColor("_BaseColor",Color.white);solid.SetFloat("_Smoothness",.12f);
            var glass=CreateMaterial(shader,"ConceptGlass");glass.SetTexture("_BaseMap",palette);
            glass.SetColor("_BaseColor",new Color(1,1,1,.2f));glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);
            glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite",0);glass.SetFloat("_Cull",0);glass.SetFloat("_Smoothness",.35f);
            glass.SetOverrideTag("RenderType","Transparent");glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.renderQueue=(int)RenderQueue.Transparent;glass.SetShaderPassEnabled("ShadowCaster",false);
            EditorUtility.SetDirty(solid);EditorUtility.SetDirty(glass);return new[]{solid,glass};
        }

        private static Material CreateMaterial(Shader shader,string name)
        {
            string path=Root+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader){name=name};AssetDatabase.CreateAsset(material,path);}
            return material;
        }

        private static string Number(float n)=>n.ToString("0.########",CultureInfo.InvariantCulture);

        private static void WriteMtl()
        {
            File.WriteAllText(Root+"/Models/ConceptTownKit.mtl",
                "newmtl ConceptSurface\nKd 1 1 1\nKa 1 1 1\nd 1\nillum 1\nmap_Kd ../Materials/ConceptPalette.png\n\n"+
                "newmtl ConceptGlass\nKd 1 1 1\nKa 1 1 1\nd 0.2\nillum 1\nmap_Kd ../Materials/ConceptPalette.png\n");
        }

        private static void WriteObj(Mesh mesh,string path)
        {
            var text=new StringBuilder("# ConceptTownKit; metres; grounded origin\nmtllib ../ConceptTownKit.mtl\no "+mesh.name+"\n");
            // Wavefront's right-handed coordinates require mirroring X and reversing each face.
            foreach(var v in mesh.vertices)text.AppendLine("v "+Number(-v.x)+" "+Number(v.y)+" "+Number(v.z));
            foreach(var uv in mesh.uv)text.AppendLine("vt "+Number(uv.x)+" "+Number(uv.y));
            foreach(var n in mesh.normals)text.AppendLine("vn "+Number(-n.x)+" "+Number(n.y)+" "+Number(n.z));
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                text.AppendLine("usemtl "+(sub==0?"ConceptSurface":"ConceptGlass"));var t=mesh.GetTriangles(sub);
                for(int i=0;i<t.Length;i+=3)
                {
                    int a=t[i]+1,b=t[i+2]+1,c=t[i+1]+1;
                    text.AppendLine($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}");
                }
            }
            File.WriteAllText(path,text.ToString(),new UTF8Encoding(false));
        }

        private static void ValidateMesh(Mesh mesh,string id)
        {
            if(mesh.vertexCount==0||mesh.triangles.Length/3>24000||mesh.subMeshCount<1||mesh.subMeshCount>2)
                throw new InvalidOperationException("Invalid/excessive geometry: "+id);
            var v=mesh.vertices;var n=mesh.normals;var uv=mesh.uv;var t=mesh.triangles;
            if(n.Length!=v.Length||uv.Length!=v.Length||Mathf.Abs(mesh.bounds.min.y)>.0001f)
                throw new InvalidOperationException("Missing attributes or ungrounded pivot: "+id);
            for(int i=0;i<v.Length;i++)
            {
                if(!Finite(v[i].x)||!Finite(v[i].y)||!Finite(v[i].z)||!Finite(n[i].x)||!Finite(n[i].y)||!Finite(n[i].z)
                    ||Mathf.Abs(n[i].magnitude-1)>.002f||!Finite(uv[i].x)||!Finite(uv[i].y)||uv[i].x<0||uv[i].x>1)
                    throw new InvalidOperationException("Invalid vertex/normal/UV: "+id);
            }
            for(int i=0;i<t.Length;i+=3)
                if(Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).sqrMagnitude<1e-12f)
                    throw new InvalidOperationException("Degenerate triangle: "+id);
        }

        private static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);

        private static void ValidateSavedAssets(Manifest manifest)
        {
            foreach(var entry in manifest.assets)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+entry.prefab);
                if(prefab==null||prefab.GetComponentsInChildren<Renderer>().Length!=1
                    ||prefab.GetComponentsInChildren<MonoBehaviour>(true).Length!=0||prefab.GetComponentsInChildren<Collider>(true).Length!=0)
                    throw new InvalidOperationException("Invalid draft prefab: "+entry.id);
                var mesh=prefab.GetComponent<MeshFilter>().sharedMesh;ValidateMesh(mesh,entry.id);
                var materials=prefab.GetComponent<MeshRenderer>().sharedMaterials;
                if(materials.Length!=entry.materials||materials.Any(m=>m==null||m.shader==null||m.GetTexture("_BaseMap")==null))
                    throw new InvalidOperationException("Missing material/texture: "+entry.id);
                if(entry.materials==2&&(materials[1].renderQueue<3000||materials[1].GetFloat("_ZWrite")!=0))
                    throw new InvalidOperationException("Glazing lost its transparent configuration: "+entry.id);
                if(mesh.triangles.Length/3!=entry.triangles||(mesh.bounds.size-entry.size).sqrMagnitude>.00001f)
                    throw new InvalidOperationException("Stale saved geometry: "+entry.id);
                var imported=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+entry.obj);
                if(imported==null)throw new InvalidOperationException("OBJ import failed: "+entry.id);
                var importedMeshes=imported.GetComponentsInChildren<MeshFilter>();
                if(importedMeshes.Sum(f=>f.sharedMesh.triangles.Length/3)!=entry.triangles)
                    throw new InvalidOperationException("OBJ changed the triangle count: "+entry.id);
                foreach(string preview in new[]{entry.preview,entry.rearPreview})
                    if(!File.Exists(Root+"/"+preview)||new FileInfo(Root+"/"+preview).Length<5000)
                        throw new InvalidOperationException("Preview missing or empty: "+entry.id);
            }
        }

        private static void WriteGallery(Manifest manifest)
        {
            var html=new StringBuilder("<!doctype html><html lang=\"ru\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>ConceptTownKit — 12 моделей</title><style>body{margin:28px;background:#f4f2e9;color:#254341;font:16px system-ui}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:20px}figure{margin:0;background:white;border-radius:8px;overflow:hidden}img{width:100%;display:block}figcaption{padding:16px}small{display:block;margin:8px 0;color:#64716f}a{color:#197c7c}details{margin-top:12px}h1{font-size:28px}</style></head><body><h1>12 объектов по утверждённому арту</h1><p>Настоящие рендеры Unity. Модели подготовлены для просмотра и ещё не перенесены на игровую карту.</p><p>1 единица = 1 метр. Статическое окружение без интерьеров, коллайдеров и игровой логики. Стёкла остановки и теплицы прозрачные.</p><main>");
            foreach(var e in manifest.assets)
            {
                string size=e.size.x.ToString("0.#",CultureInfo.InvariantCulture)+" × "+e.size.y.ToString("0.#",CultureInfo.InvariantCulture)+" × "+e.size.z.ToString("0.#",CultureInfo.InvariantCulture);
                html.Append($"<figure><img src=\"{e.preview}\" alt=\"{e.name}\"><figcaption><b>{e.id.Substring(0,2)} · {e.name}</b><small>{e.triangles} треугольников · {size} м · материалов: {e.materials}</small><a href=\"{e.obj}\">Модель OBJ</a><details><summary>Вид сзади</summary><img src=\"{e.rearPreview}\" alt=\"{e.name} — сзади\"></details></figcaption></figure>");
            }
            html.Append("</main></body></html>");File.WriteAllText(Root+"/index.html",html.ToString(),new UTF8Encoding(false));
        }
    }

    internal static class ConceptPreviews
    {
        private const int Size=640;

        public static Texture2D Render(GameObject prefab,bool rear)
        {
            var scene=EditorSceneManager.NewPreviewScene();RenderTexture target=null;Texture2D image=null;
            var previous=RenderTexture.active;
            try
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                foreach(var transform in instance.GetComponentsInChildren<Transform>())transform.gameObject.layer=30;
                var bounds=instance.GetComponent<MeshFilter>().sharedMesh.bounds;float radius=bounds.extents.magnitude;
                var cameraObject=new GameObject("Concept preview camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.cullingMask=1<<30;
                camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.956f,.949f,.922f,1);
                camera.transform.position=bounds.center+new Vector3(rear?-1:1,.88f,rear?-1.3f:1.3f).normalized*radius*4;
                camera.transform.LookAt(bounds.center);
                float horizontal=ProjectedExtent(bounds.extents,camera.transform.right),vertical=ProjectedExtent(bounds.extents,camera.transform.up);
                camera.orthographicSize=Mathf.Max(horizontal,vertical)*1.15f;camera.nearClipPlane=.01f;camera.farClipPlane=radius*10+10;
                var lightObject=new GameObject("Concept key light");SceneManager.MoveGameObjectToScene(lightObject,scene);
                lightObject.transform.rotation=Quaternion.Euler(40,-125,0);var light=lightObject.AddComponent<Light>();
                light.type=LightType.Directional;light.intensity=1.2f;light.color=new Color(1,.97f,.91f);light.cullingMask=1<<30;
                light.shadows=LightShadows.Soft;light.shadowBias=.04f;light.shadowNormalBias=.15f;
                var fillObject=new GameObject("Concept fill light");SceneManager.MoveGameObjectToScene(fillObject,scene);
                fillObject.transform.position=bounds.center+new Vector3(-1,1,-.6f).normalized*radius*3;
                var fill=fillObject.AddComponent<Light>();fill.type=LightType.Point;fill.range=radius*12;fill.intensity=5*radius*radius;
                fill.color=new Color(.91f,.96f,1);fill.cullingMask=1<<30;
                target=new RenderTexture(Size,Size,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
                camera.Render();RenderTexture.active=target;
                image=new Texture2D(Size,Size,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,Size,Size),0,0);image.Apply();
                var pixels=image.GetPixels32();var background=pixels[0];
                int occupied=pixels.Count(p=>Math.Abs(p.r-background.r)+Math.Abs(p.g-background.g)+Math.Abs(p.b-background.b)>35);
                if(occupied<Size*Size*.04f)throw new InvalidOperationException("Empty or tiny Unity render: "+prefab.name);
                return image;
            }
            catch {if(image!=null)Object.DestroyImmediate(image);throw;}
            finally
            {
                RenderTexture.active=previous;
                if(target!=null){target.Release();Object.DestroyImmediate(target);}
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static float ProjectedExtent(Vector3 extent,Vector3 axis)
            =>Mathf.Abs(axis.x)*extent.x+Mathf.Abs(axis.y)*extent.y+Mathf.Abs(axis.z)*extent.z;

        public static void Sheet(IReadOnlyList<Texture2D> images,string path)
        {
            if(images.Count!=12)throw new ArgumentException("Expected exactly twelve preview tiles.");
            var sheet=new Texture2D(Size*4,Size*3,TextureFormat.RGBA32,false);
            try
            {
                for(int i=0;i<images.Count;i++)sheet.SetPixels((i%4)*Size,(2-i/4)*Size,Size,Size,images[i].GetPixels());
                sheet.Apply();File.WriteAllBytes(path,sheet.EncodeToPNG());
            }
            finally {Object.DestroyImmediate(sheet);}
        }
    }
}
