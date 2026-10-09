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

/// <summary>Authors one shared town prefab. Models, physics and street layout have separate responsibilities.</summary>
internal sealed partial class TownWorldAssembler
{
    public const string Folder="Assets/Prefabs/TownWorld";
    public const string PrefabPath=Folder+"/TownEnvironment.prefab";
    private readonly TownWorldLayout layout;
    private readonly Material material;
    private readonly GameObject root;
    private readonly List<DeliveryCheckpoint> suppliers=new List<DeliveryCheckpoint>();
    private readonly Dictionary<string,GameObject> models=new Dictionary<string,GameObject>();
    private readonly Dictionary<string,Transform> sections=new Dictionary<string,Transform>();
    private readonly List<Rect> pavedAreas=new List<Rect>();
    private readonly List<Rect> buildingAreas=new List<Rect>();

    public TownWorldAssembler(TownWorldLayout layout)
    {
        this.layout=layout;
        material=TownWorldArtDirection.Palette();
        if(material==null)throw new InvalidOperationException("TownKit palette material is missing.");
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Art/TownKit/Prefabs"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);models.Add(Path.GetFileNameWithoutExtension(path),AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }
        root=new GameObject("Town Environment");
    }

    public GameObject Build()
    {
        try
        {
            Terrain();Roads();SupplierSites();Residential();TownCentre();Rural();Park();Streets();Outskirts();Streetscape();ExpansionScenery();
            var places=root.AddComponent<TownWorldPlaces>();places.layout=layout;places.suppliers=suppliers.ToArray();
            CombineVisualChunks();
            return PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally {Object.DestroyImmediate(root);}
    }

    private Transform Section(string name)
    {
        if(sections.TryGetValue(name,out var section))return section;
        section=new GameObject(name).transform;section.SetParent(root.transform,false);sections.Add(name,section);return section;
    }
    private GameObject Model(string id,Vector3 position,float yaw=0,string section="Details",Vector3? scale=null)
    {
        if(!models.TryGetValue(id,out var prefab))throw new InvalidOperationException("Town model missing: "+id);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        instance.name=id;instance.transform.SetParent(Section(section),false);
        instance.transform.localPosition=position;instance.transform.localRotation=Quaternion.Euler(0,yaw,0);
        if(scale.HasValue)instance.transform.localScale=scale.Value;
        if(id.StartsWith("Tree",StringComparison.Ordinal)&&id!="TreeBelt"||id=="Pine")
        {
            var trunk=instance.AddComponent<CapsuleCollider>();trunk.center=Vector3.up*.9f;trunk.height=1.8f;trunk.radius=.2f;
        }
        if(id=="WarehouseRack"||id=="BoxTruck"||id=="ForkliftStatic"||section=="Parked cars"||id=="DisplayCar")
        {
            var bounds=instance.GetComponent<MeshFilter>().sharedMesh.bounds;BoxCollider(instance,bounds.center,bounds.size);
        }
        if(id=="LoadingCanopy")
        {
            foreach(float x in new[]{-4f,4f})foreach(float z in new[]{-3f,3f})BoxCollider(instance,new Vector3(x,1.85f,z),new Vector3(.2f,3.7f,.2f));
            BoxCollider(instance,new Vector3(0,3.95f,0),new Vector3(8.7f,.5f,6.7f));
        }
        return instance;
    }
    private GameObject Geometry(string name,TownMeshBuilder geometry,string section)
    {
        var instance=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));instance.transform.SetParent(Section(section),false);
        instance.GetComponent<MeshFilter>().sharedMesh=geometry.Build(name);instance.GetComponent<MeshRenderer>().sharedMaterial=material;
        return instance;
    }
    private void Surface(string name,Rect rect,TownColor color,float y=.025f)
    {
        var mesh=new TownMeshBuilder();mesh.Ground(rect.center.x,rect.center.y,rect.width,rect.height,y,color);Geometry(name,mesh,"Ground and courtyards");
        if(color==TownColor.Asphalt||color==TownColor.Pavement)pavedAreas.Add(rect);
    }
    private void BoxCollider(GameObject instance,Vector3 center,Vector3 size)
    {
        var collider=instance.AddComponent<BoxCollider>();collider.center=center;collider.size=size;
    }
    private GameObject Building(string id,Vector3 position,float yaw,Vector3 bodySize,string district)
    {
        var instance=Model(id,position,yaw,district);
        BackFacades(instance,id,bodySize);
        ArchitecturalDetail(instance,id);
        BoxCollider(instance,new Vector3(0,bodySize.y/2,0),bodySize);
        bool side=Mathf.RoundToInt(yaw/90)%2!=0;
        float width=side?bodySize.z:bodySize.x,depth=side?bodySize.x:bodySize.z;
        buildingAreas.Add(new Rect(position.x-width/2-3,position.z-depth/2-3,width+6,depth+6));
        return instance;
    }
    private void BackFacades(GameObject building,string id,Vector3 size)
    {
        if(id=="FarmersMarket")return;
        var geometry=new TownMeshBuilder();
        int levels=id=="ApartmentSmall"?4:id=="Townhouse"?2:1;
        int rearCount=Mathf.Max(2,Mathf.FloorToInt(size.x/3));
        for(int level=0;level<levels;level++)
        {
            float y=levels==1?2.15f:1.7f+level*2.5f;
            for(int i=0;i<rearCount;i++)
            {
                float x=(i-(rearCount-1)*.5f)*Mathf.Min(3,size.x/(rearCount+1));
                geometry.Box(new Vector3(x,y,-size.z/2-.025f),new Vector3(1.2f,1.5f,.09f),TownColor.Cream);
                geometry.Box(new Vector3(x,y,-size.z/2-.08f),new Vector3(.95f,1.2f,.025f),TownColor.Glass);
            }
            for(int i=0;i<2;i++)
            {
                float z=(i-.5f)*size.z/2;
                geometry.Box(new Vector3(-size.x/2-.025f,y,z),new Vector3(.09f,1.5f,1.2f),TownColor.Cream);
                geometry.Box(new Vector3(-size.x/2-.08f,y,z),new Vector3(.025f,1.2f,.95f),TownColor.Glass);
            }
        }
        var windows=Geometry("Rear and side windows",geometry,"Facade details");windows.transform.SetParent(building.transform,false);
    }
    private void Terrain()
    {
        var b=layout.cityBounds;var mesh=new TownMeshBuilder();
        mesh.Ground(b.center.x,b.center.y,b.width+240,b.height+240,-.06f,TownColor.Grass);
        mesh.Box(new Vector3(b.center.x,-.35f,b.center.y),new Vector3(b.width,.7f,b.height),TownColor.Grass);
        var ground=Geometry("Town ground",mesh,"Ground and courtyards");BoxCollider(ground,new Vector3(b.center.x,-.35f,b.center.y),new Vector3(b.width,.7f,b.height));
        // Invisible edge physics is hidden behind the tree belt, not a wall through the town centre.
        foreach(bool vertical in new[]{true,false})foreach(int side in new[]{-1,1})
        {
            var edge=new GameObject("World edge");edge.transform.SetParent(Section("World boundaries"),false);
            var center=vertical?new Vector3(side<0?b.xMin:b.xMax,2,b.center.y):new Vector3(b.center.x,2,side<0?b.yMin:b.yMax);
            BoxCollider(edge,center,vertical?new Vector3(1,4,b.height):new Vector3(b.width,4,1));
        }
        // Two paved accesses serve the player's existing loading wall and front parking yard.
        Surface("Store front visitor lane",new Rect(-104,-133,100,9),TownColor.Asphalt);
        Surface("Store west service lane",new Rect(-87,-128,8,172),TownColor.Asphalt);
        Surface("Forecourt connection",new Rect(-124,-133,28,9),TownColor.Asphalt);
        Surface("Warehouse loading apron",new Rect(-86,-91,10,16),TownColor.Asphalt);
    }
    private void Roads()
    {
        foreach(var road in layout.roads)
        {
            int mask=road.connections,count=Enumerable.Range(0,4).Count(i=>(mask&(1<<i))!=0);
            string id=count==4?"RoadCross":count==3?"RoadTee":mask==5||mask==10?"RoadStraight":"RoadCorner";
            int nativeMask=id=="RoadCross"?15:id=="RoadTee"?14:id=="RoadStraight"?5:3;
            int turn=0;while(turn<4&&RotateMask(nativeMask,turn)!=mask)turn++;
            if(turn==4)throw new InvalidOperationException("Unsupported road shape: "+road.cell);
            Model(id,road.Position(layout.roadTileSize)+Vector3.up*.01f,turn*90,"Road network");
            if(count<3)continue;
            var p=road.Position(layout.roadTileSize);
            foreach(int direction in Enumerable.Range(0,4).Where(i=>(mask&(1<<i))!=0))
            {
                var step=TownWorldPlan.Steps[direction];
                var offset=new Vector3(step.x,0,step.y)*8.5f;
                Model("Crosswalk",p+offset+Vector3.up*.055f,direction*90,"Road markings");
            }
        }
    }
    private static int RotateMask(int mask,int turns)=>((mask<<turns)|(mask>>(4-turns)))&15;

    private void SupplierSites()
    {
        Site("WholesaleBase",new Vector3(-168,0,-20),180,new Vector3(20,5.6f,13),SupplierKind.Wholesale,new Vector3(-168,0,-47));
        Site("EquipmentSupplier",new Vector3(-168,0,100),180,new Vector3(14,4.5f,9),SupplierKind.Equipment,new Vector3(-168,0,77));
        Site("ConstructionSupplier",new Vector3(-168,0,180),180,new Vector3(15,4.5f,10),SupplierKind.Construction,new Vector3(-168,0,157));
        Site("Bakery",new Vector3(-70,0,74),180,new Vector3(8,4.5f,6),SupplierKind.Bakery,new Vector3(-70,0,57));
        Site("FlowerShop",new Vector3(120,0,120),0,new Vector3(8,4.5f,6),SupplierKind.Decoration,new Vector3(120,0,134));
        Site("FarmersMarket",new Vector3(216,0,96),180,new Vector3(12,3.5f,6),SupplierKind.Farmers,new Vector3(216,0,70));
        Site("CarDealership",new Vector3(216,0,-92),90,new Vector3(16,4.5f,10),SupplierKind.Showroom,new Vector3(237,0,-92));
        Building("WholesaleBase",new Vector3(-193,0,195),180,new Vector3(20,5.6f,13),"Industrial yards");
        Building("WholesaleBase",new Vector3(-193,0,-99),180,new Vector3(20,5.6f,13),"Industrial yards");
        Building("EquipmentSupplier",new Vector3(-149,0,-99),180,new Vector3(14,4.5f,9),"Industrial yards");
        Surface("Freight courtyard",new Rect(-207,-130,77,42),TownColor.Pavement);
        Model("BoxTruck",new Vector3(-178,0,-121),90,"Industrial yards");
        Model("CargoContainer",new Vector3(-199,0,-122),0,"Industrial yards");
        Model("CargoContainer",new Vector3(-192,0,-25),90,"Industrial yards");
        Model("CargoContainer",new Vector3(-198,0,-25),90,"Industrial yards");
        Model("BoxTruck",new Vector3(-195,0,174),0,"Industrial yards");
        Model("ForkliftStatic",new Vector3(-151,0,-33),180,"Industrial yards");
        foreach(float z in new[]{-16f,102f,188f})
        {
            Surface("Service yard",new Rect(-207,z-15,77,30),TownColor.Pavement);
            Model("WasteContainer",new Vector3(-202,0,z+8),0,"Industrial yards");
            for(int i=0;i<3;i++)Model("Pallet",new Vector3(-202+i*1.6f,0,z+11),0,"Industrial yards");
        }
        foreach(float z in new[]{-3f,82f,123f,170f})
        {
            Building("EquipmentSupplier",new Vector3(-195,0,z),180,new Vector3(14,4.5f,9),"Industrial yards");
            Model("CargoContainer",new Vector3(-202,0,z-9),0,"Industrial yards");
            Model("Pallet",new Vector3(-184,0,z-7),0,"Industrial yards");
        }
        foreach(float x in new[]{230f,238f,246f})
        {
            Model("DisplayCar",new Vector3(x,0,-114),0,"Showroom forecourt");
            Model("ParkingBay",new Vector3(x,0,-114),0,"Showroom forecourt");
        }
        Surface("Showroom forecourt",new Rect(222,-122,34,53),TownColor.Asphalt);
    }
    private void Site(string model,Vector3 position,float yaw,Vector3 bodySize,SupplierKind supplier,Vector3 checkpointPosition)
    {
        var supplierBuilding=Building(model,position,yaw,bodySize,"District suppliers");
        supplierBuilding.transform.localScale=Vector3.one*(supplier==SupplierKind.Wholesale?1.3f:1.4f);
        var sign=Model(supplier==SupplierKind.Showroom?"ParkingSign":"PickupSign",checkpointPosition+Vector3.right*5,yaw,"Supplier signs");
        var marker=new GameObject(SupplierRouting.Name(supplier));marker.transform.SetParent(Section("Supplier checkpoints"),false);
        marker.transform.localPosition=checkpointPosition+Vector3.up*.055f;
        var checkpoint=marker.AddComponent<DeliveryCheckpoint>();checkpoint.supplier=supplier;checkpoint.radius=4;
        checkpoint.kind=supplier==SupplierKind.Showroom?CheckpointKind.Showroom:CheckpointKind.Depot;
        checkpoint.displayName=SupplierRouting.Name(supplier);suppliers.Add(checkpoint);
        Model("PickupStopZone",checkpointPosition+Vector3.up*.055f,0,"Loading bays");
        Surface("Supplier apron",new Rect(checkpointPosition.x-9,checkpointPosition.z-7,18,14),TownColor.Asphalt,.045f);
        Driveway(checkpointPosition,TownWorldPlan.NearestRoad(layout,checkpointPosition));
        if(supplier==SupplierKind.Showroom)return;
        Model("LoadingCanopy",checkpointPosition,0,"Loading bays");
        Model("WarehouseRack",checkpointPosition+new Vector3(7,0,3),180,"Loading bays");
        Model("Pallet",checkpointPosition+new Vector3(7,0,-3),0,"Loading bays");
        Model("BoxMedium",checkpointPosition+new Vector3(7,.245f,-3),0,"Loading bays");
    }
    private void Driveway(Vector3 from,Vector3 to)
    {
        var delta=to-from;float length=delta.magnitude;var mesh=new TownMeshBuilder();
        mesh.Box((from+to)/2+Vector3.up*.02f,new Vector3(8,.02f,length+4),TownColor.Asphalt,Quaternion.LookRotation(delta));
        Geometry("Supplier driveway",mesh,"Ground and courtyards");
        pavedAreas.Add(Rect.MinMaxRect(Mathf.Min(from.x,to.x)-5,Mathf.Min(from.z,to.z)-5,Mathf.Max(from.x,to.x)+5,Mathf.Max(from.z,to.z)+5));
    }
    private void Residential()
    {
        for(int row=0;row<2;row++)for(int column=0;column<5;column++)
        {
            var p=new Vector3(-200+column*16,0,-177+row*17);House(p,(column+row)%3,row==0?180:0);
        }
        foreach(int block in new[]{0,1,2,3,4})for(int column=0;column<3;column++)
        {
            float x=-92+block*56+column*16;
            if(layout.roads.Any(r=>Mathf.Abs(r.cell.x*12-x)<10&&r.cell.y==-16&&(r.connections&1)!=0))continue;
            House(new Vector3(x,0,-173),column%3,180);
        }
        foreach(float x in new[]{-76f,118f})
        {
            foreach(float dx in new[]{-21f,21f})foreach(float z in new[]{164f,181f,199f})
                House(new Vector3(x+dx,0,z),Mathf.RoundToInt(z)%2,dx<0?90:270);
            Building("ApartmentSmall",new Vector3(x,0,182),0,new Vector3(10,12,8),"Residential courtyards");
            Surface("Apartment courtyard",new Rect(x-16,165,32,38),TownColor.Pavement);
            Model("CustomerParking",new Vector3(x+10,0,175),90,"Residential courtyards");
            Model("VisitorHatchback",new Vector3(x+10,0,175),90,"Parked cars");
            for(int i=0;i<3;i++)Model("TreeRound",new Vector3(x-12+i*11,0,202),0,"Residential greenery");
        }
    }
    private void House(Vector3 position,int variant,float yaw)
    {
        string id=variant==0?"HouseCream":variant==1?"HouseBrick":"Townhouse";
        Building(id,position,yaw,new Vector3(variant==2?5.4f:7,variant==2?7.8f:5,6.5f),"Residential houses");
        Surface("Private garden",new Rect(position.x-7,position.z-6,14,13),TownColor.LeafLight,.012f);
        Model("DomesticFence",position+new Vector3(-6,0,0),90,"Garden fences",new Vector3(4,1,1));
        Model("DomesticFence",position+new Vector3(6,0,0),90,"Garden fences",new Vector3(4,1,1));
        Model("TreeTall",position+new Vector3(4.6f,0,3.7f),0,"Residential greenery",Vector3.one*.72f);
        Model("Mailbox",position+new Vector3(-4.6f,0,yaw==180?-6:6),yaw,"Street furniture");
        if(variant==1)Model("VisitorSedan",position+new Vector3(-4.8f,0,1.5f),yaw,"Parked cars");
    }
    private void TownCentre()
    {
        Building("Pharmacy",new Vector3(24,0,74),180,new Vector3(8,4.5f,6),"Town centre");
        Building("Cafe",new Vector3(24,0,120),0,new Vector3(8,4.5f,6),"Town centre");
        Building("ApartmentSmall",new Vector3(-70,0,115),0,new Vector3(10,12,8),"Town centre");
        Building("Townhouse",new Vector3(120,0,75),180,new Vector3(5.4f,7.8f,6.5f),"Town centre");
        foreach(float x in new[]{-70f,24f,120f})
        {
            foreach(float dx in new[]{-20f,20f})foreach(float z in new[]{64f,80f,112f,128f})
                House(new Vector3(x+dx,0,z),Mathf.RoundToInt(z)%3,dx<0?90:270);
            Building("HouseBrick",new Vector3(x-20,0,96),90,new Vector3(7,5,6.5f),"Town centre");
            Building("Townhouse",new Vector3(x+20,0,96),270,new Vector3(5.4f,7.8f,6.5f),"Town centre");
            Surface("Town square",new Rect(x-29,83,58,27),TownColor.Pavement);
            for(int i=0;i<4;i++)
            {
                Model("FlowerPlanter",new Vector3(x-24+i*16,0,85),0,"Town square furniture");
                Model("Bench",new Vector3(x-24+i*16,0,106),180,"Town square furniture");
                Model("TreeRound",new Vector3(x-24+i*16,0,109),0,"Town square greenery");
            }
            Model("CustomerParking",new Vector3(x+16,0,70),0,"Customer parking");
            Model("VisitorSedan",new Vector3(x+16,0,70),0,"Parked cars");
            Model("BusStop",new Vector3(x,0,137),180,"Bus stops");
            Model("LitterBin",new Vector3(x+4,0,136),0,"Street furniture");
        }
    }
    private void Rural()
    {
        Building("Garage",new Vector3(225,0,118),180,new Vector3(5.4f,4.4f,6.5f),"Farm buildings");
        Building("HouseCream",new Vector3(204,0,123),180,new Vector3(7,5,6.5f),"Farm buildings");
        for(int field=0;field<4;field++)
        {
            float x=194+(field%2)*33,z=-26+(field/2)*46;
            Surface("Cultivated field",new Rect(x-10,z-16,20,32),TownColor.Soil,.015f);
            var crop=new TownMeshBuilder();
            for(int row=0;row<5;row++)for(int plant=0;plant<12;plant++)
            {
                var p=new Vector3(x-8+row*4,.08f,z-13+plant*2.2f);
                crop.Ellipsoid(p+Vector3.up*.23f,new Vector3(.46f,.3f,.5f),row%2==0?TownColor.Leaf:TownColor.LeafLight,6,4);
            }
            Geometry("Crop rows",crop,"Farm fields");
        }
        for(int i=0;i<4;i++)Model("FruitCrate",new Vector3(201+i*1.4f,0,103),0,"Farm equipment");
        Model("DeliveryPickup",new Vector3(235,0,106),180,"Parked cars");
        Model("DomesticFence",new Vector3(216,0,39),0,"Farm fences",new Vector3(24,1,1));
    }
    private void Park()
    {
        Surface("Park lawn",new Rect(-16,155,80,51),TownColor.LeafLight,.01f);
        ParkLoop();
        for(int i=0;i<13;i++)
        {
            float angle=i*2.39996f;var p=new Vector3(24+Mathf.Cos(angle)*(12+i%3*3),0,180+Mathf.Sin(angle)*(7+i%3*2));
            Model(i%3==0?"Pine":"TreeWide",p,0,"Park trees");
        }
        foreach(float x in new[]{2f,18f,34f,48f})
        {
            Model("Bench",new Vector3(x,0,167),0,"Park furniture");
            Model("FlowerBed",new Vector3(x,0,202),0,"Park furniture");
        }
        Model("RockGroup",new Vector3(35,0,183),0,"Park rocks");
        Model("LitterBin",new Vector3(-8,0,169),0,"Park furniture");
    }
    private bool ClearVerge(Vector3 p)
    {
        var plot=layout.reservedShopPlot;
        if(new Rect(plot.xMin-3,plot.yMin-3,plot.width+6,plot.height+6).Contains(new Vector2(p.x,p.z)))return false;
        if(buildingAreas.Any(rect=>rect.Contains(new Vector2(p.x,p.z))))return false;
        return !pavedAreas.Any(rect=>new Rect(rect.xMin-2,rect.yMin-2,rect.width+4,rect.height+4).Contains(new Vector2(p.x,p.z)));
    }
    private void Streets()
    {
        foreach(var road in layout.roads)
        {
            var p=road.Position(layout.roadTileSize);
            if(road.connections!=5&&road.connections!=10)continue;
            if((road.cell.x+road.cell.y)%4!=0)continue;
            Vector3 side=road.connections==5?Vector3.right:Vector3.forward;
            foreach(int sign in new[]{-1,1})
            {
                var verge=p+side*sign*7.3f;if(!ClearVerge(verge))continue;
                Model("StreetLamp",verge,road.connections==5?sign*90:sign<0?180:0,"Street lights");
                var tree=verge+side*sign*2.2f+Vector3.forward*2;
                if(ClearVerge(tree))Model("TreeTall",tree,0,"Street trees",Vector3.one*.78f);
            }
        }
        foreach(var road in layout.roads.Where(r=>Enumerable.Range(0,4).Count(i=>(r.connections&(1<<i))!=0)>2))
        {
            var p=road.Position(layout.roadTileSize)+new Vector3(7.3f,0,7.3f);
            if(ClearVerge(p))Model("YieldSign",p,180,"Traffic signs");
        }
    }
    private void Outskirts()
    {
        var b=layout.cityBounds;var random=new System.Random(934);
        for(int i=0;i<45;i++)foreach(int side in new[]{-1,1})
        {
            float x=b.xMin+8+i*(b.width-16)/44;
            x+=(float)random.NextDouble()*6-3;
            float z=(side<0?b.yMin+8:b.yMax-8)+(float)random.NextDouble()*6-3;
            Model(i%3==0?"TreeRound":"Pine",new Vector3(x,0,z),i*47,"Boundary forest",Vector3.one*(.8f+(float)random.NextDouble()*.55f));
        }
        for(int i=0;i<33;i++)foreach(int side in new[]{-1,1})
        {
            float z=b.yMin+16+i*(b.height-32)/32;
            z+=(float)random.NextDouble()*6-3;
            float x=(side<0?b.xMin+9:b.xMax-9)+(float)random.NextDouble()*6-3;
            Model(i%3==0?"TreeWide":"Pine",new Vector3(x,0,z),i*71,"Boundary forest",Vector3.one*(.8f+(float)random.NextDouble()*.55f));
        }
        for(int i=0;i<24;i++)
        {
            float angle=i*2.39996f;var p=new Vector3(216+Mathf.Cos(angle)*(8+i%4*6),0,183+Mathf.Sin(angle)*(7+i%3*4));
            Model(i%2==0?"Pine":"TreeRound",p,0,"Rural woodland");
        }
        foreach(var p in new[]{new Vector3(-264,0,228),new Vector3(252,0,228),new Vector3(-262,0,-208)})
        {
            Model("GrassyHill",p,0,"Landscape hills",new Vector3(2,1.3f,1.8f));
            Model("RockGroup",p+Vector3.right*7,0,"Landscape rocks",Vector3.one*2);
        }
        for(int i=0;i<80;i++)
        {
            var p=new Vector3(-267+(i%4)*9+(float)random.NextDouble()*5-2.5f,0,-164+(i/4)*18+(float)random.NextDouble()*9-4.5f);
            Model(i%3==0?"TreeWide":"Pine",p,i*27,"Western woodland",Vector3.one*(.85f+(float)random.NextDouble()*.45f));
        }
    }

    private void CombineVisualChunks()
    {
        Directory.CreateDirectory(Folder+"/Meshes");AssetDatabase.Refresh();
        var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null&&f.GetComponentInParent<TownExpansionScenery>()==null).ToArray();
        var groups=filters.GroupBy(f=>new Vector2Int(Mathf.FloorToInt(f.GetComponent<Renderer>().bounds.center.x/48),Mathf.FloorToInt(f.GetComponent<Renderer>().bounds.center.z/48)));
        foreach(var group in groups)
        {
            var batch=new List<MeshFilter>();int vertices=0,part=0;
            foreach(var filter in group)
            {
                if(batch.Count>0&&vertices+filter.sharedMesh.vertexCount>60000){Combine(group.Key,part++,batch);batch.Clear();vertices=0;}
                batch.Add(filter);vertices+=filter.sharedMesh.vertexCount;
            }
            if(batch.Count>0)Combine(group.Key,part,batch);
        }
        var transientMeshes=filters.Select(f=>f.sharedMesh).Where(m=>!AssetDatabase.Contains(m)).Distinct().ToArray();
        foreach(var filter in filters)
        {
            var instance=filter.gameObject;Object.DestroyImmediate(instance.GetComponent<MeshRenderer>());Object.DestroyImmediate(filter);
            if(instance.GetComponents<Component>().Length==1&&instance.transform.childCount==0)Object.DestroyImmediate(instance);
        }
        foreach(var mesh in transientMeshes)Object.DestroyImmediate(mesh);
    }
    private void ExpansionScenery()
    {
        var scenery=Section("Future store landscapes").gameObject.AddComponent<TownExpansionScenery>();
        var plots=new List<TownPlotScenery>();
        foreach(var plot in layout.expansionPlots)
        {
            var group=new GameObject("Future plot "+plot.territory);group.transform.SetParent(scenery.transform,false);
            var sources=new List<MeshFilter>();int index=0;var random=new System.Random(714+(int)plot.territory);
            for(float x=plot.bounds.xMin+5;x<plot.bounds.xMax-5;x+=10)
                for(float z=plot.bounds.yMin+5;z<plot.bounds.yMax-5;z+=11)
                {
                    if(Mathf.PerlinNoise(x*.045f+12,z*.04f+19)<.46f)continue;
                    float dx=(float)random.NextDouble()*6-3,dz=(float)random.NextDouble()*6-3;
                    float scale=.72f+(float)random.NextDouble()*.65f;
                    var tree=Model(index++%3==0?"Pine":"TreeRound",new Vector3(x+dx,0,z+dz),(float)random.NextDouble()*360,"Temporary greenery",Vector3.one*scale);
                    foreach(var collider in tree.GetComponents<Collider>())Object.DestroyImmediate(collider);
                    tree.transform.SetParent(group.transform,true);sources.Add(tree.GetComponent<MeshFilter>());
                }
            if(sources.Count>0)Combine(new Vector2Int((int)plot.territory,0),0,sources,group.transform);
            foreach(var source in sources)Object.DestroyImmediate(source.gameObject);
            plots.Add(new TownPlotScenery{territory=plot.territory,model=group});
        }
        scenery.plots=plots.ToArray();
    }
    private void Combine(Vector2Int cell,int part,List<MeshFilter> sources,Transform parent=null)
    {
        string name=(parent==null?"":"Reserve_")+$"TownChunk_{cell.x}_{cell.y}_{part}";var instance=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
        instance.transform.SetParent(parent!=null?parent:Section("Render chunks — 48 metre cells"),false);instance.transform.localPosition=new Vector3(cell.x*48,0,cell.y*48);
        var combine=sources.Select(s=>new CombineInstance{mesh=s.sharedMesh,transform=instance.transform.worldToLocalMatrix*s.transform.localToWorldMatrix}).ToArray();
        var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combine,true,true);mesh.RecalculateBounds();
        // Preserve procedural material scale when the exact same geometry is shown at 1:3 in the shop scene.
        var nativeUv=mesh.vertices.Select(v=>instance.transform.TransformPoint(v)).Select(p=>new Vector2(p.x,p.z)).ToArray();
        mesh.uv2=nativeUv;
        string path=Folder+"/Meshes/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
        else
        {
            saved.Clear();saved.indexFormat=mesh.indexFormat;saved.vertices=mesh.vertices;saved.normals=mesh.normals;
            saved.uv=mesh.uv;saved.uv2=mesh.uv2;saved.triangles=mesh.triangles;saved.RecalculateBounds();EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);
        }
        instance.GetComponent<MeshFilter>().sharedMesh=saved;instance.GetComponent<MeshRenderer>().sharedMaterial=material;
    }
}
