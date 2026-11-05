using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Authored right-hand lanes in native town metres. Never uses private driveways or shop plots.</summary>
    public sealed class TrafficRoadNetwork : ScriptableObject
    {
        [Serializable]
        public sealed class Junction
        {
            public Vector3 position;
            public int signalIndex = -1;
        }

        [Serializable]
        public sealed class Lane
        {
            public int from, to;
            public float speedLimit = 9;
            public bool rural;
            public Vector3[] points = Array.Empty<Vector3>();
            [NonSerialized] private TrafficPolyline path;
            public TrafficPolyline Path => path ??= new TrafficPolyline(points);
        }

        [SerializeField] private Junction[] junctions = Array.Empty<Junction>();
        [SerializeField] private Lane[] lanes = Array.Empty<Lane>();
        [SerializeField] private Rect sparseTrafficArea;
        [SerializeField] private int sparseTrafficMaximum;
        [SerializeField] private float sparseTrafficSpawnInterval=8;
        private List<int>[] departures;
        [NonSerialized] private Dictionary<(int, int), TrafficPolyline> turns;
        [NonSerialized] private bool[] sparseLanes;
        public IReadOnlyList<Junction> Junctions => junctions;
        public IReadOnlyList<Lane> Lanes => lanes;
        public int SparseTrafficMaximum=>sparseTrafficMaximum;
        public float SparseTrafficSpawnInterval=>sparseTrafficSpawnInterval;

        /// <summary>Caps ambient traffic in an authored neighbourhood, including its incoming gateway lanes.</summary>
        public void ConfigureSparseTraffic(Rect nativeArea,int maximum,float spawnInterval)
        {
            if(!float.IsFinite(nativeArea.xMin)||!float.IsFinite(nativeArea.xMax)||!float.IsFinite(nativeArea.yMin)||!float.IsFinite(nativeArea.yMax)||nativeArea.width<=0||nativeArea.height<=0||maximum<1||maximum>32||!float.IsFinite(spawnInterval)||spawnInterval<1)
                throw new ArgumentException("A finite area, positive capacity and spawn interval are required.");
            sparseTrafficArea=nativeArea;sparseTrafficMaximum=maximum;sparseTrafficSpawnInterval=spawnInterval;sparseLanes=null;
        }
        public bool IsSparseLane(int lane)
        {
            if(sparseTrafficMaximum==0)return false;
            if(lane<0||lane>=lanes.Length)throw new ArgumentOutOfRangeException(nameof(lane));
            // Unity's editor reload can restore an empty private array. This derived
            // cache must always match the authored network before runtime reads it.
            if(sparseLanes==null||sparseLanes.Length!=lanes.Length)
            {
                sparseLanes=new bool[lanes.Length];
                for(int i=0;i<lanes.Length;i++)
                    foreach(var p in lanes[i].points)if(sparseTrafficArea.Contains(new Vector2(p.x,p.z))){sparseLanes[i]=true;break;}
            }
            return sparseLanes[lane];
        }
        public bool IsSparseResidentLane(int lane)
        {
            if(!IsSparseLane(lane))return false;
            var a=junctions[lanes[lane].from].position;var b=junctions[lanes[lane].to].position;
            return sparseTrafficArea.Contains(new Vector2(a.x,a.z))&&sparseTrafficArea.Contains(new Vector2(b.x,b.z));
        }

        public void Configure(Junction[] nodes, Lane[] connections)
        {
            if (nodes == null || connections == null || nodes.Length < 2 || connections.Length < 2)
                throw new ArgumentException("A traffic network requires connected junctions and lanes.");
            foreach (var lane in connections)
                if (lane.from < 0 || lane.from >= nodes.Length || lane.to < 0 || lane.to >= nodes.Length || lane.from == lane.to || lane.Path.Length < 1)
                    throw new ArgumentException("Invalid directed lane.");
            junctions = nodes; lanes = connections; departures = null;sparseLanes=null;turns=null;
        }

        public IReadOnlyList<int> Departures(int node)
        {
            if (departures == null)
            {
                departures = new List<int>[junctions.Length];
                for (int i = 0; i < departures.Length; i++) departures[i] = new List<int>();
                for (int i = 0; i < lanes.Length; i++) departures[lanes[i].from].Add(i);
            }
            return departures[node];
        }

        public TrafficPolyline Turn(int incoming, int outgoing)
        {
            turns ??= new Dictionary<(int, int), TrafficPolyline>();
            if (turns.TryGetValue((incoming, outgoing), out var cached)) return cached;
            var a = lanes[incoming].Path;
            var b = lanes[outgoing].Path;
            if (lanes[incoming].to != lanes[outgoing].from) throw new ArgumentException("Turn lanes must share a junction.");
            var start = a.Sample(a.Length, out var arrival);
            var end = b.Sample(0, out var departure);
            // Cubic tangents keep the wheel direction continuous through the intersection.
            float control = Vector3.Distance(start, end) * .56f;
            var points = new Vector3[33];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / (float)(points.Length - 1), u = 1 - t;
                points[i] = u*u*u*start + 3*u*u*t*(start+arrival*control)
                    + 3*u*t*t*(end-departure*control) + t*t*t*end;
            }
            var path = new TrafficPolyline(points);
            turns.Add((incoming, outgoing), path);
            return path;
        }

        internal float TurnSpeed(int incoming, int outgoing)
        {
            var lane = lanes[incoming];
            var next = lanes[outgoing];
            lane.Path.Sample(lane.Path.Length, out var arrival);
            next.Path.Sample(0, out var departure);
            return Mathf.Min(Mathf.Min(lane.speedLimit, next.speedLimit),
                Mathf.Lerp(3.5f, 8f, Mathf.InverseLerp(.2f, .96f, Vector3.Dot(arrival, departure))));
        }
    }

    /// <summary>Distance-parametrised path: speed stays in metres/second even on densely sampled curves.</summary>
    public sealed class TrafficPolyline
    {
        private readonly Vector3[] points;
        private readonly float[] distances;
        public float Length => distances[distances.Length-1];
        public TrafficPolyline(Vector3[] samples)
        {
            if (samples == null || samples.Length < 2) throw new ArgumentException("Path needs two points.");
            points = (Vector3[])samples.Clone(); distances = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                if (!Finite(points[i])) throw new ArgumentException("Non-finite road point.");
                if (i > 0) distances[i] = distances[i-1] + Vector3.Distance(points[i-1], points[i]);
            }
            if (Length < .001f) throw new ArgumentException("Zero-length road.");
        }
        public Vector3 Sample(float distance, out Vector3 direction)
        {
            float d = Mathf.Clamp(distance, 0, Length);
            int index = Array.BinarySearch(distances, d);
            if (index < 0) index = ~index;
            index = Mathf.Clamp(index, 1, points.Length-1);
            while (index < points.Length-1 && distances[index]-distances[index-1] < .0001f) index++;
            var delta = points[index]-points[index-1];
            direction = delta.normalized;
            return Vector3.Lerp(points[index-1], points[index], Mathf.InverseLerp(distances[index-1], distances[index], d));
        }
        private static bool Finite(Vector3 p) => !float.IsNaN(p.x+p.y+p.z) && !float.IsInfinity(p.x+p.y+p.z);
    }
}
