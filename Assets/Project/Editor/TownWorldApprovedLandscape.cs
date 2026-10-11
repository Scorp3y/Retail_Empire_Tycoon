using System;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEngine;

internal sealed partial class TownWorldAssembler
{
    private void ApprovedFarm()
    {
        ApprovedModel("10_Farmstead",new Vector3(535,-.24f,341),180,"Farm buildings",
            new[]{Wall(-7,2.15f,-3,7,3.7f,6.4f),Wall(4.8f,2.9f,-3,8,5.2f,9)});
        ApprovedModel("11_Greenhouse",new Vector3(590,-.24f,336),180,"Farm buildings",
            new[]{Wall(-3,2.4f,-1,5.8f,4.4f,9),Wall(3,2.4f,-1,5.8f,4.4f,9)});
        ApprovedModel("12_SilosAndGarage",new Vector3(618,-.14f,332),180,"Farm buildings",
            new[]{Wall(-8,6,-3,4,12,4),Wall(-3,6,-3,4,12,4),Wall(1.5f,2.7f,-2.5f,.3f,5,8),Wall(10.5f,2.7f,-2.5f,.3f,5,8),Wall(6,2.7f,-6.4f,9,5,.3f),Wall(5,1.5f,4.7f,3,3,6)});
        Driveway(new Vector3(550,0,336),new Vector3(578,0,336));
        Surface("Farm residential footpath",new Rect(519,324,4,21),TownColor.Sand,.04f);
        Field(new Rect(471,366,61,81),false);Field(new Rect(544,366,58,81),true);
        Field(new Rect(614,355,35,92),false);
        var random=new System.Random(712);
        Surface("Farm orchard soil",new Rect(466,299,40,52),TownColor.LeafLight);
        for(int x=0;x<4;x++)for(int z=0;z<5;z++)
            Model("TreeRound",new Vector3(471+x*9,0,304+z*9),random.Next(360),"Farm orchard",Vector3.one*.8f);
        // The pasture ends before the visitor layby: no fence through parking spaces or the market approach.
        Surface("Animal meadow",new Rect(468,251,34,37),TownColor.LeafLight);
        RuralFence(new Rect(468,251,34,37),"Pasture fence");
        var hay=new TownMeshBuilder();
        foreach(var p in new[]{new Vector3(479,0,262),new Vector3(491,0,271),new Vector3(501,0,260)})
            hay.Cylinder(p+Vector3.up*.75f,.75f,.75f,1.3f,TownColor.Sand,12,Quaternion.Euler(0,0,90));
        Geometry("Pasture hay bales",hay,"Animal meadow");
        for(int i=0;i<4;i++)Model("Pallet",new Vector3(520+i*2,0,317),0,"Farm crates");
    }
    private void Field(Rect bounds,bool vegetables)
    {
        Surface(vegetables?"Vegetable field":"Wheat field",bounds,vegetables?TownColor.Soil:TownColor.Sand,.016f);
        RuralFence(bounds,vegetables?"Vegetable field boundary":"Wheat field boundary");
        var mesh=new TownMeshBuilder();
        for(float x=bounds.xMin+2;x<bounds.xMax-2;x+=2)
        {
            mesh.Box(new Vector3(x,.07f,bounds.center.y),new Vector3(1,.1f,bounds.height-5),TownColor.Soil);
            for(float z=bounds.yMin+3;z<bounds.yMax-2;z+=vegetables?2.4f:1.3f)
            {
                if(vegetables)mesh.Ellipsoid(new Vector3(x,.35f,z),new Vector3(.48f,.38f,.55f),TownColor.Leaf,6,3);
                else
                {
                    mesh.Box(new Vector3(x,.48f,z),new Vector3(.065f,.9f,.065f),TownColor.Yellow);
                    mesh.Ellipsoid(new Vector3(x,.92f,z),new Vector3(.16f,.24f,.13f),TownColor.Sand,5,2);
                }
            }
        }
        Geometry(vegetables?"Crop rows — vegetables":"Crop rows — wheat",mesh,"Farm fields");
    }
    private void RuralFence(Rect bounds,string name)
    {
        var mesh=new TownMeshBuilder();
        foreach(float z in new[]{bounds.yMin,bounds.yMax})
            mesh.Box(new Vector3(bounds.center.x,.9f,z),new Vector3(bounds.width,.1f,.1f),TownColor.WoodLight);
        foreach(float x in new[]{bounds.xMin,bounds.xMax})
            mesh.Box(new Vector3(x,.9f,bounds.center.y),new Vector3(.1f,.1f,bounds.height),TownColor.WoodLight);
        for(float x=bounds.xMin;x<=bounds.xMax;x+=4)foreach(float z in new[]{bounds.yMin,bounds.yMax})
            mesh.Box(new Vector3(x,.7f,z),new Vector3(.16f,1.4f,.16f),TownColor.Wood);
        for(float z=bounds.yMin;z<=bounds.yMax;z+=4)foreach(float x in new[]{bounds.xMin,bounds.xMax})
            mesh.Box(new Vector3(x,.7f,z),new Vector3(.16f,1.4f,.16f),TownColor.Wood);
        Geometry(name,mesh,"Farm boundaries");
    }
    private void ApprovedLandscape()
    {
        var random=new System.Random(3027);var bounds=layout.cityBounds;
        // Organic woodland bands separate town and farm; exclude every road, rail, yard and store plot.
        for(float x=bounds.xMin+10;x<bounds.xMax-9;x+=11)
            for(float z=bounds.yMin+10;z<bounds.yMax-9;z+=12)
            {
                var p=new Vector3(x+(float)random.NextDouble()*7-3.5f,0,z+(float)random.NextDouble()*7-3.5f);
                bool woodland=x>277&&x<450||z>247&&x<454||x>450&&z<240||x<-267||z<-201||z>468||x>655;
                if(!woodland||!ClearVerge(p,originalLandscapePavingCount))continue;
                if(Mathf.PerlinNoise(x*.028f+4,z*.025f+9)<.34f)continue;
                string tree=random.Next(3)==0?"TreeWide":"Pine";
                float yaw=random.Next(360),scale=1.1f+(float)random.NextDouble()*1.4f;
                bool bush=random.Next(4)==0;
                // Consume the historical sequence even when new local paths exclude a tree.
                // Otherwise one omitted tree would shuffle every later woodland cell, including farm surroundings.
                if(!ClearVerge(p))continue;
                Model(tree,p,yaw,"Forest and river corridor",Vector3.one*scale);
                if(bush)Model("BushRound",p+new Vector3(2,0,-2),0,"Forest understory",Vector3.one*1.2f);
            }
        foreach(var p in new[]{new Vector3(408,0,-154),new Vector3(477,0,170),new Vector3(-261,0,296)})
            if(ClearVerge(p))Model("GrassyHill",p,0,"Woodland hills",new Vector3(3,2,3));
        var sign=Model("PickupSign",new Vector3(271,0,-27),90,"Farm road direction");
        sign.name="Farm road entrance sign";
    }
}
