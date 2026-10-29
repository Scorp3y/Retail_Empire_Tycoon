using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Five individually authored player-vehicle silhouettes, in town metres, from the approved concept.</summary>
public static class PlayerVehicleModelBatch
{
    internal const string Root = "Assets/Art/PlayerVehicles", Data = "Assets/Resources/PlayerVehicles";
    internal const string Report = "Library/PlayerVehicleQA", Pictures = "Docs/PlayerVehicles";
    private const int Paint=0,Trim=1,Glass=2,Metal=3,Lamp=4,Tail=5;
    private sealed class Design
    {
        public string Id, Name; public Color Color; public int Price;
        public float Length, Width, Height, Radius, Payload, Volume, Mass;
        public float[] Axles; public bool Van, Curtain;
        public float CabHeight => Curtain ? 2.92f : Length > 8 ? 3.08f : 2.65f;
        public Design(string id,string name,Color color,int price,float l,float w,float h,float radius,float kg,float volume,float mass,float[] axles,bool van=false,bool curtain=false)
        {Id=id;Name=name;Color=color;Price=price;Length=l;Width=w;Height=h;Radius=radius;Payload=kg;Volume=volume;Mass=mass;Axles=axles;Van=van;Curtain=curtain;}
    }
    private static readonly Design[] Designs={
        new("compact_van","Compact van",new Color(.84f,.79f,.64f),8000,4.25f,1.78f,2.05f,.34f,600,4,1600,new[]{1.27f,-1.30f},true),
        new("high_roof_van","High-roof van",new Color(.19f,.55f,.51f),16000,5.45f,2.02f,2.70f,.38f,1000,8,2300,new[]{1.74f,-1.78f},true),
        new("light_box_truck","Light box truck",new Color(.76f,.31f,.18f),30000,6.15f,2.20f,3.12f,.44f,1800,14,3200,new[]{2.03f,-1.99f}),
        new("medium_cargo_truck","Medium cargo truck",new Color(.24f,.43f,.60f),55000,7.30f,2.36f,3.53f,.48f,3500,24,4800,new[]{2.53f,-2.40f},false,true),
        new("heavy_box_truck","Heavy box truck",new Color(.22f,.43f,.27f),90000,8.40f,2.48f,3.78f,.51f,6000,40,7000,new[]{3.02f,-2.03f,-3.14f})
    };
    public static void Build()=>Run(true);
    public static void RebuildModels()=>Run(false);
    public static void Review()
    {
        TownWorldSafety.RequireIsolatedEditor();
        try{Gallery();PlayerVehicleShowroomBatch.CaptureAndValidate();EditorApplication.Exit(0);}
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static void Run(bool install)
    {
        TownWorldSafety.RequireIsolatedEditor(); Directory.CreateDirectory(Report); Directory.CreateDirectory(Pictures);
        try
        {
            File.WriteAllText(Report+"/build.txt","RUNNING\n");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            foreach(string folder in new[]{Root,Root+"/Meshes",Root+"/Materials",Root+"/Prefabs",Data})Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            File.WriteAllText(Report+"/geometry.txt",string.Empty);
            foreach(var design in Designs) Author(design);
            AssetDatabase.SaveAssets();
            if(install)PlayerVehicleShowroomBatch.Install();else PlayerVehicleShowroomBatch.CaptureAndValidate();
            Gallery();
            File.WriteAllText(Report+"/build.txt","PASS five distinct concept-matched vehicle prefabs, opaque windows, separate wheel pivots, increasing capacities and five dealership displays.\n");
            File.WriteAllLines(Report+"/outputs.txt",Directory.GetFiles(Root,"*",SearchOption.AllDirectories).Concat(Directory.GetFiles(Data,"*",SearchOption.AllDirectories))
                .Concat(new[]{Root+".meta",Data+".meta",TownWorldAssembler.PrefabPath})
                .Concat(Directory.GetFiles(TownShowroomPlacement.Root+"/Meshes","FleetTrim*.asset*"))
                .Concat(Directory.GetFiles("Assets/Art/ShopUi/Items","player_*.png*")).Select(p=>p.Replace('\\','/')).Distinct());
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Report+"/build.txt","FAIL "+error);EditorApplication.Exit(1);}
    }
    private static Material Material(string name,Color color)
    {
        string path=Root+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.name=name;material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",name=="OpaqueGlass"?.27f:.12f);
        material.SetFloat("_Surface",0);material.SetFloat("_Cull",0);material.enableInstancing=true;
        if(name=="WarmHeadlamps"){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(.22f,.17f,.08f));}
        EditorUtility.SetDirty(material);return material;
    }
    private static Material[] Palette(Design design,bool cargo=false)=>new[]{
        Material(cargo?"IvoryCargo":design.Id,cargo?new Color(.83f,.81f,.75f):design.Color),
        Material("CharcoalRubber",new Color(.13f,.15f,.16f)),Material("OpaqueGlass",new Color(.095f,.14f,.19f,1)),
        Material("SatinMetal",new Color(.50f,.53f,.53f)),Material("WarmHeadlamps",new Color(.99f,.89f,.65f)),Material("TailLamps",new Color(.65f,.10f,.07f))};
    private static void Author(Design d)
    {
        var root=new GameObject(d.Name);var body=new LowPolyPropMesh(Root+"/Meshes");var cargo=new LowPolyPropMesh(Root+"/Meshes");
        var materials=Palette(d);var bodyPrefab=root.AddComponent<PlayerVehicleModel>();
        float f=d.Length*.5f,b=-f,r=d.Radius;
        // The lower contour follows each wheel well, so tires sit in actual openings rather than through a solid cuboid.
        var profile=new List<Vector2>{new(b,r+.05f)};
        foreach(float axle in d.Axles.OrderBy(a=>a))
            for(int step=0;step<=8;step++){float angle=Mathf.PI-step*Mathf.PI/8;profile.Add(new Vector2(axle+Mathf.Cos(angle)*(r+.075f),r+Mathf.Sin(angle)*(r+.075f)));}
        profile.Add(new Vector2(f,r+.05f));
        float cabinRear=d.Van?b:f-1.88f;
        if(d.Van)
        {
            profile.AddRange(new[]{new Vector2(f,1.12f),new Vector2(f-.23f,1.26f),new Vector2(f-1.12f,d.Height>2.5f?2.2f:d.Height-.15f),new Vector2(f-1.42f,d.Height-.025f),new Vector2(b+.13f,d.Height),new Vector2(b,d.Height-.14f)});
            body.Profile(profile.ToArray(),0,d.Width,.045f,Paint);
            VanDetails(body,d);
        }
        else
        {
            // Trucks share a frame, but the cab and freight body remain separate shapes/materials.
            body.BeveledBox(new Vector3(0,.64f,-.1f),new Vector3(d.Width*.72f,.23f,d.Length-.30f),.035f,Trim);
            var cabProfile=new[]{new Vector2(cabinRear,r+.05f),new Vector2(f,r+.05f),new Vector2(f,1.44f),new Vector2(f-.24f,d.CabHeight-.18f),new Vector2(f-.43f,d.CabHeight),new Vector2(cabinRear+.12f,d.CabHeight),new Vector2(cabinRear,d.CabHeight-.13f)};
            // Cab arch uses the same real cutout as the van.
            var contour=new List<Vector2>{cabProfile[0]};float frontAxle=d.Axles[0];
            for(int step=0;step<=8;step++){float a=Mathf.PI-step*Mathf.PI/8;contour.Add(new Vector2(frontAxle+Mathf.Cos(a)*(r+.08f),r+Mathf.Sin(a)*(r+.08f)));}
            contour.AddRange(cabProfile.Skip(1));body.Profile(contour.ToArray(),0,d.Width-.10f,.055f,Paint);
            TruckDetails(body,cargo,d,cabinRear);
        }
        // Bumpers, grille and lights repeat the restrained rectangular language of the illustration.
        body.BeveledBox(new Vector3(0,r+.08f,f+.015f),new Vector3(d.Width+.045f,.37f,.23f),.055f,Trim);
        body.BeveledBox(new Vector3(0,r+.07f,b-.015f),new Vector3(d.Width,.25f,.18f),.035f,Trim);
        float lightY=d.Van?1.02f:1.12f;
        body.BeveledBox(new Vector3(0,lightY,f+.026f),new Vector3(d.Width*.47f,.31f,.045f),.014f,Trim);
        foreach(float y in new[]{lightY-.075f,lightY+.075f})body.Box(new Vector3(0,y,f+.055f),new Vector3(d.Width*.43f,.025f,.012f),Metal);
        foreach(float side in new[]{-1f,1f})
        {
            body.BeveledBox(new Vector3(side*d.Width*.365f,lightY,f+.036f),new Vector3(d.Width*.22f,.31f,.07f),.024f,Metal);
            body.BeveledBox(new Vector3(side*d.Width*.365f,lightY+.015f,f+.078f),new Vector3(d.Width*.19f,.25f,.025f),.014f,Lamp);
            float x=side*(d.Width*.5f+.095f),z=f-(d.Van?.70f:.48f),y=d.Van?1.42f:1.85f;
            body.BeveledBox(new Vector3(x,y,z),new Vector3(.20f,d.Van?.28f:.43f,.17f),.025f,Trim);
            body.Box(new Vector3(side*(d.Width*.5f+.02f),y-.11f,z),new Vector3(.18f,.05f,.045f),Trim);
            body.BeveledBox(new Vector3(side*(d.Width*.5f-.13f),.75f,b-.105f),new Vector3(.19f,.16f,.025f),.012f,Tail);
        }
        body.Box(new Vector3(0,r+.08f,f+.137f),new Vector3(.47f,.10f,.012f),Trim);
        AddMesh(root.transform,"Body",body.Save(d.Id+"_body"),materials);
        var indicators=new LowPolyPropMesh(Root+"/Meshes");
        foreach(float side in new[]{-1f,1f})
        {
            indicators.BeveledBox(new Vector3(side*d.Width*.447f,lightY+.015f,f+.095f),new Vector3(d.Width*.032f,.24f,.022f),.006f,Paint);
            if(!d.Van)foreach(float z in new[]{cabinRear-.3f,b+.25f})indicators.Box(new Vector3(side*(d.Width*.5f+.034f),1.18f,z),new Vector3(.025f,.06f,.14f),Paint);
        }
        var amber=(Material[])materials.Clone();amber[Paint]=Material("AmberIndicators",new Color(.94f,.46f,.075f));
        AddMesh(root.transform,"Amber marker lenses",indicators.Save(d.Id+"_indicators"),amber);
        if(!d.Van)AddMesh(root.transform,"Cargo body",cargo.Save(d.Id+"_cargo"),Palette(d,true));
        var wheel=WheelMesh(d);var mounts=new List<PlayerVehicleModel.Wheel>();
        for(int axle=0;axle<d.Axles.Length;axle++)foreach(float side in new[]{-1f,1f})
        {
            var pivot=new GameObject((axle==0?"Front":"Rear")+" wheel "+side).transform;pivot.SetParent(root.transform,false);
            pivot.localPosition=new Vector3(side*(d.Width*.5f-.09f),r,d.Axles[axle]);
            AddMesh(pivot,"Tire and inset metal hub",wheel,materials);mounts.Add(new PlayerVehicleModel.Wheel{pivot=pivot,front=axle==0});
        }
        bodyPrefab.wheels=mounts.ToArray();
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+d.Id+".prefab");
        string dataPath=Data+"/"+d.Id+".asset";var definition=AssetDatabase.LoadAssetAtPath<PlayerVehicleDefinition>(dataPath);
        if(definition==null){definition=ScriptableObject.CreateInstance<PlayerVehicleDefinition>();AssetDatabase.CreateAsset(definition,dataPath);}
        definition.id=d.Id;definition.displayName=d.Name;definition.price=d.Price;definition.payloadKg=d.Payload;definition.cargoM3=d.Volume;
        definition.width=d.Width;definition.length=d.Length+.25f;definition.height=d.Height;definition.wheelRadius=r;definition.mass=d.Mass;
        definition.topSpeed=d.Van?15:12;definition.acceleration=d.Van?4.5f:3.5f;definition.model=prefab;
        definition.icon=ShopUiPreviews.Render(prefab,"player_"+d.Id,25);EditorUtility.SetDirty(definition);
        int triangles=root.GetComponentsInChildren<MeshFilter>().Sum(filter=>filter.sharedMesh.triangles.Length/3);
        File.AppendAllText(Report+"/geometry.txt",$"{d.Id}: {triangles} triangles, {mounts.Count} wheels, {d.Payload} kg / {d.Volume} m3; {d.Length} x {d.Width} x {d.Height} m.\n");
        if(triangles>9500)throw new InvalidOperationException("Vehicle exceeds low-poly budget.");
        Object.DestroyImmediate(root);
    }
    private static void VanDetails(LowPolyPropMesh m,Design d)
    {
        float f=d.Length*.5f,b=-f,w=d.Width*.5f,h=d.Height;
        // Windscreen follows the sloped nose. Opaque dark quads conceal the otherwise unmodelled interior.
        float cabTop=h>2.5f?2.2f:h-.15f;
        float lowerY=1.34f,upperY=cabTop-.07f;
        float WindZ(float y)=>f-.23f-(y-1.26f)/(cabTop-1.26f)*.89f+.012f;
        float lowerZ=WindZ(lowerY),upperZ=WindZ(upperY);
        m.Quad(new Vector3(-w+.13f,lowerY,lowerZ),new Vector3(w-.13f,lowerY,lowerZ),new Vector3(w-.18f,upperY,upperZ),new Vector3(-w+.18f,upperY,upperZ),Glass);
        foreach(float side in new[]{-1f,1f})
        {
            float x=side*(w+.003f);float rear=f-1.52f;
            m.Quad(new Vector3(x,1.29f,f-.45f),new Vector3(x,cabTop-.08f,f-1.17f),new Vector3(x,cabTop-.08f,rear),new Vector3(x,1.29f,rear),Glass);
            // Dark fine seams, pressed cargo panel and small handles instead of extra windows.
            m.Box(new Vector3(x,1.14f,rear-.045f),new Vector3(.012f,.11f,.14f),Trim);
            m.Box(new Vector3(x,.94f,rear-.15f),new Vector3(.012f,1.08f,.012f),Paint);
            m.Box(new Vector3(x,.47f,(b+rear)*.5f),new Vector3(.015f,.026f,rear-b-.18f),Paint);
            foreach(float y in new[]{1.30f,h-.27f})m.Box(new Vector3(x,y,(b+rear)*.5f),new Vector3(.014f,.022f,rear-b-.30f),Paint);
            m.Box(new Vector3(x,(h+1.04f)*.5f,b+.18f),new Vector3(.014f,h-1.54f,.023f),Paint);
            m.Box(new Vector3(side*(w-.025f),1.05f,b-.014f),new Vector3(.07f,.56f,.03f),Tail);
            foreach(float axle in d.Axles)Arch(m,side*w,axle,d.Radius+.10f,.055f,Paint);
        }
        m.Box(new Vector3(0,1.22f,b-.008f),new Vector3(.016f,1.34f,.012f),Metal);
        m.Box(new Vector3(.10f,1.05f,b-.02f),new Vector3(.16f,.07f,.027f),Trim);
        if(d.Height>2.5f)foreach(float x in new[]{-.58f,0,.58f})m.Box(new Vector3(x,h+.003f,-.4f),new Vector3(.035f,.018f,d.Length-2),Paint);
    }
    private static void TruckDetails(LowPolyPropMesh m,LowPolyPropMesh box,Design d,float cabinRear)
    {
        float f=d.Length*.5f,b=-f,w=d.Width*.5f,h=d.Height,cabH=d.CabHeight;
        // Narrow front chamfer ends above the wrap-around bumper; a tall cab-over windscreen sits flush on the sloping face.
        float WindZ(float y)=>f-(y-1.44f)/((cabH-.18f)-1.44f)*.24f+.012f;
        float lo=1.56f,hi=cabH-.30f;
        m.Quad(new Vector3(-w+.18f,lo,WindZ(lo)),new Vector3(w-.18f,lo,WindZ(lo)),new Vector3(w-.22f,hi,WindZ(hi)),new Vector3(-w+.22f,hi,WindZ(hi)),Glass);
        foreach(float side in new[]{-1f,1f})
        {
            float x=side*(w-.046f);
            m.Quad(new Vector3(x,1.52f,cabinRear+.20f),new Vector3(x,hi,cabinRear+.20f),new Vector3(x,hi,f-.46f),new Vector3(x,1.52f,f-.27f),Glass);
            m.Box(new Vector3(x,1.23f,cabinRear+.30f),new Vector3(.017f,.10f,.16f),Trim);
            m.Box(new Vector3(x,1.20f,cabinRear+.12f),new Vector3(.013f,1.10f,.013f),Metal);
            m.BeveledBox(new Vector3(side*(w-.05f),.55f,cabinRear+.28f),new Vector3(.16f,.13f,.50f),.015f,Metal);
            Arch(m,side*(w-.04f),d.Axles[0],d.Radius+.12f,.10f,Trim);
        }
        float start=cabinRear-.09f,end=b+.04f,depth=start-end,center=(start+end)*.5f;
        box.BeveledBox(new Vector3(0,(h+1.11f)*.5f,center),new Vector3(d.Width,h-1.11f,depth),.055f,Paint);
        foreach(float side in new[]{-1f,1f})
        {
            float x=side*(w+.012f);
            foreach(float y in new[]{1.13f,h-.02f})box.BeveledBox(new Vector3(x,y,center),new Vector3(.065f,.095f,depth+.025f),.012f,Metal);
            foreach(float z in new[]{start,end})box.BeveledBox(new Vector3(x,(h+1.11f)*.5f,z),new Vector3(.065f,h-1.11f,.07f),.009f,Metal);
            foreach(float z in new[]{start-.20f,end+.20f,center})m.Box(new Vector3(x,.89f,z),new Vector3(.06f,.07f,.15f),Lamp);
            // Open side-impact guard under the freight body.
            float rearFront=d.Axles[1]+d.Radius+.18f,frontRear=cabinRear-.18f;
            foreach(float y in new[]{.51f,.77f})m.BeveledBox(new Vector3(x,y,(rearFront+frontRear)*.5f),new Vector3(.07f,.07f,frontRear-rearFront),.01f,Metal);
            foreach(float z in new[]{rearFront,frontRear})m.Box(new Vector3(x,.65f,z),new Vector3(.07f,.31f,.07f),Metal);
            if(d.Curtain)
            {
                for(int i=0;i<6;i++)
                {
                    float z=Mathf.Lerp(end+.18f,start-.18f,i/5f);
                    box.Box(new Vector3(x,2.35f,z),new Vector3(.018f,1.98f,.065f),Paint);
                    box.Box(new Vector3(x+side*.014f,1.44f,z),new Vector3(.025f,.55f,.06f),Trim);
                    box.BeveledBox(new Vector3(x+side*.029f,1.61f,z),new Vector3(.025f,.11f,.085f),.007f,Metal);
                }
            }
            foreach(float axle in d.Axles.Skip(1))Arch(m,side*w,axle,d.Radius+.10f,.07f,Trim);
        }
        // Rear double doors, hinges and locking bars read from behind when driving.
        box.Box(new Vector3(0,(h+1.15f)*.5f,end-.014f),new Vector3(.022f,h-1.32f,.022f),Metal);
        foreach(float x in new[]{-.38f,.38f})
        {
            box.Box(new Vector3(x,(h+1.15f)*.5f,end-.035f),new Vector3(.028f,h-1.38f,.035f),Metal);
            box.Box(new Vector3(x,1.45f,end-.055f),new Vector3(.17f,.05f,.045f),Trim);
        }
        if(d.Curtain)m.BeveledBox(new Vector3(0,cabH+.06f,(f+cabinRear)*.5f),new Vector3(d.Width-.31f,.24f,1.18f),.06f,Paint);
    }
    private static void Arch(LowPolyPropMesh m,float x,float z,float radius,float thickness,int material)
    {
        for(int i=0;i<10;i++)
        {
            Vector3 P(int step,float r){float a=step*Mathf.PI/10;return new Vector3(x, radius-.10f+Mathf.Sin(a)*r,z+Mathf.Cos(a)*r);}
            m.Quad(P(i,radius),P(i+1,radius),P(i+1,radius+thickness),P(i,radius+thickness),material);
        }
    }
    private static Mesh WheelMesh(Design d)
    {
        var m=new LowPolyPropMesh(Root+"/Meshes");float r=d.Radius,width=d.Van?.24f:.29f;
        // Four bevel rings on a 20-sided tire, inset discs, raised cap and five bolt heads.
        float[] xs={-width*.5f,-width*.37f,width*.37f,width*.5f};float[] rs={r*.88f,r,r,r*.88f};
        Vector3 P(float x,float radius,float a)=>new Vector3(x,Mathf.Sin(a)*radius,Mathf.Cos(a)*radius);
        for(int i=0;i<20;i++)
        {
            float a=i*Mathf.PI*2/20,b=(i+1)*Mathf.PI*2/20;
            for(int ring=0;ring<3;ring++)m.Quad(P(xs[ring],rs[ring],a),P(xs[ring+1],rs[ring+1],a),P(xs[ring+1],rs[ring+1],b),P(xs[ring],rs[ring],b),Trim);
            foreach(float side in new[]{-1f,1f})
            {
                float x=side*width*.50f;
                m.Quad(P(x,r*.88f,a),P(x,r*.88f,b),P(x,r*.62f,b),P(x,r*.62f,a),Trim);
                m.Triangle(new Vector3(x,0,0),P(x,r*.62f,a),P(x,r*.62f,b),Metal);
                m.Quad(P(x+side*.012f,r*.30f,a),P(x+side*.012f,r*.30f,b),P(x,r*.40f,b),P(x,r*.40f,a),Metal);
                m.Triangle(new Vector3(x+side*.012f,0,0),P(x+side*.012f,r*.30f,a),P(x+side*.012f,r*.30f,b),Metal);
            }
        }
        foreach(float side in new[]{-1f,1f})for(int i=0;i<5;i++)
        {
            float a=i*Mathf.PI*2/5;
            m.BeveledBox(new Vector3(side*(width*.5f+.006f),Mathf.Sin(a)*r*.47f,Mathf.Cos(a)*r*.47f),new Vector3(.018f,.037f,.037f),.005f,Trim);
        }
        return m.Save(d.Id+"_wheel");
    }
    private static void AddMesh(Transform parent,string name,Mesh mesh,Material[] materials)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=materials;
    }
    internal static void Gallery()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.66f,.68f,.65f);
        var sun=new GameObject("Studio daylight").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.15f;sun.transform.rotation=Quaternion.Euler(45,-35,0);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(24,-.075f,0);floor.transform.localScale=new Vector3(160,.15f,160);
        floor.GetComponent<Renderer>().sharedMaterial=Material("ReviewFloor",new Color(.73f,.72f,.67f));
        for(int i=0;i<Designs.Length;i++)
        {
            var d=Designs[i];var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+d.Id+".prefab"));
            instance.transform.position=new Vector3(i*12,0,0);var target=instance.transform.position+Vector3.up*d.Height*.48f;
            TownFloristBatch.Render(scene,Pictures+"/"+d.Id+".png",target+new Vector3(6,3.3f,7),target,Mathf.Max(2.1f,d.Length*.41f));
        }
        var lineup=Object.FindObjectsOfType<PlayerVehicleModel>().OrderBy(model=>model.transform.position.x).ToArray();
        for(int i=0;i<lineup.Length;i++)lineup[i].transform.position=new Vector3(i*3.9f,0,0);
        TownFloristBatch.Render(scene,Pictures+"/Fleet.png",new Vector3(20,10,23),new Vector3(8,1,0),9);
    }
}
