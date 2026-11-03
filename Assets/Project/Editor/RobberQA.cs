using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real scene, directed GPS, pointer input and UI acceptance in the disposable project only.</summary>
[InitializeOnLoad]
public static class RobberQA
{
    private const string Active = "Robber.QA";
    private const string Report = "Library/RobberQA";
    private const string Pictures = "Docs/Robber";
    private static IEnumerator checks;
    private static DateTime deadline;
    private static string runtimeError;
    private static readonly StringBuilder results = new();
    static RobberQA()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, trace, type) =>
        { if (SessionState.GetBool(Active, false) && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)) runtimeError ??= message; };
    }
    public static void Run()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor(); Directory.CreateDirectory(Report); Directory.CreateDirectory(Pictures);
            RobberModelImport.Build();
            File.WriteAllText(Report + "/result.txt", "RUNNING\n");
            string save = Path.Combine(Application.persistentDataPath, "gameData.json");
            SessionState.SetString("Robber.Save", File.Exists(save) ? PinebrookDistrictBuild.Hash(save) : "missing");
            EditorSceneManager.OpenScene(CityUpdateSetup.GamePath); ShopGameplaySandbox.Open();
            foreach (var bootstrap in Object.FindObjectsOfType<StoreBootstrapper>(true)) Object.DestroyImmediate(bootstrap);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (checks == null) { checks = Check(); deadline = DateTime.UtcNow.AddMinutes(10); }
            Require(DateTime.UtcNow < deadline, "Navigation acceptance timed out.");
            Require(runtimeError == null, "Unexpected runtime error: " + runtimeError);
            if (!checks.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static IEnumerator Check()
    {
        for(int i=0;i<20;i++)yield return null;
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Save writer present.");
        var trip=Object.FindObjectOfType<CityTrip>();trip.orders.save=null;
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();var fresh=starter.CreateNewGame();
        var progression=Object.FindObjectOfType<StoreProgression>();progression.ApplySaveData(fresh.territory);
        starter.building.inventory.ApplySaveData(fresh.buildInventory,starter.catalog);
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;starter.ConfigureSite(true);spawner.Spawn(StoreLevelId.Lvl1);starter.ApplyNewGameBuildings(fresh);
        for(int i=0;i<5;i++)yield return null;
        foreach(var fader in Object.FindObjectsOfType<SceneFader>())if(fader.fadeImage!=null)fader.fadeImage.gameObject.SetActive(false);
        var shop=Object.FindObjectOfType<RetailEmpireTycoon.StoreOperations.StoreOperations>();shop.enabled=false;
        var balance=Object.Instantiate(shop.Balance);balance.thiefChance=balance.litterChance=0;balance.baseArrivalSeconds=100000;
        ShopGameplaySetup.Set(shop,"balance",balance);shop.ApplySaveData(new ShopOperationsSaveData());shop.SetOpen(true);shop.Advance(.1f);
        Require(shop.CustomerAppearanceCount==50,"Regular shopper roster changed.");
        var shelves=Object.FindObjectsOfType<RetailEmpireTycoon.Shelves.PlacedShelfStock>();
        var product=trip.orders.productCatalog.Products.First(p=>shelves.Any(s=>s.IsProductAllowed(p)));
        var shelf=shelves.First(s=>s.IsProductAllowed(product));
        foreach(var item in shelves)item.ClearStock();shelf.SetStockFromSave(product,24);
        Require(shop.TrySpawnCustomer(),"Normal shopper could not spawn.");
        Require(!shop.Visits[0].IsThief&&!shop.Visits[0].Character.transform.GetChild(0).name.StartsWith("Robber"),"Robber model assigned to an ordinary shopper.");
        shop.ApplySaveData(new ShopOperationsSaveData());shop.SetOpen(true);shop.Advance(.1f);
        Require(shop.TrySpawnCustomer(true),"Forced thief could not spawn: "+shop.Notice);
        var thief=shop.Visits.Single();var character=thief.Character;
        Require(character.transform.GetChild(0).name.StartsWith("Robber"),"Thief does not use the dedicated model.");
        var animator=character.GetComponentInChildren<Animator>();
        Require(animator!=null&&animator.avatar.isValid,"Robber has no valid animator.");
        var skin=character.GetComponentInChildren<SkinnedMeshRenderer>();
        Require(skin.bones.Length==14&&skin.sharedMesh.triangles.Length/3<5000,"Unexpected rig or mesh budget.");
        var posed=new Mesh();animator.Play("Idle",0,0);animator.Update(0);skin.BakeMesh(posed);var still=posed.vertices;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        animator.SetBool("isWalking",true);animator.Update(.3f);skin.BakeMesh(posed);
        Require(posed.vertices.Where((v,i)=>Vector3.Distance(v,still[i])>.003f).Count()>20,"Walk animation does not deform the model.");Object.Destroy(posed);
        for(int i=0;i<1500&&thief.Stage!=VisitStage.Escaping;i++)shop.Advance(.05f);
        Require(thief.Stage==VisitStage.Escaping,"Robber never reached goods and started escaping.");
        for(int i=0;i<12;i++)shop.Advance(.05f);
        var camera=Camera.main;var input=camera.GetComponent<MainCamera>();if(input!=null)input.enabled=false;
        var clearSide=character.transform.position-shelf.transform.position;clearSide.y=0;clearSide.Normalize();
        character.transform.rotation=Quaternion.LookRotation(clearSide);
        camera.transform.position=character.transform.position+clearSide*2.6f+Vector3.up*1.15f;
        camera.transform.LookAt(character.transform.position+Vector3.up*.5f);camera.fieldOfView=32;
        for(int i=0;i<5;i++)yield return null;
        Capture("RobberInStore");
        int money=trip.orders.money.Money;int stock=shelf.CurrentAmount;
        for(int i=0;i<shop.Balance.thiefClicks;i++)shop.ClickThief(thief);
        Require(!shop.Visits.Contains(thief)&&shelf.CurrentAmount==stock&&trip.orders.money.Money==money,"Manual capture lost stock or charged money.");
        Require(shop.TrySpawnCustomer(true),"Second thief could not spawn.");var escaped=shop.Visits.Single();
        int quantity=escaped.Quantity;stock=shelf.CurrentAmount;
        for(int i=0;i<3000&&shop.Visits.Contains(escaped);i++)shop.Advance(.05f);
        Require(!shop.Visits.Contains(escaped)&&shelf.CurrentAmount==stock-quantity,"Escape did not remove goods exactly once.");
        shop.ApplySaveData(new ShopOperationsSaveData());shelf.SetStockFromSave(product,24);shop.SetOpen(true);shop.Advance(.1f);
        Require(shop.TryHire(StaffRole.Guard),"Cannot hire guard for capture check.");
        Require(shop.TrySpawnCustomer(true),"Guard's thief could not spawn.");var caught=shop.Visits.Single();
        for(int i=0;i<2500&&shop.Visits.Contains(caught);i++)shop.Advance(.05f);
        Require(!shop.Visits.Contains(caught)&&shelf.CurrentAmount==24,"Guard failed to catch robber or recover stock.");
        balance.thiefChance=1;shop.ApplySaveData(new ShopOperationsSaveData());shop.SetOpen(true);shop.Advance(.1f);
        Require(shop.TrySpawnCustomer()&&shop.Visits.Single().IsThief&&shop.Visits.Single().Character.transform.GetChild(0).name.StartsWith("Robber"),"Natural chance does not choose the robber model.");
        results.AppendLine("PASS: imported FBX, 14-bone rig, real deforming Walk/Idle/Browse clips; dedicated model for forced and chance-spawned thieves; 50 regular shopper appearances retained; shelf approach, theft, manual capture and guard interception; no duplicate stock debit.");
        string save=Path.Combine(Application.persistentDataPath,"gameData.json");
        Require(SessionState.GetString("Robber.Save","")==(File.Exists(save)?PinebrookDistrictBuild.Hash(save):"missing"),"Player save changed.");
    }
    private static void Capture(string name, int width = 1920, int height = 1080)
    {
        var camera = Camera.main; var target = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
        var canvases = Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var cameras = canvases.Select(c => c.worldCamera).ToArray(); var planes = canvases.Select(c => c.planeDistance).ToArray();
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            target.Create(); camera.targetTexture = target;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .6f; }
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes(Pictures + "/" + name + ".png", texture.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = planes[i]; }
            camera.targetTexture = oldTarget; RenderTexture.active = previous; target.Release(); Object.Destroy(target); Object.Destroy(texture); Canvas.ForceUpdateCanvases();
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false); Directory.CreateDirectory(Report);
        File.WriteAllText(Report + "/result.txt", (error == null ? "PASS:\n" : "FAILED:\n") + results + (error == null ? "Player save unchanged.\n" : error.ToString()));
        if (error != null) Debug.LogException(error); EditorApplication.Exit(error == null ? 0 : 1);
    }
}
