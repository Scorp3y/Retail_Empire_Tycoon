using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Editor.TownKit;
using RetailEmpireTycoon.Parking;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object=UnityEngine.Object;

public static class ConstructionWorkspaceBatch
{
    public const string Root="Assets/Art/ConstructionWorkspace",Report="Library/ConstructionWorkspaceQA";
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);
        try
        {
            Directory.CreateDirectory(Root+"/Prefabs");AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.GamePath);
            var starter=Object.FindObjectOfType<StarterStoreInitialization>(true);var catalog=starter.catalog;
            var definitions=catalog.Items.Concat(Resources.LoadAll<BuildItemData>("MultiStorey")).Where(item=>item!=null).Distinct().ToArray();
            var outputs=new HashSet<string>();
            if(starter.blueprint.initialState.shopProportionsVersion<ShopProportions.Version)
            {
                var copies=new Dictionary<string,GameObject>();
                GameObject Copy(GameObject original)
                {
                    if(original==null)return null;
                    string path=AssetDatabase.GetAssetPath(original);if(copies.TryGetValue(path,out var saved))return saved;
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(original);
                    try
                    {
                        go.transform.localScale*=1.1f;
                        var bay=go.GetComponent<ParkingBay>();if(bay!=null){bay.width*=1.1f;bay.length*=1.1f;}
                        string target=Root+"/Prefabs/"+Path.GetFileNameWithoutExtension(path)+"_"+AssetDatabase.AssetPathToGUID(path).Substring(0,8)+".prefab";
                        saved=PrefabUtility.SaveAsPrefabAsset(go,target);copies.Add(path,saved);return saved;
                    }
                    finally{Object.DestroyImmediate(go);}
                }
                Bounds Scale(Bounds bounds)=>new Bounds(bounds.center*1.1f,bounds.size*1.1f);
                foreach(var item in definitions)
                {
                    item.prefab=Copy(item.prefab);item.previousWallPrefab=Copy(item.previousWallPrefab);item.previousParkingPrefab=Copy(item.previousParkingPrefab);
                    item.placementBounds=Scale(item.placementBounds);item.previousWallBounds=Scale(item.previousWallBounds);item.previousParkingBounds=Scale(item.previousParkingBounds);
                    item.placementAlignmentOffset*=1.1f;item.previousWallAlignmentOffset*=1.1f;item.wallCellSize*=1.1f;item.legacyWallDepth*=1.1f;
                }
                starter.building.grid.cellSize=ShopProportions.CellSize;starter.floors.surfaceHeight*=1.1f;
                starter.land.initialBounds=new Rect(starter.land.initialBounds.position*1.1f,starter.land.initialBounds.size*1.1f);
                foreach(var plot in starter.land.plots)plot.bounds=new Rect(plot.bounds.position*1.1f,plot.bounds.size*1.1f);
                ShopProportions.Upgrade(starter.blueprint.initialState,starter.building.grid,catalog);
                starter.blueprint.cameraFocus*=1.1f;EditorUtility.SetDirty(starter.blueprint);
            }
            foreach(var item in definitions)
            {item.displayName=ShopText.Item(item);EditorUtility.SetDirty(item);outputs.Add(AssetDatabase.GetAssetPath(item));}
            outputs.Add(AssetDatabase.GetAssetPath(starter.blueprint));
            var overlay=starter.building.gridOverlay;overlay.floors=starter.floors;overlay.yOffset=.002f;overlay.lineWidth=.008f;overlay.gridColor=new Color(.17f,.29f,.23f,.38f);
            var workspace=Object.FindObjectOfType<BuildEditorHud>(true);
            ShopGameplaySetup.Set(Object.FindObjectOfType<GameplayControls>(true),"construction",workspace);
            Object.FindObjectOfType<ShopHudPresenter>(true).construction=workspace;
            Object.FindObjectOfType<ConstructionGuide>(true).enabled=false;
            EditorSceneManager.SaveScene(scene);outputs.Add(CityUpdateSetup.GamePath);
            AssetDatabase.SaveAssets();
            var translations=new Dictionary<string,string>(StringComparer.Ordinal);
            var ru=AssetDatabase.LoadAssetAtPath<StringTable>("Assets/Localization/UI_Texts/UI_Texts_ru.asset");
            var en=AssetDatabase.LoadAssetAtPath<StringTable>("Assets/Localization/UI_Texts/UI_Texts_en.asset");
            foreach(var entry in ru.Values)
            {
                var target=en.GetEntry(entry.KeyId);
                if(!string.IsNullOrWhiteSpace(entry.LocalizedValue) && target!=null)translations[entry.LocalizedValue]=target.LocalizedValue;
            }
            var untranslated=new SortedSet<string>(StringComparer.Ordinal);
            translations["Загрузка..."]="Loading...";
            translations["Удалить сохранение"]="Delete save";
            string English(string text)
            {
                if(string.IsNullOrEmpty(text) || !text.Any(character=>character>='А'&&character<='я'||character=='Ё'||character=='ё'))return text;
                if(translations.TryGetValue(text,out var translated))return translated;
                untranslated.Add(text);return text;
            }
            foreach(string path in new[]{CityUpdateSetup.GamePath,CityUpdateSetup.CityPath,"Assets/Project/Scenes/Menu.unity","Assets/Project/Scenes/Loading.unity"})
            {
                var loaded=EditorSceneManager.OpenScene(path);
                foreach(var root in loaded.GetRootGameObjects())
                {
                    foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))text.text=English(text.text);
                    foreach(var text in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))text.text=English(text.text);
                    foreach(var text in root.GetComponentsInChildren<TextMesh>(true))text.text=English(text.text);
                    foreach(var tutorial in root.GetComponentsInChildren<TutorialDialogue>(true))tutorial.dialogueLines=tutorial.dialogueLines.Select(English).ToList();
                    foreach(var checkpoint in root.GetComponentsInChildren<DeliveryCheckpoint>(true))checkpoint.displayName=checkpoint.kind==CheckpointKind.Return?"My store":RetailEmpireTycoon.Logistics.SupplierRouting.Name(checkpoint.supplier);
                }
                EditorSceneManager.SaveScene(loaded);outputs.Add(path);
            }
            File.WriteAllLines(Report+"/untranslated-scene-copy.txt",untranslated);
            AssetDatabase.SaveAssets();File.WriteAllLines(Report+"/outputs.txt",outputs.OrderBy(path=>path,StringComparer.Ordinal));
            File.WriteAllText(Report+"/build.txt","PASS private 1.1x kit; proportions version 2; actual-surface grid; construction bindings; English scene copy migration. Untranslated="+untranslated.Count+"\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    public static void Record(string stage)
    {
        var files=File.ReadAllLines(Report+"/outputs.txt").Concat(Directory.GetFiles(Root,"*",SearchOption.AllDirectories))
            .Concat(Directory.GetFiles("Assets/Project/Scripts","*.cs",SearchOption.AllDirectories))
            .Concat(Directory.GetFiles("Assets/Localization","*.cs",SearchOption.TopDirectoryOnly))
            .Concat(AssetDatabase.GetDependencies(File.ReadAllLines(Report+"/outputs.txt"),true).Where(path=>path.StartsWith("Assets/",StringComparison.Ordinal)&&File.Exists(path)))
            .Concat(Directory.GetFiles("Assets/Project/Editor","ConstructionWorkspace*.cs"))
            .Concat(new[]{"Assets/Project/Editor/MultiStoreyValidation.cs"})
            .Distinct().OrderBy(path=>path,StringComparer.Ordinal);
        File.WriteAllLines(Report+"/"+stage+"-inputs.sha256",files.Select(path=>TrafficFleetBatch.Hash(path)+" "+path.Replace('\\','/')));
    }
}
