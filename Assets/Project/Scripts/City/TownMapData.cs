using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Logistics;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    public enum TownMapCategory { Store, Supplies, Services, Landmark, Waypoint }
    public enum TownMapSymbol { Home, Cart, Leaf, Bread, Shelf, Hammer, Flower, Car, Unload, Fuel, Train, Park, Pin }

    /// <summary>A real destination and its authorised driveway, in the town's shared native coordinates.</summary>
    public sealed class TownMapPoint
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public TownMapCategory Category { get; }
        public TownMapSymbol Symbol { get; }
        public DeliveryCheckpoint Checkpoint { get; }
        private readonly Func<Vector3> position;
        private readonly Func<Vector3[]> access;
        public Vector3 Position => position();
        public Vector3[] Access => access();
        public TownMapPoint(string id, string name, string description, TownMapCategory category, TownMapSymbol symbol,
            Func<Vector3> position, Func<Vector3[]> access = null, DeliveryCheckpoint checkpoint = null)
        {
            Id = id; Name = name; Description = description; Category = category; Symbol = symbol;
            this.position = position ?? throw new ArgumentNullException(nameof(position));
            this.access = access ?? (() => new[] { position() }); Checkpoint = checkpoint;
        }
    }

    /// <summary>One cached, simplified map of the authored scenery; no render camera or per-frame hierarchy scans.</summary>
    public sealed class TownMapData : IDisposable
    {
        public readonly struct Road
        {
            public readonly Vector3[] Points;
            public readonly float Width;
            public Road(Vector3[] points, float width) { Points = points; Width = width; }
        }
        public TownWorldPlaces Town { get; }
        public TrafficRoadNetwork Roads { get; }
        public Rect Bounds { get; private set; }
        public IReadOnlyList<TownMapPoint> Points => points;
        public IReadOnlyList<Road> Streets => streets;
        public IReadOnlyList<Rect> Buildings => buildings;
        public IReadOnlyList<Vector3> Woodland => woodland;
        public IReadOnlyList<Rect> Fields => fields;
        private readonly List<Vector3> woodland = new();
        private readonly List<Rect> fields = new();
        public TownForestMapData Forest { get; }
        public TownMapPoint Home { get; private set; }
        public Rect? StoreFootprint { get; private set; }
        private readonly List<TownMapPoint> points = new();
        private readonly List<Road> streets = new();
        private readonly List<Rect> buildings = new();
        private readonly DeliveryOrders orders;
        private readonly Dictionary<SupplierKind, int> ready = new();
        private bool readyDirty = true;
        public event Action OrdersChanged;
        public TownMapData(TownWorldPlaces town, DeliveryOrders orders = null, Transform home = null,
            IEnumerable<DeliveryCheckpoint> unloading = null, Func<Vector3[]> homeAccess = null)
        {
            Town = town ?? throw new ArgumentNullException(nameof(town)); this.orders = orders;
            if (orders != null) orders.Changed += InvalidateOrders;
            Roads = town.GetComponentInChildren<TownRoadTraffic>(true)?.Network;
            Forest = town.GetComponentInChildren<TownForestMapData>(true);
            Bounds = town.layout.cityBounds;
            if (Forest != null) Include(Forest.DistrictBounds);
            if (Roads != null)
            {
                // Join lane endpoints through their real junctions; rural curves keep their authored samples.
                foreach (var lane in Roads.Lanes)
                {
                    var line = new List<Vector3> { Roads.Junctions[lane.from].position };
                    line.AddRange(lane.points); line.Add(Roads.Junctions[lane.to].position);
                    streets.Add(new Road(SimplifyForMap(line.ToArray(), .35f), 4.2f));
                }
                foreach (var node in Roads.Junctions) Include(node.position);
            }
            var courts = town.GetComponentsInChildren<SupplierLoadingCourt>(true);
            foreach (var checkpoint in town.suppliers.Where(p => p != null))
            {
                var stop = checkpoint; var court = courts.FirstOrDefault(c => c.checkpoint == stop);
                var kind = stop.supplier;
                var symbol = kind switch
                {
                    SupplierKind.Farmers => TownMapSymbol.Leaf, SupplierKind.Bakery => TownMapSymbol.Bread,
                    SupplierKind.Equipment => TownMapSymbol.Shelf, SupplierKind.Construction => TownMapSymbol.Hammer,
                    SupplierKind.Decoration => TownMapSymbol.Flower, SupplierKind.Showroom => TownMapSymbol.Car,
                    _ => TownMapSymbol.Cart
                };
                Vector3[] Access()
                {
                    if (court == null) return new[] { ToNative(stop.transform.position) };
                    return new[] { court.streetAccess, court.entrance }.Concat(court.approachWaypoints)
                        .Concat(new[] { court.staging, ToNative(stop.transform.position) }).ToArray();
                }
                points.Add(new TownMapPoint("supplier_" + kind, SupplierRouting.Name(kind), Description(kind),
                    kind == SupplierKind.Showroom ? TownMapCategory.Services : TownMapCategory.Supplies, symbol,
                    () => ToNative(stop.transform.position), Access, stop));
            }
            Vector3 HomePosition() => home != null ? ToNative(home.position) : town.layout.cityShopOrigin;
            Home = new TownMapPoint("my_store", "My store", "Your store and private vehicle parking", TownMapCategory.Store,
                TownMapSymbol.Home, HomePosition, () =>
                {
                    var route = homeAccess?.Invoke();
                    var entry = route != null && route.Length > 1 ? route[0] : town.layout.StoreStreetEntry;
                    var yard = route != null && route.Length > 1 ? route[route.Length - 2] : new Vector3(entry.x, 0, HomePosition().z);
                    var access = StoreApproach(yard, HomePosition());
                    return access.Length == 0 ? access : new[] { entry }.Concat(access).ToArray();
                });
            points.Insert(0, Home);
            if (unloading != null) foreach (var checkpoint in unloading.Where(p => p != null && p.isActiveAndEnabled))
            {
                var gate = checkpoint; int number = points.Count(p => p.Symbol == TownMapSymbol.Unload) + 1;
                Vector3 Position() => ToNative(gate.transform.position);
                points.Add(new TownMapPoint("unload_" + number, "Store unloading" + (number > 1 ? " " + number : ""),
                    "Reverse to the loading door to deliver cargo", TownMapCategory.Store, TownMapSymbol.Unload, Position,
                    () =>
                    {
                        var route = homeAccess?.Invoke();
                        var entry = route != null && route.Length > 1 ? route[0] : town.layout.StoreStreetEntry;
                        var yard = route != null && route.Length > 1 ? route[route.Length - 2] : entry;
                        var access = StoreApproach(yard, Position());
                        return access.Length == 0 ? access : new[] { entry }.Concat(access).ToArray();
                    }, gate));
            }
            // These are orientation landmarks, not invented interactable suppliers.
            AddLandmark("fuel", "Gas station", "Roadside landmark", "FuelShopDetailed", TownMapSymbol.Fuel);
            AddLandmark("station", "Railway station", "Passenger station", "Railway station", TownMapSymbol.Train);
            var workshop = town.GetComponentsInChildren<GarageDoor>(true).FirstOrDefault(door => door.name == "Door_2");
            if (workshop != null)
            {
                Vector3 At(float depth) => ToNative(workshop.transform.TransformPoint(0, 0, depth));
                var destination = At(3);
                var street = At(25);
                var entrance = At(17);
                points.Add(new TownMapPoint("auto_works", "AUTO WORKS", "Vehicle tuning and custom paint",
                    TownMapCategory.Services, TownMapSymbol.Car, () => destination, () => new[] { street, entrance, destination }));
            }
            if (Forest != null) points.Add(new TownMapPoint("pinebrook_park", "Brookside Park", "Pinebrook public park",
                TownMapCategory.Landmark, TownMapSymbol.Park, () => new Vector3(108, 0, 386),
                () => new[] { new Vector3(20, 0, 376) }));
            foreach (var point in points) Include(point.Position);
            Bounds = new Rect(Bounds.xMin - 24, Bounds.yMin - 24, Bounds.width + 48, Bounds.height + 48);
            foreach (var box in town.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger || !box.enabled) continue;
                var b = box.bounds; var size = b.size / town.transform.lossyScale.x;
                if(box.name.ToLowerInvariant().Contains("field")&&size.x>12&&size.z>12&&size.x<180&&size.z<180)
                {var fieldMin=ToNative(b.min);var fieldMax=ToNative(b.max);fields.Add(Rect.MinMaxRect(fieldMin.x,fieldMin.z,fieldMax.x,fieldMax.z));}
                if (size.y < 3.5f || size.x < 5 || size.z < 4 || size.x > 90 || size.z > 90) continue;
                var min = ToNative(b.min); var max = ToNative(b.max);
                buildings.Add(Rect.MinMaxRect(min.x, min.z, max.x, max.z));
            }
            var trees = new HashSet<Vector2Int>();
            var cropPlots=new Dictionary<string,List<Vector3>>();
            foreach(var node in town.GetComponentsInChildren<Transform>(true))
            {
                if(node.name=="FieldWheatClump"||node.name=="FieldLeafClump"||node.name=="FieldRootClump")
                {if(!cropPlots.TryGetValue(node.name,out var crop))cropPlots.Add(node.name,crop=new List<Vector3>());crop.Add(ToNative(node.position));}
                Vector2 size=node.name switch {"PackingBarn"=>new Vector2(28,20),"RedBarn"=>new Vector2(16,18),"FarmHouse"=>new Vector2(12,10),"Workshop"=>new Vector2(18,15),"Greenhouse"=>new Vector2(9,18),_=>Vector2.zero};
                if(size!=Vector2.zero){var p=ToNative(node.position);if(p.x>450)buildings.Add(new Rect(p.x-size.x/2,p.z-size.y/2,size.x,size.y));}
            }
            foreach(var plot in cropPlots.Values)if(plot.Count>10)
                fields.Add(Rect.MinMaxRect(plot.Min(p=>p.x)-1,plot.Min(p=>p.z)-1,plot.Max(p=>p.x)+1,plot.Max(p=>p.z)+1));
            foreach(var renderer in town.GetComponentsInChildren<MeshRenderer>(true))
            {
                var name=renderer.name.ToLowerInvariant();var bounds=renderer.bounds;
                var size=bounds.size/town.transform.lossyScale.x;var center=ToNative(bounds.center);
                if(name.Contains("tree")&&size.x>2&&size.x<35&&size.z<35)
                {var cell=new Vector2Int(Mathf.RoundToInt(center.x/24),Mathf.RoundToInt(center.z/24));if(trees.Add(cell)&&!streets.Any(r=>r.Points.Any(p=>(p-center).sqrMagnitude<32*32))&&!buildings.Any(r=>r.Contains(new Vector2(center.x,center.z))))woodland.Add(new Vector3(cell.x*24,0,cell.y*24));}
                if((name.Contains("field")||name.Contains("crop"))&&size.x>12&&size.z>12&&size.x<180&&size.z<180)
                {var min=ToNative(bounds.min);var max=ToNative(bounds.max);fields.Add(Rect.MinMaxRect(min.x,min.z,max.x,max.z));}
                if((name.Contains("barn")||name.Contains("greenhouse")||name.Contains("farmshop"))&&size.y>3&&size.x>5&&size.z>4&&size.x<80&&size.z<80)
                {var min=ToNative(bounds.min);var max=ToNative(bounds.max);buildings.Add(Rect.MinMaxRect(min.x,min.z,max.x,max.z));}
            }
        }
        public Vector3 ToNative(Vector3 world) { var p = Town.transform.InverseTransformPoint(world); p.y = 0; return p; }
        public void SetStoreFootprint(Rect? bounds)
        {
            if (bounds.HasValue && (!float.IsFinite(bounds.Value.x) || !float.IsFinite(bounds.Value.y) || bounds.Value.width <= 0 || bounds.Value.height <= 0))
                throw new ArgumentException("A finite positive store footprint is required.");
            StoreFootprint = bounds;
        }
        internal Vector3[] StoreApproach(Vector3 from, Vector3 to)
        {
            if (!StoreFootprint.HasValue) return new[] { from, to };
            var body = StoreFootprint.Value;
            var clear = Rect.MinMaxRect(body.xMin - 1.6f, body.yMin - 1.6f, body.xMax + 1.6f, body.yMax + 1.6f);
            // Visibility graph around the actual store outline: a rear gate must never
            // be reached by drawing the final driveway straight through the shop.
            var nodes = new[] { from, to, new Vector3(clear.xMin, 0, clear.yMin), new Vector3(clear.xMin, 0, clear.yMax), new Vector3(clear.xMax, 0, clear.yMax), new Vector3(clear.xMax, 0, clear.yMin) };
            bool Blocked(Vector3 a, Vector3 b)
            {
                var inside = Rect.MinMaxRect(clear.xMin + .01f, clear.yMin + .01f, clear.xMax - .01f, clear.yMax - .01f);
                float enter = 0, leave = 1; var d = b - a;
                bool Slab(float start, float delta, float min, float max)
                {
                    if (Mathf.Abs(delta) < .0001f) return start >= min && start <= max;
                    float x = (min - start) / delta, y = (max - start) / delta;
                    enter = Mathf.Max(enter, Mathf.Min(x, y)); leave = Mathf.Min(leave, Mathf.Max(x, y)); return enter <= leave;
                }
                return Slab(a.x, d.x, inside.xMin, inside.xMax) && Slab(a.z, d.z, inside.yMin, inside.yMax) && leave > .001f && enter < .999f;
            }
            var costs = Enumerable.Repeat(float.PositiveInfinity, nodes.Length).ToArray(); var previous = Enumerable.Repeat(-1, nodes.Length).ToArray(); var visited = new bool[nodes.Length]; costs[0] = 0;
            for (int step = 0; step < nodes.Length; step++)
            {
                int current = -1; for (int i = 0; i < nodes.Length; i++) if (!visited[i] && (current < 0 || costs[i] < costs[current])) current = i;
                if (current < 0 || float.IsPositiveInfinity(costs[current])) break;
                if (current == 1)
                { var path = new List<Vector3>(); for (int i = current; i >= 0; i = previous[i]) path.Insert(0, nodes[i]); return path.ToArray(); }
                visited[current] = true;
                for (int i = 0; i < nodes.Length; i++)
                {
                    if (i == current || visited[i] || Blocked(nodes[current], nodes[i])) continue;
                    float cost = costs[current] + Vector3.Distance(nodes[current], nodes[i]); if (cost >= costs[i]) continue;
                    costs[i] = cost; previous[i] = current;
                }
            }
            return Array.Empty<Vector3>();
        }
        private static Vector3[] SimplifyForMap(Vector3[] source, float tolerance)
        {
            // Preserve the road outline, not the simulation's 10,000 dense samples.
            // Maximum deviation is 35 cm, below a pixel in the world overview.
            if (source.Length < 3) return source;
            var keep = new bool[source.Length]; keep[0] = keep[source.Length - 1] = true;
            var ranges = new Stack<(int Start, int End)>(); ranges.Push((0, source.Length - 1));
            while (ranges.Count > 0)
            {
                var range = ranges.Pop(); var a = source[range.Start]; var delta = source[range.End] - a;
                float best = tolerance * tolerance; int split = -1;
                for (int i = range.Start + 1; i < range.End; i++)
                {
                    float t = Mathf.Clamp01(Vector3.Dot(source[i] - a, delta) / Mathf.Max(.000001f, delta.sqrMagnitude));
                    float error = (source[i] - a - delta * t).sqrMagnitude;
                    if (error <= best) continue; best = error; split = i;
                }
                if (split < 0) continue;
                keep[split] = true; ranges.Push((range.Start, split)); ranges.Push((split, range.End));
            }
            return source.Where((p, i) => keep[i]).ToArray();
        }
        public int ReadyUnits(TownMapPoint point)
        {
            if (orders == null || point.Checkpoint == null || point.Category != TownMapCategory.Supplies) return 0;
            if (readyDirty)
            {
                ready.Clear();
                foreach (var entry in orders.Ledger.Entries)
                {
                    if (entry.location != DeliveryLocation.Depot) continue;
                    var supplier = orders.SupplierFor(entry); ready.TryGetValue(supplier, out int quantity); ready[supplier] = quantity + entry.quantity;
                }
                readyDirty = false;
            }
            return ready.TryGetValue(point.Checkpoint.supplier, out int count) ? count : 0;
        }
        private void InvalidateOrders() { readyDirty = true; OrdersChanged?.Invoke(); }
        public void Dispose() { if (orders != null) orders.Changed -= InvalidateOrders; }
        private void AddLandmark(string id, string name, string description, string objectName, TownMapSymbol symbol)
        {
            var anchor = Town.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == objectName);
            if (anchor == null) return;
            // Several legacy buildings bake native coordinates into their mesh and keep
            // a zero transform. Use the actual visible footprint, never that container origin.
            var renderers = anchor.GetComponentsInChildren<Renderer>(true);
            var filters = anchor.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
            var colliders = anchor.GetComponentsInChildren<Collider>(true);
            Vector3 center = anchor.position;
            if (renderers.Length > 0)
            { var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); center = bounds.center; }
            else if (filters.Length > 0)
            {
                var bounds = new UnityEngine.Bounds(filters[0].transform.TransformPoint(filters[0].sharedMesh.bounds.center), Vector3.zero);
                foreach (var filter in filters) bounds.Encapsulate(filter.transform.TransformPoint(filter.sharedMesh.bounds.center)); center = bounds.center;
            }
            else if (colliders.Length > 0)
            { var bounds = colliders[0].bounds; foreach (var collider in colliders.Skip(1)) bounds.Encapsulate(collider.bounds); center = bounds.center; }
            var position = ToNative(center);
            var approach = Roads != null ? new DriverRoadRoute(Roads, stopMargin: 0).Nearest(position).Position : position;
            points.Add(new TownMapPoint(id, name, description, TownMapCategory.Landmark, symbol, () => position, () => new[] { approach }));
        }
        private void Include(Vector3 p) => Include(new Rect(p.x, p.z, .01f, .01f));
        private void Include(Rect b) => Bounds = Rect.MinMaxRect(Mathf.Min(Bounds.xMin, b.xMin), Mathf.Min(Bounds.yMin, b.yMin), Mathf.Max(Bounds.xMax, b.xMax), Mathf.Max(Bounds.yMax, b.yMax));
        private static string Description(SupplierKind supplier) => supplier switch
        {
            SupplierKind.Farmers => "Fresh fruit and vegetables", SupplierKind.Bakery => "Bread, bakery and desserts",
            SupplierKind.Equipment => "Shelves, displays and checkout equipment", SupplierKind.Construction => "Walls, floors and floor connections",
            SupplierKind.Decoration => "Plants and store decorations", SupplierKind.Showroom => "Buy or exchange your delivery vehicle",
            _ => "Packaged food, chilled goods and household supplies"
        };
    }
}
