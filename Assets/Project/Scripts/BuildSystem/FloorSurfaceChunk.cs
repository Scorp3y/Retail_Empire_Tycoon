using System;
using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Draws a 16 by 16 region with one surface per floor style, without per-tile renderers or colliders.</summary>
    internal sealed class FloorSurfaceChunk : IDisposable
    {
        private const int Size = 16;
        private readonly Vector3Int coordinates;
        private readonly GameObject root;
        private readonly Dictionary<BuildItemData, MeshFilter> surfaces = new();

        public IEnumerable<MeshFilter> AsphaltSurfaces => surfaces.Where(pair => pair.Key.isAsphalt).Select(pair => pair.Value);
        public IEnumerable<MeshFilter> Surfaces => surfaces.Values;

        public static Vector3Int Coordinates(Vector3Int cell) => new Vector3Int(
            Mathf.FloorToInt((float)cell.x / Size), cell.y, Mathf.FloorToInt((float)cell.z / Size));

        public FloorSurfaceChunk(Vector3Int coordinates, Transform parent)
        {
            this.coordinates = coordinates;
            root = new GameObject($"Floor region {coordinates.x}, {coordinates.z}, layer {coordinates.y}");
            root.transform.SetParent(parent, false);
        }

        public void Rebuild(IReadOnlyDictionary<Vector3Int, BuildItemData> tiles, GridSystem grid, float height)
        {
            var groups = new Dictionary<BuildItemData, List<Vector3Int>>();
            for (int x = coordinates.x * Size; x < (coordinates.x + 1) * Size; x++)
            for (int z = coordinates.z * Size; z < (coordinates.z + 1) * Size; z++)
            {
                var cell = new Vector3Int(x, coordinates.y, z);
                if (!tiles.TryGetValue(cell, out var item) || item == null) continue;
                if (!groups.TryGetValue(item, out var cells)) groups.Add(item, cells = new List<Vector3Int>());
                cells.Add(cell);
            }
            foreach (var obsolete in surfaces.Keys.Where(item => !groups.ContainsKey(item)).ToArray())
            {
                ReleaseSurface(surfaces[obsolete]);
                surfaces.Remove(obsolete);
            }
            foreach (var group in groups)
            {
                if (!surfaces.TryGetValue(group.Key, out var filter))
                {
                    var surface = new GameObject(group.Key.id, typeof(MeshFilter), typeof(MeshRenderer));
                    if (coordinates.y > 0) surface.AddComponent<MeshCollider>();
                    surface.transform.SetParent(root.transform, false);
                    filter = surface.GetComponent<MeshFilter>();
                    filter.sharedMesh = new Mesh { name = "Floor surface " + group.Key.id };
                    filter.sharedMesh.MarkDynamic();
                    var renderer = surface.GetComponent<MeshRenderer>();
                    // A horizontal floor receives furniture shadows but cannot cast useful ones.
                    renderer.shadowCastingMode = coordinates.y > 0 ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                    renderer.sharedMaterial = group.Key.floorMaterial;
                    surfaces.Add(group.Key, filter);
                }
                // A 5 mm separation prevents the upper slab fighting with the coplanar lower-wall cap.
                RebuildSurface(filter, group.Value, grid, coordinates.y>0?FloorPainter.UpperSurfaceLift:group.Key.isAsphalt?.015f:height);
            }
        }

        private static void RebuildSurface(MeshFilter filter, List<Vector3Int> cells, GridSystem grid, float height)
        {
            var vertices = new List<Vector3>(cells.Count * 4);
            var normals = new List<Vector3>(cells.Count * 4);
            var uvs = new List<Vector2>(cells.Count * 4);
            var surfaceCoordinates = new List<Vector2>(cells.Count * 4);
            var indices = new List<int>(cells.Count * 6);
            var offsets = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            foreach (var cell in cells)
            {
                int first = vertices.Count;
                foreach (var offset in offsets)
                {
                    var world = grid.origin + new Vector3((cell.x + offset.x) * grid.cellSize, height + cell.y * GridSystem.StoreyHeight, (cell.z + offset.y) * grid.cellSize);
                    vertices.Add(filter.transform.InverseTransformPoint(world));
                    normals.Add(filter.transform.InverseTransformDirection(Vector3.up).normalized);
                    // Preserve the four-cell texture repeat and its continuity across region borders.
                    uvs.Add(new Vector2(cell.x + offset.x, cell.z + offset.y) * .25f);
                    // Remains in source shop coordinates when the same mesh is copied into City.
                    surfaceCoordinates.Add(new Vector2(world.x,world.z));
                }
                indices.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                if (cell.y > 0)
                {
                    // Separate underside vertices preserve correct ceiling lighting with single-sided floor materials.
                    for (int i = 0; i < 4; i++)
                    {
                        vertices.Add(vertices[first + i] - Vector3.up * .035f);
                        normals.Add(filter.transform.InverseTransformDirection(Vector3.down).normalized);
                        uvs.Add(uvs[first + i]);
                        surfaceCoordinates.Add(surfaceCoordinates[first + i]);
                    }
                    indices.AddRange(new[] { first+4, first+6, first+5, first+4, first+7, first+6 });
                }
            }
            var mesh = filter.sharedMesh;
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, surfaceCoordinates);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            var collider = filter.GetComponent<MeshCollider>();
            if (collider != null) { collider.sharedMesh = null; collider.sharedMesh = mesh; }
        }

        public void SetVisible(bool visible)
        {
            foreach (var surface in surfaces.Values) surface.GetComponent<MeshRenderer>().forceRenderingOff = !visible;
        }

        private static void ReleaseSurface(MeshFilter filter)
        {
            filter.gameObject.SetActive(false);
            Object.Destroy(filter.sharedMesh);
            Object.Destroy(filter.gameObject);
        }

        public void Dispose()
        {
            foreach (var filter in surfaces.Values) ReleaseSurface(filter);
            surfaces.Clear();
            root.SetActive(false);
            Object.Destroy(root);
        }
    }
}
