using System;
using System.IO;
using System.Linq;
using UnityEngine;
using RetailEmpireTycoon.City;

/// <summary>Checks the visual/physical failures from the close-up road and parking review.</summary>
internal static class PinebrookRoadAcceptance
{
    internal static void Validate(GameObject town,PinebrookDistrictLayout plan)
    {
        var geometry=new PinebrookStreetGeometry(plan);var crossings=geometry.Crossings;
        Vector2[] Corners(PinebrookStreetGeometry.Crossing c)
        {
            var a=new Vector2(c.Direction.x,c.Direction.z)*1.2f;var b=new Vector2(-c.Direction.z,c.Direction.x)*3.5f;var p=new Vector2(c.Center.x,c.Center.z);
            return new[]{p-a-b,p+a-b,p+a+b,p-a+b};
        }
        bool Overlap(Vector2[] a,Vector2[] b)
        {
            foreach(var polygon in new[]{a,b})for(int i=0;i<4;i++)
            {var d=polygon[(i+1)%4]-polygon[i];var axis=new Vector2(-d.y,d.x).normalized;
                if(a.Max(p=>Vector2.Dot(p,axis))<=b.Min(p=>Vector2.Dot(p,axis))+.02f||b.Max(p=>Vector2.Dot(p,axis))<=a.Min(p=>Vector2.Dot(p,axis))+.02f)return false;}
            return true;
        }
        for(int i=0;i<crossings.Length;i++)
        {
            foreach(var p in Corners(crossings[i]))Asphalt(new Vector3(p.x,0,p.y));
            for(int j=i+1;j<crossings.Length;j++)Require(!Overlap(Corners(crossings[i]),Corners(crossings[j])),"Crosswalks overlap at "+crossings[i].Center+" / "+crossings[j].Center);
        }
        for(float z=296;z<=319;z+=.5f)for(float x=-27.5f;x<=-20.5f;x+=1)Asphalt(new Vector3(x,0,z));
        int free=0,occupied=0,drivewayProbes=0;
        foreach(var lot in plan.parking)
        {
            for(int slot=0;slot<lot.stalls;slot++)
            {
                var center=lot.Local((slot-(lot.stalls-1)/2f)*2.8f,9.1f);Asphalt(center);
                var hits=Physics.OverlapBox(center+Vector3.up*1.05f,new Vector3(1.02f,.55f,2.25f),Quaternion.Euler(0,lot.yaw,0),~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>c.transform.IsChildOf(town.transform)).ToArray();
                if(lot.occupied.Contains(slot)){occupied++;Require(hits.Any(c=>c.transform.IsChildOf(town.transform.Find("Pinebrook/Props/Parked_"+lot.name+"_"+(slot+1).ToString("00")))),"Parked model not aligned with bay "+lot.name+" / "+slot);}
                else {free++;Require(hits.Length==0,"Free parking bay obstructed: "+lot.name+" / "+slot+" / "+string.Join(",",hits.Select(h=>h.name)));}
            }
            var start=new Vector3(lot.roadX,0,lot.roadZ);var end=lot.Local(0,3.1f);var forward=(end-start).normalized;int steps=Mathf.CeilToInt(Vector3.Distance(start,end));
            for(int i=0;i<=steps;i++)
            {
                var p=Vector3.Lerp(start,end,(float)i/steps);Asphalt(p);
                var hits=Physics.OverlapBox(p+Vector3.up*1.05f,new Vector3(1.02f,.55f,2.25f),Quaternion.LookRotation(forward),~0,QueryTriggerInteraction.Ignore).Where(c=>c.transform.IsChildOf(town.transform)).ToArray();
                Require(hits.Length==0,"Parking entrance obstructed: "+lot.name+" / "+p+" / "+string.Join(",",hits.Select(h=>h.name)));drivewayProbes++;
            }
        }
        Require(free>occupied&&plan.parking.Any(l=>l.occupied.Length==0),"Parking needs varied occupancy and an entirely free visitor lot.");
        var traffic=town.GetComponentInChildren<TownRoadTraffic>();Require(traffic.Network.SparseTrafficMaximum==4&&traffic.MaximumCars==29,"Local density changed the global town pool.");
        File.WriteAllText(PinebrookDistrictBuild.Report+"/roads-and-parking.txt",$"PASS: {crossings.Length} non-overlapping crossings fully on asphalt; full-width gateway connection; {plan.parking.Length} connected visitor lots, {free} free and {occupied} occupied bays; {drivewayProbes} unobstructed access probes; local ambient traffic capped at four, global pool remains 29.\n");
        void Asphalt(Vector3 p)
        {
            var hits=Physics.RaycastAll(p+Vector3.up*2,Vector3.down,3,~0,QueryTriggerInteraction.Ignore).Where(h=>h.normal.y>.9f&&h.point.y<.3f&&h.collider.transform.IsChildOf(town.transform)).OrderByDescending(h=>h.point.y).ToArray();
            bool valid=hits.Length>0&&(hits[0].collider.name=="NewRoadsUnion"||Mathf.FloorToInt(hits[0].textureCoord.x*32)==7);
            string face="";
            if(!valid&&hits.Length>0&&hits[0].collider is MeshCollider collider)
            {
                var mesh=collider.sharedMesh;var ids=mesh.triangles;var vertices=mesh.vertices;int i=hits[0].triangleIndex*3;
                if(i>=0&&i+2<ids.Length)
                {var a=vertices[ids[i]];var b=vertices[ids[i+1]];var c=vertices[ids[i+2]];float longest=Mathf.Max(Vector3.Distance(a,b),Mathf.Max(Vector3.Distance(b,c),Vector3.Distance(c,a)));face=" Face: "+a.ToString("F6")+" / "+b.ToString("F6")+" / "+c.ToString("F6")+"; minimum altitude "+Vector3.Cross(b-a,c-a).magnitude/longest;}
            }
            Require(valid,"Missing/covered asphalt at "+p+": "+string.Join(",",hits.Select(h=>h.collider.name+" @ "+h.point.y))+face);
        }
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
