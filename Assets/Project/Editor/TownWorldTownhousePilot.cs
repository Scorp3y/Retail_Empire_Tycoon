using System;
using System.Globalization;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEngine;

/// <summary>The two authored compositions share the same protected street block.</summary>
internal enum TownhousePilotVariant
{
    FourBuildings,
    FiveBuildings
}

internal sealed partial class TownWorldAssembler
{
    private const string PilotSection="Townhouse pilot gardens";
    private static readonly Rect TownhousePilotBlock=new Rect(-210,-186,84,36);
    private static readonly Rect PilotParkingCourt=new Rect(-138.5f,-186,12.5f,14);
    private static readonly Rect PilotParkingAisle=new Rect(-138.5f,-186,12.5f,6);

    private readonly struct PilotBuilding
    {
        public readonly Vector3 Position;
        public readonly float Width;
        public readonly float Depth;
        public readonly float WallHeight;
        public readonly int Floors;
        public readonly float Yaw;
        public readonly int Variant;
        public readonly bool IsTownhouse;

        public Rect Footprint=>new Rect(Position.x-Width/2,Position.z-Depth/2,Width,Depth);
        public float BuiltArea=>IsTownhouse?2*8.8f*Depth:Width*Depth;

        private PilotBuilding(float x,float z,float width,int floors,float yaw,int variant,bool townhouse)
        {
            Position=new Vector3(x,0,z);
            Width=width;
            Depth=12;
            Floors=floors;
            WallHeight=townhouse?8.6f:floors*3.1f;
            Yaw=yaw;
            Variant=variant;
            IsTownhouse=townhouse;
        }

        public static PilotBuilding Townhouse(float x)
            =>new PilotBuilding(x,-178,18,3,180,0,true);

        public static PilotBuilding Section(float x,float z,float width,int floors,float yaw,int variant)
            =>new PilotBuilding(x,z,width,floors,yaw,variant,false);
    }

    private PilotBuilding[] TownhousePilotBuildings()
    {
        if(townhousePilotVariant==TownhousePilotVariant.FourBuildings)
            return new[]{
                PilotBuilding.Townhouse(-197),PilotBuilding.Townhouse(-175),
                PilotBuilding.Section(-193.5f,-158,26,3,0,1),
                PilotBuilding.Section(-153,-158,32,4,0,4)
            };

        return new[]{
            PilotBuilding.Townhouse(-197),PilotBuilding.Townhouse(-175),
            PilotBuilding.Section(-150,-178,22,2,180,2),
            PilotBuilding.Section(-188,-158,34,3,0,1),
            PilotBuilding.Section(-150,-158,34,4,0,4)
        };
    }

    private void BuildTownhousePilot(Rect block)
    {
        var buildings=TownhousePilotBuildings();
        ValidateTownhousePilot(block,buildings);

        // Grass is the default land use; paving belongs only to an entrance, path or parking court.
        PilotSurface("Townhouse pilot planted base",block,TownColor.Grass,.06f,block);
        // Legacy street/landscape placement treats pavedAreas as occupancy masks, not a material survey.
        // Retain this block's old exclusion so neighbouring generated decorations remain unchanged.
        pavedAreas.Add(block);
        foreach(var building in buildings)
        {
            if(building.IsTownhouse)
                DetailBuilding("TownhousePair",building.Position,building.Yaw,
                    new Vector3(building.Width,building.WallHeight,building.Depth));
            else
                UrbanBuilding(building.Position,building.Width,building.Depth,building.Floors,
                    building.Yaw,false,"",serviceDetails:false,variantOverride:building.Variant);
        }

        BuildPilotParking(block);
        BuildPilotPaths(block,buildings);
        BuildPilotGardens(block,buildings);
        // The generic frontage tree positions would overlap this denser western building line.
        completedFrontages++;
        WriteTownhousePilotMeasurements(block,buildings);
    }

    private void BuildPilotParking(Rect block)
    {
        courtyardVehicleAisles.Add(PilotParkingAisle);
        PilotSurface("Townhouse pilot two-car court",PilotParkingCourt,TownColor.Asphalt,.09f,block);
        // A shallow flush seam joins the existing sidewalk; no street tile or road connection changes.
        PilotSurface("Townhouse pilot flush parking entrance",new Rect(-136,-188,6,8),TownColor.Asphalt,.145f,block);
        ParkingRow(new Vector3(-137.5f,0,-176),2,true,"Townhouse pilot residents");

        var edge=new TownMeshBuilder();
        PaintedLine(edge,new Vector3(-138.5f,.115f,-186),new Vector3(-138.5f,.115f,-172),.07f,TownColor.Pavement);
        PaintedLine(edge,new Vector3(-138.5f,.115f,-172),new Vector3(-126,.115f,-172),.07f,TownColor.Pavement);
        Geometry("Townhouse pilot parking edges",edge,"Townhouse pilot parking");
    }

    private void BuildPilotPaths(Rect block,PilotBuilding[] buildings)
    {
        PilotWalkway("Townhouse pilot quiet garden path",new Vector3(-208,0,-168),new Vector3(-125.5f,0,-168),block,buildings);
        PilotWalkway("Townhouse pilot west sidewalk",new Vector3(-208,0,-186.5f),new Vector3(-208,0,-149.5f),block,buildings);
        // Keep the established southwest junction signal at X=-126.8; route beside its pole instead.
        PilotWalkway("Townhouse pilot east sidewalk",new Vector3(-125.5f,0,-186.5f),new Vector3(-125.5f,0,-149.5f),block,buildings);
        PilotWalkway("Townhouse pilot south frontage",new Vector3(-208,0,-185.15f),new Vector3(-139,0,-185.15f),block,buildings);
        PilotWalkway("Townhouse pilot north frontage",new Vector3(-208,0,-150.1f),new Vector3(-125.5f,0,-150.1f),block,buildings);

        if(townhousePilotVariant==TownhousePilotVariant.FourBuildings)
            PilotWalkway("Townhouse pilot open garden access",new Vector3(-151,0,-185.15f),new Vector3(-151,0,-168),block,buildings);
    }

    private void BuildPilotGardens(Rect block,PilotBuilding[] buildings)
    {
        foreach(var building in buildings)
        {
            bool south=building.Position.z<-168;
            float gardenZ=south?-171.8f:-166.7f;
            var garden=new Rect(building.Footprint.xMin+.8f,gardenZ,building.Width-1.6f,2.5f);
            PilotSurface("Townhouse pilot rear garden",garden,TownColor.LeafLight,.08f,block);
            Border(garden,TownColor.Pavement,"Townhouse pilot garden edging");

            float treeZ=south?-170.55f:-165.45f;
            PlacePilotTree(new Vector3(building.Position.x+4,0,treeZ),block,buildings);
            CityProp("FlowerBorder",new Vector3(building.Position.x-5,.08f,treeZ),0,PilotSection);
        }

        if(townhousePilotVariant==TownhousePilotVariant.FourBuildings)
        {
            var garden=new Rect(-163,-184,23,12);
            PilotSurface("Townhouse pilot alternative quiet garden",garden,TownColor.LeafLight,.08f,block);
            Border(garden,TownColor.Pavement,"Townhouse pilot alternative garden edging");
            PlacePilotTree(new Vector3(-159,0,-177),block,buildings);
            PlacePilotTree(new Vector3(-144,0,-177),block,buildings);
            Model("Bench",new Vector3(-156,.08f,-174),0,PilotSection);
            Model("FlowerPlanter",new Vector3(-145,.08f,-181),0,PilotSection);
        }

        // Both alternatives keep this seat at the same quiet-path node, away from entrances and cars.
        Model("Bench",new Vector3(-185,.08f,-170.45f),0,PilotSection);
        Model("LitterBin",new Vector3(-181.8f,.08f,-170.45f),0,PilotSection);
    }

    private void PlacePilotTree(Vector3 position,Rect block,PilotBuilding[] buildings)
    {
        const float scale=.5f;
        var bounds=models["TreeTall"].GetComponent<MeshFilter>().sharedMesh.bounds;
        float effectiveScale=scale*TownModelScale("TreeTall");
        var crownFootprint=Rect.MinMaxRect(position.x+bounds.min.x*effectiveScale,position.z+bounds.min.z*effectiveScale,
            position.x+bounds.max.x*effectiveScale,position.z+bounds.max.z*effectiveScale);
        RequirePilotArea(crownFootprint,block,0,"tree crown");
        if(buildings.Any(building=>building.Footprint.Overlaps(crownFootprint))
            ||publicPaths.Any(path=>TransportDistance(position,path)<.7f))
            throw new InvalidOperationException("Townhouse pilot tree intersects a building or walking route at "+position);
        Model("TreeTall",position,0,PilotSection,Vector3.one*scale);
    }

    private void PilotWalkway(string name,Vector3 from,Vector3 to,Rect block,PilotBuilding[] buildings)
    {
        const float width=2.2f;
        Rect footprint;
        if(Mathf.Abs(from.x-to.x)<.01f)
            footprint=new Rect(from.x-width/2,Mathf.Min(from.z,to.z),width,Mathf.Abs(to.z-from.z));
        else if(Mathf.Abs(from.z-to.z)<.01f)
            footprint=new Rect(Mathf.Min(from.x,to.x),from.z-width/2,Mathf.Abs(to.x-from.x),width);
        else throw new InvalidOperationException("Townhouse pilot paths must follow the authored block axes.");
        RequirePilotArea(footprint,block,2,name);
        if(buildings.Any(building=>building.Footprint.Overlaps(footprint)))
            throw new InvalidOperationException("Townhouse pilot walkway intersects a building: "+name);
        Walkway(name,from,to,width);
    }

    private void PilotSurface(string name,Rect area,TownColor color,float elevation,Rect block)
    {
        RequirePilotArea(area,block,2,name);
        Surface(name,area,color,elevation);
    }

    private void ValidateTownhousePilot(Rect block,PilotBuilding[] buildings)
    {
        if(block!=TownhousePilotBlock)
            throw new InvalidOperationException("The townhouse composition is limited to its original 84 by 36 metre pilot plot.");
        int expectedCount=townhousePilotVariant==TownhousePilotVariant.FourBuildings?4:5;
        if(buildings.Length!=expectedCount)throw new InvalidOperationException("Unexpected townhouse pilot building count.");

        for(int i=0;i<buildings.Length;i++)
        {
            var footprint=buildings[i].Footprint;
            RequirePilotArea(footprint,block,0,"building body");
            if(footprint.Overlaps(layout.reservedShopPlot)||footprint.Overlaps(PilotParkingCourt))
                throw new InvalidOperationException("Townhouse pilot building overlaps protected land or its parking court.");
            for(int j=0;j<i;j++)
                if(footprint.Overlaps(buildings[j].Footprint))
                    throw new InvalidOperationException("Townhouse pilot building bodies overlap.");
        }

        float builtCoverage=buildings.Sum(building=>building.BuiltArea)/(block.width*block.height);
        if(builtCoverage<.35f||builtCoverage>.55f)
            throw new InvalidOperationException("Townhouse pilot built coverage must remain between 35 and 55 percent.");
        if(buildings.Select(building=>Mathf.RoundToInt(building.WallHeight*10)).Distinct().Count()<3)
            throw new InvalidOperationException("Townhouse pilot needs at least three wall heights without scaling storeys.");

        RequirePilotArea(PilotParkingCourt,block,0,"parking court");
        RequirePilotArea(PilotParkingAisle,block,0,"parking manoeuvring aisle");
        var parkingBays=new Rect(-137.5f,-179.9f,2*3.4f*1.3f,6*1.3f);
        RequirePilotArea(parkingBays,PilotParkingCourt,0,"two standard parking bays");
        if(parkingBays.Overlaps(PilotParkingAisle)||PilotParkingAisle.height<6)
            throw new InvalidOperationException("Townhouse pilot parking bays consume the six metre manoeuvring aisle.");
    }

    private static void RequirePilotArea(Rect area,Rect block,float seamAllowance,string purpose)
    {
        const float tolerance=.001f;
        if(area.width<=0||area.height<=0||area.xMin<block.xMin-seamAllowance-tolerance
            ||area.xMax>block.xMax+seamAllowance+tolerance||area.yMin<block.yMin-seamAllowance-tolerance
            ||area.yMax>block.yMax+seamAllowance+tolerance)
            throw new InvalidOperationException("Townhouse pilot area leaves its authorised plot: "+purpose);
    }

    private void WriteTownhousePilotMeasurements(Rect block,PilotBuilding[] buildings)
    {
        var culture=CultureInfo.InvariantCulture;
        float area=block.width*block.height;
        float bodyArea=buildings.Sum(building=>building.BuiltArea);
        float envelopeArea=buildings.Sum(building=>building.Width*building.Depth);
        string heights=string.Join(", ",buildings.Select(building=>building.WallHeight).Distinct().OrderBy(height=>height)
            .Select(height=>height.ToString("F1",culture)));
        Directory.CreateDirectory("Library/TownWorldQA");
        File.WriteAllText("Library/TownWorldQA/townhouse-pilot-layout.txt",
            "PASS native composition constraints: "+townhousePilotVariant+".\n"
            +"Plot: 84 x 36 m; "+area.ToString("F1",culture)+" m2.\n"
            +"Building count: "+buildings.Length+". Wall heights: "+heights+" m.\n"
            +"Actual building bodies: "+bodyArea.ToString("F1",culture)+" m2; "+(100*bodyArea/area).ToString("F2",culture)+"%.\n"
            +"Bounding envelopes: "+envelopeArea.ToString("F1",culture)+" m2; "+(100*envelopeArea/area).ToString("F2",culture)+"%.\n"
            +"Quiet garden strip: 8 m between body rows; central path: 2.2 m.\n"
            +"Parking: two 4.42 x 7.8 m bays; manoeuvring aisle: 6 m.\n"
            +"No new model types, decorative people, street tiles or road graph connections.\n"
            +"Remaining visual quality and driving acceptance must be verified by the isolated scene run.\n");
    }
}
