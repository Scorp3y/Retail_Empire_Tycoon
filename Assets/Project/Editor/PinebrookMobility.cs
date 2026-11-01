using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using RetailEmpireTycoon.City;

/// <summary>Builds right-hand lanes from the same line segments used by the visible plan.</summary>
internal static class PinebrookMobility
{
    internal readonly struct Segment
    {
        public readonly Vector3 A,B;
        public Segment(float ax,float az,float bx,float bz)
        {
            // Intersections calculated from two different lines must share the same
            // centimetre coordinate; float drift must not duplicate crossings/signs.
            A=new Vector3(Mathf.Round(ax*100)/100,0,Mathf.Round(az*100)/100);
            B=new Vector3(Mathf.Round(bx*100)/100,0,Mathf.Round(bz*100)/100);
        }
    }
    internal static Segment[] Streets=>Read().streets.Select(s=>new Segment(s.ax,s.az,s.bx,s.bz)).ToArray();
    private static PinebrookDistrictLayout Read()=>JsonUtility.FromJson<PinebrookDistrictLayout>(System.IO.File.ReadAllText(PinebrookDistrictArt.Root+"/Layout/PinebrookLayout.json"));
    internal static Vector3[] Junctions=>Split().SelectMany(s=>new[]{s.A,s.B}).Distinct().ToArray();
    private static float Cross(Vector3 a,Vector3 b)=>a.x*b.z-a.z*b.x;
    internal static List<Segment> Split()
    {
        var streets=Streets;var result=new List<Segment>();
        foreach(var line in streets)
        {
            var points=new List<Vector3>{line.A,line.B};var d=line.B-line.A;
            foreach(var other in streets)
            {
                var e=other.B-other.A;float cross=Cross(d,e);if(Mathf.Abs(cross)<.001f)continue;
                float t=Cross(other.A-line.A,e)/cross,u=Cross(other.A-line.A,d)/cross;
                if(t>=0&&t<=1&&u>=0&&u<=1)points.Add(line.A+d*t);
            }
            var sorted=points.GroupBy(p=>new Vector2(Mathf.Round(p.x*100),Mathf.Round(p.z*100))).Select(g=>g.First()).OrderBy(p=>(p-line.A).sqrMagnitude).ToArray();
            for(int i=1;i<sorted.Length;i++)if(Vector3.Distance(sorted[i-1],sorted[i])>10)result.Add(new Segment(sorted[i-1].x,sorted[i-1].z,sorted[i].x,sorted[i].z));
        }
        return result;
    }
    internal static void Configure(GameObject town,PinebrookDistrictLayout plan)
    {
        var traffic=town.GetComponentInChildren<TownRoadTraffic>();var original=AssetDatabase.LoadAssetAtPath<TrafficRoadNetwork>("Assets/Art/TrafficFleet/Data/RoadNetwork.asset");
        var nodes=original.Junctions.ToList();
        // Keep all city/crossing lanes. Only the replaced neighbourhood loops are removed.
        var lanes=original.Lanes.Where(l=>!(nodes[l.from].position.z>=300&&nodes[l.from].position.z<=440&&nodes[l.to].position.z>=300&&nodes[l.to].position.z<=440)).ToList();
        int Node(Vector3 p){int i=nodes.FindIndex(n=>(n.position-p).sqrMagnitude<.01f);if(i>=0)return i;nodes.Add(new TrafficRoadNetwork.Junction{position=p,signalIndex=-1});return nodes.Count-1;}
        void Lane(Vector3 a,Vector3 b)
        {
            int from=Node(a),to=Node(b);if(lanes.Any(l=>l.from==from&&l.to==to))return;
            var forward=(b-a).normalized;var right=Vector3.Cross(Vector3.up,forward)*1.8f;
            lanes.Add(new TrafficRoadNetwork.Lane{from=from,to=to,speedLimit=5.5f,points=new[]{a+forward*5+right,b-forward*5+right}});
        }
        var split=Split();foreach(var line in split){Lane(line.A,line.B);Lane(line.B,line.A);}
        var network=ScriptableObject.CreateInstance<TrafficRoadNetwork>();network.name="Pinebrook connected right-hand traffic";network.Configure(nodes.ToArray(),lanes.ToArray());network.ConfigureSparseTraffic(new Rect(plan.bounds.x,238,plan.bounds.width,430),4,8);PinebrookDistrictArt.Save(network,"Layout/ConnectedRoadNetwork.asset");
        var serial=new SerializedObject(traffic);serial.FindProperty("network").objectReferenceValue=AssetDatabase.LoadAssetAtPath<TrafficRoadNetwork>(PinebrookDistrictArt.Root+"/Layout/ConnectedRoadNetwork.asset");serial.ApplyModifiedPropertiesWithoutUndo();
        var population=town.GetComponentInChildren<TownPedestrianTraffic>();var originalRoutes=AssetDatabase.LoadAssetAtPath<TownPedestrianRoutes>(AssetDatabase.GUIDToAssetPath("8de52c99f6eb41e4ea7c8d7827258e8b"));
        if(originalRoutes==null)throw new InvalidOperationException("Original pedestrian routes missing.");
        var sections=originalRoutes.Routes.Where(r=>!r.points.Any(p=>p.z>300&&p.x<200)).ToList();
        foreach(var line in split)
        {
            var forward=(line.B-line.A).normalized;var side=Vector3.Cross(Vector3.up,forward)*5.5f;
            foreach(float sign in new[]{-1f,1f})sections.Add(new TownPedestrianRoutes.Route{points=new[]{line.A+forward*8+side*sign+Vector3.up*.16f,line.B-forward*8+side*sign+Vector3.up*.16f}});
        }
        foreach(var walk in plan.paths.Where(p=>Vector3.Distance(p.Start,p.End)>=8&&!p.name.StartsWith("Entrance")&&!p.name.StartsWith("BridgeApproach")))sections.Add(new TownPedestrianRoutes.Route{points=new[]{walk.Start,walk.End}});
        sections.Add(new TownPedestrianRoutes.Route{points=new[]{new Vector3(70,.145f,396),new Vector3(70,.28f,404.5f),new Vector3(70,.465f,405.4f),new Vector3(70,.465f,410.6f),new Vector3(70,.28f,411.5f),new Vector3(70,.145f,423)}});
        var routes=ScriptableObject.CreateInstance<TownPedestrianRoutes>();routes.name="Pinebrook revised sidewalk routes";routes.Configure(sections.ToArray());PinebrookDistrictArt.Save(routes,"Layout/ConnectedPedestrianRoutes.asset");
        serial=new SerializedObject(population);serial.FindProperty("routes").objectReferenceValue=AssetDatabase.LoadAssetAtPath<TownPedestrianRoutes>(PinebrookDistrictArt.Root+"/Layout/ConnectedPedestrianRoutes.asset");serial.ApplyModifiedPropertiesWithoutUndo();
    }
}
