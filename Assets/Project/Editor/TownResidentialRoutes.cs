using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Orthogonal entrance paths connect to the existing public sidewalk, not through solid building wings.</summary>
internal static class TownResidentialRoutes
{
    private const float Margin=1.4f;
    private const float Clearance=1.12f;
    public static IReadOnlyList<TownResidentialWalkway> Build(TownResidentialBlock block,Transform town)
    {
        var bounds=block.Bounds;
        var inner=Rect.MinMaxRect(bounds.xMin+Margin,bounds.yMin+Margin,bounds.xMax-Margin,bounds.yMax-Margin);
        var obstacles=town.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&!c.isTrigger)
            .Select(TownResidentialPlacement.ColliderBounds).Where(b=>b.max.y>.5f&&b.min.y<2&&
                b.max.x>=bounds.xMin-2&&b.min.x<=bounds.xMax+2&&b.max.z>=bounds.yMin-2&&b.min.z<=bounds.yMax+2)
            .Select(b=>Rect.MinMaxRect(b.min.x-Clearance,b.min.z-Clearance,b.max.x+Clearance,b.max.z+Clearance)).ToArray();
        var doors=block.Buildings.SelectMany(b=>b.Model.Entrances.Select(e=>
            b.Pose.MultiplyPoint3x4(e.Position)+b.Pose.MultiplyVector(e.Outward)*1.3f)).ToArray();
        float[] Coordinates(bool horizontal)
        {
            float min=horizontal?inner.xMin:inner.yMin,max=horizontal?inner.xMax:inner.yMax;
            return obstacles.SelectMany(o=>horizontal?new[]{o.xMin-.04f,o.xMax+.04f}:new[]{o.yMin-.04f,o.yMax+.04f})
                .Concat(doors.Select(p=>horizontal?p.x:p.z)).Concat(new[]{min,max})
                .Where(v=>v>=min&&v<=max).Select(v=>(float)Math.Round(v,4)).Distinct().OrderBy(v=>v).ToArray();
        }
        var xs=Coordinates(true);var zs=Coordinates(false);
        int count=xs.Length*zs.Length;
        var predecessor=Enumerable.Repeat(-2,count).ToArray();
        var queue=new Queue<int>();
        Vector3 Point(int index)=>new Vector3(xs[index%xs.Length],0,zs[index/xs.Length]);
        bool Clear(Vector3 point)=>!obstacles.Any(o=>o.Contains(new Vector2(point.x,point.z)));
        for(int i=0;i<count;i++)
        {
            int x=i%xs.Length,z=i/xs.Length;
            if((x==0||x==xs.Length-1||z==0||z==zs.Length-1)&&Clear(Point(i)))
            {predecessor[i]=-1;queue.Enqueue(i);}
        }
        while(queue.Count>0)
        {
            int current=queue.Dequeue(),x=current%xs.Length,z=current/xs.Length;
            foreach(var next in new[]{x>0?current-1:-1,x+1<xs.Length?current+1:-1,z>0?current-xs.Length:-1,z+1<zs.Length?current+xs.Length:-1})
            {
                if(next<0||predecessor[next]!=-2||!Clear(Point(next))||!ClearSegment(Point(current),Point(next),obstacles))continue;
                predecessor[next]=current;queue.Enqueue(next);
            }
        }
        var result=new List<TownResidentialWalkway>();
        var corners=new[]{new Vector3(inner.xMin,0,inner.yMin),new Vector3(inner.xMax,0,inner.yMin),
            new Vector3(inner.xMax,0,inner.yMax),new Vector3(inner.xMin,0,inner.yMax)};
        for(int i=0;i<4;i++)
        {
            if(!ClearSegment(corners[i],corners[(i+1)%4],obstacles))
                throw new InvalidOperationException(block.Id+": perimeter sidewalk is obstructed.");
            result.Add(new TownResidentialWalkway("Perimeter"+i,corners[i],corners[(i+1)%4]));
        }
        foreach(int corner in new[]{0,2})
        {
            var from=corners[corner];
            var to=new Vector3(corner==0?bounds.xMin-.5f:bounds.xMax+.5f,0,from.z);
            if(!ClearSegment(from,to,obstacles))throw new InvalidOperationException(block.Id+": public sidewalk seam is obstructed.");
            result.Add(new TownResidentialWalkway("PublicSidewalk"+corner,from,to));
        }
        int doorIndex=0;
        foreach(var door in doors)
        {
            int x=Array.FindIndex(xs,v=>Mathf.Abs(v-door.x)<.001f),z=Array.FindIndex(zs,v=>Mathf.Abs(v-door.z)<.001f);
            if(x<0||z<0||predecessor[z*xs.Length+x]==-2)
                throw new InvalidOperationException(block.Id+": no full-width sidewalk route for door "+door);
            var points=new List<Vector3>{door};int node=z*xs.Length+x;
            while(predecessor[node]>=0) {node=predecessor[node];points.Add(Point(node));}
            var simplified=new List<Vector3>();
            foreach(var point in points)
            {
                if(simplified.Count>=2)
                {
                    var a=simplified[simplified.Count-1]-simplified[simplified.Count-2];var b=point-simplified[simplified.Count-1];
                    if(Vector3.Cross(a,b).sqrMagnitude<.000001f&&Vector3.Dot(a,b)>0)simplified.RemoveAt(simplified.Count-1);
                }
                simplified.Add(point);
            }
            for(int i=1;i<simplified.Count;i++)
                result.Add(new TownResidentialWalkway("Entrance"+doorIndex+"."+i,simplified[i-1],simplified[i]));
            doorIndex++;
        }
        return result.AsReadOnly();
    }

    private static bool ClearSegment(Vector3 a,Vector3 b,IEnumerable<Rect> obstacles)
    {
        foreach(var obstacle in obstacles)
        {
            if(Mathf.Abs(a.x-b.x)<.001f)
            {
                if(a.x>=obstacle.xMin&&a.x<=obstacle.xMax&&Mathf.Max(a.z,b.z)>=obstacle.yMin&&Mathf.Min(a.z,b.z)<=obstacle.yMax)return false;
            }
            else if(a.z>=obstacle.yMin&&a.z<=obstacle.yMax&&Mathf.Max(a.x,b.x)>=obstacle.xMin&&Mathf.Min(a.x,b.x)<=obstacle.xMax)return false;
        }
        return true;
    }

    public static void ReadSaved(TownResidentialPlan plan,Transform town)
    {
        foreach(var block in plan.Blocks)
        {
            var group=town.Find(TownResidentialPlan.HierarchyRoot+"/"+block.Id+"/Walkways");
            if(group==null)throw new InvalidOperationException("Saved residential walkways missing: "+block.Id);
            var paths=new List<TownResidentialWalkway>();
            foreach(Transform route in group)
            {
                var from=route.Find("From");var to=route.Find("To");
                if(from==null||to==null)throw new InvalidOperationException("Incomplete walkway metadata: "+route.name);
                paths.Add(new TownResidentialWalkway(route.name,town.InverseTransformPoint(from.position),town.InverseTransformPoint(to.position)));
            }
            block.InitializeWalkways(paths);
        }
    }
}
