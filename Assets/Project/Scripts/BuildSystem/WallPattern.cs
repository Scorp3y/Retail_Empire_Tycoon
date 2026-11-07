using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Shares grid-aligned mesh variants so neighbouring modules continue the same atlas pattern.</summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class WallPattern : MonoBehaviour
    {
        public const int RepeatCells = 8;
        public const float RepeatUv = 192f / 2048;
        [SerializeField] private Mesh template;
        [SerializeField] private Mesh glazingTemplate;
        [SerializeField] private MeshRenderer leftJamb, rightJamb;
        [SerializeField] private bool solidWall;
        private static readonly Dictionary<(Mesh, int, int, int, int, int, bool, bool), Mesh> variants = new();

        public void Configure(Mesh mesh, Mesh glazing, bool solid) { template = mesh; glazingTemplate = glazing; solidWall = solid; }
        public void ConfigureJambs(MeshRenderer left, MeshRenderer right) { leftJamb=left; rightJamb=right; }
        public bool HasJambs => leftJamb != null;
        public void SetJambs(bool left, bool right)
        {
            if (!HasJambs) return;
            leftJamb.enabled=left; rightJamb.enabled=right;
        }

        internal static void RefreshConnections(GridSystem grid)
        {
            var rows = new Dictionary<Vector3Int,WallPattern>();
            var modules = new List<WallPattern>();
            var solids = new List<WallPattern>();
            var occupied = new Dictionary<Vector3Int,WallPattern>();
            float cell=grid.cellSize;
            Vector3Int Key(Vector3 p) => Vector3Int.RoundToInt(p/(cell*.5f));
            foreach (var module in FindObjectsOfType<WallPattern>())
            {
                var placed=module.GetComponent<PlacedObject>();
                if (placed == null || placed.item == null) continue;
                if (module.HasJambs)
                {
                    modules.Add(module);
                    rows[Key(module.transform.position)]=module;
                }
                if (!module.solidWall && placed.item.id!=Core.BuildItemData.StoreStripeCornerId) continue;
                if (module.solidWall) solids.Add(module);
                foreach (var at in placed.occupiedCells) occupied[at]=module;
            }
            foreach (var module in modules)
            {
                Vector3 origin=module.transform.position,right=module.transform.right;
                float halfLength=module.template.bounds.extents.x;
                bool SameNeighbour(Vector3 direction)
                {
                    return rows.TryGetValue(Key(origin+direction*(2*halfLength)),out var other) &&
                        other.template==module.template && Mathf.Abs(Vector3.Dot(other.transform.right,right))>.99f;
                }
                bool SpanEdge(Vector3 direction)
                {
                    var edge=origin+direction*halfLength;
                    float along=Mathf.Abs(right.x)>.5f?edge.x:edge.z;
                    return Mathf.FloorToInt(along/cell+.01f)%4==0;
                }
                module.SetJambs(!SameNeighbour(-right)||SpanEdge(-right),!SameNeighbour(right)||SpanEdge(right));
            }
            foreach (var module in solids) module.ConnectSolidEnds(grid,occupied);
        }

        private void ConnectSolidEnds(GridSystem grid, Dictionary<Vector3Int,WallPattern> occupied)
        {
            var origin=transform.position;
            var right=transform.right;
            float halfWidth=template.bounds.extents.x;
            var left=Connection(-right);
            var rightEnd=Connection(right);
            ApplyMesh(GetComponent<MeshFilter>(),template,origin,transform.rotation,left.millimetres,rightEnd.millimetres,left.open,rightEnd.open);
            var box=GetComponent<BoxCollider>();
            float extensionLeft=left.millimetres*.001f,extensionRight=rightEnd.millimetres*.001f;
            box.size=new Vector3(halfWidth*2+extensionLeft+extensionRight,box.size.y,box.size.z);
            box.center=new Vector3((extensionRight-extensionLeft)*.5f,box.center.y,box.center.z);

            (int millimetres,bool open) Connection(Vector3 direction)
            {
                var edge=origin+direction*halfWidth;
                // The endpoint may lie on a cell boundary across a perpendicular row.
                for(int side=-1;side<=1;side+=2)
                {
                    var probe=edge+direction*.015f+transform.forward*(side*.01f);
                    if(!occupied.TryGetValue(grid.WorldToCell(probe),out var other) || other==this)continue;
                    var delta=other.transform.position-origin;
                    if(!other.solidWall)
                    {
                        var arm=other.transform.InverseTransformDirection(-direction);
                        if(GetComponent<PlacedObject>().item.id==Core.BuildItemData.StoreStripeId &&
                            (arm.x>.99f || arm.z>.99f) && Mathf.Abs(Vector3.Dot(delta,transform.forward))<.001f)
                            return (0,true);
                        continue;
                    }
                    if(Mathf.Abs(Vector3.Dot(other.transform.right,right))>.99f)
                    {
                        // Equal sections share an edge. Their internal end faces otherwise compete for depth and shadows.
                        float gap=Vector3.Dot(delta,direction)-halfWidth-other.template.bounds.extents.x;
                        if(other.template==template && Mathf.Abs(Vector3.Dot(delta,transform.forward))<.001f && Mathf.Abs(gap)<.001f)
                            return (0,true);
                        continue;
                    }
                    if(other.template.bounds.max.y+.001f<template.bounds.max.y)continue;
                    // Continue just inside the other solid wall. Its exterior face covers this extension.
                    float depth=other.GetComponent<BoxCollider>().size.z;
                    float needed=Vector3.Dot(delta,direction)-halfWidth-depth*.35f;
                    if(needed>.001f) return (Mathf.CeilToInt(needed*1000),true);
                }
                return (0,false);
            }
        }
        private void Awake() => Align();
        public void Align() => Apply(GetComponent<MeshFilter>(), transform.position, transform.rotation);

        internal Bounds BoundsBeforeJoining(Bounds visibleBounds)
        {
            if(!solidWall) return visibleBounds;
            var bounds=template.bounds;
            bool turned=Mathf.Abs(transform.right.z)>.5f;
            var size=turned?new Vector3(bounds.size.z,bounds.size.y,bounds.size.x):bounds.size;
            return new Bounds(transform.TransformPoint(bounds.center),size);
        }

        public void Apply(MeshFilter target, Vector3 position, Quaternion rotation)
        {
            if (template == null || target == null) return;
            ApplyMesh(target, template, position, rotation);
            if (glazingTemplate != null)
                ApplyMesh(target.transform.Find("Glazing").GetComponent<MeshFilter>(), glazingTemplate, position, rotation);
        }

        private static void ApplyMesh(MeshFilter target, Mesh template, Vector3 position, Quaternion rotation,
            int leftExtension=0,int rightExtension=0,bool openLeft=false,bool openRight=false)
        {
            var right = rotation * Vector3.right;
            var forward = rotation * Vector3.forward;
            int yaw = (Mathf.RoundToInt(rotation.eulerAngles.y / 90) + 4) % 4;
            int Phase(float metres)
            {
                int halfCells = Mathf.FloorToInt(metres / (ShopProportions.CellSize * .5f) + .01f);
                return (halfCells % (RepeatCells * 2) + RepeatCells * 2) % (RepeatCells * 2);
            }
            bool sideways = yaw % 2 != 0;
            int front = Phase(sideways ? position.z : position.x);
            int end = Phase(sideways ? position.x : position.z);
            var key = (template, yaw, front, end,leftExtension,rightExtension,openLeft,openRight);
            if (!variants.TryGetValue(key, out var mesh) || mesh == null)
            {
                var uv = template.uv;
                var vertices=template.vertices;
                float min=template.bounds.min.x,max=template.bounds.max.x;
                bool AtLeft(int index)=>Mathf.Abs(vertices[index].x-min)<.00001f;
                bool AtRight(int index)=>Mathf.Abs(vertices[index].x-max)<.00001f;
                List<int> visibleTriangles=null;
                if(openLeft || openRight)
                {
                    var indices=template.triangles;
                    visibleTriangles=new List<int>(indices.Length);
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        int a=indices[i],b=indices[i+1],c=indices[i+2];
                        if((openLeft && AtLeft(a) && AtLeft(b) && AtLeft(c)) ||
                           (openRight && AtRight(a) && AtRight(b) && AtRight(c)))continue;
                        visibleTriangles.Add(a);visibleTriangles.Add(b);visibleTriangles.Add(c);
                    }
                }
                var surface = new List<Vector2>();
                template.GetUVs(1, surface);
                float step = RepeatUv / (RepeatCells * 2);
                float metresToUv = RepeatUv / (RepeatCells * ShopProportions.CellSize);
                for (int i = 0; i < uv.Length; i++)
                {
                    float extra=AtLeft(i)?-leftExtension*.001f:AtRight(i)?rightExtension*.001f:0;
                    vertices[i].x+=extra;
                    if (surface[i].y == 0) continue;
                    bool isEnd = surface[i].y == 2;
                    float sign = isEnd ? (sideways ? forward.x : forward.z) : (sideways ? right.z : right.x);
                    uv[i].x += (isEnd ? end : front) * step + (surface[i].x+(isEnd?0:extra)) * sign * metresToUv;
                }
                mesh = Instantiate(template);
                mesh.name = $"{template.name} phase {yaw}-{front}-{end}";
                mesh.hideFlags = HideFlags.HideAndDontSave;
                if(leftExtension!=0 || rightExtension!=0)mesh.vertices=vertices;
                if(visibleTriangles!=null)mesh.SetTriangles(visibleTriangles,0);
                mesh.uv = uv;
                variants[key] = mesh;
            }
            if (target.sharedMesh != mesh) target.sharedMesh = mesh;
        }
    }
}
