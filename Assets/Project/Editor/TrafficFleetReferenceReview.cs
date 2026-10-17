using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

/// <summary>One native render per vehicle, fixed viewing angle, composed into the reference's 5x4 layout.</summary>
internal static class TrafficFleetReferenceReview
{
    private static readonly string[] Names={"HATCHBACK","CLASSIC SEDAN","STATION WAGON","COUPE","BOX SUV","PICKUP","MINIVAN","PANEL VAN","DELIVERY VAN","TAXI","MINIBUS","FLATBED TRUCK","BOX TRUCK","CROSSOVER","EXECUTIVE SEDAN","MICROCAR","COMPACT CARGO VAN","DOUBLE CAB PICKUP","SMALL CAMPER","FASTBACK"};
    internal static void Capture()
    {
        const int width=480,height=330,header=44;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.73f,.75f,.78f);
        var sun=new GameObject("Soft studio key",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.05f;
        sun.transform.rotation=Quaternion.Euler(52,-35,0);sun.shadows=LightShadows.Soft;sun.shadowStrength=.32f;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.localScale=new Vector3(40,.10f,40);floor.transform.position=new Vector3(0,-.052f,0);
        var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMaterial.color=new Color(.72f,.71f,.69f);
        floor.GetComponent<MeshRenderer>().sharedMaterial=floorMaterial;
        var camera=new GameObject("Reference camera",typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.72f,.71f,.69f);camera.nearClipPlane=.1f;camera.farClipPlane=100;
        var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.renderShadows=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=4};target.Create();camera.targetTexture=target;
        var sheet=new Texture2D(width*5,height*4+header,TextureFormat.RGB24,false);
        var background=Enumerable.Repeat(new Color(.72f,.71f,.69f),sheet.width*sheet.height).ToArray();sheet.SetPixels(background);
        var paths=AssetDatabase.FindAssets("t:Prefab",new[]{TrafficFleetBatch.Root+"/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        var label=new GameObject("Reference caption",typeof(TextMesh)).GetComponent<TextMesh>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=64;label.color=new Color(.10f,.12f,.15f);
        var title=new GameObject("Reference title",typeof(TextMesh)).GetComponent<TextMesh>();title.font=label.font;title.GetComponent<MeshRenderer>().sharedMaterial=title.font.material;
        title.anchor=TextAnchor.MiddleCenter;title.alignment=TextAlignment.Center;title.fontSize=64;title.color=label.color;title.text="LOW POLY TRAFFIC — 20 REBUILT MODELS";
        title.gameObject.SetActive(false);
        for(int i=0;i<paths.Length;i++)
        {
            var car=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]));
            Object.DestroyImmediate(car.GetComponent<RetailEmpireTycoon.City.TrafficVehicle>());Object.DestroyImmediate(car.GetComponent<Rigidbody>());
            var renderers=car.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            var look=new Vector3(0,bounds.size.y*.43f,0);camera.transform.position=look+new Vector3(-10,4.6f,12);camera.transform.LookAt(look);
            camera.aspect=(float)width/height;
            float projectedWidth=0,projectedHeight=0;
            var right=camera.transform.right;var up=camera.transform.up;
            projectedWidth=Mathf.Abs(right.x)*bounds.size.x+Mathf.Abs(right.y)*bounds.size.y+Mathf.Abs(right.z)*bounds.size.z;
            projectedHeight=Mathf.Abs(up.x)*bounds.size.x+Mathf.Abs(up.y)*bounds.size.y+Mathf.Abs(up.z)*bounds.size.z;
            camera.orthographicSize=Mathf.Max(projectedHeight*.64f,projectedWidth/camera.aspect*.59f);
            // Put the vehicle slightly above centre to leave a dedicated caption strip.
            camera.transform.position-=camera.transform.up*(camera.orthographicSize*.10f);
            label.text=(i+1).ToString("00")+"  "+Names[i];label.transform.rotation=camera.transform.rotation;
            label.characterSize=1;
            label.transform.position=camera.transform.position+camera.transform.forward*6-camera.transform.up*camera.orthographicSize*.84f;
            label.gameObject.SetActive(true);camera.Render();
            float textHeight=label.GetComponent<MeshRenderer>().localBounds.size.y;
            label.characterSize=camera.orthographicSize*.12f/Mathf.Max(.001f,textHeight);camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;
            var cell=new Texture2D(width,height,TextureFormat.RGB24,false);cell.ReadPixels(new Rect(0,0,width,height),0,0);cell.Apply();
            sheet.SetPixels(i%5*width,(3-i/5)*height,width,height,cell.GetPixels());
            Directory.CreateDirectory(TrafficFleetBatch.Pictures+"/Vehicles");File.WriteAllBytes(TrafficFleetBatch.Pictures+"/Vehicles/"+Path.GetFileNameWithoutExtension(paths[i])+".png",cell.EncodeToPNG());
            RenderTexture.active=previous;Object.DestroyImmediate(cell);Object.DestroyImmediate(car);
        }
        // Native text render for the common sheet heading, not an AI illustration of the models.
        label.gameObject.SetActive(false);floor.SetActive(false);title.gameObject.SetActive(true);
        camera.orthographicSize=1;camera.transform.position=new Vector3(0,0,-10);camera.transform.rotation=Quaternion.identity;
        title.transform.position=Vector3.zero;title.transform.rotation=Quaternion.identity;title.characterSize=1;
        var headingTarget=new RenderTexture(width*5,header,0);headingTarget.Create();camera.targetTexture=headingTarget;
        camera.aspect=(float)sheet.width/header;camera.Render();
        title.characterSize=1.35f/Mathf.Max(.001f,title.GetComponent<MeshRenderer>().localBounds.size.y);camera.Render();
        var old=RenderTexture.active;RenderTexture.active=headingTarget;
        sheet.ReadPixels(new Rect(0,0,sheet.width,header),0,height*4);sheet.Apply();
        var divider=new Color(.57f,.57f,.56f);
        for(int x=0;x<sheet.width;x++)for(int y=0;y<height*4;y++)
            if(x%width<3||y%height<3)sheet.SetPixel(x,y,divider);
        sheet.Apply();
        RenderTexture.active=old;File.WriteAllBytes(TrafficFleetBatch.Pictures+"/ReferenceFleet.png",sheet.EncodeToPNG());
        camera.targetTexture=null;target.Release();headingTarget.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(headingTarget);Object.DestroyImmediate(sheet);Object.DestroyImmediate(floorMaterial);
    }
}
