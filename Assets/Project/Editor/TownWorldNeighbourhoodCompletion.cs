using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEngine;

/// <summary>Completes public blocks without changing land ownership, supplier routes or the road graph.</summary>
internal sealed partial class TownWorldAssembler
{
    private readonly List<Vector3[]> entrancePaths=new List<Vector3[]>();
    private readonly List<Vector3[]> publicPaths=new List<Vector3[]>();
    private int completedFrontages;
    private int smallCourtyards;
    private int connectedDoors;
    private int neighbourhoodStreetOpenings;

    private int NeighbourhoodStreetMask(TownRoadTile road)
    {
        int mask=road.connections;
        if(road.cell==new Vector2Int(-4,-16))mask|=1;
        foreach(var cell in new[]{new Vector2Int(-10,8),new Vector2Int(-10,-9),new Vector2Int(-2,8),
            new Vector2Int(22,8),new Vector2Int(6,8),new Vector2Int(14,8),new Vector2Int(22,-2)})
            if(road.cell==cell)mask|=8;
        if(mask!=road.connections)neighbourhoodStreetOpenings++;
        return mask;
    }

    private void DistrictPaving(string name,Rect area,TownColor tone)
    {
        Surface(name,area,tone,.045f);
        // Large slabs rather than a dense noisy checkerboard; colours come from the existing palette.
        var joints=new TownMeshBuilder();
        for(float x=area.xMin+6;x<area.xMax;x+=6)joints.Ground(x,area.center.y,.025f,area.height,.052f,TownColor.Stone);
        for(float z=area.yMin+6;z<area.yMax;z+=6)joints.Ground(area.center.x,z,area.width,.025f,.052f,TownColor.Stone);
        Geometry(name+" slab joints",joints,"District paving detail");
        pavedAreas.Add(area);
    }

    private void Walkway(string name,Vector3 from,Vector3 to,float width=2.2f,bool recordEntrance=false)
    {
        from.y=to.y=.145f;
        var delta=to-from;if(delta.sqrMagnitude<.01f)return;
        var footprint=Rect.MinMaxRect(Mathf.Min(from.x,to.x)-width/2,Mathf.Min(from.z,to.z)-width/2,
            Mathf.Max(from.x,to.x)+width/2,Mathf.Max(from.z,to.z)+width/2);
        if(footprint.Overlaps(layout.reservedShopPlot)||footprint.Overlaps(new Rect(-282,-196,70,378))
            ||footprint.Overlaps(new Rect(454,236,200,238)))
            throw new InvalidOperationException("Public path overlaps protected land: "+name);
        var mesh=new TownMeshBuilder();var side=Vector3.Cross(Vector3.up,delta).normalized*width/2;
        mesh.Quad(to-side,to+side,from+side,from-side,TownColor.Pavement);
        PaintedLine(mesh,from-side,to-side,.035f,TownColor.Stone);
        PaintedLine(mesh,from+side,to+side,.035f,TownColor.Stone);
        Geometry(name,mesh,"Neighbourhood paths");pavedAreas.Add(footprint);
        publicPaths.Add(new[]{from,to});
        if(recordEntrance)entrancePaths.Add(new[]{from,to});
    }

    private void ConnectBuildingEntrances(GameObject building,float depth,IEnumerable<float> entrances,bool finishEntrance=true)
    {
        var forward=building.transform.forward;
        foreach(float entrance in entrances)
        {
            connectedDoors++;
            var door=building.transform.TransformPoint(new Vector3(entrance,0,depth/2+.7f));
            var street=layout.roads.Select(r=>r.Position(layout.roadTileSize))
                .Where(p=>Vector3.Dot(p-door,forward)>0&&Mathf.Abs(Vector3.Dot(p-door,building.transform.right))<6.1f)
                .OrderBy(p=>(p-door).sqrMagnitude).FirstOrDefault();
            float distance=Vector3.Dot(street-door,forward)-5.5f;
            if(distance>0&&distance<=32)
                Walkway("Door to public sidewalk "+building.name,door,door+forward*distance,2.2f,true);
            else
            {
                // Some long blocks face a lawn, not a street. Follow their frontage to the nearest side street.
                var corner=door+forward*2.2f;
                var sideStreet=layout.roads.Select(r=>r.Position(layout.roadTileSize))
                    .Where(p=>Mathf.Abs(Vector3.Dot(p-corner,forward))<6.1f)
                    .OrderBy(p=>(p-corner).sqrMagnitude).FirstOrDefault();
                var sideways=building.transform.right*Mathf.Sign(Vector3.Dot(sideStreet-corner,building.transform.right));
                float sideDistance=Vector3.Dot(sideStreet-corner,sideways)-5.5f;
                if(sideDistance<=0||sideDistance>96)throw new InvalidOperationException("No side sidewalk for "+building.name+" at "+door);
                Walkway("Entrance frontage corner "+building.name,door,corner,2.2f,true);
                Walkway("Entrance frontage to side street "+building.name,corner,corner+sideways*sideDistance,2.2f,true);
            }
            if(!finishEntrance)continue;
            // Existing new low-rise models already contain a canopy. Only long blocks need another one.
            if(building.name=="Residential long block"||building.name=="Office long block")
            {
                var canopy=CityProp("EntranceCanopy",building.transform.TransformPoint(new Vector3(entrance,2.4f,depth/2+.8f)),
                    building.transform.localEulerAngles.y,"Finished entrances");
                canopy.transform.SetParent(building.transform,true);
            }
            if(building.name=="Residential long block")
            {
                var steps=CityProp("StairLanding",building.transform.TransformPoint(new Vector3(entrance,.04f,depth/2+1.1f)),
                    building.transform.localEulerAngles.y,"Finished entrances");
                steps.transform.SetParent(building.transform,true);
            }
        }
    }

    private void StreetTree(Vector3 position,float scale=.8f)
    {
        if(publicPaths.Any(path=>TransportDistance(position,path)<2.5f))return;
        CityProp("TreePit",position+Vector3.up*.06f,0,"Street tree beds");
        Model("TreeRound",position,0,"Street tree beds",Vector3.one*scale);
    }

    private void FinishBlockFrontage(Rect block)
    {
        completedFrontages++;
        // Furnish the side strip, away from front-door paths and courtyard driveways.
        float x=block.xMin+2.5f;
        foreach(float z in new[]{block.yMin+4,block.yMax-4})StreetTree(new Vector3(x,0,z),.75f);
        CityProp("FlowerBorder",new Vector3(x,.06f,block.center.y),90,"Neighbourhood planting");
        Model("LitterBin",new Vector3(x,0,block.yMin+10),0,"Neighbourhood furniture");
    }

    private void CornerHousingQuarter(Rect block)
    {
        DistrictPaving("Corner residential paving",block,TownColor.Sand);
        foreach(float x in new[]{-186f,-150f})
        {
            DetailBuilding("ResidentialCorner",new Vector3(x,0,-46),180,new Vector3(28,9.3f,12));
            Surface("Corner residential front lawn",new Rect(x-13,-63,26,7),TownColor.LeafLight,.07f);
            Walkway("Corner house garden gate",new Vector3(x,0,-52.7f),new Vector3(x,0,-66.5f));
            CityProp("FlowerBorder",new Vector3(x-8,.08f,-60),0,"Neighbourhood planting");
            CityProp("LowRailing",new Vector3(x+8,0,-63),0,"Neighbourhood boundaries");
            StreetTree(new Vector3(x+8,0,-58),.8f);
        }
        Walkway("Corner houses shared footpath",new Vector3(-205,0,-65),new Vector3(-131,0,-65));
        FinishBlockFrontage(block);
    }

    private void NeighbourhoodShoppingQuarter(Rect block)
    {
        DistrictPaving("Local shopping paving",block,TownColor.Pavement);
        DetailBuilding("NeighbourhoodShops",new Vector3(-83,0,-166),180,new Vector3(36,4.2f,10));
        DetailBuilding("GaragePair",new Vector3(-48,0,-165),180,new Vector3(12,3.2f,7));
        Surface("Local garages asphalt apron",new Rect(-55,-185,14,16),TownColor.Asphalt,.06f);
        Surface("Local garage street connection",new Rect(-52,-192,8,23),TownColor.Asphalt,.04f);
        CityProp("CafeTerrace",new Vector3(-57,.06f,-176),180,"Local shop terrace");
        CityProp("BicycleRack",new Vector3(-104,.06f,-176),0,"Local shop terrace");
        CityProp("FlowerBorder",new Vector3(-63,.06f,-181),90,"Local shop terrace");
        Walkway("Local shops public promenade",new Vector3(-107,0,-184),new Vector3(-56,0,-184));
        FinishBlockFrontage(block);
    }

    private void GardenHousingQuarter(Rect block)
    {
        DistrictPaving("Garden housing paving",block,TownColor.Sand);
        DetailBuilding("ResidentialCorner",new Vector3(201,0,-167),180,new Vector3(28,9.3f,12));
        DetailBuilding("TownhousePair",new Vector3(237,0,-167),180,new Vector3(18,8.6f,12));
        Surface("Garden street lawn",new Rect(190,-183,57,8),TownColor.LeafLight,.07f);
        foreach(float x in new[]{201f,232.4f,241.6f})Walkway("Garden street door path",new Vector3(x,0,-173.7f),new Vector3(x,0,-186.5f));
        foreach(float x in new[]{192f,216f,247f})StreetTree(new Vector3(x,0,-179),.8f);
        CityProp("PicnicTable",new Vector3(222,.08f,-178),0,"Garden street rest area");
        Walkway("Garden street promenade",new Vector3(185,0,-185),new Vector3(252,0,-185));
        FinishBlockFrontage(block);
    }

    private void FurnishQuietCourt(Rect block,Rect court,bool office)
    {
        smallCourtyards++;
        var aisle=new Rect(court.xMin,court.yMin+8,court.width,4);
        courtyardVehicleAisles.Add(aisle);Surface("Small courtyard vehicle aisle",aisle,TownColor.Asphalt,.085f);
        var garden=new Rect(court.xMin+4,court.yMin+13,court.width-13,4.5f);
        Surface("Quiet courtyard planting",garden,TownColor.LeafLight,.095f);
        CityProp("PicnicTable",new Vector3(garden.xMin+5,.1f,garden.center.y+.4f),0,"Quiet courtyard");
        CityProp("FlowerBorder",new Vector3(garden.xMax-4,.1f,garden.center.y),0,"Quiet courtyard");
        float pathZ=garden.yMin+.6f;
        Walkway("Quiet courtyard pedestrian link",new Vector3(court.xMin+2,0,pathZ),new Vector3(block.xMax+1,0,pathZ),1.6f);
    }

    private void ConnectGarden(Rect block,Rect garden)
    {
        // The eastern side is open; do not route people through the three enclosing buildings.
        float z=garden.yMin+1.2f;
        Walkway("Garden to street crossing",new Vector3(garden.xMin+2,0,z),new Vector3(block.xMax+1.5f,0,z),1.6f);
        Walkway("Garden eastern walkway",new Vector3(block.xMax-2,0,z),new Vector3(block.xMax-2,0,block.center.y),1.6f);
        CityProp("FlowerBorder",new Vector3(garden.xMin+11,.09f,garden.yMax+2.5f),0,"Courtyard planted borders");
    }

    private void CompleteCivicSquare()
    {
        Walkway("Civic square central promenade",new Vector3(-108,0,152),new Vector3(-35,0,152),3);
        CityProp("StreetNoticeboard",new Vector3(-100,0,156),180,"Postal square amenities",true);
        CityProp("FlowerBorder",new Vector3(-92,.06f,156),0,"Postal square amenities");
        CityProp("FlowerBorder",new Vector3(-62,.06f,156),0,"Postal square amenities");
        CityProp("PicnicTable",new Vector3(-41,.07f,179),0,"Postal square rear garden");
    }

    private void CompletePublicSpaces()
    {
        // Railway square is north of all protected supplier plots; the station and tracks stay in place.
        DistrictPaving("Station arrival square",new Rect(-208,198,42,13),TownColor.Pavement);
        Walkway("Station sidewalk connection",new Vector3(-186,0,197.5f),new Vector3(-186,0,203.5f),3);
        Walkway("Station eastern approach",new Vector3(-186,0,203.5f),new Vector3(-176,0,203.5f),2.2f);
        Walkway("Station side passage",new Vector3(-176,0,203.5f),new Vector3(-176,0,215),2.2f);
        Walkway("Station front passage",new Vector3(-176,0,215),new Vector3(-190,0,215),2.2f);
        foreach(float x in new[]{-205f,-170f})
        {
            StreetTree(new Vector3(x,0,207),.9f);
            CityProp("FlowerBorder",new Vector3(x,.06f,201),90,"Station arrival amenities");
        }
        CityProp("StreetNoticeboard",new Vector3(-180,0,207),180,"Station arrival amenities",true);
        CityProp("BicycleRack",new Vector3(-200,.06f,208),90,"Station arrival amenities");
        Model("Bench",new Vector3(-193,0,207),180,"Station arrival amenities");

        Walkway("Park central fountain approach",new Vector3(24,0,155),new Vector3(24,0,166),3);
        foreach(float x in new[]{17f,31f})CityProp("FlowerBorder",new Vector3(x,.06f,157),90,"Park entrance planting");
        CityProp("StreetNoticeboard",new Vector3(37,0,160),180,"Park entrance amenities",true);
        CityProp("PicnicTable",new Vector3(45,.05f,170),0,"Park picnic area");

        CompleteRiverWalk();
        StreetSurfaceDetails();
    }

    private void CompleteRiverWalk()
    {
        // Extend on the city bank only; stay south of the road bridge's ramp and its clear driving envelope.
        var path=new TownMeshBuilder();
        for(float z=-191;z<-119;z+=3)
        {
            float a=layout.RiverCenter(z)-23,b=layout.RiverCenter(z+3)-23;
            path.Quad(new Vector3(a-1.5f,.07f,z),new Vector3(b-1.5f,.07f,z+3),new Vector3(b+1.5f,.07f,z+3),new Vector3(a+1.5f,.07f,z),TownColor.Sand);
            if(((int)z+191)%12==0)CityProp("RiverReeds",new Vector3(layout.RiverCenter(z)-19.1f,0,z),0,"Soft riverbank planting");
        }
        Geometry("Extended river promenade",path,"Riverfront promenade");
        pavedAreas.Add(new Rect(274,-193,45,77));
        Walkway("River southern city connection",new Vector3(269,0,-191),new Vector3(layout.RiverCenter(-191)-23,0,-191),3);
        float view=layout.RiverCenter(-158)-23;
        DistrictPaving("River southern viewing place",new Rect(view-8,-162,7,9),TownColor.Pavement);
        Model("Bench",new Vector3(view-5,0,-158),90,"Riverfront seating");
        CityProp("LowRailing",new Vector3(view-2,0,-161),90,"Riverfront seating",true);
        // Small green strips soften the upper banks without modifying the river collision surface.
        var planting=new TownMeshBuilder();
        for(float z=-190;z<-68;z+=6)
        {
            float a=layout.RiverCenter(z),b=layout.RiverCenter(z+6);
            planting.Quad(new Vector3(a-18,.015f,z),new Vector3(b-18,.015f,z+6),new Vector3(b-16.8f,-.65f,z+6),new Vector3(a-16.8f,-.65f,z),TownColor.LeafLight);
        }
        Geometry("Soft grassy upper riverbank",planting,"Soft riverbank planting");
        var bridgeNotice=new Vector3(291,0,-53);
        CityProp("StreetNoticeboard",bridgeNotice,180,"Bridge approach amenities",true);
        Model("Bench",new Vector3(285,0,-53),180,"Bridge approach amenities");
        Walkway("Bridge approach rest area",new Vector3(270,0,-52),new Vector3(294,0,-52),2.5f);
    }

    private void StreetSurfaceDetails()
    {
        int index=0;
        foreach(var road in layout.roads.Where(r=>r.connections==5||r.connections==10))
        {
            var p=road.Position(layout.roadTileSize);
            if(p.x<-205||p.x>266||p.z<-193||p.z>193||++index%5!=0)continue;
            var side=road.connections==5?Vector3.right:Vector3.forward;
            CityProp("DrainGrate",p+side*3.7f+Vector3.up*.05f,road.connections==5?90:0,"Street drainage details");
            CityProp("ManholeCover",p-side*1.8f+Vector3.up*.05f,0,"Street drainage details");
            var seams=new TownMeshBuilder();var forward=Vector3.Cross(side,Vector3.up);
            foreach(float sign in new[]{-1f,1f})
            {
                var edge=p+side*sign*5;
                for(int slab=-2;slab<=2;slab++)seams.Ground(edge.x+forward.x*slab*2.1f,edge.z+forward.z*slab*2.1f,
                    side.x!=0?1.8f:.025f,side.z!=0?1.8f:.025f,.145f,TownColor.Stone);
            }
            Geometry("Street sidewalk expansion joints",seams,"Street paving detail");
        }
    }

    private void ValidateCompletedNeighbourhoods()
    {
        Physics.SyncTransforms();
        foreach(var route in publicPaths)
            for(int leg=1;leg<route.Length;leg++)
            {
                int samples=Mathf.CeilToInt(Vector3.Distance(route[leg-1],route[leg])/.5f);
                for(int i=0;i<=samples;i++)
                {
                    var p=Vector3.Lerp(route[leg-1],route[leg],(float)i/samples);p.y=1;
                    var obstacles=Physics.OverlapBox(p,new Vector3(.27f,.7f,.27f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
                        .Where(c=>c.transform.IsChildOf(root.transform)).ToArray();
                    if(obstacles.Length>0)throw new InvalidOperationException("Authored pedestrian connection blocked at "+p+": "+string.Join(",",obstacles.Select(c=>c.name)));
                }
            }
        Directory.CreateDirectory("Library/TownWorldQA");
        File.WriteAllText("Library/TownWorldQA/detail-coverage.txt",
            $"PASS {completedFrontages} completed block frontages; {smallCourtyards} formerly empty small courts; {connectedDoors} doors connected through {entrancePaths.Count} path segments; no decorative townspeople; {neighbourhoodStreetOpenings} refreshed courtyard/garage curb openings.\n"+
            "Every route and public path sampled against final world colliders at 0.5m intervals. Supplier/farm preservation is checked separately against the previous world.\n");
    }
}
