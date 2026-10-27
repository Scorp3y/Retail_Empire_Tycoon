using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.StoreOperations;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Actual imported, skinned Unity assets in a numbered gallery, not a generated concept image.</summary>
public static class ShopperFleetReview
{
    public static void CaptureStudy(GameObject[] prefabs)
    {
        Capture(prefabs,"StyleStudy.png",3,null,null);
    }
    public static void Capture(GameObject[] prefabs)
    {
        Capture(prefabs,"UnityLineup.png",10,null,null);
    }
    public static void CaptureMotion(GameObject prefab)
    {
        var prefabs=Enumerable.Repeat(prefab,12).ToArray();
        var states=Enumerable.Range(0,12).Select(i=>new[]{"Idle","Walk","Browse"}[i/4]).ToArray();
        var phases=Enumerable.Range(0,12).Select(i=>(i%4)*.25f).ToArray();
        Capture(prefabs,"UnityMotion.png",4,states,phases);
    }
    private static void Capture(GameObject[] prefabs,string outputName,int columns,string[] states,float[] phases)
    {
        var scene=EditorSceneManager.NewPreviewScene();RenderTexture target=null;Texture2D output=null;
        var previous=RenderTexture.active;
        try
        {
            int rows=Mathf.CeilToInt(prefabs.Length/(float)columns);
            float halfHeight=Mathf.Max(1.3f,rows*.65f+.35f);
            float halfWidth=columns*.57f+.5f;
            var camera=New(scene,"Shopper review camera").AddComponent<Camera>();camera.enabled=false;camera.scene=scene;
            camera.orthographic=true;camera.orthographicSize=halfHeight;camera.cullingMask=1<<30;
            camera.transform.position=new Vector3(0,0,25);camera.transform.rotation=Quaternion.Euler(0,180,0);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ShopUiTheme.Paper;
            int width=columns==10?2400:1400;
            target=new RenderTexture(width,Mathf.RoundToInt(width*halfHeight/halfWidth),24);target.antiAliasing=4;target.Create();camera.targetTexture=target;
            Light(scene,new Vector3(30,-30,0),1.35f);Light(scene,new Vector3(30,150,0),.6f);
            var canvas=New(scene,"Review labels",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var ui=new ShopUi(Resources.Load<ShopUiTheme>("ShopUi/Theme"));
            for(int i=0;i<prefabs.Length;i++)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[i],scene);
                instance.GetComponent<ShopperAppearance>().FitHeight(1);
                instance.transform.position=new Vector3(((columns-1)*.5f-i%columns)*1.14f,(rows-1)*.65f-(i/columns)*1.3f-.5f,0);
                instance.transform.rotation=Quaternion.Euler(12,-22,0);
                foreach(var transform in instance.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=30;
                var animator=instance.GetComponentInChildren<Animator>();animator.enabled=false;
                var clip=animator.runtimeAnimatorController.animationClips.First(c=>c.name==(states==null?"Idle":states[i]));
                clip.SampleAnimation(animator.gameObject,phases==null?0:clip.length*phases[i]);
                var point=camera.WorldToViewportPoint(instance.transform.position+Vector3.down*.11f);
                string text=states==null?(i+1).ToString("00"):states[i]+" "+(i%4+1);
                var label=ui.Label(canvas.transform,text,Vector2.zero,new Vector2(200,42),27);
                label.alignment=TextAlignmentOptions.Center;
                label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(point.x,point.y);label.rectTransform.pivot=new Vector2(.5f,.5f);
                label.rectTransform.anchoredPosition=Vector2.zero;
            }
            var heading=ui.Label(canvas.transform,states==null?"SHOPPERS / IMPORTED UNITY MODELS":"IDLE / WALK / BROWSE — ACTUAL ANIMATION FRAMES",new Vector2(20,-8),new Vector2(width-40,50),30);heading.alignment=TextAlignmentOptions.Center;
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            output=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);output.ReadPixels(new Rect(0,0,target.width,target.height),0,0);output.Apply();
            Directory.CreateDirectory(ShopperFleetBatch.Evidence);File.WriteAllBytes(ShopperFleetBatch.Evidence+"/"+outputName,output.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;
            if(target!=null){target.Release();Object.DestroyImmediate(target);}if(output!=null)Object.DestroyImmediate(output);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static GameObject New(Scene scene,string name,params Type[] components)
    {
        var root=new GameObject(name,components);root.layer=30;SceneManager.MoveGameObjectToScene(root,scene);return root;
    }
    private static void Light(Scene scene,Vector3 angles,float intensity)
    {
        var light=New(scene,"Review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=intensity;
        light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(angles);
    }
}
