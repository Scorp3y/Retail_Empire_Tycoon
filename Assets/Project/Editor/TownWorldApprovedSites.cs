using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using RetailEmpireTycoon.Logistics;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed partial class TownWorldAssembler
{
    // Concept dioramas supply the visual mesh; explicit wall volumes supply usable vehicle physics.
    // Glass is kept in its own batch, never flattened into an opaque building material.
    private GameObject ApprovedModel(string id,Vector3 position,float yaw,string section,Bounds[] walls,bool removeMiniatureRails=false,bool removeGroundPad=false)
    {
        // Diorama pads have their top at local y=.24. Keep them above terrain and courtyard paving.
        if(position.y<0)position.y=Mathf.Max(position.y,-.14f);
        var source=models[id];var sourceMesh=source.GetComponent<MeshFilter>().sharedMesh;
        var instance=new GameObject(id);instance.transform.SetParent(Section(section),false);
        instance.transform.localPosition=position;instance.transform.localRotation=Quaternion.Euler(0,yaw,0);
        var vertices=sourceMesh.vertices;var normals=sourceMesh.normals;var uv=sourceMesh.uv;
        for(int sub=0;sub<sourceMesh.subMeshCount;sub++)
        {
            var indices=sourceMesh.GetTriangles(sub);var remap=new Dictionary<int,int>();
            var v=new List<Vector3>();var n=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();
            for(int triangle=0;triangle<indices.Length;triangle+=3)
            {
                if(removeMiniatureRails&&Enumerable.Range(0,3).All(j=>vertices[indices[triangle+j]].z>4.5f))continue;
                if(removeGroundPad&&Enumerable.Range(0,3).All(j=>vertices[indices[triangle+j]].y<=.25f))continue;
                if(removeGroundPad&&Enumerable.Range(0,3).All(j=>vertices[indices[triangle+j]].y<=.3f
                    &&Mathf.FloorToInt(uv[indices[triangle+j]].x*32)==(int)TownColor.Asphalt))continue;
                for(int j=0;j<3;j++)
                {
                    int original=indices[triangle+j];
                    if(!remap.TryGetValue(original,out int compact))
                    {
                        var vertex=vertices[original];
                        // Re-seat the diorama's traffic paint on the common forecourt instead of leaving it floating.
                        if(removeGroundPad&&vertex.y>.298f&&vertex.y<.313f&&Mathf.FloorToInt(uv[original].x*32)==(int)TownColor.White)
                            vertex.y-=.117f;
                        compact=v.Count;remap.Add(original,compact);v.Add(vertex);n.Add(normals[original]);u.Add(uv[original]);
                    }
                    t.Add(compact);
                }
            }
            if(t.Count==0)continue;
            var mesh=new Mesh{name=id+" surface "+sub,indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,u);mesh.SetTriangles(t,0);mesh.RecalculateBounds();
            var visual=new GameObject(sub==0?"Solid architecture":"Transparent glazing",typeof(MeshFilter),typeof(MeshRenderer));
            visual.transform.SetParent(instance.transform,false);visual.GetComponent<MeshFilter>().sharedMesh=mesh;
            visual.GetComponent<MeshRenderer>().sharedMaterial=sub==0?material:TownWorldArtDirection.Glass();
        }
        foreach(var wall in walls)BoxCollider(instance,wall.center,wall.size);
        var bounds=sourceMesh.bounds;var rotation=instance.transform.localRotation;
        var corners=new[]{bounds.min,new Vector3(bounds.min.x,0,bounds.max.z),bounds.max,new Vector3(bounds.max.x,0,bounds.min.z)}
            .Select(p=>position+rotation*p).ToArray();
        buildingAreas.Add(Rect.MinMaxRect(corners.Min(p=>p.x)-2,corners.Min(p=>p.z)-2,corners.Max(p=>p.x)+2,corners.Max(p=>p.z)+2));
        return instance;
    }
    private static Bounds Wall(float x,float y,float z,float width,float height,float depth)=>new Bounds(new Vector3(x,y,z),new Vector3(width,height,depth));
    private void ApprovedSuppliers()
    {
        // Only the northern freight district contains urban warehouses. There is no warehouse at the gas station.
        Site("WholesaleBase",new Vector3(-170,0,171),180,new Vector3(20,5.6f,13),SupplierKind.Wholesale,new Vector3(-170,0,155));
        Site("EquipmentSupplier",new Vector3(-170,0,108),0,new Vector3(14,4.5f,9),SupplierKind.Equipment,new Vector3(-170,0,129));
        Site("ConstructionSupplier",new Vector3(-245,0,162),180,new Vector3(15,4.5f,10),SupplierKind.Construction,new Vector3(-245,0,145));
        Site("Bakery",new Vector3(-165,0,-104),180,new Vector3(8,4.5f,6),SupplierKind.Bakery,new Vector3(-165,0,-127));
        Site("FlowerShop",new Vector3(-67,0,78),180,new Vector3(8,4.5f,6),SupplierKind.Decoration,new Vector3(-67,0,59));
        Site("CarDealership",new Vector3(218,0,108),90,new Vector3(16,4.5f,10),SupplierKind.Showroom,new Vector3(238,0,108));
        // The farm has its own rural access, not a straight driveway back across the river to the city grid.
        RuralSupplier();
        ApprovedModel("06_Warehouse",new Vector3(-245,-.24f,199),180,"Industrial yards",new[]{Wall(-1.5f,3.14f,-3,18,5.8f,11)});
        Surface("Freight loading court",new Rect(-260,177,32,13),TownColor.Pavement);
        Driveway(new Vector3(-245,0,182),new Vector3(-216,0,180));
        Model("BoxTruck",new Vector3(-238,0,182),90,"Industrial yards");
        Model("CargoContainer",new Vector3(-250,0,213),90,"Industrial yards");
        Model("CargoContainer",new Vector3(-232,0,213),90,"Industrial yards");
        var dock=new TownMeshBuilder();dock.Box(new Vector3(-260,1,198),new Vector3(10,2,24),TownColor.Pavement);
        dock.Box(new Vector3(-264.8f,2.025f,198),new Vector3(.15f,.05f,24),TownColor.Yellow);
        Geometry("Rail freight transfer platform",dock,"Industrial yards");
        Model("ForkliftStatic",new Vector3(-182,0,180),90,"Industrial yards");
        for(int i=0;i<5;i++)Model("Pallet",new Vector3(-186+i*1.6f,0,181),0,"Industrial yards");
        Surface("Showroom forecourt",new Rect(225,82,28,40),TownColor.Asphalt);
        foreach(float x in new[]{232f,240f,248f})Model("DisplayCar",new Vector3(x,0,91),0,"Showroom forecourt");
    }
    private void RuralSupplier()
    {
        Building("FarmersMarket",new Vector3(530,0,307),180,new Vector3(12,3.5f,6),"Farm buildings");
        var position=new Vector3(550,0,285);
        var marker=new GameObject(SupplierRouting.Name(SupplierKind.Farmers));marker.transform.SetParent(Section("Supplier checkpoints"),false);
        marker.transform.localPosition=position+Vector3.up*.055f;
        var checkpoint=marker.AddComponent<DeliveryCheckpoint>();checkpoint.supplier=SupplierKind.Farmers;checkpoint.radius=4;
        checkpoint.kind=CheckpointKind.Depot;checkpoint.displayName=SupplierRouting.Name(SupplierKind.Farmers);suppliers.Add(checkpoint);
        Surface("Farm delivery turning yard",new Rect(516,273,60,42),TownColor.Asphalt,.045f);
        Model("PickupStopZone",position+Vector3.up*.055f,0,"Loading bays");
        Model("PickupSign",position+Vector3.right*7,180,"Supplier signs");
        Model("LoadingCanopy",position,0,"Loading bays");
        Model("WarehouseRack",position+new Vector3(8,0,3),180,"Loading bays");
        Model("Pallet",position+new Vector3(8,0,-3),0,"Loading bays");
    }
    private void ApprovedNeighbourhoods()
    {
        // City streets frame the editable shop block. Buildings never occupy purchasable land.
        foreach(float z in new[]{-166f,-73f,-46f,-19f,8f,31f,76f})
            foreach(float x in new[]{-194f,-143f})House(new Vector3(x,0,z),Mathf.Abs(Mathf.RoundToInt(z))%3,x<-170?90:270);
        foreach(float z in new[]{-73f,-46f,-19f,8f,31f})House(new Vector3(-169,0,z),Mathf.Abs(Mathf.RoundToInt(z))%3,180);
        foreach(float z in new[]{-166f,-100f,-70f,-40f,-10f,20f,72f,126f})
            foreach(float x in new[]{190f,241f})
            {
                if(z==126)continue;
                House(new Vector3(x,0,z),Mathf.Abs(Mathf.RoundToInt(z/10))%3,x<220?90:270);
            }
        foreach(float block in new[]{-72f,24f,120f})
        {
            ApprovedModel("01_Apartment",new Vector3(block,-.24f,113),180,"Residential courtyards",new[]{Wall(0,5.04f,-1,10,9.6f,8)});
            foreach(float x in new[]{block-23,block+23})foreach(float z in new[]{76f,102f,128f,168f})
                if(block!=24||z!=168)House(new Vector3(x,0,z),Mathf.Abs(Mathf.RoundToInt(x+z))%3,180);
            Surface("Residential square",new Rect(block-13,88,26,14),TownColor.Pavement);
            Model("TreeRound",new Vector3(block-9,0,95),0,"Residential greenery");
            Model("Bench",new Vector3(block,0,96),180,"Town square furniture");
            Model("FlowerPlanter",new Vector3(block+8,0,96),0,"Town square furniture");
            ParkingRow(new Vector3(block-10,0,127),5,true,"Residential staff");
        }
        ApprovedModel("02_RetailRow",new Vector3(-70,-.24f,-166),0,"Town centre",new[]{Wall(-6,2.39f,-1.2f,5.9f,4.3f,6.8f),Wall(0,2.54f,-1.2f,5.9f,4.6f,6.8f),Wall(6,2.39f,-1.2f,5.9f,4.3f,6.8f)});
        ApprovedModel("03_Cafe",new Vector3(23,-.24f,-166),0,"Town centre",new[]{Wall(.8f,2.29f,-1.8f,8.8f,4.1f,7)});
        ApprovedModel("05_BusStop",new Vector3(111,-.24f,-151),180,"Bus stops",new[]{Wall(-2.5f,1.62f,0,.15f,2.75f,.15f),Wall(2.5f,1.62f,0,.15f,2.75f,.15f)});
        ApprovedModel("04_FuelStation",new Vector3(216,-.24f,-100),180,"Roadside gas station",new[]{Wall(6,2.1f,-5,7,3.6f,6),Wall(-6,2.75f,0,.24f,4.95f,.24f),Wall(0,2.75f,0,.24f,4.95f,.24f),Wall(-6,1.04f,-1.2f,.8f,1.6f,.65f),Wall(0,1.04f,-1.2f,.8f,1.6f,.65f)});
        Surface("Gas station access",new Rect(195,-124,44,15),TownColor.Asphalt);
        Driveway(new Vector3(239,0,-117),new Vector3(264,0,-117));
        ApprovedModel("07_ConstructionSite",new Vector3(-245,-.24f,-93),0,"Construction plot",new[]{Wall(-1,4,-1,10,8,10),Wall(-10.4f,1.2f,0,.25f,2,20),Wall(10.4f,1.2f,0,.25f,2,20)});
        Driveway(new Vector3(-245,0,-109),new Vector3(-216,0,-109));
        ApprovedModel("01_Apartment",new Vector3(-170,-.24f,-166),0,"Residential courtyards",new[]{Wall(0,5.04f,-1,10,9.6f,8)});
        Building("Pharmacy",new Vector3(24,0,76),180,new Vector3(8,4.5f,6),"Town centre");
        House(new Vector3(120,0,76),0,180);
        foreach(float x in new[]{-44f,0f,46f,94f,142f})House(new Vector3(x,0,-174),Mathf.Abs(Mathf.RoundToInt(x))%3,0);
        ApprovedPark();
    }
    private void ApprovedPark()
    {
        Surface("Neighbourhood public park",new Rect(-15,155,77,28),TownColor.LeafLight);
        var paths=new TownMeshBuilder();
        for(int i=0;i<48;i++)
        {
            float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;
            Vector3 P(float angle,float width)=>new Vector3(24+Mathf.Cos(angle)*(30+width),.04f,171+Mathf.Sin(angle)*(9+width));
            paths.Quad(P(a,-1),P(b,-1),P(b,1),P(a,1),TownColor.Sand);
        }
        paths.Cylinder(new Vector3(24,.35f,171),3.6f,3.6f,.7f,TownColor.Pavement,24);
        paths.Cylinder(new Vector3(24,.72f,171),3.3f,3.3f,.05f,TownColor.Glass,24);
        paths.Cylinder(new Vector3(24,1.1f,171),.4f,.2f,.8f,TownColor.Stone,12);
        Geometry("Park promenade and fountain",paths,"Public park");
        for(int i=0;i<8;i++)
        {
            float angle=i*Mathf.PI/4;var p=new Vector3(24+Mathf.Cos(angle)*25,0,171+Mathf.Sin(angle)*9);
            if(Mathf.Abs(p.x-24)<1&&p.z<167)p.x+=10; // Keep the central gate-to-fountain approach open.
            Model("TreeRound",p,i*40,"Public park");
            Model("Bench",p+new Vector3(0,0,-2),i*45,"Public park furniture");
        }
    }
}
