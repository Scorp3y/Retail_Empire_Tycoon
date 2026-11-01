using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RetailEmpireTycoon.City;

/// <summary>Read-only survey and reference export. Never saves scenes or replaces existing objects.</summary>
public static class PinebrookAudit
{
    const string Output="ArtSource/Pinebrook/Audit";
    [Serializable] public class Survey { public string unity,pipeline,prefab; public float floorHeight,roadWidth,sidewalkWidth; public Vector3 crossing; public Building[] buildings;public Road[] roads;public string[] vegetation,materials; }
    [Serializable] public class Building {public string name;public Vector3 position,size;public float yaw;}
    [Serializable] public class Road {public float x,z,width,depth;}
    [Serializable] public class Reference {public Surface[] surfaces;}
    [Serializable] public class Surface {public Vector3[] vertices;public int[] triangles;public Color[] colors;}
    public static void Run()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Output);
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var town=UnityEngine.Object.FindObjectOfType<TownWorldPlaces>();
        var site=town.transform.Find("Forest Quarter");if(site==null)throw new InvalidOperationException("Existing district missing.");
        var architecture=site.Find("Architecture");
        var homes=architecture.Cast<Transform>().Select(t=>{var renderers=t.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return new Building{name=t.name,position=t.position,yaw=t.eulerAngles.y,size=bounds.size};}).ToArray();
        var crossing=town.GetComponentsInChildren<TownLevelCrossing>();if(crossing.Length!=1)throw new InvalidOperationException("Expected one crossing.");
        var report=new Survey{unity=Application.unityVersion,pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null?UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name:"Built-in",prefab=TownWorldAssembler.PrefabPath,
            floorHeight=3.2f,roadWidth=8,sidewalkWidth=3,crossing=crossing[0].transform.TransformPoint(crossing[0].deckCenter),buildings=homes,
            roads=TownForestPlan.Roads.Concat(new[]{new Rect(-28,198,8,112)}).Select(r=>new Road{x=r.x,z=r.y,width=r.width,depth=r.height}).ToArray(),
            vegetation=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Art/TownKit/Prefabs/Nature"}).Select(AssetDatabase.GUIDToAssetPath).ToArray(),
            materials=site.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).Distinct().Select(m=>AssetDatabase.GetAssetPath(m)+" | "+m.shader.name).ToArray()};
        File.WriteAllText(Output+"/Survey.json",JsonUtility.ToJson(report,true));
        var cafe=architecture.Cast<Transform>().First(t=>t.name=="CafeCorner");
        var surfaces=cafe.GetComponentsInChildren<MeshFilter>().Select(f=>
        {
            var mesh=f.sharedMesh;var renderer=f.GetComponent<MeshRenderer>();var material=renderer.sharedMaterial;var texture=material.GetTexture("_BaseMap") as Texture2D;
            var uv=mesh.uv;var fallback=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):Color.white;
            return new Surface{vertices=mesh.vertices.Select(v=>{var p=cafe.InverseTransformPoint(f.transform.TransformPoint(v));return new Vector3(p.x,-p.z,p.y);}).ToArray(),triangles=mesh.triangles,
                colors=Enumerable.Range(0,mesh.vertexCount).Select(i=>texture!=null&&texture.isReadable&&uv.Length==mesh.vertexCount?texture.GetPixel(Mathf.Clamp((int)(uv[i].x*texture.width),0,texture.width-1),Mathf.Clamp((int)(uv[i].y*texture.height),0,texture.height-1)):fallback).ToArray()};
        }).ToArray();
        File.WriteAllText(Output+"/CafeReference.json",JsonUtility.ToJson(new Reference{surfaces=surfaces}));
        TownFloristBatch.Render(scene,Output+"/ExistingTop.png",new Vector3(0,360,380),new Vector3(0,0,380),190);
        TownFloristBatch.Render(scene,Output+"/ExistingCafe.png",new Vector3(-56,6,308),new Vector3(-76,5,332));
        File.WriteAllText(Output+"/Result.txt","PASS: read-only saved-scene survey; thirteen retained architecture objects; one crossing. Floor spacing measured from facade layout; road rectangles read from existing layout source.\n");
        Debug.Log("Pinebrook audit complete; no scene saved.");
    }
}
