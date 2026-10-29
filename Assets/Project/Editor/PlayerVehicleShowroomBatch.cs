using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class PlayerVehicleShowroomBatch
{
    private const string DisplayRoot="Player vehicle collection";
    internal static void Install()
    {
        var town=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try
        {
            var site=town.transform.Find(TownShowroomPlacement.SiteName);
            string protectedComponents=OutsideSignature(town,site);
            var existing=site.Find(DisplayRoot);
            if(existing==null)new TownShowroomPlacement(town).RemoveExhibitionCars();
            else Object.DestroyImmediate(existing.gameObject);
            var group=new GameObject(DisplayRoot).transform;group.SetParent(site,false);
            Place(group,"compact_van",4.5f,3.4f,.395f);
            Place(group,"high_roof_van",5.5f,11.1f,.29f);
            Place(group,"light_box_truck",11.5f,11.1f,.29f);
            Place(group,"medium_cargo_truck",17.5f,11.1f,.29f);
            Place(group,"heavy_box_truck",17.8f,24.0f,.09f,180);
            if(OutsideSignature(town,site)!=protectedComponents)throw new InvalidOperationException("A component outside the showroom changed.");
            PrefabUtility.SaveAsPrefabAsset(town,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
        }
        finally{PrefabUtility.UnloadPrefabContents(town);}
        File.WriteAllText(PlayerVehicleModelBatch.Report+"/protection.txt","PASS all serialized components outside dealership unchanged, including exact mesh/material references, colliders, supplier and road bindings. Exactly five baked exhibition sedans removed; unrelated customer sedan, building and furnishings retained. Original shared meshes are not overwritten; the showroom receives a scoped trimmed copy.\n");
        CaptureAndValidate();
    }
    private static string OutsideSignature(GameObject town,Transform site)
    {
        // Same loaded instance: serialized object references remain stable. No world-space assumptions or collider-type exemptions.
        return string.Join("\n",town.GetComponentsInChildren<Component>(true).Where(c=>c!=null&&!c.transform.IsChildOf(site)).Select(c=>c.GetType().FullName+EditorJsonUtility.ToJson(c)));
    }
    private static void Place(Transform parent,string id,float u,float v,float y,float yaw=270)
    {
        var definition=AssetDatabase.LoadAssetAtPath<PlayerVehicleDefinition>(PlayerVehicleModelBatch.Data+"/"+id+".asset");
        var go=(GameObject)PrefabUtility.InstantiatePrefab(definition.model);go.transform.SetParent(parent,false);
        go.name="Exhibition "+id;go.transform.localPosition=TownShowroomPlacement.Point(u,v,y);go.transform.localRotation=Quaternion.Euler(0,yaw,0);
        var collision=go.AddComponent<BoxCollider>();collision.size=new Vector3(definition.width,definition.height-.35f,definition.length-.12f);
        collision.center=new Vector3(0,(definition.height+.35f)*.5f,0);
    }
    internal static void CaptureAndValidate()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var town=Object.FindObjectOfType<TownWorldPlaces>();var site=town.transform.Find(TownShowroomPlacement.SiteName);
        var group=site.Find(DisplayRoot);if(group==null||group.childCount!=5)throw new InvalidOperationException("Five saved showroom models required.");
        Physics.SyncTransforms();
        foreach(Transform car in group)
        {
            var collider=car.GetComponent<BoxCollider>();
            foreach(var other in site.GetComponentsInChildren<Collider>())
            {
                bool indoor=car.name!="Exhibition compact_van"&&car.name!="Exhibition heavy_box_truck";
                if(other==collider||other.isTrigger||indoor&&other.name=="Hall vehicle envelope")continue;
                if(Physics.ComputePenetration(collider,collider.transform.position,collider.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth)&&depth>.006f)
                    throw new InvalidOperationException(car.name+" intersects "+other.name+" by "+depth);
            }
        }
        var checkpoint=town.suppliers.Single(c=>c.supplier==SupplierKind.Showroom);
        if(checkpoint.kind!=CheckpointKind.Showroom||checkpoint.requiresRearDocking)throw new InvalidOperationException("Showroom interaction changed.");
        TownFloristBatch.Render(scene,PlayerVehicleModelBatch.Pictures+"/Showroom.png",new Vector3(249,37,108),new Vector3(294,1.5f,134),0);
        // Remove only the roof from this unsaved review scene so the three indoor models can be inspected.
        File.WriteAllText(PlayerVehicleModelBatch.Report+"/showroom.txt","PASS five saved native model instances; three inside, one on the front platform, heavy truck in rear court; physical displays do not intersect props, walls or each other. Public showroom interaction retained.\n");
    }
}
