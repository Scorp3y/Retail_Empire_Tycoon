using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Shelves;
public static class Wave2Integration
{
 const string Art="Assets/Art/Wave2", Data="Assets/Resources/StoreAssortment/Wave2";
 [Serializable] public class Source { public string key,name,storage; public int cost,sale; public float weight,volume;public Part[] parts;public Mat[] materials; public bool fixture; public Anchor[] anchors; }
 [Serializable] public class Anchor {public float[] position,space; public string kind; public float rotation_z;}
 [Serializable] public class Part { public float[] positions,normals,colors;public int[] indices;public int material; }
 [Serializable] public class Mat {public string name;public float[] color,emission;public float metallic,roughness;public bool vertexColor;}
 public static void Build()
 {
  Build(Art,Data);
 }
 // Additional approved waves use the same native mesh, colour and icon path.
 public static void Build(string art,string data)
 {
  try {
   if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
    throw new InvalidOperationException("Product icons require a graphics device. Run Unity without -nographics.");
   foreach(var f in new[]{art+"/Meshes",art+"/Materials",art+"/Products",data+"/Products",data+"/Fixtures"})Directory.CreateDirectory(f);
   AssetDatabase.Refresh();var pack=StoreAssortmentPack.Load();if(pack==null)throw new Exception("Missing existing pack");
   var all=pack.Products.ToList();var fixtures=pack.Fixtures.ToList();
   foreach(var path in Directory.GetFiles(art+"/Source","*.json").OrderBy(p=>Path.GetFileName(p).StartsWith("fixture_")?1:0)) {
    var source=JsonUtility.FromJson<Source>(File.ReadAllText(path));var root=new GameObject(source.key);
    try {
     var mats=source.materials.Select((m,i)=>Material(art,source.key,i,m)).ToArray();int index=0;
     foreach(var p in source.parts) {
      var mesh=new Mesh {name=source.key+"_"+index};int n=p.positions.Length/3;
      var vs=new Vector3[n];var ns=new Vector3[n];var cs=new Color[n];
      for(int i=0;i<n;i++){vs[i]=new Vector3(p.positions[i*3],p.positions[i*3+1],p.positions[i*3+2]);ns[i]=new Vector3(p.normals[i*3],p.normals[i*3+1],p.normals[i*3+2]);cs[i]=new Color(p.colors[i*4],p.colors[i*4+1],p.colors[i*4+2],p.colors[i*4+3]);}
      mesh.vertices=vs;mesh.normals=ns;mesh.colors=cs;mesh.triangles=p.indices;mesh.RecalculateBounds();
      string mp=art+"/Meshes/"+source.key+"_"+index+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(mp);if(old!=null){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,mp);
      var child=new GameObject("Surface_"+index++);child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=mats[p.material];
     }
     // One renderer per sale unit preserves shelf display/counting contracts.
     var filters=root.GetComponentsInChildren<MeshFilter>();var combined=new Mesh{name=source.key};
     combined.CombineMeshes(filters.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray(),false,true);
     string combinedPath=art+"/Meshes/"+source.key+"_combined.asset";
     var saved=AssetDatabase.LoadAssetAtPath<Mesh>(combinedPath);
     if(saved!=null){EditorUtility.CopySerialized(combined,saved);UnityEngine.Object.DestroyImmediate(combined);combined=saved;}else AssetDatabase.CreateAsset(combined,combinedPath);
     foreach(var child in filters)UnityEngine.Object.DestroyImmediate(child.gameObject);
     root.AddComponent<MeshFilter>().sharedMesh=combined;root.AddComponent<MeshRenderer>().sharedMaterials=source.parts.Select(p=>mats[p.material]).ToArray();
     if(source.fixture) {
      var fixture=StoreFixtureInstallation.Create(source,root,art,data);
      fixtures.RemoveAll(f=>f.id==fixture.id);fixtures.Add(fixture);continue;
     }
     var prefab=PrefabUtility.SaveAsPrefabAsset(root,art+"/Products/"+source.key+".prefab");
     var definition=new AssortmentUpdateBatch.ProductDefinition{key=source.key,name=source.name,storage=source.storage,cost=source.cost,sale=source.sale,weight=source.weight,volume=source.volume};
     var item=AssortmentUpdateBatch.MakeProduct(definition,null,art,data,prefab);
     // Imported Blender labels face Unity -Z after converting the source axes.
     var so=new SerializedObject(item);so.FindProperty("icon").objectReferenceValue=ShopUiPreviews.Render(prefab,item.Id,205);so.ApplyModifiedPropertiesWithoutUndo();
     all.RemoveAll(p=>p.Id==item.Id);all.Add(item);
    } finally { UnityEngine.Object.DestroyImmediate(root); }
   }
   var serialized=new SerializedObject(pack);var array=serialized.FindProperty("products");array.arraySize=all.Count;for(int i=0;i<all.Count;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=all[i];serialized.ApplyModifiedPropertiesWithoutUndo();
   var fa=serialized.FindProperty("fixtures");fa.arraySize=fixtures.Count;for(int i=0;i<fixtures.Count;i++)fa.GetArrayElementAtIndex(i).objectReferenceValue=fixtures[i];serialized.ApplyModifiedPropertiesWithoutUndo();
   AssetDatabase.SaveAssets();Validate(art);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);throw;}
 }
 static Material Material(string art,string key,int i,Mat d) {
  var path=art+"/Materials/"+key+"_"+i+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  var shader=Shader.Find(d.vertexColor?"RetailEmpire/ProductVertexColor":"Universal Render Pipeline/Lit");if(shader==null)throw new Exception("Missing product shader");
  if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}m.shader=shader;
  m.SetColor("_BaseColor",new Color(d.color[0],d.color[1],d.color[2],d.color[3]));
  if(!d.vertexColor){m.SetFloat("_Metallic",d.metallic);m.SetFloat("_Smoothness",1-d.roughness);m.SetFloat("_Cull",0);
   if(d.color[3]<1){m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;m.SetShaderPassEnabled("ShadowCaster",false);}}
  if(d.emission!=null && d.emission.Length==3 && d.emission.Any(v=>v>0)){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(d.emission[0],d.emission[1],d.emission[2]));}
  EditorUtility.SetDirty(m);return m;
 }
 public static void Validate() {
  Validate(Art);
 }
 public static void Validate(string art) {
  var pack=StoreAssortmentPack.Load();var trace=new List<string>();
  foreach(var path in Directory.GetFiles(art+"/Source","*.json")) {
   var s=JsonUtility.FromJson<Source>(File.ReadAllText(path));if(s.fixture)continue;var p=pack.Products.Single(x=>x.Id=="product_"+s.key);
   if(p.Icon==null||p.ShelfDisplayPrefab==null||p.BuyPrice<=0||p.SellPrice<=0)throw new Exception("Incomplete SKU "+s.key);
   var candidates=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs", "Assets/Art", "Assets/Resources"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(x=>x!=null).Select(x=>x.GetComponent<PlacedShelfStock>()).Where(x=>x!=null&&x.IsProductAllowed(p)).ToArray();
   if(candidates.Length==0)throw new Exception("No compatible shelf for "+s.key);
   trace.Add("PASS "+p.Id+"; icon, purchase economics, catalogue, "+candidates.Length+" compatible fixtures");
  }
  Directory.CreateDirectory("Library/Wave2Integration");File.WriteAllLines("Library/Wave2Integration/build.txt",trace);Debug.Log("Wave2 integration PASS "+trace.Count+" products");
 }
}
