using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.StoreOperations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Adds only a shared sidewalk population. Does not regenerate any town geometry or store saves.</summary>
public static class StreetLifeBatch
{
    public const string Root="Assets/Art/StreetLife";
    public const string Report="Library/StreetLifeQA";
    internal static string[] Inputs()=>new[]{TownWorldAssembler.PrefabPath,CityUpdateSetup.CityPath,CityUpdateSetup.GamePath,
        Root+".meta",Root+"/Sidewalks.asset",Root+"/Sidewalks.asset.meta","Assets/Art/TrafficFleet/Data/RoadNetwork.asset",
        "Assets/Resources/Shoppers/Roster.asset","Assets/Project/Editor/StreetLifeBatch.cs","Assets/Project/Editor/StreetLifeValidation.cs"}
        .Concat(new[]{"TownPedestrianRoutes","TownPedestrianTraffic","TownJunctionSignals","TownSignalChunks","TownRoadTraffic","TrafficVehicle","CityWorld"}
            .Select(name=>"Assets/Project/Scripts/City/"+name+".cs"))
        .Concat(Directory.GetFiles("Assets/Art/Shoppers","*",SearchOption.AllDirectories))
        .OrderBy(path=>path,StringComparer.Ordinal).ToArray();
    public static void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        try
        {
            var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
            var town=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<TownWorldPlaces>()).Single();
            var traffic=town.GetComponentInChildren<TownRoadTraffic>();
            ValidateSignals(town,traffic);
            Physics.SyncTransforms();
            var routes=CreateRoutes(town,traffic.Network);
            TrafficFleetBatch.Save(routes,Root+"/Sidewalks.asset");AssetDatabase.SaveAssets();
            var contents=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var protectedComponents=contents.GetComponentsInChildren<Component>(true).Where(c=>c!=null && c!=contents.transform && c.GetComponentInParent<TownPedestrianTraffic>()==null)
                    .ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
                var population=contents.GetComponentInChildren<TownPedestrianTraffic>(true);
                if(population==null)
                {
                    var root=new GameObject("Street pedestrians");root.transform.SetParent(contents.transform,false);population=root.AddComponent<TownPedestrianTraffic>();
                }
                population.Configure(AssetDatabase.LoadAssetAtPath<TownPedestrianRoutes>(Root+"/Sidewalks.asset"),
                    Resources.Load<ShopperRoster>("Shoppers/Roster"),ShopperDimensions.StreetHeight);
                foreach(var entry in protectedComponents)Require(EditorJsonUtility.ToJson(entry.Key)==entry.Value,"Existing town component changed: "+entry.Key.name);
                PrefabUtility.SaveAsPrefabAsset(contents,TownWorldAssembler.PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(contents);}
            AssetDatabase.SaveAssets();
            foreach(var path in new[]{CityUpdateSetup.CityPath,CityUpdateSetup.GamePath})
            {
                var saved=EditorSceneManager.OpenScene(path);
                Require(saved.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<TownPedestrianTraffic>(true)).Count()==1,"Missing shared street population: "+path);
            }
            File.WriteAllText(Report+"/build.txt","PASS: "+routes.Routes.Count+" collision-checked sidewalk sections; shared City/Game population, 32 active/pool limit; fifty existing appearances; pre-existing town components unchanged.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    private static TownPedestrianRoutes CreateRoutes(TownWorldPlaces town,TrafficRoadNetwork network)
    {
        var sections=new List<TownPedestrianRoutes.Route>();var obstacles=town.GetComponentsInChildren<Collider>();
        var bounds=town.layout.reservedShopPlot;
        foreach(var lane in network.Lanes)
        {
            // Rural lanes lack a continuous sidewalk. In particular, never lead people across the railway deck.
            if(lane.rural || lane.points.Any(p=>p.z>205))continue;
            var run=new List<Vector3>();
            for(float d=6;d<=lane.Path.Length-6;d+=.5f)
            {
                var p=lane.Path.Sample(d,out var direction);p+=Vector3.Cross(Vector3.up,direction).normalized*3.4f;p.y=.144f;
                bool safe=!bounds.Contains(new Vector2(p.x,p.z));
                foreach(float track in new[]{-.23f,.23f})
                {
                    var point=p+Vector3.Cross(Vector3.up,direction).normalized*track;
                    var hits=Physics.OverlapCapsule(point+Vector3.up*.30f,point+Vector3.up*.82f,.24f,~0,QueryTriggerInteraction.Ignore);
                    if(hits.Any(hit=>obstacles.Contains(hit)))safe=false;
                }
                if(safe)run.Add(p);else Flush();
            }
            Flush();
            void Flush()
            {
                if(run.Count>=17 && Vector3.Distance(run[0],run[run.Count-1])>=8)
                    sections.Add(new TownPedestrianRoutes.Route{points=run.ToArray()});
                run.Clear();
            }
        }
        Require(sections.Count>25,"Too few clear sidewalks; do not spawn people on roads as a fallback.");
        var result=ScriptableObject.CreateInstance<TownPedestrianRoutes>();result.name="Public sidewalk sections";result.Configure(sections.ToArray());return result;
    }
    internal static void ValidateSignals(TownWorldPlaces town,TownRoadTraffic traffic)
    {
        var serialized=new SerializedObject(traffic);var array=serialized.FindProperty("signals");
        var centers=town.GetComponent<TownSignalChunks>().Junctions;
        int bindings=0;
        foreach(var node in traffic.Network.Junctions.Where(node=>node.signalIndex>=0))
        {
            Require(node.signalIndex<array.arraySize,"Signal array index outside configured bindings.");
            var signal=array.GetArrayElementAtIndex(node.signalIndex).objectReferenceValue as TownJunctionSignals;
            Require(signal!=null && signal.IsConfigured && centers.Any(entry=>entry.Signal==signal && Vector3.Distance(entry.Center,node.position)<.1f),"Traffic reads the wrong junction's signal.");
            foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.right,Vector3.left})
            foreach(double phase in new[]{0d,19.99,20,23,25,26,45.99,46,51,58,63})
            {
                double clock=phase-signal.PhaseOffset;signal.DisplayAt(phase);
                bool green=(Mathf.Abs(direction.z)>.5f?signal.Current.NorthSouth:signal.Current.EastWest)==JunctionSignalAspect.Green;
                Require(signal.LampsMatchState && signal.AllowsVehicle(direction,clock)==green,"Car permission differs from visible lamps.");
            }
            signal.DisplayAt(signal.PhaseOffset);bindings++;
        }
        Require(bindings==6,"All six traffic-light junctions must be bound.");
    }
    internal static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
