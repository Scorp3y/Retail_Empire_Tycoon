using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

/// <summary>Local replacement of three approved sites; retained city and supplier identities are not rebuilt.</summary>
internal sealed class TownGatewayPlacement
{
    internal const string Root="Assets/Art/TownGatewayUpdate",Farm="Rebuilt rural farm",Fuel="Upgraded fuel station",Crossing="New district railway crossing",Repairs="Dairy and bridge repairs";
    internal const float Floor=.15f;
    // Three metres at the fuel frontage include only the two driveway-to-street joins.
    internal static readonly Rect FarmArea=new(454,244,200,114),FuelArea=new(174,-141,84,57),GatewayArea=new(-42,195,40,125);
    internal static readonly Rect[] Permissions={FarmArea,FuelArea,GatewayArea,new(291,-48,76,16),new(435,45,165,165)};
    private readonly GameObject town;
    private readonly Dictionary<string,GameObject> models=new();
    private Material paint,glass;
    private TownStaticTypography lettering;
    private int trimIndex;
    private readonly List<string> audit=new();
    internal TownGatewayPlacement(GameObject town){this.town=town;}
    internal void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(Farm)!=null)throw new InvalidOperationException("Update already installed; restart from the recorded original.");
        Catalogue();
        ClearParcel(FarmArea,"farm");ClearParcel(FuelArea,"fuel");ClearGatewayTrees();
        DairyAndBridge();FarmSite();FuelSite();RailCrossing();
        File.WriteAllLines(TownGatewayBatch.Report+"/placement.txt",audit);
    }
    private void Catalogue()
    {
        foreach(string folder in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(Root+"/"+folder);AssetDatabase.Refresh();
        paint=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat")){name="Gateway palette",enableInstancing=true};
        TownBakeryPlacement.SaveAsset(paint,Root+"/Materials/Surface.mat");paint=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        glass=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Gateway glass",renderQueue=3000};glass.SetFloat("_Surface",1);glass.SetFloat("_ZWrite",0);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetColor("_BaseColor",new Color(.6f,.8f,.84f,.13f));glass.SetOverrideTag("RenderType","Transparent");glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.SetShaderPassEnabled("ShadowCaster",false);TownBakeryPlacement.SaveAsset(glass,Root+"/Materials/Glass.mat");glass=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Glass.mat");
        lettering=new TownStaticTypography(Root,"FARM MARKET FARM PICKUP SHOP & COFFEE FUEL AIR & WATER NEW DISTRICT STOP 30 ENTRY PARKING");
        var recipes=TownFarmUpdateModels.Create().Concat(TownFuelUpdateModels.Create()).ToList();
        recipes.AddRange(TownBakeryQuarterModels.Create().Where(m=>new[]{"SquareTree","LowHedge","SquareBench","LitterBin","BicycleRack","FlowerBed","FlowerPot","UmbrellaTableSet","PatioTableSet","SquareLantern"}.Contains(m.Id)));
        recipes.AddRange(TownEquipmentModels.Create().Where(m=>new[]{"RetailShelf","DisplayFridge","CheckoutCounter","PalletRack","WrappedPallet","PalletStack","BlueCustomerCar","GreenCustomerCar","SafetyBollard","SiteLamp"}.Contains(m.Id)));
        recipes.Add(new EquipmentModel("Forklift",()=>Native(TownVehicles.Forklift())));recipes.Add(new EquipmentModel("FarmVan",()=>Native(TownVehicles.Truck())));
        foreach(var r in recipes)
        {
            var go=new GameObject(r.Id);go.isStatic=r.Id!="BarrierArm";
            try
            {
                Part(go.transform,r.Id,r.Draft.Mesh,false);
                foreach(var s in r.Draft.Solids){var node=Child(go.transform,s.Id,Vector3.zero);var box=node.gameObject.AddComponent<BoxCollider>();box.center=s.Center;box.size=s.Size;}
                if(r.Id=="SiteLamp"){var stem=Child(go.transform,"Lamp stem",Vector3.zero);var box=stem.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,1.86f,0);box.size=new Vector3(.1f,3.72f,.1f);}
                switch(r.Id)
                {
                    case "FarmMarket":lettering.Add(go.transform,"Market sign","FARM MARKET",new Vector3(0,3.1f,2.69f),7.5f,.45f);break;
                    case "PackingHall":lettering.Add(go.transform,"Pickup sign","FARM PICKUP",new Vector3(0,4.95f,6.2f),11,.5f);break;
                    case "FuelShop":lettering.Add(go.transform,"Shop sign","SHOP & COFFEE",new Vector3(0,3.7f,5.14f),11.5f,.43f);ShopGlass(go.transform);break;
                    case "FuelPylon":lettering.Add(go.transform,"Fuel sign","FUEL",new Vector3(0,4.7f,.25f),2.2f,.6f);break;
                    case "AirWater":lettering.Add(go.transform,"Air sign","AIR & WATER",new Vector3(0,1.45f,.4f),1.4f,.19f);break;
                    case "DistrictSign":lettering.Add(go.transform,"District sign","NEW DISTRICT",new Vector3(0,3.1f,.09f),4,.45f);break;
                    case "CrossingSign":lettering.Add(go.transform,"Stop sign","STOP",new Vector3(0,1.3f,.08f),.9f,.22f);lettering.Add(go.transform,"Speed sign","30",new Vector3(0,2.2f,.06f),.65f,.35f);break;
                    case "Greenhouse":GreenhouseGlass(go.transform);break;
                }
                string path=Root+"/Prefabs/"+r.Id+".prefab";PrefabUtility.SaveAsPrefabAsset(go,path);models.Add(r.Id,AssetDatabase.LoadAssetAtPath<GameObject>(path));
                audit.Add(r.Id+": "+go.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)+" triangles.");
            }
            finally{Object.DestroyImmediate(go);}
        }
        AssetDatabase.SaveAssets();
    }
    private static EquipmentDraft Native(TownMeshBuilder mesh){var d=new EquipmentDraft();var m=mesh.Build("Native vehicle");d.Mesh.Append(m,Matrix4x4.identity);d.Collision("Parked vehicle",m.bounds.center,m.bounds.size);Object.DestroyImmediate(m);return d;}
    private void ShopGlass(Transform parent)
    {
        var d=new TownMeshBuilder();foreach(float x in new[]{-4f,4})Pane(d,new Vector3(x,1.8f,5),5.2f,2.9f);Part(parent,"FuelShop glazing",d,false,glass);
    }
    private void GreenhouseGlass(Transform parent)
    {
        var d=new TownMeshBuilder();foreach(float x in new[]{-4.4f,4.4f}){var a=new Vector3(x,.25f,-9);var b=new Vector3(x,.25f,9);var c=new Vector3(x,3.4f,9);var e=new Vector3(x,3.4f,-9);d.Quad(a,b,c,e,TownColor.White);d.Quad(e,c,b,a,TownColor.White);}
        foreach(float x in new[]{-4.4f,4.4f}){var a=new Vector3(x,3.4f,-9);var b=new Vector3(x,3.4f,9);var c=new Vector3(0,4.85f,9);var e=new Vector3(0,4.85f,-9);d.Quad(a,b,c,e,TownColor.White);d.Quad(e,c,b,a,TownColor.White);}
        foreach(float z in new[]{-9f,9})foreach(float x in new[]{-3f,3})Pane(d,new Vector3(x,1.8f,z),2.7f,3);
        Part(parent,"Greenhouse glazing",d,false,glass);
    }
    private static void Pane(TownMeshBuilder d,Vector3 p,float w,float h){var a=p+new Vector3(-w/2,-h/2,0);var b=p+new Vector3(w/2,-h/2,0);var c=p+new Vector3(w/2,h/2,0);var e=p+new Vector3(-w/2,h/2,0);d.Quad(a,b,c,e,TownColor.White);d.Quad(e,c,b,a,TownColor.White);}
    private static bool Inside(Rect r,Vector3 p)=>TownDairyPlacement.Inside(r,p);
    private void Trim(Func<MeshFilter,bool> choose,Func<Vector3,Vector3,Vector3,bool> remove)
    {
        foreach(var f in town.GetComponentsInChildren<MeshFilter>(true).Where(choose).ToArray())
        {
            var mesh=TownBakeryPlacement.CopyWithoutTriangles(f.sharedMesh,f.transform,remove,out int count);if(count==0)continue;
            string path=Root+"/Meshes/LegacyTrim"+trimIndex+++".asset";TownBakeryPlacement.SaveAsset(mesh,path);var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var collider=f.GetComponent<MeshCollider>();if(collider!=null&&collider.sharedMesh==f.sharedMesh)collider.sharedMesh=saved;f.sharedMesh=saved;
            audit.Add("Trimmed "+count+" complete triangles from "+f.name);
        }
    }
    private void ClearParcel(Rect area,string label)
    {
        var road=town.GetComponent<TownWorldPlaces>().layout.countryRoad.points;
        bool RoadSurface(Vector3 p)=>(Mathf.Abs(p.y-.12f)<.0005f||Mathf.Abs(p.y-.14f)<.0005f)&&road.Skip(1).Select((b,i)=>TownDairyPlacement.SegmentDistance(p,road[i],b)).Min()<6.3f;
        Trim(f=>!f.name.Contains("country")&&!f.name.Contains("Country"),(a,b,c)=>Inside(area,a)&&Inside(area,b)&&Inside(area,c)&&!(label=="farm"&&RoadSurface(a)&&RoadSurface(b)&&RoadSurface(c)));
        foreach(var c in town.GetComponentsInChildren<Collider>(true).ToArray())
        {
            var b=TownResidentialPlacement.ColliderBounds(c);if(Inside(area,b.min)&&Inside(area,b.max)&&!(c is MeshCollider))Object.DestroyImmediate(c);
        }
        audit.Add("Replaced legacy "+label+" within "+area+"; country-road surface retained.");
    }
    private void ClearGatewayTrees()
    {
        // Recognise entire native vegetation templates rather than cutting crowns at a rectangle edge.
        var keys=new HashSet<string>();var selected=new List<Transform>();
        foreach(var node in town.GetComponentsInChildren<Transform>(true))
        {
            if(!new[]{"Pine","TreeWide","TreeRound","BushRound","GrassClump"}.Contains(node.name)||node.position.z<197||node.position.z>312||Mathf.Abs(node.position.x+24)>13)continue;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/"+node.name+".prefab");if(source==null)continue;
            var mesh=source.GetComponent<MeshFilter>().sharedMesh;AddKeys(keys,mesh,node.localToWorldMatrix);selected.Add(node);
            // Original forest batching left collider-free shrubs without their source transforms.
            var bush=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/TownKit/Prefabs/Nature/BushRound.prefab").GetComponent<MeshFilter>().sharedMesh;
            AddKeys(keys,bush,Matrix4x4.TRS(node.position+new Vector3(2,0,-2),Quaternion.identity,Vector3.one*1.2f));
        }
        Trim(f=>true,(a,b,c)=>keys.Contains(Key(a,b,c)));
        foreach(var t in selected)Object.DestroyImmediate(t.gameObject);
        audit.Add("Crossing corridor: removed "+selected.Count+" whole native vegetation nodes; surrounding forest retained.");
    }
    private static string Key(Vector3 a,Vector3 b,Vector3 c)=>string.Join("|",new[]{Point(a),Point(b),Point(c)}.OrderBy(v=>v,StringComparer.Ordinal));
    private static string Point(Vector3 p)=>Mathf.RoundToInt(p.x*100)+","+Mathf.RoundToInt(p.y*100)+","+Mathf.RoundToInt(p.z*100);
    private static void AddKeys(HashSet<string> keys,Mesh mesh,Matrix4x4 pose){var v=mesh.vertices.Select(pose.MultiplyPoint3x4).ToArray();var t=mesh.triangles;for(int i=0;i<t.Length;i+=3)keys.Add(Key(v[t[i]],v[t[i+1]],v[t[i+2]]));}
    private void DairyAndBridge()
    {
        var repair=Child(town.transform,Repairs,Vector3.zero);var dairy=town.transform.Find(TownDairyPlacement.SiteName);if(dairy==null)throw new InvalidOperationException("Installed dairy missing.");
        var trees=dairy.Find("Office and landscape").Cast<Transform>().Where(t=>t.name=="SquareTree").ToArray();if(trees.Length!=6)throw new InvalidOperationException("Expected six dairy trees.");
        var keys=new HashSet<string>();var source=AssetDatabase.LoadAssetAtPath<GameObject>(TownDairyPlacement.Root+"/Prefabs/SquareTree.prefab").GetComponent<MeshFilter>().sharedMesh;
        foreach(var t in trees)AddKeys(keys,source,t.localToWorldMatrix);
        Trim(f=>f.transform.IsChildOf(dairy),(a,b,c)=>keys.Contains(Key(a,b,c))||IsDairyGrass(a,b,c));
        foreach(var t in trees)Object.DestroyImmediate(t.gameObject);
        var landscape=Child(repair,"Dairy entrance planters",dairy.position);landscape.rotation=dairy.rotation;
        foreach(var p in new[]{new Vector2(18,29),new Vector2(9,30),new Vector2(-18,30)}){Place(landscape,"TreePlanter",p.x,p.y,0,TownDairyPlacement.Floor);Place(landscape,"SquareTree",p.x,p.y,0,TownDairyPlacement.Floor+.12f);}
        // Fill under the original sampled ribbon, never place another driving face above it.
        var d=new TownMeshBuilder();var points=town.GetComponent<TownWorldPlaces>().layout.countryRoad.points;
        for(int i=1;i<points.Length;i++)
        {
            var a=points[i-1];var b=points[i];if(a.x<298||b.x>360||Mathf.Abs(a.z+40)>.01f||Mathf.Abs(b.z+40)>.01f||a.x>311&&b.x<345)continue;
            if(a.x==311&&b.x==345)continue;
            float ya=a.y+.115f,yb=b.y+.115f;float width=11.6f;
            Vector3 P(float x,float y,float z)=>new Vector3(x,y,z);
            foreach(float z in new[]{-40-width/2,-40+width/2}){d.Quad(P(a.x,-.2f,z),P(b.x,-.2f,z),P(b.x,yb,z),P(a.x,ya,z),TownColor.Stone);d.Quad(P(a.x,ya,z),P(b.x,yb,z),P(b.x,-.2f,z),P(a.x,-.2f,z),TownColor.Stone);}
            d.Quad(P(a.x,ya,-45.8f),P(b.x,yb,-45.8f),P(b.x,yb,-34.2f),P(a.x,ya,-34.2f),TownColor.Stone);
            foreach(var p in new[]{a,b})d.Quad(P(p.x,-.2f,-45.8f),P(p.x,p.y+.115f,-45.8f),P(p.x,p.y+.115f,-34.2f),P(p.x,-.2f,-34.2f),TownColor.Stone);
        }
        Part(repair,"Bridge solid approach infill",d,true);
        audit.Add("Dairy: six legacy grass inserts removed; three service trees removed, three entrance trees re-seated inside soil planters. Bridge approach concrete follows exact existing ramp heights, road collider retained.");
        Combine(repair,"Repair");
    }
    private static bool IsDairyGrass(Vector3 a,Vector3 b,Vector3 c)
    {
        if(Mathf.Abs(a.y-(TownDairyPlacement.Floor+.002f))>.0005f||Mathf.Abs(b.y-a.y)>.0005f||Mathf.Abs(c.y-a.y)>.0005f)return false;
        var inverse=Matrix4x4.TRS(TownDairyBatch.Origin,TownDairyBatch.Rotation,Vector3.one).inverse;
        a=inverse.MultiplyPoint3x4(a);b=inverse.MultiplyPoint3x4(b);c=inverse.MultiplyPoint3x4(c);
        return new[]{new Vector2(18,29),new Vector2(9,30),new Vector2(-18,30),new Vector2(39,-31),new Vector2(39,18),new Vector2(-36,-34.8f)}.Any(p=>new[]{a,b,c}.All(v=>Mathf.Abs(v.x-p.x)<=3.501f&&Mathf.Abs(v.z-p.y)<=2.501f));
    }
    private void FarmSite()
    {
        var site=Child(town.transform,Farm,Vector3.zero);var paving=new TownMeshBuilder();
        // The original curved country ribbon and its shoulders are retained by exact height/path
        // recognition above; only the visitor branch is new. A straight replacement would leave a kink.
        paving.Ground(531.5f,270,37,8,Floor,TownColor.Asphalt);paving.Ground(513,275,8,10,Floor,TownColor.Asphalt);
        paving.Ground(550,297,65,42,Floor,TownColor.Asphalt);paving.Ground(513,283,18,13,Floor,TownColor.Asphalt);
        paving.Ground(608,321,68,26,Floor,TownColor.Sand);paving.Ground(525,343,46,27,Floor,TownColor.Sand);
        paving.Ground(530,329,19,5,Floor,TownColor.Sand);paving.Ground(526,319,5,22,Floor,TownColor.Sand);
        paving.Ground(487,320,42,55,Floor-.01f,TownColor.LeafLight);paving.Ground(485,269,34,36,Floor-.01f,TownColor.Grass);
        paving.Ground(558,352,6,12,Floor,TownColor.Sand);paving.Ground(607,353,60,4,Floor,TownColor.Sand);
        for(int i=0;i<4;i++)paving.Ground(505.5f+i*4.8f,282,.07f,6,Floor+.006f,TownColor.White);
        foreach(float z in new[]{298f,312f})paving.Ground(560,z,6,.08f,Floor+.006f,TownColor.Yellow);foreach(float x in new[]{557f,563f})paving.Ground(x,305,.08f,14,Floor+.006f,TownColor.Yellow);
        Part(site,"Farm ground and markings",paving,true);
        Place(site,"FarmHouse",535,343,180);Place(site,"RedBarn",514,343,180);Place(site,"PackingHall",560,321,180);Place(site,"SortingCanopy",560,312,180);
        Place(site,"HayShelter",508,321);foreach(float x in new[]{505f,508f,511f})Place(site,"HayRound",x,321);
        foreach(float x in new[]{590f,604f}){Place(site,"Greenhouse",x,341,180);Place(site,"IrrigationStation",x+5,338);}
        Place(site,"TractorGarage",630,326,180);Place(site,"Tractor",628,320,180);Place(site,"FarmTrailer",634,312,180);
        foreach(float x in new[]{623f,634f})Place(site,"GrainSilo",x,348);
        Place(site,"FarmTools",635.8f,329,0);Place(site,"HandToolsAndSpares",625,329);Place(site,"PalletStack",622,322);Place(site,"GrainSacks",621,326);Place(site,"CompostBay",610,312);Place(site,"FuelTank",644,314);Place(site,"ControlCabinet",645,319);
        foreach(float x in new[]{549f,553f,569f,573f})Place(site,"SortingTable",x,311);
        foreach(float x in new[]{541f,579f}){Place(site,"PalletRack",x,321,180);Place(site,"FarmCrates",x,310);Place(site,"FarmCrates",x,306);}
        Place(site,"Forklift",579,299,90);Place(site,"FarmVan",570,286,0);Place(site,"Wheelbarrow",540,331,90);
        Place(site,"FarmMarket",513,295,180);Place(site,"BlueCustomerCar",508,282);Place(site,"GreenCustomerCar",513,282);Place(site,"BlueCustomerCar",518,282);
        for(int ix=0;ix<4;ix++)for(int iz=0;iz<5;iz++)Place(site,"FruitTree",471+ix*9,300+iz*10,ix*33+iz*21);
        Place(site,"AnimalShelter",476,283,180);Place(site,"ChickenCoop",497,282,180);Place(site,"FeedTrough",475,265);Place(site,"FeedTrough",493,263);
        foreach(var p in new[]{new Vector2(479,273),new Vector2(488,278),new Vector2(493,269),new Vector2(483,259)})Place(site,"Cow",p.x,p.y,p.x*19);
        for(int i=0;i<7;i++)Place(site,i==6?"Rooster":"Hen",495+i%3,276-i/3*1.5f,i*47);
        foreach(float x in new[]{474f,478f,482f})Place(site,"HaySquare",x,284);
        var fence=new EquipmentDraft();Fence(fence,new Vector3(468,Floor,251),new Vector3(502,Floor,251),false);Fence(fence,new Vector3(468,Floor,251),new Vector3(468,Floor,288),false);Fence(fence,new Vector3(468,Floor,288),new Vector3(502,Floor,288),false);Fence(fence,new Vector3(502,Floor,251),new Vector3(502,Floor,270),false);Fence(fence,new Vector3(502,Floor,275),new Vector3(502,Floor,288),false);
        // Open 12 m service entrance, separated from the market footpath.
        Fence(fence,new Vector3(520,Floor,276),new Vector3(543,Floor,276),false);Fence(fence,new Vector3(556,Floor,276),new Vector3(584,Floor,276),false);Fence(fence,new Vector3(584,Floor,276),new Vector3(584,Floor,305),false);
        Part(site,"Farm boundary",fence.Mesh,false);Solids(site,"Farm boundary volumes",fence);
        foreach(var p in new[]{new Vector2(521,295),new Vector2(506,292),new Vector2(534,278),new Vector2(580,282),new Vector2(586,328),new Vector2(643,338)}){Place(site,"FlowerBed",p.x,p.y);}
        foreach(var p in new[]{new Vector2(521,287),new Vector2(504,293),new Vector2(539,353),new Vector2(579,280)})Place(site,"SquareTree",p.x,p.y);
        foreach(float x in new[]{522f,528f,538f,562f,568f,574f,581f})Place(site,"LowHedge",x,277);
        foreach(float z in new[]{300f,310f,320f,330f,340f})Place(site,"LowHedge",466.5f,z,90);
        var paths=new TownMeshBuilder();paths.Ground(487,322,2,53,Floor+.006f,TownColor.Sand);paths.Ground(487,322,40,1.5f,Floor+.007f,TownColor.Sand);paths.Ground(505.5f,272.5f,7,5,Floor,TownColor.Sand);Part(site,"Orchard and pasture paths",paths,true);
        Place(site,"SquareBench",505,299,90);Place(site,"BicycleRack",504,290,90);Place(site,"LitterBin",522,291);foreach(float x in new[]{525f,578f})Place(site,"SiteLamp",x,289);
        var garden=new TownMeshBuilder();for(int i=0;i<4;i++){garden.Ground(542+i,352,.6f,5,Floor+.01f,TownColor.Soil);for(int j=0;j<5;j++)garden.Ellipsoid(new Vector3(542+i,.4f,350+j),new Vector3(.25f,.24f,.25f),TownColor.Leaf,6,3);}Part(site,"House kitchen garden",garden,false);
        var checkpoint=town.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Farmers);var court=town.GetComponentsInChildren<SupplierLoadingCourt>().Single(c=>c.checkpoint==checkpoint);
        checkpoint.transform.position=new Vector3(560,Floor+.02f,305);checkpoint.transform.rotation=Quaternion.Euler(0,180,0);checkpoint.requiresRearDocking=true;
        court.bounds=new Rect(520,276,64,42);court.streetAccess=new Vector3(550,0,272);court.entrance=new Vector3(550,Floor,279);court.staging=new Vector3(560,Floor,289);court.approachWaypoints=new[]{new Vector3(550,Floor,289)};court.designVehicleLength=6.8f;
        Combine(site,"Farm");audit.Add("Farm rebuilt in original parcel; original field rows and field fences retained; original Farmers checkpoint reference reconnected to new packing bay. Decorative animals only.");
    }
    private void FuelSite()
    {
        var site=Child(town.transform,Fuel,Vector3.zero);var ground=new TownMeshBuilder();ground.Ground(216,-110,76,48,Floor,TownColor.Asphalt);
        foreach(float x in new[]{204f,228f})ground.Ground(x,-136,9,8,Floor,TownColor.Asphalt);
        ground.Ground(195,-101,20,3,Floor+.02f,TownColor.Pavement);ground.Ground(181,-94,8,14,Floor+.02f,TownColor.Pavement);
        for(int i=0;i<4;i++)ground.Ground(187.5f+i*4.8f,-110,.12f,6,Floor+.012f,TownColor.White);
        ground.Ground(194.7f,-107,14.4f,.12f,Floor+.012f,TownColor.White);
        foreach(float x in new[]{214f,224f,234f})Arrow(ground,new Vector3(x,Floor+.012f,-121),180);Arrow(ground,new Vector3(228,Floor+.012f,-130),0);Arrow(ground,new Vector3(204,Floor+.012f,-130),180);
        foreach(float x in new[]{239f,245f})ground.Ground(x,-91,.07f,7,Floor+.006f,TownColor.White);Part(site,"Fuel paving and markings",ground,true);
        Place(site,"FuelShop",197,-94,180);Place(site,"FuelCanopy",224,-113);
        foreach(float x in new[]{219f,229f}){Place(site,"PumpIsland",x,-113);foreach(float z in new[]{-116f,-110f})Place(site,"FuelPump",x,z,0,Floor+.24f);foreach(float z in new[]{-117.5f,-108.5f})Place(site,"SafetyBollard",x,z,0,Floor+.24f);}
        Place(site,"FuelPylon",252,-129,180);Place(site,"AirWater",249,-90,270);Place(site,"ControlCabinet",252,-85,180);Place(site,"FireCabinet",209,-96,180);
        Place(site,"CheckoutCounter",200,-94,180);Place(site,"CoffeeCounter",193,-90,180);Place(site,"DisplayFridge",201,-90,180);Place(site,"RetailShelf",193,-96,90);Place(site,"CarGoods",201,-96,270);Place(site,"BasketStack",198,-98);
        Place(site,"BlueCustomerCar",190,-110,0);Place(site,"GreenCustomerCar",195,-110,0);Place(site,"BlueCustomerCar",214,-115,180);Place(site,"GreenCustomerCar",234,-112,180);
        foreach(float z in new[]{-91f,-97f})Place(site,"UmbrellaTableSet",181,-94+(z+94),0);Place(site,"BicycleRack",187,-88,90);Place(site,"SquareBench",186,-99,180);
        foreach(var p in new[]{new Vector2(206,-100),new Vector2(220.2f,-119),new Vector2(230.2f,-119)})Place(site,"LitterBin",p.x,p.y);
        foreach(var p in new[]{new Vector2(207,-132),new Vector2(234,-132),new Vector2(251,-107)})Place(site,"Drain",p.x,p.y);
        foreach(var p in new[]{new Vector2(178,-131),new Vector2(211,-88),new Vector2(253,-102),new Vector2(234,-86)})Place(site,"SiteLamp",p.x,p.y);
        foreach(var p in new[]{new Vector2(179,-119),new Vector2(195,-129),new Vector2(215,-88),new Vector2(252,-118)}){Place(site,"TreePlanter",p.x,p.y);Place(site,"SquareTree",p.x,p.y,0,Floor+.12f);}
        foreach(float x in new[]{193f,200f})Place(site,"FlowerPot",x,-100);
        foreach(float x in new[]{181f,188f,195f,202f,209f,216f,223f,230f,237f,244f,251f}){Place(site,"LowHedge",x,-85);}
        foreach(float x in new[]{181f,188f,195f,213f,219f,237f,244f,251f})Place(site,"LowHedge",x,-133);
        var edges=new TownMeshBuilder();
        // Continuous rear/side kerbs, with deliberate gaps for the two driveway connections.
        foreach(var span in new[]{new Vector2(178,199.5f),new Vector2(208.5f,223.5f),new Vector2(232.5f,254)}){edges.Box(new Vector3((span.x+span.y)/2,Floor+.09f,-134),new Vector3(span.y-span.x,.18f,.18f),TownColor.Pavement);edges.Ground((span.x+span.y)/2,-133,span.y-span.x,1.5f,Floor+.004f,TownColor.Soil);}
        edges.Box(new Vector3(216,Floor+.09f,-86),new Vector3(76,.18f,.18f),TownColor.Pavement);
        foreach(float x in new[]{178f,254f})edges.Box(new Vector3(x,Floor+.09f,-110),new Vector3(.18f,.18f,48),TownColor.Pavement);
        foreach(var p in new[]{new Vector2(204,-136),new Vector2(228,-136)})foreach(float side in new[]{-1f,1f})edges.Box(new Vector3(p.x+side*4.5f,Floor+.06f,p.y),new Vector3(.15f,.12f,8),TownColor.Pavement);
        edges.Ground(216,-85,76,2,Floor+.005f,TownColor.Soil);
        Part(site,"Fuel kerbs and planted borders",edges,false);
        Combine(site,"Fuel");audit.Add("Fuel: two islands, exactly four pumps, three parking spaces (two occupied), two canopy cars, one unobstructed AIR & WATER space. Shop interior furnished; no new fuel economy mechanic.");
    }
    private void RailCrossing()
    {
        var site=Child(town.transform,Crossing,Vector3.zero);var d=new TownMeshBuilder();var collision=new TownMeshBuilder();
        // Railway top is 2.30 m. Smooth approaches rise to it; the original rails are never moved or cut.
        float Height(float z)=>z<224?Mathf.Lerp(.12f,2.30f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(198,224,z))):z>232?Mathf.Lerp(2.30f,.12f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(232,264,z))):2.30f;
        for(int z=198;z<310;z++)
        {
            float a=Height(z),b=Height(z+1);QuadRibbon(d,-28,-20,z,z+1,a,b,TownColor.Asphalt);QuadRibbon(collision,-28,-20,z,z+1,a,b,TownColor.Asphalt);
            foreach(var edge in new[]{new Vector2(-30,-28),new Vector2(-20,-18)}){QuadRibbon(d,edge.x,edge.y,z,z+1,a,b,TownColor.Pavement);QuadRibbon(collision,edge.x,edge.y,z,z+1,a,b,TownColor.Pavement);}
            foreach(float x in new[]{-28.15f,-19.85f})QuadRibbon(d,x-.04f,x+.04f,z,z+1,a+.006f,b+.006f,TownColor.White);
            if(z%6<3&&Mathf.Abs(z-228)>4)QuadRibbon(d,-24.05f,-23.95f,z,z+1,a+.008f,b+.008f,TownColor.White);
            foreach(float x in new[]{-30f,-18f}){d.Quad(new Vector3(x,0,z),new Vector3(x,a,z),new Vector3(x,b,z+1),new Vector3(x,0,z+1),TownColor.Stone);d.Quad(new Vector3(x,0,z+1),new Vector3(x,b,z+1),new Vector3(x,a,z),new Vector3(x,0,z),TownColor.Stone);}
        }
        foreach(float z in new[]{219f,237f})d.Ground(-24,z,7.8f,.28f,Height(z)+.01f,TownColor.White);
        // Narrow flangeways leave the two original rail heads visible while wheel collision stays smooth.
        foreach(var span in new[]{new Vector2(225.4f,227.18f),new Vector2(227.38f,228.62f),new Vector2(228.82f,230.6f)})d.Ground(-24,(span.x+span.y)/2,12,span.y-span.x,2.305f,TownColor.Rubber);
        Part(site,"Crossing road and sidewalks",d,false);Part(site,"Crossing physical ribbon",collision,true,paint,false);
        var arms=new List<Transform>();var lamps=new List<GameObject>();
        foreach(var p in new[]{new Vector3(-28.8f,Height(218),218),new Vector3(-19.2f,Height(238),238)})
        {
            float yaw=p.z<228?0:180;Place(site,"BarrierBase",p.x,p.z,yaw,p.y);var pivot=Child(site,"Barrier pivot",p+Vector3.up*1.2f);pivot.rotation=Quaternion.Euler(0,yaw,0);var arm=Place(pivot,"BarrierArm",0,0,0,0);arm.localRotation=Quaternion.Euler(0,0,90);arms.Add(arm);
            var signal=Place(site,"RailSignal",p.x,p.z+(p.z<228?-1:1),yaw,p.y);var lights=new TownMeshBuilder();foreach(float x in new[]{-.32f,.32f})lights.Cylinder(new Vector3(x,2.7f,.235f),.16f,.16f,.025f,TownColor.Red,10,Quaternion.Euler(90,0,0));var lamp=Part(signal,"Active red lamps "+lamps.Count,lights,false);lamps.Add(lamp.gameObject);lamp.gameObject.SetActive(false);
        }
        Place(site,"ControlHut",-9,247,180);Place(site,"ControlCabinet",-6,243,180);Place(site,"DistrictSign",-14,267,180);
        Place(site,"CrossingSign",-18.5f,211,180,Height(211));Place(site,"CrossingSign",-29.5f,246,0,Height(246));
        var guards=new EquipmentDraft();foreach(float x in new[]{-30.5f,-17.5f}){Fence(guards,new Vector3(x,Height(208),208),new Vector3(x,Height(218),218),true);Fence(guards,new Vector3(x,Height(238),238),new Vector3(x,Height(250),250),true);}
        Part(site,"Crossing pedestrian guides",guards.Mesh,false);Solids(site,"Crossing guide volumes",guards);
        var tech=new TownMeshBuilder();tech.Ground(-9,246,9,8,Floor,TownColor.Pavement);
        for(int i=0;i<6;i++){float x=-17.625f+i*.75f;float top=Mathf.Lerp(Height(246),Floor,(i+1)/6f);tech.Box(new Vector3(x,(top+Floor)/2,246),new Vector3(.75f,Mathf.Max(.02f,top-Floor),2),TownColor.Stone);}
        Part(site,"Technical pad",tech,true);
        var techFence=new EquipmentDraft();Fence(techFence,new Vector3(-13.5f,Floor,242),new Vector3(-4.5f,Floor,242),true);Fence(techFence,new Vector3(-4.5f,Floor,242),new Vector3(-4.5f,Floor,250),true);Fence(techFence,new Vector3(-4.5f,Floor,250),new Vector3(-13.5f,Floor,250),true);Part(site,"Technical enclosure",techFence.Mesh,false);Solids(site,"Technical enclosure volumes",techFence);
        foreach(float z in new[]{203f,255f,295f})Place(site,"SiteLamp",-32,z,0,Height(z));
        var control=site.gameObject.AddComponent<TownLevelCrossing>();control.train=town.GetComponentInChildren<TownRailTraffic>();control.trackDistance=336;control.arms=arms.ToArray();control.redLamps=lamps.ToArray();control.deckCenter=new Vector3(-24,3.8f,228);control.occupancyHalfSize=new Vector3(4.2f,1.3f,7);
        // Dynamic arms and lamps remain separate from static geometry and keep real colliders.
        Combine(site,"Crossing",f=>!f.transform.IsChildOf(arms[0])&&!f.transform.IsChildOf(arms[1])&&!lamps.Any(l=>f.transform.IsChildOf(l.transform)));
        audit.Add("Northern crossing at (-24,228): original railway preserved, 112 m road extension, raised deck at original 2.30 m rail-head height, 2 physical dynamic barrier arms, warning lamps and occupied-deck train hold.");
    }
    private static void QuadRibbon(TownMeshBuilder d,float left,float right,float za,float zb,float ya,float yb,TownColor color)=>d.Quad(new Vector3(left,ya,za),new Vector3(left,yb,zb),new Vector3(right,yb,zb),new Vector3(right,ya,za),color);
    private static void Arrow(TownMeshBuilder d,Vector3 p,float yaw){var q=Quaternion.Euler(0,yaw,0);Vector3 P(float x,float z)=>p+q*new Vector3(x,0,z);d.Quad(P(-.08f,-1),P(-.08f,.5f),P(.08f,.5f),P(.08f,-1),TownColor.White);d.Triangle(P(-.45f,.2f),P(0,1),P(.45f,.2f),TownColor.White);}
    private static void Fence(EquipmentDraft d,Vector3 a,Vector3 b,bool metal)
    {
        var delta=b-a;float length=new Vector2(delta.x,delta.z).magnitude;var rotation=Quaternion.LookRotation(new Vector3(delta.x,0,delta.z));int segments=Mathf.CeilToInt(length/3);
        for(int i=0;i<=segments;i++){var p=Vector3.Lerp(a,b,(float)i/segments);d.Solid("Fence post",p+Vector3.up*.6f,new Vector3(.12f,1.2f,.12f),metal?TownColor.Frame:TownColor.Wood);}
        foreach(float y in new[]{.45f,1f})d.Mesh.Box((a+b)/2+Vector3.up*y,new Vector3(.07f,.09f,delta.magnitude),metal?TownColor.Frame:TownColor.WoodLight,Quaternion.LookRotation(delta));
        // Individual collision spans follow sloped verges instead of a tall rectangular wall through them.
        for(int i=0;i<segments;i++){var p=Vector3.Lerp(a,b,(i+.5f)/segments);var size=Mathf.Abs(delta.x)>Mathf.Abs(delta.z)?new Vector3(length/segments,1.2f,.12f):new Vector3(.12f,1.2f,length/segments);d.Collision("Fence span",p+Vector3.up*.6f,size);}
    }
    private Transform Place(Transform parent,string id,float x,float z,float yaw=0,float y=Floor)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(models[id]);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(x,y,z);go.transform.localRotation=Quaternion.Euler(0,yaw,0);return go.transform;
    }
    private Transform Part(Transform parent,string id,TownMeshBuilder d,bool physical,Material material=null,bool visible=true)
    {
        var mesh=d.Build(id);mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path=Root+"/Meshes/"+id.Replace(" ","")+".asset";TownBakeryPlacement.SaveAsset(mesh,path);var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);var node=Child(parent,id,Vector3.zero);
        if(visible){node.gameObject.AddComponent<MeshFilter>().sharedMesh=saved;node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material??paint;if(material==glass)node.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;}
        if(physical)
        {
            var physicalMesh=saved;
            if(id.Contains("markings"))
            {
                // Raised paint is visual only. A pickup's flat chassis can otherwise snag even a
                // six-millimetre triangle edge while reversing; wheel support comes from the base paving.
                var uv=saved.uv;var indices=saved.triangles;var kept=new List<int>();
                for(int i=0;i<indices.Length;i+=3){int color=Mathf.FloorToInt(uv[indices[i]].x*32);if(color==(int)TownColor.White||color==(int)TownColor.Yellow)continue;kept.Add(indices[i]);kept.Add(indices[i+1]);kept.Add(indices[i+2]);}
                var floor=Object.Instantiate(saved);floor.name=id+" collision without paint";floor.triangles=kept.ToArray();floor.RecalculateBounds();string collisionPath=Root+"/Meshes/"+id.Replace(" ","")+"Collision.asset";TownBakeryPlacement.SaveAsset(floor,collisionPath);physicalMesh=AssetDatabase.LoadAssetAtPath<Mesh>(collisionPath);
            }
            node.gameObject.AddComponent<MeshCollider>().sharedMesh=physicalMesh;
        }
        return node;
    }
    private static void Solids(Transform site,string id,EquipmentDraft d){var root=Child(site,id,Vector3.zero);foreach(var s in d.Solids){var node=Child(root,s.Id,Vector3.zero);var box=node.gameObject.AddComponent<BoxCollider>();box.center=s.Center;box.size=s.Size;}}
    private void Combine(Transform site,string prefix,Func<MeshFilter,bool> include=null)
    {
        var filters=site.GetComponentsInChildren<MeshFilter>(true).Where(f=>include==null||include(f)).ToArray();int index=0;
        foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var mesh=new Mesh{name=prefix+" batch "+index,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=site.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();string path=Root+"/Meshes/"+prefix+"Batch"+index+".asset";TownBakeryPlacement.SaveAsset(mesh,path);
            var node=Child(site,prefix+" visual batch "+index++,Vector3.zero);node.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
        }
        foreach(var f in filters){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
    }
    private static Transform Child(Transform parent,string id,Vector3 p)=>TownBakeryPlacement.Child(parent,id,p);
}
