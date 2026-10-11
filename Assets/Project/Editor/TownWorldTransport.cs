using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

internal sealed partial class TownWorldAssembler
{
    private static float TransportDistance(Vector3 point,Vector3[] path)
    {
        float best=float.MaxValue;
        for(int i=1;i<path.Length;i++)
        {
            var a=path[i-1];var b=path[i];a.y=b.y=point.y;
            var delta=b-a;float t=delta.sqrMagnitude<.001f?0:Mathf.Clamp01(Vector3.Dot(point-a,delta)/delta.sqrMagnitude);
            best=Mathf.Min(best,Vector3.Distance(point,a+delta*t));
        }
        return best;
    }

    private Mesh PersistMesh(Mesh mesh,string name)
    {
        string path=Folder+"/Meshes/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        saved.Clear();saved.indexFormat=mesh.indexFormat;saved.vertices=mesh.vertices;saved.normals=mesh.normals;
        saved.uv=mesh.uv;saved.subMeshCount=mesh.subMeshCount;
        for(int sub=0;sub<mesh.subMeshCount;sub++)saved.SetTriangles(mesh.GetTriangles(sub),sub);
        saved.RecalculateBounds();EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);return saved;
    }

    private void ApprovedTerrain()
    {
        System.IO.Directory.CreateDirectory(Folder+"/Meshes");AssetDatabase.Refresh();
        var ground=new TownMeshBuilder();var water=new TownMeshBuilder();var bounds=layout.cityBounds;
        const float step=12;
        for(float z=bounds.yMin-120;z<bounds.yMax+120;z+=step)
        {
            float next=Mathf.Min(z+step,bounds.yMax+120),a=layout.RiverCenter(z),b=layout.RiverCenter(next);
            float left=bounds.xMin-120,right=bounds.xMax+120;
            ground.Quad(new Vector3(left,0,next),new Vector3(b-18,0,next),new Vector3(a-18,0,z),new Vector3(left,0,z),TownColor.Grass);
            ground.Quad(new Vector3(b+18,0,next),new Vector3(right,0,next),new Vector3(right,0,z),new Vector3(a+18,0,z),TownColor.Grass);
            ground.Quad(new Vector3(b-18,0,next),new Vector3(b-10,-4.5f,next),new Vector3(a-10,-4.5f,z),new Vector3(a-18,0,z),TownColor.Sand);
            ground.Quad(new Vector3(b+10,-4.5f,next),new Vector3(b+18,0,next),new Vector3(a+18,0,z),new Vector3(a+10,-4.5f,z),TownColor.Sand);
            ground.Quad(new Vector3(b-10,-4.5f,next),new Vector3(b+10,-4.5f,next),new Vector3(a+10,-4.5f,z),new Vector3(a-10,-4.5f,z),TownColor.Sand);
            water.Quad(new Vector3(b-12,-3.05f,next),new Vector3(b+12,-3.05f,next),new Vector3(a+12,-3.05f,z),new Vector3(a-12,-3.05f,z),TownColor.Glass);
            // Small irregular ripples, not repeated white bars down the river centre.
            float offset=Mathf.Sin(z*.47f)*7;
            water.Arc(new Vector3(a+offset,-3.025f,z+3),.8f,.86f,10,120,TownColor.White,5);
        }
        var terrain=Geometry("River-cut town terrain",ground,"Ground and courtyards");
        var physical=PersistMesh(terrain.GetComponent<MeshFilter>().sharedMesh,"RiverTerrainCollision");
        terrain.GetComponent<MeshFilter>().sharedMesh=physical;terrain.AddComponent<MeshCollider>().sharedMesh=physical;
        Geometry("River surface",water,"River and banks");
        foreach(bool vertical in new[]{true,false})foreach(int side in new[]{-1,1})
        {
            var edge=new GameObject("World edge");edge.transform.SetParent(Section("World boundaries"),false);
            var center=vertical?new Vector3(side<0?bounds.xMin:bounds.xMax,2,bounds.center.y):new Vector3(bounds.center.x,2,side<0?bounds.yMin:bounds.yMax);
            BoxCollider(edge,center,vertical?new Vector3(1,4,bounds.height):new Vector3(bounds.width,4,1));
        }
    }

    private void ApprovedTransport()
    {
        CountryRoad();RoadRiverBridge();RailLine(layout.railway,false);RailLine(layout.freightSiding,true);RailRiverBridge();
        RailwayStation();PassengerTrain();
    }

    private void CountryRoad()
    {
        var mesh=new TownMeshBuilder();var physical=new TownMeshBuilder();var points=layout.countryRoad.points;
        for(int i=1;i<points.Length;i++)
        {
            if(i==1)continue; // The city T-module already supplies the first six metres, including its collision surface.
            var a=points[i-1];var b=points[i];var delta=b-a;var side=Vector3.Cross(Vector3.up,delta).normalized;
            var sideA=i>1?Vector3.Cross(Vector3.up,b-points[i-2]).normalized:side;
            var sideB=i+1<points.Length?Vector3.Cross(Vector3.up,points[i+1]-a).normalized:side;
            var lift=Vector3.up*.12f;
            mesh.Quad(b-sideB*4+lift,b+sideB*4+lift,a+sideA*4+lift,a-sideA*4+lift,TownColor.Asphalt);
            physical.Quad(b-sideB*4.1f+lift,b+sideB*4.1f+lift,a+sideA*4.1f+lift,a-sideA*4.1f+lift,TownColor.Asphalt);
            var shoulder=a.y>1.9f&&b.y>1.9f?TownColor.Pavement:TownColor.Sand;
            foreach(float sign in new[]{-1f,1f})
                if(sign>0)mesh.Quad(b+sideB*4+lift,b+sideB*5.8f+lift,a+sideA*5.8f+lift,a+sideA*4+lift,shoulder);
                else mesh.Quad(a-sideA*4+lift,a-sideA*5.8f+lift,b-sideB*5.8f+lift,b-sideB*4+lift,shoulder);
            int marks=delta.magnitude<3?(i%4==0?1:0):Mathf.Max(1,Mathf.FloorToInt(delta.magnitude/5));
            for(int mark=0;mark<marks;mark++)
            {
                var p=Vector3.Lerp(a,b,(mark+.15f)/marks)+Vector3.up*.14f;
                var q=Vector3.Lerp(a,b,Mathf.Min(1,(mark+.65f)/marks))+Vector3.up*.14f;
                PaintedLine(mesh,p,q,.1f,TownColor.White);
            }
        }
        // One continuous collision ribbon follows the same vertices as the visible road.
        // Overlapping flat join disks create steps when neighbouring samples have different ramp heights.
        var physics=new GameObject("Country road physical surface",typeof(MeshCollider));physics.transform.SetParent(Section("Road infrastructure"),false);
        physics.GetComponent<MeshCollider>().sharedMesh=PersistMesh(physical.Build("CountryRoadCollision"),"CountryRoadCollision");
        Geometry("Continuous country asphalt road",mesh,"Road infrastructure");
    }

    private void RoadRiverBridge()
    {
        var mesh=new TownMeshBuilder();const float x=328,z=-40;
        mesh.Box(new Vector3(x,1.8f,z),new Vector3(34,.6f,12),TownColor.Pavement);
        foreach(float side in new[]{-5.85f,5.85f})
        {
            mesh.Box(new Vector3(x,2.17f,z+side),new Vector3(34,.16f,.3f),TownColor.White);
            for(float p=311;p<=345;p+=2)
                mesh.Box(new Vector3(p,2.73f,z+side),new Vector3(.065f,1.1f,.065f),TownColor.Frame);
            mesh.Box(new Vector3(x,3.25f,z+side),new Vector3(34,.07f,.07f),TownColor.Frame);
            mesh.Box(new Vector3(x,2.6f,z+side),new Vector3(34,.055f,.055f),TownColor.Frame);
        }
        foreach(float p in new[]{318f,338f})mesh.Box(new Vector3(p,-1.35f,z),new Vector3(1.4f,6.3f,7),TownColor.Stone);
        foreach(float p in new[]{311f,345f})
        {
            mesh.Box(new Vector3(p,-.2f,z),new Vector3(2,4,12),TownColor.Stone);
            foreach(float s in new[]{-1f,1f})mesh.Box(new Vector3(p+s*2.2f,.6f,z+s*5.4f),new Vector3(5,2.4f,.6f),TownColor.Stone,Quaternion.Euler(0,s*20,0));
        }
        var bridge=Geometry("Road bridge — farm connection",mesh,"Road infrastructure");
        foreach(float side in new[]{-5.85f,5.85f})BoxCollider(bridge,new Vector3(x,2.7f,z+side),new Vector3(34,1.4f,.25f));
    }

    private void RailLine(TownTransportPath path,bool siding)
    {
        var mesh=new TownMeshBuilder();var bed=new TownMeshBuilder();
        for(int i=1;i<path.points.Length;i++)
        {
            var a=path.points[i-1];var b=path.points[i];var delta=b-a;var side=Vector3.Cross(Vector3.up,delta).normalized;
            bool bridge=!siding&&a.x>=299&&b.x<=345&&Mathf.Abs(a.z-228)<.01f;
            if(!bridge)
            {
                bed.Quad(b-side*2.2f-Vector3.up*.27f,b+side*2.2f-Vector3.up*.27f,a+side*2.2f-Vector3.up*.27f,a-side*2.2f-Vector3.up*.27f,TownColor.Stone);
                foreach(float sign in new[]{-1f,1f})
                {
                    var outerA=new Vector3(a.x,0,a.z)+side*sign*4;var innerA=a+side*sign*2.2f-Vector3.up*.27f;
                    var outerB=new Vector3(b.x,0,b.z)+side*sign*4;var innerB=b+side*sign*2.2f-Vector3.up*.27f;
                    if(sign>0)bed.Quad(outerA,innerA,innerB,outerB,TownColor.LeafLight);
                    else bed.Quad(outerB,innerB,innerA,outerA,TownColor.LeafLight);
                }
            }
            foreach(float sign in new[]{-1f,1f})
                mesh.Box((a+b)*.5f+side*sign*.72f+Vector3.up*.03f,new Vector3(.075f,.14f,delta.magnitude+.05f),TownColor.Metal,Quaternion.LookRotation(delta));
            int ties=Mathf.CeilToInt(delta.magnitude/.85f);
            for(int tie=0;tie<ties;tie++)mesh.Box(Vector3.Lerp(a,b,(tie+.5f)/ties)-Vector3.up*.11f,new Vector3(2.2f,.15f,.2f),TownColor.Wood,Quaternion.LookRotation(delta));
        }
        Geometry(siding?"Freight rail siding":"Continuous northern railway",mesh,"Railway infrastructure");
        Geometry(siding?"Freight ballast":"Railway embankment",bed,"Railway infrastructure");
        if(siding)
        {
            var end=path.points.Last();var buffer=new TownMeshBuilder();
            var forward=(end-path.points[path.points.Length-2]).normalized;
            buffer.Box(end+new Vector3(0,.55f,0),new Vector3(2.6f,1.1f,.2f),TownColor.Red,Quaternion.LookRotation(forward));
            Geometry("Freight siding buffer stop",buffer,"Railway infrastructure");
        }
    }

    private void RailRiverBridge()
    {
        var mesh=new TownMeshBuilder();var center=new Vector3(322,4.05f,228);
        mesh.Box(center,new Vector3(46,.6f,5.2f),TownColor.Stone);
        foreach(float z in new[]{225.5f,230.5f})
        {
            mesh.Box(new Vector3(322,4.35f,z),new Vector3(46,.3f,.3f),TownColor.Metal);
            mesh.Box(new Vector3(322,6.1f,z),new Vector3(46,.15f,.15f),TownColor.Frame);
            for(int i=0;i<10;i++)
            {
                float x=299+i*4.6f;
                mesh.Box(new Vector3(x+2.3f,5.2f,z),new Vector3(.12f,4.9f,.12f),TownColor.Frame,Quaternion.Euler(0,0,-65));
                mesh.Box(new Vector3(x,5.25f,z),new Vector3(.12f,1.7f,.12f),TownColor.Frame);
            }
        }
        foreach(float x in new[]{303f,342f})mesh.Box(new Vector3(x,-.2f,228),new Vector3(2,8.2f,6),TownColor.Stone);
        Geometry("Separate railway river bridge",mesh,"Railway infrastructure");
    }

    private void RailwayStation()
    {
        // The concept's miniature rails are removed; the station uses the world's continuous main line.
        ApprovedModel("08_RailwayStation",new Vector3(-190,-.24f,212),0,"Railway station",
            new[]{new Bounds(new Vector3(1,2.9f,-2.4f),new Vector3(10,4.6f,6)),new Bounds(new Vector3(8.3f,2.25f,-2.4f),new Vector3(3.8f,3.2f,6))},true);
        var platform=new TownMeshBuilder();
        platform.Box(new Vector3(-190,1.02f,224),new Vector3(35,2.04f,4.4f),TownColor.Pavement);
        platform.Box(new Vector3(-190,2.07f,226.05f),new Vector3(35,.04f,.14f),TownColor.Yellow);
        for(int i=0;i<8;i++)platform.Box(new Vector3(-200,.12f+i*.13f,216.6f+i*.65f),new Vector3(4,.24f+i*.26f,.7f),TownColor.Pavement);
        Geometry("Station platform and pedestrian steps",platform,"Railway station");
        Surface("Station forecourt",new Rect(-213,198,46,6),TownColor.Pavement);
        Driveway(new Vector3(-214,0,201),new Vector3(-216,0,192));
        Model("BusStop",new Vector3(-174,0,202),180,"Station street furniture");
        Model("CustomerParking",new Vector3(-200,0,200),90,"Station parking");
    }

    private void PassengerTrain()
    {
        var group=new GameObject("Passenger train service");group.transform.SetParent(root.transform,false);
        var service=group.AddComponent<TownRailTraffic>();service.layout=layout;
        var cars=new List<Transform>();
        for(int carriage=0;carriage<4;carriage++)
        {
            var mesh=new TownMeshBuilder();bool engine=carriage==0;
            mesh.Box(new Vector3(0,1.75f,0),new Vector3(2.6f,2.6f,10.5f),engine?TownColor.GreenStripe:TownColor.Cream);
            mesh.Box(new Vector3(0,.65f,0),new Vector3(2.7f,.35f,10.7f),TownColor.Frame);
            mesh.Box(new Vector3(0,3.12f,0),new Vector3(2.75f,.25f,10.6f),TownColor.Mint);
            foreach(float x in new[]{-1.31f,1.31f})
            {
                mesh.Box(new Vector3(x,1.1f,0),new Vector3(.025f,.22f,10.3f),TownColor.Red);
                for(int window=0;window<7;window++)mesh.Box(new Vector3(x,2.3f,-4.3f+window*1.4f),new Vector3(.03f,.95f,.95f),TownColor.Glass);
            }
            foreach(float z in new[]{-3.4f,3.4f})foreach(float x in new[]{-1.05f,1.05f})
                mesh.Cylinder(new Vector3(x,.36f,z),.33f,.33f,.18f,TownColor.Rubber,10,Quaternion.Euler(0,0,90));
            mesh.Box(new Vector3(0,2.25f,5.28f),new Vector3(2.1f,.85f,.035f),TownColor.Glass);
            if(engine)foreach(float x in new[]{-.85f,.85f})mesh.Box(new Vector3(x,1.6f,5.31f),new Vector3(.32f,.23f,.04f),TownColor.Light);
            var car=new GameObject(engine?"Locomotive":"Passenger carriage "+carriage,typeof(MeshFilter),typeof(MeshRenderer));
            car.transform.SetParent(group.transform,false);car.GetComponent<MeshFilter>().sharedMesh=PersistMesh(mesh.Build(car.name),"TrainCar"+carriage);
            car.GetComponent<MeshRenderer>().sharedMaterial=material;cars.Add(car.transform);
        }
        service.cars=cars.ToArray();service.Advance(.001f);
    }
}
