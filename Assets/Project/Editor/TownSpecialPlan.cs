using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEngine;

/// <summary>Two explicit industrial places; no supplier, shop, residential or landscape replanning.</summary>
internal sealed class TownSpecialPlan
{
    public const string AssetRoot="Assets/Art/TownSpecialSites";
    public const string WorkshopRoot="Special sites stage 5/Workshop";
    public const string ConstructionRoot="Construction plot/Construction stage 5";
    public IReadOnlyList<TownSpecialSite> Sites { get; }
    public IReadOnlyList<TownSpecialModel> Models { get; }
    // The northern stop bay is deliberately outside every permission rectangle.
    public IReadOnlyList<Rect> SiteAreas { get; } = Array.AsReadOnly(new[] {
        new Rect(76,-188,88,34), new Rect(94,-188,70,40), new Rect(76,-158,10,10),
        new Rect(128,-90,36,64)
    });
    public IReadOnlyList<Rect> AllowedAreas { get; }

    private TownSpecialPlan(IReadOnlyList<TownSpecialModel> models,TownSpecialSite[] sites)
    {
        Models=models; Sites=Array.AsReadOnly(sites);
        // Complete source sidewalk tiles are replaced around the construction gate, not a whole street.
        AllowedAreas=Array.AsReadOnly(SiteAreas.Concat(new[]{new Rect(118,-66,46,24)}).ToArray());
    }

    public static TownSpecialPlan Create()
    {
        var models=TownSpecialModels.Create(); var catalogue=models.ToDictionary(m=>m.Id);
        TownSpecialProp Prop(string site,int n,string model,float x,float z,float yaw=0,float y=.08f,Vector3? scale=null)
            =>new TownSpecialProp(site+"."+n,catalogue[model],new Vector3(x,y,z),yaw,scale??Vector3.one);
        var workshop=new TownSpecialSite("Workshop",WorkshopRoot,new Rect(78,-186,84,36));
        var work=new List<TownSpecialProp> {
            Prop("Workshop",0,"WorkshopHall",110,-160,180),
            Prop("Workshop",1,"WorkshopOffice",133,-158),
            Prop("Workshop",2,"WorkshopStore",150,-158,180),
            Prop("Workshop",3,"WorkshopCanopy",100,-180,0,.09f),
            Prop("Workshop",4,"Barrels",91,-183,0,.09f),
            Prop("Workshop",5,"MaterialPallet",94,-183,0,.09f),
            Prop("Workshop",6,"PipeRack",117,-181,0,.09f),
            Prop("Workshop",7,"ToolStation",89,-177,90,.09f),
            Prop("Workshop",8,"Barrels",157,-173,0,.09f)
        };
        Fence(workshop.Id,work,catalogue["SiteFence"],new Vector3(79,.08f,-185.5f),new Vector3(161,.08f,-185.5f),0,
            new[]{new Vector2(80.1f,82.9f),new Vector2(140.9f,147.1f)});
        Fence(workshop.Id,work,catalogue["SiteFence"],new Vector3(161,.08f,-185.5f),new Vector3(161,.08f,-152),90);
        Fence(workshop.Id,work,catalogue["SiteFence"],new Vector3(97,.08f,-152),new Vector3(161,.08f,-152),0,new[]{new Vector2(131.5f,134.5f)});
        Fence(workshop.Id,work,catalogue["SiteFence"],new Vector3(79,.08f,-185.5f),new Vector3(79,.08f,-156),90);
        workshop.Initialize(work,new[] {
            new TownSpecialSurface("GroundSouth",new Rect(78,-186,84,30),.08f,TownColor.Grass),
            new TownSpecialSurface("GroundNorthEast",new Rect(96,-156,66,6),.08f,TownColor.Grass),
            new TownSpecialSurface("GroundNorthWest",new Rect(78,-156,6,6),.08f,TownColor.Grass),
            new TownSpecialSurface("WorkingApron",new Rect(86,-185,74,15),.09f,TownColor.Asphalt),
            new TownSpecialSurface("EntrySidewalkShoulders",new Rect(138,-188,12,2),.14f,TownColor.Pavement)
        },new[] {
            new TownSpecialRoute("Workshop.VehicleEntry",new Vector3(144,0,-192),new Vector3(144,0,-173),3.8f,true,true),
            new TownSpecialRoute("Workshop.WorkingLane",new Vector3(144,0,-173),new Vector3(97.3f,0,-173),3.8f,true,true),
            new TownSpecialRoute("Workshop.PedEntry",new Vector3(81.5f,0,-187.5f),new Vector3(81.5f,0,-173),2.2f,false,true),
            new TownSpecialRoute("Workshop.HallApproach",new Vector3(81.5f,0,-173),new Vector3(97.3f,0,-173),2.2f,false,true),
            new TownSpecialRoute("Workshop.HallDoor",new Vector3(97.3f,0,-173),new Vector3(97.3f,0,-168.4f),2.2f,false,true),
            new TownSpecialRoute("Workshop.OfficeDoor",new Vector3(133,0,-149.5f),new Vector3(133,0,-152.7f),2.2f,false,true),
            new TownSpecialRoute("Workshop.StopLink",new Vector3(90,0,-149.5f),new Vector3(133,0,-149.5f),2.2f,false,false)
        },new[] {
            new TownSpecialRamp("Workshop.EntryRamp",new Vector3(144,.012f,-188),new Vector3(144,.09f,-184),5.8f)
        });

        var construction=new TownSpecialSite("Construction",ConstructionRoot,new Rect(130,-88,32,60));
        var build=new List<TownSpecialProp> {
            Prop("Construction",0,"ConstructionFrame",144,-40),
            Prop("Construction",1,"TowerCrane",159,-38,90),
            Prop("Construction",2,"SiteCabin",137,-83),
            Prop("Construction",3,"SiteCabin",146,-83),
            Prop("Construction",4,"SiteCanopy",139,-71),
            Prop("Construction",5,"SiteStore",155,-73,180),
            Prop("Construction",6,"MaterialPallet",137,-73),
            Prop("Construction",7,"MaterialPallet",141,-73),
            Prop("Construction",8,"PipeRack",139,-68),
            Prop("Construction",9,"Barrels",155,-80),
            Prop("Construction",10,"ToolStation",152,-86)
        };
        Fence(construction.Id,build,catalogue["SiteFence"],new Vector3(130.5f,.08f,-87),new Vector3(161,.08f,-87),0);
        Fence(construction.Id,build,catalogue["SiteFence"],new Vector3(161,.08f,-87),new Vector3(161,.08f,-29),90);
        Fence(construction.Id,build,catalogue["SiteFence"],new Vector3(130.5f,.08f,-29),new Vector3(161,.08f,-29),0);
        Fence(construction.Id,build,catalogue["SiteFence"],new Vector3(130.5f,.08f,-87),new Vector3(130.5f,.08f,-29),90,new[]{new Vector2(-55.1f,-48.9f)});
        construction.Initialize(build,new[] {
            new TownSpecialSurface("WorkGround",construction.Bounds,.08f,TownColor.Soil),
            new TownSpecialSurface("UnloadingPad",new Rect(134,-64,24,9),.09f,TownColor.Stone),
            new TownSpecialSurface("EntrySidewalkShoulders",new Rect(124,-66,2,24),.14f,TownColor.Pavement)
        },new[] {
            new TownSpecialRoute("Construction.VehicleEntry",new Vector3(120,0,-52),new Vector3(142,0,-52),3.8f,true,true),
            new TownSpecialRoute("Construction.UnloadingTurn",new Vector3(140,0,-52),new Vector3(140,0,-59),3.8f,true,true),
            new TownSpecialRoute("Construction.OfficeWalk",new Vector3(131.7f,0,-82),new Vector3(131.7f,0,-52),2.2f,false,true),
            new TownSpecialRoute("Construction.OfficeApproach",new Vector3(131.7f,0,-79.6f),new Vector3(134.5f,0,-79.6f),2.2f,false,true),
            new TownSpecialRoute("Construction.SecondOfficeApproach",new Vector3(134.5f,0,-79.6f),new Vector3(143.5f,0,-79.6f),2.2f,false,true)
        },new[] {
            new TownSpecialRamp("Construction.EntryRamp",new Vector3(124,.012f,-52),new Vector3(134,.08f,-52),5.8f,TownColor.Soil),
            new TownSpecialRamp("Construction.PadRamp",new Vector3(140,.08f,-54),new Vector3(140,.09f,-56),5f,TownColor.Soil)
        });
        return new TownSpecialPlan(models,new[]{workshop,construction});
    }

    private static void Fence(string site,List<TownSpecialProp> props,TownSpecialModel model,Vector3 a,Vector3 b,float yaw,Vector2[] openings=null)
    {
        var direction=(b-a).normalized; a+=direction*.1f; b-=direction*.1f;
        bool horizontal=Mathf.Abs(b.x-a.x)>.1f;
        float first=horizontal?a.x:a.z,last=horizontal?b.x:b.z;
        float cursor=first;
        var gaps=(openings??Array.Empty<Vector2>()).OrderBy(g=>g.x).ToArray();
        void Run(float begin,float end)
        {
            if(end-begin<.1f)return;
            int count=Mathf.CeilToInt((end-begin)/3); float length=(end-begin)/count;
            for(int i=0;i<count;i++)
            {
                float value=begin+(i+.5f)*length;
                var point=horizontal?new Vector3(value,a.y,a.z):new Vector3(a.x,a.y,value);
                props.Add(new TownSpecialProp(site+".Fence"+props.Count,model,point,yaw,new Vector3(length/3,1,1)));
            }
        }
        foreach(var gap in gaps){Run(cursor,gap.x);cursor=gap.y;}
        Run(cursor,last);
    }
}

internal sealed class TownSpecialSite
{
    public string Id { get; }
    public string RootPath { get; }
    public Rect Bounds { get; }
    public IReadOnlyList<TownSpecialProp> Props { get; private set; }
    public IReadOnlyList<TownSpecialSurface> Surfaces { get; private set; }
    public IReadOnlyList<TownSpecialRoute> Routes { get; private set; }
    public IReadOnlyList<TownSpecialRamp> Ramps { get; private set; }
    public TownSpecialSite(string id,string rootPath,Rect bounds){Id=id;RootPath=rootPath;Bounds=bounds;}
    public void Initialize(IEnumerable<TownSpecialProp> props,IEnumerable<TownSpecialSurface> surfaces,IEnumerable<TownSpecialRoute> routes,IEnumerable<TownSpecialRamp> ramps)
    {
        if(Props!=null)throw new InvalidOperationException(Id+": site already authored.");
        Props=Array.AsReadOnly(props.ToArray());Surfaces=Array.AsReadOnly(surfaces.ToArray());Routes=Array.AsReadOnly(routes.ToArray());
        Ramps=Array.AsReadOnly(ramps.ToArray());
    }
}
/// <summary>Axis-aligned drivable transition: its visual and physical top use the same sloped plane.</summary>
internal sealed class TownSpecialRamp
{
    public string Id { get; }
    public Vector3 From { get; }
    public Vector3 To { get; }
    public float Width { get; }
    public Rect Bounds { get; }
    public TownColor SurfaceColor { get; }
    public TownSpecialRamp(string id,Vector3 from,Vector3 to,float width,TownColor surfaceColor=TownColor.Asphalt)
    {
        var delta=to-from;
        float length=new Vector2(delta.x,delta.z).magnitude;
        if(string.IsNullOrWhiteSpace(id)||new[]{width,from.x,from.y,from.z,to.x,to.y,to.z}.Any(v=>float.IsNaN(v)||float.IsInfinity(v))||
            width<3.8f||Mathf.Min(from.y,to.y)<0||Mathf.Max(from.y,to.y)>.1f||delta.y<=0||delta.y/length>.05f||
            (Mathf.Abs(delta.x)>.001f&&Mathf.Abs(delta.z)>.001f)||length<2)
            throw new ArgumentException("Invalid native driveway ramp: "+id);
        Id=id;From=from;To=to;Width=width;SurfaceColor=surfaceColor;
        var side=Vector3.Cross(Vector3.up,new Vector3(delta.x,0,delta.z).normalized)*width/2;
        var a=from-side;var b=from+side;var c=to-side;var d=to+side;
        Bounds=Rect.MinMaxRect(Mathf.Min(a.x,b.x,c.x,d.x),Mathf.Min(a.z,b.z,c.z,d.z),
            Mathf.Max(a.x,b.x,c.x,d.x),Mathf.Max(a.z,b.z,c.z,d.z));
    }
    public float HeightAt(Vector3 p)
    {
        var delta=To-From;delta.y=0;
        return Mathf.Lerp(From.y,To.y,Mathf.Clamp01(Vector3.Dot(p-From,delta)/delta.sqrMagnitude));
    }
    public void Append(TownMeshBuilder mesh)
    {
        var delta=To-From;delta.y=0;var side=Vector3.Cross(Vector3.up,delta.normalized)*Width/2;
        mesh.Quad(From-side,To-side,To+side,From+side,SurfaceColor);
    }
    public Mesh BuildMesh(){var mesh=new TownMeshBuilder();Append(mesh);return mesh.Build(Id+" physical ramp");}
}

internal sealed class TownSpecialProp
{
    public string Id { get; }
    public TownSpecialModel Model { get; }
    public Vector3 Position { get; }
    public float Yaw { get; }
    public Vector3 Scale { get; }
    public Matrix4x4 Pose => Matrix4x4.TRS(Position,Quaternion.Euler(0,Yaw,0),Scale);
    public TownSpecialProp(string id,TownSpecialModel model,Vector3 position,float yaw,Vector3 scale)
    {Id=id;Model=model;Position=position;Yaw=yaw;Scale=scale;}
}
internal sealed class TownSpecialSurface
{
    public string Id { get; }
    public Rect Bounds { get; }
    public float Y { get; }
    public TownColor Color { get; }
    public TownSpecialSurface(string id,Rect bounds,float y,TownColor color){Id=id;Bounds=bounds;Y=y;Color=color;}
}
internal sealed class TownSpecialRoute
{
    public string Id { get; }
    public Vector3 From { get; }
    public Vector3 To { get; }
    public float Width { get; }
    public bool Vehicle { get; }
    public bool Draw { get; }
    public TownSpecialRoute(string id,Vector3 from,Vector3 to,float width,bool vehicle,bool draw)
    {Id=id;From=from;To=to;Width=width;Vehicle=vehicle;Draw=draw;}
}
