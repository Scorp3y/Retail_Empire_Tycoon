using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using RetailEmpireTycoon.Editor.TownKit;
using RetailEmpireTycoon.SaveSystem;
using RetailEmpireTycoon.Territory;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Scoped, repeatable authoring of the store site. Never rebuilds the shared town.</summary>
internal sealed class TownForgottenYardAuthoring
{
    internal const string Root="Assets/Art/TownForgottenYard";
    internal const string RulesPath=Root+"/Land24.asset";
    internal const string LayoutPath="Assets/Prefabs/TownWorld/TownLayout.asset";
    private readonly Dictionary<string,Mesh> meshes=new();
    private Material surface;
    private TerritorySubdivisionRules rules;

    internal void Build()
    {
        foreach(string dir in new[]{"Meshes","Materials","Prefabs","Barriers"})Directory.CreateDirectory(Root+"/"+dir);
        AssetDatabase.Refresh();
        var land=Object.FindObjectOfType<TerritoryPlotLayout>(true);
        rules=CreateRules(land);
        BuildModels();
        ConfigureZones(land);
        ConfigureStarter(land);
        ReplaceScenery();
        // All modular stages use the same shared parcel-controlled scenery. Legacy non-modular
        // stage prefabs stay untouched, including their original store shells.
        var empty=new GameObject("Shared yard scenery lives in TownEnvironment");
        var emptyPrefab=PrefabUtility.SaveAsPrefabAsset(empty,Root+"/Prefabs/ModularLandscape.prefab");Object.DestroyImmediate(empty);
        var spawner=new SerializedObject(Object.FindObjectOfType<StorePrefabSpawner>(true));
        var stages=spawner.FindProperty("modularPrefabs");
        for(int i=0;i<stages.arraySize;i++)stages.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue=emptyPrefab;
        spawner.ApplyModifiedPropertiesWithoutUndo();
        var town=Object.FindObjectsOfType<TownExpansionScenery>(true).Single();
        var scenerySettings=new SerializedObject(town);
        scenerySettings.FindProperty("progression").objectReferenceValue=land.progression;
        scenerySettings.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(town);town.Refresh();
        AssetDatabase.SaveAssets();
    }

    private static TerritorySubdivisionRules CreateRules(TerritoryPlotLayout land)
    {
        var rules=AssetDatabase.LoadAssetAtPath<TerritorySubdivisionRules>(RulesPath);
        if(rules==null){rules=ScriptableObject.CreateInstance<TerritorySubdivisionRules>();AssetDatabase.CreateAsset(rules,RulesPath);}
        // Re-authoring must keep stable IDs and prices; use the original nine-region bounds.
        var regions=land.plots.GroupBy(p=>(int)p.id<9?p.id:rules.Parcels.Single(q=>q.Id==p.id).LegacyRegion)
            .OrderBy(g=>(int)g.Key).ToArray();
        var prices=new[]{1000,2000,3000,5000,4000,6500,8000,9500,11000};
        var counts=new[]{3,2,2,3,3,3,3,2,3};
        var splitX=new[]{true,false,false,false,true,true,false,true,true};
        var parcels=new List<TerritorySubdivisionRules.Parcel>();int id=100;
        foreach(var region in regions)
        {
            int i=(int)region.Key;float x=region.Min(p=>p.bounds.xMin),z=region.Min(p=>p.bounds.yMin);
            var rect=Rect.MinMaxRect(x,z,region.Max(p=>p.bounds.xMax),region.Max(p=>p.bounds.yMax));
            for(int j=0;j<counts[i];j++)
            {
                var part=splitX[i]?new Rect(rect.x+j*rect.width/counts[i],rect.y,rect.width/counts[i],rect.height)
                    :new Rect(rect.x,rect.y+j*rect.height/counts[i],rect.width,rect.height/counts[i]);
                int price=prices[i]/counts[i]+(j<prices[i]%counts[i]?1:0);
                parcels.Add(new TerritorySubdivisionRules.Parcel((TerritoryId)id++,region.Key,part,price));
            }
        }
        rules.Configure(land.initialBounds,parcels);EditorUtility.SetDirty(rules);return rules;
    }

    private void ConfigureZones(TerritoryPlotLayout land)
    {
        var zones=Object.FindObjectsOfType<TerritoryZone>(true).OrderBy(z=>(int)z.Id).ToList();
        while(zones.Count<rules.Parcels.Count)zones.Add(new GameObject("Territory parcel").AddComponent<TerritoryZone>());
        var materials=new[]{"Concrete","WarningYellow","WarningBlack","SignCream"}
            .Select(id=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Territory/"+id+".mat")).ToArray();
        if(materials.Any(m=>m==null))throw new InvalidOperationException("Existing territory border materials missing.");
        land.plots.Clear();
        for(int i=0;i<rules.Parcels.Count;i++)
        {
            var p=rules.Parcels[i];var zone=zones[i];zone.Id=p.Id;zone.Price=p.Price;zone.name="Territory "+p.Id;
            StoreUpgradeSetup.ConfigureZone(zone,p.Bounds);
            TerritoryBarrierSetup.Build(zone,materials,Root+"/Barriers",true);
            zone.Bind(land.progression);
            land.plots.Add(new TerritoryPlotLayout.Plot{id=p.Id,bounds=p.Bounds});
        }
        land.layoutVersion=TerritorySubdivisionRules.CurrentVersion;
        ShopUiSetup.Set(land.progression,"landRules",rules);
        var mode=new SerializedObject(Object.FindObjectOfType<TerritoryPurchaseModeManager>(true));var list=mode.FindProperty("_zones");
        list.arraySize=zones.Count;for(int i=0;i<zones.Count;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=zones[i];
        mode.ApplyModifiedPropertiesWithoutUndo();land.Apply();
    }

    private static void ConfigureStarter(TerritoryPlotLayout land)
    {
        var starter=AssetDatabase.LoadAssetAtPath<StarterStoreBlueprint>(StarterStoreSetup.BlueprintPath);
        var state=new GameData{usesModularStore=true};var grid=land.grid;
        var start=grid.WorldToCell(new Vector3(land.initialBounds.x+.001f,0,land.initialBounds.y+.001f));
        void Add(string id,int x,int z,int facing=0,string product=null,int amount=0)
        {
            var p=new PlacedBuildSaveData{itemId=id,x=start.x+x,z=start.z+z,facing=facing,rotated=facing%2!=0,wallModuleVersion=BuildItemData.CurrentWallModuleVersion};
            state.placedObjects.Add(p);
            if(product!=null)state.shelfStocks.Add(new ShelfStockSaveEntry{buildItemId=id,anchorX=p.x,anchorZ=p.z,facing=facing,rotated=p.rotated,productId=product,amount=amount});
        }
        for(int x=0;x<36;x++)for(int z=0;z<30;z++)
        {
            string style=x<12?"floor_concrete":"floor_cream";
            if(x>=14&&x<22&&z>=15&&z<24)style="floor_wood";
            if(x<2||x>=34||z<2||z>=28)style="floor_graphite";
            state.floorTiles.Add(new FloorTileSaveData{itemId=style,x=start.x+x,z=start.z+z});
        }
        // Keep the established public street connection, parking and loading apron. The building,
        // not the player's already-owned starter land, is reduced to 36 x 30 cells.
        state.floorTiles.AddRange(starter.initialState.floorTiles.Where(f=>f.itemId=="asphalt_01")
            .Select(f=>new FloorTileSaveData{itemId=f.itemId,x=f.x,z=f.z}));
        foreach(var p in starter.initialState.placedObjects.Where(p=>p.itemId=="parking_01"))
            state.placedObjects.Add(JsonUtility.FromJson<PlacedBuildSaveData>(JsonUtility.ToJson(p)));
        for(int x=2;x<34;x++)
        {
            if(x<22||x>=28)Add("wall_01",x,0);
            Add("wall_01",x,28);
        }
        for(int z=2;z<28;z++)
        {
            if(z<20||z>=26)Add("wall_01",0,z,1);
            Add("wall_01",34,z,1);
            if(z<8||z>=14)Add("wall_01",10,z,1);
        }
        Add("wallcorner_01",0,0);Add("wallcorner_01",34,0,3);Add("wallcorner_01",34,28,2);Add("wallcorner_01",0,28,1);
        Add("door_01",24,0);Add("door_01",10,10,1);Add("unloading_gate_01",0,20,1);
        Add("storage_rack_01",3,3,1);Add("storage_rack_01",3,17,1);
        Add("cash_register_01",28,6,2);
        Add("shelf_fresh_01",14,18,2,"product_bread",30);
        Add("shelf_produce_01",23,18,2,"product_apple",24);
        Add("shelf_refrigerated_01",29,21,3,"product_milk",16);
        Add("decor_plant",14,4);Add("decor_bin",31,3);
        starter.initialState=state;starter.previousStarterStructure.Clear();
        starter.cameraFocus=grid.CellToWorld(start+new Vector3Int(18,0,14));
        EditorUtility.SetDirty(starter);
    }

    private void BuildModels()
    {
        var colors=TownPalette.Colors.ToArray();
        colors[(int)TownColor.Mint]=new Color32(115,149,136,255);
        colors[(int)TownColor.Red]=new Color32(163,86,65,255);
        colors[(int)TownColor.Metal]=new Color32(110,112,105,255);
        var palette=new Texture2D(32,1,TextureFormat.RGBA32,false){name="Forgotten yard faded palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        palette.SetPixels32(colors);palette.Apply();Save(palette,"Materials/Palette.asset");
        var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat")){name="Forgotten yard wood porcelain faded paint",enableInstancing=true};
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/Palette.asset"));
        material.SetFloat("_PaletteIndex",-1);Save(material,"Materials/Surface.mat");surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        foreach(var recipe in TownForgottenYardModels.Create())
        {
            var mesh=recipe.Value.Build(recipe.Key);mesh.uv2=mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();Save(mesh,"Meshes/"+recipe.Key+".asset");
            meshes.Add(recipe.Key,AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+recipe.Key+".asset"));
            var go=new GameObject(recipe.Key,typeof(MeshFilter),typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh=meshes[recipe.Key];go.GetComponent<MeshRenderer>().sharedMaterial=surface;
            PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+recipe.Key+".prefab");Object.DestroyImmediate(go);
        }
    }
    private void ReplaceScenery()
    {
        var layout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>(LayoutPath);
        layout.expansionPlots=rules.Parcels.Select(p=>
        {
            var origin=layout.MapShopPoint(new Vector3(p.Bounds.x,0,p.Bounds.y));
            return new TownExpansionPlot{territory=p.Id,bounds=new Rect(origin.x,origin.z,p.Bounds.width*layout.shopToCityScale,p.Bounds.height*layout.shopToCityScale)};
        }).ToList();EditorUtility.SetDirty(layout);
        var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try
        {
            var protectedComponents=root.GetComponentsInChildren<Component>(true)
                .Where(c=>c!=root.transform&&c.GetComponentInParent<TownExpansionScenery>()==null)
                .ToDictionary(c=>c.GetInstanceID(),c=>EditorJsonUtility.ToJson(c));
            // Keep the root/component identity: Game has a prefab override linking this component
            // to StoreProgression. Replacing it would leave the saved override pointing at a dead ID.
            var existing=root.transform.Find("Future store landscapes");
            var site=existing!=null?existing.gameObject:new GameObject("Future store landscapes");
            site.transform.SetParent(root.transform,false);
            for(int i=site.transform.childCount-1;i>=0;i--)Object.DestroyImmediate(site.transform.GetChild(i).gameObject);
            var scenery=site.GetComponent<TownExpansionScenery>()??site.AddComponent<TownExpansionScenery>();
            var plots=new List<TownPlotScenery>();
            foreach(var p in layout.expansionPlots)
            {
                var group=new GameObject("Forgotten yard "+p.territory);group.transform.SetParent(site.transform,false);
                var geometry=new TownMeshBuilder();int index=(int)p.territory-100;var r=p.bounds;
                void Prop(string id,float u,float v,float yaw=0,float scale=1,float pitch=0,float roll=0)
                {
                    var position=new Vector3(Mathf.Lerp(r.xMin,r.xMax,u),0,Mathf.Lerp(r.yMin,r.yMax,v));
                    var rotation=Quaternion.Euler(pitch,yaw,roll);
                    // Tilted pallets, broken furniture and round props sit on the ground rather than
                    // rotating their lower vertices under it or hovering at the original pivot.
                    float bottom=meshes[id].vertices.Min(vtx=>(rotation*(vtx*scale)).y);
                    position.y=.065f-bottom;
                    var pose=Matrix4x4.TRS(position,rotation,Vector3.one*scale);
                    var transformed=meshes[id].vertices.Select(pose.MultiplyPoint3x4).ToArray();
                    // Placement containment is a hard contract: buying a parcel never leaves half a prop.
                    if(transformed.Any(vtx=>vtx.x<r.xMin+.2f||vtx.x>r.xMax-.2f||vtx.z<r.yMin+.2f||vtx.z>r.yMax-.2f))
                        throw new InvalidOperationException(id+" exceeds "+p.territory+" at "+u+","+v);
                    geometry.Append(meshes[id],pose);
                    var marker=new GameObject(id);marker.transform.SetParent(group.transform,false);marker.transform.localPosition=position;
                }
                var random=new System.Random(14200+index);
                float Rand(float a,float b)=>a+(float)random.NextDouble()*(b-a);
                // Broad open patches, narrow worn desire lines and irregular plant clusters; never an orchard grid.
                geometry.Ground(r.center.x,r.center.y,r.width,r.height,.04f,TownColor.Grass);
                for(int j=0;j<5;j++)
                {
                    float rx=Rand(.7f,1.8f),rz=Rand(.5f,1.4f);
                    float x=Rand(r.xMin+rx+.1f,r.xMax-rx-.1f),z=Rand(r.yMin+rz+.1f,r.yMax-rz-.1f);
                    geometry.Ellipsoid(new Vector3(x,.039f,z),new Vector3(rx,.012f,rz),j%2==0?TownColor.Grass:TownColor.LeafLight,8,2);
                }
                // The worn path is deliberately broken into slabs and grass gaps rather than a perfect cross.
                for(float z=r.yMin+.4f;z<r.yMax-.7f;z+=1.6f)
                    if(random.NextDouble()>.2)
                    {
                        float depth=Mathf.Min(Rand(.6f,1.2f),2*(z-r.yMin-.08f),2*(r.yMax-.08f-z));
                        geometry.Ground(r.center.x+Mathf.Sin(z*.3f)*.24f,z,1.15f,depth,.061f,TownColor.Sand);
                    }
                Prop(index%2==0?"OldMaple":"Birch",.24f,.7f,index*37,.85f);
                if(r.width>15)Prop("Birch",.78f,.76f,40,1.05f);
                // Every parcel has one understandable vignette; only three have conspicuous scrap.
                switch(index)
                {
                    case 0:Prop("BrokenBench",.64f,.6f,92);Prop("FallenBicycle",.73f,.3f,18);Prop("DetachedWheel",.56f,.23f);break;
                    case 1:Prop("PicnicTable",.57f,.57f,8);Prop("OldChair",.78f,.57f,-15);Prop("OldChair",.56f,.38f,175);break;
                    case 2:Prop("OldNoticeboard",.55f,.5f,180);Prop("FenceRemnant",.45f,.85f,10);break;
                    case 3:Prop("FadedSlide",.66f,.38f,28);Prop("BrokenBench",.24f,.35f,100);break;
                    case 4:Prop("OneSeatSwing",.66f,.45f,12);Prop("Stump",.32f,.27f);break;
                    case 5:Prop("OvergrownSandbox",.62f,.48f,0);Prop("RustBucket",.76f,.7f);break;
                    case 6:Prop("WeatheredBench",.66f,.45f,-90);Prop("WateringCan",.74f,.34f);break;
                    case 7:Prop("Stump",.7f,.3f);Prop("FallenBranch",.64f,.45f,35);break;
                    case 8:Prop("PicnicTable",.65f,.45f,14);Prop("OldChair",.76f,.25f,155);break;
                    case 9:Prop("BrokenBench",.5f,.35f,190);break;
                    case 10:Prop("LeaningPallet",.69f,.48f,0,1,65);Prop("SplitCrate",.7f,.28f);Prop("LooseBoards",.4f,.28f);break;
                    case 11:Prop("WeatheredBench",.64f,.38f,180);break;
                    case 12:Prop("FallenBranch",.6f,.37f,22);break;
                    case 13:Prop("Stump",.65f,.4f);break;
                    case 14:Prop("FenceRemnant",.55f,.42f,10);break;
                    case 15:Prop("BrokenBench",.62f,.35f,170);break;
                    case 16:Prop("OldTyre",.68f,.45f);Prop("RustBucket",.72f,.34f);break;
                    case 17:Prop("PicnicTable",.65f,.38f,20);Prop("OldChair",.8f,.49f,70);break;
                    case 18:Prop("WeatheredBench",.6f,.42f,0);break;
                    case 19:Prop("FenceRemnant",.63f,.75f,0);Prop("LooseBoards",.68f,.45f);break;
                    case 20:Prop("OldNoticeboard",.55f,.42f);break;
                    case 21:Prop("BrokenBench",.6f,.32f,180);break;
                    case 22:Prop("Stump",.7f,.4f);Prop("FallenBranch",.55f,.3f);break;
                    case 23:Prop("DiscardedToilet",.73f,.69f,24,1,0,8);Prop("OldTyre",.62f,.7f);Prop("FenceRemnant",.65f,.83f,0);Prop("RustBucket",.8f,.62f);break;
                }
                // Plants frame the stories and plot edges without burying the silhouettes.
                for(int j=0;j<7;j++)Prop("Shrub",Rand(.16f,.85f),j%2==0?.14f:.86f,Rand(0,360),Rand(.65f,1.1f));
                for(int j=0;j<28;j++)Prop("TallWeeds",Rand(.1f,.9f),Rand(.12f,.88f),Rand(0,360),Rand(.7f,1.4f));
                string id="Parcel"+(index+1).ToString("00");var mesh=geometry.Build(id);mesh.uv2=mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();Save(mesh,"Meshes/"+id+".asset");
                group.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+id+".asset");group.AddComponent<MeshRenderer>().sharedMaterial=surface;
                plots.Add(new TownPlotScenery{territory=p.territory,model=group});
            }
            scenery.plots=plots.ToArray();
            var current=root.GetComponentsInChildren<Component>(true).ToDictionary(c=>c.GetInstanceID());
            TownForgottenYardBatch.Require(protectedComponents.All(p=>current.TryGetValue(p.Key,out var c)&&EditorJsonUtility.ToJson(c)==p.Value),"A protected town component changed.");
            PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static void Save(Object asset,string relative)=>TownBakeryPlacement.SaveAsset(asset,Root+"/"+relative);
}
