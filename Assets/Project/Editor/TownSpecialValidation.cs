using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Saved native industrial geometry, actual coverage, working access and live pickup acceptance.</summary>
internal static class TownSpecialValidation
{
    private const string Report="Library/TownSpecialQA";
    private const float Epsilon=.0001f;
    public static void ValidateCatalogue(IReadOnlyList<TownSpecialModel> models)
    {
        Require(models.Count>0&&models.Select(m=>m.Id).Distinct().Count()==models.Count,"Industrial catalogue IDs must be unique.");
        foreach(var model in models)
        {
            var mesh=model.BuildMesh();
            try
            {
                Require(mesh.vertexCount>0&&mesh.uv.Length==mesh.vertexCount&&mesh.normals.Length==mesh.vertexCount,model.Id+": incomplete native model.");
                Require(Mathf.Abs(mesh.bounds.min.y)<Epsilon&&model.Solids.Count>0,model.Id+": ungrounded visual/physical model.");
                foreach(var vertex in mesh.vertices)Require(Finite(vertex),model.Id+": non-finite geometry.");
                foreach(var uv in mesh.uv)Require(Finite(uv.x)&&Finite(uv.y)&&uv.x>0&&uv.x<1&&Mathf.Abs(uv.y-.5f)<Epsilon,model.Id+": wrong shared atlas.");
                foreach(var part in model.Solids)
                    Require(part.Size.x>0&&part.Size.y>0&&part.Size.z>0&&Contains(mesh.bounds,part.Bounds,.002f),model.Id+": physical part leaves its real visual bounds: "+part.Id);
            }
            finally{Object.DestroyImmediate(mesh);}
        }
        GeometryContracts();
    }

    public static void ValidateSaved(TownSpecialPlan plan)
    {
        TownWorldSafety.RequireIsolatedEditor();ValidateCatalogue(plan.Models);Directory.CreateDirectory(Report);
        var log=new StringBuilder();bool passed=false;var town=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat");
            var obstacles=town.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&!c.isTrigger).Select(Solid.From).ToArray();
            var floor=ReadFloor(town.transform,material);
            foreach(var site in plan.Sites)
            {
                var root=town.transform.Find(site.RootPath);Require(root!=null,site.Id+": saved complex is missing.");
                Require(root.GetComponentsInChildren<MonoBehaviour>(true).Length==0,site.Id+": scenery introduced gameplay/pickup behaviour.");
                var geometry=root.Find("Site geometry");
                Require(geometry!=null&&geometry.GetComponents<MeshFilter>().Length==1&&root.GetComponentsInChildren<Renderer>(true).Length==1,site.Id+": exactly one combined visual mesh required.");
                Require(geometry.GetComponent<MeshRenderer>().sharedMaterial==material,site.Id+": existing shared material was lost.");
                CheckGeometry(site,geometry,plan.AllowedAreas);
                var declared=new HashSet<Collider>();var coverage=new List<Rect>();var supports=new List<Collider>();
                foreach(var prop in site.Props)
                {
                    var marker=root.Find("Props/"+prop.Id);Require(marker!=null,prop.Id+": missing instance metadata.");
                    Require((marker.localPosition-prop.Position).sqrMagnitude<1e-8f&&Quaternion.Angle(marker.localRotation,Quaternion.Euler(0,prop.Yaw,0))<.002f
                        &&(marker.localScale-prop.Scale).sqrMagnitude<1e-8f,prop.Id+": saved model pose changed.");
                    Require(prop.Model.Id=="SiteFence"||prop.Scale==Vector3.one,prop.Id+": arbitrary native building scale is forbidden.");
                    Require(marker.Find(prop.Model.Id)!=null&&marker.GetComponentsInChildren<Collider>().Length==prop.Model.Solids.Count,prop.Id+": physical piece count differs.");
                    foreach(var part in prop.Model.Solids)
                    {
                        var saved=marker.Find("Part_"+part.Id);var box=saved!=null?saved.GetComponent<BoxCollider>():null;
                        Require(box!=null&&box.enabled&&!box.isTrigger&&box.center==Vector3.zero&&(box.size-part.Size).sqrMagnitude<1e-8f
                            &&(saved.localPosition-part.Centre).sqrMagnitude<1e-8f&&saved.localRotation==Quaternion.identity&&saved.localScale==Vector3.one,
                            prop.Id+": saved physical part differs: "+part.Id);
                        declared.Add(box);
                        if(part.Id==prop.Model.CoveragePart)coverage.Add(TownCourtyardGeometry.Rect(TownResidentialPlacement.ColliderBounds(box)));
                    }
                    Require(Mathf.Abs((prop.Position.y)-FloorHeight(floor,prop.Position))<.011f,prop.Id+": visible support is missing or floats.");
                }
                foreach(var surface in site.Surfaces)
                {
                    var marker=root.Find("Surfaces/"+surface.Id);var pieces=TownSpecialPlacement.SurfacePieces(surface.Bounds,site.Ramps);
                    Require(marker!=null&&marker.localPosition==Vector3.zero&&marker.childCount==pieces.Count,surface.Id+": actual driveway cutout differs.");
                    for(int i=0;i<pieces.Count;i++)
                    {
                        var area=pieces[i];var saved=marker.Find("Piece"+i);var box=saved!=null?saved.GetComponent<BoxCollider>():null;
                        Require(box!=null&&box.enabled&&!box.isTrigger&&box.center==Vector3.zero
                            &&(box.size-new Vector3(area.width,surface.Y,area.height)).sqrMagnitude<1e-8f
                            &&(saved.localPosition-new Vector3(area.center.x,surface.Y/2,area.center.y)).sqrMagnitude<1e-8f
                            &&saved.localRotation==Quaternion.identity&&saved.localScale==Vector3.one,
                            site.Id+": thin actual ground support differs: "+surface.Id);
                        declared.Add(box);supports.Add(box);
                    }
                    SampleSurface(floor,surface,site.Ramps);
                }
                foreach(var ramp in site.Ramps)
                {
                    var marker=root.Find("Surfaces/"+ramp.Id);var collider=marker!=null?marker.GetComponent<MeshCollider>():null;
                    Require(collider!=null&&collider.enabled&&!collider.isTrigger&&!collider.convex&&marker.localPosition==Vector3.zero
                        &&marker.localRotation==Quaternion.identity&&marker.localScale==Vector3.one,ramp.Id+": native physical slope is missing.");
                    var expected=ramp.BuildMesh();
                    try
                    {
                        var actual=collider.sharedMesh;Require(actual!=null&&actual.triangles.SequenceEqual(expected.triangles),ramp.Id+": collision triangles changed.");
                        var a=actual.vertices;var e=expected.vertices;Require(a.Length==e.Length,ramp.Id+": collision vertices changed.");
                        for(int i=0;i<a.Length;i++)Require((a[i]-e[i]).sqrMagnitude<Epsilon*Epsilon,ramp.Id+": collision does not follow the rendered slope.");
                    }
                    finally{Object.DestroyImmediate(expected);}
                    declared.Add(collider);supports.Add(collider);
                    foreach(float t in new[]{.05f,.5f,.95f})
                    {
                        var p=Vector3.Lerp(ramp.From,ramp.To,t);
                        Require(Mathf.Abs(floor.Height(p)-ramp.HeightAt(p))<.02f,ramp.Id+": real visible slope is covered or missing at "+p+"; actual="+floor.Height(p)+", expected="+ramp.HeightAt(p));
                    }
                }
                Require(root.GetComponentsInChildren<Collider>(true).All(declared.Contains),site.Id+": undeclared enclosing collision box.");
                CheckForeignIntersections(site,root,obstacles,supports);
                foreach(var route in site.Routes)
                {
                    var marker=root.Find("Routes/"+route.Id);
                    Require(marker!=null&&marker.Find("From")!=null&&marker.Find("To")!=null
                        &&(marker.Find("From").localPosition-route.From).sqrMagnitude<1e-8f&&(marker.Find("To").localPosition-route.To).sqrMagnitude<1e-8f,
                        route.Id+": saved route metadata differs.");
                    CheckSavedRoute(route,obstacles);
                    CheckRouteFloor(route,floor);
                }
                CheckConnectivity(site);
                float built=UnionArea(coverage.Select(r=>Intersection(r,site.Bounds)).ToArray());
                float percent=built*100/(site.Bounds.width*site.Bounds.height);
                Require(percent>=30,site.Id+": real body/roof coverage is below 30%: "+percent);
                float blank=BlankSquare(site.Bounds,floor,coverage);
                Require(blank<12,site.Id+": actual unassigned paved square is "+blank+" metres.");
                log.AppendLine($"{site.Id}: {site.Props.Count} native objects; {site.Routes.Count} clear routes; real body/roof union {built:F2} / {site.Bounds.width*site.Bounds.height:F2} m² = {percent:F2}%; largest unmarked hardscape square {blank:F1}m (0.5m sampling).");
            }
            Require(town.GetComponentsInChildren<TownAmbientWalker>(true).Length==0,"Decorative people returned.");
            int renderers=town.GetComponentsInChildren<Renderer>(true).Length;
            int triangles=town.GetComponentsInChildren<MeshFilter>(true).Sum(m=>m.sharedMesh.triangles.Length/3);
            Require(renderers<=480&&triangles<=1400000,"Industrial candidate exceeds provisional town geometry budget.");
            log.AppendLine($"PASS town budget {renderers}/480 renderers / {triangles}/1400000 triangles. No FPS claim.");
            passed=true;
        }
        finally{PrefabUtility.UnloadPrefabContents(town);File.WriteAllText(Report+"/assets.txt",(passed?"PASS\n":"FAILED\n")+log);}
    }

    private static void CheckGeometry(TownSpecialSite site,Transform geometry,IReadOnlyList<Rect> permissions)
    {
        var actual=geometry.GetComponent<MeshFilter>().sharedMesh;Require(actual!=null,site.Id+": native mesh reference is missing.");
        var vertices=actual.vertices;var uv=actual.uv;var uv2=actual.uv2;var normals=actual.normals;var indices=actual.triangles;
        Require(uv.Length==vertices.Length&&uv2.Length==vertices.Length&&normals.Length==vertices.Length,site.Id+": lost native vertex attributes.");
        int vertexOffset=0,indexOffset=0;
        void CheckPart(Mesh source,Matrix4x4 pose,string name)
        {
            var points=source.vertices;var palette=source.uv;var sourceIndices=source.triangles;
            Require(vertexOffset+points.Length<=vertices.Length&&indexOffset+sourceIndices.Length<=indices.Length,name+": real model geometry is missing.");
            for(int i=0;i<points.Length;i++)
            {
                var expected=pose.MultiplyPoint3x4(points[i]);int at=vertexOffset+i;
                Require((vertices[at]-expected).sqrMagnitude<=Epsilon*Epsilon&&(uv[at]-palette[i]).sqrMagnitude<1e-12f
                    &&(uv2[at]-new Vector2(vertices[at].x,vertices[at].z)).sqrMagnitude<1e-8f&&Finite(normals[at]),name+": rendered native XYZ/UV mismatch at vertex "+i);
            }
            for(int i=0;i<sourceIndices.Length;i++)Require(indices[indexOffset+i]==sourceIndices[i]+vertexOffset,name+": real triangle index/winding changed.");
            vertexOffset+=points.Length;indexOffset+=sourceIndices.Length;
        }
        foreach(var prop in site.Props)
        {var mesh=prop.Model.BuildMesh();try{CheckPart(mesh,prop.Pose,prop.Id);}finally{Object.DestroyImmediate(mesh);}}
        var layout=TownSpecialPlacement.BuildLayout(site,permissions);
        try{CheckPart(layout,Matrix4x4.identity,site.Id+" real layout");}finally{Object.DestroyImmediate(layout);}
        Require(vertexOffset==vertices.Length&&indexOffset==indices.Length,site.Id+": undeclared visual geometry in combined mesh.");
    }

    private static void CheckForeignIntersections(TownSpecialSite site,Transform root,Solid[] obstacles,List<Collider> ground)
    {
        foreach(var prop in site.Props)
        {
            var marker=root.Find("Props/"+prop.Id);var own=marker.GetComponentsInChildren<Collider>();
            foreach(var part in own.Select(Solid.From))
                foreach(var other in obstacles.Where(o=>!own.Contains(o.Collider)&&!ground.Contains(o.Collider)))
                    Require(!Overlap(part,other),prop.Id+": physical piece penetrates "+other.Name);
        }
    }
    private static void CheckSavedRoute(TownSpecialRoute route,Solid[] obstacles)
    {
        Require(route.Width>=(route.Vehicle?3.8f:2.2f),route.Id+": inadequate physical width.");
        int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(route.From,route.To)/.5f));
        var rotation=Quaternion.LookRotation(route.To-route.From);
        for(int i=0;i<=count;i++)
        {
            var centre=Vector3.Lerp(route.From,route.To,(float)i/count)+Vector3.up*(route.Vehicle?1.1f:1);
            var probe=new Solid(route.Id,centre,new Vector3(route.Width/2,route.Vehicle?.65f:.7f,route.Vehicle?2.3f:.28f),rotation,null);
            foreach(var obstacle in obstacles)Require(!Overlap(probe,obstacle),route.Id+": full physical passage blocked by "+obstacle.Name+" at "+centre);
        }
    }
    private static void CheckConnectivity(TownSpecialSite site)
    {
        var network=site.Routes.Where(r=>r.Id.EndsWith("VehicleEntry")||r.Id.EndsWith("PedEntry")||r.Id.EndsWith("StopLink")).ToList();
        var pending=site.Routes.Except(network).ToList();bool progress=true;
        while(progress&&pending.Count>0)
        {
            progress=false;
            for(int i=pending.Count-1;i>=0;i--)
                if(network.Any(r=>Distance(pending[i].From,r.From,r.To)<.05f||Distance(pending[i].To,r.From,r.To)<.05f
                    ||Distance(r.From,pending[i].From,pending[i].To)<.05f||Distance(r.To,pending[i].From,pending[i].To)<.05f))
                {network.Add(pending[i]);pending.RemoveAt(i);progress=true;}
        }
        Require(pending.Count==0,site.Id+": disconnected private routes: "+string.Join(", ",pending.Select(r=>r.Id)));
    }

    public static IEnumerator Check(CityWorld city,TownSpecialPlan plan)
    {
        TownWorldSafety.RequireIsolatedEditor();Require(Application.isPlaying&&city!=null&&city.pickup!=null,"Live industrial acceptance requires City.");
        var pickup=city.pickup;var body=pickup.Body;var camera=city.chaseCamera;
        var position=body.position;var rotation=body.rotation;var velocity=body.velocity;var angular=body.angularVelocity;
        bool keyboard=pickup.ReadKeyboard,input=pickup.InputEnabled,mouse=camera.ReadMouse;bool passed=false;var log=new StringBuilder();
        try
        {
            pickup.Stop();pickup.ReadKeyboard=false;pickup.InputEnabled=true;camera.ReadMouse=false;Physics.SyncTransforms();
            foreach(var site in plan.Sites)
            {
                foreach(var route in site.Routes)
                {
                    int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(route.From,route.To)/.5f));var q=Quaternion.LookRotation(route.To-route.From);
                    for(int i=0;i<=count;i++)
                    {
                        var p=Vector3.Lerp(route.From,route.To,(float)i/count)+Vector3.up*(route.Vehicle?1.1f:1);
                        var half=new Vector3(route.Vehicle?1.25f:1.1f,route.Vehicle?.65f:.7f,route.Vehicle?2.3f:.28f);
                        var blocked=Physics.OverlapBox(p,half,q,~0,QueryTriggerInteraction.Ignore)
                            .Where(c=>c.gameObject.scene==city.gameObject.scene&&!c.transform.IsChildOf(pickup.transform)).ToArray();
                        Require(blocked.Length==0,route.Id+": live passage blocked by "+string.Join(", ",blocked.Select(c=>c.name)));
                    }
                }
                var entry=site.Routes.Single(r=>r.Id.EndsWith("VehicleEntry"));yield return Drive(city,entry);
                log.AppendLine($"PASS {site.Id}: {site.Routes.Count} full-width live routes and actual native pickup entry >= {Vector3.Distance(entry.From,entry.To):F1} m; stable heading and no blockage.");
            }
            passed=true;
        }
        finally
        {
            pickup.Stop();body.position=position;body.rotation=rotation;body.velocity=velocity;body.angularVelocity=angular;
            pickup.ReadKeyboard=keyboard;pickup.InputEnabled=input;camera.ReadMouse=mouse;Physics.SyncTransforms();
            Directory.CreateDirectory(Report);File.WriteAllText(Report+"/runtime.txt",(passed?"PASS\n":"FAILED\n")+log);
        }
    }
    private static IEnumerator Drive(CityWorld city,TownSpecialRoute route)
    {
        var pickup=city.pickup;var body=pickup.Body;var direction=(route.To-route.From).normalized;var rotation=Quaternion.LookRotation(direction);
        pickup.Stop();body.position=route.From+Vector3.up*.15f;body.rotation=rotation;Physics.SyncTransforms();yield return new WaitForFixedUpdate();
        pickup.SetInput(.65f,0,false);float start=Time.realtimeSinceStartup,progressAt=start,progress=0,length=Vector3.Distance(route.From,route.To);
        while(Vector3.Dot(body.position-route.From,direction)<length&&Time.realtimeSinceStartup-start<8)
        {
            yield return new WaitForFixedUpdate();float at=Vector3.Dot(body.position-route.From,direction);var side=body.position-route.From-direction*at;side.y=0;
            Require(side.magnitude<.8f&&Quaternion.Angle(body.rotation,rotation)<5,route.Id+": pickup rotated or escaped the real gate.");
            if(at>progress+.15f){progress=at;progressAt=Time.realtimeSinceStartup;}
            if(Time.realtimeSinceStartup-progressAt>=1.5f)
                throw new InvalidOperationException(route.Id+": native pickup stuck. "+NativeContactState(pickup));
        }
        Require(Vector3.Dot(body.position-route.From,direction)>=length,route.Id+": real gate traversal timed out.");pickup.Stop();
    }

    private static string NativeContactState(RetailEmpireTycoon.City.PickupDrive pickup)
    {
        var body=pickup.Body;var bounds=pickup.GetComponent<Collider>().bounds;
        var contacts=Physics.OverlapBox(bounds.center,bounds.extents+Vector3.one*.02f,Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
            .Where(c=>!c.transform.IsChildOf(pickup.transform))
            .Select(c=>c.transform.parent?.name+"/"+c.name+" bounds="+c.bounds);
        var ground=Physics.RaycastAll(body.position+Vector3.up*2,Vector3.down,4,~0,QueryTriggerInteraction.Ignore)
            .Where(h=>!h.transform.IsChildOf(pickup.transform))
            .Select(h=>h.collider.name+" y="+h.point.y.ToString("F3")+" normal="+h.normal);
        return "position="+body.position.ToString("F3")+" velocity="+body.velocity.ToString("F3")+
            " input="+pickup.InputEnabled+" enabled="+pickup.enabled+"; contacts: "+string.Join(" | ",contacts)+"; ground: "+string.Join(" | ",ground);
    }

    private readonly struct Solid
    {
        public readonly string Name;public readonly Vector3 Centre,Half;public readonly Quaternion Rotation;public readonly Collider Collider;
        public Solid(string name,Vector3 centre,Vector3 half,Quaternion rotation,Collider collider)
        {Name=name;Centre=centre;Half=half;Rotation=rotation;Collider=collider;}
        public Vector3[] Axes=>new[]{Rotation*Vector3.right,Rotation*Vector3.up,Rotation*Vector3.forward};
        public static Solid From(Collider c)
        {
            if(c is BoxCollider b)
            {var s=b.transform.lossyScale;return new Solid(c.transform.parent.name+"/"+c.name,b.transform.TransformPoint(b.center),Vector3.Scale(b.size/2,new Vector3(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z))),b.transform.rotation,c);}
            var bounds=TownResidentialPlacement.ColliderBounds(c);return new Solid(c.name,bounds.center,bounds.extents,Quaternion.identity,c);
        }
    }
    private static bool Overlap(Solid a,Solid b)
    {
        var aa=a.Axes;var ba=b.Axes;
        foreach(var raw in aa.Concat(ba).Concat(aa.SelectMany(x=>ba.Select(y=>Vector3.Cross(x,y)))))
        {
            if(raw.sqrMagnitude<1e-8f)continue;var axis=raw.normalized;
            float Radius(Solid s,Vector3[] basis)=>Mathf.Abs(Vector3.Dot(axis,basis[0]))*s.Half.x+Mathf.Abs(Vector3.Dot(axis,basis[1]))*s.Half.y+Mathf.Abs(Vector3.Dot(axis,basis[2]))*s.Half.z;
            if(Mathf.Abs(Vector3.Dot(b.Centre-a.Centre,axis))>=Radius(a,aa)+Radius(b,ba)-.003f)return false;
        }
        return true;
    }

    private readonly struct Floor
    {
        public readonly Vector3 A,B,C;public readonly Vector2 Uv;public readonly Rect Bounds;
        public Floor(Vector3 a,Vector3 b,Vector3 c,Vector2 uv){A=a;B=b;C=c;Uv=uv;Bounds=Rect.MinMaxRect(Mathf.Min(a.x,b.x,c.x),Mathf.Min(a.z,b.z,c.z),Mathf.Max(a.x,b.x,c.x),Mathf.Max(a.z,b.z,c.z));}
        public bool Covers(Vector3 p)
        {
            if(!Bounds.Contains(new Vector2(p.x,p.z)))return false;
            float Side(Vector3 a,Vector3 b)=>(b.x-a.x)*(p.z-a.z)-(b.z-a.z)*(p.x-a.x);
            float x=Side(A,B),y=Side(B,C),z=Side(C,A);return (x>=-.001f&&y>=-.001f&&z>=-.001f)||(x<=.001f&&y<=.001f&&z<=.001f);
        }
        public float HeightAt(Vector3 p)
        {var n=Vector3.Cross(B-A,C-A);return A.y-(n.x*(p.x-A.x)+n.z*(p.z-A.z))/n.y;}
    }
    private sealed class Floors
    {
        private readonly Dictionary<Vector2Int,List<Floor>> cells=new Dictionary<Vector2Int,List<Floor>>();
        private static Vector2Int Key(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/2),Mathf.FloorToInt(p.z/2));
        public void Add(Floor f)
        {
            for(int z=Mathf.FloorToInt(f.Bounds.yMin/2);z<=Mathf.FloorToInt(f.Bounds.yMax/2);z++)
                for(int x=Mathf.FloorToInt(f.Bounds.xMin/2);x<=Mathf.FloorToInt(f.Bounds.xMax/2);x++)
                {var key=new Vector2Int(x,z);if(!cells.TryGetValue(key,out var list))cells.Add(key,list=new List<Floor>());list.Add(f);}
        }
        public IEnumerable<Floor> At(Vector3 p)=>cells.TryGetValue(Key(p),out var list)?list.Where(t=>t.Covers(p)):Enumerable.Empty<Floor>();
        public float Height(Vector3 p)=>At(p).Select(t=>t.HeightAt(p)).DefaultIfEmpty(float.NegativeInfinity).Max();
        public bool Paved(Vector3 p)
        {
            var at=At(p).ToArray();if(at.Length==0)return false;float top=at.Max(t=>t.HeightAt(p));
            return at.Any(t=>t.HeightAt(p)>=top-.001f&&new[]{TownColor.Asphalt,TownColor.Pavement,TownColor.Stone}.Any(c=>(t.Uv-TownPalette.UV(c)).sqrMagnitude<1e-12f));
        }
    }
    private static Floors ReadFloor(Transform town,Material palette)
    {
        var result=new Floors();
        foreach(var filter in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var r=filter.GetComponent<MeshRenderer>();if(r==null||!r.enabled||r.sharedMaterials.Any(m=>m==null||m.mainTexture!=palette.mainTexture))continue;
            var mesh=filter.sharedMesh;var v=mesh.vertices.Select(filter.transform.TransformPoint).ToArray();var uv=mesh.uv;var t=mesh.triangles;
            for(int i=0;i<t.Length;i+=3)
            {
                int a=t[i],b=t[i+1],c=t[i+2];
                if(v[a].y<-.01f||v[a].y>.27f||v[b].y<-.01f||v[b].y>.27f||v[c].y<-.01f||v[c].y>.27f
                    ||Vector3.Cross(v[b]-v[a],v[c]-v[a]).normalized.y<.99f)continue;
                result.Add(new Floor(v[a],v[b],v[c],uv[a]));
            }
        }
        return result;
    }
    private static float FloorHeight(Floors floor,Vector3 p)=>floor.Height(p);
    private static void SampleSurface(Floors floor,TownSpecialSurface surface,IReadOnlyList<TownSpecialRamp> ramps)
    {
        for(float z=surface.Bounds.yMin+.2f;z<surface.Bounds.yMax;z+=.5f)
            for(float x=surface.Bounds.xMin+.2f;x<surface.Bounds.xMax;x+=.5f)
            {
                if(ramps.Any(r=>r.Bounds.Contains(new Vector2(x,z))))continue;
                Require(floor.At(new Vector3(x,0,z)).Any(t=>Mathf.Abs(t.A.y-surface.Y)<.001f&&(t.Uv-TownPalette.UV(surface.Color)).sqrMagnitude<1e-12f),surface.Id+": actual ground surface is missing.");
            }
    }
    private static void CheckRouteFloor(TownSpecialRoute route,Floors floor)
    {
        var side=Vector3.Cross(Vector3.up,(route.To-route.From).normalized);int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(route.From,route.To)/.5f));
        for(int i=0;i<=n;i++)foreach(float offset in new[]{-1f,0,1f})
            Require(Finite(floor.Height(Vector3.Lerp(route.From,route.To,(float)i/n)+side*(route.Width/2-.02f)*offset)),route.Id+": actual ground missing across full width.");
    }
    private static float BlankSquare(Rect bounds,Floors floor,List<Rect> covered)
    {
        int width=Mathf.FloorToInt(bounds.width*2),height=Mathf.FloorToInt(bounds.height*2),largest=0;var previous=new int[width];
        for(int z=0;z<height;z++)
        {
            var row=new int[width];
            for(int x=0;x<width;x++)
            {
                var p=new Vector3(bounds.xMin+(x+.5f)/2,0,bounds.yMin+(z+.5f)/2);
                if(covered.Any(r=>r.Contains(new Vector2(p.x,p.z)))||!floor.Paved(p))continue;
                row[x]=1+Mathf.Min(previous[x],x>0?row[x-1]:0,x>0?previous[x-1]:0);largest=Mathf.Max(largest,row[x]);
            }
            previous=row;
        }
        return largest/2f;
    }
    private static float UnionArea(Rect[] areas)
    {
        var xs=areas.SelectMany(r=>new[]{r.xMin,r.xMax}).Distinct().OrderBy(x=>x).ToArray();float total=0;
        for(int i=1;i<xs.Length;i++)
        {
            float middle=(xs[i-1]+xs[i])/2,end=float.NegativeInfinity;
            foreach(var r in areas.Where(r=>middle>r.xMin&&middle<r.xMax).OrderBy(r=>r.yMin))
            {if(r.yMax>end){total+=(xs[i]-xs[i-1])*(r.yMax-Mathf.Max(r.yMin,end));end=r.yMax;}}
        }
        return total;
    }
    private static Rect Intersection(Rect a,Rect b)=>Rect.MinMaxRect(Mathf.Max(a.xMin,b.xMin),Mathf.Max(a.yMin,b.yMin),Mathf.Min(a.xMax,b.xMax),Mathf.Min(a.yMax,b.yMax));
    private static bool Contains(Bounds a,Bounds b,float pad)=>a.min.x-pad<=b.min.x&&a.min.y-pad<=b.min.y&&a.min.z-pad<=b.min.z&&a.max.x+pad>=b.max.x&&a.max.y+pad>=b.max.y&&a.max.z+pad>=b.max.z;
    private static float Distance(Vector3 p,Vector3 a,Vector3 b){var d=b-a;return Vector3.Distance(p,a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude));}
    private static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
    private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
    private static void GeometryContracts()
    {
        var slope=new TownSpecialRamp("RampContract",Vector3.zero,new Vector3(0,.09f,4),5.8f);
        var slopeMesh=slope.BuildMesh();
        try
        {
            var v=slopeMesh.vertices;
            Require(Vector3.Cross(v[1]-v[0],v[2]-v[0]).normalized.y>.99f,"Driveway collision faces downwards.");
            var plane=new Floor(v[0],v[1],v[2],TownPalette.UV(TownColor.Asphalt));
            Require(Mathf.Abs(plane.HeightAt(new Vector3(0,0,2))-.045f)<Epsilon,"Sloped ground height contract failed.");
            var pieces=TownSpecialPlacement.SurfacePieces(new Rect(-5,1,10,8),new[]{slope});
            Require(Mathf.Abs(pieces.Sum(r=>r.width*r.height)-(80-5.8f*3))<.001f&&!pieces.Any(r=>r.Overlaps(slope.Bounds)),"Driveway cutout retained a blocking vertical ledge.");
        }
        finally{Object.DestroyImmediate(slopeMesh);}
        var a=new Solid("a",Vector3.zero,Vector3.one,Quaternion.identity,null);
        Require(Overlap(a,new Solid("b",Vector3.right,Vector3.one,Quaternion.identity,null)),"Physical overlap contract failed.");
        Require(!Overlap(a,new Solid("b",Vector3.right*2,Vector3.one,Quaternion.identity,null)),"Physical touching contract failed.");
        Require(Mathf.Abs(UnionArea(new[]{new Rect(0,0,2,2),new Rect(1,0,2,2)})-6)<Epsilon,"Coverage union double-counted overlapping roofs.");
        var floor=new Floors();var uv=TownPalette.UV(TownColor.Pavement);
        floor.Add(new Floor(new Vector3(0,.08f,0),new Vector3(12,.08f,0),new Vector3(12,.08f,12),uv));
        floor.Add(new Floor(new Vector3(0,.08f,0),new Vector3(12,.08f,12),new Vector3(0,.08f,12),uv));
        Require(BlankSquare(new Rect(0,0,12,12),floor,new List<Rect>())==12,"Real blank paving contract failed.");
        floor.Add(new Floor(new Vector3(0,.09f,0),new Vector3(12,.09f,0),new Vector3(12,.09f,12),TownPalette.UV(TownColor.Grass)));
        floor.Add(new Floor(new Vector3(0,.09f,0),new Vector3(12,.09f,12),new Vector3(0,.09f,12),TownPalette.UV(TownColor.Grass)));
        Require(BlankSquare(new Rect(0,0,12,12),floor,new List<Rect>())==0,"Hidden paving was counted as visible blank space.");
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
