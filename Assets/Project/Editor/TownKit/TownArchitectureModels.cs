using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    internal enum TownArchitectureRoofShape { Flat, Gable, Mansard }

    /// <summary>A reusable native-mesh recipe. Building it never changes a scene or creates assets.</summary>
    internal sealed class TownArchitectureDefinition
    {
        private readonly Func<TownArchitectureModel> build;
        public string Id { get; }
        public string Label { get; }
        public string Family { get; }

        public TownArchitectureDefinition(string id, string label, string family, Func<TownArchitectureModel> build)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(family))
                throw new ArgumentException("Architecture definitions need an ID, label and family.");
            Id = id;
            Label = label;
            Family = family;
            this.build = build ?? throw new ArgumentNullException(nameof(build));
        }

        public TownArchitectureModel Build()
        {
            var model = build() ?? throw new InvalidOperationException($"{Id}: recipe returned no geometry.");
            model.Validate();
            return model;
        }
    }

    /// <summary>A solid wing, not an enclosing box around an L/U courtyard. Coordinates are metres, ground Y=0.</summary>
    internal sealed class TownArchitectureVolume
    {
        public Bounds BodyBounds { get; }
        public int Floors { get; }
        public float FloorHeight { get; }
        public TownArchitectureRoofShape RoofShape { get; }
        public float EaveHeight { get; }
        public float Rise { get; }
        public Rect RoofFootprint { get; }

        public TownArchitectureVolume(Vector2 centre, Vector2 size, int floors, float floorHeight,
            TownArchitectureRoofShape roofShape, float rise = 0, Vector2? roofOverhang = null)
        {
            if (!Finite(centre.x) || !Finite(centre.y) || !Finite(size.x) || !Finite(size.y) || size.x <= 0 || size.y <= 0)
                throw new ArgumentException("Architecture body dimensions must be finite and positive.");
            if (floors < 1 || floors > 12 || !Finite(floorHeight) || floorHeight < 2.8f || floorHeight > 3.2f)
                throw new ArgumentException("Architecture floors must be 1..12 with a 2.8..3.2 metre floor height.");
            if (!Enum.IsDefined(typeof(TownArchitectureRoofShape), roofShape) || !Finite(rise) || rise < 0 ||
                (roofShape == TownArchitectureRoofShape.Flat ? rise != 0 : rise <= 0))
                throw new ArgumentException("A flat roof has zero rise; a pitched roof needs a positive rise.");
            Vector2 overhang = roofOverhang ?? new Vector2(.2f, .2f);
            if (!Finite(overhang.x) || !Finite(overhang.y) || overhang.x < 0 || overhang.y < 0 || overhang.x > .5f || overhang.y > .5f)
                throw new ArgumentException("Roof overhang must be finite and between zero and half a metre.");
            Floors = floors;
            FloorHeight = floorHeight;
            RoofShape = roofShape;
            Rise = rise;
            float height = floors * floorHeight;
            BodyBounds = new Bounds(new Vector3(centre.x, height / 2, centre.y), new Vector3(size.x, height, size.y));
            EaveHeight = height + (roofShape == TownArchitectureRoofShape.Flat ? .2f : .05f);
            RoofFootprint = new Rect(centre.x - size.x / 2 - overhang.x, centre.y - size.y / 2 - overhang.y,
                size.x + overhang.x * 2, size.y + overhang.y * 2);
        }

        /// <summary>Height of the real roof mesh at local X/Z. Throws outside its support footprint.</summary>
        public float SurfaceHeightAt(Vector3 position)
        {
            if (!Finite(position.x) || !Finite(position.z) || !ContainsRoof(position.x, position.z))
                throw new ArgumentOutOfRangeException(nameof(position), "The point is outside this roof.");
            if (RoofShape == TownArchitectureRoofShape.Flat) return EaveHeight;
            float distance = Mathf.Clamp01(Mathf.Abs(position.x - BodyBounds.center.x) / (RoofFootprint.width / 2));
            if (RoofShape == TownArchitectureRoofShape.Gable) return EaveHeight + Rise * (1 - distance);
            // The same break points are used by the mansard mesh, not an approximation of a gable.
            if (distance <= .18f) return EaveHeight + Rise;
            if (distance <= .42f) return EaveHeight + Rise * Mathf.Lerp(1, .78f, (distance - .18f) / .24f);
            return EaveHeight + Rise * .78f * (1 - distance) / .58f;
        }

        internal bool ContainsRoof(float x, float z) => x >= RoofFootprint.xMin - .001f && x <= RoofFootprint.xMax + .001f &&
            z >= RoofFootprint.yMin - .001f && z <= RoofFootprint.yMax + .001f;
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Roof-mounted detail, including its actual support footprint and small embedded skirt.</summary>
    internal sealed class TownArchitectureAttachment
    {
        public string Id { get; }
        public Vector3 BasePosition { get; }
        public Vector2 BaseSize { get; }
        public float SurfaceHeight { get; }
        public int RoofVolumeIndex { get; }
        public bool ConformsToRoof { get; }
        public float EmbedDepth { get; }

        public TownArchitectureAttachment(string id, Vector3 basePosition, Vector2 baseSize, float surfaceHeight,
            int roofVolumeIndex, bool conformsToRoof = false, float embedDepth = 0)
        {
            if (string.IsNullOrWhiteSpace(id) || !Finite(basePosition) || !TownArchitectureVolume.Finite(baseSize.x) ||
                !TownArchitectureVolume.Finite(baseSize.y) || baseSize.x <= 0 || baseSize.y <= 0 ||
                !TownArchitectureVolume.Finite(surfaceHeight) || roofVolumeIndex < 0 ||
                !TownArchitectureVolume.Finite(embedDepth) || embedDepth < 0 || embedDepth > .1f)
                throw new ArgumentException("Roof detail support data must be finite, positive and refer to a volume.");
            Id = id;
            BasePosition = basePosition;
            BaseSize = baseSize;
            SurfaceHeight = surfaceHeight;
            RoofVolumeIndex = roofVolumeIndex;
            ConformsToRoof = conformsToRoof;
            EmbedDepth = embedDepth;
        }

        internal static bool Finite(Vector3 position) => TownArchitectureVolume.Finite(position.x) &&
            TownArchitectureVolume.Finite(position.y) && TownArchitectureVolume.Finite(position.z);
    }

    internal sealed class TownArchitectureEntrance
    {
        public Vector3 Position { get; }
        public Vector3 Outward { get; }
        public int VolumeIndex { get; }

        public TownArchitectureEntrance(Vector3 position, Vector3 outward, int volumeIndex)
        {
            if (!TownArchitectureAttachment.Finite(position) || !TownArchitectureAttachment.Finite(outward) ||
                Mathf.Abs(position.y) > .001f || Mathf.Abs(outward.y) > .001f || outward.sqrMagnitude < .9f || volumeIndex < 0)
                throw new ArgumentException("Entrances need a ground position, horizontal outward normal and volume index.");
            Position = position;
            Outward = outward.normalized;
            VolumeIndex = volumeIndex;
        }
    }

    /// <summary>Geometry and protected placement metadata; saving meshes/prefabs belongs to the generation pipeline.</summary>
    internal sealed class TownArchitectureModel
    {
        private readonly TownArchitectureVolume[] volumes;
        private readonly TownArchitectureAttachment[] attachments;
        private readonly TownArchitectureEntrance[] entrances;
        private readonly Rect[] courtyardOpenings;
        public TownMeshBuilder Geometry { get; }
        public TownArchitectureVolume[] Volumes => (TownArchitectureVolume[])volumes.Clone();
        public TownArchitectureAttachment[] Attachments => (TownArchitectureAttachment[])attachments.Clone();
        public TownArchitectureEntrance[] Entrances => (TownArchitectureEntrance[])entrances.Clone();
        public Rect[] CourtyardOpenings => (Rect[])courtyardOpenings.Clone();
        public float FootprintArea { get; }

        public TownArchitectureModel(TownMeshBuilder geometry, TownArchitectureVolume[] volumes,
            TownArchitectureAttachment[] attachments, TownArchitectureEntrance[] entrances, Rect[] courtyardOpenings = null)
        {
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            this.volumes = volumes == null ? throw new ArgumentNullException(nameof(volumes)) : (TownArchitectureVolume[])volumes.Clone();
            this.attachments = attachments == null ? throw new ArgumentNullException(nameof(attachments)) : (TownArchitectureAttachment[])attachments.Clone();
            this.entrances = entrances == null ? throw new ArgumentNullException(nameof(entrances)) : (TownArchitectureEntrance[])entrances.Clone();
            this.courtyardOpenings = courtyardOpenings == null ? Array.Empty<Rect>() : (Rect[])courtyardOpenings.Clone();
            foreach (var volume in this.volumes)
            {
                if (volume == null) throw new ArgumentException("Architecture volumes cannot contain null.");
                FootprintArea += volume.BodyBounds.size.x * volume.BodyBounds.size.z;
            }
            Validate();
        }

        public void Validate()
        {
            if (volumes.Length == 0 || entrances.Length == 0) throw new InvalidOperationException("Architecture needs solids and accessible entrances.");
            for (int i = 0; i < volumes.Length; i++)
            {
                Bounds body = volumes[i].BodyBounds;
                if (Mathf.Abs(body.min.y) > .001f || Mathf.Abs(body.size.y - volumes[i].Floors * volumes[i].FloorHeight) > .001f)
                    throw new InvalidOperationException("Body ground and floor heights are inconsistent.");
                for (int j = i + 1; j < volumes.Length; j++)
                    if (OverlapXZ(body, volumes[j].BodyBounds)) throw new InvalidOperationException("Solid wing footprints overlap.");
            }
            foreach (var opening in courtyardOpenings)
            {
                if (opening.width <= 0 || opening.height <= 0 || !TownArchitectureVolume.Finite(opening.x) || !TownArchitectureVolume.Finite(opening.y))
                    throw new InvalidOperationException("Courtyard openings must be positive finite rectangles.");
                foreach (var volume in volumes)
                    if (OverlapXZ(volume.BodyBounds, new Bounds(new Vector3(opening.center.x, 1, opening.center.y),
                        new Vector3(opening.width, 2, opening.height)))) throw new InvalidOperationException("A courtyard is occupied by a solid wing.");
            }
            foreach (var attachment in attachments)
            {
                if (attachment == null || attachment.RoofVolumeIndex >= volumes.Length) throw new InvalidOperationException("Roof detail refers to a missing wing.");
                var roof = volumes[attachment.RoofVolumeIndex];
                if (Mathf.Abs(roof.SurfaceHeightAt(attachment.BasePosition) - attachment.SurfaceHeight) > .001f ||
                    Mathf.Abs(attachment.BasePosition.y - attachment.SurfaceHeight + attachment.EmbedDepth) > .001f)
                    throw new InvalidOperationException("Roof attachment is not seated at its declared surface.");
                foreach (float xSign in new[] { -1f, 1f }) foreach (float zSign in new[] { -1f, 1f })
                {
                    Vector3 corner = attachment.BasePosition + new Vector3(xSign * attachment.BaseSize.x / 2, 0, zSign * attachment.BaseSize.y / 2);
                    float height = roof.SurfaceHeightAt(corner);
                    if (!attachment.ConformsToRoof && Mathf.Abs(height - attachment.SurfaceHeight) > .025f)
                        throw new InvalidOperationException("A rigid flat detail cannot hover above a pitched roof.");
                }
            }
            foreach (var entrance in entrances)
            {
                if (entrance == null || entrance.VolumeIndex >= volumes.Length) throw new InvalidOperationException("Entrance refers to a missing wing.");
                Bounds body = volumes[entrance.VolumeIndex].BodyBounds;
                Vector3 wallProbe = entrance.Position - entrance.Outward * .025f + Vector3.up;
                if (!body.Contains(wallProbe)) throw new InvalidOperationException("Entrance is not on its declared wall.");
                Vector3 outsideProbe = entrance.Position + entrance.Outward * .3f + Vector3.up;
                foreach (var volume in volumes)
                    if (volume.BodyBounds.Contains(outsideProbe)) throw new InvalidOperationException("Entrance opens into another solid wing.");
            }
        }

        private static bool OverlapXZ(Bounds a, Bounds b) => Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x) > .001f &&
            Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z) > .001f;
    }

    internal static class TownArchitectureModels
    {
        public static IReadOnlyList<TownArchitectureDefinition> Create() => Array.AsReadOnly(new[]
        {
            new TownArchitectureDefinition("LCorner3", "Угловой дом, 3 этажа", "LCorner", () => LCorner(3)),
            new TownArchitectureDefinition("LCorner5", "Угловой дом, 5 этажей", "LCorner", () => LCorner(5)),
            new TownArchitectureDefinition("Sectional3", "Секционный дом, 3 секции", "Sectional", () => Sectional(3, 3)),
            new TownArchitectureDefinition("Sectional5", "Секционный дом, 5 секций", "Sectional", () => Sectional(5, 5)),
            new TownArchitectureDefinition("TownhouseRow3", "Ряд таунхаусов, 3 секции", "TownhouseRow", () => TownhouseRow(3)),
            new TownArchitectureDefinition("TownhouseRow6", "Ряд таунхаусов, 6 секций", "TownhouseRow", () => TownhouseRow(6)),
            new TownArchitectureDefinition("UCourtyard4", "Дом с открытым двором, 4 этажа", "UCourtyard", UCourtyard),
            new TownArchitectureDefinition("Stepped469", "Ступенчатый дом, 4–6–9 этажей", "Stepped", Stepped),
            new TownArchitectureDefinition("Mansard3", "Дом с мансардой, 3 этажа", "Mansard", Mansard)
        });

        private static TownArchitectureModel LCorner(int floors)
        {
            var recipe = new TownArchitectureRecipe();
            recipe.AddWing(new Vector2(0, -7), new Vector2(28, 10), floors, TownColor.BrickLight, TownColor.RoofSlate);
            recipe.AddWing(new Vector2(-9, 5), new Vector2(10, 14), floors, TownColor.PlasterWarm, TownColor.RoofSlate);
            recipe.Openings.Add(new Rect(-4, -2, 18, 14));
            recipe.AddEntrance(0, 4, Vector3.forward, false);
            recipe.AddEntrance(1, -9, Vector3.forward, true);
            recipe.DecorateWindows(floors == 3 ? TownArchitectureBalcony.OpenRail : TownArchitectureBalcony.SolidPanel, 2);
            recipe.AddRoofDetail(0, "Vent", new Vector2(7, -9), new Vector2(1.1f, .8f));
            if (floors == 5) recipe.AddRoofDetail(1, "ElevatorHousing", new Vector2(-9, 2), new Vector2(2.5f, 3));
            else recipe.AddRoofDetail(1, "Antenna", new Vector2(-9, 4), new Vector2(.45f, .45f));
            recipe.AddBackAirConditioners(0, 2);
            return recipe.Finish();
        }

        private static TownArchitectureModel Sectional(int sections, int floors)
        {
            float width = sections * 10 + 4;
            var recipe = new TownArchitectureRecipe();
            recipe.AddWing(Vector2.zero, new Vector2(width, 12), floors, TownColor.PlasterGrey,
                sections == 3 ? TownColor.RoofZinc : TownColor.RoofSlate);
            for (int i = 0; i < sections; i++)
            {
                float x = (i - (sections - 1) / 2f) * 10;
                recipe.AddEntrance(0, x, Vector3.forward, i == 0);
                recipe.AddFacadeStripe(0, x, i % 2 == 0 ? TownColor.PlasterWhite : TownColor.PlasterWarm);
            }
            recipe.DecorateWindows(TownArchitectureBalcony.SolidPanel, 3);
            recipe.AddRoofDetail(0, "RoofHatch", new Vector2(-width * .23f, -2), new Vector2(1.25f, 1.4f));
            recipe.AddRoofDetail(0, "Vent", new Vector2(width * .25f, -2.5f), new Vector2(1.2f, .9f));
            if (sections == 5) recipe.AddRoofDetail(0, "ElevatorHousing", new Vector2(0, 0), new Vector2(2.8f, 3.2f));
            recipe.AddBackAirConditioners(0, sections == 3 ? 2 : 3);
            return recipe.Finish();
        }

        private static TownArchitectureModel TownhouseRow(int units)
        {
            var recipe = new TownArchitectureRecipe();
            TownColor[] walls = { TownColor.Brick, TownColor.BrickDeep, TownColor.PlasterWhite };
            for (int i = 0; i < units; i++)
            {
                float x = (i - (units - 1) / 2f) * 6;
                recipe.AddWing(new Vector2(x, 0), new Vector2(6, 12), 3, walls[i % walls.Length], TownColor.RoofTerracotta,
                    TownArchitectureRoofShape.Gable, 1.8f, 2.9f, new Vector2(0, .2f));
                recipe.AddEntrance(i, x, Vector3.forward, false);
                if (i % 2 == 0) recipe.AddRoofDetail(i, "Chimney", new Vector2(x - 1.4f, -2.8f), new Vector2(.75f, .8f));
            }
            recipe.DecorateWindows(TownArchitectureBalcony.OpenRail, 4);
            recipe.AddBackAirConditioners(units - 1, 1);
            return recipe.Finish();
        }

        private static TownArchitectureModel UCourtyard()
        {
            var recipe = new TownArchitectureRecipe();
            recipe.AddWing(new Vector2(0, -10), new Vector2(40, 10), 4, TownColor.PlasterWhite, TownColor.RoofZinc);
            recipe.AddWing(new Vector2(-15, 5), new Vector2(10, 20), 4, TownColor.PlasterWarm, TownColor.RoofZinc);
            recipe.AddWing(new Vector2(15, 5), new Vector2(10, 20), 4, TownColor.PlasterGrey, TownColor.RoofZinc);
            recipe.Openings.Add(new Rect(-10, -5, 20, 20));
            recipe.AddEntrance(0, 0, Vector3.forward, true);
            recipe.AddEntrance(1, -15, Vector3.forward, false);
            recipe.AddEntrance(2, 15, Vector3.forward, false);
            recipe.DecorateWindows(TownArchitectureBalcony.OpenRail, 3);
            recipe.AddRoofDetail(0, "ElevatorHousing", new Vector2(3, -10), new Vector2(2.6f, 3));
            recipe.AddRoofDetail(1, "RoofHatch", new Vector2(-15, 1), new Vector2(1.3f, 1.4f));
            recipe.AddRoofDetail(2, "Vent", new Vector2(15, -1), new Vector2(1.1f, .8f));
            recipe.AddBackAirConditioners(0, 2);
            return recipe.Finish();
        }

        private static TownArchitectureModel Stepped()
        {
            var recipe = new TownArchitectureRecipe();
            int[] floors = { 4, 6, 9 };
            TownColor[] walls = { TownColor.PlasterWarm, TownColor.PlasterGrey, TownColor.PlasterWhite };
            for (int i = 0; i < floors.Length; i++)
            {
                float x = (i - 1) * 12;
                recipe.AddWing(new Vector2(x, 0), new Vector2(12, 14), floors[i], walls[i], TownColor.RoofSlate);
                recipe.AddEntrance(i, x, Vector3.forward, i == 0);
            }
            recipe.DecorateWindows(TownArchitectureBalcony.SolidPanel, 4);
            recipe.AddRoofDetail(0, "Vent", new Vector2(-12, -2), new Vector2(1.2f, .9f));
            recipe.AddRoofDetail(1, "RoofHatch", new Vector2(0, -2.5f), new Vector2(1.3f, 1.4f));
            recipe.AddRoofDetail(2, "ElevatorHousing", new Vector2(12, -1.5f), new Vector2(2.8f, 3));
            recipe.AddRoofDetail(2, "Antenna", new Vector2(15, 3), new Vector2(.45f, .45f));
            recipe.AddBackAirConditioners(1, 2);
            return recipe.Finish();
        }

        private static TownArchitectureModel Mansard()
        {
            var recipe = new TownArchitectureRecipe();
            recipe.AddWing(Vector2.zero, new Vector2(22, 12), 3, TownColor.BrickDeep, TownColor.RoofSlate,
                TownArchitectureRoofShape.Mansard, 2.6f);
            recipe.AddEntrance(0, -6, Vector3.forward, false);
            recipe.AddStorefront(0, 4, "ЦВЕТЫ");
            recipe.DecorateWindows(TownArchitectureBalcony.OpenRail, 3);
            recipe.AddRoofDetail(0, "Dormer", new Vector2(-6.5f, 3.8f), new Vector2(1.9f, 2));
            recipe.AddRoofDetail(0, "Dormer", new Vector2(6.5f, 3.8f), new Vector2(1.9f, 2));
            recipe.AddRoofDetail(0, "Chimney", new Vector2(-2.7f, -2.7f), new Vector2(.85f, .9f));
            recipe.AddBackAirConditioners(0, 1);
            return recipe.Finish();
        }

        /// <summary>Small negative-contract checks used by the asset-only QA entry point.</summary>
        public static void ValidateNegativeContracts()
        {
            void Rejected(Action action)
            {
                try { action(); }
                catch (ArgumentException) { return; }
                catch (InvalidOperationException) { return; }
                throw new InvalidOperationException("An invalid architecture specification was accepted.");
            }
            Rejected(() => new TownArchitectureVolume(Vector2.zero, new Vector2(-1, 12), 3, 3.1f, TownArchitectureRoofShape.Flat));
            Rejected(() => new TownArchitectureVolume(Vector2.zero, new Vector2(12, 12), 0, 3.1f, TownArchitectureRoofShape.Flat));
            Rejected(() => new TownArchitectureVolume(Vector2.zero, new Vector2(12, 12), 3, float.NaN, TownArchitectureRoofShape.Flat));
            var roof = new TownArchitectureVolume(Vector2.zero, new Vector2(12, 12), 3, 3.1f, TownArchitectureRoofShape.Flat);
            var door = new TownArchitectureEntrance(new Vector3(0, 0, 6), Vector3.forward, 0);
            Rejected(() => new TownArchitectureModel(new TownMeshBuilder(TownArchitecturePalette.UV), new[] { roof },
                new[] { new TownArchitectureAttachment("InvalidIndex", new Vector3(0, roof.EaveHeight, 0), Vector2.one, roof.EaveHeight, 1) }, new[] { door }));
            Rejected(() => new TownArchitectureModel(new TownMeshBuilder(TownArchitecturePalette.UV), new[] { roof },
                new[] { new TownArchitectureAttachment("OutsideRoof", new Vector3(5.9f, roof.EaveHeight, 0), new Vector2(2, 2), roof.EaveHeight, 0) }, new[] { door }));
        }
    }
}
