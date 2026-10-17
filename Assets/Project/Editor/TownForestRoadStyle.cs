using System;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;

/// <summary>Shares the accepted crossing's road finish without changing road or pavement physics.</summary>
internal static class TownForestRoadStyle
{
    internal const string Root=TownForestArt.Root+"/RoadStyle";
    internal const string MaterialPath="Assets/Art/TownGatewayUpdate/Materials/Surface.mat";
    internal static void Apply(Transform streets)
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(material==null)throw new InvalidOperationException("The reference approach-road material is missing.");
        foreach(string name in new[]{"ForestRoadAndWalkSurface","ForestPavingJoints"})
            streets.Find(name).GetComponent<MeshRenderer>().sharedMaterial=material;

        var paint=new TownMeshBuilder();
        // World-aligned 3 m dash / 3 m gap, 10 cm wide, as on the existing approach.
        foreach(float x in new[]{-120f,-24f,72f})
            for(int z=315;z<426;z++)
                if(z%6<3&&!AtCrosswalk(z+.5f))paint.Ground(x,z+.5f,.10f,1,.128f,TownColor.White);
        foreach(float z in new[]{310f,430f})
            for(int x=-115;x<68;x++)
                if((x%6+6)%6<3&&Mathf.Abs(x+.5f+24)>8)paint.Ground(x+.5f,z,1,.10f,.128f,TownColor.White);

        // Paint only exposed road edges: never close the entrance or draw lines through junctions.
        for(int x=-124;x<76;x++)for(int z=306;z<434;z++)
        {
            var p=new Vector3(x+.5f,0,z+.5f);
            if(!TownForestPlan.OnRoad(p))continue;
            foreach(float side in new[]{-1f,1f})
            {
                if(!TownForestPlan.OnRoad(p+Vector3.right*side)&&!AtCrosswalk(p.z))
                    paint.Ground(p.x+side*.65f,p.z,.08f,1,.166f,TownColor.White);
                bool entrance=z==306&&x>=-28&&x<-20;
                if(!entrance&&!TownForestPlan.OnRoad(p+Vector3.forward*side))
                    paint.Ground(p.x,p.z+side*.65f,1,.08f,.166f,TownColor.White);
            }
        }
        foreach(float x in new[]{-120f,-24f,72f})foreach(float z in new[]{320f,420f})
            for(int i=0;i<7;i++)paint.Ground(x-3+i,z,.55f,2.2f,.13f,TownColor.White);
        foreach(float z in new[]{310f,430f})foreach(float side in new[]{-1f,1f})
            for(int i=0;i<5;i++)paint.Ground(-24+side*10,z+side*2-1.5f+i*.65f,.25f,.42f,.13f,TownColor.White);

        Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        var mesh=paint.Build("Forest road paint - approach style");
        mesh.uv2=mesh.vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();
        TownBakeryPlacement.SaveAsset(mesh,Root+"/RoadPaint.asset");
        var node=streets.Find("ForestRoadPaint")??TownForestArt.Child(streets,"ForestRoadPaint");
        var filter=node.GetComponent<MeshFilter>()??node.gameObject.AddComponent<MeshFilter>();
        var renderer=node.GetComponent<MeshRenderer>()??node.gameObject.AddComponent<MeshRenderer>();
        filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/RoadPaint.asset");renderer.sharedMaterial=material;
    }
    private static bool AtCrosswalk(float z)=>Mathf.Abs(z-320)<2.5f||Mathf.Abs(z-420)<2.5f;
}
