using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using V=TownCityCorrectionsGeometry.Vertex;

/// <summary>Copy-on-write corrections to the accepted town. No whole-city generator is run.</summary>
internal sealed class TownSiteRevisionPlacement
{
    internal const string Root="Assets/Art/TownSiteRevision",Site="Site cleanup and access revision";
    internal static readonly Rect ClearedFreight=new(-268,174,48,48);
    internal static readonly TownCityCorrectionsPlacement.Apron ShopEntry=new("Store straight entrance",new(-58,0,-140.1f),new(-58,.02f,-123.9f),8);
    internal readonly HashSet<Object> Mutable=new();
    private readonly GameObject town;
    private readonly List<string> audit=new();
    private readonly List<Rect> physicalCuts=new();
    private readonly Dictionary<Material,Material> materials=new();
    private TownCityCorrectionsGeometry geometry;
    private Transform site;
    private Material road;
    internal TownSiteRevisionPlacement(GameObject town){this.town=town;}
    internal void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(Site)!=null)throw new InvalidOperationException("Use the recorded original town when revising.");
        foreach(string f in new[]{"Meshes","Materials"})Directory.CreateDirectory(Root+"/"+f);AssetDatabase.Refresh();
        site=TownBakeryPlacement.Child(town.transform,Site,Vector3.zero);
        road=AssetDatabase.LoadAssetAtPath<Material>(TownCityCorrectionsPlacement.Root+"/Materials/Ground0.mat");
        if(road==null)throw new InvalidOperationException("Accepted city road material is missing.");
        geometry=new TownCityCorrectionsGeometry(town.transform);
        FarmParking();FarmApproachBlend();RemoveOrphanSigns();RemoveOldShopMarks();ClearEntranceFurniture(); // Exact recipes precede clipping.
        ClearFreight();ConstructionPad();CloseOrphanOpenings();ShopAccess();
        audit.Add("COPLANAR: "+TownGroundOverlapRepair.Repair(geometry)+" overlapping horizontal ground faces subtracted; no height-offset camouflage.");
        BakeryColour();GroundShadows();PhysicalFloors();Signals();
        foreach(var v in geometry.Views)if(v.Removed.Count>0||v.Added.Count>0)Mutable.Add(v.Filter);
        geometry.Save(Root);AssetDatabase.SaveAssets();File.WriteAllLines(TownSiteRevisionBatch.Report+"/placement.txt",audit);
    }
    private void FarmParking()
    {
        var recipe=new TownMeshBuilder();
        TownRuralPolishPlacement.Ribbon(recipe,new[]{new Vector3(546,.15f,270),new Vector3(513,.15f,270),new Vector3(513,.15f,280)},6,TownColor.Asphalt);
        TownRuralPolishPlacement.RoundedGround(recipe,new Rect(504.5f,277,17,11),1,.15f,TownColor.Asphalt);
        var mesh=recipe.Build("Removed farm visitor spur");
        try
        {
            geometry.MatchAndRemove(mesh,Matrix4x4.identity,town.transform);
            var collider=town.transform.Find(TownRuralPolishPlacement.Farm).GetComponentsInChildren<MeshCollider>(true).Single(c=>c.name=="Farm driveable base");
            EditCollider(collider,g=>g.MatchAndRemove(mesh,Matrix4x4.identity,g.Views[0].Filter.transform));
        }
        finally{Object.DestroyImmediate(mesh);}
        var paint=new TownMeshBuilder();for(int i=0;i<4;i++)paint.Ground(505.5f+i*4.8f,282,.12f,6,.158f,TownColor.White);
        mesh=paint.Build("Removed visitor stripes");try{geometry.MatchAndRemove(mesh,Matrix4x4.identity,town.transform);}finally{Object.DestroyImmediate(mesh);}
        var farm=town.transform.Find(TownRuralPolishPlacement.Farm);
        foreach(var stop in farm.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==farm&&t.name=="WheelStop").ToArray())
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TownRuralArtCatalogue.Root+"/Prefabs/WheelStop.prefab");
            foreach(var f in prefab.GetComponentsInChildren<MeshFilter>())geometry.MatchAndRemove(f.sharedMesh,stop.localToWorldMatrix*f.transform.localToWorldMatrix,town.transform);
            RemoveNode(stop);
        }
        audit.Add("FARM: exact visitor parking, four stripes, three wheel stops and lateral spur removed from visuals AND collision. Main curved road and pickup yard untouched.");
    }
    private void ClearFreight()
    {
        int triangles=geometry.Remove((v,i)=>
        {
            var p=TownCityCorrectionsGeometry.Points(v,i);
            return p.All(a=>Inside(ClearedFreight,a))&&p.Max(a=>a.y)>.005f;
        });
        geometry.ClipGround(ClearedFreight,3);
        int colliders=0;
        foreach(var c in town.GetComponentsInChildren<Collider>(true).Where(c=>!(c is MeshCollider)&&!c.isTrigger).ToArray())
        {
            var b=TownResidentialPlacement.ColliderBounds(c);
            if(!Inside(ClearedFreight,b.center)||b.max.y<=.005f)continue;
            Mutable.Add(c);Mutable.Add(c.gameObject);Object.DestroyImmediate(c);colliders++;
        }
        physicalCuts.Add(ClearedFreight);
        audit.Add($"FREIGHT CLEARANCE: {triangles} object triangles and {colliders} physical volumes removed from the abandoned northwest hardstand. Railway, station and active suppliers remain outside the cut.");
    }
    private void FarmApproachBlend()
    {
        // The removed spur formerly hid a 3 cm leading edge on the surviving farm ribbon.
        // Retain the original curved centreline and remove only raised faces under this ramp.
        var points=town.GetComponent<TownWorldPlaces>().layout.countryRoad.points;int first=Array.FindIndex(points,p=>p.z>=258);
        if(first<1)throw new InvalidOperationException("Missing curved farm approach samples.");
        var draft=new TownMeshBuilder();
        for(int i=first+1;i<points.Length;i++)
        {
            var a=points[i-1];var b=points[i];var sideA=Vector3.Cross(Vector3.up,b-points[i-2]).normalized;
            var sideB=i+1<points.Length?Vector3.Cross(Vector3.up,points[i+1]-a).normalized:Vector3.Cross(Vector3.up,b-a).normalized;
            float Height(Vector3 p)=>Mathf.Lerp(.12f,.15f,Mathf.InverseLerp(points[first].z,points[points.Length-1].z,p.z));
            a.y=Height(a);b.y=Height(b);draft.Quad(b-sideB*4.1f,b+sideB*4.1f,a+sideA*4.1f,a-sideA*4.1f,TownColor.Asphalt);
        }
        var ramp=draft.Build("Curved farm approach blend");
        try
        {
            SubtractRaised(geometry,ramp);
            var collider=town.transform.Find(TownRuralPolishPlacement.Farm).GetComponentsInChildren<MeshCollider>(true).Single(c=>c.name=="Farm driveable base");
            EditCollider(collider,g=>SubtractRaised(g,ramp));
            Part("Curved farm approach blend",Object.Instantiate(ramp),road,true);
        }
        finally{Object.DestroyImmediate(ramp);}
        audit.Add("FARM ACCESS: original country-road centreline retained; a 12-to-15 cm curved top-only ramp replaces the exposed leading lip. Main pickup court remains at 15 cm; removed visitor spur stays absent.");
    }
    private static void SubtractRaised(TownCityCorrectionsGeometry source,Mesh footprint)
    {
        var p=footprint.vertices;var t=footprint.triangles;var cutters=new List<V[]>();
        for(int i=0;i<t.Length;i+=3)cutters.Add(Enumerable.Range(0,3).Select(k=>new V(p[t[i+k]],Vector3.up,Vector2.zero,Vector2.zero)).ToArray());
        var area=footprint.bounds;area.Expand(new Vector3(.01f,1,.01f));
        foreach(var v in source.Views.Where(v=>v.Bounds.Intersects(area)))
        {
            List<List<V>> Cut(V[] triangle,out bool changed)
            {
                changed=false;var pieces=new List<List<V>>{triangle.ToList()};
                if(triangle.Min(a=>a.P.y)<.139f||triangle.Max(a=>a.P.y)>.18f||triangle.Max(a=>a.P.y)-triangle.Min(a=>a.P.y)>.0001f)return pieces;
                foreach(var cutter in cutters)
                {
                    var next=new List<List<V>>();foreach(var piece in pieces){next.AddRange(TownGroundOverlapRepair.Subtract(piece,cutter,out bool clipped));changed|=clipped;}pieces=next;if(pieces.Count==0)break;
                }
                return pieces;
            }
            var fragments=v.Added.ToArray();v.Added.Clear();
            for(int i=0;i<v.Indices.Length;i+=3)
            {
                if(v.Removed.Contains(i))continue;var triangle=Enumerable.Range(0,3).Select(k=>{var a=v.Vertices[v.Indices[i+k]];return new V(v.World[v.Indices[i+k]],v.Filter.transform.TransformDirection(a.N),a.U,a.V);}).ToArray();
                var pieces=Cut(triangle,out bool changed);if(!changed)continue;v.Removed.Add(i);foreach(var piece in pieces)for(int j=1;j<piece.Count-1;j++)v.AddWorld(piece[0],piece[j],piece[j+1]);
            }
            for(int i=0;i<fragments.Length;i+=3)
            {
                var triangle=fragments.Skip(i).Take(3).Select(a=>new V(v.Filter.transform.TransformPoint(a.P),v.Filter.transform.TransformDirection(a.N),a.U,a.V)).ToArray();
                var pieces=Cut(triangle,out bool changed);if(!changed)v.Added.AddRange(fragments.Skip(i).Take(3));else foreach(var piece in pieces)for(int j=1;j<piece.Count-1;j++)v.AddWorld(piece[0],piece[j],piece[j+1]);
            }
        }
    }
    private void ConstructionPad()
    {
        var area=new Rect(133.8f,-64.2f,24.4f,9.4f);
        // This is an unloading guide, not part of the building frame. Remove slab, guide lines
        // and its little entry ramp, then give the remaining exposed yard the existing soil level.
        Cut(area);Cut(new Rect(137.3f,-56.2f,5.4f,2.4f));
        var soil=new TownMeshBuilder();soil.Ground(146,-59.5f,24.4f,9.4f,.08f,TownColor.Soil);
        soil.Ground(140,-55,5.4f,2.4f,.08f,TownColor.Soil);Part("Construction plain earth",soil,road,true);
        audit.Add("CONSTRUCTION: unloading concrete, yellow hatch/corridor and pad ramp removed; visual and physical yard now use the same compacted earth. Buildings, crane and gate retained.");
    }
    private void CloseOrphanOpenings()
    {
        var layout=town.GetComponent<TownWorldPlaces>().layout;int count=0;
        foreach(var j in TownJunctionPlan.Create(layout))foreach(int arm in j.Approaches.Where(j.IsPrivate))
        {
            if(j.Cell.y==-12&&(j.Cell.x==17||j.Cell.x==19))continue; // The two real fuel entries.
            var direction=TownJunctionPlan.Outward(arm);var across=TownJunctionPlan.ArrivalRight(arm);
            var center=j.Center+direction*5;
            var strip=Mathf.Abs(direction.x)>.5f?new Rect(center.x-1,center.z-4,2,8):new Rect(center.x-4,center.z-1,8,2);
            if(TownCityCorrectionsPlacement.Aprons.Any(a=>a.Cut.Overlaps(strip)))continue;
            Cut(strip);
            var paving=new TownMeshBuilder();paving.Ground(strip.center.x,strip.center.y,strip.width,strip.height,.14f,TownColor.Pavement);
            var lineCenter=j.Center+direction*3.96f;
            paving.Ground(lineCenter.x,lineCenter.z,Mathf.Abs(direction.x)>.5f?.065f:8,Mathf.Abs(direction.x)>.5f?8:.065f,.035f,TownColor.White);
            Part("Closed orphan mouth "+count++,paving,road,true);
            // The private yield teeth can sit just beyond the old kerb, but never in a public arm.
            var teethCenter=j.Center+direction*6.05f+across*2;
            var teeth=Mathf.Abs(direction.x)>.5f?new Rect(teethCenter.x-.35f,teethCenter.z-2, .7f,4):new Rect(teethCenter.x-2,teethCenter.z-.35f,4,.7f);
            geometry.ClipGround(teeth,.2f,(v,i)=>v.Filter.transform.IsChildOf(town.transform.Find(TownJunctionPlan.Site)));
        }
        audit.Add("STREETS: "+count+" unused private arms closed with continuous pavement and street edge. No public graph connection removed; all established supplier aprons and fuel entries retained.");
    }
    private void RemoveOrphanSigns()
    {
        foreach(var j in TownJunctionPlan.Create(town.GetComponent<TownWorldPlaces>().layout))foreach(int arm in j.Approaches.Where(j.IsPrivate))
        {
            if(j.Cell.y==-12&&(j.Cell.x==17||j.Cell.x==19))continue;
            var d=TownJunctionPlan.Outward(arm);var p=j.Center+d*5;
            var r=Mathf.Abs(d.x)>.5f?new Rect(p.x-1,p.z-4,2,8):new Rect(p.x-4,p.z-1,8,2);
            if(TownCityCorrectionsPlacement.Aprons.Any(a=>a.Cut.Overlaps(r)))continue;
            var marker=town.transform.Find(TownJunctionPlan.Site+"/Junction "+j.Cell.x+" "+j.Cell.y+"/Approach "+arm);if(marker==null)continue;
            foreach(var child in marker.Cast<Transform>().ToArray())
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TownJunctionArt.Root+"/Prefabs/"+child.name+".prefab");
                if(prefab!=null)foreach(var f in prefab.GetComponentsInChildren<MeshFilter>())
                {
                    // The accepted driveway pass already clipped some pole-foot triangles.
                    // Its narrow, exact template envelope also identifies those retained fragments.
                    var matrix=child.localToWorldMatrix*f.transform.localToWorldMatrix;
                    var points=f.sharedMesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();var envelope=new Bounds(points[0],Vector3.zero);foreach(var point in points)envelope.Encapsulate(point);envelope.Expand(.003f);
                    geometry.Remove((v,i)=>v.Filter.transform.IsChildOf(town.transform.Find(TownJunctionPlan.Site))&&TownCityCorrectionsGeometry.Points(v,i).All(envelope.Contains));
                }
            }
            RemoveNode(marker);
        }
    }
    private void ShopAccess()
    {
        // Delete only the obsolete outside-land apron pieces, never the initial or purchasable plots.
        Cut(new Rect(-70.01f,-124.01f,8.01f,40.02f));
        Cut(new Rect(-32.01f,-140,12.02f,24.01f));
        // Old right driveway met the street at (-24,-140). Its public sidewalk must close too.
        // CloseOrphanOpenings has already restored the old mouth's sidewalk and edge line.
        Cut(ShopEntry.Cut);
        Part("Store straight lowered entrance",TownCityCorrectionsPlacement.BuildApron(ShopEntry,true),road,true);
        audit.Add("STORE: removed outside-land left strip and former right turn; straight 8 m entrance at x=-58 from street to left forecourt, with a physically lowered crossing.");
    }
    private void ClearEntranceFurniture()
    {
        var perimeter=town.transform.Find(TownShopPerimeterPlacement.SiteName);var corridor=new Rect(-63,-140,10,16.2f);int count=0;
        var names=new[]{"UrbanTree","TreePlanter","TimberBench","StreetBin"};
        foreach(var t in perimeter.GetComponentsInChildren<Transform>(true).Where(t=>t.parent!=null&&t.parent.parent==perimeter&&names.Contains(t.name)&&Inside(corridor,t.position)).ToArray())
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TownShopPerimeterPlacement.Root+"/Prefabs/"+t.name+".prefab")??AssetDatabase.LoadAssetAtPath<GameObject>(TownForestArt.Root+"/Prefabs/"+t.name+".prefab");
            if(prefab==null)throw new InvalidOperationException("No entrance furnishing template "+t.name);
            foreach(var f in prefab.GetComponentsInChildren<MeshFilter>())geometry.MatchAndRemove(f.sharedMesh,t.localToWorldMatrix*f.transform.localToWorldMatrix,town.transform);
            RemoveNode(t);count++;
        }
        audit.Add("STORE CLEARANCE: "+count+" complete tree/planter/bench assemblies removed from the blue entrance footprint, including their combined canopy meshes and all collision.");
    }
    private void RemoveOldShopMarks()
    {
        var draft=new TownMeshBuilder();
        for(float stripe=-27.5f;stripe<-20;stripe+=1.35f)draft.Box(new Vector3(stripe,.075f,-137),new Vector3(.65f,.018f,3.7f),TownColor.White);
        foreach(float x in new[]{-28f,-20f})draft.Box(new Vector3(x,.05f,-132.5f),new Vector3(.1f,.018f,17),TownColor.White);
        var mesh=draft.Build("Obsolete shop crossing and edges");try{geometry.MatchAndRemove(mesh,Matrix4x4.identity,town.transform);}finally{Object.DestroyImmediate(mesh);}
    }
    private void BakeryColour()
    {
        var bakery=town.transform.Find(TownBakeryPlacement.SiteName);var draft=new TownMeshBuilder();int count=0;
        foreach(var v in geometry.Views.Where(v=>v.Filter.transform.IsChildOf(bakery)))
        {
            bool Asphalt(V[] t)=>t.All(p=>Mathf.FloorToInt(p.U.x*32)==29)&&t.Max(p=>p.P.y)<.25f;
            for(int i=0;i<v.Indices.Length;i+=3)
            {
                if(v.Removed.Contains(i))continue;var t=Enumerable.Range(0,3).Select(k=>{var a=v.Vertices[v.Indices[i+k]];return new V(v.World[v.Indices[i+k]],a.N,a.U,a.V);}).ToArray();
                if(!Asphalt(t))continue;v.Removed.Add(i);draft.Triangle(t[0].P,t[1].P,t[2].P,TownColor.Asphalt);count++;
            }
            var added=v.Added.ToArray();v.Added.Clear();
            for(int i=0;i<added.Length;i+=3)
            {
                var t=added.Skip(i).Take(3).Select(a=>new V(v.Filter.transform.TransformPoint(a.P),a.N,a.U,a.V)).ToArray();
                if(Asphalt(t)){draft.Triangle(t[0].P,t[1].P,t[2].P,TownColor.Asphalt);count++;}
                else v.Added.AddRange(added.Skip(i).Take(3));
            }
        }
        if(count==0)throw new InvalidOperationException("Bakery custom asphalt was not identified.");
        Part("Bakery common city asphalt",draft,road);
        geometry.ClipGround(new Rect(-90.1f,-148.4f,6.2f,1),.12f,(v,i)=>TownCityCorrectionsGeometry.Color(v,i,TownColor.White));
        audit.Add("BAKERY: "+count+" custom-atlas asphalt triangles normalized to exact city-road palette; street edge paint removed only across its active exit.");
    }
    private void GroundShadows()
    {
        var drafts=new Dictionary<Material,List<V>>();
        foreach(var v in geometry.Views.Where(v=>v.HasPalette))
        {
            var material=v.Filter.GetComponent<MeshRenderer>().sharedMaterial;
            bool Ground(V[] t)=>t.All(p=>p.P.y>=-.061f&&p.P.y<=.18f)&&t.Max(p=>p.P.y)-t.Min(p=>p.P.y)<.0001f;
            void Add(V[] t){if(!drafts.TryGetValue(material,out var list))drafts.Add(material,list=new());list.AddRange(t);}
            for(int i=0;i<v.Indices.Length;i+=3)
            {
                if(v.Removed.Contains(i))continue;var t=Enumerable.Range(0,3).Select(k=>{var a=v.Vertices[v.Indices[i+k]];return new V(v.World[v.Indices[i+k]],v.Filter.transform.TransformDirection(a.N),a.U,a.V);}).ToArray();
                if(!Ground(t))continue;v.Removed.Add(i);Add(t);
            }
            var old=v.Added.ToArray();v.Added.Clear();
            for(int i=0;i<old.Length;i+=3)
            {
                var t=old.Skip(i).Take(3).Select(a=>new V(v.Filter.transform.TransformPoint(a.P),v.Filter.transform.TransformDirection(a.N),a.U,a.V)).ToArray();
                if(Ground(t))Add(t);else v.Added.AddRange(old.Skip(i).Take(3));
            }
        }
        int n=0;
        foreach(var pair in drafts)
        {
            var source=pair.Key;
            if(!materials.TryGetValue(source,out var material))
            {
                material=new Material(source){name=source.name+" stable ground",shader=road.shader};
                string path=Root+"/Materials/Ground"+n+".mat";TownBakeryPlacement.SaveAsset(material,path);material=AssetDatabase.LoadAssetAtPath<Material>(path);materials.Add(source,material);
            }
            var vertices=pair.Value;var mesh=new Mesh{name="Stable flat ground "+n,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.vertices=vertices.Select(v=>v.P).ToArray();mesh.normals=vertices.Select(v=>v.N).ToArray();mesh.uv=vertices.Select(v=>v.U).ToArray();mesh.uv2=vertices.Select(v=>new Vector2(v.P.x,v.P.z)).ToArray();mesh.triangles=Enumerable.Range(0,vertices.Count).ToArray();mesh.RecalculateBounds();
            Part("Stable flat ground "+n++,mesh,material,false);
        }
        audit.Add("GROUND: existing flat ground separated into "+n+" private palette-preserving batches; per-pixel cascade shader and no self-shadow casting. Buildings and glazing retain their materials.");
    }
    private void Signals()
    {
        var layout=town.GetComponent<TownWorldPlaces>().layout;
        var signals=town.GetComponentsInChildren<TownJunctionSignals>(true);
        var controller=town.AddComponent<TownSignalChunks>();Mutable.Add(town);
        controller.Configure(signals.Select(s=>{var p=s.name.Split(' ');return new TownSignalChunks.Junction(s,new Vector3(int.Parse(p[1])*layout.roadTileSize,0,int.Parse(p[2])*layout.roadTileSize));}));
        audit.Add("SIGNALS: "+signals.Length+" existing junctions registered by real native centre; one 5 Hz coordinator evaluates only 3x3 player-neighbouring 96 m chunks. Both vehicle axes have 20 s green plus safe amber/all-red/pedestrian phases.");
    }
    private void Cut(Rect cut){geometry.ClipGround(cut,.25f);physicalCuts.Add(cut);}
    private void PhysicalFloors()
    {
        foreach(var c in town.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.sharedMesh!=null&&!c.transform.IsChildOf(site)).ToArray())
            EditCollider(c,g=>{foreach(var cut in physicalCuts)g.ClipGround(cut,3);});
        int count=0;
        foreach(var c in town.GetComponentsInChildren<BoxCollider>(true).Where(c=>!c.transform.IsChildOf(site)).ToArray())
        {
            var b=TownResidentialPlacement.ColliderBounds(c);if(b.max.y>.3f||b.max.y<.005f)continue;
            var rect=Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);if(!physicalCuts.Any(rect.Overlaps))continue;
            var pieces=new List<Rect>{rect};foreach(var cut in physicalCuts)pieces=pieces.SelectMany(r=>Subtract(r,cut)).ToList();
            Mutable.Add(c);Mutable.Add(c.gameObject);Object.DestroyImmediate(c);
            var draft=new TownMeshBuilder();foreach(var r in pieces)draft.Ground(r.center.x,r.center.y,r.width,r.height,b.max.y,TownColor.Pavement);
            if(pieces.Count>0)Part("Retained floor "+count++,draft,null,true);
        }
        audit.Add("PHYSICS: clipped old raised floors into owned mesh copies; "+count+" remaining low box slabs retained as top-only support. No hidden old kerbs across the new entrance.");
    }
    private int physicalIndex;
    private void EditCollider(MeshCollider c,Action<TownCityCorrectionsGeometry> edit)
    {
        if(c.sharedMesh==null)return;var temporary=new GameObject("Floor edit",typeof(MeshFilter));
        try
        {
            temporary.transform.SetPositionAndRotation(c.transform.position,c.transform.rotation);temporary.transform.localScale=c.transform.lossyScale;
            var f=temporary.GetComponent<MeshFilter>();f.sharedMesh=c.sharedMesh;var g=new TownCityCorrectionsGeometry(temporary.transform);edit(g);
            var view=g.Views[0];if(view.Removed.Count==0)return;view.Save("PhysicalFloor"+physicalIndex++,Root);Mutable.Add(c);var mesh=f.sharedMesh;
            bool enabled=c.enabled;c.enabled=false;c.sharedMesh=null;
            if(mesh.triangles.Length>0){c.sharedMesh=mesh;c.enabled=enabled;}
        }
        finally{Object.DestroyImmediate(temporary);}
    }
    private void RemoveNode(Transform t)
    {
        Mutable.Add(t.parent);foreach(var child in t.GetComponentsInChildren<Transform>(true)){Mutable.Add(child.gameObject);foreach(var c in child.GetComponents<Component>())Mutable.Add(c);}Object.DestroyImmediate(t.gameObject);
    }
    private void Part(string id,TownMeshBuilder draft,Material material,bool physical=false)=>Part(id,draft.Build(id),material,physical);
    private void Part(string id,Mesh mesh,Material material,bool physical)
    {
        if(mesh.vertexCount==0){Object.DestroyImmediate(mesh);return;}
        if(mesh.uv2.Length!=mesh.vertexCount)mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();
        string path=Root+"/Meshes/"+id.Replace(' ','_')+".asset";TownBakeryPlacement.SaveAsset(mesh,path);mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var node=TownBakeryPlacement.Child(site,id,Vector3.zero);node.gameObject.isStatic=true;
        if(material!=null){node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
        if(physical)node.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
    private static bool Inside(Rect r,Vector3 p)=>p.x>=r.xMin-.001f&&p.x<=r.xMax+.001f&&p.z>=r.yMin-.001f&&p.z<=r.yMax+.001f;
    private static IEnumerable<Rect> Subtract(Rect s,Rect c)
    {
        if(!s.Overlaps(c)){yield return s;yield break;}float x0=Mathf.Max(s.xMin,c.xMin),x1=Mathf.Min(s.xMax,c.xMax),z0=Mathf.Max(s.yMin,c.yMin),z1=Mathf.Min(s.yMax,c.yMax);
        if(x0>s.xMin+.001f)yield return Rect.MinMaxRect(s.xMin,s.yMin,x0,s.yMax);if(x1<s.xMax-.001f)yield return Rect.MinMaxRect(x1,s.yMin,s.xMax,s.yMax);
        if(z0>s.yMin+.001f)yield return Rect.MinMaxRect(x0,s.yMin,x1,z0);if(z1<s.yMax-.001f)yield return Rect.MinMaxRect(x0,z1,x1,s.yMax);
    }
}
