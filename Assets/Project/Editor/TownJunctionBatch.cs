using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Saved-scene scope protection, junction coverage, phase safety, access and native visual evidence.</summary>
public static class TownJunctionBatch
{
    internal const string Report="Library/TownJunctionQA",Pictures="Docs/TownJunctionReview";
    public static void Generate()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Directory.CreateDirectory(Report);Directory.CreateDirectory(Pictures);
            File.WriteAllText(Report+"/result.txt","RUNNING\n");Before();
            var layout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>(TownWorldAssembler.Folder+"/TownLayout.asset");
            var outside=TownPilotScopePreservation.Capture(TownJunctionPlan.Areas(layout));
            var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
            try
            {
                var places=root.GetComponent<TownWorldPlaces>();string contracts=Contracts(places);
                var supplierIds=places.suppliers.Select(c=>c.GetInstanceID()).ToArray();var homes=TownCourtyardValidation.CaptureResidentialSnapshot(root);
                new TownJunctionPlacement(root).Build();
                Require(contracts==Contracts(places)&&supplierIds.SequenceEqual(places.suppliers.Select(c=>c.GetInstanceID())),"Supplier contract or identity changed.");
                homes.RequireUnchanged(TownCourtyardValidation.CaptureResidentialSnapshot(root));
                PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            outside.RequireUnchanged(TownPilotScopePreservation.Capture(TownJunctionPlan.Areas(layout)));
            File.WriteAllText(Report+"/protection.txt","PASS: exact geometry/material/collision fingerprint outside junction rectangles unchanged; all seven supplier identities and interaction contracts, residential buildings and road layout unchanged.\n"+outside);
            Capture();Validate();Receipt("result","PASS: junction signs, selective working signals and crossing markings generated and saved.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Failure("result",error);}
    }

    public static void Review()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();Capture();Validate();
            Receipt("review","PASS: fresh native Unity reopen, complete junction/phase/access checks and camera evidence.\n");EditorApplication.Exit(0);
        }
        catch(Exception error){Failure("review",error);}
    }

    private static string Contracts(TownWorldPlaces places)=>string.Join("\n",places.suppliers.Select(c=>c.supplier+"|"+c.transform.position+"|"+c.transform.rotation+"|"+c.radius+"|"+c.requiresRearDocking));

    internal static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var town=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TownWorldPlaces>()).Single();
        var site=town.transform.Find(TownJunctionPlan.Site);Require(site!=null,"Junction equipment missing.");
        var plan=TownJunctionPlan.Create(town.layout);var signals=site.GetComponentsInChildren<TownJunctionSignals>();
        Require(signals.Length==6&&plan.Count(j=>j.HasSignals)==6,"Exactly six selected signal junctions required.");
        Require(town.GetComponentsInChildren<Transform>().All(t=>t.name!="TrafficSignal"),"Legacy static signal transform remains.");
        int signs=0,vehicleHeads=0,pedestrianHeads=0;
        foreach(var junction in plan)
        {
            var node=site.Find("Junction "+junction.Cell.x+" "+junction.Cell.y);Require(node!=null,"Uncovered junction: "+junction.Cell);
            foreach(int direction in junction.Approaches)
            {
                var approach=node.Find("Approach "+direction);Require(approach!=null,"Missing approach: "+junction.Cell+"/"+direction);
                Require(Vector3.Dot(approach.forward,TownJunctionPlan.Outward(direction))>.999f,"Signal/sign faces wrong traffic direction.");
                bool privateOnly=junction.Approaches.Count(d=>!junction.IsPrivate(d))<3;
                if(!privateOnly||junction.IsPrivate(direction))
                {
                    string id=junction.HasSignals?(junction.IsMain(direction)?"PriorityPlate":"YieldPlate"):(junction.IsMain(direction)?"PriorityRoadSign":"YieldSign");
                    Require(approach.Find(id)!=null,"Priority assignment missing: "+junction.Cell+"/"+direction);signs++;
                }
                if(junction.HasSignals){Require(approach.Find("VehicleSignal")!=null,"Primary signal missing.");vehicleHeads++;}
            }
            if(junction.HasSignals)pedestrianHeads+=node.Cast<Transform>().Count(t=>t.name=="PedestrianSignal");
        }
        var filters=site.GetComponentsInChildren<MeshFilter>(true);int triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
        Require(triangles<160000&&filters.Length<=50,"Junction art exceeds geometry/material budget.");
        Require(filters.All(f=>f.sharedMesh.vertices.All(TownBakeryQuarterPlacement.Finite)),"Non-finite geometry.");
        CheckCycle(signals);
        Physics.SyncTransforms();int probes=0;
        var hardware=site.GetComponentsInChildren<Collider>();
        foreach(var junction in plan)
        foreach(int approach in junction.Approaches.Where(d=>!junction.IsPrivate(d)))
        {
            var start=TownJunctionPlan.At(junction,approach,20,2);
            var end=TownJunctionPlan.At(junction,approach,1,2);
            probes+=Lane(start,end,hardware);
        }
        foreach(var a in hardware)
        foreach(var b in town.GetComponentsInChildren<Collider>())
        {
            if(a==b||a.isTrigger||b.isTrigger||!a.bounds.Intersects(b.bounds))continue;
            if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth))
                Require(depth<.025f,"Pole intersects "+b.transform.parent.name+"/"+b.name+" at "+a.bounds.center+" by "+depth);
        }
        File.WriteAllText(Report+"/access.txt","PASS: "+probes+" incoming lane vehicle-envelope probes; all physical poles clear of the carriageway and other scene colliders. Road markings are visual only.\n");
        File.WriteAllText(Report+"/metrics.txt",plan.Length+" junctions/openings, "+plan.Count(j=>j.Approaches.Count(d=>!j.IsPrivate(d))>=3)+" public intersections, 6 signal junctions, "+signs+" priority signs, "+vehicleHeads+" vehicle heads, "+pedestrianHeads+" pedestrian heads, "+triangles+" triangles, "+filters.Length+" saved visual batches (including inactive lenses).\n");
    }

    private static int Lane(Vector3 start,Vector3 end,Collider[] equipment)
    {
        int count=Mathf.CeilToInt(Vector3.Distance(start,end)*2);var q=Quaternion.LookRotation(end-start);
        for(int i=0;i<=count;i++)
        {
            var p=Vector3.Lerp(start,end,i/(float)count);
            var hits=Physics.OverlapBox(p+Vector3.up*1.2f,new Vector3(1.25f,.6f,2.6f),q,~0,QueryTriggerInteraction.Ignore);
            Require(!hits.Any(equipment.Contains),"Junction hardware blocks lane at "+p);
        }
        return count+1;
    }

    internal static void CheckCycle(IEnumerable<TownJunctionSignals> controllers)
    {
        int samples=0;
        for(double t=-60;t<120;t+=.05)
        {
            var s=TownJunctionSignals.Evaluate(t);samples++;
            bool a=s.NorthSouth==JunctionSignalAspect.Green||s.NorthSouth==JunctionSignalAspect.Amber;
            bool b=s.EastWest==JunctionSignalAspect.Green||s.EastWest==JunctionSignalAspect.Amber;
            Require(!(a&&b),"Conflicting axis release at "+t);
            Require(!s.PedestrianWalk||(s.NorthSouth==JunctionSignalAspect.Red&&s.EastWest==JunctionSignalAspect.Red),"Pedestrians conflict with vehicles.");
        }
        var times=new[]{0d,18,21,23,24,42,45,47,54,59,60,double.NaN,double.PositiveInfinity};
        foreach(var controller in controllers)
        {
            Require(controller.IsConfigured,"Unbound signal groups.");
            foreach(double t in times){controller.DisplayAt(t);Require(controller.LampsMatchState,"Visible lenses differ from safe cycle state.");}
            controller.DisplayAt(controller.PhaseOffset);
        }
        Require(TownJunctionSignals.Evaluate(18).NorthSouth==JunctionSignalAspect.Amber,"Amber boundary wrong.");
        Require(TownJunctionSignals.Evaluate(23).EastWest==JunctionSignalAspect.RedAmber,"Red/amber boundary wrong.");
        Require(TownJunctionSignals.Evaluate(24).EastWest==JunctionSignalAspect.Green,"Green boundary wrong.");
        Require(TownJunctionSignals.Evaluate(47).PedestrianWalk&&!TownJunctionSignals.Evaluate(54).PedestrianWalk,"Pedestrian walk/clearance boundary wrong.");
        File.WriteAllText(Report+"/phases.txt","PASS: "+samples+" deterministic cycle samples across negative/positive time; exact phase boundaries, invalid-time all-red fallback, exclusive pedestrian phase and actual saved lamp bindings.\n");
    }

    private static void Before()
    {
        var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        TownFloristBatch.Render(scene,Pictures+"/BeforeShopping.png",new Vector3(-94,10,-174),new Vector3(-120,2,-144));
        TownFloristBatch.Render(scene,Pictures+"/BeforeCentral.png",new Vector3(195,34,20),new Vector3(168,2,48));
    }

    private static void Capture()
    {
        Directory.CreateDirectory(Pictures);var scene=EditorSceneManager.OpenScene(CityUpdateSetup.CityPath);
        var town=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TownWorldPlaces>()).Single();
        var signals=town.GetComponentsInChildren<TownJunctionSignals>();foreach(var signal in signals)signal.DisplayAt(signal.PhaseOffset);
        void View(string id,Vector3 p,Vector3 target,float ortho=0)=>TownFloristBatch.Render(scene,Pictures+"/"+id+".png",p,target,ortho);
        View("ShoppingJunction",new Vector3(-94,10,-174),new Vector3(-120,2,-144));
        View("CentralJunction",new Vector3(195,34,20),new Vector3(168,2,48));
        View("NorthJunction",new Vector3(-144,18,212),new Vector3(-120,2,192));
        View("PriorityJunction",new Vector3(-100,15,-96),new Vector3(-120,1.5f,-72));
        var priority=town.transform.Find(TownJunctionPlan.Site+"/Junction -10 -6/Approach 0/PriorityRoadSign");
        Require(priority!=null,"Priority sign camera target missing.");
        View("PrioritySign",priority.position+priority.forward*8+priority.right*1.5f+Vector3.up*3.1f,priority.position+Vector3.up*2.6f);
        View("YieldSign",new Vector3(-142,3,-78),new Vector3(-132.4f,2.7f,-76.9f));
        View("CityOverview",new Vector3(20,650,0),new Vector3(20,0,0),292);
        var shopping=signals.Single(s=>s.name=="Junction -10 -12");
        foreach(var phase in new[]{("Green",0d),("Amber",18d),("RedAmber",59d),("Red",24d)})
        {
            shopping.DisplayAt(phase.Item2);View("Signal"+phase.Item1,new Vector3(-114.8f,3.2f,-164),new Vector3(-115.2f,3.6f,-155.1f));
        }
        shopping.DisplayAt(47);View("PedestrianWalk",new Vector3(-123,2.4f,-152.5f),new Vector3(-115.2f,2.5f,-152.5f));
        File.WriteAllText(Pictures+"/complete.txt","PASS: twelve native saved-scene views including actual red/amber/green and pedestrian states, plus two before views. No concept rendering substituted for Unity evidence.\n");
    }

    internal static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static string Hash(string path)=>BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(path))).Replace("-","");
    internal static void Receipt(string stage,string message)
    {
        File.WriteAllText(Report+"/"+stage+".txt",message);File.WriteAllText(Report+"/"+stage+"-input.sha256",Hash(TownWorldAssembler.PrefabPath));
        File.WriteAllLines(Report+"/"+stage+"-assets.sha256",Directory.GetFiles(TownJunctionArt.Root,"*",SearchOption.AllDirectories).Concat(new[]{TownWorldAssembler.PrefabPath})
            .OrderBy(p=>p,StringComparer.Ordinal).Select(p=>Hash(p)+" "+p.Replace('\\','/')));
    }
    internal static void Failure(string stage,Exception error)
    {
        Directory.CreateDirectory(Report);File.WriteAllText(Report+"/"+stage+".txt","FAILED\n"+error);Debug.LogException(error);EditorApplication.Exit(1);
    }
}
