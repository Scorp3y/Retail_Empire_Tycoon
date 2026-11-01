using System;
using UnityEngine;

/// <summary>Exact metre-based authoring contract. Positions are native town coordinates.</summary>
[Serializable] internal sealed class PinebrookDistrictLayout
{
    public string name;public int version;public Area bounds;public Area[] roads,clearings,plazas;public Walk[] paths;public Walk[] streets;public Parking[] parking;public Retained[] retained;public Placement[] placements;
    [Serializable] internal sealed class Parking
    {
        public string name;public float x,z,yaw,roadX,roadZ;public int stalls;public int[] occupied;
        public float Width=>stalls*2.8f+.8f;
        public Vector3 Local(float across,float depth)=>new Vector3(x,0,z)+Quaternion.Euler(0,yaw,0)*new Vector3(across,0,depth);
    }
    [Serializable] internal sealed class Retained{public int index;public Vector3 position;public float yaw;}
    [Serializable] internal sealed class Area
    {
        public float x,z,width,depth;
        public Rect Rect=>new Rect(x,z,width,depth);
        public bool Contains(Vector3 p)=>p.x>=x&&p.x<x+width&&p.z>=z&&p.z<z+depth;
        public Rect Expanded(float margin)=>new Rect(x-margin,z-margin,width+2*margin,depth+2*margin);
    }
    [Serializable] internal sealed class Walk
    {
        public string name;public float ax,az,bx,bz,width,height;
        public Vector3 Start=>new Vector3(ax,height,az);public Vector3 End=>new Vector3(bx,height,bz);
        public float Distance(Vector3 p){var a=new Vector2(ax,az);var d=new Vector2(bx-ax,bz-az);float t=Mathf.Clamp01(Vector2.Dot(new Vector2(p.x,p.z)-a,d)/d.sqrMagnitude);return Vector2.Distance(new Vector2(p.x,p.z),a+t*d);}
    }
    [Serializable] internal sealed class Placement{public string name,group,prefab,label;public Vector3 position;public float yaw,scale;}
}
