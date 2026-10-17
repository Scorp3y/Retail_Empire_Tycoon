using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;

/// <summary>Acceptance of authored residential parcels, actual wing collisions and their public access.</summary>
internal static class TownResidentialValidation
{
    private const string ReportFolder = "Library/TownResidentialQA";
    private const float Tolerance = .005f;
    private const float LongitudinalSample = .5f;

    /// <summary>
    /// Reads the saved town without changing assets. Routes are checked against actual saved collision shapes,
    /// not Physics queries in a preview scene, whose collision world is independent of the live City scene.
    /// </summary>
    public static void ValidateSaved(TownResidentialPlan plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        TownWorldSafety.RequireIsolatedEditor();
        TownPilotScopePreservation.ValidateMultiAreaContracts();
        var log = new StringBuilder();
        Directory.CreateDirectory(ReportFolder);
        var root = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        bool passed = false;
        try
        {
            var residential = root.transform.Find(TownResidentialPlan.HierarchyRoot);
            Require(residential != null, "Saved residential placement hierarchy is missing.");
            Require(root.transform.position == Vector3.zero && root.transform.rotation == Quaternion.identity
                && root.transform.lossyScale == Vector3.one, "Town no longer uses its native metre coordinate frame.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(TownArchitecturePalette.MaterialPath);
            Require(material != null, "The shared architecture atlas material is missing.");
            TownArchitectureValidation.ValidateMaterial(material);
            var obstacles = root.GetComponentsInChildren<Collider>(true)
                .Where(collider => collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy)
                .Select(collider => new SavedObstacle(collider)).ToArray();
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            var allBodies = new List<NamedBody>();
            int buildings = 0, wings = 0, entrances = 0, routes = 0;
            foreach (var block in plan.Blocks)
            {
                Require(block != null && identifiers.Add(block.Id), "Missing or duplicate residential block ID.");
                Require(block.Bounds.width > 0 && block.Bounds.height > 0 && Finite(block.Bounds), block.Id + ": invalid parcel bounds.");
                var blockRoot = residential.Find(block.Id);
                Require(blockRoot != null, block.Id + ": saved parcel is missing.");
                float area = 0;
                var heights = new HashSet<int>();
                var families = new HashSet<string>(StringComparer.Ordinal);
                foreach (var building in block.Buildings)
                {
                    Require(building != null && identifiers.Add(building.Id), block.Id + ": missing or duplicate building ID.");
                    var authored = blockRoot.Find(building.Id);
                    Require(authored != null, building.Id + ": saved building is missing.");
                    ValidateBuilding(building, authored, material, block.Bounds, allBodies);
                    area += building.Model.FootprintArea;
                    foreach (var volume in building.Model.Volumes) heights.Add(Mathf.RoundToInt(volume.BodyBounds.size.y * 100));
                    families.Add(building.Family);
                    buildings++;
                    wings += building.WorldBodies.Length;
                    entrances += building.WorldEntrances.Length;
                }
                float coverage = area / (block.Bounds.width * block.Bounds.height);
                Require(coverage >= .35f - .001f && coverage <= .55f + .001f,
                    block.Id + ": residential coverage must be 35–55%; actual " + (coverage * 100).ToString("F2") + "%. ");
                Require(heights.Count >= 3, block.Id + ": at least three distinct body heights are required, not cloned uniform-height buildings.");
                Require(families.Count >= 3, block.Id + ": at least three building families are required.");
                Require(block.Walkways.Count > 0, block.Id + ": no authored access paths.");
                ValidateSavedPaths(block, blockRoot);
                foreach (var walkway in block.Walkways)
                {
                    ValidateWalkway(walkway, block.Id);
                    CheckSavedRoute(obstacles, walkway);
                    routes++;
                }
                ValidateWalkingNetwork(block);
                ValidateEntranceConnections(block, obstacles);
                foreach (var approach in block.VehicleApproaches)
                {
                    ValidateWalkway(approach, block.Id);
                    Require(approach.Width >= 6, block.Id + ": vehicle approaches need at least six metres of clear width.");
                    CheckSavedRoute(obstacles, approach);
                }
                log.AppendLine($"{block.Id}: {block.Buildings.Count} buildings, footprint {area:F1} m2, coverage {coverage * 100:F2}%, "
                    + $"{families.Count} families, {heights.Count} body heights, {block.Walkways.Count} access segments.");
            }
            for (int first = 0; first < allBodies.Count; first++)
                for (int second = first + 1; second < allBodies.Count; second++)
                    Require(!OverlapXZ(allBodies[first].Bounds, allBodies[second].Bounds),
                        "Residential solid wings overlap: " + allBodies[first].Name + " / " + allBodies[second].Name);
            ValidateFacadeDetailClearance(plan, allBodies);
            foreach (var body in allBodies)
                foreach (var obstacle in obstacles.Where(obstacle => !obstacle.Collider.transform.IsChildOf(residential)
                    && obstacle.Bounds.max.y > .3f && obstacle.Bounds.min.y < body.Bounds.max.y))
                    Require(!OverlapXZ(body.Bounds, obstacle.Bounds),
                        body.Name + ": new solid wing intersects a preserved town object " + obstacle.Name + ".");
            foreach (var district in plan.Blocks.GroupBy(block => block.District))
            {
                var families = district.SelectMany(block => block.Buildings).Select(building => building.Family).Distinct().ToArray();
                Require(families.Length >= 5, district.Key + ": residential district must contain at least five architectural families.");
                var landmark = district.SelectMany(block => block.Buildings)
                    .Where(building => building.ModelId == "Stepped469" || building.ModelId == "LCorner5" || building.ModelId == "UCourtyard4")
                    .OrderByDescending(building => building.Model.Volumes.Max(volume => volume.Floors)).FirstOrDefault();
                Require(landmark != null, district.Key + ": missing a distinct architectural massing candidate for a district landmark.");
                log.AppendLine("District " + district.Key + ": " + string.Join(", ", families) + "; architectural landmark candidate "
                    + landmark.Id + " (" + landmark.ModelId + "); recognisability remains a visual acceptance check.");
            }
            int renderers = root.GetComponentsInChildren<Renderer>(true).Length;
            int triangles = root.GetComponentsInChildren<MeshFilter>(true).Sum(filter =>
                filter.sharedMesh != null ? filter.sharedMesh.triangles.Length / 3 : 0);
            Require(renderers < 480 && triangles < 1400000,
                "World geometry budget exceeded: " + renderers + " renderers, " + triangles + " triangles.");
            Require(root.GetComponentsInChildren<TownAmbientWalker>(true).Length == 0,
                "Removed decorative townspeople returned to the city.");
            log.AppendLine($"PASS {buildings} saved buildings / {wings} individual solid wings / {entrances} entrances / {routes} full-width paths.");
            log.AppendLine($"PASS shared architecture material, native metre poses, open L/U courts, renderer budget {renderers}/480, triangles {triangles}/1400000.");
            log.AppendLine("PASS multi-parcel scope contracts: spanning triangles/colliders remain protected; differing scopes cannot be compared.");
            passed = true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            File.WriteAllText(ReportFolder + "/assets.txt", (passed ? "PASS\n" : "FAILED\n") + log);
        }
    }

    private static void ValidateBuilding(TownResidentialBuilding building, Transform authored, Material material,
        Rect parcel, List<NamedBody> bodies)
    {
        building.Model.Validate();
        Require(authored.localScale == Vector3.one && (authored.position - building.Position).sqrMagnitude < Tolerance * Tolerance
            && Quaternion.Angle(authored.rotation, Quaternion.Euler(0, building.Yaw, 0)) < Tolerance,
            building.Id + ": authored placement altered metre scale, pivot or quarter-turn orientation.");
        Require(Mathf.Abs(Mathf.DeltaAngle(building.Yaw, Mathf.Round(building.Yaw / 90) * 90)) < Tolerance,
            building.Id + ": collision validation supports native quarter-turn placements only.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(TownArchitecturePalette.Root + "/Prefabs/" + building.ModelId + ".prefab");
        Require(source != null, building.Id + ": native architecture prefab is missing.");
        var sourceMesh = source.GetComponent<MeshFilter>().sharedMesh;
        var filters = authored.GetComponentsInChildren<MeshFilter>(true);
        var renderers = authored.GetComponentsInChildren<MeshRenderer>(true);
        Require(filters.Length == 1 && filters[0].sharedMesh == sourceMesh && renderers.Length == 1
            && renderers[0].sharedMaterials.Length == 1 && renderers[0].sharedMaterial == material,
            building.Id + ": original native mesh/shared architecture atlas must remain intact.");
        Require(authored.GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
            building.Id + ": decorative architecture must not introduce new gameplay or pickup behaviours.");
        var colliders = authored.GetComponentsInChildren<Collider>(true);
        var expected = building.WorldBodies;
        Require(colliders.Length == expected.Length && colliders.All(collider => collider is BoxCollider
            && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy),
            building.Id + ": each solid wing needs exactly one active non-trigger box; enclosing courtyard boxes are forbidden.");
        var actual = colliders.Select(collider => BoundsOf(collider)).ToList();
        for (int wing = 0; wing < expected.Length; wing++)
        {
            Bounds body = expected[wing];
            Require(Contains(parcel, body), building.Id + ": solid wing leaves its authorised parcel.");
            Require(Mathf.Abs(body.min.y) <= Tolerance, building.Id + ": building floats or sinks relative to its native ground.");
            int match = actual.FindIndex(candidate => (candidate.center - body.center).sqrMagnitude < Tolerance * Tolerance
                && (candidate.size - body.size).sqrMagnitude < Tolerance * Tolerance);
            Require(match >= 0, building.Id + ": saved collision does not match the real solid wing " + wing + ".");
            actual.RemoveAt(match);
            bodies.Add(new NamedBody(building.Id + "/wing" + wing, body));
        }
        foreach (var opening in building.Model.CourtyardOpenings)
        {
            Bounds local = new Bounds(new Vector3(opening.center.x, 1, opening.center.y), new Vector3(opening.width, 2, opening.height));
            var transformed = TransformBounds(building.Pose, local);
            Require(!expected.Any(body => OverlapXZ(body, transformed)), building.Id + ": authored collider fills the open courtyard.");
        }
    }

    private static void ValidateFacadeDetailClearance(TownResidentialPlan plan, IReadOnlyList<NamedBody> bodies)
    {
        var meshes = new Dictionary<string, Mesh>(StringComparer.Ordinal);
        foreach (var building in plan.Blocks.SelectMany(block => block.Buildings))
        {
            if (!meshes.TryGetValue(building.ModelId, out var mesh))
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(TownArchitecturePalette.Root + "/Prefabs/" + building.ModelId + ".prefab");
                mesh = source.GetComponent<MeshFilter>().sharedMesh;
                meshes.Add(building.ModelId, mesh);
            }
            var envelope = TransformBounds(building.Pose, mesh.bounds);
            var neighbours = bodies.Where(body => !body.Name.StartsWith(building.Id + "/", StringComparison.Ordinal)
                && OverlapXZ(envelope, body.Bounds)).ToArray();
            if (neighbours.Length == 0) continue;
            foreach (var local in mesh.vertices)
            {
                Vector3 point = building.Pose.MultiplyPoint3x4(local);
                foreach (var neighbour in neighbours)
                {
                    var body = neighbour.Bounds;
                    bool strictlyInside = point.x > body.min.x + Tolerance && point.x < body.max.x - Tolerance
                        && point.z > body.min.z + Tolerance && point.z < body.max.z - Tolerance
                        && point.y > body.min.y + Tolerance && point.y < body.max.y - Tolerance;
                    Require(!strictlyInside, building.Id + ": a real facade/entrance/roof detail intersects neighbour " + neighbour.Name + ".");
                }
            }
        }
    }

    private static void ValidateEntranceConnections(TownResidentialBlock block, SavedObstacle[] obstacles)
    {
        foreach (var building in block.Buildings)
        {
            var entrances = building.Model.Entrances;
            for (int index = 0; index < entrances.Length; index++)
            {
                Vector3 outward = building.Pose.MultiplyVector(entrances[index].Outward).normalized;
                Vector3 entry = building.WorldEntrances[index] + outward * 1.3f;
                Require(block.Walkways.Any(route => DistanceToSegment(entry, route.From, route.To) < route.Width / 2 + .05f),
                    building.Id + ": entrance " + index + " has no connection to an authored pedestrian path.");
                CheckSavedRoute(obstacles, new TownResidentialWalkway(building.Id + "/door" + index,
                    entry, entry + outward * .1f, 2.2f));
            }
        }
    }

    private static void ValidateSavedPaths(TownResidentialBlock block, Transform blockRoot)
    {
        var markers = blockRoot.Find("Walkways");
        Require(markers != null && markers.childCount == block.Walkways.Count,
            block.Id + ": saved pedestrian route markers differ from the authored plan.");
        var ground = blockRoot.Find("Residential ground and walkways");
        Require(ground != null && ground.GetComponent<MeshFilter>() != null && ground.GetComponent<MeshFilter>().sharedMesh != null,
            block.Id + ": authored pedestrian surface is missing.");
        var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
        var positions = mesh.vertices.Select(ground.TransformPoint).ToArray();
        var uv = mesh.uv;
        var pavingUv = TownPalette.UV(TownColor.Pavement);
        var indices = mesh.triangles;
        var paving = new List<Vector3[]>();
        Require(uv.Length == positions.Length, block.Id + ": pedestrian surface lost its atlas coordinates.");
        for (int triangle = 0; triangle < indices.Length; triangle += 3)
            if ((uv[indices[triangle]] - pavingUv).sqrMagnitude < .000001f
                && (uv[indices[triangle + 1]] - pavingUv).sqrMagnitude < .000001f
                && (uv[indices[triangle + 2]] - pavingUv).sqrMagnitude < .000001f)
                paving.Add(new[] { positions[indices[triangle]], positions[indices[triangle + 1]], positions[indices[triangle + 2]] });
        foreach (var route in block.Walkways)
        {
            var marker = markers.Find(route.Id);
            var from = marker != null ? marker.Find("From") : null;
            var to = marker != null ? marker.Find("To") : null;
            Require(from != null && to != null && (from.position - route.From).sqrMagnitude < Tolerance * Tolerance
                && (to.position - route.To).sqrMagnitude < Tolerance * Tolerance,
                route.Id + ": persisted route endpoints differ from the authored route.");
            var side = Vector3.Cross(Vector3.up, (route.To - route.From).normalized);
            foreach (float fraction in new[] { .05f, .5f, .95f })
                foreach (float offset in new[] { -route.Width / 2 + .025f, 0, route.Width / 2 - .025f })
                {
                    Vector3 point = Vector3.Lerp(route.From, route.To, fraction) + side * offset;
                    Require(paving.Any(triangle => OnPaving(point, triangle)),
                        route.Id + ": saved pavement does not actually cover its full declared width at " + point + ".");
                }
        }
    }

    private static bool OnPaving(Vector3 point, Vector3[] triangle)
    {
        var a = new Vector2(triangle[0].x, triangle[0].z);
        var b = new Vector2(triangle[1].x, triangle[1].z);
        var c = new Vector2(triangle[2].x, triangle[2].z);
        var p = new Vector2(point.x, point.z);
        float cross(Vector2 first, Vector2 second) => first.x * second.y - first.y * second.x;
        float area = cross(b - a, c - a);
        if (Mathf.Abs(area) < .00001f) return false;
        float second = cross(p - a, c - a) / area, third = cross(b - a, p - a) / area;
        if (second < -.001f || third < -.001f || second + third > 1.001f) return false;
        float height = triangle[0].y + second * (triangle[1].y - triangle[0].y) + third * (triangle[2].y - triangle[0].y);
        return height >= .08f && height <= .2f;
    }

    private static void ValidateWalkingNetwork(TownResidentialBlock block)
    {
        var routes = block.Walkways;
        var connected = new HashSet<int>();
        var queue = new Queue<int>();
        for (int index = 0; index < routes.Count; index++)
            if (NearPublicEdge(routes[index].From, block.Bounds) || NearPublicEdge(routes[index].To, block.Bounds))
                queue.Enqueue(index);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (!connected.Add(current)) continue;
            for (int other = 0; other < routes.Count; other++)
            {
                if (connected.Contains(other)) continue;
                float joinDistance = Mathf.Min(routes[current].Width, routes[other].Width) / 2 + .01f;
                if (DistanceToSegment(routes[current].From, routes[other].From, routes[other].To) <= joinDistance
                    || DistanceToSegment(routes[current].To, routes[other].From, routes[other].To) <= joinDistance
                    || DistanceToSegment(routes[other].From, routes[current].From, routes[current].To) <= joinDistance
                    || DistanceToSegment(routes[other].To, routes[current].From, routes[current].To) <= joinDistance)
                    queue.Enqueue(other);
            }
        }
        Require(connected.Count == routes.Count, block.Id + ": a pedestrian route is disconnected from the public parcel-edge sidewalks.");
    }

    private static bool NearPublicEdge(Vector3 point, Rect parcel) => Mathf.Abs(point.x - parcel.xMin) <= 2.5f
        || Mathf.Abs(point.x - parcel.xMax) <= 2.5f || Mathf.Abs(point.z - parcel.yMin) <= 2.5f || Mathf.Abs(point.z - parcel.yMax) <= 2.5f;

    private static void ValidateWalkway(TownResidentialWalkway route, string blockId)
    {
        Require(route != null && !string.IsNullOrWhiteSpace(route.Id) && Finite(route.From) && Finite(route.To)
            && Finite(route.Width) && route.Width >= 2.2f && Mathf.Abs(route.From.y) < Tolerance && Mathf.Abs(route.To.y) < Tolerance
            && Vector3.Distance(route.From, route.To) > .001f, blockId + ": invalid metre-sized public access route.");
    }

    private static void CheckSavedRoute(SavedObstacle[] obstacles, TownResidentialWalkway route)
    {
        var forward = (route.To - route.From).normalized;
        var side = Vector3.Cross(Vector3.up, forward);
        int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(route.From, route.To) / LongitudinalSample));
        for (int sample = 0; sample <= samples; sample++)
        {
            Vector3 centre = Vector3.Lerp(route.From, route.To, (float)sample / samples) + Vector3.up;
            foreach (var obstacle in obstacles)
                Require(!OverlapsPassage(centre, side, forward, route.Width / 2, .28f, obstacle.Bounds),
                    route.Id + ": full-width pedestrian access is blocked at " + centre + " by " + obstacle.Name + ".");
        }
    }

    /// <summary>
    /// Checks live collision clearance and drives the existing pickup along two protected streets.
    /// The full store/supplier regression remains TownWorldValidation's responsibility. Vehicle/camera state is restored.
    /// </summary>
    public static IEnumerator Check(CityWorld city, TownResidentialPlan plan)
    {
        TownWorldSafety.RequireIsolatedEditor();
        Require(Application.isPlaying && city != null && city.town != null && city.pickup != null
            && city.pickup.Body != null && city.chaseCamera != null, "Residential runtime acceptance requires the live City scene.");
        Require(plan != null && Mathf.Abs(Time.timeScale - 1) < .001f, "Residential driving acceptance requires the plan and normal unpaused time.");
        var pickup = city.pickup;
        TownResidentialRoutes.ReadSaved(plan, city.town.transform);
        var camera = city.chaseCamera;
        var body = pickup.Body;
        Vector3 savedPosition = body.position, savedVelocity = body.velocity, savedAngular = body.angularVelocity;
        Quaternion savedRotation = body.rotation, savedCameraRotation = camera.transform.rotation;
        Vector3 savedCameraPosition = camera.transform.position;
        float savedYaw = camera.Yaw, savedPitch = camera.Pitch, savedDistance = camera.distance;
        bool savedKeyboard = pickup.ReadKeyboard, savedInput = pickup.InputEnabled, savedMouse = camera.ReadMouse;
        bool passed = false;
        var log = new StringBuilder();
        Directory.CreateDirectory(ReportFolder);
        File.WriteAllText(ReportFolder + "/runtime.txt", "RUNNING\n");
        try
        {
            pickup.ReadKeyboard = false;
            pickup.InputEnabled = true;
            camera.ReadMouse = false;
            pickup.Stop();
            Physics.SyncTransforms();
            int paths = 0;
            foreach (var block in plan.Blocks)
                foreach (var route in block.Walkways)
                {
                    CheckLiveRoute(city, route);
                    paths++;
                }
            Require(paths > 0, "Residential runtime plan lost its persisted pedestrian routes.");
            log.AppendLine("PASS " + paths + " live full-width pedestrian routes; sampled every 0.5 m with overlapping boxes.");
            foreach (var route in new[]
            {
                new TownResidentialWalkway("north street", new Vector3(-104, 0, 192), new Vector3(-53, 0, 192), 6),
                new TownResidentialWalkway("river-side city street", new Vector3(264, 0, 65), new Vector3(264, 0, 116), 6)
            })
            {
                CheckLiveVehicleRoute(city, route);
                yield return DriveStraight(city, route);
                log.AppendLine("PASS real native pickup drove >=51 m along " + route.Id + "; no blockage, uncontrolled yaw or lateral escape.");
            }
            passed = true;
        }
        finally
        {
            pickup.Stop();
            body.position = savedPosition;
            body.rotation = savedRotation;
            body.velocity = savedVelocity;
            body.angularVelocity = savedAngular;
            pickup.ReadKeyboard = savedKeyboard;
            pickup.InputEnabled = savedInput;
            camera.Orbit(Mathf.DeltaAngle(camera.Yaw, savedYaw), savedPitch - camera.Pitch);
            camera.distance = savedDistance;
            camera.ReadMouse = savedMouse;
            camera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
            Physics.SyncTransforms();
            File.WriteAllText(ReportFolder + "/runtime.txt", (passed ? "PASS\n" : "FAILED\n") + log);
        }
    }

    private static void CheckLiveRoute(CityWorld city, TownResidentialWalkway route)
    {
        ValidateWalkway(route, route.Id);
        int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(route.From, route.To) / LongitudinalSample));
        var rotation = Quaternion.LookRotation(route.To - route.From);
        for (int sample = 0; sample <= samples; sample++)
            RequireLiveClear(city, Vector3.Lerp(route.From, route.To, (float)sample / samples) + Vector3.up,
                new Vector3(route.Width / 2, .7f, .28f), rotation, route.Id);
    }

    private static void CheckLiveVehicleRoute(CityWorld city, TownResidentialWalkway route)
    {
        int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(route.From, route.To) / LongitudinalSample));
        var rotation = Quaternion.LookRotation(route.To - route.From);
        for (int sample = 0; sample <= samples; sample++)
            RequireLiveClear(city, Vector3.Lerp(route.From, route.To, (float)sample / samples) + Vector3.up * 1.1f,
                new Vector3(1.25f, .65f, 2.3f), rotation, route.Id);
    }

    private static IEnumerator DriveStraight(CityWorld city, TownResidentialWalkway route)
    {
        var pickup = city.pickup;
        var body = pickup.Body;
        var forward = (route.To - route.From).normalized;
        Quaternion rotation = Quaternion.LookRotation(forward);
        pickup.Stop();
        body.position = route.From + Vector3.up * .15f;
        body.rotation = rotation;
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
        pickup.SetInput(.8f, 0, false);
        float start = Time.realtimeSinceStartup, progressAt = start, progress = 0;
        float required = Vector3.Distance(route.From, route.To);
        while (Vector3.Dot(body.position - route.From, forward) < required && Time.realtimeSinceStartup - start < 8)
        {
            yield return new WaitForFixedUpdate();
            float current = Vector3.Dot(body.position - route.From, forward);
            Vector3 lateral = body.position - route.From - forward * current;
            lateral.y = 0;
            Require(lateral.magnitude < 1.5f && Quaternion.Angle(body.rotation, rotation) < 5,
                route.Id + ": pickup left its protected street or unexpectedly rotated: " + body.position + ".");
            if (current >= progress + .2f) { progress = current; progressAt = Time.realtimeSinceStartup; }
            Require(Time.realtimeSinceStartup - progressAt < 1.5f, route.Id + ": native pickup became stuck.");
        }
        Require(Vector3.Dot(body.position - route.From, forward) >= required, route.Id + ": native driving traversal timed out.");
        pickup.Stop();
    }

    private static void RequireLiveClear(CityWorld city, Vector3 position, Vector3 halfExtents, Quaternion rotation, string purpose)
    {
        var obstacles = Physics.OverlapBox(position, halfExtents, rotation, ~0, QueryTriggerInteraction.Ignore)
            .Where(collider => collider.gameObject.scene == city.gameObject.scene && !collider.transform.IsChildOf(city.pickup.transform)).ToArray();
        Require(obstacles.Length == 0, purpose + ": live passage blocked at " + position + " by "
            + string.Join(", ", obstacles.Take(6).Select(collider => collider.name)) + ".");
    }

    private sealed class SavedObstacle
    {
        public Collider Collider { get; }
        public string Name { get; }
        public Bounds Bounds { get; }
        public SavedObstacle(Collider collider) { Collider = collider; Name = collider.name; Bounds = BoundsOf(collider); }
    }

    private readonly struct NamedBody
    {
        public string Name { get; }
        public Bounds Bounds { get; }
        public NamedBody(string name, Bounds bounds) { Name = name; Bounds = bounds; }
    }

    private static Bounds BoundsOf(Collider collider)
    {
        if (collider is BoxCollider box) return TransformBounds(box.transform.localToWorldMatrix, new Bounds(box.center, box.size));
        if (collider is SphereCollider sphere)
        {
            Vector3 scale = sphere.transform.lossyScale;
            float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return new Bounds(sphere.transform.TransformPoint(sphere.center), Vector3.one * radius * 2);
        }
        if (collider is CapsuleCollider capsule)
        {
            Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
            Vector3 scale = capsule.transform.lossyScale;
            float transverse = capsule.direction == 0 ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))
                : capsule.direction == 1 ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            float radius = capsule.radius * transverse;
            Vector3 worldAxis = capsule.transform.TransformVector(axis);
            float half = Mathf.Max(0, capsule.height * worldAxis.magnitude / 2 - radius);
            Vector3 centre = capsule.transform.TransformPoint(capsule.center);
            var bounds = new Bounds(centre - worldAxis.normalized * half, Vector3.one * radius * 2);
            bounds.Encapsulate(new Bounds(centre + worldAxis.normalized * half, Vector3.one * radius * 2));
            return bounds;
        }
        if (collider is MeshCollider mesh && mesh.sharedMesh != null)
            return TransformBounds(mesh.transform.localToWorldMatrix, mesh.sharedMesh.bounds);
        throw new InvalidOperationException("Residential access audit cannot guarantee unsupported collider " + collider.GetType().Name + ".");
    }

    private static Bounds TransformBounds(Matrix4x4 pose, Bounds source)
    {
        var result = new Bounds(pose.MultiplyPoint3x4(source.min), Vector3.zero);
        for (int corner = 1; corner < 8; corner++)
            result.Encapsulate(pose.MultiplyPoint3x4(new Vector3((corner & 1) == 0 ? source.min.x : source.max.x,
                (corner & 2) == 0 ? source.min.y : source.max.y, (corner & 4) == 0 ? source.min.z : source.max.z)));
        return result;
    }

    private static bool OverlapsPassage(Vector3 centre, Vector3 side, Vector3 forward, float sideExtent, float forwardExtent, Bounds obstacle)
    {
        if (obstacle.max.y <= .3f || obstacle.min.y >= 1.7f) return false;
        Vector3 difference = obstacle.center - centre;
        Vector3 extents = obstacle.extents;
        foreach (Vector3 axis in new[] { side, forward, Vector3.right, Vector3.forward })
        {
            float separation = Mathf.Abs(Vector3.Dot(difference, axis));
            float passageRadius = Mathf.Abs(Vector3.Dot(side, axis)) * sideExtent + Mathf.Abs(Vector3.Dot(forward, axis)) * forwardExtent;
            float obstacleRadius = Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.z) * extents.z;
            if (separation >= passageRadius + obstacleRadius - .002f) return false;
        }
        return true;
    }

    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        Vector3 segment = to - from;
        return Vector3.Distance(point, from + segment * Mathf.Clamp01(Vector3.Dot(point - from, segment) / segment.sqrMagnitude));
    }

    private static bool OverlapXZ(Bounds first, Bounds second) => Mathf.Min(first.max.x, second.max.x) - Mathf.Max(first.min.x, second.min.x) > .002f
        && Mathf.Min(first.max.z, second.max.z) - Mathf.Max(first.min.z, second.min.z) > .002f;
    private static bool Contains(Rect parcel, Bounds body) => body.min.x >= parcel.xMin - Tolerance && body.max.x <= parcel.xMax + Tolerance
        && body.min.z >= parcel.yMin - Tolerance && body.max.z <= parcel.yMax + Tolerance;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Finite(Rect value) => Finite(value.xMin) && Finite(value.yMin) && Finite(value.xMax) && Finite(value.yMax);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
