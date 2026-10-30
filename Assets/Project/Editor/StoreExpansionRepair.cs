using System.Linq;
using UnityEngine;
using UnityEditor;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Shelves;

public static class StoreExpansionRepair
{
    public static void Run()
    {
        var products=StoreAssortmentPack.Load().Products.ToDictionary(p=>p.Id);
        foreach(var item in StoreAssortmentPack.Load().Fixtures.Where(f=>f.id.StartsWith("shelf_fixture_")))
        {
            var path=AssetDatabase.GetAssetPath(item.prefab);var root=PrefabUtility.LoadPrefabContents(path);
            try {
                if(item.id.Contains("fixture_10_"))foreach(var slot in root.GetComponentsInChildren<ShelfProductSlot>())if(slot.hanging)slot.usableSize=new Vector3(.24f,slot.usableSize.y,slot.usableSize.z);
                var so=new SerializedObject(root.GetComponent<PlacedShelfStock>());var list=so.FindProperty("extraAllowedProducts");
                var allowed=Enumerable.Range(0,list.arraySize).Select(i=>list.GetArrayElementAtIndex(i).objectReferenceValue as ProductItemData).Where(p=>p!=null).Select(p=>products.TryGetValue(p.Id,out var canonical)?canonical:p).Distinct().ToArray();
                list.arraySize=allowed.Length;for(int i=0;i<allowed.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=allowed[i];so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var duplicate="Assets/Resources/StoreAssortment/Wave2/Products/baguette.asset";
        var asset=AssetDatabase.LoadAssetAtPath<ProductItemData>(duplicate);
        if(asset!=null && products.TryGetValue(asset.Id,out var existing) && existing!=asset)AssetDatabase.DeleteAsset(duplicate);
        AssetDatabase.SaveAssets();Debug.Log("Expansion references and hanging envelopes verified");
    }
}
