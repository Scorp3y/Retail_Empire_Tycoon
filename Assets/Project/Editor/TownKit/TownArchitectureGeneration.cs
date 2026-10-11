using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>Creates the architecture catalogue only. Does not open or save gameplay scenes.</summary>
    public static class TownArchitectureGeneration
    {
        public const string Root = "Assets/Art/TownArchitecture";
        public const string Report = "Library/TownArchitectureQA";
        private static readonly string[] RequiredIds =
        {
            "LCorner3", "LCorner5", "Sectional3", "Sectional5", "TownhouseRow3", "TownhouseRow6",
            "UCourtyard4", "Stepped469", "Mansard3"
        };

        public static void GenerateBatch()
        {
            try
            {
                TownWorldSafety.RequireIsolatedEditor();
                Directory.CreateDirectory(Report);
                File.WriteAllText(Report + "/result.txt", "RUNNING architecture asset generation\n");
                Generate();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(Report);
                File.WriteAllText(Report + "/result.txt", "FAILED: " + error);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void Generate()
        {
            foreach (string folder in new[] { "Meshes", "Prefabs", "Models", "Materials", "Previews" })
                Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            Material material = TownArchitecturePalette.CreateMaterial();
            var definitions = TownArchitectureModels.Create();
            Require(definitions.Count == RequiredIds.Length && definitions.Select(definition => definition.Id).Distinct().Count() == RequiredIds.Length,
                "Exactly nine distinct architecture variants are required.");
            Require(RequiredIds.All(id => definitions.Any(definition => definition.Id == id)), "The approved variant catalogue changed.");
            Require(definitions.Select(definition => definition.Family).Distinct().Count() == 6, "Exactly six architectural families are required.");
            // Export uses the same neutral material name as the shared OBJ writer, but only this new atlas.
            File.WriteAllText(Root + "/Models/TownKit.mtl",
                "newmtl TownPalette\nKa 1 1 1\nKd 1 1 1\nKs 0 0 0\nd 1\nillum 1\nmap_Kd ../Materials/"
                + Path.GetFileName(TownArchitecturePalette.PngPath) + "\n");

            var manifest = new CatalogueManifest();
            var frontImages = new List<Texture2D>();
            var rearImages = new List<Texture2D>();
            var roofImages = new List<Texture2D>();
            var detailImages = new List<Texture2D>();
            var ids = new List<string>();
            try
            {
                foreach (string id in RequiredIds)
                {
                    var definition = definitions.Single(candidate => candidate.Id == id);
                    var model = definition.Build();
                    Mesh mesh = model.Geometry.Build(id);
                    try
                    {
                        // Geometry and metadata share native coordinates. Moving vertices to ground here
                        // would silently invalidate roof attachments and collision-volume contracts.
                        TownArchitectureValidation.Validate(definition, model, mesh, material);
                        string meshPath = Root + "/Meshes/" + id + ".asset";
                        mesh = SaveMesh(mesh, meshPath);
                        string prefabPath = Root + "/Prefabs/" + id + ".prefab";
                        GameObject prefab = SavePrefab(id, mesh, material, prefabPath);
                        TownArchitectureValidation.ValidateSaved(prefab, material);
                        string objPath = Root + "/Models/" + id + ".obj";
                        TownModelExport.Obj(mesh, objPath, "TownKit.mtl");

                        var front = TownArchitecturePreviews.Render(prefab, ArchitecturePreviewView.Front);
                        frontImages.Add(front);
                        var rear = TownArchitecturePreviews.Render(prefab, ArchitecturePreviewView.Rear);
                        rearImages.Add(rear);
                        var roof = TownArchitecturePreviews.Render(prefab, ArchitecturePreviewView.Roof);
                        roofImages.Add(roof);
                        var detail = TownArchitecturePreviews.RenderDetail(prefab);
                        detailImages.Add(detail);
                        string frontPath = "Previews/" + id + "_Front.png";
                        string rearPath = "Previews/" + id + "_Rear.png";
                        string roofPath = "Previews/" + id + "_Roof.png";
                        File.WriteAllBytes(Root + "/" + frontPath, front.EncodeToPNG());
                        File.WriteAllBytes(Root + "/" + rearPath, rear.EncodeToPNG());
                        File.WriteAllBytes(Root + "/" + roofPath, roof.EncodeToPNG());
                        File.WriteAllBytes(Root + "/Previews/" + id + "_Detail.png", detail.EncodeToPNG());
                        ids.Add(id);
                        manifest.assets.Add(CatalogueEntry.From(definition, model, mesh, prefabPath, objPath, frontPath, rearPath, roofPath));
                    }
                    finally
                    {
                        if (mesh != null && !AssetDatabase.Contains(mesh)) Object.DestroyImmediate(mesh);
                    }
                }
                TownArchitecturePreviews.Sheet(frontImages, ids, "Front", Root + "/Previews/Fronts.png");
                TownArchitecturePreviews.Sheet(rearImages, ids, "Rear", Root + "/Previews/Rears.png");
                TownArchitecturePreviews.Sheet(roofImages, ids, "Roof", Root + "/Previews/Roofs.png");
                TownModelPreviews.Sheet(detailImages, 3, Root + "/Previews/Details.png");
                File.WriteAllText(Root + "/manifest.json", JsonUtility.ToJson(manifest, true));
                File.WriteAllText(Root + "/Previews/camera-manifest.json", TownArchitecturePreviews.CameraManifest());
                WriteGallery(manifest);
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                VerifySavedCatalogue(manifest, material);
                // Bind the acceptance report to every delivered native asset and preview.
                var inputs = new CatalogueHashes
                {
                    files = Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
                        .OrderBy(path => path, StringComparer.Ordinal).Select(Hash).ToArray()
                };
                File.WriteAllText(Report + "/catalogue-hashes.json", JsonUtility.ToJson(inputs, true));
                File.WriteAllText(Report + "/manifest.json", JsonUtility.ToJson(manifest, true));
                File.WriteAllText(Report + "/result.txt",
                    $"TOWN ARCHITECTURE PASSED: {manifest.assets.Count} native variants in six families; {manifest.assets.Sum(entry => entry.triangles)} triangles total.\n"
                    + "Shared new atlas/material, grounded native geometry, volume/roof/attachment contracts, saved visual-only prefabs and OBJ round-trip passed.\n"
                    + "Twenty-seven fixed native perspective front/rear/roof previews, nine explicitly bounds-framed detail close-ups and four contact sheets passed.\n"
                    + "Asset-only generation: no gameplay scene, supplier, farm, old palette, catalogue, inventory or player save was modified.\n"
                    + "Image review and the separate before/after city hash guard are still required before Apply.\n");
            }
            finally
            {
                foreach (var image in frontImages.Concat(rearImages).Concat(roofImages).Concat(detailImages)) Object.DestroyImmediate(image);
            }
        }

        private static Mesh SaveMesh(Mesh generated, string path)
        {
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            saved.Clear();
            saved.indexFormat = generated.indexFormat;
            saved.vertices = generated.vertices;
            saved.normals = generated.normals;
            saved.uv = generated.uv;
            saved.triangles = generated.triangles;
            saved.RecalculateBounds();
            EditorUtility.SetDirty(saved);
            Object.DestroyImmediate(generated);
            return saved;
        }

        private static GameObject SavePrefab(string id, Mesh mesh, Material material, string path)
        {
            var instance = new GameObject(id, typeof(MeshFilter), typeof(MeshRenderer));
            try
            {
                instance.GetComponent<MeshFilter>().sharedMesh = mesh;
                instance.GetComponent<MeshRenderer>().sharedMaterial = material;
                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static void VerifySavedCatalogue(CatalogueManifest manifest, Material material)
        {
            foreach (var entry in manifest.assets)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + entry.prefab);
                TownArchitectureValidation.ValidateSaved(prefab, material);
                var native = prefab.GetComponent<MeshFilter>().sharedMesh;
                Require(native.triangles.Length / 3 == entry.triangles && (native.bounds.size - entry.size).sqrMagnitude < .000001f,
                    "Saved mesh metadata differs: " + entry.id);
                var importer = AssetImporter.GetAtPath(Root + "/" + entry.obj) as ModelImporter;
                Require(importer != null, "OBJ importer missing: " + entry.id);
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.isReadable = true;
                importer.globalScale = 1;
                importer.useFileScale = false;
                importer.SaveAndReimport();
                var imported = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + entry.obj);
                Require(imported != null, "OBJ round-trip missing: " + entry.id);
                var filters = imported.GetComponentsInChildren<MeshFilter>(true);
                Require(filters.Length > 0 && filters.Sum(filter => filter.sharedMesh.triangles.Length / 3) == entry.triangles,
                    "OBJ triangle round-trip mismatch: " + entry.id);
                var importedVertices = filters.SelectMany(filter => filter.sharedMesh.vertices.Select(filter.transform.TransformPoint)).ToArray();
                var importedBounds = new Bounds(importedVertices[0], Vector3.zero);
                foreach (var vertex in importedVertices) importedBounds.Encapsulate(vertex);
                Require((importedBounds.size - native.bounds.size).sqrMagnitude < .0001f
                    && Mathf.Abs(importedBounds.min.y - native.bounds.min.y) < .001f,
                    "OBJ metre-scale / grounded pivot round-trip mismatch: " + entry.id);
                string UvKey(Vector2 uv) => Mathf.RoundToInt(uv.x * 100000) + "/" + Mathf.RoundToInt(uv.y * 100000);
                var expectedUvs = new HashSet<string>(native.uv.Select(UvKey));
                var importedUvs = new HashSet<string>(filters.SelectMany(filter => filter.sharedMesh.uv).Select(UvKey));
                Require(expectedUvs.SetEquals(importedUvs), "OBJ palette UV round-trip mismatch: " + entry.id);
                string objDirectory = Path.GetDirectoryName(Root + "/" + entry.obj);
                string library = File.ReadAllLines(Root + "/" + entry.obj).Single(line => line.StartsWith("mtllib ", StringComparison.Ordinal)).Substring(7);
                string libraryPath = Path.GetFullPath(Path.Combine(objDirectory, library));
                Require(File.Exists(libraryPath), "OBJ material library path is broken: " + entry.id);
                string texture = File.ReadAllLines(libraryPath).Single(line => line.StartsWith("map_Kd ", StringComparison.Ordinal)).Substring(7);
                Require(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(libraryPath), texture)) == Path.GetFullPath(TownArchitecturePalette.PngPath),
                    "OBJ material library does not reference the approved palette: " + entry.id);
                Require(File.Exists(Root + "/" + entry.front) && File.Exists(Root + "/" + entry.rear)
                    && File.Exists(Root + "/" + entry.roof), "Preview missing: " + entry.id);
            }
        }

        private static void WriteGallery(CatalogueManifest manifest)
        {
            string Encode(string value) => SecurityElement.Escape(value) ?? "";
            var html = new StringBuilder("<!doctype html><html lang=\"ru\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>Архитектура города — этап 2</title><style>body{margin:28px;font:16px system-ui;background:#f4f2e9;color:#263c35}main{max-width:1560px;margin:auto}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:20px}figure{margin:0;background:#fff;padding:12px}img{width:100%;height:auto}h2{font-size:19px;margin:8px 0}small{display:block;line-height:1.5}details img{margin-top:8px}@media(max-width:900px){.grid{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:580px){.grid{grid-template-columns:1fr}}</style><main><h1>Архитектура города: 6 семейств, 9 вариантов</h1><p>Настоящие Unity-модели и OBJ. Метры; Y вверх; фронт +Z. Фасады/крыши для сравнения сняты одинаковыми перспективными камерами с сеткой. Отдельные крупные планы деталей подогнаны под габариты модели и не служат сравнением масштаба.</p><p>Набор не расставлен в городе. Ферма, пункты выдачи и игровая сцена не изменяются генератором.</p><p><a href=\"Previews/Details.png\">Крупные планы</a> · <a href=\"Previews/Fronts.png\">Все фасады</a> · <a href=\"Previews/Rears.png\">Все задние фасады</a> · <a href=\"Previews/Roofs.png\">Все крыши</a> · <a href=\"manifest.json\">Метаданные</a></p><section class=\"grid\">");
            foreach (var entry in manifest.assets)
            {
                string dimensions = string.Format(CultureInfo.InvariantCulture, "{0:0.##} × {1:0.##} × {2:0.##}", entry.size.x, entry.size.y, entry.size.z);
                string floors = string.Join(" / ", entry.volumes.Select(volume => volume.floors).Distinct().OrderBy(number => number));
                html.Append("<figure><img src=\"").Append(entry.detail).Append("\" alt=\"").Append(Encode(entry.label)).Append(" — детали, индивидуальное кадрирование\"><figcaption><h2>")
                    .Append(Encode(entry.label)).Append("</h2><small>").Append(Encode(entry.id)).Append(" · ").Append(Encode(entry.family))
                    .Append("<br>").Append(dimensions).Append(" м (ширина × высота × глубина); этажи ").Append(floors).Append("<br>").Append(entry.triangles)
                    .Append(" треугольников; ").Append(entry.volumes.Length).Append(" объёмов; ").Append(entry.attachments.Length)
                    .Append(" крышных деталей<br><a href=\"").Append(entry.obj).Append("\">OBJ</a> · <a href=\"").Append(entry.prefab)
                    .Append("\">Unity prefab</a><br>Крупный план выше подогнан под модель; для сравнения размеров — фиксированные кадры ниже.</small></figcaption><details><summary>Фиксированный масштаб: фронт, задний фасад и крыша</summary><img src=\"").Append(entry.front).Append("\"><img src=\"")
                    .Append(entry.rear).Append("\" alt=\"Задний фасад\"><img src=\"").Append(entry.roof).Append("\" alt=\"Крыша\"></details></figure>");
            }
            html.Append("</section><p>Коробки коллизий и точки крепления представлены метаданными; визуальные prefab не содержат коллайдеров или игровых компонентов.</p></main></html>");
            File.WriteAllText(Root + "/index.html", html.ToString());
        }

        private static AssetHash Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var algorithm = SHA256.Create())
                return new AssetHash { path = path.Replace('\\', '/'), sha256 = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "") };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        [Serializable] private sealed class CatalogueManifest
        {
            public int schemaVersion = 1;
            public string units = "Native metres; Y up; front +Z; grounded pivot; visual-only assets.";
            public string scope = "Stage 2 asset catalogue only; no city placement or gameplay changes.";
            public string material = TownArchitecturePalette.MaterialPath;
            public string palette = TownArchitecturePalette.PngPath;
            public List<CatalogueEntry> assets = new List<CatalogueEntry>();
        }

        [Serializable] private sealed class CatalogueEntry
        {
            public string id, label, family, prefab, obj, front, rear, roof, detail;
            public int triangles, vertices;
            public Vector3 size;
            public float footprintSquareMetres;
            public VolumeEntry[] volumes;
            public AttachmentEntry[] attachments;
            public EntranceEntry[] entrances;
            public Rect[] courtyardOpenings;

            public static CatalogueEntry From(TownArchitectureDefinition definition, TownArchitectureModel model, Mesh mesh,
                string prefabPath, string objPath, string front, string rear, string roof)
            {
                return new CatalogueEntry
                {
                    id = definition.Id, label = definition.Label, family = definition.Family,
                    prefab = prefabPath.Substring(Root.Length + 1), obj = objPath.Substring(Root.Length + 1),
                    front = front, rear = rear, roof = roof, detail = "Previews/" + definition.Id + "_Detail.png",
                    triangles = mesh.triangles.Length / 3, vertices = mesh.vertexCount,
                    size = mesh.bounds.size, footprintSquareMetres = model.FootprintArea,
                    volumes = model.Volumes.Select(volume => new VolumeEntry
                    {
                        bodyBounds = volume.BodyBounds, floors = volume.Floors, floorHeight = volume.FloorHeight,
                        roofShape = volume.RoofShape.ToString(), eaveHeight = volume.EaveHeight, rise = volume.Rise,
                        roofFootprint = volume.RoofFootprint
                    }).ToArray(),
                    attachments = model.Attachments.Select(attachment => new AttachmentEntry
                    {
                        id = attachment.Id, basePosition = attachment.BasePosition, baseSize = attachment.BaseSize,
                        surfaceHeight = attachment.SurfaceHeight, roofVolumeIndex = attachment.RoofVolumeIndex,
                        conformsToRoof = attachment.ConformsToRoof, embedDepth = attachment.EmbedDepth
                    }).ToArray(),
                    entrances = model.Entrances.Select(entrance => new EntranceEntry { position = entrance.Position, outward = entrance.Outward }).ToArray(),
                    courtyardOpenings = model.CourtyardOpenings
                };
            }
        }

        [Serializable] private sealed class VolumeEntry
        {
            public Bounds bodyBounds;
            public int floors;
            public float floorHeight, eaveHeight, rise;
            public string roofShape;
            public Rect roofFootprint;
        }
        [Serializable] private sealed class AttachmentEntry
        {
            public string id;
            public Vector3 basePosition;
            public Vector2 baseSize;
            public float surfaceHeight, embedDepth;
            public int roofVolumeIndex;
            public bool conformsToRoof;
        }
        [Serializable] private sealed class EntranceEntry { public Vector3 position, outward; }
        [Serializable] private sealed class AssetHash { public string path, sha256; }
        [Serializable] private sealed class CatalogueHashes { public AssetHash[] files; }
    }
}
