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
        private List<int>[] departures;
        public IReadOnlyList<Junction> Junctions => junctions;
        public IReadOnlyList<Lane> Lanes => lanes;

        public void Configure(Junction[] nodes, Lane[] connections)
        {
            if (nodes == null || connections == null || nodes.Length < 2 || connections.Length < 2)
                throw new ArgumentException("A traffic network requires connected junctions and lanes.");
            foreach (var lane in connections)
                if (lane.from < 0 || lane.from >= nodes.Length || lane.to < 0 || lane.to >= nodes.Length || lane.from == lane.to || lane.Path.Length < 1)
                    throw new ArgumentException("Invalid directed lane.");
            junctions = nodes; lanes = connections; departures = null;
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
            var a = lanes[incoming].Path;
            var b = lanes[outgoing].Path;
            if (lanes[incoming].to != lanes[outgoing].from) throw new ArgumentException("Turn lanes must share a junction.");
            var start = a.Sample(a.Length, out var arrival);
            var end = b.Sample(0, out var departure);
            // Cubic tangents keep the wheel direction continuous through the intersection.
            float control = Vector3.Distance(start, end) * .56f;
            var points = new Vector3[19];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / (float)(points.Length - 1), u = 1 - t;
                points[i] = u*u*u*start + 3*u*u*t*(start+arrival*control)
                    + 3*u*t*t*(end-departure*control) + t*t*t*end;
            }
            return new TrafficPolyline(points);
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
