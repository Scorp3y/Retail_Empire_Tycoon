using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Replaces only the two criticised sites. New buildings and ground composition follow the approved art.</summary>
internal sealed class TownRuralPolishPlacement
{
    internal const float Floor=.15f;
    internal const string Farm="Farm art refinement",Fuel="Fuel station art refinement";
    internal static readonly Rect FarmArea=new(454,244,200,210),FuelArea=TownGatewayPlacement.FuelArea;
    internal static readonly Rect[] Permissions={FarmArea,FuelArea};
    private readonly GameObject town;
    private readonly TownRuralArtCatalogue art=new();
    private readonly TownMeshBuilder farmGround=new(),farmPaint=new(),fuelGround=new(),fuelPaint=new();
    private readonly List<string> audit=new();
    private Transform farm,fuel;
    private int bedNumber;
    internal TownRuralPolishPlacement(GameObject town){this.town=town;}
    internal void Build()
    {
        TownWorldSafety.RequireIsolatedEditor();
        if(town.transform.Find(Farm)!=null||town.transform.Find(Fuel)!=null)throw new InvalidOperationException("Refinement already installed; use recorded original town.");
        foreach(string id in new[]{TownGatewayPlacement.Farm,TownGatewayPlacement.Fuel}){var previous=town.transform.Find(id);if(previous==null)throw new InvalidOperationException("Expected installed site "+id);Object.DestroyImmediate(previous.gameObject);}
        art.Build();ClearLegacyFields();
        farm=TownRuralArtCatalogue.Child(town.transform,Farm,Vector3.zero);fuel=TownRuralArtCatalogue.Child(town.transform,Fuel,Vector3.zero);
        FarmSurfaces();FarmBuildings();FarmWork();FarmLandscape();Fields();FuelSurfaces();FuelBuildings();FuelLandscape();
        art.MeshPart(farm,"Farm driveable base",farmGround,true);art.PaintedPart(farm,"Farm visual ground accents",farmPaint);
        art.MeshPart(fuel,"Fuel driveable base",fuelGround,true);art.PaintedPart(fuel,"Fuel visual ground accents",fuelPaint);
        art.Combine(farm,"FarmRefined");art.Combine(fuel,"FuelRefined");
        File.WriteAllLines(TownRuralPolishBatch.Report+"/placement.txt",audit);
    }
    private Transform Place(Transform parent,string id,float x,float z,float yaw=0,float y=Floor,float scale=1)=>art.Place(parent,id,x,z,yaw,y,scale);
    private void ClearLegacyFields()
    {
        var fields=new Rect(466,354,186,96);int count=0;
        foreach(var f in town.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh=TownBakeryPlacement.CopyWithoutTriangles(f.sharedMesh,f.transform,(a,b,c)=>Inside(fields,a)&&Inside(fields,b)&&Inside(fields,c),out int removed);if(removed==0)continue;
            string path="Meshes/LegacyFieldTrim"+count+++".asset";TownRuralArtCatalogue.Save(mesh,path);f.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(TownRuralArtCatalogue.Root+"/"+path);audit.Add("Replaced "+removed+" original field/fence triangles in "+f.name);
        }
    }
    private void FarmSurfaces()
    {
        // Only the useful collection apron stays asphalt; the former 65 x 42 m slab is gone.
        RoundedGround(farmGround,new Rect(540,281,44,33),2.5f,Floor,TownColor.Asphalt);
        Ribbon(farmGround,new[]{new Vector3(550,Floor,273),new Vector3(550,Floor,290)},8,TownColor.Asphalt);
        Ribbon(farmGround,new[]{new Vector3(546,Floor,270),new Vector3(513,Floor,270),new Vector3(513,Floor,280)},6,TownColor.Asphalt);
        RoundedGround(farmGround,new Rect(504.5f,277,17,11),1,Floor,TownColor.Asphalt);
        RoundedGround(farmGround,new Rect(503,332,40,24),3,Floor,TownColor.Sand);
        RoundedGround(farmGround,new Rect(621,315,22,21),2,Floor,TownColor.Sand);
        Ribbon(farmGround,new[]{new Vector3(580,Floor,299),new Vector3(610,Floor,299),new Vector3(617,Floor,307),new Vector3(617,Floor,340),new Vector3(641,Floor,340)},6,TownColor.Sand);
        Ribbon(farmGround,new[]{new Vector3(527,Floor,286),new Vector3(527,Floor,326),new Vector3(535,Floor,335)},3,TownColor.Sand);
        Ribbon(farmGround,new[]{new Vector3(529,Floor,330),new Vector3(549,Floor,333),new Vector3(578,Floor,333),new Vector3(584,Floor,339),new Vector3(613,Floor,339)},3,TownColor.Sand);
        Ribbon(farmGround,new[]{new Vector3(486,Floor,294),new Vector3(486,Floor,347),new Vector3(504,Floor,347)},2.2f,TownColor.Sand);
        Ribbon(farmGround,new[]{new Vector3(466.5f,Floor,322),new Vector3(504,Floor,322),new Vector3(509,Floor,329)},2,TownColor.Sand);
        Ribbon(farmGround,new[]{new Vector3(504,Floor,298),new Vector3(504,Floor,293),new Vector3(523,Floor,293),new Vector3(527,Floor,300)},2.2f,TownColor.Pavement);
        Ribbon(farmGround,new[]{new Vector3(531,Floor,351),new Vector3(538,Floor,359),new Vector3(649,Floor,359)},4,TownColor.Sand);
        Ribbon(farmGround,new[]{new Vector3(538,Floor,359),new Vector3(538,Floor,450)},5,TownColor.Sand);
        RoundedGround(farmGround,new Rect(540.5f,358,3.5f,7),.5f,Floor,TownColor.Sand);
        farmGround.Ground(628,348,24,10,Floor,TownColor.Pavement);
        farmGround.Ground(485,269.5f,34,37,Floor-.02f,TownColor.Grass);
        for(int i=0;i<4;i++)farmPaint.Ground(505.5f+i*4.8f,282,.12f,6,Floor+.008f,TownColor.White);
        foreach(float x in new[]{557f,563f})farmPaint.Ground(x,305,.10f,14,Floor+.008f,TownColor.Yellow);foreach(float z in new[]{298f,312f})farmPaint.Ground(560,z,6,.10f,Floor+.008f,TownColor.Yellow);
        Arrow(farmPaint,new Vector3(550,Floor+.008f,283),0);Arrow(farmPaint,new Vector3(577,Floor+.008f,289),90);
        // Low-contrast gravel and paving detail is non-colliding, never a stack of physical decals.
        var random=new System.Random(1405);for(int i=0;i<150;i++){float x=622+(float)random.NextDouble()*20,z=316+(float)random.NextDouble()*19;farmPaint.Box(new Vector3(x,Floor+.004f,z),new Vector3(.07f,.008f,.06f),i%2==0?TownColor.Pavement:TownColor.Stone);}
        audit.Add("Collection apron reduced from 2730 to 1452 square metres (excluding the retained road); gravel/pedestrian ribbons connect seven farm zones.");
    }
    private void FarmBuildings()
    {
        Place(farm,"PackingBarn",560,321,180);
        Place(farm,"FarmHouse",535,343,180);Place(farm,"FarmHouseDress",535,343,180);
        Place(farm,"RedBarn",514,343,180);Place(farm,"BarnDress",514,343,180);
        Place(farm,"HayShelter",507,327,180);foreach(float x in new[]{504f,507f,510f})Place(farm,"HayBaleDetailed",x,327,90);
        Place(farm,"Workshop",630,326,180);
        Place(farm,"DetailedTractor",627.5f,315,180);Place(farm,"FarmTrailerDetailed",627.5f,321,180);
        foreach(float x in new[]{623f,634f})Place(farm,"GrainSiloDetailed",x,348);
        foreach(float x in new[]{590f,604f})Place(farm,"Greenhouse",x,342,180);
        var bridge=new EquipmentDraft();bridge.Box(new Vector3(628.5f,14.17f,348),new Vector3(8.7f,.12f,1.1f),TownColor.Metal);
        foreach(float z in new[]{347.4f,348.6f}){TownRuralDetailModels.Beam(bridge,new Vector3(624.3f,15, z),new Vector3(632.7f,15,z),.06f,TownColor.Frame);for(float x=624.3f;x<633;x+=1.1f)bridge.Box(new Vector3(x,14.57f,z),new Vector3(.05f,.8f,.05f),TownColor.Frame);}
        art.MeshPart(farm,"Grain silo upper walkway",bridge.Mesh);
        Place(farm,"FarmMarket",513,295,180);Place(farm,"MarketHarvestDisplay",513,295,180);Place(farm,"MarketPriceBoard",507,291,180);Place(farm,"MarketPriceBoard",519,291,180);
        var station=town.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Farmers);station.transform.position=new Vector3(560,.17f,305);station.transform.rotation=Quaternion.Euler(0,180,0);
        var court=town.GetComponentsInChildren<SupplierLoadingCourt>().Single(c=>c.checkpoint==station);court.bounds=new Rect(540,281,44,33);court.streetAccess=new Vector3(550,0,272);court.entrance=new Vector3(550,Floor,283);court.staging=new Vector3(560,Floor,289);court.approachWaypoints=new[]{new Vector3(550,Floor,289)};court.designVehicleLength=6.8f;
    }
    private void FarmWork()
    {
        foreach(float x in new[]{549f,553f,568f,572f})Place(farm,"ProduceStand",x,310,180);
        foreach(float x in new[]{549f,571f}){Place(farm,"ProduceStand",x,323,180);Place(farm,"SeedSack",x,326);Place(farm,"WoodBarrel",x+1.4f,326);}
        foreach(float x in new[]{541.5f,578.5f}){Place(farm,"PalletRack",x,321,180);Place(farm,"HarvestPallet",x,312);Place(farm,"HarvestPallet",x,308);Place(farm,"PalletStack",x,304);}
        Place(farm,"Forklift",579,296,90);Place(farm,"FarmVan",572,286,0);
        Place(farm,"ToolWall",635,332.2f,180);Place(farm,"HandToolsAndSpares",625,330);Place(farm,"DetailedWheelbarrow",641,326,0);
        Place(farm,"HandSpade",626,332,180);Place(farm,"HandRake",627,332,180);Place(farm,"BucketDetailed",628,331.5f);
        foreach(float z in new[]{320f,321.5f,323}){Place(farm,"SeedSack",639,z,0);Place(farm,"WoodBarrel",641,z,90);}
        Place(farm,"FuelTank",644,313);Place(farm,"ControlCabinet",645,318);Place(farm,"FireCabinet",644,310,180);
        foreach(float x in new[]{584f,598f}){Place(farm,"GreenhouseWorkstation",x,338,90);Place(farm,"WateringCan",x,337);Place(farm,"BucketDetailed",x+1,337);Place(farm,"IrrigationStation",x,345);}
        Place(farm,"CompostBay",606,316);Place(farm,"CompostBay",606,320);Place(farm,"DetailedWheelbarrow",608,325,90);
        foreach(float x in new[]{584f,598f,611f}){Place(farm,"HarvestPallet",x,332);Place(farm,"OrchardBasket",x+1.2f,332);}
        foreach(float x in new[]{508f,513f,518f})Place(farm,"WheelStop",x,284.6f);
        Place(farm,"CoffeeMenuBoard",522,291,180);Place(farm,"LitterBin",523,288);Place(farm,"BicycleRack",503,291,90);Place(farm,"SquareBench",503,297,90);
        Place(farm,"AnimalShelter",476,283,180);Place(farm,"ChickenCoop",497,282,180);Place(farm,"FeedTrough",475,265);Place(farm,"FeedTrough",493,263);
        foreach(var p in new[]{new Vector2(479,273),new Vector2(488,278),new Vector2(493,269),new Vector2(483,259)})Place(farm,"Cow",p.x,p.y,p.x*19);
        for(int i=0;i<7;i++)Place(farm,i==6?"Rooster":"Hen",495+i%3,276-i/3*1.5f,i*47);
        foreach(float x in new[]{474f,478f,482f})Place(farm,"HayBaleDetailed",x,285,90);
        Place(farm,"RecyclingStation",581,311,270);Place(farm,"DetailedTractor",542.3f,362,0);
        foreach(float z in new[]{310f,320f,330f,340f})Place(farm,"OrchardBasket",468,z);
        foreach(var p in new[]{new Vector2(543,329),new Vector2(611,328),new Vector2(579,282),new Vector2(526,289),new Vector2(621,341)})Place(farm,"SiteLamp",p.x,p.y);
        audit.Add("Pitched timber pickup barn with attached produce veranda; detailed authored tractor, coupled trailer, silo catwalk, workshop tool wall, greenhouse potting/irrigation clusters and dressed market.");
    }
    private void FarmLandscape()
    {
        var fence=new EquipmentDraft();Fence(fence,new Vector3(468,Floor,251),new Vector3(502,Floor,251));Fence(fence,new Vector3(468,Floor,251),new Vector3(468,Floor,288));Fence(fence,new Vector3(468,Floor,288),new Vector3(502,Floor,288));Fence(fence,new Vector3(502,Floor,251),new Vector3(502,Floor,270));Fence(fence,new Vector3(502,Floor,275),new Vector3(502,Floor,288));
        Fence(fence,new Vector3(540,Floor,280),new Vector3(543.8f,Floor,280));Fence(fence,new Vector3(556.2f,Floor,280),new Vector3(584,Floor,280));Fence(fence,new Vector3(584,Floor,280),new Vector3(584,Floor,293));Fence(fence,new Vector3(584,Floor,305),new Vector3(584,Floor,314));
        Fence(fence,new Vector3(503,Floor,355.8f),new Vector3(532,Floor,355.8f));
        art.MeshPart(farm,"Farm boundary and gate frames",fence.Mesh);AddSolids(farm,"Farm fence collision",fence);
        // Broad mixed planting beds, not identical boxes evenly scattered over the asphalt.
        Bed(farm,new Rect(523,277,17,5),true,0);Bed(farm,new Rect(557,275.5f,24,4),false,1);
        Bed(farm,new Rect(533,315,5,14),true,2);Bed(farm,new Rect(584,306,15,8),true,3);Bed(farm,new Rect(613,344,5,10),true,4);
        Bed(farm,new Rect(525,334,4,16),false,5);Bed(farm,new Rect(542,338,4,15),false,6);Bed(farm,new Rect(500,300,4,18),false,7);
        Bed(farm,new Rect(468,291,33,3),false,8);Bed(farm,new Rect(468,348,31,4),false,9);
        for(int x=0;x<4;x++)for(int z=0;z<5;z++)Place(farm,"FruitTree",471+x*9,300+z*10,x*31+z*43,Floor,1.05f+(x%2)*.08f);
        var garden=new TownMeshBuilder();for(int i=0;i<4;i++){garden.Ground(541+i,352,.6f,5,Floor+.006f,TownColor.Soil);for(int j=0;j<5;j++)Place(farm,i%2==0?"FieldLeafClump":"FieldRootClump",541+i,350+j,0,Floor+.01f,.8f);}art.MeshPart(farm,"Farmhouse kitchen beds",garden);
        foreach(var p in new[]{new Vector2(505,289),new Vector2(520,291),new Vector2(536,333),new Vector2(544,354),new Vector2(645,338)})Place(farm,"FlowerPot",p.x,p.y);
        // Light meadow edge around the pasture keeps the centre readable for livestock.
        for(int i=0;i<20;i++){float x=469+i*1.5f;Place(farm,"MeadowClump",x,252.2f,0,Floor,1.2f);if(i%3==0)Place(farm,"FlowerCluster",x,252.5f,0,Floor,.6f);}
    }
    private void Fields()
    {
        Field(new Rect(471,366,61,81),"FieldWheatClump",1.7f,1.8f,1.3f,401);
        Field(new Rect(544,366,58,81),"FieldLeafClump",1.6f,2.0f,1.25f,402);
        Field(new Rect(614,366,35,81),"FieldRootClump",1.5f,1.6f,1.2f,403);
        foreach(var p in new[]{new Vector2(474,363),new Vector2(548,363),new Vector2(640,363)}){Place(farm,"HarvestPallet",p.x,p.y);Place(farm,"SeedSack",p.x+1.4f,p.y);}
    }
    private void Field(Rect bounds,string crop,float row,float spacing,float scale,int seed)
    {
        var soil=new TownMeshBuilder();soil.Ground(bounds.center.x,bounds.center.y,bounds.width,bounds.height,.035f,TownColor.Soil);
        var random=new System.Random(seed);int number=0;
        for(float x=bounds.xMin+2;x<bounds.xMax-2;x+=row)
        {
            soil.Ground(x,bounds.center.y,row*.64f,bounds.height-4,.04f,TownColor.BoxDark);
            for(float z=bounds.yMin+2;z<bounds.yMax-2;z+=spacing){Place(farm,crop,x+(float)random.NextDouble()*.14f-.07f,z+(float)random.NextDouble()*.2f-.1f,random.Next(-12,13),.045f,scale*(.9f+(float)random.NextDouble()*.2f));number++;}
        }
        art.MeshPart(farm,crop+" soil and furrows",soil);
        var fence=new EquipmentDraft();Fence(fence,new Vector3(bounds.xMin,0,bounds.yMin),new Vector3(bounds.center.x-3,0,bounds.yMin));Fence(fence,new Vector3(bounds.center.x+3,0,bounds.yMin),new Vector3(bounds.xMax,0,bounds.yMin));Fence(fence,new Vector3(bounds.xMin,0,bounds.yMin),new Vector3(bounds.xMin,0,bounds.yMax));Fence(fence,new Vector3(bounds.xMax,0,bounds.yMin),new Vector3(bounds.xMax,0,bounds.yMax));Fence(fence,new Vector3(bounds.xMin,0,bounds.yMax),new Vector3(bounds.xMax,0,bounds.yMax));art.MeshPart(farm,crop+" field fence",fence.Mesh);AddSolids(farm,crop+" boundary collision",fence);
        audit.Add(crop+": "+number+" varied clumps; 6 m field gate and unobstructed headland, distinct soil/furrow bands.");
    }
    private void FuelSurfaces()
    {
        RoundedGround(fuelGround,new Rect(185,-128,60,36),3,Floor,TownColor.Asphalt);
        foreach(float x in new[]{204f,228f})RoundedGround(fuelGround,new Rect(x-4.5f,-140,9,17),.6f,Floor,TownColor.Asphalt);
        RoundedGround(fuelGround,new Rect(177,-103.5f,31,17),1.2f,Floor,TownColor.Pavement);
        RoundedGround(fuelGround,new Rect(185,-118,20,13),1,Floor,TownColor.Asphalt);
        RoundedGround(fuelGround,new Rect(239,-103,14,15),1,Floor,TownColor.Pavement);
        for(int i=0;i<4;i++)fuelPaint.Ground(187.5f+i*4.8f,-111,.12f,6.5f,Floor+.008f,TownColor.White);fuelPaint.Ground(194.7f,-107.75f,14.4f,.12f,Floor+.008f,TownColor.White);
        foreach(float x in new[]{240f,246.5f})fuelPaint.Ground(x,-97,.12f,7,Floor+.008f,TownColor.White);fuelPaint.Ground(243.25f,-93.5f,6.5f,.12f,Floor+.008f,TownColor.White);
        foreach(var p in new[]{(228f,-132f,0f),(238f,-123f,0f),(238f,-104f,270f),(224f,-104f,180f),(224f,-123f,270f),(204f,-132f,180f)})Arrow(fuelPaint,new Vector3(p.Item1,Floor+.008f,p.Item2),p.Item3);
        // A separate pavement strip reaches the shop; visitors do not walk between fuel islands.
        fuelPaint.Ground(197,-104,2.2f,5,Floor+.008f,TownColor.Pavement);
        var kerb=new TownMeshBuilder();foreach(var rect in new[]{new Rect(177,-137,22,10),new Rect(209,-137,13,8),new Rect(234,-137,20,10),new Rect(177,-124,7,19),new Rect(209,-90,29,5)})Kerb(kerb,rect,1.2f,Floor);
        art.MeshPart(fuel,"Fuel curved garden kerbs",kerb);
        audit.Add("Fuel asphalt reduced to a compact manoeuvring court, with broad front islands and rear/side garden buffers; exactly two existing street connections retained.");
    }
    private void FuelBuildings()
    {
        Place(fuel,"FuelShopDetailed",197,-96,180);Place(fuel,"FuelCanopyDetailed",224,-114);
        foreach(float x in new[]{219f,229f})
        {
            Place(fuel,"PumpIsland",x,-114);foreach(float z in new[]{-117f,-111f})Place(fuel,"FuelPumpDetailed",x,z,0,Floor+.24f);
            foreach(float z in new[]{-118.5f,-109.5f})Place(fuel,"SafetyBollard",x,z,0,Floor+.24f);
            Place(fuel,"ServiceKit",x,-118.65f,0,Floor+.24f);
        }
        Place(fuel,"FuelPylonDetailed",245,-131,180);Place(fuel,"AirWaterStation",250,-96,270);
        foreach(float z in new[]{-99.7f,-92.3f})Place(fuel,"SafetyBollard",248,z);
        Place(fuel,"ControlCabinet",251,-88,180);Place(fuel,"FireCabinet",209,-96,180);
        Place(fuel,"CheckoutCounter",200,-96,180);Place(fuel,"CoffeeService",193,-92,180);Place(fuel,"DisplayFridge",201,-92,180);Place(fuel,"RetailShelf",193,-98,90);Place(fuel,"CarGoods",201,-98,270);Place(fuel,"BasketStack",198,-100);
        Place(fuel,"BlueCustomerCar",190,-111);Place(fuel,"GreenCustomerCar",195,-111);foreach(float x in new[]{190f,195f,200f})Place(fuel,"WheelStop",x,-108.2f);
        Place(fuel,"BlueCustomerCar",214,-116,180);Place(fuel,"GreenCustomerCar",234,-113,180);
        foreach(float z in new[]{-91f,-98f})Place(fuel,"UmbrellaTableSet",181,z);Place(fuel,"CoffeeMenuBoard",187,-100,180);Place(fuel,"BicycleRack",186,-88,90);Place(fuel,"SquareBench",185,-102,180);
        Place(fuel,"RecyclingStation",180,-105,180);Place(fuel,"LitterBin",208,-102);Place(fuel,"LitterBin",247,-102);
        foreach(var p in new[]{new Vector2(207,-124),new Vector2(235,-124),new Vector2(244,-104),new Vector2(190,-120)})Place(fuel,"Drain",p.x,p.y);
        foreach(var p in new[]{new Vector2(198,-103),new Vector2(186,-93),new Vector2(252,-104),new Vector2(210,-136),new Vector2(237,-136),new Vector2(252,-123)})Place(fuel,"SiteLamp",p.x,p.y);
        foreach(float x in new[]{192f,202f})Place(fuel,"FlowerPot",x,-102);
        audit.Add("Fuel: layered fascia and ribbed canopy, four detailed dual-face pumps with curved hoses, windscreen-cleaning kits, full air/water station, coffee counter, service goods, terrace and parking wheel stops.");
    }
    private void FuelLandscape()
    {
        Bed(fuel,new Rect(178,-136,20,8),true,20);Bed(fuel,new Rect(210,-136,11,7),true,21);Bed(fuel,new Rect(235,-136,18,8),false,22);
        Bed(fuel,new Rect(178,-123,6,17),true,23);Bed(fuel,new Rect(209,-89.5f,29,4.8f),true,24);Bed(fuel,new Rect(253,-125,3,33),false,25);
        Bed(fuel,new Rect(238,-88.8f,15,4),false,26);
        foreach(var p in new[]{new Vector2(178,-87),new Vector2(206,-88),new Vector2(234,-88),new Vector2(253,-127)})Place(fuel,"FlowerClusterPink",p.x,p.y,0,Floor,1.4f);
    }
    private void Bed(Transform site,Rect rect,bool tree,int seed)
    {
        var d=new TownMeshBuilder();float radius=Mathf.Min(1,Mathf.Min(rect.width,rect.height)*.2f);RoundedGround(d,rect,radius,Floor+.01f,TownColor.Soil);
        var outline=Rounded(rect,radius);for(int i=0;i<outline.Length;i++){var a=outline[i];var b=outline[(i+1)%outline.Length];d.Quad(new Vector3(a.x,0,a.y),new Vector3(a.x,Floor+.01f,a.y),new Vector3(b.x,Floor+.01f,b.y),new Vector3(b.x,0,b.y),TownColor.Soil);}
        art.MeshPart(site,"Mixed planting bed "+bedNumber++,d,true);
        var random=new System.Random(8921+seed);int count=Mathf.CeilToInt((rect.width+rect.height)*1.2f);
        for(int i=0;i<count;i++)
        {
            float x=rect.xMin+.7f+(float)random.NextDouble()*Mathf.Max(.1f,rect.width-1.4f);float z=rect.yMin+.7f+(float)random.NextDouble()*Mathf.Max(.1f,rect.height-1.4f);
            if(tree&&Vector2.Distance(new Vector2(x,z),rect.center)<1.3f)continue;
            Place(site,i%3==0?"MixedShrub":i%2==0?"FlowerCluster":"FlowerClusterPink",x,z,random.Next(360),Floor+.012f,i%3==0?.7f:.65f+random.Next(30)*.01f);
        }
        if(tree)Place(site,"SquareTree",rect.center.x,rect.center.y-(site==fuel&&seed==24?1.2f:0),seed*19,Floor+.01f,rect.width<8?.8f:1.1f);
    }
    private static void AddSolids(Transform site,string name,EquipmentDraft d){var node=TownRuralArtCatalogue.Child(site,name,Vector3.zero);foreach(var s in d.Solids)TownRuralArtCatalogue.Box(node,s.Id,s.Center,s.Size);}
    private static void Fence(EquipmentDraft d,Vector3 a,Vector3 b)
    {
        var delta=b-a;int n=Mathf.CeilToInt(delta.magnitude/3);bool horizontal=Mathf.Abs(delta.x)>Mathf.Abs(delta.z);
        for(int i=0;i<=n;i++){var p=Vector3.Lerp(a,b,i/(float)n);d.Solid("Fence post",p+Vector3.up*.65f,new Vector3(.14f,1.3f,.14f),TownColor.Wood);}
        foreach(float y in new[]{.45f,.95f})TownRuralDetailModels.Beam(d,a+Vector3.up*y,b+Vector3.up*y,.10f,TownColor.WoodLight);
        d.Collision("Fence rail span",(a+b)/2+Vector3.up*.6f,horizontal?new Vector3(delta.magnitude,1.2f,.10f):new Vector3(.10f,1.2f,delta.magnitude));
    }
    internal static bool Inside(Rect rect,Vector3 p)=>TownDairyPlacement.Inside(rect,p);
    private static void Arrow(TownMeshBuilder d,Vector3 p,float yaw){var q=Quaternion.Euler(0,yaw,0);Vector3 P(float x,float z)=>p+q*new Vector3(x,0,z);d.Quad(P(-.08f,-1),P(-.08f,.4f),P(.08f,.4f),P(.08f,-1),TownColor.White);d.Triangle(P(-.45f,.2f),P(0,1),P(.45f,.2f),TownColor.White);}
    internal static void Ribbon(TownMeshBuilder d,Vector3[] points,float width,TownColor color)
    {
        for(int i=1;i<points.Length;i++){var a=points[i-1];var b=points[i];var side=Vector3.Cross(Vector3.up,b-a).normalized*width/2;d.Quad(a-side,b-side,b+side,a+side,color);}
        foreach(var p in points)for(int i=0;i<12;i++){float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;d.Triangle(p,p+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*width/2,p+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*width/2,color);}
    }
    private static Vector2[] Rounded(Rect r,float radius)
    {
        radius=Mathf.Min(radius,Mathf.Min(r.width,r.height)/2);var points=new List<Vector2>();
        foreach(var corner in new[]{(r.xMax-radius,r.yMax-radius,0f),(r.xMin+radius,r.yMax-radius,90f),(r.xMin+radius,r.yMin+radius,180f),(r.xMax-radius,r.yMin+radius,270f)})
            for(int i=0;i<=5;i++){float angle=(corner.Item3+i*18)*Mathf.Deg2Rad;points.Add(new Vector2(corner.Item1+Mathf.Cos(angle)*radius,corner.Item2+Mathf.Sin(angle)*radius));}
        return points.ToArray();
    }
    internal static void RoundedGround(TownMeshBuilder d,Rect rect,float radius,float y,TownColor color){var p=Rounded(rect,radius);var c=new Vector3(rect.center.x,y,rect.center.y);for(int i=0;i<p.Length;i++){var a=p[i];var b=p[(i+1)%p.Length];d.Triangle(c,new Vector3(b.x,y,b.y),new Vector3(a.x,y,a.y),color);}}
    private static void Kerb(TownMeshBuilder d,Rect rect,float radius,float y)
    {
        var points=Rounded(rect,radius);for(int i=0;i<points.Length;i++){var a=points[i];var b=points[(i+1)%points.Length];var delta=b-a;d.Box(new Vector3((a.x+b.x)/2,y+.06f,(a.y+b.y)/2),new Vector3(.14f,.12f,delta.magnitude+.02f),TownColor.Pavement,Quaternion.LookRotation(new Vector3(delta.x,0,delta.y)));}
    }
}
