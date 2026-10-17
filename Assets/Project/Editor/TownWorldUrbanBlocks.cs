using System;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEngine;

internal sealed partial class TownWorldAssembler
{
    private void CompactCity()
    {
        CornerHousingQuarter(new Rect(-210,-66,84,36));
        UrbanBlock(new Rect(-210,-18,84,60),5,false);
        // Reuse the former scattered supplier plots as serviced city blocks.
        UrbanBlock(new Rect(-204,60,78,78),5,true);
        UrbanBlock(new Rect(-204,-138,78,60),4,false);
        UrbanBlock(new Rect(-108,56,72,82),5,false);
        UrbanBlock(new Rect(180,54,78,84),5,true);
        UrbanBlock(new Rect(-18,54,84,84),5,false);
        UrbanBlock(new Rect(78,54,84,84),9,true);
        CivicQuarter(new Rect(-114,150,84,36));
        UrbanBlock(new Rect(78,150,84,36),6,true);
        SmallOfficeQuarter(new Rect(180,150,78,36));
        foreach(float x in new[]{-210f,-114f,-18f,78f,180f})
        {
            if(x==-210)TownhouseQuarter(new Rect(x,-186,84,36));
            else if(x==78)WorkshopQuarter(new Rect(x,-186,84,36));
            else if(x==-114)NeighbourhoodShoppingQuarter(new Rect(x,-186,84,36));
            else if(x==180)GardenHousingQuarter(new Rect(x,-186,78,36));
            else UrbanBlock(new Rect(x,-186,x==180?78:84,36),x==-18?6:4,x==-18);
        }

        // These fronts frame the reserve but never occupy it. A side street serves the long offices.
        foreach(float x in new[]{-40f,8f,56f})UrbanBuilding(new Vector3(x,0,-3),34,10,4,0,false,"ЖИЛОЙ ДОМ");
        foreach(float x in new[]{-72f,24f})UrbanBuilding(new Vector3(x,0,30),60,12,5,180,true,"ОФИСЫ");
        UrbanBuilding(new Vector3(102,0,-78),48,12,6,90,true,"ДЕЛОВОЙ ЦЕНТР");
        UrbanBuilding(new Vector3(102,0,-34),26,12,4,90,false,"ТОРГОВЫЙ ДОМ");
        UrbanBuilding(new Vector3(144,0,-117),30,14,9,270,true,"ОФИСЫ");
        UrbanBuilding(new Vector3(144,0,-9),24,14,5,270,true,"ОФИСЫ");
        UrbanBuilding(new Vector3(144,0,30),20,14,4,270,false,"ЖИЛОЙ ДОМ");

        // Construction occupies a serviced block in town, across the local street from the store reserve.
        ApprovedModel("07_ConstructionSite",new Vector3(144,-.14f,-52),270,"Construction plot",
            new[]{Wall(-1,4,-1,10,8,10),Wall(-10.4f,1.2f,0,.25f,2,20),Wall(10.4f,1.2f,0,.25f,2,20)});
        Driveway(new Vector3(132,0,-52),new Vector3(120,0,-52));
        Model("WasteContainer",new Vector3(155,0,-61),0,"Construction plot");

        UrbanBlock(new Rect(180,-78,78,120),4,false);
        ApprovedPark();
        ApprovedModel("05_BusStop",new Vector3(90,-.14f,-151),180,"Bus stops",
            new[]{Wall(-2.5f,1.62f,0,.15f,2.75f,.15f),Wall(2.5f,1.62f,0,.15f,2.75f,.15f)});
        ApprovedModel("04_FuelStation",new Vector3(216,-.14f,-100),180,"Roadside gas station",
            new[]{Wall(6,2.1f,-5,7,3.6f,6),Wall(-6,2.75f,0,.24f,4.95f,.24f),Wall(0,2.75f,0,.24f,4.95f,.24f),Wall(-6,1.04f,-1.2f,.8f,1.6f,.65f),Wall(0,1.04f,-1.2f,.8f,1.6f,.65f)},removeGroundPad:true);
        Surface("Connected gas station forecourt",new Rect(200,-116,34,24),TownColor.Asphalt,.035f);
        Driveway(new Vector3(204,0,-112),new Vector3(204,0,-144));
        Driveway(new Vector3(228,0,-112),new Vector3(228,0,-144));
        // Small amenities belong to defined plots, not isolated lawns.
        Model("LitterBin",new Vector3(86,0,-150),0,"Street furniture");
        Model("Bench",new Vector3(80,0,-150),180,"Street furniture");
    }

    private void UrbanBlock(Rect block,int floors,bool offices)
    {
        DistrictPaving("Urban block pedestrian paving",block,offices?TownColor.Pavement:TownColor.Sand);
        float length=block.width-24;
        if(block.height<45)
        {
            UrbanBuilding(new Vector3(block.center.x,0,block.center.y),length,14,floors,180,offices,offices?"ОФИСЫ":"МАГАЗИНЫ");
            for(float x=block.xMin+5;x<block.xMax;x+=14)
                StreetTree(new Vector3(x,0,block.yMin+4),.9f);
            FinishBlockFrontage(block);
            return;
        }
        UrbanBuilding(new Vector3(block.center.x+5,0,block.yMin+9),length-6,14,floors,180,offices,offices?"ОФИСЫ":"ЖИЛОЙ ДОМ");
        UrbanBuilding(new Vector3(block.center.x+5,0,block.yMax-9),length-6,14,floors+1,0,offices,offices?"БИЗНЕС ЦЕНТР":"ЖИЛОЙ ДОМ");
        UrbanBuilding(new Vector3(block.xMin+9,0,block.center.y),block.height-40,12,floors,270,offices,offices?"ОФИСЫ":"ЖИЛОЙ ДОМ");
        var court=new Rect(block.xMin+20,block.yMin+21,block.width-25,block.height-42);
        Surface("Shared residential access court",court,TownColor.Asphalt,.05f);
        Driveway(new Vector3(block.xMax,0,block.center.y),new Vector3(block.xMax+6,0,block.center.y));
        ParkingRow(new Vector3(court.xMin+2,0,court.yMin+4),Mathf.Min(8,Mathf.FloorToInt((court.width-4)/4.42f)),true,"Inner courtyard");
        if(court.height>24)
        {
            var garden=new Rect(court.xMin+4,court.yMin+12,court.width-12,court.height-17);
            Surface("Courtyard shared garden",garden,TownColor.LeafLight,.075f);
            FurnishGarden(block,garden,offices);
        }
        else FurnishQuietCourt(block,court,offices);
        Model("WasteContainer",new Vector3(block.xMax-2,0,block.center.y-7),0,"Urban block utilities");
        Model("StreetLamp",new Vector3(block.xMax-2,0,block.center.y+7),270,"Urban block lighting");
        FinishBlockFrontage(block);
    }

    private GameObject UrbanBuilding(Vector3 position,float width,float depth,int floors,float yaw,bool office,string sign,
        bool serviceDetails=true,int? variantOverride=null)
    {
        if(variantOverride.HasValue&&(variantOverride.Value<0||variantOverride.Value>4))
            throw new ArgumentOutOfRangeException(nameof(variantOverride),"Building variant must be between zero and four.");
        float height=floors*3.1f;
        var entrances=new System.Collections.Generic.List<float>();
        for(float entrance=-width/2+5;entrance<width/2;entrance+=12)entrances.Add(entrance);
        int variant=variantOverride??Mathf.Abs(Mathf.RoundToInt(position.x/6+position.z/12))%5;
        var mesh=new TownMeshBuilder();var facade=office?(variant==1?TownColor.Metal:TownColor.Cream):((floors%2==0)?TownColor.Brick:TownColor.Cream);
        mesh.Box(new Vector3(0,height/2,0),new Vector3(width,height,depth),facade);
        mesh.Box(new Vector3(0,.4f,0),new Vector3(width+.3f,.8f,depth+.3f),TownColor.Stone);
        for(int floor=0;floor<floors;floor++)
        {
            float y=1.9f+floor*3.1f;
            foreach(float face in new[]{-1f,1f})for(float x=-width/2+2;x<width/2-1;x+=3.4f)
            {
                float windowHalfWidth=(office?2.4f:1.6f)/2;
                // Omit every ground-floor window whose full frame intersects an entrance bay.
                if(floor==0&&face>0&&entrances.Exists(entrance=>Mathf.Abs(x-entrance)<windowHalfWidth+1.25f))continue;
                if(!office&&face>0&&floor>0&&entrances.Exists(entrance=>Mathf.Abs(x-entrance)<2.1f))continue;
                mesh.Box(new Vector3(x,y,face*(depth/2+.045f)),new Vector3(office?2.4f:1.6f,1.7f,.09f),TownColor.White);
                mesh.Box(new Vector3(x,y,face*(depth/2+.11f)),new Vector3(office?2.2f:1.4f,1.5f,.035f),TownColor.Glass);
                if(!office&&floor>0&&face>0&&Mathf.Abs(Mathf.RoundToInt((x+width/2)/3.4f))%3==variant)
                {
                    mesh.Box(new Vector3(x,y-1,depth/2+.7f),new Vector3(2,.15f,1.4f),TownColor.Pavement);
                    mesh.Box(new Vector3(x,y-.6f,depth/2+1.3f),new Vector3(2,.8f,.12f),variant==1?TownColor.Frame:TownColor.Mint);
                    foreach(float edge in new[]{-1f,1f})mesh.Box(new Vector3(x+edge,y-.6f,depth/2+.7f),new Vector3(.1f,.8f,1.2f),TownColor.Frame);
                }
            }
            foreach(float side in new[]{-1f,1f})for(float z=-depth/2+2;z<depth/2-1;z+=3)
                mesh.Box(new Vector3(side*(width/2+.06f),y,z),new Vector3(.1f,1.5f,1.4f),TownColor.Glass);
            mesh.Box(new Vector3(0,3.1f*(floor+1),0),new Vector3(width+.12f,.13f,depth+.12f),TownColor.Pavement);
            if(variant>=3)
                foreach(float x in new[]{-width/2+.3f,width/2-.3f})
                    mesh.Box(new Vector3(x,y,depth/2+.16f),new Vector3(.5f,3.1f,.2f),office?TownColor.Metal:TownColor.Brick);
        }
        TownArchitecturalParts.FlatRoof(mesh,width,depth,height);
        if(!office)foreach(float entrance in entrances)for(int landing=0;landing<floors-1;landing++)
            TownArchitecturalParts.Window(mesh,new Vector3(entrance,3.65f+landing*3.1f,depth/2+.06f),1,1);
        if(!office&&variant!=1&&variant!=4)mesh.Gable(new Vector3(0,height+.2f,0),width+.6f,depth+.6f,2.4f,variant==2?TownColor.RoofDark:TownColor.Mint,facade);
        if(office&&variant>=3)
        {
            mesh.Box(new Vector3(0,height+1.1f,-1),new Vector3(width*.45f,1.8f,depth*.6f),TownColor.Glass);
            mesh.Box(new Vector3(0,height+2.05f,-1),new Vector3(width*.45f+.3f,.12f,depth*.6f+.3f),TownColor.RoofDark);
        }
        if(office)
            for(float x=-width/2+6;x<width/2;x+=12)mesh.Box(new Vector3(x,height/2,depth/2+.14f),new Vector3(.22f,height,.16f),TownColor.Frame);
        foreach(float x in entrances)
        {
            TownArchitecturalParts.Door(mesh,new Vector3(x,office?1.25f:1.6f,depth/2+.15f),1.8f,2.4f,true);
        }
        var instance=Geometry(office?"Office long block":"Residential long block",mesh,"Urban buildings");
        instance.transform.localPosition=position;instance.transform.localRotation=Quaternion.Euler(0,yaw,0);
        BoxCollider(instance,new Vector3(0,height/2,0),new Vector3(width,height,depth));
        bool rotated=Mathf.RoundToInt(yaw/90)%2!=0;
        var footprint=new Rect(position.x-(rotated?depth:width)/2,position.z-(rotated?width:depth)/2,rotated?depth:width,rotated?width:depth);
        if(footprint.Overlaps(layout.reservedShopPlot))throw new InvalidOperationException("Urban building overlaps purchasable store land.");
        buildingAreas.Add(new Rect(footprint.xMin-2,footprint.yMin-2,footprint.width+4,footprint.height+4));
        if(serviceDetails)FacadeDetails(instance,width,depth,height,office,variant);
        ConnectBuildingEntrances(instance,depth,entrances);
        return instance;
    }
}
