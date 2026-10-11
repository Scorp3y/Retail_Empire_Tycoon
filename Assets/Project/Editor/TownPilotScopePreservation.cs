using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Rejects visual or collision changes outside the explicitly authorised pilot block.</summary>
internal static class TownPilotScopePreservation
{
    // The two-metre margin includes the pilot's junctions with its existing public pavement.
    private static readonly Rect AllowedArea = new Rect(-212, -188, 88, 40);
    private const double CoordinatePrecision = 10000;

    internal sealed class Snapshot
    {
        public string GeometryHash { get; }
        public int TriangleCount { get; }
        public string ColliderHash { get; }
        public int ColliderCount { get; }
        public string Fingerprint { get; }

        internal Snapshot(string geometryHash, int triangles, string colliderHash, int colliders)
        {
            GeometryHash = geometryHash;
            TriangleCount = triangles;
            ColliderHash = colliderHash;
            ColliderCount = colliders;
            using (var hash = SHA256.Create())
                Fingerprint = Hex(hash.ComputeHash(Encoding.UTF8.GetBytes(ToString())));
        }

        public void RequireUnchanged(Snapshot current)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            var changes = new List<string>();
            if (GeometryHash != current.GeometryHash || TriangleCount != current.TriangleCount)
                changes.Add($"visual geometry/materials: {TriangleCount} -> {current.TriangleCount} protected triangles");
            if (ColliderHash != current.ColliderHash || ColliderCount != current.ColliderCount)
                changes.Add($"collision geometry/settings: {ColliderCount} -> {current.ColliderCount} protected colliders");
            if (changes.Count != 0)
                throw new InvalidOperationException("Pilot scope violation outside Rect(-212,-188,88,40): " + string.Join("; ", changes));
        }

        public override string ToString()
            => $"geometry={GeometryHash}; triangles={TriangleCount}; collision={ColliderHash}; colliders={ColliderCount}";
    }

    /// <summary>
    /// Loads the saved prefab without modifying it. Positions and attributes are compared at 0.0001 precision,
    /// independent of mesh GUIDs, batching, triangle order and hierarchy names. Winding remains significant.
    /// Only triangles and complete collision volumes contained in AllowedArea are excluded.
    /// Unsupported collision shapes fail closed rather than silently weakening the scope guarantee.
    /// </summary>
    public static Snapshot Capture()
    {
        var root = PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        try
        {
            if (!root.transform.position.Equals(Vector3.zero) || !root.transform.rotation.Equals(Quaternion.identity) || !root.transform.lossyScale.Equals(Vector3.one))
                throw new InvalidOperationException("Pilot scope audit requires the saved town prefab in its original native coordinate frame.");
            using (var hash = SHA256.Create())
            {
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                    if(!(renderer is MeshRenderer) || renderer.GetComponent<MeshFilter>()==null)
                        throw new InvalidOperationException("Pilot scope audit cannot protect unsupported renderer "+renderer.GetType().Name+".");
                var geometry = new List<Digest>();
                var collision = new List<Digest>();
                var triangleBuffer = new byte[192];
                var materialSignatures = new Dictionary<Material, Digest>();
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    CaptureMesh(root.transform, filter, geometry, materialSignatures, hash, triangleBuffer);
                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                {
                    if (Contains(CollisionBounds(collider))) continue;
                    collision.Add(CaptureCollider(root.transform, collider, hash));
                }
                return new Snapshot(HashSorted(geometry, hash), geometry.Count,
                    HashSorted(collision, hash), collision.Count);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void CaptureMesh(Transform root, MeshFilter filter, List<Digest> protectedTriangles,
        Dictionary<Material, Digest> materialSignatures, SHA256 hash, byte[] buffer)
    {
        var mesh = filter.sharedMesh;
        if (mesh == null) throw new InvalidOperationException("Pilot scope audit found a MeshFilter without a mesh.");
        var renderer = filter.GetComponent<MeshRenderer>();
        if (renderer == null) throw new InvalidOperationException("Pilot scope audit found a mesh without its renderer.");
        var positions = mesh.vertices;
        var normals = mesh.normals;
        var uv = mesh.uv;
        var uv2 = mesh.uv2;
        var colours = mesh.colors32;
        var pose = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        var normalPose = pose.inverse.transpose;
        var vertices = new Vertex[positions.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var point = pose.MultiplyPoint3x4(positions[i]);
            var normal = normals.Length == positions.Length ? normalPose.MultiplyVector(normals[i]).normalized : Vector3.zero;
            vertices[i] = new Vertex(point, normal, uv.Length == positions.Length ? uv[i] : Vector2.zero,
                uv2.Length == positions.Length ? uv2[i] : Vector2.zero,
                colours.Length == positions.Length ? colours[i] : new Color32(255, 255, 255, 255));
        }
        var materials = renderer.sharedMaterials;
        if(materials.Length!=mesh.subMeshCount)
            throw new InvalidOperationException("Pilot scope audit requires one material per submesh.");
        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
        {
            if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                throw new InvalidOperationException("Pilot scope audit only supports triangle meshes.");
            if (submesh >= materials.Length || materials[submesh] == null)
                throw new InvalidOperationException("Pilot scope audit found a triangle submesh without its material.");
            var material = materials[submesh];
            if (!materialSignatures.TryGetValue(material, out var signature))
            {
                signature = MaterialSignature(material, hash);
                materialSignatures.Add(material, signature);
            }
            var indices = mesh.GetTriangles(submesh);
            for (int i = 0; i < indices.Length; i += 3)
            {
                var a = vertices[indices[i]];
                var b = vertices[indices[i + 1]];
                var c = vertices[indices[i + 2]];
                if (a.InPilot && b.InPilot && c.InPilot) continue;
                // Cyclic rotation is batching-independent, unlike sorting all three vertices, which loses winding.
                if (b.CompareTo(a) < 0 && b.CompareTo(c) <= 0) { var original = a; a = b; b = c; c = original; }
                else if (c.CompareTo(a) < 0 && c.CompareTo(b) < 0) { var original = a; a = c; c = b; b = original; }
                int offset = 0;
                Write(signature, buffer, ref offset);
                Write(renderer.enabled ? 1 : 0, buffer, ref offset);
                Write(renderer.gameObject.activeInHierarchy ? 1 : 0, buffer, ref offset);
                Write(renderer.gameObject.layer, buffer, ref offset);
                Write((int)renderer.shadowCastingMode, buffer, ref offset);
                Write(renderer.receiveShadows ? 1 : 0, buffer, ref offset);
                a.Write(buffer, ref offset); b.Write(buffer, ref offset); c.Write(buffer, ref offset);
                protectedTriangles.Add(new Digest(hash.ComputeHash(buffer, 0, offset)));
            }
        }
    }

    private static Digest MaterialSignature(Material material, SHA256 hash)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(material.name);
            writer.Write(material.shader != null ? material.shader.name : "<missing shader>");
            writer.Write(material.renderQueue);
            writer.Write(material.doubleSidedGI);
            writer.Write((int)material.globalIlluminationFlags);
            writer.Write(material.GetTag("RenderType",false,""));
            for(int pass=0;pass<material.passCount;pass++)
            {
                string passName=material.GetPassName(pass);
                writer.Write(passName);writer.Write(material.GetShaderPassEnabled(passName));
            }
            foreach (var keyword in material.shaderKeywords.OrderBy(k => k, StringComparer.Ordinal)) writer.Write(keyword);
            if (material.shader == null) throw new InvalidOperationException("Pilot scope audit found a material without its shader.");
            var properties = Enumerable.Range(0, ShaderUtil.GetPropertyCount(material.shader))
                .OrderBy(i => ShaderUtil.GetPropertyName(material.shader, i), StringComparer.Ordinal);
            foreach (int index in properties)
            {
                string name = ShaderUtil.GetPropertyName(material.shader, index);
                var kind = ShaderUtil.GetPropertyType(material.shader, index);
                writer.Write(name); writer.Write((int)kind);
                switch (kind)
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        var colour = material.GetColor(name); Point(writer, new Vector4(colour.r, colour.g, colour.b, colour.a)); break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        Point(writer, material.GetVector(name)); break;
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        writer.Write(Quantize(material.GetFloat(name))); break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        var texture = material.GetTexture(name);
                        writer.Write(texture != null);
                        if (texture != null)
                        {
                            writer.Write(texture.name); writer.Write((int)texture.dimension);
                            writer.Write(texture.width); writer.Write(texture.height);
                            writer.Write((int)texture.wrapMode); writer.Write((int)texture.filterMode);
                            if (texture is Texture2D palette && palette.isReadable && palette.width * palette.height <= 1024)
                            {
                                writer.Write((int)palette.format);
                                foreach (var pixel in palette.GetPixels32())
                                { writer.Write(pixel.r); writer.Write(pixel.g); writer.Write(pixel.b); writer.Write(pixel.a); }
                            }
                            else writer.Write(texture.imageContentsHash.ToString());
                        }
                        Point(writer, material.GetTextureScale(name)); Point(writer, material.GetTextureOffset(name)); break;
                    default:
                        throw new InvalidOperationException("Pilot scope audit does not support shader property " + name + " (" + kind + ").");
                }
            }
            writer.Flush();
            return new Digest(hash.ComputeHash(stream.GetBuffer(), 0, checked((int)stream.Length)));
        }
    }

    private static Bounds CollisionBounds(Collider collider)
    {
        if (collider is BoxCollider box) return TransformedBounds(collider.transform.localToWorldMatrix, new Bounds(box.center, box.size));
        if (collider is SphereCollider sphere)
            return new Bounds(collider.transform.TransformPoint(sphere.center), Vector3.one * WorldRadius(collider.transform, sphere.radius) * 2);
        if (collider is CapsuleCollider capsule)
        {
            CapsuleShape(capsule, out var first, out var second, out float radius);
            var bounds = new Bounds(first, Vector3.one * radius * 2); bounds.Encapsulate(new Bounds(second, Vector3.one * radius * 2));
            return bounds;
        }
        if (collider is MeshCollider mesh && mesh.sharedMesh != null)
            return TransformedBounds(collider.transform.localToWorldMatrix, mesh.sharedMesh.bounds);
        throw new InvalidOperationException("Pilot scope audit cannot guarantee the unsupported collision shape " + collider.GetType().Name + ".");
    }

    private static Digest CaptureCollider(Transform root, Collider collider, SHA256 hash)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(collider.GetType().Name); writer.Write(collider.enabled); writer.Write(collider.isTrigger);
            writer.Write(collider.gameObject.activeInHierarchy); writer.Write(collider.gameObject.layer);
            writer.Write(Quantize(collider.contactOffset));
            var material = collider.sharedMaterial;
            writer.Write(material != null);
            if (material != null)
            {
                writer.Write(material.name); writer.Write(Quantize(material.dynamicFriction)); writer.Write(Quantize(material.staticFriction));
                writer.Write(Quantize(material.bounciness)); writer.Write((int)material.frictionCombine); writer.Write((int)material.bounceCombine);
            }
            var pose = root.worldToLocalMatrix * collider.transform.localToWorldMatrix;
            if (collider is BoxCollider box)
            {
                Point(writer, pose.MultiplyPoint3x4(box.center));
                Point(writer, pose.MultiplyVector(Vector3.right * box.size.x));
                Point(writer, pose.MultiplyVector(Vector3.up * box.size.y));
                Point(writer, pose.MultiplyVector(Vector3.forward * box.size.z));
            }
            else if (collider is SphereCollider sphere)
            {
                Point(writer, pose.MultiplyPoint3x4(sphere.center)); writer.Write(Quantize(WorldRadius(collider.transform, sphere.radius)));
            }
            else if (collider is CapsuleCollider capsule)
            {
                CapsuleShape(capsule, out var first, out var second, out float radius);
                first = root.InverseTransformPoint(first); second = root.InverseTransformPoint(second);
                if (Compare(first, second) > 0) { var swap = first; first = second; second = swap; }
                Point(writer, first); Point(writer, second); writer.Write(Quantize(radius));
            }
            else if (collider is MeshCollider mesh)
            {
                writer.Write(mesh.convex); writer.Write((int)mesh.cookingOptions);
                var vertices = mesh.sharedMesh.vertices; var triangles = mesh.sharedMesh.triangles;
                var parts = new List<Digest>(triangles.Length / 3);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var points = new[] { pose.MultiplyPoint3x4(vertices[triangles[i]]), pose.MultiplyPoint3x4(vertices[triangles[i + 1]]), pose.MultiplyPoint3x4(vertices[triangles[i + 2]]) };
                    int start = Compare(points[1], points[0]) < 0 && Compare(points[1], points[2]) <= 0 ? 1 : Compare(points[2], points[0]) < 0 ? 2 : 0;
                    using (var triangle = new MemoryStream())
                    using (var triangleWriter = new BinaryWriter(triangle))
                    {
                        for (int vertex = 0; vertex < 3; vertex++) Point(triangleWriter, points[(start + vertex) % 3]);
                        triangleWriter.Flush(); parts.Add(new Digest(hash.ComputeHash(triangle.ToArray())));
                    }
                }
                writer.Write(HashSorted(parts, hash)); writer.Write(parts.Count);
            }
            writer.Flush(); return new Digest(hash.ComputeHash(stream.GetBuffer(), 0, checked((int)stream.Length)));
        }
    }

    private static void CapsuleShape(CapsuleCollider capsule, out Vector3 first, out Vector3 second, out float radius)
    {
        var scale = capsule.transform.lossyScale;
        var axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
        var worldAxis = capsule.transform.TransformVector(axis);
        float transverse = capsule.direction == 0 ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))
            : capsule.direction == 1 ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        radius = capsule.radius * transverse;
        float halfSegment = Mathf.Max(0, capsule.height * worldAxis.magnitude / 2 - radius);
        var centre = capsule.transform.TransformPoint(capsule.center);
        first = centre - worldAxis.normalized * halfSegment; second = centre + worldAxis.normalized * halfSegment;
    }

    private static float WorldRadius(Transform transform, float radius)
    {
        var scale = transform.lossyScale;
        return radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

    private static Bounds TransformedBounds(Matrix4x4 pose, Bounds bounds)
    {
        var result = new Bounds(pose.MultiplyPoint3x4(bounds.min), Vector3.zero);
        for (int corner = 1; corner < 8; corner++)
            result.Encapsulate(pose.MultiplyPoint3x4(new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                (corner & 2) == 0 ? bounds.min.y : bounds.max.y, (corner & 4) == 0 ? bounds.min.z : bounds.max.z)));
        return result;
    }

    private static bool Contains(Bounds bounds) => Contains(bounds.min) && Contains(bounds.max);
    private static bool Contains(Vector3 point) => point.x >= AllowedArea.xMin && point.x <= AllowedArea.xMax
        && point.z >= AllowedArea.yMin && point.z <= AllowedArea.yMax;
    private static int Quantize(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Pilot scope audit found a non-finite geometry/material value.");
        return checked((int)Math.Round(value * CoordinatePrecision, MidpointRounding.AwayFromZero));
    }
    private static int Compare(Vector3 a, Vector3 b)
    {
        int result = Quantize(a.x).CompareTo(Quantize(b.x)); if (result != 0) return result;
        result = Quantize(a.y).CompareTo(Quantize(b.y)); return result != 0 ? result : Quantize(a.z).CompareTo(Quantize(b.z));
    }
    private static void Point(BinaryWriter writer, Vector2 value) { writer.Write(Quantize(value.x)); writer.Write(Quantize(value.y)); }
    private static void Point(BinaryWriter writer, Vector3 value) { writer.Write(Quantize(value.x)); writer.Write(Quantize(value.y)); writer.Write(Quantize(value.z)); }
    private static void Point(BinaryWriter writer, Vector4 value) { writer.Write(Quantize(value.x)); writer.Write(Quantize(value.y)); writer.Write(Quantize(value.z)); writer.Write(Quantize(value.w)); }

    private readonly struct Vertex : IComparable<Vertex>
    {
        private readonly int x, y, z, nx, ny, nz, u, v, u2, v2, colour;
        public readonly bool InPilot;
        public Vertex(Vector3 position, Vector3 normal, Vector2 uv, Vector2 uv2, Color32 color)
        {
            x = Quantize(position.x); y = Quantize(position.y); z = Quantize(position.z);
            nx = Quantize(normal.x); ny = Quantize(normal.y); nz = Quantize(normal.z);
            u = Quantize(uv.x); v = Quantize(uv.y); u2 = Quantize(uv2.x); v2 = Quantize(uv2.y);
            colour = color.r | color.g << 8 | color.b << 16 | color.a << 24; InPilot = Contains(position);
        }
        public int CompareTo(Vertex other)
        {
            int result = x.CompareTo(other.x); if (result != 0) return result;
            result = y.CompareTo(other.y); if (result != 0) return result;
            result = z.CompareTo(other.z); if (result != 0) return result;
            result = nx.CompareTo(other.nx); if (result != 0) return result;
            result = ny.CompareTo(other.ny); if (result != 0) return result;
            result = nz.CompareTo(other.nz); if (result != 0) return result;
            result = u.CompareTo(other.u); if (result != 0) return result;
            result = v.CompareTo(other.v); if (result != 0) return result;
            result = u2.CompareTo(other.u2); if (result != 0) return result;
            result = v2.CompareTo(other.v2); return result != 0 ? result : colour.CompareTo(other.colour);
        }
        public void Write(byte[] buffer, ref int offset)
        {
            TownPilotScopePreservation.Write(x, buffer, ref offset); TownPilotScopePreservation.Write(y, buffer, ref offset);
            TownPilotScopePreservation.Write(z, buffer, ref offset); TownPilotScopePreservation.Write(nx, buffer, ref offset);
            TownPilotScopePreservation.Write(ny, buffer, ref offset); TownPilotScopePreservation.Write(nz, buffer, ref offset);
            TownPilotScopePreservation.Write(u, buffer, ref offset); TownPilotScopePreservation.Write(v, buffer, ref offset);
            TownPilotScopePreservation.Write(u2, buffer, ref offset); TownPilotScopePreservation.Write(v2, buffer, ref offset);
            TownPilotScopePreservation.Write(colour, buffer, ref offset);
        }
    }

    // Four integers per triangle avoid retaining a million hexadecimal strings or vertex descriptions.
    private readonly struct Digest : IComparable<Digest>
    {
        private readonly ulong a, b, c, d;
        public Digest(byte[] bytes) { a = BitConverter.ToUInt64(bytes, 0); b = BitConverter.ToUInt64(bytes, 8); c = BitConverter.ToUInt64(bytes, 16); d = BitConverter.ToUInt64(bytes, 24); }
        public int CompareTo(Digest other)
        {
            int result = a.CompareTo(other.a); if (result != 0) return result;
            result = b.CompareTo(other.b); if (result != 0) return result;
            result = c.CompareTo(other.c); return result != 0 ? result : d.CompareTo(other.d);
        }
        public void Write(byte[] buffer, ref int offset)
        {
            WriteInteger(a, buffer, ref offset); WriteInteger(b, buffer, ref offset);
            WriteInteger(c, buffer, ref offset); WriteInteger(d, buffer, ref offset);
        }
        private static void WriteInteger(ulong value, byte[] buffer, ref int offset)
        { for (int i = 0; i < 8; i++) { buffer[offset++] = (byte)value; value >>= 8; } }
    }

    private static void Write(Digest digest, byte[] buffer, ref int offset) => digest.Write(buffer, ref offset);
    private static void Write(int value, byte[] buffer, ref int offset)
    { uint bits = unchecked((uint)value); for (int i = 0; i < 4; i++) { buffer[offset++] = (byte)bits; bits >>= 8; } }
    private static string HashSorted(List<Digest> records, SHA256 hash)
    {
        records.Sort(); hash.Initialize(); var buffer = new byte[65536]; int offset = 0;
        foreach (var digest in records)
        {
            digest.Write(buffer, ref offset);
            if (offset != buffer.Length) continue;
            hash.TransformBlock(buffer, 0, offset, null, 0); offset = 0;
        }
        hash.TransformFinalBlock(buffer, 0, offset); return Hex(hash.Hash);
    }
    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
}
