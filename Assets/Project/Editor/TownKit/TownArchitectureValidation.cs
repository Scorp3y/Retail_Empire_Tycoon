using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>Asset-only acceptance of native geometry and the future placement/collision contracts.</summary>
    internal static class TownArchitectureValidation
    {
        private const float PositionTolerance = .005f;
        private const string LegacySourceColorsHash = "77916F1F5E19D2118CB02B61A4E8A58A90CE507222DBF0A7C687A6210B6FFE16";
        private static readonly string[] LegacyNames =
        {
            "Cream", "Brick", "Mint", "RoofDark", "Frame", "Glass", "White", "Asphalt",
            "Pavement", "Wood", "WoodLight", "Leaf", "LeafLight", "LeafDark", "Trunk", "Grass",
            "Yellow", "Orange", "Red", "Blue", "Box", "BoxDark", "Metal", "Rubber",
            "Flowers", "Sand", "Stone", "Soil", "GreenStripe", "Light", "Pink", "Charcoal"
        };

        /// <summary>Checks the already-built mesh against its authored metadata; never grounds or repairs it.</summary>
        public static void Validate(TownArchitectureDefinition definition, TownArchitectureModel model, Mesh mesh, Material material)
        {
            Require(definition != null && model != null, "Missing architectural definition or model.");
            string id = definition.Id;
            Require(!string.IsNullOrWhiteSpace(id), "Architecture ID is empty.");
            model.Validate();
            TownArchitectureModels.ValidateNegativeContracts();
            ValidateMaterial(material);
            ValidateMesh(mesh, id);
            Require(model.Volumes != null && model.Volumes.Length > 0, id + ": no collision-wing contract.");
            var wings = new List<Rect>();
            foreach (var volume in model.Volumes)
            {
                var body = volume.BodyBounds;
                Require(Finite(body.center) && Finite(body.size) && body.size.x > 0 && body.size.y > 0 && body.size.z > 0,
                    id + ": invalid native building-body bounds.");
                Require(volume.Floors > 0 && Finite(volume.FloorHeight) && volume.FloorHeight >= 2.8f && volume.FloorHeight <= 3.2f,
                    id + ": floor dimensions are not native 2.8–3.2 metre architectural storeys.");
                Require(body.min.y >= -PositionTolerance && body.size.y >= volume.Floors * volume.FloorHeight - PositionTolerance,
                    id + ": body height does not contain its declared storeys.");
                var footprint = Rect.MinMaxRect(body.min.x, body.min.z, body.max.x, body.max.z);
                wings.Add(footprint);
                Require(Finite(volume.EaveHeight) && Finite(volume.Rise) && volume.Rise >= 0 && Finite(volume.RoofFootprint),
                    id + ": non-finite roof specification.");
                foreach (var sample in Samples(footprint))
                {
                    float height = volume.SurfaceHeightAt(new Vector3(sample.x, 0, sample.y));
                    Require(Finite(height) && height >= body.max.y - .2f,
                        id + ": roof profile leaves its supporting body.");
                    Require(HasRoofSurface(mesh, sample, height), id + ": declared roof has no actual supporting surface at " + sample);
                }
            }
            float unionArea = UnionArea(wings);
            Require(Finite(model.FootprintArea) && Mathf.Abs(model.FootprintArea - unionArea) < .05f,
                id + ": footprint area must be the union of body wings, not the enclosing rectangle or overlapping-area sum.");
            if (definition.Family == "LCorner" || definition.Family == "UCourtyard")
                Require(wings.Count >= (definition.Family == "LCorner" ? 2 : 3) && model.CourtyardOpenings.Length > 0,
                    id + ": non-rectangular buildings require separate solid wings and an explicitly open courtyard.");
            ValidateCourtyards(model, mesh, wings, id);
            ValidateRoofAttachments(model, mesh, id);
            ValidateEntrances(model, wings, id);
            // Quarter-turns are used by the town. Area and open courtyard contracts must survive every orientation.
            for (int quarter = 1; quarter < 4; quarter++)
            {
                var rotation = Quaternion.Euler(0, quarter * 90, 0);
                var turnedWings = wings.Select(rect => RotatedRect(rect, rotation)).ToList();
                Require(Mathf.Abs(UnionArea(turnedWings) - unionArea) < .05f, id + ": quarter-turn changes its collision footprint.");
                foreach (var courtyard in model.CourtyardOpenings)
                    Require(!turnedWings.Any(wing => Overlaps(wing, RotatedRect(courtyard, rotation))),
                        id + ": rotated wing collision fills an open courtyard.");
            }
        }

        /// <summary>Ensures the saved prefab is native, purely visual and shares the one kit material.</summary>
        public static void ValidateSaved(GameObject prefab, Material material)
        {
            Require(prefab != null, "Missing saved architecture prefab.");
            Require(prefab.transform.localPosition == Vector3.zero && prefab.transform.localRotation == Quaternion.identity
                && prefab.transform.localScale == Vector3.one, prefab.name + ": prefab root must preserve its grounded native pivot and metre scale.");
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Require(filters.Length == 1 && renderers.Length == 1 && renderers[0] is MeshRenderer
                && renderers[0].sharedMaterials.Length == 1 && renderers[0].sharedMaterial == material,
                prefab.name + ": expected one native mesh and the same shared architecture material.");
            Require(prefab.GetComponentsInChildren<Collider>(true).Length == 0 && prefab.GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
                prefab.name + ": stage-two assets must not introduce runtime features or collision objects into the city.");
            ValidateMaterial(material);
            ValidateMesh(filters[0].sharedMesh, prefab.name);
        }

        public static void ValidateMaterial(Material material)
        {
            ValidateLegacyContract();
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/TownWorld/TownPalette.mat");
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(TownArchitecturePalette.TexturePath);
            Require(material != null && source != null && atlas != null
                && AssetDatabase.GetAssetPath(material) == TownArchitecturePalette.MaterialPath
                && material.shader == source.shader && material.enableInstancing && material.GetTexture("_BaseMap") == atlas
                && material.GetFloat("_PaletteIndex") == -1 && material.GetColor("_BaseColor") == source.GetColor("_BaseColor"),
                "Architecture must share its new native material without changing the original shader or world tint.");
            Require(atlas.isReadable && atlas.width == 32 && atlas.height == 3 && atlas.mipmapCount == 1
                && atlas.filterMode == FilterMode.Point && atlas.wrapMode == TextureWrapMode.Clamp,
                "Architecture atlas dimensions or sampling contract changed.");
            var legacy = TownArchitecturePalette.ReadLegacyColors();
            var pixels = atlas.GetPixels32();
            for (int column = 0; column < 32; column++)
                Require(pixels[32 + column].Equals(legacy[column]), "Architecture atlas altered legacy colour " + column + " at its original UV.");
            var exported = AssetDatabase.LoadAssetAtPath<Texture2D>(TownArchitecturePalette.PngPath);
            Require(exported != null && exported.isReadable && exported.width == 32 && exported.height == 3
                && exported.GetPixels32().SequenceEqual(pixels), "OBJ palette export differs from the native atlas.");
        }

        /// <summary>Golden contract catches renumbering, UV renormalisation and accidental global recolouring.</summary>
        public static void ValidateLegacyContract()
        {
            Require(TownPalette.Colors.Length == 32, "Legacy town palette must remain exactly 32 colours.");
            var bytes = TownPalette.Colors.SelectMany(color => new[] { color.r, color.g, color.b, color.a }).ToArray();
            using (var hash = SHA256.Create())
                Require(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "") == LegacySourceColorsHash,
                    "Legacy source-kit colour values were changed by the architecture extension.");
            for (int index = 0; index < 32; index++)
            {
                var color = (TownColor)index;
                var expected = new Vector2((index + .5f) / 32, .5f);
                Require(Enum.GetName(typeof(TownColor), color) == LegacyNames[index]
                    && TownPalette.UV(color) == expected && TownArchitecturePalette.UV(color) == expected,
                    "Legacy enum ID or UV changed at slot " + index + ".");
            }
            bool rejected = false;
            try { new TownMeshBuilder().Triangle(Vector3.zero, Vector3.right, Vector3.up, TownColor.BrickLight); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected, "The default mesh builder silently accepted an architecture colour on the old atlas.");
            rejected = false;
            try { TownArchitecturePalette.UV((TownColor)40); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected, "An unknown extended colour silently resolved to an atlas cell.");
        }

        private static void ValidateMesh(Mesh mesh, string id)
        {
            Require(mesh != null && mesh.vertexCount > 0 && mesh.subMeshCount == 1 && mesh.triangles.Length / 3 <= 12000
                && mesh.normals.Length == mesh.vertexCount && mesh.uv.Length == mesh.vertexCount,
                id + ": empty, excessive or incomplete single-material geometry.");
            Require(Finite(mesh.bounds.center) && Finite(mesh.bounds.size) && Mathf.Abs(mesh.bounds.min.y) <= .0001f,
                id + ": non-finite bounds or floating/sunken ground pivot; automatic grounding is not a repair.");
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            var knownUv = Enum.GetValues(typeof(TownColor)).Cast<TownColor>().Select(TownArchitecturePalette.UV).ToArray();
            for (int index = 0; index < vertices.Length; index++)
                Require(Finite(vertices[index]) && Finite(normals[index]) && Mathf.Abs(normals[index].magnitude - 1) < .001f
                    && Finite(uv[index]) && knownUv.Any(cell => (cell - uv[index]).sqrMagnitude < 1e-12f),
                    id + ": non-finite vertex/normal or UV outside a declared palette-cell centre.");
            var triangles = mesh.triangles;
            Require(triangles.Length % 3 == 0, id + ": incomplete triangle indices.");
            for (int index = 0; index < triangles.Length; index += 3)
            {
                int a = triangles[index], b = triangles[index + 1], c = triangles[index + 2];
                Require(a >= 0 && a < vertices.Length && b >= 0 && b < vertices.Length && c >= 0 && c < vertices.Length,
                    id + ": triangle index leaves the vertex array.");
                var cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                Require(cross.sqrMagnitude >= 1e-12f && Vector3.Dot(cross.normalized, normals[a]) > .99f
                    && uv[a] == uv[b] && uv[a] == uv[c], id + ": degenerate/reversed triangle or interpolated palette colours.");
            }
        }

        private static void ValidateCourtyards(TownArchitectureModel model, Mesh mesh, List<Rect> wings, string id)
        {
            Require(model.CourtyardOpenings != null, id + ": missing open-courtyard contract.");
            foreach (var courtyard in model.CourtyardOpenings)
            {
                Require(Finite(courtyard) && courtyard.width > 0 && courtyard.height > 0
                    && !wings.Any(wing => Overlaps(wing, courtyard)), id + ": a body-wing collision volume fills its declared courtyard.");
                foreach (var sample in Samples(courtyard))
                    Require(!HasSurfaceAbove(mesh, sample, .35f), id + ": actual geometry bridges/fills the open courtyard at " + sample);
            }
        }

        private static void ValidateRoofAttachments(TownArchitectureModel model, Mesh mesh, string id)
        {
            Require(model.Attachments != null, id + ": missing roof-attachment contract.");
            var vertices = mesh.vertices;
            foreach (var attachment in model.Attachments)
            {
                Require(attachment.RoofVolumeIndex >= 0 && attachment.RoofVolumeIndex < model.Volumes.Length
                    && Finite(attachment.BasePosition) && Finite(attachment.BaseSize) && attachment.BaseSize.x > 0 && attachment.BaseSize.y > 0
                    && Finite(attachment.EmbedDepth) && attachment.EmbedDepth >= 0 && attachment.EmbedDepth <= .1f,
                    id + ": invalid attachment support specification: " + attachment.Id);
                var volume = model.Volumes[attachment.RoofVolumeIndex];
                float surface = volume.SurfaceHeightAt(attachment.BasePosition);
                Require(Finite(attachment.SurfaceHeight) && Mathf.Abs(attachment.SurfaceHeight - surface) < PositionTolerance
                    && Mathf.Abs(attachment.BasePosition.y - (surface - attachment.EmbedDepth)) < PositionTolerance,
                    id + ": attachment anchor does not meet its actual roof profile: " + attachment.Id);
                foreach (float x in new[] { -.5f, .5f }) foreach (float z in new[] { -.5f, .5f })
                {
                    var corner = attachment.BasePosition + new Vector3(x * attachment.BaseSize.x, 0, z * attachment.BaseSize.y);
                    Require(Contains(volume.RoofFootprint, corner), id + ": roof attachment overhangs its supporting wing: " + attachment.Id);
                    float roofHeight = volume.SurfaceHeightAt(corner);
                    Require(HasRoofSurface(mesh, new Vector2(corner.x, corner.z), roofHeight),
                        id + ": roof attachment corner has no real support: " + attachment.Id);
                    float baseHeight = attachment.ConformsToRoof ? roofHeight - attachment.EmbedDepth : attachment.BasePosition.y;
                    Require(baseHeight <= roofHeight + PositionTolerance && baseHeight >= roofHeight - attachment.EmbedDepth - PositionTolerance,
                        id + ": attachment floats or penetrates its roof incorrectly: " + attachment.Id);
                    var baseCorner = new Vector3(corner.x, baseHeight, corner.z);
                    Require(vertices.Any(vertex => (vertex - baseCorner).sqrMagnitude < PositionTolerance * PositionTolerance),
                        id + ": declared attachment skirt/foot does not exist in its real mesh: " + attachment.Id);
                }
            }
        }

        private static void ValidateEntrances(TownArchitectureModel model, List<Rect> wings, string id)
        {
            Require(model.Entrances != null && model.Entrances.Length > 0, id + ": no authored entrance anchors.");
            foreach (var entrance in model.Entrances)
            {
                Require(Finite(entrance.Position) && Finite(entrance.Outward) && Mathf.Abs(entrance.Outward.y) < .001f
                    && Mathf.Abs(entrance.Outward.magnitude - 1) < .001f, id + ": invalid entrance anchor or outward direction.");
                var approach = entrance.Position + entrance.Outward * .8f;
                Require(!wings.Any(wing => Contains(wing, approach)), id + ": entrance approach points into another body wing.");
            }
        }

        private static IEnumerable<Vector2> Samples(Rect rectangle)
        {
            foreach (float x in new[] { .25f, .5f, .75f }) foreach (float z in new[] { .25f, .5f, .75f })
                yield return new Vector2(Mathf.Lerp(rectangle.xMin, rectangle.xMax, x), Mathf.Lerp(rectangle.yMin, rectangle.yMax, z));
        }

        private static float UnionArea(List<Rect> rectangles)
        {
            var x = rectangles.SelectMany(rectangle => new[] { rectangle.xMin, rectangle.xMax }).Distinct().OrderBy(value => value).ToArray();
            var z = rectangles.SelectMany(rectangle => new[] { rectangle.yMin, rectangle.yMax }).Distinct().OrderBy(value => value).ToArray();
            float area = 0;
            for (int column = 1; column < x.Length; column++) for (int row = 1; row < z.Length; row++)
            {
                var centre = new Vector2((x[column - 1] + x[column]) / 2, (z[row - 1] + z[row]) / 2);
                if (rectangles.Any(rectangle => rectangle.Contains(centre))) area += (x[column] - x[column - 1]) * (z[row] - z[row - 1]);
            }
            return area;
        }

        private static Rect RotatedRect(Rect rectangle, Quaternion rotation)
        {
            var corners = new[] { new Vector3(rectangle.xMin, 0, rectangle.yMin), new Vector3(rectangle.xMin, 0, rectangle.yMax),
                new Vector3(rectangle.xMax, 0, rectangle.yMin), new Vector3(rectangle.xMax, 0, rectangle.yMax) }.Select(point => rotation * point).ToArray();
            return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.z), corners.Max(point => point.x), corners.Max(point => point.z));
        }

        private static bool HasRoofSurface(Mesh mesh, Vector2 point, float expectedHeight)
        {
            var vertices = mesh.vertices;
            var uv = mesh.uv;
            var triangles = mesh.triangles;
            var roofUv = new[] { TownColor.RoofDark, TownColor.Mint, TownColor.RoofSlate, TownColor.RoofTerracotta, TownColor.RoofZinc }
                .Select(TownArchitecturePalette.UV).ToArray();
            for (int index = 0; index < triangles.Length; index += 3)
            {
                int a = triangles[index], b = triangles[index + 1], c = triangles[index + 2];
                if (!roofUv.Contains(uv[a]) || Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).y <= 1e-6f) continue;
                if (SurfaceHeight(point, vertices[a], vertices[b], vertices[c], out float height)
                    && Mathf.Abs(height - expectedHeight) <= PositionTolerance) return true;
            }
            return false;
        }

        private static bool HasSurfaceAbove(Mesh mesh, Vector2 point, float minimumHeight)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            for (int index = 0; index < triangles.Length; index += 3)
                if (SurfaceHeight(point, vertices[triangles[index]], vertices[triangles[index + 1]], vertices[triangles[index + 2]], out float height)
                    && height > minimumHeight) return true;
            return false;
        }

        private static bool SurfaceHeight(Vector2 point, Vector3 a, Vector3 b, Vector3 c, out float height)
        {
            float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            height = 0;
            if (Mathf.Abs(denominator) < 1e-8f) return false;
            float first = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.y - c.z)) / denominator;
            float second = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.y - c.z)) / denominator;
            float third = 1 - first - second;
            if (first < -.0001f || second < -.0001f || third < -.0001f) return false;
            height = first * a.y + second * b.y + third * c.y;
            return true;
        }

        private static bool Contains(Rect rectangle, Vector3 point) => point.x >= rectangle.xMin - PositionTolerance
            && point.x <= rectangle.xMax + PositionTolerance && point.z >= rectangle.yMin - PositionTolerance && point.z <= rectangle.yMax + PositionTolerance;
        private static bool Overlaps(Rect first, Rect second) => Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin) > .001f
            && Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin) > .001f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Rect value) => Finite(value.x) && Finite(value.y) && Finite(value.width) && Finite(value.height);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
