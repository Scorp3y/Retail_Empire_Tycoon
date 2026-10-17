using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEngine;

/// <summary>Only recognised legacy attachments; buildings, routes and accepted courtyards are not reconstructed.</summary>
internal sealed class TownSeamPlan
{
    public const string AssetRoot="Assets/Art/TownSeamRepairs";
    public readonly List<TownSeamAttachment> Attachments=new List<TownSeamAttachment>();
    public readonly List<string> Audit=new List<string>();
    public readonly List<TownSeamReplacement> Replacements=new List<TownSeamReplacement>();
    public TownSeamGeometry Geometry { get; }
    public IReadOnlyList<Rect> AllowedAreas=>Replacements.Select(a=>a.scope).ToArray();
    public TownSeamPlan(GameObject town)
    {
        Geometry=new TownSeamGeometry(town);
        foreach(var body in town.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name=="Residential long block"||c.name=="Office long block"))
        {
            var node=body.transform;float w=body.size.x,d=body.size.z,h=body.size.y;
            bool office=body.name=="Office long block";
            void Try(string model,Vector3 local,Vector2 foot)
            {
                var template=TownSeamGeometry.Template(model);var pose=node.localToWorldMatrix*Matrix4x4.Translate(local);
                var matches=Geometry.Match(template,pose);if(matches==null)return;
                var item=new TownSeamAttachment(model,node,template,pose,matches,foot,h,Geometry);
                Attachments.Add(item);Audit.Add(item.Describe());
                if(item.NeedsRepair)Add(new TownSeamReplacement{kind="Roof",model=model,originalPose=pose,pose=item.RepairedPose,roofCorners=item.RoofCorners,originalTriangles=matches});
            }
            if(!office)
            {
                Try("ChimneyStack",new Vector3(-w*.3f,h+2.25f,0),new Vector2(.84f,1.04f));
                Try("RoofAerial",new Vector3(w*.3f,h+2.5f,0),new Vector2(.18f,.18f));
            }
            Try("RoofAerial",new Vector3(w*.35f,h+.11f,d*.25f),new Vector2(.18f,.18f));
            Try("VentilationBank",new Vector3(-w*.23f,h+.11f,0),new Vector2(2.84f,1.3f));
            Try("VentilationBank",new Vector3(w*.24f,h+.11f,-1),new Vector2(2.84f,1.3f));
            if(w>=26)Try("RoofGarden",new Vector3(0,h+.12f,d*.3f),new Vector2(5.5f,1.6f));
            for(float entrance=-w/2+5;entrance<w/2;entrance+=12)
            {
                var canopyPose=node.localToWorldMatrix*Matrix4x4.Translate(new Vector3(entrance,2.4f,d/2+.8f));
                var canopy=TownSeamGeometry.Template("EntranceCanopy");var matches=Geometry.Match(canopy,canopyPose);
                if(matches!=null)
                {
                    float plate=canopy.vertices.Take(36).Average(v=>v.y);
                    Add(new TownSeamReplacement{kind="Canopy",model="EntranceCanopy",originalPose=canopyPose,pose=canopyPose,plate=plate,
                        braceLeft=Mathf.Max(-1.2f,-w/2+.08f-entrance),braceRight=Mathf.Min(1.2f,w/2-.08f-entrance),originalTriangles=matches});
                }
                if(!office)
                {
                    var original=node.localToWorldMatrix*Matrix4x4.Translate(new Vector3(entrance,.04f,d/2+1.1f));
                    matches=Geometry.Match(TownSeamGeometry.Template("StairLanding"),original);
                    if(matches!=null)
                    {
                        var excluded=new HashSet<TownSeamGeometry.Triangle>(matches);
                        var p=node.TransformPoint(new Vector3(entrance,0,d/2+1.95f));float ground=Geometry.Height(p,-.01f,.3f,excluded)-node.position.y;
                        if(float.IsNaN(ground))throw new InvalidOperationException("No actual footpath at "+p);
                        Add(new TownSeamReplacement{kind="Steps",model="StairLanding",originalPose=original,pose=node.localToWorldMatrix*Matrix4x4.Translate(new Vector3(entrance,0,d/2+1.1f)),ground=ground,top=.55f,originalTriangles=matches});
                    }
                }
            }
            foreach(float x in new[]{-w/2+.3f,w/2-.3f})
            {
                var original=node.localToWorldMatrix*Matrix4x4.TRS(new Vector3(x,0,-d/2-.16f),Quaternion.Euler(0,180,0),new Vector3(1,h/6,1));
                var matches=Geometry.Match(TownSeamGeometry.Template("Downpipe"),original);if(matches==null)continue;
                var position=node.TransformPoint(new Vector3(x,0,-d/2-.16f));float ground=Geometry.Height(position,-.01f,.3f,new HashSet<TownSeamGeometry.Triangle>(matches))-node.position.y;
                if(float.IsNaN(ground))throw new InvalidOperationException("No ground under old pipe at "+position);
                float roof=Geometry.Height(position,node.position.y+h-.15f,node.position.y+h+3.5f,new HashSet<TownSeamGeometry.Triangle>(matches))-node.position.y;
                if(float.IsNaN(roof))throw new InvalidOperationException("No actual eave over old pipe at "+position);
                Add(new TownSeamReplacement{kind="Pipe",model="Downpipe",originalPose=original,pose=node.localToWorldMatrix*Matrix4x4.TRS(new Vector3(x,0,-d/2-.16f),Quaternion.Euler(0,180,0),Vector3.one),ground=ground,top=roof,originalTriangles=matches});
            }
        }
        var shelter=town.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="BusShelter");
        if(shelter!=null)
        {
            var model=TownSeamGeometry.Template("BusShelter");var matches=Geometry.Match(model,shelter.localToWorldMatrix);
            if(matches==null)throw new InvalidOperationException("The saved bus shelter does not match its native template.");
            // Shelter's first native primitive is the complete floor box. Post foot faces share its Y range.
            var baseFaces=matches.Take(12).ToList();
            Platform(baseFaces,"BusShelterFloor");
        }
        var approved=town.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="05_BusStop");
        if(approved!=null)
        {
            // Read-only reconstruction of the pad contract from Tools/ConceptTownKit/ConceptParts.Pad.
            // The previous industrial trim can remove its eastern side; match only actual surviving faces,
            // including all inlaid slab joints. Do not select surrounding road triangles by height/bounds.
            var builder=new TownMeshBuilder();builder.Box(new Vector3(0,.12f,0),new Vector3(8.5f,.24f,5),TownColor.Pavement);
            var pad=builder.Build("Approved pad recognition");List<TownSeamGeometry.Triangle> faces;
            try{faces=Geometry.Match(pad,approved.localToWorldMatrix,true);}
            finally{UnityEngine.Object.DestroyImmediate(pad);}
            // Native saved audit found eight survivors: ground/previous trim removed four box faces.
            // Still require exact XYZ + atlas UV + winding matches, not a permissive rectangular selection.
            if(faces.Count!=8&&faces.Count!=10&&faces.Count!=12)throw new InvalidOperationException("Approved pad native box recognition failed: "+faces.Count+"/12 actual faces.");
            int bodyFaces=faces.Count;
            builder=new TownMeshBuilder();
            for(float x=-8.5f/2+2;x<8.5f/2;x+=2)builder.Box(new Vector3(x,.243f,0),new Vector3(.015f,.006f,5),TownColor.Stone);
            for(float z=-5f/2+2;z<5f/2;z+=2)builder.Box(new Vector3(0,.243f,z),new Vector3(8.5f,.006f,.015f),TownColor.Stone);
            pad=builder.Build("Approved pad joint recognition");
            try{faces.AddRange(Geometry.Match(pad,approved.localToWorldMatrix,true));}
            finally{UnityEngine.Object.DestroyImmediate(pad);}
            Audit.Add("Approved stop actual pad faces="+bodyFaces+"/12; surviving native slab/joint faces="+faces.Count+".");
            Platform(faces,"ApprovedStopFloor",false);
        }
        Audit.Add("Recognised "+Attachments.Count+" complete legacy attachments; repairs required "+Attachments.Count(a=>a.NeedsRepair)+".");
        Audit.Add("Native replacement contracts: "+Replacements.Count+"; "+string.Join(", ",Replacements.GroupBy(r=>r.kind).Select(g=>g.Key+"="+g.Count())));
    }
    private void Add(TownSeamReplacement replacement)
    {
        replacement.id="Repair"+Replacements.Count+"."+replacement.model;replacement.MeasureScope();Replacements.Add(replacement);
        Audit.Add(replacement.id+": "+replacement.kind+"; ground="+replacement.ground.ToString("F3")+"; top="+replacement.top.ToString("F3")+"; scope="+replacement.scope);
    }
    private void Platform(List<TownSeamGeometry.Triangle> triangles,string label,bool completeBox=true)
    {
        if(completeBox&&triangles.Count!=12)throw new InvalidOperationException(label+": old pad is not a complete native box.");
        var p=triangles.SelectMany(t=>new[]{t.A,t.B,t.C}).ToArray();
        var bounds=Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.z),p.Max(v=>v.x),-150);
        if(bounds.height<=0||bounds.height>8)throw new InvalidOperationException(label+": unexplained connection to sidewalk.");
        Add(new TownSeamReplacement{kind="Platform",model=label,originalPose=Matrix4x4.identity,pose=Matrix4x4.identity,area=bounds,ground=.14f,originalTriangles=triangles});
    }
}
internal sealed class TownSeamAttachment
{
    public readonly string Model;
    public readonly Mesh Template;
    public readonly Matrix4x4 OriginalPose,RepairedPose;
    public readonly List<TownSeamGeometry.Triangle> Triangles;
    public readonly Vector3[] RoofCorners;
    public readonly float Gap,RoofRange;
    public readonly bool NeedsRepair;
    public readonly Rect Scope;
    public readonly Vector3 OriginalPosition,RepairedPosition;
    public readonly Vector2 Foot;
    public TownSeamAttachment(string model,Transform building,Mesh template,Matrix4x4 pose,List<TownSeamGeometry.Triangle> triangles,Vector2 foot,float bodyHeight,TownSeamGeometry geometry)
    {
        Model=model;Template=template;OriginalPose=pose;Triangles=triangles;Foot=foot;
        OriginalPosition=pose.MultiplyPoint3x4(Vector3.zero);var excluded=new HashSet<TownSeamGeometry.Triangle>(triangles);
        var local=building.InverseTransformPoint(OriginalPosition);
        Vector3[] Sample(Vector3 centre)
        {
            return new[]{new Vector2(-1,1),new Vector2(1,1),new Vector2(1,-1),new Vector2(-1,-1)}.Select(c=>
            {
                var p=building.TransformPoint(centre+new Vector3(c.x*foot.x/2,0,c.y*foot.y/2));
                p.y=geometry.Height(p,building.position.y+bodyHeight-.15f,building.position.y+bodyHeight+3.5f,excluded);return p;
            }).ToArray();
        }
        var corners=Sample(local);if(corners.Any(p=>float.IsNaN(p.y)))throw new InvalidOperationException(model+": no actual roof under "+OriginalPosition);
        Gap=OriginalPosition.y-corners.Max(c=>c.y);RoofRange=corners.Max(c=>c.y)-corners.Min(c=>c.y);
        NeedsRepair=Gap>.03f||Gap<-.03f||RoofRange>.35f;
        if(NeedsRepair&&RoofRange>.35f)
        {
            // Keep equipment off the edge of an elevator/attic volume rather than build a two-metre pedestal.
            local.x=Mathf.Sign(local.x==0?1:local.x)*(building.GetComponent<BoxCollider>().size.x*.225f+foot.x/2+.3f);
            corners=Sample(local);
            if(corners.Any(p=>float.IsNaN(p.y))||corners.Max(c=>c.y)-corners.Min(c=>c.y)>.35f)
                throw new InvalidOperationException(model+": no coherent free roof seat at "+OriginalPosition);
        }
        RoofCorners=corners;
        var next=building.TransformPoint(local);next.y=NeedsRepair?corners.Max(c=>c.y)+.035f:OriginalPosition.y;
        RepairedPosition=next;var delta=next-OriginalPosition;RepairedPose=Matrix4x4.Translate(delta)*pose;
        var points=template.vertices.Select(pose.MultiplyPoint3x4).Concat(template.vertices.Select(RepairedPose.MultiplyPoint3x4)).Concat(corners).ToArray();
        Scope=Rect.MinMaxRect(points.Min(p=>p.x)-.03f,points.Min(p=>p.z)-.03f,points.Max(p=>p.x)+.03f,points.Max(p=>p.z)+.03f);
    }
    public string Describe()=>Model+" at "+OriginalPosition.ToString("F3")+"; actual roof gap="+Gap.ToString("F3")+"m; roof variation="+RoofRange.ToString("F3")+"m; "+(NeedsRepair?"REPAIR -> "+RepairedPosition.ToString("F3"):"OK");
}
