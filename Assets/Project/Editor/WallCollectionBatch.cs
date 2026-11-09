using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

internal static class WallCollectionBatch
{
    internal const string Root="Assets/Art/WallCollection";
    internal const string Catalog="Assets/Resources/WallCollection";
    internal const string Pictures="Docs/WallCollection";
    private const string ScenePath=Root+"/WallCollectionShowcase.unity";

    public static void Run()
    {
        int exit=0;
        try { TownWorldSafety.RequireIsolatedEditor(); Generate(); }
        catch(Exception error){Debug.LogException(error);exit=1;}
        EditorApplication.Exit(exit);
    }

    public static void RunOpenings()
    {
        try
        {
            TownWorldSafety.RequireIsolatedEditor();
            BuildOpenings(AssetDatabase.LoadAssetAtPath<Material>(Root+"/Wallfinishes.mat"));
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }

    [MenuItem("Retail Empire/Art/Rebuild wall collection")]
    private static void Generate()
    {
        foreach(string path in new[]{Root+"/Meshes",Root+"/Prefabs",Root+"/Icons",Root+"/Showcase",Catalog,Pictures})
            Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
        var baseline=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Structures/Wall.asset");
        var oldSize=baseline.placementBounds.size;
        var size=new Vector3(oldSize.x*2,oldSize.y,oldSize.x);
        var atlas=BakeAtlas(size.y);
        var opaque=Material("Wall finishes",atlas,.12f);
        var ceramic=Material("Satin ceramic",atlas,.28f);
        var transparent=Material("Wall glazing",atlas,.32f,true);
        var prefabs=new List<GameObject>();
        var audit=new List<string>{"Style,Price,Available level,Triangles,Collider height (m)"};
        for(int i=0;i<WallCollectionFinishes.Styles.Length;i++)
        {
            var finish=WallCollectionFinishes.Styles[i];
            string name=$"Wall_{i+1:00}_{finish.English.Replace(" ","")}";
            string id=$"wall_finish_{i+1:00}";
            var moduleSize=size;
            var (body,glass)=WallCollectionGeometry.Create(i,moduleSize);
            var jamb=WallCollectionGeometry.HasJambs(i)?WallCollectionGeometry.CreateJamb(i,moduleSize):null;
            int triangles=(body.triangles.Length+(glass!=null?glass.triangles.Length:0)+(jamb!=null?2*jamb.triangles.Length:0))/3;
            bool relief=i==1||i==2||i==3||i==4||i==5||i==15||i==16||i==17||i==20||i>=24;
            if(triangles>(relief?500:100))throw new InvalidOperationException($"{name} exceeds its triangle budget ({triangles}).");
            var prefab=Prefab(name,i,moduleSize,body,glass,jamb,i>=6&&i<=10?ceramic:opaque,transparent);
            prefabs.Add(prefab);
            var icon=ShopUiPreviews.Render(prefab,id,14,outputFolder:Root+"/Icons",
                prepare:i==0?null:model=>PopulateStrip(prefab,model,size.x));
            var item=Object.Instantiate(baseline);
            item.name=id;item.id=id;item.displayName=finish.English;item.description=finish.Description;
            item.russianName=finish.Russian;item.russianDescription=finish.RussianDescription;item.price=finish.Price;
            item.recommendedDoorFinish=DoorFrameFinishSetup.Recommended(i+1);
            item.prefab=prefab;item.icon=icon;
            item.wallGeometryVersion=BuildItemData.TwoCellWallVersion;
            item.footprint=new Vector2Int(2,1);
            var legacySize=oldSize;
            if(i==0)legacySize.z=baseline.previousWallBounds.size.z;
            item.previousWallPrefab=PreviousWall(name,i,legacySize,i>=6&&i<=10?ceramic:opaque,transparent);
            if(i==29)legacySize.y*=.73f;
            item.previousWallBounds=new Bounds(Vector3.up*(legacySize.y*.5f),legacySize);
            if(i==29)moduleSize.y*=.73f;
            item.placementBounds=new Bounds(Vector3.up*(moduleSize.y*.5f),moduleSize);
            item.unlockedByDefault=true;item.unlockCondition=null;item.hiddenFromShop=false;
            TownBakeryPlacement.SaveAsset(item,Catalog+"/"+id+".asset");
            audit.Add(FormattableString.Invariant($"{finish.English},{finish.Price},1,{triangles},{(i==29?size.y*.5f:size.y):0.###}"));
        }
        var corner=CreateStripeCorner(size,baseline.previousWallBounds.size.z,opaque,transparent);
        BuildOpenings(opaque);
        AssetDatabase.SaveAssets();
        CreateShowcase(prefabs,size,corner);
        File.WriteAllLines(Pictures+"/WallCatalog.csv",audit);
        Debug.Log("Wall Collection: 30 catalog items, icons and prefabs generated. Triangle budgets passed.");
    }

    private static void BuildOpenings(Material finish)
    {
        if(finish==null)throw new InvalidOperationException("The wall finish material must exist before rebuilding openings.");
        foreach(string path in new[]{"Assets/Prefabs/Structures/Door.asset","Assets/Prefabs/City/unloading_gate_01.asset"})
        {
            var item=AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
            var oldPrefab=item.HasVersionedOpening?item.previousWallPrefab:item.prefab;
            var oldBounds=item.HasVersionedOpening?item.previousWallBounds:item.placementBounds;
            var root=(GameObject)PrefabUtility.InstantiatePrefab(oldPrefab);
            try
            {
                PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                string name=item.isUnloadingGate?"Wall_FittedUnloadingGate":"Wall_FittedDoor";
                root.name=name;
                float cell=ShopProportions.CellSize;
                float depth=cell/root.transform.localScale.z;
                float halfWidth=item.footprint.x*cell*.5f/root.transform.localScale.x;
                float top=oldBounds.size.y/root.transform.localScale.y;
                const float jambWidth=.08f,headerHeight=.09f;
                Trim(root.transform.Find("ContinuousTopBand"),0,halfWidth*2,top-headerHeight,top,depth,1);
                foreach(int side in new[]{-1,1})
                {
                    float x=side*(halfWidth-jambWidth*.5f);
                    Trim(root.transform.Find(side<0?"LeftJamb":"RightJamb"),x,jambWidth,0,top-headerHeight,depth,1);
                    var box=root.GetComponents<BoxCollider>().Single(collider=>Mathf.Sign(collider.center.x)==side && Mathf.Abs(collider.center.x)>.01f);
                    box.center=new Vector3(x,(top-headerHeight)*.5f,0);
                    box.size=new Vector3(jambWidth,top-headerHeight,depth);
                }

                if(item.isUnloadingGate)
                {
                    float shutterScale=(depth*.42f)/.38f;
                    root.transform.Find("Shutter").localScale=new Vector3(1,1,shutterScale);
                    root.transform.Find("ShutterSlats").localScale=new Vector3(1,1,shutterScale);
                    var shutter=root.GetComponents<BoxCollider>().Single(collider=>Mathf.Abs(collider.center.x)<.01f);
                    shutter.size=new Vector3(shutter.size.x,shutter.size.y,depth*.42f);
                }
                else
                {
                    foreach(int side in new[]{-1,1})
                    {
                        var hinge=root.transform.Find(side<0?"door-left":"door-right");
                        var panel=new GameObject(side<0?"LeftKickPlate":"RightKickPlate",typeof(MeshFilter),typeof(MeshRenderer));
                        panel.transform.SetParent(hinge,false);
                        Trim(panel.transform,-side*.33f,.65f,.018f,.17f,.068f,2);
                    }
                }
                DoorFrameFinishSetup.BindLegacy(root, DoorFrameFinishSetup.CreatePalette());
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+name+".prefab");
                item.previousWallPrefab=oldPrefab;item.previousWallBounds=oldBounds;
                item.prefab=prefab;item.wallGeometryVersion=BuildItemData.TwoCellWallVersion;
                item.footprint=new Vector2Int(item.footprint.x,1);
                item.placementBounds=new Bounds(oldBounds.center,new Vector3(item.footprint.x*cell,oldBounds.size.y,cell));
                item.icon=ShopUiPreviews.Render(prefab,item.id,14,outputFolder:Root+"/Icons");
                EditorUtility.SetDirty(item);

                void Trim(Transform part,float center,float width,float bottom,float end,float thickness,int swatch)
                {
                    var mesh=WallCollectionGeometry.OpeningTrim(part.name,center,width,bottom,end,thickness,swatch);
                    part.GetComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,Root+"/Meshes/"+name+"_"+part.name+".asset");
                    part.GetComponent<MeshRenderer>().sharedMaterial=finish;
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }

    private static GameObject PreviousWall(string name,int style,Vector3 size,Material finish,Material glazing,bool corner=false)
    {
        name+="_V3";
        var previous=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+name+".prefab");
        if(previous!=null)return previous;
        var (body,glass)=corner?(WallCollectionGeometry.StripeCorner(size.x,size.y,size.z),(Mesh)null):WallCollectionGeometry.Create(style,size);
        var jamb=WallCollectionGeometry.HasJambs(style)?WallCollectionGeometry.CreateJamb(style,size):null;
        return Prefab(name,style,size,body,glass,jamb,finish,glazing,corner,legacyCorner:corner);
    }

    private static GameObject CreateStripeCorner(Vector3 size,float depth,Material finish,Material glazing)
    {
        const string name="Wall_StoreStripeCorner";
        var legacy=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Structures/Wall_Corner.asset");
        var cornerSize=new Vector3(size.z,size.y,size.z);
        var prefab=Prefab(name,0,cornerSize,WallCollectionGeometry.Create(0,cornerSize).solid,null,null,finish,glazing,corner:true);
        var item=Object.Instantiate(legacy);
        item.name=item.id=BuildItemData.StoreStripeCornerId;
        item.recommendedDoorFinish=DoorFinish.DarkGreen;
        item.displayName="Store Stripe Corner";item.russianName="Угол с фирменной полосой";
        item.description="A single-cell corner for Store Stripe walls.";
        item.russianDescription="Угол в одну клетку для стен с фирменной полосой.";
        item.prefab=prefab;item.wallGeometryVersion=BuildItemData.TwoCellWallVersion;
        item.footprint=Vector2Int.one;
        item.placementBounds=new Bounds(Vector3.up*(size.y*.5f),cornerSize);
        item.previousWallPrefab=PreviousWall(name,0,new Vector3(size.z,size.y,depth),finish,glazing,corner:true);
        item.previousWallBounds=legacy.placementBounds;
        item.hiddenFromShop=false;item.price=105;
        item.icon=ShopUiPreviews.Render(prefab,item.id,14,outputFolder:Root+"/Icons");
        TownBakeryPlacement.SaveAsset(item,Catalog+"/"+item.id+".asset");
        return prefab;
    }

    private static Texture2D BakeAtlas(float height)
    {
        string path=Root+"/FinishAtlas.png";
        var image=WallCollectionFinishes.Bake(height);
        try { File.WriteAllBytes(path,image.EncodeToPNG()); }
        finally { Object.DestroyImmediate(image); }
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency=false;
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.mipmapEnabled=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=2048;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material Material(string name,Texture2D atlas,float smoothness,bool glass=false)
    {
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
        material.SetTexture("_BaseMap",atlas);
        material.SetFloat("_Smoothness",smoothness);
        if(glass)
        {
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_Cull",(float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");
            material.renderQueue=(int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster",false);
        }
        string path=Root+"/"+name.Replace(" ","")+".mat";
        TownBakeryPlacement.SaveAsset(material,path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static GameObject Prefab(string name,int style,Vector3 size,Mesh body,Mesh glass,Mesh jamb,Material finish,Material glazing,bool corner=false,bool legacyCorner=false)
    {
        body=SaveMesh(body,Root+"/Meshes/"+name+".asset");
        if(glass!=null)glass=SaveMesh(glass,Root+"/Meshes/"+name+"_Glass.asset");
        if(jamb!=null)jamb=SaveMesh(jamb,Root+"/Meshes/"+name+"_Jamb.asset");
        var root=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider),
            typeof(StoreWallOccluder),typeof(WallPattern));
        try
        {
            root.isStatic=true;
            root.GetComponent<MeshFilter>().sharedMesh=body;
            root.GetComponent<MeshRenderer>().sharedMaterial=finish;
            if(glass!=null)
            {
                var pane=new GameObject("Glazing",typeof(MeshFilter),typeof(MeshRenderer));
                pane.transform.SetParent(root.transform,false);pane.isStatic=true;
                pane.GetComponent<MeshFilter>().sharedMesh=glass;
                var renderer=pane.GetComponent<MeshRenderer>();
                renderer.sharedMaterial=glazing;renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            var collider=root.GetComponent<BoxCollider>();
            collider.size=new Vector3(size.x,style==29?size.y*.5f:size.y,size.z);
            collider.center=Vector3.up*(collider.size.y*.5f);
            if(legacyCorner)
            {
                float halfDepth=size.z*.5f;
                collider.size=new Vector3(size.x+halfDepth,size.y,size.z);
                collider.center=new Vector3((size.x-halfDepth)*.5f,size.y*.5f,0);
                var other=root.AddComponent<BoxCollider>();
                other.size=new Vector3(size.z,size.y,size.x-halfDepth);
                other.center=new Vector3(0,size.y*.5f,(size.x+halfDepth)*.5f);
            }
            var pattern=root.GetComponent<WallPattern>();
            pattern.Configure(body,glass,!corner && glass==null && style!=28);
            if(jamb!=null)
                pattern.ConfigureJambs(AddJamb("Left jamb",-1),AddJamb("Right jamb",1));
            MeshRenderer AddJamb(string label,int side)
            {
                var post=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));
                post.transform.SetParent(root.transform,false);
                post.transform.localPosition=Vector3.right*(side*(size.x*.5f-.012f));
                post.isStatic=true;
                post.GetComponent<MeshFilter>().sharedMesh=jamb;
                var renderer=post.GetComponent<MeshRenderer>();renderer.sharedMaterial=finish;return renderer;
            }
            return PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+name+".prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Mesh SaveMesh(Mesh mesh,string path)
    {
        TownBakeryPlacement.SaveAsset(mesh,path);
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    private static void PopulateStrip(GameObject prefab,GameObject first,float width)
    {
        first.transform.localPosition=Vector3.left*(width*1.5f);
        first.GetComponent<WallPattern>().Align();
        first.GetComponent<WallPattern>().SetJambs(true,false);
        for(int i=1;i<4;i++)
        {
            var copy=Object.Instantiate(prefab,first.transform);
            copy.transform.localPosition=Vector3.right*(i*width);
            var pattern=copy.GetComponent<WallPattern>();
            pattern.Align();
            pattern.SetJambs(i==2,i==1||i==3);
        }
    }

    private static void CreateShowcase(IReadOnlyList<GameObject> prefabs,Vector3 size,GameObject stripeCorner)
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.55f,.51f,.45f);
        Light("Warm key",new Vector3(42,-32,0),1.25f);
        Light("Soft fill",new Vector3(23,148,0),.62f);
        var collection=new GameObject("Wall collection — four real modules per finish");
        for(int i=0;i<prefabs.Count;i++)
        {
            var group=DemoStrip(prefabs[i],size.x,collection.transform);
            group.position=new Vector3((i%6-2.5f)*2.75f,(2-i/6)*2.25f,0);
            Label(group,$"{i+1:00}  {WallCollectionFinishes.Styles[i].English}",new Vector3(0,-.25f,-.33f),.027f);
        }
        Label(collection.transform,"RETAIL EMPIRE TYCOON  /  WALL COLLECTION",new Vector3(0,6.61f,-.33f),.051f);
        var junctions=new GameObject("Corner, T, cross, door, shopfront and unloading gate");
        junctions.transform.position=new Vector3(22,0,0);
        CreateJunctions(junctions.transform,prefabs,size,stripeCorner);
        var camera=new GameObject("Collection camera",typeof(Camera)).GetComponent<Camera>();
        camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.92f,.885f,.81f);camera.nearClipPlane=.05f;camera.farClipPlane=90;
        PoseCamera(camera,new Vector3(0,1.02f,0),6.28f,new Vector3(-1.6f,2.6f,-24));
        if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new InvalidOperationException("Could not save the wall showcase.");
        Capture(camera,"WallCollection",1920,1360);
        PoseCamera(camera,new Vector3(5.5f,5.32f,0),1.75f,new Vector3(-1,1.6f,-8));
        Capture(camera,"OakAndPlaster",1400,850);
        PoseCamera(camera,new Vector3(0,1,0),1.85f,new Vector3(-1.1f,1.4f,-8));
        Capture(camera,"BrickAndStone",1600,850);
        PoseCamera(camera,new Vector3(-1.375f,3.1f,0),1.55f,new Vector3(-1,1.5f,-8));
        Capture(camera,"CeramicTile",1600,850);
        PoseCamera(camera,new Vector3(-4.12f,-3.7f,0),2.45f,new Vector3(-1.1f,1.5f,-8));
        Capture(camera,"GlassAndShopfront",1600,850);
        PoseCamera(camera,junctions.transform.position+new Vector3(.65f,.64f,0),2.8f,new Vector3(-3,4,-9));
        Capture(camera,"WallJunctions",1550,900);
        AssetDatabase.SaveAssets();
    }

    private static Transform DemoStrip(GameObject prefab,float width,Transform parent)
    {
        var temp=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PopulateStrip(prefab,temp,width);
        var group=new GameObject(prefab.name).transform;group.SetParent(parent,false);
        foreach(var materialGroup in temp.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<MeshRenderer>().enabled)
            .GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var source=materialGroup.First().GetComponent<MeshRenderer>();
            var mesh=new Mesh{name=prefab.name+" preview"};
            mesh.CombineMeshes(materialGroup.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray());
            var child=new GameObject(materialGroup.Key.name,typeof(MeshFilter),typeof(MeshRenderer));
            child.transform.SetParent(group,false);
            child.GetComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,Root+"/Showcase/"+prefab.name+"_"+source.shadowCastingMode+".asset");
            var renderer=child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=materialGroup.Key;renderer.shadowCastingMode=source.shadowCastingMode;
        }
        Object.DestroyImmediate(temp);
        return group;
    }

    private static void CreateJunctions(Transform root,IReadOnlyList<GameObject> walls,Vector3 size,GameObject stripeCorner)
    {
        var bakedJunctionMeshes=new Dictionary<string,Mesh>();
        var corner=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Structures/Wall_Corner.asset").prefab;
        var door=AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/City/door_01.asset");
        if(door==null)door=AssetDatabase.FindAssets("t:BuildItemData").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildItemData>).First(item=>item.id=="door_01");
        var gate=AssetDatabase.FindAssets("t:BuildItemData").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildItemData>).First(item=>item.id=="unloading_gate_01");
        float w=size.x;
        var crossing=new Vector3(-2.75f,0,.8f);
        Place(corner,crossing,0);
        for(int i=0;i<3;i++)
        {
            float offset=(i+1)*w;
            Place(walls[12],crossing+Vector3.right*offset,0);
            Place(walls[12],crossing+Vector3.forward*offset,90);
            Place(walls[1],crossing+Vector3.left*offset,0);
            Place(walls[1],crossing+Vector3.back*offset,90);
        }
        var tee=new Vector3(-2.75f,0,-.95f);
        for(int i=-1;i<=1;i++)Place(walls[7],tee+Vector3.right*(i*w),0);
        for(int i=0;i<2;i++)Place(walls[7],tee+Vector3.back*((i+.75f)*w),90);
        Place(door.prefab,new Vector3(.12f,0,0),0);
        for(int i=0;i<2;i++)
        {
            Place(walls[2],new Vector3(.12f-door.placementBounds.extents.x-(i+.5f)*w,0,0),0);
            Place(walls[26],new Vector3(.12f+door.placementBounds.extents.x+(i+.5f)*w,0,0),0);
        }
        Place(gate.prefab,new Vector3(3.30f,0,0),0);
        for(int i=0;i<2;i++)Place(walls[20],new Vector3(3.30f+gate.placementBounds.extents.x+(i+.5f)*w,0,0),0);
        var elbow=new Vector3(.12f,0,1.8f);
        Place(stripeCorner,elbow,0);
        for(int i=0;i<3;i++)
        {
            float offset=(i+.75f)*w;
            Place(walls[0],elbow+Vector3.right*offset,0);
            Place(walls[0],elbow+Vector3.forward*offset,90);
        }

        void Place(GameObject prefab,Vector3 p,float yaw)
        {
            var module=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);
            module.transform.localPosition=p;module.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var pattern=module.GetComponent<WallPattern>();
            if(pattern==null)return;
            pattern.Align();
            foreach(var filter in module.GetComponentsInChildren<MeshFilter>())
            {
                var variant=filter.sharedMesh;
                string file=Root+"/Showcase/"+prefab.name+"_"+variant.name.Replace(" ","_")+".asset";
                if(!bakedJunctionMeshes.TryGetValue(file,out var baked))
                {
                    var copy=Object.Instantiate(variant);copy.hideFlags=HideFlags.None;
                    baked=SaveMesh(copy,file);
                    bakedJunctionMeshes.Add(file,baked);
                }
                filter.sharedMesh=baked;
            }
        }
    }

    private static void Light(string name,Vector3 euler,float intensity)
    {
        var light=new GameObject(name,typeof(Light)).GetComponent<Light>();
        light.type=LightType.Directional;light.intensity=intensity;light.transform.eulerAngles=euler;
    }
    private static void Label(Transform parent,string caption,Vector3 position,float size)
    {
        var text=new GameObject(caption,typeof(TextMesh)).GetComponent<TextMesh>();
        text.transform.SetParent(parent,false);text.transform.localPosition=position;
        text.text=caption;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
        text.characterSize=size;text.fontSize=60;text.color=new Color(.14f,.24f,.18f);
    }
    private static void PoseCamera(Camera camera,Vector3 focus,float size,Vector3 offset)
    {
        camera.transform.position=focus+offset;camera.transform.LookAt(focus);camera.orthographicSize=size;
    }
    private static void Capture(Camera camera,string name,int width,int height)
    {
        var old=RenderTexture.active;
        var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
        var png=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            target.Create();camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            png.ReadPixels(new Rect(0,0,width,height),0,0);png.Apply();
            File.WriteAllBytes(Pictures+"/"+name+".png",png.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=old;target.Release();
            Object.DestroyImmediate(target);Object.DestroyImmediate(png);
        }
    }
}
