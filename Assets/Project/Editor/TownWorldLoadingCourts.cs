using System;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using RetailEmpireTycoon.Logistics;
using UnityEngine;

internal sealed partial class TownWorldAssembler
{
    private void RearCourtyardSuppliers()
    {
        // Each entrance meets an opened west curb on the same connected city street.
        var citySuppliers=new[]{SupplierKind.Wholesale,SupplierKind.Construction,SupplierKind.Equipment,SupplierKind.Bakery,SupplierKind.Decoration};
        for(int i=0;i<citySuppliers.Length;i++)
            CityLoadingCourt(citySuppliers[i],new Rect(-280,-180+i*60,56,60),false);
        WesternShowroom();

        ApprovedModel("06_Warehouse",new Vector3(-245,-.14f,199),180,"Industrial yards",new[]{Wall(-1.5f,3.14f,-3,18,5.8f,11)});
        Surface("Freight loading court",new Rect(-260,177,32,13),TownColor.Pavement);
        Driveway(new Vector3(-245,0,182),new Vector3(-216,0,180));
        Model("BoxTruck",new Vector3(-238,0,182),90,"Industrial yards");
        Model("CargoContainer",new Vector3(-250,0,213),90,"Industrial yards");
        var dock=new TownMeshBuilder();dock.Box(new Vector3(-260,1,198),new Vector3(10,2,24),TownColor.Pavement);
        dock.Box(new Vector3(-264.8f,2.025f,198),new Vector3(.15f,.05f,24),TownColor.Yellow);
        Geometry("Rail freight transfer platform",dock,"Industrial yards");
        FarmLoadingCourt();
    }

    private void CityLoadingCourt(SupplierKind supplier,Rect plot,bool westEntrance)
    {
        // The public facade is south; loading is north, separated by the building itself.
        float width=plot.width-18;var building=new Vector3(plot.center.x,0,plot.yMin+7);
        ReceivingBuilding(supplier,building,width,12,180);
        var yard=new Rect(plot.xMin,plot.yMin+14,plot.width,plot.height-14);
        Surface("Rear service court "+supplier,yard,TownColor.Asphalt,.055f);
        Surface("Supplier public frontage",new Rect(plot.xMin,plot.yMin-1,plot.width,2),TownColor.Pavement,.07f);
        var bay=new Vector3(building.x,0,plot.yMin+18);
        var station=LoadingCheckpoint(supplier,bay,0,true);
        AttachedLoadingCanopy(new Vector3(building.x,0,plot.yMin+17),0);
        var entrance=new Vector3(westEntrance?plot.xMin:plot.xMax,0,plot.yMin+36);
        var street=new Vector3(westEntrance?plot.xMin-12:plot.xMax+8,0,entrance.z);
        Driveway(entrance,street);
        ServiceCourtFence(yard,entrance,westEntrance?3:1);
        RegisterCourt(yard,street,entrance,new Vector3(bay.x,0,entrance.z),station);
        Model("WarehouseRack",new Vector3(plot.xMin+4,0,plot.yMax-7),90,"Court storage");
        Model("WasteContainer",new Vector3(plot.xMax-4,0,plot.yMax-4),0,"Court utilities");
        for(int i=0;i<4;i++)Model("Pallet",new Vector3(plot.xMin+4+i*2,0,plot.yMax-3),0,"Court storage");
        ParkingRow(new Vector3(plot.xMin+14,0,plot.yMax-5),Mathf.Min(5,Mathf.FloorToInt((plot.width-30)/4.42f)),true,"Supplier staff");
        Building("Garage",new Vector3(plot.xMax-7,0,plot.yMax-5),180,new Vector3(5.4f,4.4f,6.5f),"Service workshops");
        Model("BoxTruck",new Vector3(plot.xMax-7,0,plot.yMax-17),0,"Court service vehicles");
        Model("StreetLamp",new Vector3(plot.xMin+2,0,plot.yMax-14),90,"Court lighting");
        YardWayfinding(entrance,new Vector3(bay.x,0,entrance.z),bay,station.transform.localRotation);
        CourtDirectionMark(bay+Vector3.forward*10,0);
    }

    private void WesternShowroom()
    {
        Building("CarDealership",new Vector3(-252,0,127),180,new Vector3(16,4.5f,10),"District suppliers");
        Surface("Showroom customer forecourt",new Rect(-280,134,56,42),TownColor.Asphalt,.05f);
        var showroom=LoadingCheckpoint(SupplierKind.Showroom,new Vector3(-252,0,138),0,false);
        Driveway(new Vector3(-224,0,156),new Vector3(-216,0,156));
        ParkingRow(new Vector3(-278,0,169),3,false,"Showroom display");
        for(int i=0;i<3;i++)Model("DisplayCar",new Vector3(-278+(i+.5f)*4.42f,0,168),180,"Showroom forecourt");
        CourtDirectionMark(showroom.transform.localPosition+Vector3.forward*10,0);
    }

    private void FarmLoadingCourt()
    {
        var yard=new Rect(524,279,60,36);
        ReceivingBuilding(SupplierKind.Farmers,new Vector3(560,0,321),32,12,0);
        Surface("Farm rear packing and collection court",yard,TownColor.Asphalt,.055f);
        var bay=new Vector3(560,0,311);
        var station=LoadingCheckpoint(SupplierKind.Farmers,bay,180,true);
        AttachedLoadingCanopy(new Vector3(560,0,311),180);
        var entrance=new Vector3(550,0,279);var staging=new Vector3(560,0,292);
        ServiceCourtFence(yard,entrance,2);
        RegisterCourt(yard,new Vector3(550,0,272),entrance,staging,station);
        CourtDirectionMark(new Vector3(560,0,301),180);
        Building("FarmersMarket",new Vector3(513,0,292),180,new Vector3(12,3.5f,6),"Farm visitor market");
        Surface("Farm visitor layby",new Rect(509,278,12,8),TownColor.Asphalt,.04f);
        ParkingRow(new Vector3(509,0,282),3,true,"Farm visitor market",sizeScale:1);
        Driveway(new Vector3(519,0,282),new Vector3(519,0,270));
        Driveway(new Vector3(519,0,270),new Vector3(550,0,270));
        for(int i=0;i<4;i++)Model("FruitCrate",new Vector3(529+i*2,0,309),0,"Farm packing storage");
        Model("ForkliftStatic",new Vector3(580,0,307),180,"Farm packing storage");
        YardWayfinding(entrance,staging,bay,station.transform.localRotation);
        Model("StreetLamp",new Vector3(526,0,292),90,"Farm court lighting");
        Driveway(new Vector3(582,0,294),new Vector3(608,0,294));
        Driveway(new Vector3(608,0,294),new Vector3(608,0,325));
    }

    private DeliveryCheckpoint LoadingCheckpoint(SupplierKind supplier,Vector3 position,float yaw,bool rearDocking)
    {
        var marker=new GameObject(SupplierRouting.Name(supplier));marker.transform.SetParent(Section("Supplier checkpoints"),false);
        marker.transform.localPosition=position+Vector3.up*.075f;marker.transform.localRotation=Quaternion.Euler(0,yaw,0);
        var checkpoint=marker.AddComponent<DeliveryCheckpoint>();checkpoint.supplier=supplier;checkpoint.radius=2.2f;
        checkpoint.kind=supplier==SupplierKind.Showroom?CheckpointKind.Showroom:CheckpointKind.Depot;
        checkpoint.displayName=SupplierRouting.Name(supplier);checkpoint.requiresRearDocking=rearDocking;suppliers.Add(checkpoint);
        Model("PickupStopZone",position+Vector3.up*.09f,yaw,"Loading bays");
        var rackPosition=position+Quaternion.Euler(0,yaw,0)*new Vector3(7,0,-1);
        Model(rearDocking?"PickupSign":"ParkingSign",rackPosition,yaw,"Supplier signs");
        return checkpoint;
    }

    private void ReceivingBuilding(SupplierKind supplier,Vector3 position,float width,float depth,float yaw)
    {
        const float height=7;
        var mesh=new TownMeshBuilder();
        mesh.Box(new Vector3(0,height/2,0),new Vector3(width,height,depth),supplier==SupplierKind.Farmers?TownColor.Brick:TownColor.Cream);
        mesh.Box(new Vector3(0,.4f,0),new Vector3(width+.3f,.8f,depth+.3f),TownColor.Stone);
        TownArchitecturalParts.FlatRoof(mesh,width,depth,height);
        TownArchitecturalParts.Sign(mesh,new Vector3(0,5.7f,depth/2+.16f),Mathf.Min(23,width-2),1,SupplierRouting.Name(supplier).ToUpperInvariant());
        TownArchitecturalParts.Door(mesh,new Vector3(0,1.5f,depth/2+.1f),1.8f,2.8f,true);
        for(float x=-width/2+3;x<width/2-1;x+=5)
            if(Mathf.Abs(x)>2)TownArchitecturalParts.Window(mesh,new Vector3(x,2.8f,depth/2+.06f),3,2);
        TownArchitecturalParts.LoadingDoor(mesh,new Vector3(0,2.5f,-depth/2-.12f),5,5,true);
        var instance=Geometry("Receiving building "+supplier,mesh,"District suppliers");
        instance.transform.localPosition=position;instance.transform.localRotation=Quaternion.Euler(0,yaw,0);
        BoxCollider(instance,new Vector3(0,height/2,0),new Vector3(width,height,depth));
        buildingAreas.Add(new Rect(position.x-width/2-1,position.z-depth/2-1,width+2,depth+2));
        var rearSign=new TownMeshBuilder();TownArchitecturalParts.Sign(rearSign,new Vector3(0,6.5f,0),14,.75f,supplier==SupplierKind.Farmers?"ОВОЩИ И ФРУКТЫ":"ВЫДАЧА ТОВАРА");
        var sign=Geometry("Rear goods collection sign",rearSign,"Loading signs");sign.transform.SetParent(instance.transform,false);
        sign.transform.localPosition=new Vector3(0,0,-depth/2-.17f);sign.transform.localRotation=Quaternion.Euler(0,180,0);
    }

    private void AttachedLoadingCanopy(Vector3 position,float yaw)
    {
        var mesh=new TownMeshBuilder();mesh.Box(new Vector3(0,5.8f,0),new Vector3(14,.3f,8),TownColor.Mint);
        foreach(float x in new[]{-6.7f,6.7f})mesh.Box(new Vector3(x,2.8f,3.6f),new Vector3(.23f,5.6f,.23f),TownColor.Frame);
        var instance=Geometry("Attached rear loading canopy",mesh,"Loading bays");
        instance.transform.localPosition=position;instance.transform.localRotation=Quaternion.Euler(0,yaw,0);
        foreach(float x in new[]{-6.7f,6.7f})BoxCollider(instance,new Vector3(x,2.8f,3.6f),new Vector3(.23f,5.6f,.23f));
        BoxCollider(instance,new Vector3(0,5.8f,0),new Vector3(14,.3f,8));
    }

    private void RegisterCourt(Rect bounds,Vector3 street,Vector3 entrance,Vector3 staging,DeliveryCheckpoint checkpoint)
    {
        if(bounds.Overlaps(layout.reservedShopPlot))throw new InvalidOperationException("Service court occupies store land.");
        var instance=new GameObject("Service court route "+checkpoint.supplier);instance.transform.SetParent(Section("Loading court routes"),false);
        var court=instance.AddComponent<SupplierLoadingCourt>();court.bounds=bounds;court.streetAccess=street;
        court.entrance=entrance;court.staging=staging;court.checkpoint=checkpoint;
    }

    private void CourtDirectionMark(Vector3 position,float yaw)
    {
        var mesh=new TownMeshBuilder();mesh.Box(new Vector3(0,.09f,0),new Vector3(.25f,.02f,3),TownColor.Yellow);
        mesh.Box(new Vector3(-.5f,.09f,-1),new Vector3(.2f,.02f,1.4f),TownColor.Yellow,Quaternion.Euler(0,-45,0));
        mesh.Box(new Vector3(.5f,.09f,-1),new Vector3(.2f,.02f,1.4f),TownColor.Yellow,Quaternion.Euler(0,45,0));
        var instance=Geometry("Reverse towards loading door arrow",mesh,"Loading bays");
        instance.transform.localPosition=position;instance.transform.localRotation=Quaternion.Euler(0,yaw,0);
    }

    private void YardWayfinding(Vector3 entrance,Vector3 staging,Vector3 bay,Quaternion heading)
    {
        var mesh=new TownMeshBuilder();var travel=staging-entrance;
        int count=Mathf.Max(1,Mathf.FloorToInt(travel.magnitude/5));
        for(int i=0;i<count;i++)PaintedLine(mesh,Vector3.Lerp(entrance,staging,(i+.2f)/count)+Vector3.up*.095f,
            Vector3.Lerp(entrance,staging,(i+.7f)/count)+Vector3.up*.095f,.12f,TownColor.White);
        // Outline the loading corridor without physical dividers in the turning space.
        foreach(float side in new[]{-1f,1f})
        {
            Vector3 Offset(float z)=>bay+heading*new Vector3(side*3,.095f,z);
            PaintedLine(mesh,Offset(-1),Offset(21),.12f,TownColor.Yellow);
        }
        Geometry("Rear yard entry and loading corridor markings",mesh,"Loading bays");
    }

    private void ServiceCourtFence(Rect bounds,Vector3 gate,int gateSide)
    {
        var mesh=new TownMeshBuilder();var instance=new GameObject("Service courtyard boundary");instance.transform.SetParent(Section("Court boundaries"),false);
        var corners=new[]{new Vector3(bounds.xMin,0,bounds.yMax),new Vector3(bounds.xMax,0,bounds.yMax),new Vector3(bounds.xMax,0,bounds.yMin),new Vector3(bounds.xMin,0,bounds.yMin)};
        for(int side=0;side<4;side++)
        {
            // The receiving wall bounds the yard; a fence must not block its loading door.
            if(side==(gateSide==2?0:2)||(gateSide==2&&side==1))continue;
            var a=corners[side];var b=corners[(side+1)%4];var direction=(b-a).normalized;
            if(side==gateSide){FenceSegment(a,gate-direction*6);FenceSegment(gate+direction*6,b);}
            else FenceSegment(a,b);
        }
        var visual=Geometry("Court perimeter fence",mesh,"Court boundaries");visual.transform.SetParent(instance.transform,false);
        void FenceSegment(Vector3 a,Vector3 b)
        {
            float length=Vector3.Distance(a,b);if(length<.1f)return;
            bool alongX=Mathf.Abs(b.x-a.x)>Mathf.Abs(b.z-a.z);
            var size=alongX?new Vector3(length,1.5f,.15f):new Vector3(.15f,1.5f,length);
            mesh.Box((a+b)/2+Vector3.up*.75f,size,TownColor.Frame);
            BoxCollider(instance,(a+b)/2+Vector3.up*.75f,size);
            int count=Mathf.CeilToInt(length/3);
            for(int i=0;i<=count;i++)mesh.Box(Vector3.Lerp(a,b,(float)i/count)+Vector3.up*.8f,new Vector3(.25f,1.6f,.25f),TownColor.Stone);
        }
    }
}
