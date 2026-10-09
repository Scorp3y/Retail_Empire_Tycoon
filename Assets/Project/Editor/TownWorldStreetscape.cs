using System;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEngine;

/// <summary>Authored detail pass: each district has its own surfaces, frontage and coherent working yards.</summary>
internal sealed partial class TownWorldAssembler
{
    private void Streetscape()
    {
        StoreFrontage();IndustrialCourts();MarketGardens();CivicSquares();LandscapeMargins();
    }
    private void ParkLoop()
    {
        var mesh=new TownMeshBuilder();
        for(int i=0;i<48;i++)
        {
            float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;
            Vector3 P(float angle,float offset)=>new Vector3(24+Mathf.Cos(angle)*(27+offset),.027f,180+Mathf.Sin(angle)*(16+offset));
            mesh.Quad(P(a,-1.5f),P(b,-1.5f),P(b,1.5f),P(a,1.5f),TownColor.Sand);
        }
        Geometry("Curved park promenade",mesh,"Park paths");
    }
    private void PaintedLine(TownMeshBuilder mesh,Vector3 a,Vector3 b,float width,TownColor color)
    {
        var delta=b-a;mesh.Box((a+b)*.5f,new Vector3(width,.018f,delta.magnitude),color,Quaternion.LookRotation(delta));
    }
    private void Border(Rect bounds,TownColor color,string name)
    {
        var mesh=new TownMeshBuilder();float y=.06f;
        mesh.Box(new Vector3(bounds.center.x,y,bounds.yMin),new Vector3(bounds.width,.12f,.25f),color);
        mesh.Box(new Vector3(bounds.center.x,y,bounds.yMax),new Vector3(bounds.width,.12f,.25f),color);
        mesh.Box(new Vector3(bounds.xMin,y,bounds.center.y),new Vector3(.25f,.12f,bounds.height),color);
        mesh.Box(new Vector3(bounds.xMax,y,bounds.center.y),new Vector3(.25f,.12f,bounds.height),color);
        Geometry(name,mesh,"Paving borders");
    }
    private void ParkingRow(Vector3 start,int count,bool occupied,string district)
    {
        var mesh=new TownMeshBuilder();
        for(int i=0;i<=count;i++)
            PaintedLine(mesh,start+new Vector3(i*3.4f,.09f,-3),start+new Vector3(i*3.4f,.09f,3),.095f,TownColor.White);
        PaintedLine(mesh,start+new Vector3(0,.09f,3),start+new Vector3(count*3.4f,.09f,3),.095f,TownColor.White);
        for(int i=0;i<count;i++)
        {
            mesh.Box(start+new Vector3((i+.5f)*3.4f,.14f,2.5f),new Vector3(1.7f,.18f,.2f),TownColor.Pavement);
            if(occupied&&i%3!=1)Model(i%2==0?"VisitorSedan":"VisitorHatchback",start+new Vector3((i+.5f)*3.4f,0,.4f),180,"Parked cars");
        }
        Geometry(district+" parking markings",mesh,"Car park markings");
    }
    private void StoreFrontage()
    {
        var paving=new Rect(-62,-124,49,15);
        Surface("Store visitor parking",new Rect(-62,-124,49,8),TownColor.Asphalt,.038f);
        Surface("Entrance promenade",new Rect(-62,-116,49,7),TownColor.Pavement,.045f);
        Border(paving,TownColor.Pavement,"Store kerb");
        ParkingRow(new Vector3(-59,0,-120),11,false,"Player storefront");
        var mesh=new TownMeshBuilder();
        for(float x=-61;x<-12;x+=2.8f)PaintedLine(mesh,new Vector3(x,.073f,-114.3f),new Vector3(x,.073f,-109.5f),.024f,TownColor.Stone);
        PaintedLine(mesh,new Vector3(-62,.073f,-111.8f),new Vector3(-13,.073f,-111.8f),.024f,TownColor.Stone);
        // Safe visual route to the shop. No collision or decorative object is added inside the editable parcel.
        for(float z=-132;z<-123;z+=.75f)mesh.Ground(-38,z,4,.36f,.082f,TownColor.White);
        Geometry("Store entrance paving and crossing",mesh,"Entrance promenade");
        foreach(float x in new[]{-60f,-16f})
        {
            Model("FlowerPlanter",new Vector3(x,0,-112),0,"Player frontage landscaping",new Vector3(1.6f,1,1));
            Model("StreetLamp",new Vector3(x,0,-113.5f),0,"Player frontage lighting");
        }
        Model("Bench",new Vector3(-54,0,-111),180,"Player frontage furniture");
        Model("LitterBin",new Vector3(-50,0,-111),0,"Player frontage furniture");
        foreach(float x in new[]{-100f,-92f})
        {
            Model("TreeWide",new Vector3(x,0,-117),15,"Store service verge",Vector3.one*1.35f);
            Model("BushWide",new Vector3(x-2,0,-116),0,"Store service verge");
        }
        var loading=new TownMeshBuilder();
        for(float z=-88;z<-75;z+=1.2f)PaintedLine(loading,new Vector3(-84,.08f,z),new Vector3(-77,.08f,z+2),.09f,TownColor.Yellow);
        Geometry("Service loading keep-clear markings",loading,"Loading bays");
        // A readable finished edge, not a broad asphalt cross spanning the entire expansion site.
        Surface("Service verge gravel",new Rect(-96,-124,7,164),TownColor.Sand,.016f);
        for(int i=0;i<9;i++)
        {
            float z=-99+i*15;
            Model(i%2==0?"TreeWide":"TreeTall",new Vector3(-92,0,z),i*37,"Store service verge",Vector3.one*(.85f+i%3*.16f));
            Model("BushWide",new Vector3(-92.8f,0,z+4),0,"Store service verge");
        }
    }
    private void ArchitecturalDetail(GameObject building,string id)
    {
        if(id=="FarmersMarket")return;
        var bounds=building.GetComponent<MeshFilter>().sharedMesh.bounds;
        var detail=new TownMeshBuilder();float w=bounds.size.x,d=bounds.size.z;
        bool house=id.StartsWith("House",StringComparison.Ordinal)||id=="Townhouse"||id=="Garage";
        float eave=house?bounds.max.y-1.75f:bounds.max.y-.65f;
        foreach(float x in new[]{-w*.46f,w*.46f})
        {
            detail.Box(new Vector3(x,eave,0),new Vector3(.1f,.1f,d*.93f),TownColor.Frame);
            detail.Cylinder(new Vector3(x,eave/2,-d*.4f),.045f,.045f,eave,TownColor.Metal,6);
        }
        if(house)
        {
            detail.Box(new Vector3(0,.085f,d*.5f+.8f),new Vector3(1.7f,.17f,1.1f),TownColor.Pavement);
            for(int i=0;i<3;i++)detail.Box(new Vector3(0,.1f,d*.5f+1.8f+i*.75f),new Vector3(1.25f,.14f,.6f),TownColor.Sand);
        }
        else
        {
            detail.Box(new Vector3(w*.26f,eave+.4f,-d*.18f),new Vector3(1.3f,.55f,1.1f),TownColor.Metal);
            for(int i=0;i<6;i++)detail.Box(new Vector3(w*.26f,eave+.68f,-d*.18f+(i-2.5f)*.15f),new Vector3(1.1f,.025f,.025f),TownColor.Frame);
        }
        var instance=Geometry("Gutters, thresholds and roof services",detail,"Architectural detail");instance.transform.SetParent(building.transform,false);
    }
    private void IndustrialCourts()
    {
        Surface("Connected industrial service courtyard",new Rect(-208,-134,79,172),TownColor.Asphalt,.019f);
        Building("WholesaleBase",new Vector3(-192,0,-67),0,new Vector3(20,5.6f,13),"Industrial yards");
        Building("EquipmentSupplier",new Vector3(-150,0,-67),90,new Vector3(14,4.5f,9),"Industrial yards");
        ParkingRow(new Vector3(-155,0,-87),6,true,"Industrial visitor");
        Border(new Rect(-157,-92,27,11),TownColor.Pavement,"Visitor parking kerb");
        Model("CargoContainer",new Vector3(-196,0,-84),0,"Industrial yards");
        Model("WarehouseRack",new Vector3(-177,0,-83),0,"Organized freight storage");
        var markings=new TownMeshBuilder();
        for(float z=-118;z<33;z+=5)PaintedLine(markings,new Vector3(-138,.067f,z),new Vector3(-138,.067f,z+2.7f),.09f,TownColor.Yellow);
        for(int i=0;i<3;i++)
        {
            float x=-168+(i-1)*7.54f;
            PaintedLine(markings,new Vector3(x-2.2f,.069f,-36),new Vector3(x-2.2f,.069f,-28.8f),.09f,TownColor.White);
            PaintedLine(markings,new Vector3(x+2.2f,.069f,-36),new Vector3(x+2.2f,.069f,-28.8f),.09f,TownColor.White);
        }
        Geometry("Truck manoeuvring lane and dock markings",markings,"Industrial surface detail");
        Model("BoxTruck",new Vector3(-148,0,-15),270,"Industrial yards");
        Model("WarehouseRack",new Vector3(-133,0,7),270,"Organized freight storage");
        for(int i=0;i<4;i++)
        {
            Model("Pallet",new Vector3(-133,0,-26+i*3),0,"Organized freight storage");
            Model("BoxLarge",new Vector3(-133,.24f,-26+i*3),0,"Organized freight storage");
        }
        foreach(float z in new[]{-128f,-36f,88f,176f})
        {
            Surface("Industrial concrete working pad",new Rect(-205,z,58,22),TownColor.Pavement,.034f);
            Border(new Rect(-205,z,58,22),TownColor.Stone,"Industrial concrete edge");
            var mesh=new TownMeshBuilder();
            for(int i=0;i<8;i++)
                PaintedLine(mesh,new Vector3(-205+i*7.25f,.054f,z),new Vector3(-205+i*7.25f,.054f,z+22),.025f,TownColor.Stone);
            for(int i=0;i<3;i++)PaintedLine(mesh,new Vector3(-205,.054f,z+i*7.33f),new Vector3(-147,.054f,z+i*7.33f),.025f,TownColor.Stone);
            Geometry("Concrete expansion joints",mesh,"Industrial surface detail");
            for(int i=0;i<4;i++)
            {
                Model("Pallet",new Vector3(-201+i*2,0,z+18),0,"Organized freight storage");
                Model("BoxLarge",new Vector3(-201+i*2,.24f,z+18),i*7,"Organized freight storage");
                if(i%2==0)Model("BoxMedium",new Vector3(-201+i*2,1.1f,z+18),12,"Organized freight storage");
            }
        }
        foreach(float z in new[]{-105f,-12f,112f,198f})
        {
            Model("IndustrialFence",new Vector3(-207,0,z),90,"Industrial boundaries",new Vector3(4,1,1));
            Model("Hedge",new Vector3(-132,0,z),90,"Industrial verge",new Vector3(7,1,1));
        }
        ParkingRow(new Vector3(-185,0,27),7,true,"Industrial staff");
        Surface("Industrial staff parking",new Rect(-187,22,28,11),TownColor.Asphalt,.035f);
        Model("BoxTruck",new Vector3(-143,0,16),180,"Industrial yards");
        Model("CargoContainer",new Vector3(-195,0,137),90,"Industrial yards");
    }
    private void CivicSquares()
    {
        foreach(float x in new[]{-70f,24f,120f})
        {
            Surface("Town centre stone pedestrian spine",new Rect(x-7,65,14,68),TownColor.Pavement,.035f);
            var tile=new TownMeshBuilder();
            for(float z=66;z<133;z+=2)PaintedLine(tile,new Vector3(x-7,.059f,z),new Vector3(x+7,.059f,z),.025f,TownColor.Stone);
            for(float dx=-6;dx<=6;dx+=2)PaintedLine(tile,new Vector3(x+dx,.059f,65),new Vector3(x+dx,.059f,133),.025f,TownColor.Stone);
            Geometry("Pedestrian paving joints",tile,"Town square detail");
            foreach(float dx in new[]{-12f,12f})foreach(float z in new[]{89f,102f})
            {
                Model("TreeWide",new Vector3(x+dx,0,z),dx*5,"Town square canopy",Vector3.one*1.35f);
                Model("FlowerPlanter",new Vector3(x+dx,0,z-3),0,"Town square detail");
            }
            Model("Bench",new Vector3(x-5,0,96),90,"Town square detail");
            Model("Bench",new Vector3(x+5,0,96),270,"Town square detail");
            Model("StreetLamp",new Vector3(x-6,0,85),0,"Town square lights");
            Model("StreetLamp",new Vector3(x+6,0,109),180,"Town square lights");
        }
        // Residential side streets now have continuous verges and paths rather than buildings floating in a field.
        Surface("Residential front promenade",new Rect(-208,-185,466,3),TownColor.Pavement,.025f);
        Surface("Shop street public footpath",new Rect(-114,-138,102,2),TownColor.Pavement,.03f);
        for(int i=0;i<24;i++)
        {
            float x=-200+i*19;
            if(!ClearVerge(new Vector3(x,0,-185)))continue;
            Model("BushWide",new Vector3(x,0,-181),0,"Residential flower borders");
        }
    }
    private void MarketGardens()
    {
        Surface("Farm central track",new Rect(207,-44,5,90),TownColor.Sand,.022f);
        Surface("Farm east track",new Rect(248,-44,4,90),TownColor.Sand,.022f);
        Surface("Farm crossing track",new Rect(181,-7,74,4),TownColor.Sand,.023f);
        var plants=new TownMeshBuilder();
        for(int field=0;field<4;field++)
        {
            float x=194+field%2*33,z=-26+field/2*46;
            for(int row=0;row<5;row++)
            {
                plants.Box(new Vector3(x-8+row*4,.04f,z),new Vector3(.95f,.045f,30),TownColor.BoxDark);
                for(int i=0;i<12;i++)
                {
                    var p=new Vector3(x-8+row*4,.42f,z-13+i*2.2f);
                    if(field==0||field==2)
                    {
                        plants.Cylinder(p+Vector3.up*.38f,.035f,.035f,1.1f,TownColor.Wood,5);
                        plants.Ellipsoid(p+Vector3.up*.4f,new Vector3(.36f,.7f,.3f),TownColor.Leaf,6,4);
                        for(int fruit=0;fruit<3;fruit++)plants.Ellipsoid(p+new Vector3((fruit-1)*.16f,.3f+fruit*.14f,-.28f),Vector3.one*.11f,TownColor.Red,6,4);
                    }
                    else if(field==3)plants.Ellipsoid(p+Vector3.up*.02f,new Vector3(.33f,.23f,.3f),TownColor.Orange,8,5);
                }
            }
        }
        Geometry("Raised beds, tomato supports and pumpkin crop",plants,"Farm planting detail");
        Greenhouse(new Vector3(240,0,160));
        Building("Garage",new Vector3(199,0,165),180,new Vector3(5.4f,4.4f,6.5f),"Farm buildings");
        for(int i=0;i<10;i++)
        {
            var p=new Vector3(188+(i%5)*13,0,183+i/5*16);
            Model("TreeRound",p,i*29,"Fruit orchard",Vector3.one*(.85f+i%3*.1f));
            var fruit=new TownMeshBuilder();
            for(int a=0;a<8;a++)fruit.Ellipsoid(p+new Vector3(Mathf.Cos(a*2.4f)*1.2f,3.1f+a%3*.35f,Mathf.Sin(a*2.4f)*1.2f),Vector3.one*.13f,TownColor.Red,6,4);
            Geometry("Orchard fruit",fruit,"Fruit orchard");
        }
    }
    private void Greenhouse(Vector3 position)
    {
        var mesh=new TownMeshBuilder();
        mesh.Box(position+Vector3.up*1.25f,new Vector3(8,2.5f,12),TownColor.Glass);
        mesh.Gable(position+Vector3.up*2.5f,8,12,1.5f,TownColor.Glass,TownColor.Glass);
        for(int i=0;i<=6;i++)
        {
            float z=position.z-6+i*2;
            foreach(float side in new[]{-1f,1f})
            {
                mesh.Box(position+new Vector3(side*4,1.3f,z-position.z),new Vector3(.1f,2.6f,.12f),TownColor.White);
                PaintedLine(mesh,new Vector3(position.x+side*4,2.52f,z),new Vector3(position.x,4,z),.1f,TownColor.White);
            }
        }
        var greenhouse=Geometry("Farm glasshouse",mesh,"Farm buildings");BoxCollider(greenhouse,position+Vector3.up*1.8f,new Vector3(8,3.6f,12));
    }
    private void LandscapeMargins()
    {
        var random=new System.Random(9013);var ground=new TownMeshBuilder();
        for(int i=0;i<180;i++)
        {
            var p=new Vector3(-275+(float)random.NextDouble()*550,0,-202+(float)random.NextDouble()*425);
            if(!ClearVerge(p))continue;
            if(layout.roads.Exists(r=>(r.Position(12)-p).sqrMagnitude<130))continue;
            float rx=3+(float)random.NextDouble()*7,rz=3+(float)random.NextDouble()*6;
            OrganicPatch(ground,p,rx,rz,i%3==0?TownColor.Sand:TownColor.LeafLight,i);
            if(i%3==0)Model("BushWide",p,i*71,"Natural undergrowth",Vector3.one*(.6f+(float)random.NextDouble()*.6f));
            if(i%7==0)Model("RockGroup",p+Vector3.right*2,0,"Natural undergrowth",Vector3.one*.7f);
        }
        Geometry("Natural ground variations",ground,"Landscape surfaces");
        var hills=new TownMeshBuilder();
        for(int i=0;i<24;i++)
        {
            float x=-335+(i%2)*17,z=-240+i*23;
            hills.Ellipsoid(new Vector3(x,-4,z),new Vector3(25+i%3*7,9+i%4*2,27),i%3==0?TownColor.LeafLight:TownColor.Grass,14,5);
            if(i%2==0)hills.Ellipsoid(new Vector3(-260+i*23,-5,286+i%3*9),new Vector3(29,12,24),TownColor.Grass,14,5);
        }
        Geometry("Rolling countryside backdrop",hills,"Landscape background");
    }
    private static void OrganicPatch(TownMeshBuilder mesh,Vector3 center,float rx,float rz,TownColor color,int seed)
    {
        center.y=.012f;
        for(int i=0;i<11;i++)
        {
            float a=i*Mathf.PI*2/11,b=(i+1)*Mathf.PI*2/11;
            float r=.8f+.18f*Mathf.Sin(i*3.1f+seed),s=.8f+.18f*Mathf.Sin((i+1)*3.1f+seed);
            mesh.Triangle(center,center+new Vector3(Mathf.Cos(b)*rx*s,0,Mathf.Sin(b)*rz*s),center+new Vector3(Mathf.Cos(a)*rx*r,0,Mathf.Sin(a)*rz*r),color);
        }
    }
}
