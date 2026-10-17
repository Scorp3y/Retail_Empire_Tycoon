using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    internal enum TownArchitectureBalcony { OpenRail, SolidPanel }

    /// <summary>One building recipe owns its solids, entrances and supported details. No scene/global state is used.</summary>
    internal sealed class TownArchitectureRecipe
    {
        private readonly TownMeshBuilder geometry = new(TownArchitecturePalette.UV);
        private readonly List<TownArchitectureVolume> volumes = new();
        private readonly List<TownArchitectureAttachment> attachments = new();
        private readonly List<TownArchitectureEntrance> entrances = new();
        private readonly List<TownColor> walls = new();
        private readonly List<TownColor> roofs = new();
        private readonly List<bool> ramps = new();
        private readonly List<(int Volume, float X, string Label)> storefronts = new();
        private TownArchitectureBalcony balcony;
        private int balconySpacing = 3;
        public List<Rect> Openings { get; } = new();

        public void AddWing(Vector2 centre, Vector2 size, int floors, TownColor wall, TownColor roof,
            TownArchitectureRoofShape shape = TownArchitectureRoofShape.Flat, float rise = 0,
            float floorHeight = 3.1f, Vector2? roofOverhang = null)
        {
            volumes.Add(new TownArchitectureVolume(centre, size, floors, floorHeight, shape, rise, roofOverhang));
            walls.Add(wall);
            roofs.Add(roof);
        }

        public void AddEntrance(int volumeIndex, float x, Vector3 outward, bool ramp)
        {
            var body = volumes[volumeIndex].BodyBounds;
            if (outward != Vector3.forward) throw new ArgumentException("These library recipes place their entrances on the +Z facade.");
            entrances.Add(new TownArchitectureEntrance(new Vector3(x, 0, body.max.z), outward, volumeIndex));
            ramps.Add(ramp);
        }

        public void DecorateWindows(TownArchitectureBalcony style, int spacing)
        {
            if (spacing < 2) throw new ArgumentOutOfRangeException(nameof(spacing));
            balcony = style;
            balconySpacing = spacing;
        }

        public void AddFacadeStripe(int volumeIndex, float x, TownColor color)
        {
            Bounds body = volumes[volumeIndex].BodyBounds;
            // Narrow vertical bays vary the long facade without changing the solid/collision footprint.
            geometry.Box(new Vector3(x, body.size.y / 2, body.max.z + .028f), new Vector3(2.6f, body.size.y, .06f), color);
        }

        public void AddStorefront(int volumeIndex, float x, string label) => storefronts.Add((volumeIndex, x, label));

        public void AddBackAirConditioners(int volumeIndex, int count)
        {
            Bounds body = volumes[volumeIndex].BodyBounds;
            for (int i = 0; i < count; i++)
            {
                float x = body.center.x + (i - (count - 1) / 2f) * 3.7f;
                // The band between apartment floors is solid wall, unlike the window-centre grid.
                float y = volumes[volumeIndex].FloorHeight + .18f;
                var center = new Vector3(x, y, body.min.z - .26f);
                int columns=Mathf.Max(1,Mathf.FloorToInt(body.size.x/3.1f));float pitch=body.size.x/columns;
                for(int floor=0;floor<volumes[volumeIndex].Floors;floor++)for(int column=0;column<columns;column++)
                {
                    float windowX=body.center.x+(column-(columns-1)/2f)*pitch,windowY=floor*volumes[volumeIndex].FloorHeight+1.7f;
                    if(Mathf.Abs(windowX-x)<1.35f&&y-.475f<windowY+.855f&&y+.325f>windowY-.975f)
                        throw new InvalidOperationException("A condenser or its mounting bracket would overlap a rear window.");
                }
                TownArchitectureParts.AirConditioner(geometry, center, Quaternion.Euler(0, 180, 0));
            }
        }

        public void AddRoofDetail(int volumeIndex, string kind, Vector2 position, Vector2 size)
        {
            var roof = volumes[volumeIndex];
            const float embed = .035f;
            Vector3 center = new(position.x, 0, position.y);
            float surface = roof.SurfaceHeightAt(center);
            float seat = TownArchitectureParts.RoofSeat(geometry, roof, center, size, embed);
            attachments.Add(new TownArchitectureAttachment($"{kind}_{volumeIndex}_{attachments.Count}",
                new Vector3(position.x, surface - embed, position.y), size, surface, volumeIndex, true, embed));
            Vector3 top = new(position.x, seat, position.y);
            switch (kind)
            {
                case "Chimney": TownArchitectureParts.Chimney(geometry, top, size); break;
                case "Vent": TownArchitectureParts.Vent(geometry, top, size); break;
                case "RoofHatch": TownArchitectureParts.RoofHatch(geometry, top, size); break;
                case "Antenna": TownArchitectureParts.Antenna(geometry, top); break;
                case "ElevatorHousing": TownArchitectureParts.ElevatorHousing(geometry, top, size, roofs[volumeIndex]); break;
                case "Dormer": TownArchitectureParts.Dormer(geometry, top, size, walls[volumeIndex], roofs[volumeIndex]); break;
                default: throw new ArgumentException($"Unsupported roof detail: {kind}", nameof(kind));
            }
        }

        public TownArchitectureModel Finish()
        {
            for (int index = 0; index < volumes.Count; index++)
            {
                TownArchitectureParts.Shell(geometry, volumes[index], volumes, walls[index], roofs[index]);
                AddWindows(index);
                TownArchitectureParts.Downpipes(geometry, volumes[index], volumes);
            }
            for (int i = 0; i < entrances.Count; i++) TownArchitectureParts.Entrance(geometry, entrances[i], ramps[i]);
            foreach (var storefront in storefronts)
                TownArchitectureParts.Storefront(geometry, new Vector3(storefront.X, 0, volumes[storefront.Volume].BodyBounds.max.z), storefront.Label);
            return new TownArchitectureModel(geometry, volumes.ToArray(), attachments.ToArray(), entrances.ToArray(), Openings.ToArray());
        }

        private void AddWindows(int volumeIndex)
        {
            var volume = volumes[volumeIndex];
            Bounds body = volume.BodyBounds;
            foreach (Vector3 outward in TownArchitectureParts.FacadeNormals)
            {
                Quaternion rotation = Quaternion.LookRotation(outward, Vector3.up);
                bool front = outward == Vector3.forward;
                float width = Mathf.Abs(outward.z) > .5f ? body.size.x : body.size.z;
                float depth = Mathf.Abs(outward.z) > .5f ? body.size.z : body.size.x;
                int columns = Mathf.Max(1, Mathf.FloorToInt(width / 3.1f));
                float pitch = width / columns;
                for (int floor = 0; floor < volume.Floors; floor++) for (int column = 0; column < columns; column++)
                {
                    Vector3 local = new((column - (columns - 1) / 2f) * pitch, floor * volume.FloorHeight + 1.7f, depth / 2 + .06f);
                    Vector3 center = new Vector3(body.center.x, 0, body.center.z) + rotation * local;
                    if (!TownArchitectureParts.IsExposed(center, outward, volumeIndex, volumes)) continue;
                    if (floor == 0 && front && GroundOpeningAt(volumeIndex, center.x)) continue;
                    bool stairBay = front && entrances.Exists(e=>e.VolumeIndex==volumeIndex&&Mathf.Abs(e.Position.x-center.x)<(body.size.x<9?.85f:1.65f));
                    if(stairBay)continue;
                    bool hasBalcony = front && floor > 0 && (column + floor + volumeIndex) % balconySpacing == 0
                        && !entrances.Exists(e=>e.VolumeIndex==volumeIndex&&Mathf.Abs(e.Position.x-center.x)<2.5f);
                    TownArchitectureParts.Window(geometry, center, 1.35f, hasBalcony ? 1.95f : 1.55f, rotation);
                    if (hasBalcony) TownArchitectureParts.Balcony(geometry, center + rotation * new Vector3(0, -1.12f, .04f), balcony, rotation);
                }
            }
            // A separate half-landing rhythm makes the staircase legible; no apartment balconies here.
            foreach(var entrance in entrances)
            {
                if(entrance.VolumeIndex!=volumeIndex)continue;
                for(int landing=0;landing<volume.Floors-1;landing++)
                {
                    var center=new Vector3(entrance.Position.x,3.65f+landing*volume.FloorHeight,body.max.z+.06f);
                    if(TownArchitectureParts.IsExposed(center,Vector3.forward,volumeIndex,volumes))
                        TownArchitectureParts.Window(geometry,center,1.0f,1.0f,Quaternion.identity);
                }
            }
        }

        private bool GroundOpeningAt(int volumeIndex, float x)
        {
            foreach (var entrance in entrances)
                if (entrance.VolumeIndex == volumeIndex && Mathf.Abs(entrance.Position.x - x) < 1.7f) return true;
            foreach (var storefront in storefronts)
                if (storefront.Volume == volumeIndex && Mathf.Abs(storefront.X - x) < 3.4f) return true;
            return false;
        }
    }

    /// <summary>Architecture-specific mesh parts. Legacy world assets and their material are deliberately untouched.</summary>
    internal static class TownArchitectureParts
    {
        internal static readonly Vector3[] FacadeNormals = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        public static void Shell(TownMeshBuilder mesh, TownArchitectureVolume volume,
            IReadOnlyList<TownArchitectureVolume> neighbours, TownColor wall, TownColor roof)
        {
            Bounds body = volume.BodyBounds;
            mesh.Box(body.center, body.size, wall);
            foreach (Vector3 outward in FacadeNormals)
            {
                FacadeBand(mesh, volume, neighbours, outward, .25f, .5f, .1f, TownColor.RoofDark, .01f);
                FacadeBand(mesh, volume, neighbours, outward, body.max.y - .12f, .22f, .14f, TownColor.Cream, body.max.y - .1f);
                if (volume.RoofShape == TownArchitectureRoofShape.Flat)
                    FacadeBand(mesh, volume, neighbours, outward, volume.EaveHeight + .27f, .54f, .16f, wall, body.max.y - .01f);
            }
            var roofRect = volume.RoofFootprint;
            if (volume.RoofShape == TownArchitectureRoofShape.Flat)
                mesh.Box(new Vector3(body.center.x, volume.EaveHeight - .1f, body.center.z), new Vector3(roofRect.width, .2f, roofRect.height), roof);
            else if (volume.RoofShape == TownArchitectureRoofShape.Gable)
                mesh.Gable(new Vector3(body.center.x, volume.EaveHeight, body.center.z), roofRect.width, roofRect.height, volume.Rise, roof, wall);
            else MansardRoof(mesh, volume, roof, wall);
        }

        private static void FacadeBand(TownMeshBuilder mesh, TownArchitectureVolume volume,
            IReadOnlyList<TownArchitectureVolume> neighbours, Vector3 outward, float y, float height, float depth,
            TownColor color, float probeY)
        {
            Bounds body = volume.BodyBounds;
            Quaternion rotation = Quaternion.LookRotation(outward, Vector3.up);
            Vector3 tangent = rotation * Vector3.right;
            float width = Mathf.Abs(outward.z) > .5f ? body.size.x : body.size.z;
            float normalDepth = Mathf.Abs(outward.z) > .5f ? body.size.z : body.size.x;
            Vector3 origin = new Vector3(body.center.x, probeY, body.center.z) + outward * (normalDepth / 2 + .025f);
            var spans = new List<Vector2> { new(-width / 2, width / 2) };
            foreach (var neighbour in neighbours)
            {
                if (ReferenceEquals(neighbour, volume)) continue;
                Bounds other = neighbour.BodyBounds;
                if (probeY < other.min.y - .001f || probeY > other.max.y + .001f) continue;
                bool liesOnWall = Mathf.Abs(outward.z) > .5f ? origin.z > other.min.z && origin.z < other.max.z : origin.x > other.min.x && origin.x < other.max.x;
                if (!liesOnWall) continue;
                float a = Vector3.Dot(other.min - origin, tangent);
                float b = Vector3.Dot(other.max - origin, tangent);
                SubtractSpan(spans, Mathf.Min(a, b), Mathf.Max(a, b));
            }
            foreach (var span in spans)
            {
                Vector3 center = new Vector3(body.center.x, y, body.center.z) + outward * (normalDepth / 2 + depth / 2) + tangent * ((span.x + span.y) / 2);
                mesh.Box(center, new Vector3(span.y - span.x, height, depth), color, rotation);
            }
        }

        private static void SubtractSpan(List<Vector2> spans, float start, float end)
        {
            for (int index = spans.Count - 1; index >= 0; index--)
            {
                Vector2 span = spans[index];
                if (end <= span.x || start >= span.y) continue;
                spans.RemoveAt(index);
                if (start > span.x + .001f) spans.Add(new Vector2(span.x, Mathf.Min(start, span.y)));
                if (end < span.y - .001f) spans.Add(new Vector2(Mathf.Max(end, span.x), span.y));
            }
        }

        public static bool IsExposed(Vector3 center, Vector3 outward, int ownIndex, IReadOnlyList<TownArchitectureVolume> volumes)
        {
            Vector3 probe = center + outward * .04f;
            for (int index = 0; index < volumes.Count; index++)
                if (index != ownIndex && volumes[index].BodyBounds.Contains(probe)) return false;
            return true;
        }

        private static void MansardRoof(TownMeshBuilder mesh, TownArchitectureVolume volume, TownColor roof, TownColor wall)
        {
            Rect rect = volume.RoofFootprint;
            float half = rect.width / 2;
            float[] profileX = { -half, -half * .42f, -half * .18f, half * .18f, half * .42f, half };
            Vector3 Front(int i) => new(rect.center.x + profileX[i], volume.SurfaceHeightAt(new Vector3(rect.center.x + profileX[i], 0, rect.center.y)), rect.yMax);
            Vector3 Back(int i) => new(Front(i).x, Front(i).y, rect.yMin);
            Vector3 frontBase = new(rect.xMin, volume.EaveHeight, rect.yMax);
            Vector3 backBase = new(rect.xMin, volume.EaveHeight, rect.yMin);
            // Profile is convex; the base-left fan produces outward front/back gable faces.
            for (int i = 1; i < profileX.Length - 1; i++)
            {
                mesh.Triangle(frontBase, Front(i + 1), Front(i), wall);
                mesh.Triangle(backBase, Back(i), Back(i + 1), wall);
            }
            for (int i = 0; i < profileX.Length - 1; i++) mesh.Quad(Back(i), Front(i), Front(i + 1), Back(i + 1), roof);
            mesh.Quad(new Vector3(rect.xMin, volume.EaveHeight, rect.yMin), new Vector3(rect.xMax, volume.EaveHeight, rect.yMin),
                new Vector3(rect.xMax, volume.EaveHeight, rect.yMax), new Vector3(rect.xMin, volume.EaveHeight, rect.yMax), roof);
        }

        public static void Window(TownMeshBuilder mesh, Vector3 center, float width, float height, Quaternion rotation)
        {
            mesh.Box(center, new Vector3(width + .16f, height + .16f, .08f), TownColor.White, rotation);
            Vector3 Point(float x, float y) => center + rotation * new Vector3(x, y, .045f);
            mesh.Quad(Point(-width / 2, -height / 2), Point(width / 2, -height / 2), Point(width / 2, height / 2), Point(-width / 2, height / 2), TownColor.Glass);
            mesh.Box(center + rotation * new Vector3(0, 0, .06f), new Vector3(.045f, height, .035f), TownColor.Frame, rotation);
            mesh.Box(center + rotation * new Vector3(0, -height / 2 - .09f, .08f), new Vector3(width + .3f, .12f, .22f), TownColor.Pavement, rotation);
        }

        public static void Balcony(TownMeshBuilder mesh, Vector3 center, TownArchitectureBalcony style, Quaternion rotation)
        {
            void Box(Vector3 offset, Vector3 size, TownColor color) => mesh.Box(center + rotation * offset, size, color, rotation);
            Box(new Vector3(0, 0, .55f), new Vector3(2, .16f, 1.1f), TownColor.Pavement);
            if (style == TownArchitectureBalcony.SolidPanel)
            {
                Box(new Vector3(0, .5f, 1.05f), new Vector3(2, .85f, .14f), TownColor.PlasterWarm);
                foreach (float x in new[] { -.94f, .94f }) Box(new Vector3(x, .5f, .55f), new Vector3(.12f, .85f, 1.1f), TownColor.PlasterWarm);
                Box(new Vector3(0, .96f, 1.05f), new Vector3(2.1f, .08f, .2f), TownColor.RoofDark);
            }
            else
            {
                Box(new Vector3(0, 1, 1.05f), new Vector3(2, .055f, .055f), TownColor.Metal);
                for (int i = 0; i < 5; i++) Box(new Vector3((i - 2) * .46f, .5f, 1.05f), new Vector3(.045f, 1, .045f), TownColor.Metal);
                foreach (float x in new[] { -.95f, .95f }) Box(new Vector3(x, 1, .55f), new Vector3(.055f, .055f, 1.05f), TownColor.Metal);
            }
        }

        public static void Entrance(TownMeshBuilder mesh, TownArchitectureEntrance entrance, bool ramp)
        {
            Quaternion rotation = Quaternion.LookRotation(entrance.Outward, Vector3.up);
            void Box(Vector3 offset, Vector3 size, TownColor color) => mesh.Box(entrance.Position + rotation * offset, size, color, rotation);
            Box(new Vector3(0, 1.48f, .06f), new Vector3(1.65f, 2.48f, .14f), TownColor.White);
            Box(new Vector3(0, 1.48f, .145f), new Vector3(1.38f, 2.32f, .06f), TownColor.Frame);
            Box(new Vector3(0, 1.75f, .19f), new Vector3(1.15f, 1.45f, .035f), TownColor.Glass);
            Box(new Vector3(.46f, 1.3f, .22f), new Vector3(.055f, .28f, .05f), TownColor.Metal);
            Box(new Vector3(0, .14f, .46f), new Vector3(2.1f, .28f, .92f), TownColor.Pavement);
            Box(new Vector3(0, .07f, 1.17f), new Vector3(2.1f, .14f, .52f), TownColor.Pavement);
            Box(new Vector3(0, 2.91f, .6f), new Vector3(2.5f, .14f, 1.4f), TownColor.RoofDark);
            foreach (float x in new[] { -.91f, .91f }) Box(new Vector3(x, 2.75f, .32f), new Vector3(.07f, .24f, .65f), TownColor.Metal);
            if (ramp) Ramp(mesh, entrance.Position + rotation * new Vector3(1.65f, 0, .2f), rotation);
        }

        private static void Ramp(TownMeshBuilder mesh, Vector3 center, Quaternion rotation)
        {
            Vector3 P(float x, float y, float z) => center + rotation * new Vector3(x, y, z);
            mesh.Quad(P(-.55f, .28f, 0), P(.55f, .28f, 0), P(.55f, .03f, 2.7f), P(-.55f, .03f, 2.7f), TownColor.Pavement);
            mesh.Quad(P(-.55f, 0, 0), P(-.55f, 0, 2.7f), P(-.55f, .03f, 2.7f), P(-.55f, .28f, 0), TownColor.Pavement);
            mesh.Quad(P(.55f, 0, 2.7f), P(.55f, 0, 0), P(.55f, .28f, 0), P(.55f, .03f, 2.7f), TownColor.Pavement);
            mesh.Quad(P(.55f, 0, 0), P(-.55f, 0, 0), P(-.55f, .28f, 0), P(.55f, .28f, 0), TownColor.Pavement);
            mesh.Quad(P(-.55f, 0, 2.7f), P(.55f, 0, 2.7f), P(.55f, .03f, 2.7f), P(-.55f, .03f, 2.7f), TownColor.Pavement);
        }

        public static void Storefront(TownMeshBuilder mesh, Vector3 ground, string label)
        {
            Window(mesh, ground + new Vector3(0, 1.28f, .12f), 5.6f, 2.1f, Quaternion.identity);
            mesh.Box(ground + new Vector3(0, 2.78f, .46f), new Vector3(6.1f, .14f, .9f), TownColor.RoofDark);
            TownArchitecturalParts.Sign(mesh, ground + new Vector3(0, 2.45f, .22f), 5.55f, .45f, label);
        }

        public static void Downpipes(TownMeshBuilder mesh, TownArchitectureVolume volume, IReadOnlyList<TownArchitectureVolume> volumes)
        {
            Bounds body = volume.BodyBounds;
            foreach (float x in new[] { body.min.x + .16f, body.max.x - .16f })
            {
                Vector3 point = new(x, body.size.y / 2, body.min.z - .12f);
                bool exposed = true;
                foreach (var other in volumes)
                    if (!ReferenceEquals(other, volume) && other.BodyBounds.Contains(point)) exposed = false;
                if (!exposed) continue;
                mesh.Cylinder(point, .055f, .055f, body.size.y, TownColor.Metal, 6);
                mesh.Box(new Vector3(x, .1f, body.min.z - .19f), new Vector3(.12f, .2f, .25f), TownColor.Metal);
            }
        }

        public static void AirConditioner(TownMeshBuilder mesh, Vector3 center, Quaternion rotation)
        {
            mesh.Box(center, new Vector3(1.05f, .65f, .45f), TownColor.PlasterWhite, rotation);
            mesh.Cylinder(center + rotation * Vector3.forward * .24f, .23f, .23f, .035f, TownColor.Frame, 8, rotation * Quaternion.Euler(90, 0, 0));
            foreach (float x in new[] { -.35f, .35f })
                mesh.Box(center + rotation * new Vector3(x, -.4f, -.07f), new Vector3(.08f, .15f, .52f), TownColor.Metal, rotation);
        }

        /// <summary>Seats every footprint corner in the sampled roof, so pitched-roof details cannot float.</summary>
        public static float RoofSeat(TownMeshBuilder mesh, TownArchitectureVolume roof, Vector3 center, Vector2 size, float embed)
        {
            Vector3 Corner(float x, float z)
            {
                Vector3 point = new(center.x + x * size.x / 2, 0, center.z + z * size.y / 2);
                point.y = roof.SurfaceHeightAt(point) - embed;
                return point;
            }
            Vector3[] bottom = { Corner(-1, 1), Corner(1, 1), Corner(1, -1), Corner(-1, -1) };
            float topY = 0;
            foreach (var point in bottom) topY = Mathf.Max(topY, point.y + embed + .055f);
            Vector3[] top = new Vector3[4];
            for (int i = 0; i < 4; i++) top[i] = new Vector3(bottom[i].x, topY, bottom[i].z);
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                mesh.Quad(bottom[i], bottom[next], top[next], top[i], TownColor.Metal);
            }
            mesh.Quad(top[0], top[1], top[2], top[3], TownColor.Metal);
            mesh.Quad(bottom[3], bottom[2], bottom[1], bottom[0], TownColor.Metal);
            return topY;
        }

        public static void Chimney(TownMeshBuilder mesh, Vector3 seat, Vector2 size)
        {
            mesh.Box(seat + Vector3.up * .55f, new Vector3(size.x * .86f, 1.1f, size.y * .86f), TownColor.BrickLight);
            mesh.Box(seat + Vector3.up * 1.14f, new Vector3(size.x + .12f, .14f, size.y + .12f), TownColor.RoofDark);
            mesh.Cylinder(seat + Vector3.up * 1.31f, .14f, .14f, .22f, TownColor.Metal, 6);
        }

        public static void Vent(TownMeshBuilder mesh, Vector3 seat, Vector2 size)
        {
            mesh.Box(seat + Vector3.up * .32f, new Vector3(size.x * .86f, .64f, size.y * .86f), TownColor.Metal);
            for (int i = 0; i < 4; i++) mesh.Box(seat + new Vector3(0, .67f, (i - 1.5f) * size.y / 4),
                new Vector3(size.x + .08f, .04f, .05f), TownColor.Frame);
        }

        public static void RoofHatch(TownMeshBuilder mesh, Vector3 seat, Vector2 size)
        {
            mesh.Box(seat + Vector3.up * .16f, new Vector3(size.x, .32f, size.y), TownColor.Frame);
            mesh.Box(seat + Vector3.up * .34f, new Vector3(size.x - .13f, .06f, size.y - .13f), TownColor.Metal);
            mesh.Box(seat + new Vector3(0, .4f, size.y * .2f), new Vector3(.35f, .06f, .05f), TownColor.Frame);
        }

        public static void Antenna(TownMeshBuilder mesh, Vector3 seat)
        {
            mesh.Cylinder(seat + Vector3.up * 1.2f, .035f, .028f, 2.4f, TownColor.Metal, 6);
            for (int i = 0; i < 3; i++) mesh.Box(seat + new Vector3(0, 1.55f + i * .28f, 0),
                new Vector3(1.45f - i * .22f, .035f, .04f), TownColor.Metal);
            mesh.Box(seat + new Vector3(0, .65f, 0), new Vector3(.04f, .04f, .8f), TownColor.Metal);
        }

        public static void ElevatorHousing(TownMeshBuilder mesh, Vector3 seat, Vector2 size, TownColor roof)
        {
            mesh.Box(seat + Vector3.up * .92f, new Vector3(size.x, 1.84f, size.y), TownColor.PlasterGrey);
            mesh.Box(seat + Vector3.up * 1.94f, new Vector3(size.x + .14f, .2f, size.y + .14f), roof);
            mesh.Box(seat + new Vector3(0, .83f, size.y / 2 + .04f), new Vector3(.85f, 1.6f, .08f), TownColor.Frame);
            for (int i = 0; i < 3; i++) mesh.Box(seat + new Vector3(0, 1.08f + i * .13f, size.y / 2 + .09f), new Vector3(.65f, .04f, .03f), TownColor.Metal);
        }

        public static void Dormer(TownMeshBuilder mesh, Vector3 seat, Vector2 size, TownColor wall, TownColor roof)
        {
            mesh.Box(seat + Vector3.up * .63f, new Vector3(size.x, 1.26f, size.y), wall);
            mesh.Gable(seat + Vector3.up * 1.26f, size.x + .12f, size.y + .12f, .52f, roof, wall);
            Window(mesh, seat + new Vector3(0, .71f, size.y / 2 + .055f), 1.13f, .88f, Quaternion.identity);
        }
    }
}
