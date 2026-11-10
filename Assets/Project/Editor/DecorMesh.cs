using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class DecorMesh
{
    internal enum Finish { Cream, Pine, Sage, Leaf, LightLeaf, Terra, Wood, DarkWood, Brass, Charcoal, Red, Blue, Glass, Glow }
    internal static readonly string[] Colors =
        { "E7D8AC", "344E3A", "89936A", "607B40", "9AAB55", "BA6246", "BB864E", "7F583B",
          "C99949", "41443D", "A6493A", "6399A1", "C0E2D9", "FFE1A2" };
    private readonly List<Vector3> vertices = new();
    private readonly SortedDictionary<Finish, List<int>> groups = new();
    internal Finish[] UsedFinishes => groups.Keys.ToArray();

    private void Face(Finish finish, params Vector3[] polygon)
    {
        if (!groups.TryGetValue(finish, out var triangles)) groups.Add(finish, triangles = new List<int>());
        int start = vertices.Count;
        vertices.AddRange(polygon);
        for (int i = 1; i + 1 < polygon.Length; i++)
        {
            triangles.Add(start);
            triangles.Add(start + i);
            triangles.Add(start + i + 1);
        }
    }

    internal void Stamp(Finish finish, Vector3 center, float width, float height)
    {
        var x = Vector3.right * (width * .5f);
        var y = Vector3.up * (height * .5f);
        Face(finish, center-x-y, center+x-y, center+x+y, center-x+y);
    }

    internal void Canopy(Finish first, Finish second, float height, float eave, float radius)
    {
        Vector3 Point(int corner,float y)
        {
            float angle=(corner+.5f)*Mathf.PI/4;
            return new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius);
        }
        for(int i=0;i<8;i++)
        {
            var finish=i%2==0?first:second;
            var a=Point(i,eave);
            var b=Point(i+1,eave);
            Face(finish,Vector3.up*height,b,a);
            Face(finish,Vector3.up*(height-.012f),a,b);
            Face(finish,a,a-Vector3.up*.075f,b-Vector3.up*.075f,b);
        }
    }

    private void Shell(Finish finish, Vector3[][] rings, bool caps = true)
    {
        int sides = rings[0].Length;
        if (caps) Face(finish, rings[0]);
        for (int layer = 1; layer < rings.Length; layer++)
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Face(finish, rings[layer - 1][i], rings[layer][i], rings[layer][j], rings[layer - 1][j]);
            }
        if (caps) Face(finish, rings[rings.Length - 1].Reverse().ToArray());
    }

    internal void Box(Finish finish, Vector3 p, Vector3 size, Vector3 angles = default)
    {
        float bevel = Mathf.Min(size.x, size.y, size.z) * .14f;
        var rotation = Quaternion.Euler(angles);
        Vector3[] Ring(float y, float inset)
        {
            float x = size.x * .5f - inset, z = size.z * .5f - inset;
            float corner = Mathf.Min(bevel, x * .4f, z * .4f);
            var points = new[] { new Vector2(x,-z+corner), new Vector2(x,z-corner), new Vector2(x-corner,z), new Vector2(-x+corner,z),
                new Vector2(-x,z-corner), new Vector2(-x,-z+corner), new Vector2(-x+corner,-z), new Vector2(x-corner,-z) };
            return points.Select(v => p + rotation * new Vector3(v.x, y, v.y)).ToArray();
        }
        float h = size.y * .5f;
        Shell(finish, new[] { Ring(-h, bevel), Ring(-h + bevel, 0), Ring(h - bevel, 0), Ring(h, bevel) });
    }

    internal void Profile(Finish finish, Vector3 p, (float y, float r)[] profile, int sides = 10,
        float depth = 1, Vector3 angles = default, bool caps = true)
    {
        var rotation = Quaternion.Euler(angles);
        var rings = profile.Select(ring => Enumerable.Range(0, sides).Select(i =>
        {
            float angle = i * Mathf.PI * 2 / sides;
            return p + rotation * new Vector3(Mathf.Cos(angle) * ring.r, ring.y, Mathf.Sin(angle) * ring.r * depth);
        }).ToArray()).ToArray();
        Shell(finish, rings, caps);
    }

    internal void Ball(Finish finish, Vector3 p, Vector3 size, Vector3 angles = default, int latitudeRings = 5)
    {
        var rotation = Quaternion.Euler(angles);
        var rings = Enumerable.Range(0, latitudeRings).Select(row => Enumerable.Range(0, 8).Select(i =>
        {
            float pitch = Mathf.Lerp(-1.55f, 1.55f, row / (latitudeRings - 1f)), yaw = i * Mathf.PI / 4;
            var point = new Vector3(Mathf.Cos(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Sin(yaw) * Mathf.Cos(pitch));
            return p + rotation * Vector3.Scale(size, point) * .5f;
        }).ToArray()).ToArray();
        Shell(finish, rings);
    }

    internal void Tube(Finish finish, float radius, params Vector3[] path)
    {
        var rings = path.Select((p, i) =>
        {
            var axis = (path[Mathf.Min(i + 1, path.Length - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
            var right = Vector3.Cross(axis, Mathf.Abs(axis.z) < .9f ? Vector3.forward : Vector3.up).normalized;
            var tangent = Vector3.Cross(right, axis);
            return Enumerable.Range(0, 6).Select(n => p + radius *
                (right * Mathf.Cos(n * Mathf.PI / 3) + tangent * Mathf.Sin(n * Mathf.PI / 3))).ToArray();
        }).ToArray();
        Shell(finish, rings);
    }

    internal Mesh Bake(string name, Vector2 fit, bool wall, Vector3 prefabScale, Vector3 offset, float heightLimit)
    {
        var min = vertices.Aggregate(Vector3.Min);
        var max = vertices.Aggregate(Vector3.Max);
        float scale = Mathf.Min(1, fit.x / (max.x - min.x));
        scale = Mathf.Min(scale,heightLimit/(max.y-min.y));
        if (fit.y > 0) scale = Mathf.Min(scale, fit.y / (max.z - min.z));
        var pivot = new Vector3((min.x + max.x) * .5f, min.y, wall ? min.z : (min.z + max.z) * .5f);
        var inverse = new Vector3(1 / prefabScale.x, 1 / prefabScale.y, 1 / prefabScale.z);
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt16, subMeshCount = groups.Count };
        mesh.SetVertices(vertices.Select(p => Vector3.Scale((p - pivot) * scale + offset, inverse)).ToList());
        int index = 0;
        foreach (var triangles in groups.Values) mesh.SetTriangles(triangles, index++);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

