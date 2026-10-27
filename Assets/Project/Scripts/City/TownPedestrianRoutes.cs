using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Collision-checked sidewalk sections in the shared town's native coordinates.
    /// Sections stop before junction crossings; pedestrians never take a shortcut across a road.</summary>
    public sealed class TownPedestrianRoutes : ScriptableObject
    {
        [Serializable]
        public sealed class Route
        {
            public Vector3[] points=Array.Empty<Vector3>();
            [NonSerialized] private TrafficPolyline path;
            public TrafficPolyline Path=>path??=new TrafficPolyline(points);
        }
        [SerializeField] private Route[] routes=Array.Empty<Route>();
        public IReadOnlyList<Route> Routes=>routes;
        public void Configure(Route[] sections)
        {
            if(sections==null || sections.Length==0)throw new ArgumentException("At least one walkable sidewalk is required.");
            foreach(var section in sections)
                if(section==null || section.Path.Length<8)throw new ArgumentException("Sidewalk sections must be at least eight metres long.");
            routes=(Route[])sections.Clone();
        }
    }
}
