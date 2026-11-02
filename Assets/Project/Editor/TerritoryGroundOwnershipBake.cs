using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Moves legacy ground copies out of permanent town batches into the existing
/// parcel-owned scenery. Original meshes and ground outside the site remain unchanged.</summary>
public static class TerritoryGroundOwnershipBake
{
    public const string Root = "Assets/Art/TownForgottenYard/OwnershipGround";
    public const string Report = "Library/TerritoryGroundOwnership";
    private sealed class Polygon : List<TownCityCorrectionsGeometry.Vertex>
    {
        public Polygon(IEnumerable<TownCityCorrectionsGeometry.Vertex> vertices) : base(vertices) { }
    }

    public static void Run()
    {
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void Bake()
    {
        TownWorldSafety.RequireIsolatedEditor();
        Directory.CreateDirectory(Root + "/Meshes"); Directory.CreateDirectory(Report); AssetDatabase.Refresh();
        var town = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try
        {
            var layout = town.GetComponent<TownWorldPlaces>().layout;
            var scenery = town.GetComponentInChildren<TownExpansionScenery>(true);
            var groups = scenery.plots.ToDictionary(p => p.territory, p => p.model.transform);
            var filters = town.GetComponentsInChildren<MeshFilter>(true).Where(f =>
                f.name.StartsWith("Stable flat ground", StringComparison.Ordinal) ||
                f.name == "Repair geometry" && f.transform.parent.name == "Seam repairs stage 6").ToArray();
            var geometry = new TownCityCorrectionsGeometry(filters);
            int moved = 0, batches = 0, sourceNumber = 0;
            double originalArea = 0, splitArea = 0;
            var audit = new List<string>();
            foreach (var view in geometry.Views)
            {
                var owned = layout.expansionPlots.ToDictionary(p => p.territory,
                    p => new List<TownCityCorrectionsGeometry.Vertex>());
                for (int i = 0; i < view.Indices.Length; i += 3)
                {
                    var points = TownCityCorrectionsGeometry.Points(view, i);
                    float low = points.Min(p => p.y), high = points.Max(p => p.y);
                    // The ordinary grass base is permanent; only raised flat decoration is owned.
                    if (low <= .005f || high > .25f || high - low > .0001f ||
                        TownCityCorrectionsGeometry.Color(view, i, TownColor.Grass)) continue;
                    var triangle = new Polygon(Enumerable.Range(0, 3).Select(c =>
                    {
                        var v = view.Vertices[view.Indices[i + c]];
                        return new TownCityCorrectionsGeometry.Vertex(points[c],
                            view.Filter.transform.TransformDirection(v.N), v.U, v.V);
                    }));
                    var remaining = new List<Polygon> { triangle };
                    bool changed = false;
                    double insideArea = 0;
                    foreach (var parcel in layout.expansionPlots)
                    {
                        var next = new List<Polygon>();
                        foreach (var polygon in remaining)
                        {
                            var inside = polygon;
                            var outside = new List<Polygon>();
                            for (int edge = 0; edge < 4 && inside.Count > 0; edge++)
                            {
                                float Distance(TownCityCorrectionsGeometry.Vertex v) => edge == 0 ? v.P.x - parcel.bounds.xMin :
                                    edge == 1 ? parcel.bounds.xMax - v.P.x : edge == 2 ? v.P.z - parcel.bounds.yMin : parcel.bounds.yMax - v.P.z;
                                var part = Clip(inside, v => -Distance(v));
                                if (Area(part) > 1e-8) outside.Add(part);
                                inside = Clip(inside, Distance);
                            }
                            double area = Area(inside);
                            if (area < 1e-8) { next.Add(polygon); continue; }
                            changed = true; insideArea += area;
                            Triangles(inside, (a, b, c) => owned[parcel.territory].AddRange(new[] { a, b, c }));
                            next.AddRange(outside);
                        }
                        remaining = next;
                    }
                    if (!changed) continue;
                    view.Removed.Add(i); moved++;
                    originalArea += Area(triangle);
                    splitArea += insideArea + remaining.Sum(Area);
                    foreach (var polygon in remaining) Triangles(polygon, view.AddWorld);
                }
                if (view.Removed.Count == 0) { sourceNumber++; continue; }
                var material = view.Filter.GetComponent<MeshRenderer>().sharedMaterial;
                var colliders = view.Filter.GetComponents<MeshCollider>().Where(c => c.sharedMesh == view.Source).ToArray();
                foreach (var parcel in layout.expansionPlots)
                {
                    var vertices = owned[parcel.territory]; if (vertices.Count == 0) continue;
                    string id = "ParcelGround_" + sourceNumber + "_" + parcel.territory;
                    var parent = groups[parcel.territory];
                    var mesh = new Mesh { name = id, indexFormat = IndexFormat.UInt32 };
                    mesh.vertices = vertices.Select(v => parent.InverseTransformPoint(v.P)).ToArray();
                    mesh.normals = vertices.Select(v => parent.InverseTransformDirection(v.N)).ToArray();
                    mesh.uv = vertices.Select(v => v.U).ToArray(); mesh.uv2 = vertices.Select(v => v.V).ToArray();
                    mesh.triangles = Enumerable.Range(0, vertices.Count).ToArray(); mesh.RecalculateBounds();
                    string path = Root + "/Meshes/" + id + ".asset";
                    TownBakeryPlacement.SaveAsset(mesh, path);
                    var child = new GameObject(id, typeof(MeshFilter), typeof(MeshRenderer));
                    child.transform.SetParent(parent, false);
                    child.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = view.Filter.GetComponent<MeshRenderer>().shadowCastingMode;
                    if (colliders.Length > 0) child.AddComponent<MeshCollider>().sharedMesh = child.GetComponent<MeshFilter>().sharedMesh;
                    batches++;
                }
                view.Save("PermanentGround_" + sourceNumber++, Root);
                foreach (var collider in colliders) collider.sharedMesh = view.Filter.sharedMesh;
                audit.Add(view.Filter.name + ": " + view.Removed.Count + " original triangles reassigned.");
            }
            if (Math.Abs(originalArea - splitArea) > Math.Max(.001, originalArea * .00001))
                throw new InvalidOperationException("Clipping changed the visible ground area.");
            PrefabUtility.SaveAsPrefabAsset(town, TownWorldAssembler.PrefabPath); AssetDatabase.SaveAssets();
            File.WriteAllLines(Report + "/Bake.txt", new[] { $"PASS: {moved} legacy ground triangles reassigned into {batches} owned pieces. Area before/after: {originalArea:F6}/{splitArea:F6}." }.Concat(audit));
        }
        finally { PrefabUtility.UnloadPrefabContents(town); }
    }

    private static Polygon Clip(Polygon input, Func<TownCityCorrectionsGeometry.Vertex, float> distance)
    {
        var result = new Polygon(Array.Empty<TownCityCorrectionsGeometry.Vertex>());
        if (input.Count == 0) return result;
        var a = input[input.Count - 1]; float da = distance(a);
        foreach (var b in input)
        {
            float db = distance(b);
            if ((da >= 0) != (db >= 0)) result.Add(TownCityCorrectionsGeometry.Vertex.Lerp(a, b, da / (da - db)));
            if (db >= 0) result.Add(b); a = b; da = db;
        }
        return result;
    }
    private static void Triangles(Polygon polygon, Action<TownCityCorrectionsGeometry.Vertex, TownCityCorrectionsGeometry.Vertex, TownCityCorrectionsGeometry.Vertex> add)
    {
        for (int i = 1; i < polygon.Count - 1; i++)
            if (Vector3.Cross(polygon[i].P - polygon[0].P, polygon[i + 1].P - polygon[0].P).sqrMagnitude > 1e-12f)
                add(polygon[0], polygon[i], polygon[i + 1]);
    }
    private static double Area(Polygon polygon)
    {
        double sum = 0;
        Triangles(polygon, (a, b, c) => sum += Vector3.Cross(b.P - a.P, c.P - a.P).magnitude * .5);
        return sum;
    }
}
