using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Products;
using RetailEmpireTycoon.UI.Shop;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Only the six whole bakery models selected by the player; existing bread keeps its save identity.</summary>
public static class ShopBakeryAssortment
{
    private const string Source = "Assets/Yodah_the_Cat/Stylized bread serving/Prefabs/";
    private const string Folder = "Assets/Prefabs/Product/Bakery";
    public static readonly string[] Models = { "Loaf", "Baguette", "BrownBread", "WhiteBread", "Bun", "BunSesame" };
    private static readonly string[] Names = { "Loaf", "Baguette", "Brown bread", "White bread", "Bun", "Sesame bun" };
    private static readonly string[] Ids = { "product_bread", "product_baguette", "product_brown_bread", "product_white_bread", "product_bun", "product_sesame_bun" };

    public static void ApplyModels()
    {
        foreach (string model in Models)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Source + model + ".prefab") == null)
                throw new InvalidOperationException("Missing selected bakery model: " + model);
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        for (int i = 0; i < Models.Length; i++)
        {
            string visualPath = i == 0 ? "Assets/Prefabs/Product/Bread.prefab" : Folder + "/" + Models[i] + ".prefab";
            var root = new GameObject(Models[i]);
            GameObject visual;
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source + Models[i] + ".prefab"));
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(ConvertMaterial).ToArray();
                Bounds bounds = Measure(model);
                if (bounds.size.x > bounds.size.z) model.transform.localRotation = Quaternion.Euler(0,90,0) * model.transform.localRotation;
                bounds = Measure(model);
                model.transform.localScale *= .19f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                bounds = Measure(model);
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                visual = PrefabUtility.SaveAsPrefabAsset(root, visualPath);
            }
            finally { Object.DestroyImmediate(root); }
            string dataPath = i == 0 ? "Assets/Prefabs/Product/Bread_Product.asset" : Folder + "/" + Models[i] + "_Product.asset";
            var item = AssetDatabase.LoadAssetAtPath<ProductItemData>(dataPath);
            bool fresh = item == null;
            if (fresh) { item = ScriptableObject.CreateInstance<ProductItemData>(); AssetDatabase.CreateAsset(item, dataPath); }
            var data = new SerializedObject(item);
            data.FindProperty("id").stringValue = Ids[i];
            data.FindProperty("displayName").stringValue = Names[i];
            data.FindProperty("storageType").enumValueIndex = (int)ProductStorageType.Bakery;
            data.FindProperty("shelfDisplayPrefab").objectReferenceValue = visual;
            if (fresh)
            {
                data.FindProperty("buyPrice").intValue = i <= 3 ? 50 : 30;
                data.FindProperty("sellPrice").intValue = i <= 3 ? 8 : 5;
                data.FindProperty("boxAmount").intValue = 10;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var window in Object.FindObjectsOfType<ShopWindow>(true)) Append(window, "productCatalog", item);
            foreach (var catalog in Object.FindObjectsOfType<ProductCatalog>(true)) Append(catalog, "products", item);
        }
    }

    private static Material ConvertMaterial(Material source)
    {
        if (source == null) throw new InvalidOperationException("Bakery material is missing.");
        if (source.shader.name.StartsWith("Universal Render Pipeline/")) return source;
        string path = Folder + "/Material_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) + ".mat";
        var target = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (target == null)
        {
            target = new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(target, path);
        }
        target.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
        // The installed Shader Graph uses _BaseColour, and can be unsupported in this Unity version.
        // Read serialized texture references even when its shader reports no compatible properties.
        var textures=new SerializedObject(source).FindProperty("m_SavedProperties.m_TexEnvs");Texture albedo=null;
        foreach(string property in new[]{"_BaseColour","_BaseMap","_MainTex"})
        {
            for(int i=0;i<textures.arraySize;i++)
            {
                var entry=textures.GetArrayElementAtIndex(i);
                if(entry.FindPropertyRelative("first").stringValue==property)
                    albedo=entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                if(albedo!=null) break;
            }
            if(albedo!=null) break;
        }
        if(albedo==null) throw new InvalidOperationException("Selected bakery model has no albedo texture: "+source.name);
        target.SetTexture("_BaseMap",albedo);target.SetFloat("_Smoothness", .1f);EditorUtility.SetDirty(target);return target;
    }
    private static Bounds Measure(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new InvalidOperationException("Empty bakery model.");
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    private static void Append(Object target, string field, Object value)
    {
        var data = new SerializedObject(target); var array = data.FindProperty(field);
        for (int i = 0; i < array.arraySize; i++) if (array.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
        array.arraySize++; array.GetArrayElementAtIndex(array.arraySize-1).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
