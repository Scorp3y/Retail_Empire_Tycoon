using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace RetailEmpireTycoon.Editor.TownKit
{
    internal static class TownModelPreviews
    {
        private const int TileSize=384;
        public static Texture2D Render(GameObject prefab)
        {
            var scene=EditorSceneManager.NewPreviewScene();RenderTexture target=null;Texture2D image=null;
            var previous=RenderTexture.active;
            try
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                var bounds=instance.GetComponent<MeshFilter>().sharedMesh.bounds;
                foreach(var t in instance.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
                var cameraObject=new GameObject("Town model preview camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;
                camera.cullingMask=1<<30;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.96f,.95f,.91f,0);
                float radius=Mathf.Max(.1f,bounds.extents.magnitude);
                camera.transform.position=bounds.center+new Vector3(1,.85f,1.3f).normalized*radius*4;
                camera.transform.LookAt(bounds.center);camera.orthographicSize=radius*1.06f;camera.nearClipPlane=.01f;camera.farClipPlane=radius*10+10;
                AddLight(scene,new Vector3(40,-135,0),1.3f);
                AddFillLight(scene,bounds.center,radius);
                target=new RenderTexture(TileSize,TileSize,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
                camera.Render();RenderTexture.active=target;
                image=new Texture2D(TileSize,TileSize,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,TileSize,TileSize),0,0);image.Apply();
                if(image.GetPixels32().Count(p=>p.a>32)<80)throw new InvalidOperationException("Empty 3D render: "+prefab.name);
                return image;
            }
            catch {if(image!=null)Object.DestroyImmediate(image);throw;}
            finally
            {
                RenderTexture.active=previous;if(target!=null){target.Release();Object.DestroyImmediate(target);}
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void AddLight(Scene scene,Vector3 rotation,float intensity)
        {
            var root=new GameObject("Town preview light");SceneManager.MoveGameObjectToScene(root,scene);
            root.transform.rotation=Quaternion.Euler(rotation);var light=root.AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=intensity;light.cullingMask=1<<30;
        }

        private static void AddFillLight(Scene scene,Vector3 center,float radius)
        {
            // URP selects only one main directional light. A point light provides a real fill.
            var root=new GameObject("Town preview fill light");SceneManager.MoveGameObjectToScene(root,scene);
            root.transform.position=center+new Vector3(-1,.8f,-.5f).normalized*radius*3;
            var light=root.AddComponent<Light>();light.type=LightType.Point;
            light.range=radius*10;light.intensity=6*radius*radius;light.cullingMask=1<<30;
        }

        public static void Sheet(IReadOnlyList<Texture2D> images,int columns,string path)
        {
            int rows=Mathf.CeilToInt((float)images.Count/columns);
            var sheet=new Texture2D(columns*TileSize,rows*TileSize,TextureFormat.RGBA32,false);
            try
            {
                var background=new Color32(244,242,233,255);
                var pixels=Enumerable.Repeat(background,sheet.width*sheet.height).ToArray();
                for(int imageIndex=0;imageIndex<images.Count;imageIndex++)
                {
                    int left=(imageIndex%columns)*TileSize, bottom=(rows-1-imageIndex/columns)*TileSize;
                    var source=images[imageIndex].GetPixels32();
                    for(int y=0;y<TileSize;y++)for(int x=0;x<TileSize;x++)
                    {
                        var pixel=source[y*TileSize+x];if(pixel.a==0)pixel=background;pixel.a=255;
                        pixels[(bottom+y)*sheet.width+left+x]=pixel;
                    }
                }
                sheet.SetPixels32(pixels);sheet.Apply();File.WriteAllBytes(path,sheet.EncodeToPNG());
            }
            finally {Object.DestroyImmediate(sheet);}
        }
    }
}
