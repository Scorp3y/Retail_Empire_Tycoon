using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using RetailEmpireTycoon.Parking;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Imports the five Blender employees and connects the delivery service in the isolated project only.</summary>
public static class StaffUpdateBatch
{
    public const string Root="Assets/Art/Staff",Report="Library/StaffQA";
    public static readonly string[] Roles={"Cashier","Guard","Stocker","Cleaner","Driver"};
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);
        try
        {
            Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(Root+"/Portraits");AssetDatabase.Refresh();
            var material=PrepareMaterial();
            var prefabs=Roles.Select((role,index)=>MakeEmployee(role,index,material)).ToArray();
            var theme=Resources.Load<ShopUiTheme>("ShopUi/Theme");theme.staffPortraits=new Sprite[5];
            for(int i=0;i<5;i++)theme.staffPortraits[i]=ShopUiPreviews.Render(prefabs[i],"Staff_"+Roles[i],25);
            EditorUtility.SetDirty(theme);AssetDatabase.SaveAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
            EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            var pickup=Object.FindObjectOfType<CityWorld>(true).pickup;
            var model=Object.Instantiate(pickup.gameObject);model.name="DriverStarterPickup";
            var wheels=model.GetComponent<PickupDrive>().wheels.Select(w=>new PlayerVehicleModel.Wheel{pivot=w.visual,front=w.front}).ToArray();
            foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            foreach(var body in model.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(body);
            model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);model.transform.localScale=Vector3.one;
            model.AddComponent<PlayerVehicleModel>().wheels=wheels;
            var vehiclePrefab=PrefabUtility.SaveAsPrefabAsset(model,Root+"/DriverStarterPickup.prefab");Object.DestroyImmediate(model);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
            var shop=Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>(true);
            var serialized=new SerializedObject(shop);var models=serialized.FindProperty("staffModels");models.arraySize=5;
            for(int i=0;i<5;i++)models.GetArrayElementAtIndex(i).objectReferenceValue=prefabs[i];serialized.ApplyModifiedPropertiesWithoutUndo();
            var trip=Object.FindObjectOfType<CityTrip>(true);var driver=trip.GetComponent<DriverDelivery>()??trip.gameObject.AddComponent<DriverDelivery>();
            driver.trip=trip;driver.shop=shop;driver.orders=trip.orders;driver.parking=Object.FindObjectOfType<ParkingTraffic>(true);
            driver.town=Object.FindObjectOfType<TownWorldPlaces>(true);driver.traffic=driver.town.GetComponentInChildren<TownRoadTraffic>(true);driver.starterVehicleModel=vehiclePrefab;
            foreach(var wheel in trip.parkedPickup.GetComponentsInChildren<WheelCollider>(true))wheel.enabled=false;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            var trace=new System.Text.StringBuilder();
            foreach(var point in driver.town.suppliers.Where(p=>p.kind==CheckpointKind.Depot))
            {
                var route=new DriverRoadRoute(driver.traffic.Network);
                var a=driver.traffic.transform.InverseTransformPoint(driver.parking.roadEntry.position);
                var court=driver.town.GetComponentsInChildren<SupplierLoadingCourt>().FirstOrDefault(c=>c.checkpoint==point);
                var b=court!=null?court.streetAccess:driver.traffic.transform.InverseTransformPoint(point.transform.position);
                if(!route.Plan(a,b)||!new DriverRoadRoute(driver.traffic.Network).Plan(b,a))throw new InvalidOperationException("Supplier has no round-trip route: "+point.displayName);
                trace.AppendLine(point.supplier+": "+route.Lanes.Count+" directed lanes to actual yard entrance, final road connection "+Vector3.Distance(route.End.Position,b).ToString("0.0")+"m");
            }
            File.WriteAllText(Report+"/routes.txt","PASS all supplier destinations have directed road return routes.\n"+trace);
            StaffUpdateReview.CaptureAll();
            File.WriteAllLines(Report+"/outputs.txt",Directory.GetFiles(Root,"*",SearchOption.AllDirectories).Append(Root+".meta")
                .Concat(theme.staffPortraits.SelectMany(sprite=>new[]{AssetDatabase.GetAssetPath(sprite),AssetDatabase.GetAssetPath(sprite)+".meta"}))
                .Concat(new[]{CityUpdateSetup.GamePath,AssetDatabase.GetAssetPath(theme)})
                .Concat(NewScripts().Select(p=>p+".meta")).Distinct().Select(p=>p.Replace('\\','/')));
            File.WriteAllText(Report+"/build.txt","PASS five Blender employees, fourteen-bone rigs, role work animations, portraits, fifth staff role and connected player-car delivery service.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    /// <summary>Replaces appearance assets in place, preserving prefab GUIDs, scene assignments and all job logic.</summary>
    public static void RefreshModels()=>RefreshModels(false);
    public static void RefreshModelsAndPlay()=>RefreshModels(true);
    private static void RefreshModels(bool verifyGameplay)
    {
        TownWorldSafety.RequireIsolatedEditor();const string report="Library/StaffStyleQA";Directory.CreateDirectory(report);
        try
        {
            AssetDatabase.Refresh();var material=PrepareMaterial();
            var prefabs=Roles.Select((role,index)=>MakeEmployee(role,index,material)).ToArray();
            var theme=Resources.Load<ShopUiTheme>("ShopUi/Theme");
            for(int i=0;i<5;i++)theme.staffPortraits[i]=ShopUiPreviews.Render(prefabs[i],"Staff_"+Roles[i],25);
            EditorUtility.SetDirty(theme);AssetDatabase.SaveAssets();StaffUpdateReview.CaptureAll();StaffUpdateReview.CaptureAgainstShopper();
            File.WriteAllLines(report+"/outputs.txt",Roles.SelectMany(role=>new[]{Root+"/Models/"+role+".fbx",Root+"/Prefabs/"+role+".prefab",Root+"/"+role+".controller","Assets/Art/ShopUi/Items/Staff_"+role+".png"})
                .Concat(new[]{Root+"/ShopperPalette.png",Root+"/Staff.mat"}).SelectMany(p=>new[]{p,p+".meta"}).Append(AssetDatabase.GetAssetPath(theme)));
            File.WriteAllText(report+"/build.txt","PASS five employees now use current mini-character shopper geometry, original face meshes, common palette and the same fourteen-bone customer rig. Existing role assignments and driver logic preserved.\n");
            if(verifyGameplay)StaffUpdatePlay.Run();else EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    private static Material PrepareMaterial()
    {
        var texture=(TextureImporter)AssetImporter.GetAtPath(Root+"/ShopperPalette.png");texture.mipmapEnabled=false;
        texture.filterMode=FilterMode.Bilinear;texture.textureCompression=TextureImporterCompression.Uncompressed;texture.SaveAndReimport();
        var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Staff.mat");
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Root+"/Staff.mat");}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ShopperPalette.png"));material.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(material);return material;
    }
    public static void PortraitsAndPlay()
    {
        TownWorldSafety.RequireIsolatedEditor();var theme=Resources.Load<ShopUiTheme>("ShopUi/Theme");
        for(int i=0;i<5;i++)theme.staffPortraits[i]=ShopUiPreviews.Render(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+Roles[i]+".prefab"),"Staff_"+Roles[i],25);
        EditorUtility.SetDirty(theme);AssetDatabase.SaveAssets();StaffUpdatePlay.Run();
    }
    private static string[] NewScripts()=>new[]{"Assets/Project/Scripts/StoreOperations/StaffAppearance.cs"}
        .Concat(Directory.GetFiles("Assets/Project/Scripts/Logistics","Driver*.cs")).Concat(Directory.GetFiles("Assets/Project/Editor","Staff*.cs")).Select(p=>p.Replace('\\','/')).ToArray();
    public static void Record()
    {
        var inputs=File.ReadAllLines(Report+"/outputs.txt")
            .Concat(Directory.GetFiles("Assets/Project/Scripts","*.cs",SearchOption.AllDirectories))
            .Concat(NewScripts()).Concat(AssetDatabase.GetDependencies(new[]{CityUpdateSetup.GamePath,CityUpdateSetup.CityPath},true).Where(p=>p.StartsWith("Assets/")&&File.Exists(p))).Distinct().OrderBy(p=>p).ToArray();
        using var hash=System.Security.Cryptography.SHA256.Create();
        File.WriteAllLines(Report+"/inputs.sha256",inputs.Select(p=>{using var file=File.OpenRead(p);return BitConverter.ToString(hash.ComputeHash(file)).Replace("-","")+" "+p.Replace('\\','/');}));
    }
    private static GameObject MakeEmployee(string role,int index,Material material)
    {
        string path=Root+"/Models/"+role+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.importAnimation=true;importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects=false;importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.importBlendShapes=false;importer.isReadable=true;importer.SaveAndReimport();
        var clips=importer.defaultClipAnimations;
        foreach(var clip in clips)
        {
            clip.name=new[]{"Idle","Walk","Browse"}.Single(n=>clip.name.IndexOf(n,StringComparison.OrdinalIgnoreCase)>=0);
            clip.loopTime=true;clip.loopPose=true;clip.lockRootPositionXZ=true;clip.lockRootRotation=true;clip.keepOriginalPositionXZ=true;clip.keepOriginalOrientation=true;
        }
        importer.clipAnimations=clips;importer.SaveAndReimport();
        var animations=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"/"+role+".controller");
        if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(Root+"/"+role+".controller");
        else
        {
            var existing=controller.layers[0].stateMachine;
            foreach(var transition in existing.anyStateTransitions)existing.RemoveAnyStateTransition(transition);
            foreach(var state in existing.states)existing.RemoveState(state.state);
            controller.parameters=Array.Empty<AnimatorControllerParameter>();
        }
        controller.AddParameter("isWalking",AnimatorControllerParameterType.Bool);controller.AddParameter("isBrowsing",AnimatorControllerParameterType.Bool);
        var machine=controller.layers[0].stateMachine;
        foreach(var clip in animations)
        {
            var state=machine.AddState(clip.name);state.motion=clip;if(clip.name=="Idle")machine.defaultState=state;
            if(clip.name=="Walk")state.speed=2;
            var transition=machine.AddAnyStateTransition(state);transition.hasExitTime=false;transition.duration=.12f;transition.canTransitionToSelf=false;
            transition.AddCondition(clip.name=="Walk"?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"isWalking");
            if(clip.name!="Walk")transition.AddCondition(clip.name=="Browse"?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"isBrowsing");
        }
        var root=new GameObject(role);
        try
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));instance.transform.SetParent(root.transform,false);
            var marker=instance.GetComponentsInChildren<Transform>().Single(t=>t.name=="FacingMarker");
            if(instance.transform.InverseTransformPoint(marker.position).z<0)instance.transform.localRotation=Quaternion.Euler(0,180,0);
            var animator=instance.GetComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.Rebind();animator.Update(0);
            animations.Single(c=>c.name=="Idle").SampleAnimation(instance,0);
            foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.sharedMaterial=material;skin.quality=SkinQuality.Bone1;var b=skin.localBounds;b.Expand(.5f);skin.localBounds=b;}
            var body=instance.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name=="Body");var baked=new Mesh();body.BakeMesh(baked,false);
            var vertices=baked.vertices.Select(v=>root.transform.InverseTransformPoint(body.transform.TransformPoint(v))).ToArray();
            var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);Object.DestroyImmediate(baked);
            root.AddComponent<ShopperAppearance>().Configure(index+1,bounds);
            var appearance=root.AddComponent<StaffAppearance>();appearance.role=(StaffRole)index;
            appearance.workTools=instance.GetComponentsInChildren<Transform>().Single(t=>t.name=="WorkTools").gameObject;appearance.workTools.SetActive(false);
            if(animator.avatar==null||!animator.avatar.isValid||body.bones.Length!=14)throw new InvalidOperationException("Invalid employee rig: "+role);
            return PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+role+".prefab");
        }
        finally{Object.DestroyImmediate(root);}
    }
}
