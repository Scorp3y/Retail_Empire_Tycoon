using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RetailEmpireTycoon.City;
public static class PinebrookExpansionSurvey
{
 public static void Run()
 {
  TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory("Library/PinebrookDistrictQA");
  EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);var town=UnityEngine.Object.FindObjectOfType<TownWorldPlaces>();
  string Path(Transform t)=>t.parent==null?t.name:Path(t.parent)+"/"+t.name;
  var lines=town.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="DistrictSign"||t.name=="DistrictMarker"||t.name.Contains("CafeCornerFrontLabel")||t.name=="Forest Quarter").Select(t=>Path(t)+" position="+t.position+" renderers="+t.GetComponentsInChildren<MeshRenderer>().Length).ToList();
  Physics.SyncTransforms();foreach(var p in new[]{new Vector3(-170,0,560),new Vector3(0,0,650),new Vector3(-240,0,370),new Vector3(160,0,600)}){var hits=Physics.RaycastAll(p+Vector3.up*50,Vector3.down,100);lines.Add("FLOOR "+p+" "+string.Join(";",hits.Select(h=>Path(h.collider.transform)+" Y="+h.point.y)));}
  lines.Add("TREE COUNT "+town.GetComponentsInChildren<Transform>(true).Count(t=>(t.name=="Pine"||t.name=="TreeRound"||t.name=="TreeWide")&&t.position.z>280&&t.position.z<670&&t.position.x<200&&t.position.x>-260));
  foreach(var c in town.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.sharedMesh==null))lines.Add("EMPTY COLLIDER "+Path(c.transform));
  foreach(var f in town.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh==null))lines.Add("MISSING RENDER MESH "+Path(f.transform));
  File.WriteAllLines("Library/PinebrookDistrictQA/survey.txt",lines);EditorApplication.Exit(0);
 }
}
