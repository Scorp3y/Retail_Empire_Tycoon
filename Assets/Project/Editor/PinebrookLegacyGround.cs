using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using RetailEmpireTycoon.Editor.TownKit;

/// <summary>Retires duplicated legacy park paving and obsolete townhouse ground strips inside the district.</summary>
internal static class PinebrookLegacyGround
{
    internal static void Apply(GameObject town)
    {
        var geometry=new TownCityCorrectionsGeometry(town.GetComponentsInChildren<MeshFilter>(true)
            .Where(f=>f.name.StartsWith("Stable flat ground",StringComparison.Ordinal)));
        bool Flat(TownCityCorrectionsGeometry.View v,int i,float minimum)
        {var p=TownCityCorrectionsGeometry.Points(v,i);return p.Min(a=>a.y)>=minimum&&p.Max(a=>a.y)-p.Min(a=>a.y)<.0001f;}
        int paths=geometry.ClipGround(new Rect(-290,296,500,372),.20f,(v,i)=>Flat(v,i,.10f)&&
            (TownCityCorrectionsGeometry.Color(v,i,TownColor.Pavement)||TownCityCorrectionsGeometry.Color(v,i,TownColor.Sand)||TownCityCorrectionsGeometry.Color(v,i,TownColor.Stone)));
        int strips=geometry.ClipGround(new Rect(-132,434,216,40),.18f,(v,i)=>Flat(v,i,.005f));
        strips+=geometry.Remove((v,i)=>Flat(v,i,-.001f)&&TownCityCorrectionsGeometry.Points(v,i).All(p=>p.x>=-132&&p.x<=84&&p.z>=434&&p.z<=474&&p.y<=.005f));
        int gateway=geometry.ClipGround(new Rect(-31,296,14,18),.20f,(v,i)=>Flat(v,i,.005f));
        foreach(var view in geometry.Views)
        {
            if(view.Removed.Count==0)continue;
            var colliders=view.Filter.GetComponents<MeshCollider>().Where(c=>c.sharedMesh==view.Source).ToArray();
            string id="LegacyGround_"+view.Filter.name.Replace(' ','_');view.Save(id,PinebrookDistrictArt.Root);
            foreach(var collider in colliders)collider.sharedMesh=view.Filter.sharedMesh;
        }
        Directory.CreateDirectory(PinebrookDistrictBuild.Report);
        File.WriteAllText(PinebrookDistrictBuild.Report+"/legacy-ground.txt",$"Legacy paving retired: {paths} path faces, {strips} obsolete garden-strip faces, {gateway} gateway faces. Low playground/rain-garden surfaces and all vertical props retained.\n");
    }
}
