using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.BuildSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class DecorCollectionBatch
{
    internal const string ArtRoot = "Assets/Art/DecorCollection";
    internal const string ItemRoot = "Assets/Resources/DecorCollection";
    private const string Balance = "Assets/Project/Editor/DecorCollectionBalance.txt";
    private const int TriangleBudget = 3000;
    private const string VisualName = "Collection model";
    private const float CellPadding = .022f;

    private sealed class Design
    {
        public int Number, Width, Depth, Price, Beauty, Level;
        public string Id, Name, RussianName;
        public DecorGroup Group;
        public DecorMount Mount;
        public DecorSeason Season;
    }

    [MenuItem("Retail Empire/Art/Build decor collection")]
    public static void Generate()
    {
        foreach (string folder in new[] { ItemRoot, ArtRoot+"/Meshes", ArtRoot+"/Materials", ArtRoot+"/Prefabs", ArtRoot+"/Icons" })
            Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        var designs = File.ReadLines(Balance).Skip(1).Where(line=>!string.IsNullOrWhiteSpace(line)).Select(Parse).ToArray();
        if (designs.Length != 48 || designs.Select(d=>d.Number).Distinct().Count()!=48 ||
            designs.Select(d=>d.Id).Distinct().Count()!=48 || designs.Any(d=>d.Number is 23 or 30))
            throw new InvalidDataException("The decor balance sheet must contain exactly 48 distinct approved designs.");
        var palette = Enumerable.Range(0,DecorMesh.Colors.Length).Select(MaterialFor).ToArray();
        var template = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/StoreUpgrade/decor_plant.asset");
        var output = new List<string> { "ID,Triangles,Materials" };
        var items = new List<BuildItemData>(designs.Length);
        foreach (var design in designs)
        {
            var item = Existing(design.Id);
            string itemPath = item != null ? AssetDatabase.GetAssetPath(item) : $"{ItemRoot}/{design.Id}.asset";
            string prefabPath = item != null && item.prefab != null ? AssetDatabase.GetAssetPath(item.prefab) : $"{ArtRoot}/Prefabs/{design.Id}.prefab";
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var root = sourcePrefab != null ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject(design.Id);
            bool rework = !itemPath.StartsWith(ItemRoot, StringComparison.Ordinal);
            try
            {
                var existingVisual = root.transform.Find(VisualName);
                if(existingVisual != null) Object.DestroyImmediate(existingVisual.gameObject);
                if(rework)
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                        renderer.enabled = false;
                var art = DecorArt.Make(design.Number);
                bool wall = (design.Mount & (DecorMount.Wall | DecorMount.Awning)) != 0;
                var originalBounds = rework ? item.placementBounds : default;
                var offset = new Vector3(originalBounds.center.x, Mathf.Max(0,originalBounds.min.y), originalBounds.center.z);
                var size = new Vector2(design.Width,design.Depth) * ShopProportions.CellSize - Vector2.one * CellPadding;
                if(design.Mount is DecorMount.Garland) size = new Vector2(1f,0f);
                else if(wall) size.y = 0f;
                float heightLimit = design.Mount.HasFlag(DecorMount.Ceiling) ? .54f : float.PositiveInfinity;
                var generated = art.Bake(design.Id, size, wall, root.transform.localScale, offset, heightLimit);
                int triangles = generated.triangles.Length / 3;
                if (triangles > TriangleBudget) throw new InvalidOperationException($"{design.Id}: {triangles} triangles exceeds {TriangleBudget}.");
                var mesh = SaveMesh(generated, $"{ArtRoot}/Meshes/{design.Id}.asset");
                var visual = new GameObject(VisualName, typeof(MeshFilter), typeof(MeshRenderer));
                visual.transform.SetParent(root.transform, false);
                visual.GetComponent<MeshFilter>().sharedMesh = mesh;
                visual.GetComponent<MeshRenderer>().sharedMaterials = art.UsedFinishes.Select(f=>palette[(int)f]).ToArray();
                if (!rework)
                {
                    var collider = visual.AddComponent<BoxCollider>();
                    collider.center = mesh.bounds.center;
                    collider.size = mesh.bounds.size;
                    collider.isTrigger = !DecorCatalog.UsesFloorSpace(design.Mount);
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if(prefab == null) throw new InvalidOperationException("Could not save " + prefabPath);
                var icon = ShopUiPreviews.Render(prefab, design.Id, 28, outputFolder: ArtRoot+"/Icons");
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<BuildItemData>();
                    AssetDatabase.CreateAsset(item,itemPath);
                }
                item.id = design.Id;
                item.displayName = design.Name;
                item.russianName = design.RussianName;
                item.category = BuildCategory.Decoration;
                item.decorGroup = design.Group;
                item.decorMount = design.Mount;
                item.decorSeason = design.Season;
                item.decorUnlockLevel = design.Level;
                item.beautyPoints = design.Beauty;
                item.price = design.Price;
                item.icon = icon;
                item.prefab = prefab;
                if (!rework)
                {
                    item.footprint = new Vector2Int(design.Width, design.Depth);
                    item.weightKg = Mathf.Max(1, design.Width * design.Depth * .7f);
                    item.cargoVolumeM3 = Mathf.Max(.01f, design.Width * design.Depth * .012f);
                    item.placementBounds = mesh.bounds;
                    item.alignModelToFootprint = true;
                    item.ruleFlags = PlacementRuleFlags.InsidePurchasedArea | PlacementRuleFlags.NoOverlap;
                    item.previewValidMaterial = template.previewValidMaterial;
                    item.previewInvalidMaterial = template.previewInvalidMaterial;
                }
                EditorUtility.SetDirty(item);
                items.Add(item);
                output.Add($"{design.Id},{triangles},{art.UsedFinishes.Length}");
            }
            finally
            {
                if (sourcePrefab != null) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }
        var manifestPath=ItemRoot+"/Manifest.asset";
        var manifest=AssetDatabase.LoadAssetAtPath<DecorCollectionManifest>(manifestPath);
        if(manifest==null)
        {
            manifest=ScriptableObject.CreateInstance<DecorCollectionManifest>();
            AssetDatabase.CreateAsset(manifest,manifestPath);
        }
        manifest.items=items.ToArray();
        EditorUtility.SetDirty(manifest);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Library/DecorCollectionQA");
        File.WriteAllLines("Library/DecorCollectionQA/meshes.csv",output);
        Debug.Log("Decor Collection: built 48 verified prefabs and transparent catalog icons.");
    }

    private static Design Parse(string line)
    {
        var column = line.Split('|');
        int Number(int i) => int.Parse(column[i], CultureInfo.InvariantCulture);
        return new Design
        {
            Number = Number(0), Id=column[1], Name=column[2], RussianName=column[3],
            Group=Enum.Parse<DecorGroup>(column[4]), Mount=Enum.Parse<DecorMount>(column[5]),
            Width=Number(6), Depth=Number(7), Price=Number(8), Beauty=Number(9), Level=Number(10),
            Season=Enum.Parse<DecorSeason>(column[11])
        };
    }

    private static BuildItemData Existing(string id) =>
        AssetDatabase.LoadAssetAtPath<BuildItemData>($"Assets/Prefabs/StoreUpgrade/{id}.asset") ??
        AssetDatabase.LoadAssetAtPath<BuildItemData>($"Assets/Resources/FrontageDecor/{id}.asset") ??
        AssetDatabase.LoadAssetAtPath<BuildItemData>($"{ItemRoot}/{id}.asset");

    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing == null) { AssetDatabase.CreateAsset(mesh,path); return mesh; }
        existing.Clear();
        existing.vertices=mesh.vertices;
        existing.normals=mesh.normals;
        existing.subMeshCount=mesh.subMeshCount;
        for(int i=0;i<mesh.subMeshCount;i++) existing.SetTriangles(mesh.GetTriangles(i),i);
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssetIfDirty(existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }

    private static Material MaterialFor(int index)
    {
        var finish=(DecorMesh.Finish)index;
        string path=$"{ArtRoot}/Materials/{finish}.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null)
        {
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material,path);
        }
        ColorUtility.TryParseHtmlString("#"+DecorMesh.Colors[index],out var color);
        color.a=finish==DecorMesh.Finish.Glass?.27f:1f;
        material.SetColor("_BaseColor",color);
        material.SetFloat("_Smoothness",finish==DecorMesh.Finish.Glass?.42f:.18f);
        material.enableInstancing=true;
        if(finish==DecorMesh.Finish.Glow)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor",color*.58f);
        }
        if(finish==DecorMesh.Finish.Glass)
        {
            material.SetFloat("_Surface",1);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);
            material.SetOverrideTag("RenderType","Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue=(int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster",false);
        }
        EditorUtility.SetDirty(material);
        return material;
    }
}

