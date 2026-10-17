using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>An authored yard composition independent of the purchase grid; only its render ownership is parcel-based.</summary>
internal sealed class TownYardStoryPlacement
{
    internal const string Root=TownForgottenYardAuthoring.Root+"/DetailPass";
    private readonly Dictionary<string,Mesh> models=new();
    private readonly Dictionary<string,int> counts=new();
    private readonly List<Bounds> focalObjects=new();
    private readonly List<Bounds> solidFootprints=new();
    private readonly List<Vector2> treeTrunks=new();
    private readonly List<Vector2[]> routes=new();
    private TownWorldLayout layout;
    private Canvas[] canvases;
    private Material surface;
    private sealed class Canvas
    {
        internal TownExpansionPlot Plot;
        internal readonly TownMeshBuilder Mesh=new();
        internal readonly List<(string id,Vector3 position)> Markers=new();
    }
    internal void Build()
    {
        foreach(string dir in new[]{"Meshes","Materials","Prefabs"})Directory.CreateDirectory(Root+"/"+dir);
        AssetDatabase.Refresh();BuildModels();
        layout=AssetDatabase.LoadAssetAtPath<TownWorldLayout>(TownForgottenYardAuthoring.LayoutPath);
        canvases=layout.expansionPlots.OrderBy(p=>(int)p.territory).Select(p=>new Canvas{Plot=p}).ToArray();
        if(canvases.Length!=24)throw new InvalidOperationException("The established 24-parcel layout is required.");
        Paths();Stories();Planting();SaveScenery();
        File.WriteAllLines(TownYardStoryBatch.Report+"/placements.txt",counts.OrderBy(p=>p.Key).Select(p=>p.Key+": "+p.Value));
    }
    private void BuildModels()
    {
        var palette=new Texture2D(32,1,TextureFormat.RGBA32,false){name="Yard story faded colours",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        var colors=TownPalette.Colors.ToArray();
        colors[(int)TownColor.Mint]=new Color32(112,156,145,255);colors[(int)TownColor.Blue]=new Color32(88,132,150,255);
        colors[(int)TownColor.Red]=new Color32(173,91,64,255);colors[(int)TownColor.Orange]=new Color32(211,149,83,255);
        colors[(int)TownColor.Sand]=new Color32(170,155,120,255);colors[(int)TownColor.Flowers]=new Color32(170,128,167,255);
        colors[(int)TownColor.Metal]=new Color32(108,117,111,255);colors[(int)TownColor.Soil]=new Color32(110,103,78,255);
        palette.SetPixels32(colors);palette.Apply();Save(palette,"Materials/Palette.asset");
        var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(TownWorldAssembler.Folder+"/TownPalette.mat")){name="Yard story surfaces",enableInstancing=true};
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/Palette.asset"));material.SetFloat("_PaletteIndex",-1);Save(material,"Materials/Surface.mat");
        surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Surface.mat");
        foreach(var id in Directory.GetFiles(TownForgottenYardAuthoring.Root+"/Prefabs","*.prefab").Select(Path.GetFileNameWithoutExtension).Where(id=>id!="ModularLandscape"))
            models.Add(id,AssetDatabase.LoadAssetAtPath<GameObject>(TownForgottenYardAuthoring.Root+"/Prefabs/"+id+".prefab").GetComponent<MeshFilter>().sharedMesh);
        foreach(var recipe in TownYardStoryModels.Create())
        {
            var mesh=recipe.Value.Build(recipe.Key);mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();Save(mesh,"Meshes/"+recipe.Key+".asset");
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+recipe.Key+".asset");models.Add(recipe.Key,saved);
            var go=new GameObject(recipe.Key,typeof(MeshFilter),typeof(MeshRenderer));go.GetComponent<MeshFilter>().sharedMesh=saved;go.GetComponent<MeshRenderer>().sharedMaterial=surface;
            PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+recipe.Key+".prefab");Object.DestroyImmediate(go);
        }
    }
    private Vector2 Center(int plot,float x=0,float z=0)=>canvases[plot].Plot.bounds.center+new Vector2(x,z);
    private void Prop(int plot,string id,float x=0,float z=0,float yaw=0,float scale=1,float elevation=0,float roll=0,bool marker=true)
    {
        var canvas=canvases[plot];var mesh=models[id];
        var pose=FittedPose(plot,id,x,z,yaw,scale,elevation,roll,out var position,out var worldBounds);
        canvas.Mesh.Append(mesh,pose);
        if(marker)canvas.Markers.Add((id,position));
        counts[id]=counts.TryGetValue(id,out int count)?count+1:1;
        if(IsTree(id))treeTrunks.Add(new Vector2(position.x,position.z));
        if(!IsTree(id)&&id!="Shrub"&&id!="TallWeeds"&&id!="WildflowerClump"&&id!="LeafPile"&&id!="Reeds")solidFootprints.Add(worldBounds);
        if(id=="PlayFort"||id=="ClimbingDome"||id=="CrookedPergola"||id=="ToolShed"||id=="BrokenGreenhouse"||id=="RepairCanopy"||id=="DryFountain"||id=="WaterTank"||id=="OvergrownSandbox"||id=="RaisedBed")
        {
            worldBounds.Expand(new Vector3(.8f,0,.8f));focalObjects.Add(worldBounds);
        }
    }
    private static bool IsTree(string id)=>id=="ElderTree"||id=="Birch"||id=="LeaningPine"||id=="OldAppleTree"||id=="OldMaple";
    private Matrix4x4 FittedPose(int plot,string id,float x,float z,float yaw,float scale,float elevation,float roll,out Vector3 position,out Bounds worldBounds)
    {
        var canvas=canvases[plot];var rect=canvas.Plot.bounds;var mesh=models[id];var rotation=Quaternion.Euler(0,yaw,roll);
        var vertices=mesh.vertices.Select(v=>rotation*(v*scale)).ToArray();
        float minX=vertices.Min(v=>v.x),maxX=vertices.Max(v=>v.x),minZ=vertices.Min(v=>v.z),maxZ=vertices.Max(v=>v.z);
        if(maxX-minX>rect.width-.4f||maxZ-minZ>rect.height-.4f)throw new InvalidOperationException(id+" cannot fit inside "+canvas.Plot.territory);
        var p=Center(plot,x,z);
        p.x=Mathf.Clamp(p.x,rect.xMin+.2f-minX,rect.xMax-.2f-maxX);p.y=Mathf.Clamp(p.y,rect.yMin+.2f-minZ,rect.yMax-.2f-maxZ);
        float minY=vertices.Min(v=>v.y),maxY=vertices.Max(v=>v.y);
        position=new Vector3(p.x,.075f-minY+elevation,p.y);
        worldBounds=new Bounds(position+new Vector3((minX+maxX)*.5f,(minY+maxY)*.5f,(minZ+maxZ)*.5f),new Vector3(maxX-minX,maxY-minY,maxZ-minZ));
        return Matrix4x4.TRS(position,rotation,Vector3.one*scale);
    }
    private void Paths()
    {
        Route(new[]{new Vector2(-45,-84),new Vector2(-49,-75),new Vector2(-40,-64),new Vector2(-30,-60),new Vector2(-29,-48),new Vector2(-17,-44),new Vector2(-15,-22)},1.8f);
        Route(new[]{new Vector2(-31,-100),new Vector2(-20,-91),new Vector2(-13,-80),new Vector2(1,-75),new Vector2(18,-70),new Vector2(34,-63),new Vector2(58,-57),new Vector2(76,-27)},1.65f);
        Route(new[]{new Vector2(-1,-103),new Vector2(14,-99),new Vector2(33,-94),new Vector2(54,-86),new Vector2(64,-74),new Vector2(61,-60),new Vector2(44,-41),new Vector2(27,-38),new Vector2(13,-36),new Vector2(-1,-34),new Vector2(-7,-28),new Vector2(-48,-28)},1.4f);
        Route(new[]{new Vector2(-40,-64),new Vector2(-29,-67),new Vector2(-26,-72),new Vector2(-24,-77),new Vector2(-16,-80),new Vector2(-4,-77)},1.25f);
    }
    private void Route(Vector2[] knots,float width)
    {
        var samples=new List<Vector2>();
        for(int i=0;i<knots.Length-1;i++)
        {
            var a=knots[Mathf.Max(0,i-1)];var b=knots[i];var c=knots[i+1];var d=knots[Mathf.Min(knots.Length-1,i+2)];
            int steps=Mathf.CeilToInt(Vector2.Distance(b,c));
            for(int j=0;j<steps;j++)
            {float t=j/(float)steps; samples.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));}
        }
        samples.Add(knots.Last());routes.Add(samples.ToArray());
        var left=new Vector2[samples.Count];var right=new Vector2[samples.Count];
        for(int i=0;i<samples.Count;i++)
        {
            // Adjacent segments share the very same edge vertices. Independent segment normals
            // leave triangular gaps along bends, especially visible from the game's high camera.
            var tangent=(samples[Mathf.Min(i+1,samples.Count-1)]-samples[Mathf.Max(0,i-1)]).normalized;
            var side=new Vector2(-tangent.y,tangent.x);float half=width*(.49f+.02f*Mathf.Sin(i*.37f));
            left[i]=samples[i]-side*half;right[i]=samples[i]+side*half;
        }
        for(int i=0;i<samples.Count-1;i++)
        {
            var a=samples[i];var b=samples[i+1];var direction=(b-a).normalized;var side=new Vector2(-direction.y,direction.x);
            Polygon(new[]{left[i],left[i+1],right[i+1],right[i]},.069f,TownColor.Sand);
            if(i%23==4)Patch(a+side*.5f,.55f,.12f,TownColor.Soil,.071f,i*31);
        }
    }
    private bool NearPath(Vector2 p,float clearance)
    {
        foreach(var route in routes)for(int i=0;i<route.Length-1;i++)
        {
            var a=route[i];var d=route[i+1]-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);
            if(Vector2.Distance(p,a+d*t)<clearance)return true;
        }
        return false;
    }
    private void Stories()
    {
        // Individual scenes have different silhouettes, ground wear and object relationships.
        Prop(0,"FallenBicycle",0,-5,28,1.2f);Prop(0,"DetachedWheel",1.3f,-4.6f);Prop(0,"CableSpool",-.5f,1.4f);
        Prop(0,"Toolbox",-.5f,1.4f,0,1,.92f);Prop(0,"BrokenBench",0,5,105,1.1f);Prop(0,"SplitCrate",1.6f,6,14);
        Prop(1,"RepairCanopy",0,-2,0,1.1f);Prop(1,"AbandonedEstateCar",0,-2,0,1.12f);Prop(1,"Workbench",0,4,0);
        Prop(1,"Toolbox",-.55f,4,0,1,1.2f);Prop(1,"OldTyre",2.9f,-5);Prop(1,"RustBarrel",2.9f,1,15);Prop(1,"FoldedTarp",-2.1f,4.5f,18);Prop(1,"LooseBoards",2,6.5f,35);
        Patch(Center(1,0,-2),3.6f,5.3f,TownColor.Soil,.064f,2);
        Prop(2,"BrickWallRemnant",0,3,0);Prop(2,"FallenGate",0,-.8f,-12);Prop(2,"OldNoticeboard",-1.6f,-6,190);Prop(2,"PaverStack",1.9f,2);Prop(2,"PaintAndBrushes",1.9f,2,0,1,.55f);

        Patch(Center(3,-1,0),12,7.5f,TownColor.Sand,.063f,3);
        Prop(3,"PlayFort",-5,-2,10,1.18f);Prop(3,"ClimbingDome",7,3,-8,1.15f);Prop(3,"SpringRider",1,-5,120,1.15f);
        Prop(3,"OneSeatSwing",-2,6,10,1.5f);Prop(3,"OvergrownSandbox",8,-3,-5,1.3f);
        Prop(3,"BrokenBench",-10,6,65,1.2f);Prop(3,"BallAndShoes",5,5);Prop(3,"RustBucket",-2,4);Prop(3,"LooseBoards",-9,-3,16);
        Hopscotch(Center(3,3,-6),-12);

        Patch(Center(4,-2,-1),5.7f,4.4f,TownColor.Soil,.063f,4);
        Prop(4,"CrookedPergola",-2,-1,12,1.2f);Prop(4,"PicnicTable",-2,-1,12,1.3f);
        Prop(4,"CoffeeLeftovers",-2,-1,12,1,1.34f);Prop(4,"OldChair",-4.5f,-1,-75,1.3f);Prop(4,"OldChair",.3f,-1,95,1.3f);Prop(4,"OldChair",-2,1.2f,-8,1.3f);
        Prop(4,"BrokenBench",8,5,195,1.2f);Prop(4,"SplitCrate",3,-2);Prop(4,"ClayPots",-6,3,28);Prop(4,"WateringCan",3.5f,-3.2f);

        Patch(Center(5,-4,1),6.5f,5.4f,TownColor.Stone,.064f,5);Prop(5,"DryFountain",-4,1,0,1.15f);
        Prop(5,"WeatheredBench",-10,4,110,1.3f);Prop(5,"BrokenBench",1,6,-150,1.2f);Prop(5,"OldChair",2,-3,33,1.2f);Prop(5,"BallAndShoes",-1,4);
        Crack(Center(5,-10,-3),Center(5,1,4),5);Prop(5,"LowRetainingBricks",7,-3,15,1.2f);

        Prop(6,"ToolShed",-6,-1,8,1.16f);Prop(6,"BrokenGreenhouse",6,1,-4,1.12f);
        Patch(Center(6,-5,1),5,5,TownColor.Soil,.063f,6);Prop(6,"Workbench",-6,5,8,1.2f);Prop(6,"PaintAndBrushes",-6.6f,5,0,1,1.43f);
        Prop(6,"Wheelbarrow",-1,2,-45,1.2f);Prop(6,"ClayPots",2,-4);Prop(6,"WateringCan",3,-4.5f);Prop(6,"RustBucket",-2,5);Prop(6,"RaisedBed",7,-5,0,1.1f);
        Prop(6,"LeaningPallet",-10,4,15,1);Prop(6,"LooseBoards",-10,-4,15);Prop(6,"SplitCrate",-3,-4);

        Court(7);Prop(7,"BasketballHoop",1,4,0,1.15f);Prop(7,"ChainlinkRemnant",-5,5.4f,0,1.1f);Prop(7,"ChainlinkRemnant",8,5.4f,0,1.1f);
        Prop(7,"BallAndShoes",5,-2);Prop(7,"BrokenBench",-13,-2,-85,1.25f);Prop(7,"FallenBicycle",13,1,-24,1.15f);Prop(7,"DetachedWheel",14,2.6f);

        Patch(Center(8,-5,0),8,4.7f,TownColor.Stone,.063f,8);Prop(8,"GardenTrailer",-8,0,90,1.15f);Prop(8,"PaverStack",-2,1,16,1.3f);
        Prop(8,"PaverStack",0,1,5);Prop(8,"FoldedTarp",-2,-2,0,1.2f);Prop(8,"RustBarrel",4,2);Prop(8,"Toolbox",-5,-2);Prop(8,"LowRetainingBricks",11,0,-6,1.5f);
        Crack(Center(8,-12,-2),Center(8,4,3),8);

        Patch(Center(9,-4,0),11,4.4f,TownColor.Pavement,.063f,9);Prop(9,"BrickWallRemnant",-6,2,0,1.3f);Prop(9,"BrickWallRemnant",3,2,-5,1.1f);
        Prop(9,"Workbench",-5,-1,0,1.3f);Prop(9,"Woodpile",7,0,5,1.4f);Prop(9,"FallenGate",1,-2,-4);Prop(9,"OldNoticeboard",-13,0,180);
        Prop(9,"PaverStack",-8,-2);Prop(9,"Toolbox",-5,-1,0,1,1.5f);Crack(Center(9,-9,-3),Center(9,7,1),9);

        Prop(10,"RaisedBed",-2,-3,-8,1.15f);Prop(10,"RaisedBed",1,3,12);Prop(10,"Wheelbarrow",2,-5,155);Prop(10,"ClayPots",-2,4);Prop(10,"WateringCan",-1,-5);
        Patch(Center(10,0,0),4.6f,6,TownColor.Soil,.062f,10);
        Prop(11,"WaterTank",-1,1,0,1.28f);Prop(11,"HandPump",2,-4,0,1.3f);Prop(11,"RustBarrel",2,4,10,1.1f);Prop(11,"RustBarrel",3.1f,3);Prop(11,"RustBucket",2,-2.6f);
        Patch(Center(11,2,-3),2,2.5f,TownColor.Soil,.063f,11);Prop(11,"Reeds",2,-2,0,1.1f);
        Prop(12,"LowRetainingBricks",0,-3,90,1.2f);Prop(12,"PaverStack",1,1,20,1.4f);Prop(12,"PaintAndBrushes",1,1,0,1,.7f);Prop(12,"FallenGate",-2,4,27);Prop(12,"OldChair",2,-5,120,1.2f);
        Prop(13,"Woodpile",-1,0,6,1.5f);Prop(13,"FoldedTarp",1,-3,30,1.3f);Prop(13,"CableSpool",0,5,0,1.3f);Prop(13,"SplitCrate",2,4);Prop(13,"LooseBoards",-2,-4,5);
        Prop(14,"OvergrownBathtub",0,2,22,1.25f);Prop(14,"ClayPots",2,-2,45,1.3f);Prop(14,"WeatheredBench",-1,-5,155,1.2f);Prop(14,"OldNoticeboard",-2,5,-15);Prop(14,"RustBucket",2,3);
        Prop(15,"FallenGate",0,-2,12,1.4f);Prop(15,"FenceRemnant",-2,2,15,1.6f);Prop(15,"OldNoticeboard",1,5,175,1.2f);Prop(15,"OldTyre",2,-4,0,1.25f);Prop(15,"LooseBoards",-2,-4);
        Prop(16,"RaisedBed",-9,0,-5,1.25f);Prop(16,"RaisedBed",-3,2,7,1.15f);Prop(16,"HandPump",4,0,30,1.1f);Prop(16,"Wheelbarrow",9,-1,70);Prop(16,"ClayPots",1,-3);Prop(16,"LowRetainingBricks",12,3,0,1.4f);
        Prop(17,"CableSpool",-11,-1,0,1.4f);Prop(17,"OldChair",-13,0,-70,1.25f);Prop(17,"CoffeeLeftovers",-11,-1,0,1,1.34f);Prop(17,"BrokenBench",6,1,185,1.4f);Prop(17,"LeafPile",7,-.5f,0,1.5f);
        Prop(18,"Woodpile",-8,1,-5,1.5f);Prop(18,"Woodpile",-4,1,8,1.2f);Prop(18,"Workbench",3,0,10,1.2f);Prop(18,"LooseBoards",6,1,50,1.5f);Prop(18,"RustBarrel",9,-2);Prop(18,"Stump",-1,-2,0,1.4f);
        Prop(19,"ElderTree",-6,2,15,1.4f);Prop(19,"WeatheredBench",-7,-2,-20,1.3f);Prop(19,"CableSpool",-2,-3,0,1.3f);Prop(19,"OldChair",-2,-5,15,1.2f);Prop(19,"CoffeeLeftovers",-2,-3,0,1,1.25f);Prop(19,"FallenBranch",4,-1,30,1.6f);
        Prop(20,"OldAppleTree",-5,1,14,1.4f);Prop(20,"PicnicTable",1,0,-12,1.25f);Prop(20,"OldChair",1,2,0,1.2f);Prop(20,"SplitCrate",4,1);Prop(20,"LeafPile",-5,-2,12,1.6f);
        Prop(21,"GardenTrailer",-4,0,50,1.2f);Prop(21,"PaverStack",3,1,0,1.5f);Prop(21,"LeaningPallet",5,-2,-18,1.4f);Prop(21,"PaintAndBrushes",3,1,0,1,.8f);Prop(21,"OldTyre",-8,2);Prop(21,"FoldedTarp",0,-4,20,1.3f);
        Prop(22,"BrickWallRemnant",-3,2,-8,1.3f);Prop(22,"FallenGate",3,1,20,1.3f);Prop(22,"BrokenBench",-6,-4,170,1.2f);Prop(22,"ClayPots",1,-3,0,1.4f);Prop(22,"FenceRemnant",7,2,3,1.5f);
        Prop(23,"DiscardedToilet",5,4,25,1.25f,0,8);Prop(23,"OvergrownBathtub",2,4,-12,1.2f);Prop(23,"RustBucket",5,2.5f);Prop(23,"SplitCrate",7,2,30,1.2f);
        Prop(23,"FenceRemnant",4,7,-8,1.6f);Prop(23,"OldTyre",6,5.5f,0,1.1f);Prop(23,"LooseBoards",1,6,26,1.4f);
    }
    private void Planting()
    {
        var random=new System.Random(60519);
        float Rand(float a,float b)=>a+(float)random.NextDouble()*(b-a);
        // Density is intentionally different by scene, not a repeated two-tree/seven-shrub template.
        int[] treeCounts={3,2,3,4,7,3,3,2,3,5,3,3,4,4,5,5,5,7,5,9,7,4,7,8};
        string[] trees={"ElderTree","Birch","LeaningPine","OldAppleTree","OldMaple"};
        for(int i=0;i<24;i++)
        {
            var r=canvases[i].Plot.bounds;var placed=new List<Vector2>();int accepted=0;
            for(int tries=0;tries<180&&accepted<treeCounts[i];tries++)
            {
                var p=new Vector2(Rand(r.xMin+3,r.xMax-3),Rand(r.yMin+3,r.yMax-3));
                string id=trees[(i+tries/3)%trees.Length];float scale=Rand(.9f,1.35f);
                float yaw=Rand(0,360);
                FittedPose(i,id,p.x-r.center.x,p.y-r.center.y,yaw,scale,0,0,out var actual,out var crown);
                p=new Vector2(actual.x,actual.z);
                var trunk=new Bounds(new Vector3(p.x,0,p.y),new Vector3(.9f,1,.9f));
                if(NearPath(p,2)||focalObjects.Any(b=>OverlapXZ(b,crown))||solidFootprints.Any(b=>OverlapXZ(b,trunk))||treeTrunks.Any(q=>Vector2.Distance(q,p)<3.8f))continue;
                Prop(i,id,p.x-r.center.x,p.y-r.center.y,yaw,scale);placed.Add(p);accepted++;
            }
            // Islands of shrubs/grass grow around trees, masonry, and the outside edge, leaving open clearings.
            for(int island=0;island<4+i%3;island++)
            {
                Vector2 anchor=island<placed.Count?placed[island]:new Vector2(Rand(r.xMin+1,r.xMax-1),Rand(r.yMin+1,r.yMax-1));
                int blades=10+(i+island)%12;
                for(int j=0;j<blades;j++)
                {
                    float a=Rand(0,Mathf.PI*2),distance=Rand(.3f,3.2f);var p=anchor+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*distance;
                    if(p.x<r.xMin+.8f||p.x>r.xMax-.8f||p.y<r.yMin+.8f||p.y>r.yMax-.8f||NearPath(p,.7f)||focalObjects.Any(b=>p.x>=b.min.x&&p.x<=b.max.x&&p.y>=b.min.z&&p.y<=b.max.z))continue;
                    string id=j%8==0?"Shrub":"TallWeeds";
                    Prop(i,id,p.x-r.center.x,p.y-r.center.y,Rand(0,360),Rand(.9f,1.7f),marker:false);
                    if(j%9==0)Prop(i,"WildflowerClump",p.x-r.center.x+.2f,p.y-r.center.y+.2f,Rand(0,360),Rand(.8f,1.2f),marker:false);
                }
                Patch(anchor,Rand(1.8f,3.2f),Rand(1.2f,2.6f),island%3==0?TownColor.Soil:TownColor.LeafLight,.061f,i*3+island);
            }
            foreach(var tree in placed.Take(3))
            {
                var p=tree+new Vector2(.7f,-.9f);Prop(i,"LeafPile",p.x-r.center.x,p.y-r.center.y,Rand(0,360),Rand(1,1.7f),marker:false);
            }
        }
    }
    private void Court(int plot)
    {
        var c=Center(plot,1,0);
        Polygon(new[]{c+new Vector2(-10,-4.2f),c+new Vector2(-9.4f,-5),c+new Vector2(9.7f,-5),c+new Vector2(10.3f,-4.3f),c+new Vector2(10.3f,3.8f),c+new Vector2(9.1f,4.8f),c+new Vector2(-9.5f,4.8f),c+new Vector2(-10.2f,4.1f)},.064f,TownColor.RoofDark);
        focalObjects.Add(new Bounds(new Vector3(c.x,0,c.y),new Vector3(21,10,11)));
        Vector2 P(float x,float z)=>c+new Vector2(x,z);
        foreach(float x in new[]{-8.4f,8.4f})Line(P(x,-4.4f),P(x,4.4f),.075f,TownColor.Cream,.068f);
        foreach(float z in new[]{-4.4f,4.4f}){Line(P(-8.4f,z),P(-1,z),.075f,TownColor.Cream,.068f);Line(P(1.2f,z),P(8.4f,z),.075f,TownColor.Cream,.068f);}
        for(int i=0;i<28;i++)if(i%6!=0)
        {float a=i*Mathf.PI/14,b=(i+1)*Mathf.PI/14;Line(P(Mathf.Cos(a)*3.1f,Mathf.Sin(a)*3.1f),P(Mathf.Cos(b)*3.1f,Mathf.Sin(b)*3.1f),.075f,TownColor.Mint,.069f);}
        Crack(P(-9,-3),P(7,3),3);Crack(P(2,-4),P(-1,4),4);
    }
    private void Hopscotch(Vector2 origin,float yaw)
    {
        var q=Quaternion.Euler(0,yaw,0);
        Vector2 P(float x,float z){var p=q*new Vector3(x,0,z);return origin+new Vector2(p.x,p.z);}
        for(int i=0;i<6;i++)
        {
            float x=i%3==1?-.4f:0,z=i*.63f;
            foreach(var edge in new[]{(P(x-.3f,z),P(x+.3f,z)),(P(x+.3f,z),P(x+.3f,z+.6f)),(P(x+.3f,z+.6f),P(x-.3f,z+.6f)),(P(x-.3f,z+.6f),P(x-.3f,z))})Line(edge.Item1,edge.Item2,.035f,i%2==0?TownColor.Cream:TownColor.Orange,.075f);
        }
    }
    private void Crack(Vector2 a,Vector2 b,int seed)
    {
        var random=new System.Random(seed);var previous=a;
        for(int i=1;i<=9;i++)
        {
            var next=Vector2.Lerp(a,b,i/9f)+new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*.7f;
            Line(previous,next,.06f,TownColor.Soil,.073f);
            if(i%3==0)Line(next,next+new Vector2(.8f,-1),.035f,TownColor.Soil,.073f);previous=next;
        }
    }
    private void Line(Vector2 a,Vector2 b,float width,TownColor color,float y)
    {var d=(b-a).normalized;var side=new Vector2(-d.y,d.x)*width*.5f;Polygon(new[]{a-side,b-side,b+side,a+side},y,color);}
    private void Patch(Vector2 center,float rx,float rz,TownColor color,float height,int seed)
    {
        var random=new System.Random(seed);var outline=new Vector2[13];
        for(int i=0;i<outline.Length;i++){float a=i*Mathf.PI*2/outline.Length,f=.85f+(float)random.NextDouble()*.15f;outline[i]=center+new Vector2(Mathf.Cos(a)*rx*f,Mathf.Sin(a)*rz*f);}
        for(int i=0;i<outline.Length;i++)Polygon(new[]{center,outline[i],outline[(i+1)%outline.Length]},height,color);
    }
    private void Polygon(IEnumerable<Vector2> polygon,float height,TownColor color)
    {
        foreach(var canvas in canvases)
        {
            var r=canvas.Plot.bounds;var points=polygon.ToList();
            points=Clip(points,p=>p.x-r.xMin);points=Clip(points,p=>r.xMax-p.x);points=Clip(points,p=>p.y-r.yMin);points=Clip(points,p=>r.yMax-p.y);
            for(int i=1;i<points.Count-1;i++)
            {
                Vector3 V(Vector2 p)=>new Vector3(p.x,height,p.y);var a=V(points[0]);var b=V(points[i]);var c=V(points[i+1]);
                if(Vector3.Cross(b-a,c-a).y<0)canvas.Mesh.Triangle(a,c,b,color);else canvas.Mesh.Triangle(a,b,c,color);
            }
        }
    }
    private static List<Vector2> Clip(List<Vector2> input,Func<Vector2,float> distance)
    {
        var output=new List<Vector2>();
        for(int i=0;i<input.Count;i++)
        {
            var a=input[i];var b=input[(i+1)%input.Count];float da=distance(a),db=distance(b);
            if(da>=0)output.Add(a);if((da>=0)!=(db>=0))output.Add(Vector2.Lerp(a,b,da/(da-db)));
        }
        return output;
    }
    private static bool OverlapXZ(Bounds a,Bounds b)=>a.min.x<b.max.x&&a.max.x>b.min.x&&a.min.z<b.max.z&&a.max.z>b.min.z;
    private void SaveScenery()
    {
        var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try
        {
            var scenery=root.GetComponentInChildren<TownExpansionScenery>(true);
            for(int i=scenery.transform.childCount-1;i>=0;i--)Object.DestroyImmediate(scenery.transform.GetChild(i).gameObject);
            var groups=new List<TownPlotScenery>();
            foreach(var canvas in canvases)
            {
                string id=canvas.Plot.territory.ToString();var mesh=canvas.Mesh.Build(id+"Story");mesh.uv2=mesh.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();Save(mesh,"Meshes/"+id+"Story.asset");
                var go=new GameObject("Yard story "+id,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(scenery.transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+id+"Story.asset");go.GetComponent<MeshRenderer>().sharedMaterial=surface;
                foreach(var marker in canvas.Markers){var t=new GameObject(marker.id).transform;t.SetParent(go.transform,false);t.localPosition=marker.position;}
                groups.Add(new TownPlotScenery{territory=canvas.Plot.territory,model=go});
            }
            scenery.plots=groups.ToArray();PrefabUtility.SaveAsPrefabAsset(root,TownWorldAssembler.PrefabPath);AssetDatabase.SaveAssets();
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static void Save(Object asset,string relative)=>TownBakeryPlacement.SaveAsset(asset,Root+"/"+relative);
}
