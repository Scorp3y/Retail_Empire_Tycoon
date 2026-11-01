using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using Object=UnityEngine.Object;

/// <summary>Rebuilds only its owned root, then connects it to retained roads and existing town simulation.</summary>
internal sealed class PinebrookDistrictPlacement
{
    private readonly GameObject town;
    private readonly PinebrookDistrictLayout plan;
    private readonly PinebrookDistrictArt art;
    private readonly Dictionary<string,Transform> groups=new();
    private Transform root;
    internal PinebrookDistrictPlacement(GameObject town,PinebrookDistrictLayout plan,PinebrookDistrictArt art){this.town=town;this.plan=plan;this.art=art;}
    internal void Build()
    {
        var previous=town.transform.Find("Pinebrook");if(previous!=null)Object.DestroyImmediate(previous.gameObject);
        root=PinebrookDistrictArt.Child(town.transform,"Pinebrook");foreach(string name in new[]{"Buildings","Props","Paths","Greenery","Signs"})groups[name]=PinebrookDistrictArt.Child(root,name);
        ReplanRetainedQuarter();new PinebrookSitePreparation(town,plan).Apply();PinebrookLegacyGround.Apply(town);ExtendBoundary();GroundAndStreets();PlaceObjects();RoadSigns();RenameCafe();
        var map=town.GetComponentInChildren<TownForestMapData>();map.Configure(plan.bounds.Rect,new[]{new Rect(-28,198,8,112)}.Concat(plan.roads.Select(r=>r.Rect)).ToArray());
        PinebrookMobility.Configure(town,plan);
    }
    private void ReplanRetainedQuarter()
    {
        var quarter=town.transform.Find("Forest Quarter");
        // Replace the obsolete two-court ground plan, not its high-detail house meshes or park.
        foreach(var child in quarter.Cast<Transform>().ToArray())
            if(child.name!="Architecture"&&child.name!="Community park"&&child.name!="Woodland trail")Object.DestroyImmediate(child.gameObject);
        var architecture=quarter.Find("Architecture");
        foreach(var item in plan.retained){var home=architecture.GetChild(item.index);home.localPosition=item.position;home.localRotation=Quaternion.Euler(0,item.yaw,0);}
    }
    private void ExtendBoundary()
    {
        var north=town.transform.Find("World boundaries").GetComponentsInChildren<BoxCollider>(true).Single(c=>c.size.x>100&&c.center.z>400);
        north.enabled=false; // Preserve the original object; its former wall must not cut the expanded streets at Z=504.
        void Edge(string name,Vector3 center,Vector3 size){var node=PinebrookDistrictArt.Child(groups["Paths"],name);var box=node.gameObject.AddComponent<BoxCollider>();box.center=center;box.size=size;}
        float min=north.center.x-north.size.x/2,max=north.center.x+north.size.x/2,oldZ=north.center.z;
        if(min<plan.bounds.x)Edge("Retained north boundary west",new Vector3((min+plan.bounds.x)/2,2,oldZ),new Vector3(plan.bounds.x-min,4,1));
        if(max>plan.bounds.x+plan.bounds.width)Edge("Retained north boundary east",new Vector3((max+plan.bounds.x+plan.bounds.width)/2,2,oldZ),new Vector3(max-plan.bounds.x-plan.bounds.width,4,1));
        float newZ=plan.bounds.z+plan.bounds.depth;
        Edge("Pinebrook north boundary",new Vector3(plan.bounds.x+plan.bounds.width/2,2,newZ),new Vector3(plan.bounds.width,4,1));
        foreach(float x in new[]{plan.bounds.x,plan.bounds.x+plan.bounds.width})Edge("Pinebrook forest boundary "+x,new Vector3(x,2,(oldZ+newZ)/2),new Vector3(1,4,newZ-oldZ));
    }
    private void GroundAndStreets()
    {
        var surface=new TownMeshBuilder();var walks=new TownMeshBuilder();var extension=new TownMeshBuilder();
        var union=new PinebrookSurfaceUnion();var streets=new PinebrookStreetGeometry(plan);streets.Roads(union,surface);
        PinebrookParkingGeometry.Surfaces(plan.parking,union,surface);streets.Sidewalks(union,walks);
        foreach(var plaza in plan.plazas)union.Rectangle(plaza.Rect,walks,.145f,TownColor.Sand);
        foreach(var walk in plan.paths)union.Segment(walk.Start,walk.End,walk.width,walks,.145f,TownColor.Sand);
        extension.Ground(-30,539,760,602,-.02f,TownColor.Grass);
        art.Part(groups["Paths"],"NewRoadsUnion",surface,art.Ground,true);art.Part(groups["Paths"],"WalksAndSquaresUnion",walks,art.Ground,true);art.Part(groups["Greenery"],"NorthernGroundExtension",extension,AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/TownWorld/TownPalette.mat"),true);
        art.Part(groups["Paths"],"LaneAndCrosswalkPaint",streets.Paint(),art.Ground);
        art.Part(groups["Paths"],"ParkingBayPaint",PinebrookParkingGeometry.Paint(plan.parking),art.Ground);
    }
    private void PlaceObjects()
    {
        string letters="PINEBROOK PINEBROOK CAFE PARK KIOSK 0123456789 "+string.Join(" ",plan.placements.Select(p=>p.label));var type=new TownStaticTypography(PinebrookDistrictArt.Root,letters);
        foreach(var item in plan.placements)
        {
            var node=art.Place(groups[item.group],item.prefab,item.position,item.yaw,item.scale);node.name=item.name;
            if(string.IsNullOrWhiteSpace(item.label))continue;
            bool board=item.prefab=="Notice_Board";bool kiosk=item.prefab=="Kiosk";
            var p=item.prefab=="Entrance_Sign"?new Vector3(0,2.8f,.17f):item.prefab=="Street_Name_Sign"?new Vector3(0,2.4f,.09f):kiosk?new Vector3(0,2.47f,-1.09f):new Vector3(0,1.5f,-.145f);
            float width=item.prefab=="Entrance_Sign"?4.3f:item.prefab=="Street_Name_Sign"?2.5f:kiosk?2.1f:1.4f;
            float height=item.prefab=="Entrance_Sign"?.5f:.23f;
            if(item.prefab.StartsWith("label:")){p=Vector3.zero;width=item.prefab=="label:Cafe"?10:5.4f;height=item.prefab=="label:Cafe"?.42f:.28f;}
            if(item.prefab=="Parking_Sign"){p=new Vector3(0,1.95f,.085f);width=.55f;height=.5f;}
            if(item.prefab=="native:BusShelter"){p=new Vector3(0,3.15f,-.535f);width=5.4f;height=.28f;}
            type.Add(node,"Label_"+item.name,item.label,p,width,height,kiosk||board?180:0);
            if(board)
            {
                var dark=new Material(node.GetComponentsInChildren<MeshRenderer>().Last().sharedMaterial);dark.SetColor("_BaseColor",new Color(.16f,.24f,.2f));PinebrookDistrictArt.Save(dark,"Materials/DarkLettering.mat");node.GetComponentsInChildren<MeshRenderer>().Last().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(PinebrookDistrictArt.Root+"/Materials/DarkLettering.mat");
            }
        }
    }
    private void RenameCafe()
    {
        var cafe=town.transform.Find("Forest Quarter/Architecture/CafeCorner");
        foreach(string suffix in new[]{"FrontLabel","RearLabel"})
        {
            var old=cafe.Find("CafeCorner"+suffix);if(old==null)throw new InvalidOperationException("Retained cafe label anchor missing.");
            var renderer=old.GetComponent<MeshRenderer>();if(renderer!=null)Object.DestroyImmediate(renderer);var filter=old.GetComponent<MeshFilter>();if(filter!=null)Object.DestroyImmediate(filter);

        }
        var pavilion=town.transform.Find("Forest Quarter/Architecture/CommunityPavilion/CommunityLabel");if(pavilion!=null){var renderer=pavilion.GetComponent<MeshRenderer>();if(renderer!=null)Object.DestroyImmediate(renderer);var filter=pavilion.GetComponent<MeshFilter>();if(filter!=null)Object.DestroyImmediate(filter);}
    }
    private void RoadSigns()
    {
        var geometry=new PinebrookStreetGeometry(plan);int index=0;
        foreach(var junction in geometry.Junctions)
        {
            var arms=junction.Arms;
            if(arms.Length<3)continue;
            foreach(var outward in arms)
            {
                var arrival=-outward;var right=Vector3.Cross(Vector3.up,arrival);
                bool main=arms.Any(a=>Vector3.Dot(a,outward)<-.98f)&&(Mathf.Abs(outward.x)>.8f||Mathf.Abs(junction.Center.x+24)<.1f);
                var p=junction.Center+outward*(junction.Mouth(outward)+6)+right*5.5f;p.y=.16f;
                var sign=art.Place(groups["Signs"],main?"junction:PriorityRoadSign":"junction:YieldSign",p,Quaternion.LookRotation(outward).eulerAngles.y,1);
                sign.name="RightHandApproach_"+index++;
                if(Vector3.Dot(sign.forward,arrival)>-.99f||Vector3.Dot(p-junction.Center,right)<5.4f)throw new InvalidOperationException("Road sign is not on the right of its arrival lane.");
            }
        }
    }
}
