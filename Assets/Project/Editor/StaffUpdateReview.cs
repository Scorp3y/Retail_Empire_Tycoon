using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Review images rendered from the actual imported employee prefabs and animation clips.</summary>
public static class StaffUpdateReview
{
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory("Docs/StaffUpdate");
        try{CaptureAll();EditorApplication.Exit(0);}
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    public static void CaptureAll(){Capture(false);Capture(true);}
    public static void CaptureAgainstShopper()=>Capture(false,true);
    private static void Capture(bool working,bool comparison=false)
    {
        var scene=EditorSceneManager.NewPreviewScene();RenderTexture target=null;Texture2D image=null;
        var previous=RenderTexture.active;
        GameObject New(string name,params Type[] components){var node=new GameObject(name,components);node.layer=30;SceneManager.MoveGameObjectToScene(node,scene);return node;}
        try
        {
            var camera=New("Staff review camera").AddComponent<Camera>();camera.scene=scene;camera.enabled=false;
            camera.transform.position=new Vector3(0,1.45f,8);camera.transform.rotation=Quaternion.LookRotation(new Vector3(0,.55f,0)-camera.transform.position);
            camera.orthographic=true;camera.orthographicSize=1.15f;camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ShopUiTheme.Paper;
            int width=comparison?2500:2100;
            target=new RenderTexture(width,700,24){antiAliasing=4};target.Create();camera.targetTexture=target;
            foreach(var angle in new[]{new Vector3(35,-25,0),new Vector3(25,145,0)})
            {var light=New("Soft studio light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=angle.y<0?1.25f:.5f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(angle);}
            var canvas=New("Staff labels",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var ui=new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            var order=comparison?new[]{"Reference","Cashier","Guard","Cleaner","Stocker","Driver"}:new[]{"Cashier","Guard","Cleaner","Stocker","Driver"};
            for(int i=0;i<order.Length;i++)
            {
                bool reference=order[i]=="Reference";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(reference?"Assets/Art/Shoppers/Prefabs/Shopper_09.prefab":StaffUpdateBatch.Root+"/Prefabs/"+order[i]+".prefab");
                var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);model.GetComponent<ShopperAppearance>().FitHeight(1.25f);
                model.transform.position=new Vector3(((order.Length-1)*.5f-i)*1.3f,0,0);model.transform.rotation=Quaternion.Euler(0,-18,0);
                foreach(var node in model.GetComponentsInChildren<Transform>(true))node.gameObject.layer=30;
                model.GetComponent<StaffAppearance>()?.SetWorking(working);
                var animator=model.GetComponentInChildren<Animator>();animator.enabled=false;
                animator.runtimeAnimatorController.animationClips.First(c=>c.name==(working?"Browse":"Idle")).SampleAnimation(animator.gameObject,0);
                var point=camera.WorldToViewportPoint(model.transform.position+Vector3.down*.13f);
                var label=ui.Label(canvas.transform,reference?"CURRENT NPC":order[i]=="Guard"?"SECURITY":order[i].ToUpperInvariant(),Vector2.zero,new Vector2(240,44),24);label.alignment=TMPro.TextAlignmentOptions.Center;
                label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(point.x,point.y);label.rectTransform.pivot=new Vector2(.5f,.5f);label.rectTransform.anchoredPosition=Vector2.zero;
            }
            var title=ui.Label(canvas.transform,comparison?"CURRENT NPC + STAFF / SAME MINI-CHARACTER STYLE":working?"STORE STAFF / WORK ANIMATIONS":"STORE STAFF / IMPORTED UNITY MODELS",new Vector2(0,-25),new Vector2(width,50),32);title.alignment=TMPro.TextAlignmentOptions.Center;
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;image=new Texture2D(width,700,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,width,700),0,0);image.Apply();File.WriteAllBytes("Docs/StaffUpdate/"+(comparison?"StaffComparison":working?"StaffWorking":"StaffLineup")+".png",image.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;if(target!=null){target.Release();Object.DestroyImmediate(target);}if(image!=null)Object.DestroyImmediate(image);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
