using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Shelves;
using RetailEmpireTycoon.BuildSystem;

/// <summary>Installs approved fixtures into the existing build/inventory/save contract.</summary>
public static class StoreFixtureInstallation
{
    static IEnumerable<string> Family(string folder) => Directory.GetFiles("ArtSource/"+folder,"*.glb").Select(p=>"product_"+Path.GetFileNameWithoutExtension(p));
    public static BuildItemData Create(Wave2Integration.Source source, GameObject root,string art,string data)
    {
        int number=int.Parse(source.key.Split('_')[1]);
        var products=AssetDatabase.FindAssets("t:ProductItemData").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ProductItemData>).Where(p=>p!=null).ToArray();
        var families=new HashSet<string>();var types=new List<ProductStorageType>();
        switch(number) {
            case 1:case 2:case 3:types.AddRange(new[]{ProductStorageType.Groceries,ProductStorageType.Oil,ProductStorageType.Drinks,ProductStorageType.Preserves,ProductStorageType.Snacks});break;
            case 4:types.Add(ProductStorageType.Any);break;
            case 5:types.Add(ProductStorageType.Bakery);break;
            case 6:types.Add(ProductStorageType.Produce);break;
            case 7:types.AddRange(new[]{ProductStorageType.Dairy,ProductStorageType.Meat,ProductStorageType.ReadyMeals});break;
            case 8:types.Add(ProductStorageType.Frozen);break;
            case 9:case 14:types.Add(ProductStorageType.Stationery);if(number==9)families.Add("product_toothbrush");break;
            case 10:families.UnionWith(Family("Expansion_05_CleaningTools"));families.UnionWith(new[]{"product_laundry_detergent","product_dish_soap","product_sponges","product_paper_towels","product_toilet_paper"});break;
            case 11:types.Add(ProductStorageType.PetSupplies);break;
            case 12:families.UnionWith(Family("Expansion_07_GardenSupplies"));break;
            case 13:types.Add(ProductStorageType.Toys);break;
            case 15:types.Add(ProductStorageType.Kitchenware);families.UnionWith(Family("Expansion_08_HomeComfort"));break;
            case 16:families.UnionWith(Family("Expansion_06_BabyCare"));break;
        }
        var allowed=products.Where(p=>types.Contains(ProductStorageType.Any)||types.Contains(p.StorageType)||families.Contains(p.Id)).ToArray();
        if(allowed.Length==0)throw new Exception("Fixture has no goods: "+source.key);
        var template=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Shelf/Assortment/ProduceRack.asset");
        root.layer=template.prefab.layer;root.AddComponent<PlacedObject>();var stock=root.AddComponent<PlacedShelfStock>();var display=root.AddComponent<ShelfProductDisplay>();
        var ss=new SerializedObject(stock);ss.FindProperty("acceptedProductTypes").arraySize=0;
        var extras=ss.FindProperty("extraAllowedProducts");extras.arraySize=allowed.Length;
        for(int i=0;i<allowed.Length;i++)extras.GetArrayElementAtIndex(i).objectReferenceValue=allowed[i];
        ss.FindProperty("maxAmount").intValue=source.anchors.Length;ss.ApplyModifiedPropertiesWithoutUndo();
        var slots=new GameObject("ProductSlots").transform;slots.SetParent(root.transform,false);
        for(int i=0;i<source.anchors.Length;i++) {
            var a=source.anchors[i];var slot=new GameObject("slot_"+(i+1).ToString("D2")).transform;slot.SetParent(slots,false);
            slot.localPosition=new Vector3(a.position[0],a.position[2],a.position[1]);slot.localRotation=Quaternion.Euler(0,-a.rotation_z*Mathf.Rad2Deg,0);
            var seat=slot.gameObject.AddComponent<ShelfProductSlot>();seat.usableSize=new Vector3(a.space[0],a.space[2],a.space[1]);seat.hanging=a.kind=="hang";
            if(number==10 && seat.hanging)seat.usableSize=new Vector3(.24f,seat.usableSize.y,seat.usableSize.z);
            if(number==9&&seat.hanging)seat.permittedProductIds=new[]{"product_pens","product_markers","product_toothbrush"};
            if(number==10)seat.permittedProductIds=allowed.Where(p=>seat.hanging?(p.Id=="product_broom"||p.Id=="product_mop"):(p.Id!="product_broom"&&p.Id!="product_mop")).Select(p=>p.Id).ToArray();
        }
        var ds=new SerializedObject(display);ds.FindProperty("shelfStock").objectReferenceValue=stock;ds.FindProperty("slotsRoot").objectReferenceValue=slots;ds.FindProperty("productScale").floatValue=1;ds.ApplyModifiedPropertiesWithoutUndo();
        var bounds=root.GetComponent<MeshFilter>().sharedMesh.bounds;
        var box=root.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;
        ShopUiSetup.Set(root.AddComponent<ShelfHighlight>(),"boundsCollider",box);
        string prefab=art+"/Products/"+source.key+".prefab";PrefabUtility.SaveAsPrefabAsset(root,prefab);
        string path=data+"/Fixtures/"+source.key+".asset";var item=AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
        if(item==null){item=ScriptableObject.CreateInstance<BuildItemData>();AssetDatabase.CreateAsset(item,path);}
        item.id="shelf_"+source.key;item.displayName=source.name;item.description="Display compatible goods using the fitted shelf slots.";
        item.price=400+number*35;item.weightKg=25;item.cargoVolumeM3=.3f;item.category=BuildCategory.Shelf;
        item.footprint=new Vector2Int(Mathf.CeilToInt(bounds.size.x/.275055f),Mathf.CeilToInt(bounds.size.z/.275055f));
        item.alignModelToFootprint=true;item.placementBounds=bounds;item.ruleFlags=template.ruleFlags;item.frontFacing=0;item.twoSidedAccess=number==2;
        item.previewValidMaterial=template.previewValidMaterial;item.previewInvalidMaterial=template.previewInvalidMaterial;
        item.prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);item.icon=ShopUiPreviews.Render(item.prefab,item.id,205);EditorUtility.SetDirty(item);
        return item;
    }
}
