using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real scene, directed GPS, pointer input and UI acceptance in the disposable project only.</summary>
[InitializeOnLoad]
public static class TerritoryPurchaseQA
{
    private const string Active = "TerritoryPurchase.QA";
    private const string Report = "Library/TerritoryPurchaseQA";
    private const string Pictures = "Docs/TerritoryPurchaseUpdate";
    private static IEnumerator checks;
    private static DateTime deadline;
    private static string runtimeError;
    private static readonly StringBuilder results = new();
    static TerritoryPurchaseQA()
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
            TerritoryGroundOwnershipBake.Bake();
            var land=AssetDatabase.LoadAssetAtPath<RetailEmpireTycoon.Territory.TerritorySubdivisionRules>("Assets/Art/TownForgottenYard/Land24.asset");
            land.Configure(land.InitialBounds,RetailEmpireTycoon.Territory.TerritorySubdivisionRules.PriceByExpansionDepth(land.InitialBounds,land.Parcels));
            EditorUtility.SetDirty(land);AssetDatabase.SaveAssets();
            File.WriteAllText(Report + "/result.txt", "RUNNING\n");
            string save = Path.Combine(Application.persistentDataPath, "gameData.json");
            SessionState.SetString("TerritoryPurchase.Save", File.Exists(save) ? PinebrookDistrictBuild.Hash(save) : "missing");
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
        float fadeDeadline=Time.realtimeSinceStartup+5;
        while(Object.FindObjectsOfType<SceneFader>().Any(f=>f.fadeImage!=null&&f.fadeImage.gameObject.activeInHierarchy))
        {Require(Time.realtimeSinceStartup<fadeDeadline,"Scene entry fade did not finish.");yield return null;}
        Require(Object.FindObjectOfType<SaveManager>(true)==null,"Save writer present.");
        var trip=Object.FindObjectOfType<CityTrip>();trip.orders.save=null;
        var starter=Object.FindObjectOfType<StarterStoreInitialization>();var fresh=starter.CreateNewGame();
        var progression=Object.FindObjectOfType<StoreProgression>();progression.ApplySaveData(fresh.territory);
        starter.building.inventory.ApplySaveData(fresh.buildInventory,starter.catalog);
        var spawner=Object.FindObjectOfType<StorePrefabSpawner>();spawner.UsesModularStore=true;starter.ConfigureSite(true);spawner.Spawn(StoreLevelId.Lvl1);starter.ApplyNewGameBuildings(fresh);
        var rules=progression.LandRules;
        var expected=RetailEmpireTycoon.Territory.TerritorySubdivisionRules.PriceByExpansionDepth(rules.InitialBounds,rules.Parcels);
        Require(expected.All(p=>p.Price==rules.PriceOf(p.Id)),"Catalogue prices mismatch.");
        Require(rules.Parcels.Sum(p=>p.Price)>300000,"All land must cost more than starter funds.");
        var old=new TerritorySaveData{layoutVersion=0};old.purchased.Add(TerritoryId.Purple.ToString());
        var restored=rules.Restore(old);Require(rules.Parcels.Where(p=>p.LegacyRegion==TerritoryId.Purple).All(p=>restored.Contains(p.Id)),"Legacy land lost.");
        var wallet=Object.FindObjectOfType<RetailEmpireTycoon.Economy.MoneyController>();wallet.SetMoney(300000);
        var mode=Object.FindObjectOfType<TerritoryPurchaseModeManager>();
        trip.controls.CloseWindows();
        for(int i=0;i<4;i++)yield return null;
        Require(Object.FindObjectsOfType<TerritoryConstructionBarrier>(true).All(b=>b.GetComponentsInChildren<Renderer>().All(r=>!r.enabled||!r.gameObject.activeInHierarchy)),"Legacy fences visible.");
        Require(Object.FindObjectsOfType<TerritoryZone>().All(z=>z.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled)),"Land blocks ordinary play.");
        Capture("NormalPlay");
        mode.Enter();float ready=Time.realtimeSinceStartup+1.3f;while(Time.realtimeSinceStartup<ready)yield return null;
        var first=rules.Parcels.First(p=>progression.IsTerritoryAvailable(p.Id));
        var selector=Object.FindObjectOfType<TerritoryRaycaster>();
        var worldPoint=new Vector3(first.Bounds.center.x,0,first.Bounds.center.y);
        var groundScreen=Camera.main.WorldToScreenPoint(worldPoint);
        var groundPointer=new PointerEventData(EventSystem.current){position=groundScreen};
        var groundHits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(groundPointer,groundHits);
        File.WriteAllLines(Report+"/GroundHits.txt",new[]{"screen="+groundScreen+" camera="+Camera.main.name+" selector="+selector.enabled}.Concat(groundHits.Select(h=>h.gameObject.name+" parent="+h.gameObject.transform.parent?.name+" module="+h.module.GetType().Name)));
        Require(selector.TrySelectAtScreenPosition(groundScreen),"Ground click failed to select land.");
        Require(mode.Selected==first.Id,"Ground click selected the wrong parcel.");
        for(int i=0;i<3;i++)yield return null;
        Require(GameObject.Find("Land purchase ground outlines")!=null,"Overlay missing.");
        Capture("SelectedPlot");Capture("SelectedPlotSmall",1280,720);
        wallet.SetMoney(0);mode.BuySelected();yield return null;
        Require(!progression.State.IsPurchased(first.Id)&&wallet.Money==0,"Unaffordable purchase accepted.");
        wallet.SetMoney(300000);yield return null;yield return null;
        var buy=GameObject.Find("Territories UI").GetComponentsInChildren<Button>().Single(b=>b.name=="Buy land");
        ClickButton(buy);yield return null;
        var confirm=Object.FindObjectOfType<ConfirmPurchaseUI>(true);
        var yes=(Button)typeof(ConfirmPurchaseUI).GetField("_yes",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(confirm);
        Capture("Confirmation");ClickButton(yes);
        for(int i=0;i<8;i++)yield return null;
        Require(progression.State.IsPurchased(first.Id)&&wallet.Money==300000-first.Price,"Purchase did not charge exactly once.");
        Require(mode.IsActive,"Purchase closed land mode.");
        var scenery=Object.FindObjectOfType<TownExpansionScenery>();
        Require(scenery.plots.Sum(p=>p.model.GetComponentsInChildren<MeshFilter>(true).Count(f=>f.name.StartsWith("ParcelGround_")))==24,"Legacy ground not assigned to all parcels.");
        Require(scenery.plots.All(p=>p.model.activeSelf==!progression.State.IsPurchased(p.territory)),"Ground visibility differs from land ownership after purchase.");
        mode.BuySelected();yield return null;
        Require(wallet.Money==300000-first.Price,"Duplicate purchase charged twice.");
        var saved=progression.BuildSaveData();progression.ApplySaveData(saved);
        Require(progression.State.IsPurchased(first.Id),"Ownership round trip failed.");
        var locked=rules.Parcels.First(p=>!progression.IsTerritoryAvailable(p.Id)&&!progression.State.IsPurchased(p.Id));
        mode.Select(locked.Id);mode.BuySelected();yield return null;
        Require(!progression.State.IsPurchased(locked.Id),"Non-adjacent land purchased.");
        var second=rules.Parcels.First(p=>progression.IsTerritoryAvailable(p.Id));mode.Select(second.Id);
        var previousSelection=mode.Selected;ClickButton(buy);yield return null;
        Require(confirm.IsOpen&&mode.Selected==previousSelection,"Buy button did not preserve the selected parcel.");
        var no=(Button)typeof(ConfirmPurchaseUI).GetField("_no",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(confirm);
        ClickButton(no);yield return null;
        Require(!confirm.IsOpen&&!progression.State.IsPurchased(second.Id),"Cancel purchased land or left modal open.");
        int before=wallet.Money;mode.BuySelected();yield return null;wallet.SetMoney(0);yes.onClick.Invoke();
        for(int i=0;i<8;i++)yield return null;
        Require(!progression.State.IsPurchased(second.Id)&&wallet.Money==0&&mode.IsActive,"Confirmation did not recheck funds.");
        wallet.SetMoney(before);
        ready=Time.realtimeSinceStartup+1.3f;while(Time.realtimeSinceStartup<ready)yield return null;Capture("AfterPurchase");
        var done=GameObject.Find("Territories UI").GetComponentsInChildren<Button>().Single(b=>b.name=="Done");ClickButton(done);ready=Time.realtimeSinceStartup+1.3f;while(Time.realtimeSinceStartup<ready)yield return null;
        Require(GameObject.Find("Land purchase ground outlines")==null,"Overlay visible after exit.");
        Require(Object.FindObjectOfType<CameraModeController>().IsLocked==false,"Camera was not restored.");
        results.AppendLine("PASS: ground selection and UI raycasts; Buy land, confirmation, Cancel and Done pointer events; UI clicks preserve selected parcel.\nPASS: no fences/colliders in ordinary play; mode-only outlines and details; affordable adjacent purchase and exact debit; insufficient funds, duplicate and non-adjacent purchases rejected; mode retained; camera restored; saved and legacy ownership retained.");
        File.WriteAllLines(Report+"/Prices.csv",new[]{"Plot,Width,Depth,Area,Price"}.Concat(rules.Parcels.Select(p=>$"{p.Id},{p.Bounds.width},{p.Bounds.height},{p.Bounds.width*p.Bounds.height},{p.Price}")));
        var all=new TerritorySaveData{layoutVersion=RetailEmpireTycoon.Territory.TerritorySubdivisionRules.CurrentVersion};
        all.purchased=rules.Parcels.Select(p=>p.Id.ToString()).ToList();progression.ApplySaveData(all);
        Require(scenery.plots.All(p=>!p.model.activeInHierarchy),"Scenery remains on fully owned land.");
        Require(scenery.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("ParcelGround_")).All(r=>!r.gameObject.activeInHierarchy),"Legacy floor still visible after full purchase.");
        Camera.main.transform.SetPositionAndRotation(new Vector3(36,70,20),Quaternion.Euler(90,0,0));Capture("AllLandCleared");
        progression.ApplySaveData(saved);
        Require(scenery.plots.All(p=>p.model.activeSelf==!progression.State.IsPurchased(p.territory)),"Ground visibility incorrect after saved ownership restore.");
        progression.ApplySaveData(old);
        Require(scenery.plots.All(p=>p.model.activeSelf==!progression.State.IsPurchased(p.territory)),"Ground visibility incorrect after legacy save restore.");
        results.AppendLine("PASS: all 24 legacy ground pieces share parcel ownership; full clearing, partial save restore and legacy region migration hide the correct floors.");
        string save=Path.Combine(Application.persistentDataPath,"gameData.json");
        Require(SessionState.GetString("TerritoryPurchase.Save","")==(File.Exists(save)?PinebrookDistrictBuild.Hash(save):"missing"),"Player save changed.");
    }
    private static void ClickButton(Button button)
    {
        Require(button.interactable && button.IsInteractable(), "Button is disabled: "+button.name);
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)button.transform;
        var canvas=button.GetComponentInParent<Canvas>();
        var position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        var pointer=new PointerEventData(EventSystem.current){position=position,button=PointerEventData.InputButton.Left};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Require(hits.Count>0,"No pointer hit for "+button.name);
        var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        var selector=Object.FindObjectOfType<TerritoryRaycaster>();
        var mode=Object.FindObjectOfType<TerritoryPurchaseModeManager>();var selected=mode.Selected;
        Require(!selector.TrySelectAtScreenPosition(position),"UI click leaked into land selection: "+button.name);
        Require(mode.Selected==selected,"UI click changed selected parcel: "+button.name);
        Require(handler==button.gameObject,"Click blocked: "+button.name+" by "+string.Join(" > ",hits.Select(h=>h.gameObject.name)));
        ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerClickHandler);
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
