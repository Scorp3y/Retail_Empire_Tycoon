using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Additive road furniture and exact-template removal; no street/building/landscape reconstruction.</summary>
internal sealed class TownJunctionPlacement
{
    private const float Pavement=.14f;
    private readonly GameObject town;
    private readonly TownJunctionPlan.Junction[] junctions;
    private readonly TownJunctionArt art=new();
    private readonly List<string> audit=new();
    private readonly TownMeshBuilder markings=new();
    private readonly Dictionary<MeshCollider,(Bounds bounds,Vector3[] vertices,int[] triangles)> supportMeshes=new();
    private Transform site;
    internal TownJunctionPlacement(GameObject town)
    {
        this.town=town;junctions=TownJunctionPlan.Create(town.GetComponent<TownWorldPlaces>().layout);
    }

    internal void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(TownJunctionPlan.Site)!=null)throw new InvalidOperationException("Junction update already installed; use the recorded original.");
        art.Build();RemoveLegacy();
        site=new GameObject(TownJunctionPlan.Site).transform;site.SetParent(town.transform,false);
        for(int index=0;index<junctions.Length;index++)PlaceJunction(junctions[index],index);
        art.Mesh(site,"New road markings",markings,art.Surface);
        art.CombineStatic(site);AssetDatabase.SaveAssets();
        File.WriteAllLines(TownJunctionBatch.Report+"/placement.txt",audit);
    }

    private void PlaceJunction(TownJunctionPlan.Junction junction,int index)
    {
        var node=new GameObject("Junction "+junction.Cell.x+" "+junction.Cell.y).transform;node.SetParent(site,false);
        bool privateOnly=junction.Approaches.Count(d=>!junction.IsPrivate(d))<3;
        var ns=new[]{new TownMeshBuilder(),new TownMeshBuilder(),new TownMeshBuilder()};
        var ew=new[]{new TownMeshBuilder(),new TownMeshBuilder(),new TownMeshBuilder()};
        var pedRed=new TownMeshBuilder();var pedGreen=new TownMeshBuilder();
        foreach(int approach in junction.Approaches)
        {
            var q=Quaternion.LookRotation(TownJunctionPlan.Outward(approach));
            var marker=new GameObject("Approach "+approach).transform;marker.SetParent(node,false);
            marker.position=junction.Center;marker.rotation=q;
            if(junction.HasSignals)
            {
                var position=FreePole(junction,approach,11.1f,4.8f,4.9f);
                art.Place(marker,"VehicleSignal",marker.InverseTransformPoint(position),Quaternion.identity);
                art.Place(marker,junction.IsMain(approach)?"PriorityPlate":"YieldPlate",marker.InverseTransformPoint(position),Quaternion.identity);
                var axis=approach%2==0?ns:ew;
                for(int aspect=0;aspect<3;aspect++)TownJunctionArt.Append(axis[aspect],TownJunctionArt.VehicleLens(aspect),position,q);
                foreach(int side in new[]{-1,1})
                {
                    var p=TownJunctionPlan.At(junction,approach,8.5f,side*4.8f,Pavement);
                    var facing=Quaternion.LookRotation(-TownJunctionPlan.ArrivalRight(approach)*side);
                    art.Place(node,"PedestrianSignal",p,facing);
                    TownJunctionArt.Append(pedRed,TownJunctionArt.PedestrianGlyph(false),p,facing);
                    TownJunctionArt.Append(pedGreen,TownJunctionArt.PedestrianGlyph(true),p,facing);
                }
                CrossingMarks(junction,approach);
                Line(junction,approach,11.6f,.30f,3.55f);
            }
            else if(!privateOnly||junction.IsPrivate(approach))
            {
                bool hasOriginalCrosswalk=junction.Approaches.Count(d=>(junction.PublicMask&(1<<d))!=0)>=3;
                float distance=junction.IsPrivate(approach)||!hasOriginalCrosswalk?6.2f:12.4f;
                var p=FreePole(junction,approach,distance,4.9f,2.8f);
                art.Place(marker,junction.IsMain(approach)?"PriorityRoadSign":"YieldSign",marker.InverseTransformPoint(p),Quaternion.identity);
                if(!junction.IsMain(approach))YieldMarks(junction,approach,junction.IsPrivate(approach)?6.0f:11.4f);
            }
        }
        if(junction.HasSignals)
        {
            TownJunctionSignals.LampGroup Group(string axis,TownMeshBuilder[] meshes)=>new(
                art.Light(node,index+axis+" Red",meshes[0],"Red"),art.Light(node,index+axis+" Amber",meshes[1],"Amber"),art.Light(node,index+axis+" Green",meshes[2],"Green"));
            var nsGroup=Group(" NS",ns);var ewGroup=Group(" EW",ew);
            var red=art.Light(node,index+" Pedestrian Red",pedRed,"Red");var green=art.Light(node,index+" Pedestrian Green",pedGreen,"Green");
            node.gameObject.AddComponent<TownJunctionSignals>().Configure(nsGroup,ewGroup,red,green,index*7);
        }
        audit.Add(junction.Cell+" world="+junction.Center+" arms="+junction.Mask+" public="+junction.PublicMask+" | "+(junction.HasSignals?"SIGNALS":"PRIORITY")+" | main="+(junction.MainNorthSouth?"N/S":"E/W")+(privateOnly?" | private exit only":""));
    }

    private Vector3 FreePole(TownJunctionPlan.Junction junction,int approach,float distance,float right,float height)
    {
        var distances=new[]{distance,distance+.6f,distance-.6f,distance+1.2f,distance-1.2f,6.2f,5.8f}.Distinct();
        foreach(float longitudinal in distances)foreach(float lateral in new[]{0f,.45f,.8f})
        {
            var point=OnSupport(TownJunctionPlan.At(junction,approach,longitudinal,right+lateral,Pavement));
            var envelope=new Bounds(point+Vector3.up*height/2,new Vector3(.28f,height,.28f));
            bool clear=town.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger&&!(c is MeshCollider))
                .Select(TownResidentialPlacement.ColliderBounds).All(b=>!b.Intersects(envelope));
            if(clear&&ClearOfRoadMouths(point))return point;
        }
        throw new InvalidOperationException("No clear pavement mount at "+junction.Cell+" approach "+approach);
    }

    private bool ClearOfRoadMouths(Vector3 point)
    {
        // Adjacent offset T-junctions can be only one tile apart. A nominal verge of one approach may
        // actually be the neighbouring driveway mouth; do not plant a pole in that vehicle corridor.
        foreach(var j in junctions)foreach(int direction in j.Approaches)
        {
            var delta=point-j.Center;float along=Vector3.Dot(delta,TownJunctionPlan.Outward(direction));
            float across=Mathf.Abs(Vector3.Dot(delta,TownJunctionPlan.ArrivalRight(direction)));
            if(along>=0&&along<=8.1f&&across<4.25f)return false;
        }
        return true;
    }

    private Vector3 OnSupport(Vector3 point)
    {
        // In particular, the north gateway sidewalk rises with the existing rail-crossing approach.
        // Read the actual saved collision ribbon rather than duplicating its elevation formula.
        foreach(var collider in town.GetComponentsInChildren<MeshCollider>(true))
        {
            if(collider.isTrigger||collider.sharedMesh==null)continue;
            if(!supportMeshes.TryGetValue(collider,out var data))
            {
                var v=collider.sharedMesh.vertices.Select(collider.transform.TransformPoint).ToArray();
                if(v.Length==0)continue;
                var bounds=new Bounds(v[0],Vector3.zero);foreach(var p in v)bounds.Encapsulate(p);
                data=(bounds,v,collider.sharedMesh.triangles);supportMeshes.Add(collider,data);
            }
            if(point.x<data.bounds.min.x||point.x>data.bounds.max.x||point.z<data.bounds.min.z||point.z>data.bounds.max.z)continue;
            for(int i=0;i<data.triangles.Length;i+=3)
            {
                var a=data.vertices[data.triangles[i]];var b=data.vertices[data.triangles[i+1]];var c=data.vertices[data.triangles[i+2]];
                if(Vector3.Cross(b-a,c-a).normalized.y<.6f)continue;
                float determinant=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
                if(Mathf.Abs(determinant)<.00001f)continue;
                float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/determinant;
                float v=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/determinant;
                if(u<-.0001f||v<-.0001f||u+v>1.0001f)continue;
                float y=u*a.y+v*b.y+(1-u-v)*c.y;
                if(y>=0&&y<2.5f)point.y=Mathf.Max(point.y,y+.004f);
            }
        }
        return point;
    }

    private void RemoveLegacy()
    {
        var keys=new HashSet<string>();var signals=town.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="TrafficSignal").ToArray();
        foreach(var signal in signals)Template(keys,"Assets/Art/TownDetails/Prefabs/Street/TrafficSignal.prefab",signal.localToWorldMatrix);
        var layout=town.GetComponent<TownWorldPlaces>().layout;
        foreach(var road in layout.roads.Where(r=>Enumerable.Range(0,4).Count(d=>(r.connections&(1<<d))!=0)>=3))
            Template(keys,"Assets/Art/TownKit/Prefabs/Street/YieldSign.prefab",Matrix4x4.TRS(road.Position(layout.roadTileSize)+new Vector3(7.3f,0,7.3f),Quaternion.Euler(0,180,0),Vector3.one));
        foreach(var j in junctions.Where(j=>j.HasSignals))foreach(int approach in j.Approaches)
            Template(keys,"Assets/Art/TownKit/Prefabs/Roads/Crosswalk.prefab",Matrix4x4.TRS(TownJunctionPlan.At(j,approach,8.5f,0,.055f),Quaternion.Euler(0,approach*90,0),Vector3.one));
        int index=0,total=0;
        foreach(var filter in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var old=filter.sharedMesh;
            var mesh=TownBakeryPlacement.CopyWithoutTriangles(old,filter.transform,(a,b,c)=>keys.Contains(Key(a,b,c)),out int removed);
            if(removed==0){Object.DestroyImmediate(mesh);continue;}
            string path="Meshes/LegacyEquipmentTrim"+index+++".asset";TownJunctionArt.Save(mesh,path);
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(TownJunctionArt.Root+"/"+path);
            var collider=filter.GetComponent<MeshCollider>();if(collider!=null&&collider.sharedMesh==old)collider.sharedMesh=saved;
            filter.sharedMesh=saved;total+=removed;
        }
        foreach(var signal in signals)Object.DestroyImmediate(signal.gameObject);
        if(signals.Length!=12||total<500)throw new InvalidOperationException("Legacy equipment inventory differs: "+signals.Length+" signals, "+total+" matched triangles.");
        audit.Add("Removed 12 legacy static signals and exact matching old yield/crosswalk geometry; "+total+" triangles in "+index+" new owned mesh copies. Source city meshes were not overwritten.");
    }

    private static void Template(HashSet<string> keys,string path,Matrix4x4 pose)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(source==null)throw new InvalidOperationException("Legacy template missing "+path);
        foreach(var f in source.GetComponentsInChildren<MeshFilter>())
        {
            var v=f.sharedMesh.vertices.Select(p=>(pose*f.transform.localToWorldMatrix).MultiplyPoint3x4(p)).ToArray();var t=f.sharedMesh.triangles;
            for(int i=0;i<t.Length;i+=3)keys.Add(Key(v[t[i]],v[t[i+1]],v[t[i+2]]));
        }
    }
    private static string Key(Vector3 a,Vector3 b,Vector3 c)=>string.Join("|",new[]{Point(a),Point(b),Point(c)}.OrderBy(p=>p,StringComparer.Ordinal));
    private static string Point(Vector3 p)=>Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.y*1000)+","+Mathf.RoundToInt(p.z*1000);

    private void Line(TownJunctionPlan.Junction j,int approach,float distance,float depth,float width)
    {
        var p=TownJunctionPlan.At(j,approach,distance,2,.075f);var q=Quaternion.LookRotation(TownJunctionPlan.Outward(approach));
        PaintRectangle(p,q,width,depth);
    }
    private void YieldMarks(TownJunctionPlan.Junction j,int approach,float distance)
    {
        for(int i=0;i<5;i++)
        {
            var p=TownJunctionPlan.At(j,approach,distance,.55f+i*.68f,.075f);var q=Quaternion.LookRotation(TownJunctionPlan.Outward(approach));
            Vector3 P(float x,float z)=>OnSupport(p+q*new Vector3(x,0,z));
            markings.Triangle(P(-.22f,-.24f),P(0,.24f),P(.22f,-.24f),TownColor.White);
        }
    }
    private void CrossingMarks(TownJunctionPlan.Junction j,int approach)
    {
        var q=Quaternion.LookRotation(TownJunctionPlan.Outward(approach));
        foreach(float edge in new[]{7.1f,9.9f})for(float x=-3.6f;x<3.7f;x+=.8f)
            PaintRectangle(TownJunctionPlan.At(j,approach,edge,x,.075f),q,.42f,.17f);
    }
    private void PaintRectangle(Vector3 p,Quaternion q,float width,float depth)
    {
        Vector3 P(float x,float z)=>OnSupport(p+q*new Vector3(x,0,z));
        markings.Quad(P(-width/2,depth/2),P(width/2,depth/2),P(width/2,-depth/2),P(-width/2,-depth/2),TownColor.White);
    }
}
