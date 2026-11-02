using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Logistics;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    public enum TownNavigationState { None, Following, Arrived, NoRoute }

    /// <summary>Road-following player GPS. Route planning is throttled; progress never removes the selected destination.</summary>
    public sealed class TownNavigation
    {
        private readonly TownMapData map;
        private readonly DriverRoadRoute planner;
        private readonly List<Vector3> points = new();
        private readonly List<float> distances = new();
        private readonly List<Vector3> remaining = new();
        private float progress, offRouteSince = -1, nextPlan;
        private Vector3 position, heading;
        public TownMapPoint Destination { get; private set; }
        public TownNavigationState State { get; private set; }
        public IReadOnlyList<Vector3> Route => remaining;
        public IReadOnlyList<Vector3> PlannedRoute => points;
        public float RemainingMetres { get; private set; }
        public int Revision { get; private set; }
        public int Replans { get; private set; }
        public string Instruction { get; private set; } = "Choose a destination";
        public TownNavigation(TownMapData map)
        { this.map = map ?? throw new ArgumentNullException(nameof(map)); if (map.Roads != null) planner = new DriverRoadRoute(map.Roads, true, 0); }
        public void Select(TownMapPoint destination)
        {
            Destination = destination ?? throw new ArgumentNullException(nameof(destination));
            Rebuild(0); nextPlan = 0;
        }
        public void SetWaypoint(Vector3 native)
        {
            if (planner == null) return;
            var road = planner.Nearest(native).Position;
            Select(new TownMapPoint("waypoint", "Custom waypoint", "Snapped to the nearest public road", TownMapCategory.Waypoint,
                TownMapSymbol.Pin, () => road));
        }
        public void Clear()
        {
            Destination = null; State = TownNavigationState.None; points.Clear(); remaining.Clear(); distances.Clear();
            RemainingMetres = 0; Instruction = "Choose a destination"; Revision++;
        }
        public void Tick(Vector3 nativePosition, Vector3 forward, float nativeSpeed, float clock)
        {
            nativePosition.y = 0; position = nativePosition; heading = forward;
            if (Destination == null) return;
            float arrivalRadius = Destination.Checkpoint != null ? Mathf.Clamp(Destination.Checkpoint.radius, 3, 8) : 6;
            var arrival = Destination.Access;
            if (arrival.Length == 0) { State = TownNavigationState.NoRoute; remaining.Clear(); RemainingMetres = 0; Instruction = "Clear the store driveway to reach this location"; return; }
            var arrivalPosition = arrival[arrival.Length - 1];
            if (Vector3.Distance(position, arrivalPosition) < arrivalRadius && nativeSpeed < 3)
            { State = TownNavigationState.Arrived; remaining.Clear(); RemainingMetres = 0; Instruction = "Arrived at " + Destination.Name; return; }
            if (State == TownNavigationState.Arrived)
            { if (Vector3.Distance(position, arrivalPosition) < 18) return; Rebuild(clock); }
            if (State == TownNavigationState.NoRoute)
            { if (clock >= nextPlan) Rebuild(clock); return; }
            if (points.Count < 2) return;
            float best = float.PositiveInfinity, at = progress; int segment = 0; Vector3 closest = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                if (distances[i] < progress - 8 || distances[i - 1] > progress + 70) continue;
                var delta = points[i] - points[i - 1]; float t = Mathf.Clamp01(Vector3.Dot(position - points[i - 1], delta) / Mathf.Max(.0001f, delta.sqrMagnitude));
                var p = points[i - 1] + t * delta; float distance = (p - position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; at = Mathf.Lerp(distances[i - 1], distances[i], t); segment = i; closest = p;
            }
            bool wrongWay = nativeSpeed > 1 && heading != Vector3.zero && segment > 0 && Vector3.Dot(heading.normalized, (points[segment] - points[segment - 1]).normalized) < -.65f;
            if (best > 18 * 18 || wrongWay)
            {
                if (offRouteSince < 0) offRouteSince = clock;
                if (clock - offRouteSince > 1.5f && clock >= nextPlan) { Rebuild(clock); return; }
            }
            else offRouteSince = -1;
            progress = Mathf.Max(progress - 2, at); RemainingMetres = Mathf.Max(0, distances[distances.Count - 1] - progress);
            remaining.Clear(); remaining.Add(closest);
            for (int i = segment; i < points.Count; i++) remaining.Add(points[i]);
            Instruction = NextInstruction(segment, progress); Revision++;
        }
        private void Rebuild(float clock)
        {
            points.Clear(); distances.Clear(); remaining.Clear(); progress = 0; offRouteSince = -1; nextPlan = clock + 2; Replans++;
            var access = Destination.Access;
            if (access.Length == 0) { State = TownNavigationState.NoRoute; Instruction = "Clear the store driveway to reach this location"; Revision++; return; }
            var origin = map.Points.Where(p => p.Access.Length > 1 && Vector3.Distance(p.Position, position) < 10)
                .OrderBy(p => Vector3.Distance(p.Position, position)).FirstOrDefault();
            var departure = origin != null ? origin.Access.Reverse().ToArray() : new[] { position };
            var start = departure[departure.Length - 1]; var end = access[0];
            bool insideStoreYard = origin != null && origin.Category == TownMapCategory.Store && Destination.Category == TownMapCategory.Store;
            var roadPoints = insideStoreYard ? map.StoreApproach(position, access[access.Length - 1]) : RoadRoute(start, end, origin != null);
            if (roadPoints == null || roadPoints.Length == 0)
            { State = TownNavigationState.NoRoute; Instruction = "No connected road route"; RemainingMetres = 0; Revision++; return; }
            void Add(Vector3 p) { p.y = 0; if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], p) > .05f) points.Add(p); }
            Add(position);
            if (!insideStoreYard) foreach (var p in departure.Skip(1)) Add(p);
            foreach (var p in roadPoints) Add(p);
            if (!insideStoreYard) foreach (var p in access) Add(p);
            if (points.Count < 2) Add(position + Vector3.forward * .1f);
            distances.Add(0); for (int i = 1; i < points.Count; i++) distances.Add(distances[i - 1] + Vector3.Distance(points[i - 1], points[i]));
            remaining.AddRange(points); RemainingMetres = distances[distances.Count - 1]; State = TownNavigationState.Following;
            Instruction = "Follow the route"; Revision++;
        }
        private Vector3[] RoadRoute(Vector3 start, Vector3 end, bool privateDeparture)
        {
            if (planner == null) return null;
            DriverRoadRoute.Stop[] Nearby(Vector3 p)
            {
                var stops = Enumerable.Range(0, map.Roads.Lanes.Count).Select(i => planner.NearestOnLane(i, p))
                    .OrderBy(s => (s.Position - p).sqrMagnitude).ToArray();
                float limit = Vector3.Distance(stops[0].Position, p) + 4;
                return stops.Where(s => Vector3.Distance(s.Position, p) <= limit).Take(4).ToArray();
            }
            // Off a public lane, either turn from the private driveway can be legal.
            // Once driving on a lane, retain the vehicle's heading and directed road rules.
            var starts = privateDeparture ? Nearby(start) : new[] { planner.Nearest(start, heading == Vector3.zero ? (Vector3?)null : heading) };
            var ends = Destination.Category == TownMapCategory.Waypoint ? new[] { planner.Nearest(end) } : Nearby(end);
            float best = float.PositiveInfinity; Vector3[] result = null;
            foreach (var a in starts) foreach (var b in ends)
            {
                if (!planner.Plan(a, b)) continue;
                var samples = planner.MapPoints(); float length = Vector3.Distance(start, a.Position) + Vector3.Distance(end, b.Position);
                for (int i = 1; i < samples.Length; i++) length += Vector3.Distance(samples[i - 1], samples[i]);
                if (length >= best) continue; best = length; result = samples;
            }
            return result;
        }
        private string NextInstruction(int segment, float travelled)
        {
            if (RemainingMetres < 25) return "Approach " + Destination.Name;
            // Compare headings across a bend instead of treating each small cubic sample as a turn.
            for (int i = Mathf.Max(1, segment); i < points.Count - 1; i++)
            {
                if (distances[i] - travelled > 180) break;
                int before = i - 1, after = i + 1;
                while (before > 0 && distances[i] - distances[before] < 7) before--;
                while (after < points.Count - 1 && distances[after] - distances[i] < 7) after++;
                float angle = Vector3.SignedAngle(points[i] - points[before], points[after] - points[i], Vector3.up);
                if (Mathf.Abs(angle) < 32) continue;
                return (angle > 0 ? "Turn right" : "Turn left") + " in " + FormatDistance(Mathf.Max(0, distances[i] - travelled));
            }
            return "Continue along the road";
        }
        public static string FormatDistance(float metres) => metres >= 1000 ? (metres / 1000).ToString("0.0") + " km" : Mathf.RoundToInt(metres) + " m";
    }
}
