using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Additive woodland district; removes whole recognised vegetation models only in its clearings.</summary>
internal sealed class TownForestPlacement
{
    private readonly GameObject town;
    private readonly TownForestArt art=new();
    private readonly List<string> audit=new();
    private Transform site;
    internal TownForestPlacement(GameObject town){this.town=town;}
    internal void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(TownForestPlan.Site)!=null)throw new InvalidOperationException("District already exists; rebuild from recorded baseline.");
        ClearVegetation();var materialBefore=Materials();art.Build();var materialAfter=Materials();
        File.WriteAllLines(TownForestBatch.Report+"/material-differences.txt",materialBefore.Where(p=>materialAfter[p.Key]!=p.Value).Select(p=>p.Key+"\nBEFORE "+p.Value+"\nAFTER "+materialAfter[p.Key]));
        site=TownForestArt.Child(town.transform,TownForestPlan.Site);
        site.gameObject.AddComponent<TownForestMapData>().Configure(new Rect(-132,198,290,276),
            TownForestPlan.Roads.Concat(new[]{new Rect(-28,198,8,112)}).ToArray());
        Streets();Buildings();Courtyard(-76,"West court");Courtyard(28,"East court");Gateway();Townhouses();Park();Trail();
        File.WriteAllLines(TownForestBatch.Report+"/placement.txt",audit);
    }
    private Dictionary<string,string> Materials()=>town.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r=>r.sharedMaterials).Distinct()
        .ToDictionary(m=>AssetDatabase.GetAssetPath(m),m=>EditorJsonUtility.ToJson(m)+" TEXTURES "+string.Join(" ",m.GetTexturePropertyNames().Select(n=>n+"="+(m.GetTexture(n)!=null?m.GetTexture(n).name+":"+m.GetTexture(n).imageContentsHash:"null"))));
    private void ClearVegetation()
    {
        var protectedBefore=TownPilotScopePreservation.Capture(new[]{TownForestPlan.Permission});
        var selected=new List<Transform>();var keys=new HashSet<string>();int kept=0;
        foreach(var node in town.GetComponentsInChildren<Transform>(true))
        {
            if(node.name!="Pine"&&node.name!="TreeWide"&&node.name!="TreeRound")continue;
            if(!TownForestPlan.Contains(TownForestPlan.Permission,node.position))continue;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/"+node.name+".prefab");
            if(source==null)throw new InvalidOperationException("Missing native tree template "+node.name);
            var mesh=source.GetComponent<MeshFilter>().sharedMesh;var points=mesh.vertices.Select(node.TransformPoint).ToArray();
            var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
            if(!TownForestPlan.NeedsClearing(bounds)){kept++;continue;}
            if(!TownForestPlan.Contains(TownForestPlan.Permission,bounds.min)||!TownForestPlan.Contains(TownForestPlan.Permission,bounds.max))
                throw new InvalidOperationException("Whole tree clearance would exceed district permission at "+node.position);
            AddKeys(keys,mesh,node.localToWorldMatrix);selected.Add(node);
            var bush=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/BushRound.prefab").GetComponent<MeshFilter>().sharedMesh;
            AddKeys(keys,bush,Matrix4x4.TRS(node.position+new Vector3(2,0,-2),Quaternion.identity,Vector3.one*1.2f));
        }
        int copies=0,triangles=0;
        foreach(var f in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer=f.GetComponent<MeshRenderer>();if(renderer==null)continue;
            if(!renderer.bounds.Intersects(new Bounds(new Vector3(10,10,387),new Vector3(326,60,214))))continue;
            var mesh=CopyWithoutIndices(f.sharedMesh,f.transform,(a,b,c)=>
                TownForestPlan.Contains(TownForestPlan.Permission,a)&&TownForestPlan.Contains(TownForestPlan.Permission,b)&&TownForestPlan.Contains(TownForestPlan.Permission,c)&&keys.Contains(Key(a,b,c)),out int removed);
            if(removed==0){Object.DestroyImmediate(mesh);continue;}
            VerifyKeptAttributes(f.sharedMesh,mesh,f.transform);
            Directory.CreateDirectory(TownForestArt.Root+"/Meshes");string path="Meshes/NativeForestTrim"+copies+++".asset";TownForestArt.Save(mesh,path);
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(TownForestArt.Root+"/"+path);var collider=f.GetComponent<MeshCollider>();
            if(collider!=null&&collider.sharedMesh==f.sharedMesh)collider.sharedMesh=saved;f.sharedMesh=saved;triangles+=removed;
        }
        foreach(var node in selected)Object.DestroyImmediate(node.gameObject);
        PrefabUtility.SaveAsPrefabAsset(town,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
        protectedBefore.RequireUnchanged(TownPilotScopePreservation.Capture(new[]{TownForestPlan.Permission}));
        TownForestBatch.Require(selected.Count>30&&triangles>10000,"Forest recognition failed; refusing to build over trees.");
        audit.Add("Removed "+selected.Count+" whole native trees and their associated understory; "+triangles+" exact-template triangles in "+copies+" owned mesh copies. Retained "+kept+" original trees inside district scope plus all forest outside it.");
    }
    private static Mesh CopyWithoutIndices(Mesh source,Transform pose,Func<Vector3,Vector3,Vector3,bool> remove,out int removed)
    {
        // Preserve complete native vertex buffers, including format/precision and optional channels.
        // Only index lists change; unreferenced cleared-tree vertices do not render or collide.
        var points=source.vertices.Select(pose.TransformPoint).ToArray();var kept=new List<int>[source.subMeshCount];removed=0;
        for(int sub=0;sub<source.subMeshCount;sub++)
        {
            kept[sub]=new List<int>();var indices=source.GetTriangles(sub);
            for(int i=0;i<indices.Length;i+=3)
            {
                if(remove(points[indices[i]],points[indices[i+1]],points[indices[i+2]])){removed++;continue;}
                kept[sub].Add(indices[i]);kept[sub].Add(indices[i+1]);kept[sub].Add(indices[i+2]);
            }
        }
        if(removed==0)return null;
        var result=Object.Instantiate(source);for(int sub=0;sub<kept.Length;sub++)result.SetTriangles(kept[sub],sub,false);return result;
    }
    private static void VerifyKeptAttributes(Mesh original,Mesh copy,Transform pose)
    {
        var v=original.vertices;var n=original.normals;var uv=original.uv;var uv2=original.uv2;
        var cv=copy.vertices;var cn=copy.normals;var cuv=copy.uv;var cuv2=copy.uv2;
        var originals=Enumerable.Range(0,v.Length).GroupBy(i=>v[i]).ToDictionary(g=>g.Key,g=>g.ToArray());
        for(int i=0;i<cv.Length;i++)
        {
            if(TownForestPlan.Contains(TownForestPlan.Permission,pose.TransformPoint(cv[i])))continue;
            if(!originals[cv[i]].Any(j=>n[j].Equals(cn[i])&&uv[j].Equals(cuv[i])&&(uv2.Length==v.Length?uv2[j]:Vector2.zero).Equals(cuv2.Length==cv.Length?cuv2[i]:Vector2.zero)))
                throw new InvalidOperationException("Trim altered attributes outside district in "+original.name+" at "+cv[i]+" n="+cn[i]+" uv="+cuv[i]+" uv2="+cuv2[i]);
        }
    }
    private static string Point(Vector3 p)=>Mathf.RoundToInt(p.x*100)+","+Mathf.RoundToInt(p.y*100)+","+Mathf.RoundToInt(p.z*100);
    private static string Key(Vector3 a,Vector3 b,Vector3 c)=>string.Join("|",new[]{Point(a),Point(b),Point(c)}.OrderBy(s=>s,StringComparer.Ordinal));
    private static void AddKeys(HashSet<string> keys,Mesh mesh,Matrix4x4 pose)
    {var v=mesh.vertices.Select(pose.MultiplyPoint3x4).ToArray();var indices=mesh.triangles;for(int i=0;i<indices.Length;i+=3)keys.Add(Key(v[indices[i]],v[indices[i+1]],v[indices[i+2]]));}

    private void Streets()
    {
        var group=TownForestArt.Child(site,"Street network");var floor=new TownMeshBuilder();var seams=new TownMeshBuilder();
        // One union surface per cell: no coplanar road overlaps, no raised colliding paint.
        for(int x=-128;x<80;x++)for(int z=302;z<438;z++)
        {
            if(x>=-30&&x<-18&&z<310)continue; // Retain the accepted crossing's last metres exactly.
            var p=new Vector3(x+.5f,0,z+.5f);bool road=TownForestPlan.OnRoad(p),walk=!road&&TownForestPlan.OnRoad(p,3);
            if(!road&&!walk)continue;
            float height=road?.12f:.16f;floor.Ground(p.x,p.z,1,1,height,road?TownColor.Asphalt:TownColor.Pavement);
            if(walk&&(x%2==0||z%2==0))seams.Ground(p.x,p.z,.98f,.012f,.163f,TownColor.Stone);
        }
        art.Part(group,"ForestRoadAndWalkSurface",floor,true);art.Part(group,"ForestPavingJoints",seams);
        // Side streets yield to the entrance avenue; no unnecessary traffic signals in a small local loop.
        foreach(float z in new[]{310f,430f})foreach(float sign in new[]{-1f,1f})
        {
            Sign(group,"YieldSign",-24+sign*11,z+sign*5,sign<0?270:90);
        }
        Sign(group,"PriorityRoadSign",-29.3f,299,180);Sign(group,"PriorityRoadSign",-18.7f,322,0);Sign(group,"PriorityRoadSign",-29.3f,418,180);
        TownForestRoadStyle.Apply(group);
        var kit=TownForestArt.Child(site,"Street furnishings");
        foreach(float x in new[]{-127f,-31f,79f})foreach(float z in new[]{330f,390f,412f})Place(kit,"QuarterLamp",x,z,x==79?180:0,.16f);
        foreach(float x in new[]{-108f,-44f,16f,56f})Place(kit,"QuarterLamp",x,437,90,.16f);
        foreach(float z in new[]{338f,392f})Place(kit,"StreetBin",-31,z,0,.16f);
        Parking(kit,-102,320,4,false);Parking(kit,51,320,4,false);Parking(kit,-109,414,3,true);Parking(kit,59,414,3,true);
        Place(kit,"BusShelter",-35,344,270,.16f);Place(kit,"DistrictMarker",-14,291,180,0);
        var stopPad=new TownMeshBuilder();stopPad.Ground(-33.5f,344,7,8,.16f,TownColor.Pavement);art.Part(kit,"BusStopPad",stopPad,true);
        Place(kit,"UrbanTree",-38,328,0,0);Place(kit,"MixedPlanting",-38,328,0,0);
        art.Combine(kit,"StreetFurniture");
        audit.Add("Existing crossing retained; 8 m carriageways, 3 m pavement strips, connected local loop and entrance avenue. Fourteen parking bays; seven reused priority/yield signs, no extra signal controllers.");
    }
    private void Parking(Transform group,float x,float z,int count,bool reverse)
    {
        var paving=new TownMeshBuilder();paving.Ground(x+(count-1)*1.4f,z,count*2.8f+1,5.6f,.125f,TownColor.Asphalt);
        var lines=new TownMeshBuilder();for(int i=0;i<=count;i++)lines.Ground(x-1.4f+i*2.8f,z,.08f,5.3f,.135f,TownColor.White);
        string key="Parking"+x+"_"+z;art.Part(group,key,paving,true);art.Part(group,key+"Paint",lines);
        for(int i=0;i<count-1;i++)Place(group,i%2==0?"BlueCustomerCar":"GreenCustomerCar",x+i*2.8f,z,reverse?180:0,.13f);
    }
    private void Sign(Transform parent,string id,float x,float z,float yaw)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownJunctions/Prefabs/"+id+".prefab");
        if(source==null)throw new InvalidOperationException("Accepted road sign missing "+id);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(source);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        go.name=id;go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(x,.16f,z);go.transform.localRotation=Quaternion.Euler(0,yaw,0);
    }
    private void Buildings()
    {
        var group=TownForestArt.Child(site,"Architecture");
        foreach(var home in TownForestPlan.Homes)art.Place(group,home.Id,home.Position,home.Yaw);
        audit.Add("Six courtyard homes (two with physical ground-floor passages), two mixed-use corner homes, four townhouse sections and one open community pavilion. Homes are exterior art, not new economy/tenancy systems.");
    }
    private void Courtyard(float x,string label)
    {
        var group=TownForestArt.Child(site,label);var paving=new TownMeshBuilder();
        paving.Ground(x,367,2.8f,54,.14f,TownColor.Pavement);paving.Ground(x,366,47,2.8f,.14f,TownColor.Pavement);
        paving.Ground(x+(x<0?32:-32),366,26,2.8f,.14f,TownColor.Pavement);
        paving.Ground(x,385,42,2.8f,.14f,TownColor.Pavement);
        // Deliberate garden paths rather than a solid paved courtyard.
        foreach(float side in new[]{-1f,1f})
        {
            paving.Ground(x+side*15,376,2.2f,20,.14f,TownColor.Sand);
            Place(group,"Pergola",x+side*15,376,0,.14f);
            Place(group,"TimberBench",x+side*15,374.5f,0,.14f);Place(group,"TimberBench",x+side*15,377.5f,180,.14f);
            foreach(float z in new[]{354f,389f})
            {
                Place(group,"TreePlanter",x+side*14,z,0,0);Place(group,"UrbanTree",x+side*14,z,side*37,0);
                Place(group,"FlowerStrip",x+side*14,z+2.8f,0,0);
            }
            Place(group,"MixedPlanting",x+side*20,360,90,0);Place(group,"MixedPlanting",x+side*20,382,90,0);
            Place(group,"ShortBench",x+side*7,385,0,.14f);
        }
        foreach(float z in new[]{347f,391f}){Place(group,"QuarterLamp",x+3,z,180,.14f);Place(group,"StreetBin",x+3,z+1.4f,0,.14f);}
        Place(group,"BikeRack",x+5,392.5f,0,.14f);Place(group,"Mailboxes",x-5,392.5f,180,.14f);
        paving.Arc(new Vector3(x,.142f,374),8,10,0,360,TownColor.Sand,48);
        foreach(float side in new[]{-1f,1f})
        {
            foreach(float z in new[]{345f,390f})PlantGroup(group,x+side*27,z,1.15f);
            foreach(float offset in new[]{12f,28f})PlantGroup(group,x+side*offset,418,1.1f);
            FlowerBorder(group,new Vector3(x+side*9,0,350),new Vector3(x+side*9,0,359));
            FlowerBorder(group,new Vector3(x+side*7,0,370),new Vector3(x+side*7,0,378));
            FlowerBorder(group,new Vector3(x+side*9,0,388),new Vector3(x+side*16,0,388));
        }
        art.Part(group,label.Replace(" ","")+"Paths",paving,true);art.Combine(group,label.Replace(" ",""));
    }
    private void Gateway()
    {
        var group=TownForestArt.Child(site,"Gateway square");var paving=new TownMeshBuilder();
        foreach(float x in new[]{-76f,28f})
        {
            paving.Ground(x,320,29,8,.145f,TownColor.Pavement);
            foreach(float side in new[]{-1f,1f}){Place(group,"TreePlanter",x+side*12,321,0,.145f);Place(group,"UrbanTree",x+side*12,321,side*15,.18f);}
            Place(group,"StreetBin",x+10,324,0,.145f);Place(group,"BikeRack",x-8,324,90,.145f);
        }
        foreach(float x in new[]{-82f,-70f}){Place(group,"CafeTable",x,320,0,.145f);Place(group,"CafeUmbrella",x,320,0,.145f);}
        Place(group,"CafeMenu",-73,323,180,.145f);Place(group,"ParcelLocker",38,341,180,.145f);
        Place(group,"CycleShelter",43,343,90,.145f);Place(group,"BinEnclosure",-96,344,90,.12f);Place(group,"BinEnclosure",49,344,270,.12f);
        Place(group,"UtilityScreen",-96,339,90,.12f);Place(group,"UtilityScreen",49,339,270,.12f);
        paving.Ground(-96,343,7,11,.13f,TownColor.Pavement);paving.Ground(46,343,10,11,.13f,TownColor.Pavement);
        PlantGroup(group,-107,333,1.1f);PlantGroup(group,60,333,1.1f);
        FlowerBorder(group,new Vector3(-112,0,327),new Vector3(-97,0,327));
        FlowerBorder(group,new Vector3(49,0,327),new Vector3(64,0,327));
        art.Part(group,"GatewayPlazaPaving",paving,true);art.Combine(group,"GatewaySquare");
    }
    private void Townhouses()
    {
        var group=TownForestArt.Child(site,"Townhouse gardens");var paving=new TownMeshBuilder();var fences=new TownMeshBuilder();var fenceLines=new HashSet<int>();
        foreach(var home in TownForestPlan.Homes.Where(h=>h.Id.StartsWith("Townhouse",StringComparison.Ordinal)))
        {
            float x=home.Position.x;paving.Ground(x,441,2,12,.14f,TownColor.Pavement);
            paving.Ground(x,461,9,5,.04f,TownColor.Grass);
            foreach(float side in new[]{-1f,1f})
            {
                float edge=x+side*4.2f;if(!fenceLines.Add(Mathf.RoundToInt(edge*100)))continue;
                fences.Box(new Vector3(edge,.5f,460),new Vector3(.08f,1,8),TownColor.Wood);
                for(int i=0;i<8;i++)fences.Box(new Vector3(edge,.5f,456+i),new Vector3(.13f,1,.09f),TownColor.WoodLight);
            }
            Place(group,"Mailboxes",x+2,438,180,.14f);Place(group,"MixedPlanting",x-3,440,90,0);
            Place(group,"FlowerStrip",x-3,441,0,0);Place(group,"UrbanTree",x-3,463,home.Yaw,0,.8f);
            FlowerBorder(group,new Vector3(x-4,0,466),new Vector3(x+4,0,466));
        }
        art.Part(group,"TownhousePaths",paving,true);art.Part(group,"TownhouseGardenFences",fences);art.Combine(group,"TownhouseGarden");
    }
    private void Park()
    {
        var group=TownForestArt.Child(site,"Community park");var paving=new TownMeshBuilder();
        paving.Ground(108,380,3,80,.14f,TownColor.Sand);paving.Ground(95,380,29,3,.14f,TownColor.Pavement);
        paving.Ground(120,402,15,12,.14f,TownColor.Pavement);paving.Ground(124,395,29,2.5f,.14f,TownColor.Sand);
        paving.Ground(120,365,24,18,.035f,TownColor.Sand);
        Place(group,"PlayTower",119,362,0,.04f);Place(group,"SwingSet",129,368,90,.04f);Place(group,"Sandpit",113,369,0,.04f);
        foreach(var p in new[]{new Vector2(113,379),new(130,379),new(114,411),new(130,411)})Place(group,"TimberBench",p.x,p.y,p.y<400?0:180,.14f);
        Place(group,"CafeTable",117,402,0,.14f);Place(group,"CafeTable",123,402,0,.14f);
        Place(group,"BikeRack",110,398,0,.14f);Place(group,"TrailMarker",106,419,180,.14f);
        foreach(float z in new[]{344f,382f,413f}){Place(group,"QuarterLamp",105,z,0,.14f);Place(group,"StreetBin",105,z+2,0,.14f);}
        foreach(var p in new[]{new Vector2(91,337),new(137,340),new(92,359),new(138,386),new(94,414),new(138,414)})
        {Place(group,"UrbanTree",p.x,p.y,p.x*3,0,1.15f);Place(group,"MixedPlanting",p.x,p.y+2,0,0);}
        // A visual rain garden; no water simulation or unsupported drainage mechanic.
        paving.Arc(new Vector3(122,.03f,342),0,7,0,360,TownColor.Soil,24);
        paving.Arc(new Vector3(122,.032f,342),5.8f,7.1f,0,360,TownColor.Sand,24);
        for(int i=0;i<14;i++){float a=i*Mathf.PI*2/14;Place(group,"MixedPlanting",122+Mathf.Cos(a)*5.2f,342+Mathf.Sin(a)*5.2f,i*37,0,.65f);}
        foreach(float z in new[]{352f,386f,415f})Place(group,"FlowerStrip",101,z,90,0);
        foreach(var p in new[]{new Vector2(93,349),new(137,356),new(94,391),new(136,398),new(116,421),new(132,423)})PlantGroup(group,p.x,p.y,1.1f);
        FlowerBorder(group,new Vector3(88,0,331),new Vector3(102,0,331));
        FlowerBorder(group,new Vector3(89,0,386),new Vector3(89,0,405));
        FlowerBorder(group,new Vector3(115,0,414),new Vector3(132,0,414));
        Ribbon(paving,new Vector3(108,0,420),new Vector3(100,0,438),2.4f,.145f,TownColor.Sand);
        // Small secondary loop ties the playground and rain garden together instead of leaving a bare lawn.
        paving.Arc(new Vector3(122,.047f,342),8.5f,10.5f,0,360,TownColor.Sand,48);
        Ribbon(paving,new Vector3(108,0,342),new Vector3(113,0,342),2.5f,.05f,TownColor.Sand);
        art.Part(group,"ParkPathsAndRainGarden",paving,true);art.Combine(group,"CommunityPark");
    }
    private void Trail()
    {
        var group=TownForestArt.Child(site,"Woodland trail");var ground=new TownMeshBuilder();
        for(int i=1;i<TownForestPlan.Trail.Length;i++)Ribbon(ground,TownForestPlan.Trail[i-1],TownForestPlan.Trail[i],2.4f,.045f,TownColor.Sand);
        Ribbon(ground,new Vector3(79,0,420),TownForestPlan.Trail[0],2.4f,.045f,TownColor.Sand);
        Ribbon(ground,TownForestPlan.Trail[TownForestPlan.Trail.Length-1],new Vector3(79,0,306),2.4f,.045f,TownColor.Sand);
        foreach(var p in new[]{new Vector2(139,435),new(147,389),new(137,310)})
        {Place(group,"TimberBench",p.x,p.y,90,.05f);Place(group,"TrailMarker",p.x+2,p.y,180,0);}
        art.Part(group,"WoodlandGravelTrail",ground,true);art.Combine(group,"WoodlandTrail");
        audit.Add("Community park, play equipment, open pavilion, planted rain garden and connected gravel woodland trail. No mass clearing of the forest outside actual development envelopes.");
    }
    private void PlantGroup(Transform group,float x,float z,float scale)
    {
        Place(group,"UrbanTree",x,z,x*7,0,scale);
        Place(group,"MixedPlanting",x-2,z+1.9f,35,0,.8f);Place(group,"MixedPlanting",x+2,z+.8f,125,0,.65f);
        Place(group,"FlowerStrip",x,z+3.2f,0,0,.8f);
    }
    private void FlowerBorder(Transform group,Vector3 a,Vector3 b)
    {
        var delta=b-a;int count=Mathf.CeilToInt(delta.magnitude/3.8f);float yaw=Quaternion.LookRotation(delta).eulerAngles.y+90;
        for(int i=0;i<count;i++)
        {
            var p=Vector3.Lerp(a,b,(i+.5f)/count);art.Place(group,"FlowerStrip",p,yaw,.85f);
            art.Place(group,"MixedPlanting",p-Vector3.up*.05f,yaw,.55f);
        }
    }
    private static void Ribbon(TownMeshBuilder d,Vector3 a,Vector3 b,float width,float y,TownColor color)
    {
        a.y=y;b.y=y;var right=Vector3.Cross(Vector3.up,(b-a).normalized)*width*.5f;
        d.Quad(a-right,b-right,b+right,a+right,color);
    }
    private Transform Place(Transform parent,string id,float x,float z,float yaw=0,float y=TownForestPlan.Floor,float scale=1)
        =>art.Place(parent,id,new Vector3(x,y,z),yaw,scale);
}
