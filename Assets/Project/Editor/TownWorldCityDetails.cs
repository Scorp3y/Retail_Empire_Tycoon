using System;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEngine;

/// <summary>District-specific placement. All coordinates are native town metres, outside supplier plots.</summary>
internal sealed partial class TownWorldAssembler
{
    private readonly System.Collections.Generic.List<Rect> courtyardVehicleAisles=new System.Collections.Generic.List<Rect>();
    private GameObject CityProp(string id,Vector3 position,float yaw=0,string section="City public details",bool solid=false)
    {
        var bounds=models[id].GetComponent<MeshFilter>().sharedMesh.bounds;
        var rotation=Quaternion.Euler(0,yaw,0);
        var corners=new[]{bounds.min,bounds.max,new Vector3(bounds.min.x,0,bounds.max.z),new Vector3(bounds.max.x,0,bounds.min.z)};
        float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue;
        foreach(var corner in corners)
        {
            Vector3 p=position+rotation*corner;
            minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z);
        }
        var footprint=Rect.MinMaxRect(minX,minZ,maxX,maxZ);
        if(footprint.Overlaps(layout.reservedShopPlot)||footprint.Overlaps(new Rect(-282,-196,70,378))
            ||footprint.Overlaps(new Rect(454,236,200,238)))
            throw new InvalidOperationException("Detail would touch a protected shop/supplier/farm plot: "+id);
        if((section.StartsWith("Residential",StringComparison.Ordinal)||section=="Office courtyard"||section=="Courtyard utilities"||section=="Quiet courtyard")
            &&courtyardVehicleAisles.Exists(footprint.Overlaps))
            throw new InvalidOperationException("Courtyard furniture occupies the vehicle aisle: "+id);
        var instance=Model(id,position,yaw,section);
        if(solid)BoxCollider(instance,bounds.center,bounds.size);
        return instance;
    }
    private void DetailBuilding(string id,Vector3 position,float yaw,Vector3 body)
    {
        var instance=CityProp(id,position,yaw,"Mixed low-rise streets");
        BoxCollider(instance,Vector3.up*body.y/2,body);
        bool side=Mathf.RoundToInt(yaw/90)%2!=0;
        float width=side?body.z:body.x,depth=side?body.x:body.z;
        buildingAreas.Add(new Rect(position.x-width/2-2,position.z-depth/2-2,width+4,depth+4));
        var entrances=id=="TownhousePair"?new[]{-4.6f,4.6f}:id=="NeighbourhoodShops"?new[]{-15.8f,-6.8f,2.2f,11.2f}
            :id=="BrickAnnex"?new[]{-2.4f}:id=="AutoWorkshop"?new[]{7.5f}:id=="GaragePair"?new[]{-3f,3f}:new[]{0f};
        ConnectBuildingEntrances(instance,body.z,entrances,id!="GaragePair");
    }
    private void CivicQuarter(Rect block)
    {
        DistrictPaving("Postal square paving",block,TownColor.Pavement);
        DetailBuilding("PostOffice",new Vector3(-94,0,168),180,new Vector3(20,6.2f,12));
        DetailBuilding("CornerCafe",new Vector3(-60,0,168),180,new Vector3(24,6.2f,14));
        CityProp("CafeTerrace",new Vector3(-45,.06f,156),180,"Postal square");
        CityProp("ClockColumn",new Vector3(-78,0,155),0,"Postal square",true);
        CityProp("BicycleRack",new Vector3(-106,.05f,155.5f),90,"Postal square");
        Model("Bench",new Vector3(-87,0,154),180,"Postal square");
        Model("FlowerPlanter",new Vector3(-73,0,155),0,"Postal square");
        Surface("Postal square rear planting",new Rect(-112,177,76,6),TownColor.LeafLight,.06f);
        foreach(float x in new[]{-106f,-86f,-62f,-40f})Model("TreeRound",new Vector3(x,0,180),0,"Postal square gardens",Vector3.one*.8f);
        foreach(var island in new[]{new Rect(-112,156,4,5),new Rect(-75,156,4,4)})
        {
            Surface("Postal square planted island",island,TownColor.LeafLight,.065f);
            Model("TreeRound",new Vector3(island.center.x,0,island.center.y),0,"Postal square gardens",Vector3.one*.8f);
            Border(island,TownColor.Pavement,"Postal square low edging");
        }
        Model("Bench",new Vector3(-81,0,159),180,"Postal square");
        CompleteCivicSquare();
    }
    private void TownhouseQuarter(Rect block)
    {
        BuildTownhousePilot(block);
    }
    private void SmallOfficeQuarter(Rect block)
    {
        DistrictPaving("Small office plaza",block,TownColor.Pavement);
        DetailBuilding("OfficeAnnex",new Vector3(203,0,169),180,new Vector3(20,9.3f,12));
        CityProp("Pergola",new Vector3(234,.05f,173),0,"Small office plaza");
        CityProp("ClockColumn",new Vector3(242,0,157),0,"Small office plaza",true);
        CityProp("BicycleRack",new Vector3(190,.05f,154),90,"Small office plaza");
        CityProp("UtilityCabinet",new Vector3(253,0,179),0,"Small office plaza",true);
        Surface("Office planted island",new Rect(225,154,9,8),TownColor.LeafLight,.06f);
        Model("TreeRound",new Vector3(229,0,158),0,"Small office plaza");
        Model("Bench",new Vector3(237,0,157),180,"Small office plaza");
        FinishBlockFrontage(block);
    }
    private void WorkshopQuarter(Rect block)
    {
        DistrictPaving("Workshop pavement",block,TownColor.Stone);
        DetailBuilding("AutoWorkshop",new Vector3(108,0,-165),180,new Vector3(20,4.8f,12));
        Surface("Workshop service apron",new Rect(91,-184,34,13),TownColor.Asphalt,.06f);
        // This is scenery, not a new supplier, shop upgrade or collection interaction.
        CityProp("UtilityCabinet",new Vector3(124,0,-169),180,"Workshop frontage",true);
        CityProp("BinEnclosure",new Vector3(139,.05f,-162),180,"Workshop frontage",true);
        Model("VisitorSedan",new Vector3(132,0,-180),180,"Parked cars");
        Model("StreetLamp",new Vector3(155,0,-180),0,"Workshop frontage");
        DetailBuilding("BrickAnnex",new Vector3(143,0,-170),180,new Vector3(9,4.4f,6));
        FinishBlockFrontage(block);
    }
    private void FurnishGarden(Rect block,Rect garden,bool offices)
    {
        // Traffic follows the edge, not the middle of the children's or sports area.
        var court=new Rect(block.xMin+20,block.yMin+21,block.width-25,block.height-42);
        var aisles=new[]{new Rect(court.xMin,court.yMin+8,court.width,4),
            new Rect(court.xMax-4,court.yMin+10,4,block.center.y-court.yMin-8),
            new Rect(court.xMax,block.center.y-2,block.xMax-court.xMax,4)};
        foreach(var aisle in aisles)
        {
            courtyardVehicleAisles.Add(aisle);
            Surface("Courtyard vehicle aisle",aisle,TownColor.Asphalt,.085f);
        }
        foreach(float x in new[]{garden.xMin+1,garden.xMax-2})
        {
            Model("TreeRound",new Vector3(x,0,garden.yMin+3),0,"Courtyard edge planting",Vector3.one*.8f);
            Model("Bench",new Vector3(x,0,garden.yMax+1.5f),180,"Courtyard furniture");
        }
        CityProp("BinEnclosure",new Vector3(garden.xMax-3,.06f,garden.yMax-2),180,"Courtyard utilities",true);
        ConnectGarden(block,garden);
        if(offices)
        {
            CityProp("Pergola",new Vector3(garden.xMin+9,.08f,garden.center.y),0,"Office courtyard");
            CityProp("BicycleRack",new Vector3(garden.xMin+20,.08f,garden.yMin+6),90,"Office courtyard");
            Surface("Office garden walkway",new Rect(garden.xMin+3,garden.yMin+2,2.2f,garden.height-4),TownColor.Sand,.09f);
        }
        else if(block.xMin>-30)
        {
            float sportZ=garden.height>40?garden.yMin+12:garden.center.y+1;
            CityProp("BasketballCourt",new Vector3(garden.xMin+13,.09f,sportZ),0,"Residential sport courts");
            CityProp("OutdoorGym",new Vector3(garden.xMin+29,.09f,sportZ),0,"Residential sport courts");
            if(garden.height>40)
            {
                CityProp("SwingSet",new Vector3(garden.xMin+8,.09f,garden.yMax-16),0,"Residential playgrounds");
                CityProp("PlaySlide",new Vector3(garden.xMin+22,.09f,garden.yMax-13),0,"Residential playgrounds");
                CityProp("Sandpit",new Vector3(garden.xMin+11,.09f,garden.yMax-6),0,"Residential playgrounds");
            }
        }
        else
        {
            CityProp("PlaySlide",new Vector3(garden.xMin+7,.09f,garden.yMin+6),0,"Residential playgrounds");
            CityProp("SwingSet",new Vector3(garden.xMin+20,.09f,garden.yMin+7),0,"Residential playgrounds");
            CityProp("Sandpit",new Vector3(garden.xMin+9,.09f,garden.yMax-5),0,"Residential playgrounds");
            Surface("Playground footpath",new Rect(garden.xMin+2,garden.yMin+1,2,garden.height-2),TownColor.Sand,.1f);
        }
    }
    private void FacadeDetails(GameObject building,float width,float depth,float height,bool office,int variant)
    {
        void Attach(string id,Vector3 local,float yaw=0,Vector3? scale=null)
        {
            // Validate in world coordinates, then keep transform ownership with the building.
            var detail=CityProp(id,building.transform.TransformPoint(local),building.transform.localEulerAngles.y+yaw,"Building service details");
            detail.transform.SetParent(building.transform,true);
            if(scale.HasValue)detail.transform.localScale=scale.Value;
        }
        foreach(float x in new[]{-width/2+.3f,width/2-.3f})
            Attach("Downpipe",new Vector3(x,0,-depth/2-.16f),180,new Vector3(1,height/6,1));
        if(office||variant==1||variant==4)
        {
            Attach("VentilationBank",new Vector3(-width*.23f,height+.11f,0));
            Attach("VentilationBank",new Vector3(width*.24f,height+.11f,-1));
            Attach("RoofAerial",new Vector3(width*.35f,height+.11f,depth*.25f));
            if(width>=26)Attach("RoofGarden",new Vector3(0,height+.12f,depth*.3f));
        }
        else
        {
            Attach("RoofAerial",new Vector3(width*.3f,height+2.5f,0));
            Attach("ChimneyStack",new Vector3(-width*.3f,height+2.25f,0));
        }
        for(float x=-width/2+7;x<width/2-3;x+=14)
            Attach("AirConditioner",new Vector3(x,2.9f,-depth/2-.5f),180);
    }
    private void CityPublicDetails()
    {
        // Outside editable/purchasable store land; a custom shop cannot build into a permanent shelter.
        Surface("Trolley shelter sidewalk recess",new Rect(-43.5f,-135.5f,5,3),TownColor.Pavement,.075f);
        var corral=CityProp("TrolleyCorral",new Vector3(-41,.08f,-133.8f),180,"Player frontage furniture");
        foreach(float x in new[]{-1.7f,1.7f})foreach(float z in new[]{-.9f,.9f})
            BoxCollider(corral,new Vector3(x,1.1f,z),new Vector3(.09f,2.2f,.09f));
        CityProp("ParkArch",new Vector3(24,0,155),0,"Park entrance");
        Surface("Park gate footpath",new Rect(21.5f,154.5f,5,8),TownColor.Sand,.06f);
        CityProp("BicycleRack",new Vector3(34,.04f,156),90,"Park entrance");
        CityProp("Pergola",new Vector3(5,.04f,170),90,"Park amenities");
        var shelter=CityProp("BusShelter",new Vector3(-80,.04f,-151.5f),180,"Bus stops");
        foreach(float x in new[]{-2.8f,2.8f})foreach(float z in new[]{-1.2f,1.2f})
            BoxCollider(shelter,new Vector3(x,1.35f,z),new Vector3(.12f,2.7f,.12f));
        BoxCollider(shelter,new Vector3(0,1.4f,-1.2f),new Vector3(5.5f,2.4f,.045f));
        foreach(var centre in new[]{new Vector3(-120,0,-192),new Vector3(168,0,48),new Vector3(72,0,144)})
        {
            foreach(float x in new[]{-6.8f,6.8f})foreach(float z in new[]{-6.8f,6.8f})
            {
                // Signals are outside the carriageway and do not pretend to control traffic AI.
                var signal=CityProp("TrafficSignal",centre+new Vector3(x,0,z),z>0?180:0,"Main junction signals");
                BoxCollider(signal,new Vector3(0,1.7f,0),new Vector3(.2f,3.4f,.2f));
                CityProp("DrainGrate",centre+new Vector3(x>0?5.4f:-5.4f,.02f,z>0?9:-9),0,"Street service covers");
            }
            CityProp("ManholeCover",centre+new Vector3(2.5f,.02f,15),0,"Street service covers");
        }
        // A short river walk, not a new road or any decoration on the farm side.
        const float start=-119,end=-71;
        var path=new TownMeshBuilder();
        for(float z=start;z<end;z+=3)
        {
            float a=layout.RiverCenter(z)-23,b=layout.RiverCenter(z+3)-23;
            path.Quad(new Vector3(a-1.5f,.07f,z),new Vector3(b-1.5f,.07f,z+3),new Vector3(b+1.5f,.07f,z+3),new Vector3(a+1.5f,.07f,z),TownColor.Sand);
            if(((int)z+119)%9==0)CityProp("RiverReeds",new Vector3(layout.RiverCenter(z)-19.1f,0,z),0,"Soft riverbank planting");
        }
        Geometry("Riverbank walking path",path,"Riverfront promenade");
        var connection=new Rect(269,-121,layout.RiverCenter(start)-23-269,3);
        Surface("River walk sidewalk connection",connection,TownColor.Sand,.075f);pavedAreas.Add(connection);
        // Register its footprint before woodland generation; no tree trunks grow through the path.
        pavedAreas.Add(new Rect(layout.RiverCenter(start)-29,start-2,17,end-start+4));
        float viewX=layout.RiverCenter(-95)-23;
        Surface("River viewing place",new Rect(viewX-7,-100,6,9),TownColor.Pavement,.08f);
        Model("Bench",new Vector3(viewX-4,0,-96),90,"River viewing place");
        CityProp("BollardGroup",new Vector3(viewX-6,0,-101),0,"River viewing place");
    }
}
