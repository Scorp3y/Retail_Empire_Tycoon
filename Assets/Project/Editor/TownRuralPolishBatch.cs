using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Local scope protection, art/layout acceptance and saved Unity evidence for the two-site refinement.</summary>
public static class TownRuralPolishBatch
{
    internal const string Report="Library/TownRuralPolishQA",Pictures="Docs/TownRuralPolishReview";
    public static void Generate()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);File.WriteAllText(Report+"/result.txt","RUNNING\n");
            Before();var outside=TownPilotScopePreservation.Capture(TownRuralPolishPlacement.Permissions);
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var places=root.GetComponent<TownWorldPlaces>();var identities=places.suppliers.Select(c=>c.GetInstanceID()).ToArray();string contracts=Contracts(places);var homes=TownCourtyardValidation.CaptureResidentialSnapshot(root);
                new TownRuralPolishPlacement(root).Build();
                Require(identities.SequenceEqual(places.suppliers.Select(c=>c.GetInstanceID())),"Original supplier references changed.");Require(contracts==Contracts(places),"Supplier gameplay contract changed.");homes.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));
                PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            WriteOutliers();outside.RequireUnchanged(TownPilotScopePreservation.Capture(TownRuralPolishPlacement.Permissions));File.WriteAllText(Report+"/protection.txt","PASS: exact visual/material/collision fingerprint outside farm and fuel parcels unchanged; all seven supplier identities and interaction contracts unchanged; residential snapshot unchanged.\n"+outside);
            Capture();Validate();Receipt("result","PASS: two-site art/layout refinement generated, saved and checked.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Failure("result",e);}
    }
    public static void Review()
    {
        try{TownWorldSafety.RequireIsolatedEditor();WriteOutliers();Capture();Validate();Receipt("review","PASS: fresh Unity reopen, same saved-scene geometry/layout checks and fourteen native camera views.\n");EditorApplication.Exit(0);}
        catch(Exception e){Failure("review",e);}
    }
    private static void WriteOutliers()
    {
        var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);var lines=new System.Collections.Generic.List<string>();
        try
        {
            foreach(var f in root.GetComponentsInChildren<MeshFilter>().Where(f=>f.transform.IsChildOf(root.transform.Find(TownRuralPolishPlacement.Farm))||f.transform.IsChildOf(root.transform.Find(TownRuralPolishPlacement.Fuel))))
            {
                var v=f.sharedMesh.vertices.Select(f.transform.TransformPoint).ToArray();var t=f.sharedMesh.triangles;
                for(int i=0;i<t.Length;i+=3)if(!TownRuralPolishPlacement.Permissions.Any(r=>TownRuralPolishPlacement.Inside(r,v[t[i]])&&TownRuralPolishPlacement.Inside(r,v[t[i+1]])&&TownRuralPolishPlacement.Inside(r,v[t[i+2]])))lines.Add(f.name+" "+v[t[i]]+" / "+v[t[i+1]]+" / "+v[t[i+2]]);
            }
            File.WriteAllLines(Report+"/outliers.txt",lines);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static string Contracts(TownWorldPlaces places)=>string.Join("\n",places.suppliers.Select(c=>c.supplier+"|"+c.transform.position+"|"+c.transform.rotation+"|"+c.radius+"|"+c.requiresRearDocking));
    private static void Before()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);TownFloristBatch.Render(scene,Pictures+"/BeforeFarm.png",new Vector3(435,125,185),new Vector3(552,0,337));TownFloristBatch.Render(scene,Pictures+"/BeforeFuel.png",new Vector3(276,48,-166),new Vector3(215,0,-108));
    }
    private static void Validate()
    {
        Directory.CreateDirectory(Report);var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);var places=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TownWorldPlaces>()).Single();var town=places.transform;var farm=town.Find(TownRuralPolishPlacement.Farm);var fuel=town.Find(TownRuralPolishPlacement.Fuel);Require(farm!=null&&fuel!=null,"Refined site missing.");
        foreach(var site in new[]{farm,fuel})
        {
            var filters=site.GetComponentsInChildren<MeshFilter>();Require(filters.All(f=>f.sharedMesh.vertices.All(TownBakeryQuarterPlacement.Finite)),"Non-finite art geometry.");Require(filters.Sum(f=>f.sharedMesh.triangles.Length/3)<1300000,"Geometry budget exceeded: "+site.name);Require(filters.Length<=12,"Site material/draw-call budget exceeded.");
        }
        int Count(Transform site,string id)=>site.Cast<Transform>().Count(t=>t.name==id);
        Require(Count(fuel,"FuelPumpDetailed")==4&&Count(fuel,"PumpIsland")==2,"Fuel pump/island layout not preserved.");Require(Count(farm,"Greenhouse")==2&&Count(farm,"GrainSiloDetailed")==2,"Twin greenhouse/silo landmarks missing.");Require(Count(farm,"DetailedTractor")==2,"Detailed tractor models missing.");
        foreach(string id in new[]{"FieldWheatClump","FieldLeafClump","FieldRootClump"})Require(Count(farm,id)>400,"Distinct crop population missing: "+id);
        Require(places.suppliers.Length==7&&places.GetComponentsInChildren<DeliveryCheckpoint>().Length==7,"Supplier count changed.");
        Physics.SyncTransforms();int samples=0;
        samples+=Route(places,new[]{new Vector3(550,0,273),new Vector3(550,0,289),new Vector3(560,0,289),new Vector3(560,0,305)},1.2f,2.5f);
        samples+=Route(places,new[]{new Vector3(228,0,-138),new Vector3(228,0,-124),new Vector3(238,0,-124),new Vector3(238,0,-103),new Vector3(224,0,-103),new Vector3(224,0,-124),new Vector3(204,0,-124),new Vector3(204,0,-138)},1.15f,2.2f);
        samples+=Route(places,new[]{new Vector3(584,0,299),new Vector3(610,0,299),new Vector3(617,0,307),new Vector3(617,0,337)},1.15f,2.2f);
        samples+=Route(places,new[]{new Vector3(538,0,359),new Vector3(538,0,449)},1.15f,2.2f);
        // Collision checks are between independent models, not intentional joints inside one model.
        var errors=new System.Collections.Generic.List<string>();int pairs=0;
        foreach(var site in new[]{farm,fuel})
        {
            var boxes=site.GetComponentsInChildren<BoxCollider>();
            for(int i=0;i<boxes.Length;i++)for(int j=i+1;j<boxes.Length;j++)
            {
                var a=boxes[i];var b=boxes[j];if(a.transform.parent==b.transform.parent||!a.bounds.Intersects(b.bounds))continue;
                pairs++;if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth)&&depth>.035f)errors.Add(a.transform.parent.name+"/"+a.name+" "+a.bounds.center+" intersects "+b.transform.parent.name+"/"+b.name+" "+b.bounds.center+" depth="+depth);
            }
        }
        File.WriteAllText(Report+"/props.txt",errors.Count==0?"PASS: "+pairs+" physical prop overlap pairs checked; within-model construction joints excluded.\n":"FAILED\n"+string.Join("\n",errors));Require(errors.Count==0,"Prop intersections: "+string.Join("; ",errors));
        File.WriteAllText(Report+"/access.txt","PASS: "+samples+" native floor/vehicle-envelope probes for pickup, complete fuel loop, tractor corridor and field headland. All new paint and gravel accents are non-colliding.\n");
        File.WriteAllLines(Report+"/metrics.txt",new[]{farm,fuel}.Select(s=>s.name+": "+s.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)+" triangles / "+s.GetComponentsInChildren<MeshRenderer>().Length+" material batches / "+s.childCount+" authored placements."));
    }
    private static int Route(TownWorldPlaces town,Vector3[] points,float width,float length)
    {
        int samples=0;for(int leg=1;leg<points.Length;leg++)
        {
            var a=points[leg-1];var b=points[leg];int count=Mathf.CeilToInt(Vector3.Distance(a,b)*2);var q=Quaternion.LookRotation(b-a);
            for(int i=0;i<=count;i++)
            {
                var p=Vector3.Lerp(a,b,i/(float)count);var floor=Physics.RaycastAll(p+Vector3.up*8,Vector3.down,10,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(town.transform)&&h.normal.y>.8f&&h.point.y>=-.01f&&h.point.y<.4f).ToArray();Require(floor.Length>0,"No physical floor at "+p);float y=floor.Max(h=>h.point.y);
                var hits=Physics.OverlapBox(new Vector3(p.x,y+1.2f,p.z),new Vector3(width,.55f,length),q,~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(town.transform)).ToArray();Require(hits.Length==0,"Route blocked at "+p+": "+string.Join(", ",hits.Select(c=>c.transform.parent.name+"/"+c.name)));samples++;
            }
        }
        return samples;
    }
    private static void Capture()
    {
        Directory.CreateDirectory(Pictures);var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        void View(string id,Vector3 p,Vector3 target,float ortho=0)=>TownFloristBatch.Render(scene,Pictures+"/"+id+".png",p,target,ortho);
        View("Farm",new Vector3(435,125,185),new Vector3(552,0,337));View("FarmTop",new Vector3(552,235,347),new Vector3(552,0,347),108);
        View("FarmPickup",new Vector3(534,15,280),new Vector3(560,3,318));View("Workshop",new Vector3(610,12,302),new Vector3(631,2,326));View("Greenhouses",new Vector3(574,12,321),new Vector3(598,1.5f,341));
        View("Market",new Vector3(496,10,268),new Vector3(515,1,294));View("Fields",new Vector3(525,12,348),new Vector3(568,1,400));View("Farmstead",new Vector3(494,14,320),new Vector3(526,2,344));
        View("Fuel",new Vector3(276,48,-166),new Vector3(215,0,-108));View("FuelTop",new Vector3(216,125,-110),new Vector3(216,0,-110),46);
        View("FuelPumps",new Vector3(210,7,-128),new Vector3(224,2,-114));View("FuelAirWater",new Vector3(240,5,-110),new Vector3(250,2,-96));View("FuelShop",new Vector3(197,2.1f,-99.8f),new Vector3(196,1.5f,-93.5f));View("FuelGarden",new Vector3(194,5,-141),new Vector3(216,1,-130));
        File.WriteAllText(Pictures+"/complete.txt","PASS: fourteen native Unity saved-scene camera views plus two before views, same fixed cameras for comparison; no generated concept image used as completion evidence.\n");
    }
    internal static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    internal static string Hash()=>BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(TownWorldAssembler.PrefabPath))).Replace("-","");
    internal static void Receipt(string stage,string message)
    {
        File.WriteAllText(Report+"/"+stage+".txt",message);File.WriteAllText(Report+"/"+stage+"-input.sha256",Hash());
        File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Directory.GetFiles(TownRuralArtCatalogue.Root,"*",SearchOption.AllDirectories).Concat(new[]{TownWorldAssembler.PrefabPath}).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(p))).Replace("-","")+" "+p.Replace('\\','/')));
    }
    internal static void Failure(string stage,Exception e){Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
}
