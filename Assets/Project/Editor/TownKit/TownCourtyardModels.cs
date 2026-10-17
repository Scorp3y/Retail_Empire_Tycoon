using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    internal enum TownCourtyardRole
    {
        PlayEquipment, FitnessEquipment, BasketballHoop, Seating, Pergola, Planting, Refuse, Lighting, Fence
    }

    /// <summary>
    /// Native metre-scale courtyard furniture. Reservations belong to the layout;
    /// only Solids may become physical colliders. No people or gameplay behaviours.
    /// </summary>
    internal static class TownCourtyardModels
    {
        public static IReadOnlyList<TownCourtyardModel> Create()
        {
            var existing = TownDetailModels.Create().ToDictionary(model => model.Id);
            TownAssetDefinition litterBin = TownAssetCatalogue.Create().Single(model => model.Id == "LitterBin");
            var models = new List<TownCourtyardModel>();

            void Reuse(string id, string sourceId, string label, TownCourtyardRole role, Vector2 reservation,
                Vector3 access, List<TownCourtyardSolid> solids, Vector3 scale)
            {
                TownAssetDefinition source = existing[sourceId];
                models.Add(new TownCourtyardModel(id, label, role, () => source.Build(),
                    reservation, access, solids, scale));
            }

            Reuse("PlaySlide", "PlaySlide", "Горка с лестницей", TownCourtyardRole.PlayEquipment, new Vector2(4, 8),
                new Vector3(0, 0, -4), SlideSolids(), Vector3.one);
            Reuse("SwingSet", "SwingSet", "Двойные качели", TownCourtyardRole.PlayEquipment, new Vector2(7, 7),
                new Vector3(3.5f, 0, 0), SwingSolids(), Vector3.one);
            Reuse("Sandpit", "Sandpit", "Песочница с бортами", TownCourtyardRole.PlayEquipment, new Vector2(5.5f, 4.5f),
                new Vector3(0, 0, 2.25f), SandpitSolids(), Vector3.one);
            Reuse("OutdoorGym", "OutdoorGym", "Турники и параллельные брусья", TownCourtyardRole.FitnessEquipment, new Vector2(7, 8),
                new Vector3(-3.5f, 0, 0), GymSolids(), Vector3.one);
            Reuse("QuietPergola", "Pergola", "Открытая пергола со скамьёй", TownCourtyardRole.Pergola, new Vector2(6.5f, 5.5f),
                new Vector3(0, 0, 2.75f), PergolaSolids(), new Vector3(6f / 6.8f, 1, 5f / 4.2f));
            Reuse("BinEnclosure", "BinEnclosure", "Контейнеры за деревянным экраном", TownCourtyardRole.Refuse, new Vector2(6, 5),
                new Vector3(0, 0, 2.5f), BinSolids(), Vector3.one);

            models.Add(new TownCourtyardModel("Bench", "Скамья — исходный размер ×1.2", TownCourtyardRole.Seating,
                () => TownProps.Create(TownProps.Bench), new Vector2(3, 2),
                new Vector3(0, 0, 1), BenchSolids(), Vector3.one * 1.2f));
            models.Add(new TownCourtyardModel("FlowerBed", "Клумба с каменным бортом", TownCourtyardRole.Planting,
                FlowerBed, new Vector2(3.2f, 1.8f), new Vector3(0, 0, .9f),
                new List<TownCourtyardSolid> { Box(Vector3.up * .21f, new Vector3(2.6f, .42f, 1.2f)) }, Vector3.one));
            models.Add(new TownCourtyardModel("WasteContainer", "Контейнер — исходный размер ×1.2", TownCourtyardRole.Refuse,
                TownProps.WasteContainer, new Vector2(2.6f, 2.5f), new Vector3(0, 0, 1.25f),
                WasteSolids(), Vector3.one * 1.2f));
            models.Add(new TownCourtyardModel("LitterBin", "Небольшая урна — исходный размер ×1.2", TownCourtyardRole.Refuse,
                litterBin.Build, new Vector2(1.2f, 1.2f), new Vector3(0, 0, .6f),
                new List<TownCourtyardSolid>
                {
                    Box(new Vector3(0, .42f, 0), new Vector3(.54f, .84f, .54f)),
                    Box(new Vector3(0, .86f, 0), new Vector3(.6f, .08f, .6f)),
                    Box(new Vector3(0, .905f, 0), new Vector3(.3f, .012f, .3f))
                }, Vector3.one * 1.2f));
            models.Add(new TownCourtyardModel("CourtyardLamp", "Трёхметровый дворовый фонарь", TownCourtyardRole.Lighting,
                Lamp, new Vector2(1.2f, 1.2f), new Vector3(.6f, 0, 0), LampSolids(), Vector3.one));
            models.Add(new TownCourtyardModel("HedgeModule", "Трёхметровый модуль живой изгороди", TownCourtyardRole.Planting,
                Hedge, new Vector2(3.4f, 1.4f), new Vector3(0, 0, .7f),
                new List<TownCourtyardSolid> { Box(Vector3.up * .5f, new Vector3(3, 1, .85f)) }, Vector3.one));
            models.Add(new TownCourtyardModel("FenceModule", "Трёхметровая открытая ограда", TownCourtyardRole.Fence,
                Fence, new Vector2(3.3f, .8f), new Vector3(0, 0, .4f), FenceSolids(), Vector3.one));
            models.Add(new TownCourtyardModel("MiniCourt", "Одиночный щит и баскетбольное кольцо", TownCourtyardRole.BasketballHoop,
                MiniCourt, new Vector2(2, 2), new Vector3(0, 0, 1), MiniCourtSolids(), Vector3.one));
            return models.AsReadOnly();
        }

        /// <summary>Checks generated geometry without saving assets or mutating a scene.</summary>
        public static void ValidateCatalogue(IReadOnlyList<TownCourtyardModel> models)
        {
            if (models == null || models.Count == 0)
                throw new InvalidOperationException("The courtyard catalogue is empty.");
            if (models.Select(model => model.Id).Distinct().Count() != models.Count)
                throw new InvalidOperationException("Courtyard model IDs must be unique.");

            foreach (TownCourtyardModel model in models)
            {
                Mesh mesh = model.BuildMesh();
                try
                {
                    if (mesh.vertexCount == 0 || mesh.triangles.Length == 0 || mesh.subMeshCount != 1)
                        throw new InvalidOperationException(model.Id + ": expected one non-empty palette mesh.");
                    if (Mathf.Abs(mesh.bounds.min.y) > .0001f)
                        throw new InvalidOperationException(model.Id + ": model is not grounded at zero.");
                    if (mesh.uv.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount)
                        throw new InvalidOperationException(model.Id + ": incomplete vertex attributes.");
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        if (!Finite(vertex.x) || !Finite(vertex.y) || !Finite(vertex.z))
                            throw new InvalidOperationException(model.Id + ": a vertex is not finite.");
                    }
                    foreach (Vector2 uv in mesh.uv)
                    {
                        float swatch = uv.x * 32 - .5f;
                        if (Mathf.Abs(uv.y - .5f) > .0001f || swatch < -.0001f || swatch > 31.0001f ||
                            Mathf.Abs(swatch - Mathf.Round(swatch)) > .0001f)
                            throw new InvalidOperationException(model.Id + ": UV must address the existing 32-colour atlas.");
                    }
                    Bounds visual = model.VisualBounds;
                    visual.Expand(.01f);
                    foreach (TownCourtyardSolid solid in model.Solids)
                    {
                        if (!visual.Contains(solid.Bounds.min) || !visual.Contains(solid.Bounds.max))
                            throw new InvalidOperationException(model.Id + ": a collision proxy extends past its physical model.");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static TownCourtyardSolid Box(Vector3 centre, Vector3 size)
            => new TownCourtyardSolid(centre, size, Quaternion.identity);

        private static TownCourtyardSolid Beam(Vector3 from, Vector3 to, float width)
            => new TownCourtyardSolid((from + to) * .5f, new Vector3(width, (to - from).magnitude, width),
                Quaternion.FromToRotation(Vector3.up, (to - from).normalized));

        private static List<TownCourtyardSolid> SlideSolids()
        {
            var solids = new List<TownCourtyardSolid>();
            foreach (float x in new[] { -.7f, .7f })
            {
                foreach (float z in new[] { -.6f, .6f })
                    solids.Add(Box(new Vector3(x, 1, z), new Vector3(.12f, 2, .12f)));
                solids.Add(Beam(new Vector3(x, 1.9f, .65f), new Vector3(x, .2f, 3.3f), .12f));
            }
            solids.Add(Box(new Vector3(0, 1.85f, 0), new Vector3(1.7f, .15f, 1.5f)));
            solids.Add(new TownCourtyardSolid(new Vector3(0, 1.05f, 1.97f), new Vector3(1.25f, .08f, 3.15f), Quaternion.Euler(33, 0, 0)));
            foreach (float x in new[] { -.55f, .55f })
                solids.Add(Beam(new Vector3(x, 0, -2), new Vector3(x, 1.9f, -.65f), .09f));
            for (int i = 1; i < 6; i++)
                solids.Add(Beam(new Vector3(-.55f, i * .3f, -2 + i * .21f), new Vector3(.55f, i * .3f, -2 + i * .21f), .07f));
            return solids;
        }

        private static List<TownCourtyardSolid> SwingSolids()
        {
            var solids = new List<TownCourtyardSolid>();
            foreach (float x in new[] { -2.5f, 2.5f })
                foreach (float z in new[] { -1.3f, 1.3f })
                    solids.Add(Beam(new Vector3(x, 0, z), new Vector3(x, 2.7f, 0), .11f));
            solids.Add(Beam(new Vector3(-2.6f, 2.7f, 0), new Vector3(2.6f, 2.7f, 0), .15f));
            foreach (float x in new[] { -1.2f, 1.2f })
            {
                solids.Add(Box(new Vector3(x, .55f, 0), new Vector3(.8f, .12f, .4f)));
                foreach (float side in new[] { -.3f, .3f })
                    solids.Add(Beam(new Vector3(x + side, .55f, 0), new Vector3(x + side, 2.7f, 0), .025f));
            }
            return solids;
        }

        private static List<TownCourtyardSolid> SandpitSolids()
        {
            return new List<TownCourtyardSolid>
            {
                Box(new Vector3(0, .12f, 0), new Vector3(4, .24f, 3)),
                Box(new Vector3(-1.6f, .29f, 0), new Vector3(.7f, .1f, 3)),
                Box(new Vector3(1.6f, .29f, 0), new Vector3(.7f, .1f, 3))
            };
        }

        private static List<TownCourtyardSolid> GymSolids()
        {
            var solids = new List<TownCourtyardSolid>();
            for (int i = 0; i < 3; i++)
                solids.Add(Box(new Vector3(-2 + i * 2, 1.2f + i * .2f, 0), new Vector3(.14f, 2.4f + i * .4f, .14f)));
            solids.Add(Beam(new Vector3(-2, 2.4f, 0), new Vector3(0, 2.4f, 0), .09f));
            solids.Add(Beam(new Vector3(0, 2.8f, 0), new Vector3(2, 2.8f, 0), .09f));
            for (int i = 0; i < 6; i++)
                solids.Add(Beam(new Vector3(0, .3f + i * .35f, 0), new Vector3(2, .3f + i * .35f, 0), .06f));
            foreach (float x in new[] { -1f, 1f })
            {
                solids.Add(Box(new Vector3(x, .6f, 2), new Vector3(.12f, 1.2f, .12f)));
                solids.Add(Beam(new Vector3(x, .9f, 1.5f), new Vector3(x, .9f, 2.5f), .08f));
            }
            return solids;
        }

        private static List<TownCourtyardSolid> PergolaSolids()
        {
            var solids = new List<TownCourtyardSolid>();
            foreach (float x in new[] { -3f, 3f })
                foreach (float z in new[] { -1.7f, 1.7f })
                    solids.Add(Box(new Vector3(x, 1.4f, z), new Vector3(.18f, 2.8f, .18f)));
            foreach (float z in new[] { -1.7f, 1.7f })
                solids.Add(Box(new Vector3(0, 2.85f, z), new Vector3(6.6f, .2f, .16f)));
            for (int i = 0; i < 9; i++)
                solids.Add(Box(new Vector3(-3.2f + i * .8f, 3, 0), new Vector3(.14f, .16f, 4.2f)));
            solids.Add(Box(new Vector3(0, .5f, -1), new Vector3(4, .12f, .65f)));
            foreach (float x in new[] { -1.5f, 1.5f })
                solids.Add(Box(new Vector3(x, .25f, -1), new Vector3(.12f, .5f, .6f)));
            return solids;
        }

        private static List<TownCourtyardSolid> BinSolids()
        {
            var solids = new List<TownCourtyardSolid>();
            foreach (float x in new[] { -2.4f, 2.4f })
                solids.Add(Box(new Vector3(x, .85f, 0), new Vector3(.14f, 1.7f, 3.4f)));
            for (int i = 0; i < 7; i++)
                solids.Add(Box(new Vector3(0, .2f + i * .24f, -1.6f), new Vector3(4.9f, .13f, .12f)));
            foreach (float x in new[] { -1.25f, 1.25f })
            {
                solids.Add(Box(new Vector3(x, .65f, -.35f), new Vector3(1.3f, 1.1f, 1.2f)));
                solids.Add(Box(new Vector3(x, 1.25f, -.35f), new Vector3(1.4f, .16f, 1.3f)));
            }
            return solids;
        }

        private static List<TownCourtyardSolid> BenchSolids()
        {
            return new List<TownCourtyardSolid>
            {
                Box(new Vector3(0, .52f, 0), new Vector3(1.9f, .09f, .52f)),
                Box(new Vector3(0, .9f, -.3f), new Vector3(1.9f, .38f, .07f)),
                Box(new Vector3(-.7f, .25f, 0), new Vector3(.08f, .5f, .58f)),
                Box(new Vector3(.7f, .25f, 0), new Vector3(.08f, .5f, .58f))
            };
        }

        private static List<TownCourtyardSolid> WasteSolids()
        {
            var solids = new List<TownCourtyardSolid>
            {
                Box(new Vector3(0, .73f, 0), new Vector3(1.5f, 1.05f, 1.1f)),
                Box(new Vector3(0, 1.31f, 0), new Vector3(1.64f, .13f, 1.2f))
            };
            foreach (float x in new[] { -.58f, .58f })
                foreach (float z in new[] { -.41f, .41f })
                    solids.Add(Box(new Vector3(x, .14f, z), new Vector3(.08f, .28f, .28f)));
            return solids;
        }

        private static List<TownCourtyardSolid> LampSolids()
        {
            return new List<TownCourtyardSolid>
            {
                Box(new Vector3(0, .1f, 0), new Vector3(.38f, .2f, .38f)),
                Box(new Vector3(0, 1.3f, 0), new Vector3(.12f, 2.6f, .12f)),
                Box(new Vector3(0, 2.72f, 0), new Vector3(.4f, .4f, .4f))
            };
        }

        private static List<TownCourtyardSolid> FenceSolids()
        {
            var solids = new List<TownCourtyardSolid>();
            foreach (float x in new[] { -1.45f, 0, 1.45f })
                solids.Add(Box(new Vector3(x, .47f, 0), new Vector3(.1f, .94f, .1f)));
            foreach (float y in new[] { .28f, .72f })
                solids.Add(Box(new Vector3(0, y, 0), new Vector3(3, .065f, .065f)));
            return solids;
        }

        private static List<TownCourtyardSolid> MiniCourtSolids()
        {
            return new List<TownCourtyardSolid>
            {
                Box(new Vector3(0, .09f, -.35f), new Vector3(.45f, .18f, .45f)),
                Box(new Vector3(0, 1.625f, -.35f), new Vector3(.14f, 3.25f, .14f)),
                Box(new Vector3(0, 3.15f, .04f), new Vector3(1.6f, 1, .1f)),
                Beam(new Vector3(0, 3.25f, -.35f), new Vector3(0, 3.25f, .04f), .12f)
            };
        }

        private static TownMeshBuilder MiniCourt()
        {
            var mesh = new TownMeshBuilder();
            mesh.Box(new Vector3(0, .09f, -.35f), new Vector3(.45f, .18f, .45f), TownColor.Stone);
            mesh.Box(new Vector3(0, 1.625f, -.35f), new Vector3(.14f, 3.25f, .14f), TownColor.Frame);
            mesh.Box(new Vector3(0, 3.15f, .04f), new Vector3(1.6f, 1, .1f), TownColor.Cream);
            mesh.Box(new Vector3(0, 3.25f, -.155f), new Vector3(.12f, .12f, .39f), TownColor.Frame);
            foreach (float x in new[] { -.3f, .3f })
                mesh.Box(new Vector3(x, 3.17f, .096f), new Vector3(.025f, .28f, .012f), TownColor.Orange);
            foreach (float y in new[] { 3.03f, 3.31f })
                mesh.Box(new Vector3(0, y, .096f), new Vector3(.625f, .025f, .012f), TownColor.Orange);
            mesh.Box(new Vector3(0, 3.05f, .205f), new Vector3(.12f, .08f, .23f), TownColor.Orange);
            // A closed rim is visible from below as well as from the overview camera.
            for (int i = 0; i < 14; i++)
            {
                float fromAngle = i * Mathf.PI * 2 / 14;
                float toAngle = (i + 1) * Mathf.PI * 2 / 14;
                Vector3 from = new Vector3(Mathf.Cos(fromAngle) * .265f, 3.05f, .43f + Mathf.Sin(fromAngle) * .265f);
                Vector3 to = new Vector3(Mathf.Cos(toAngle) * .265f, 3.05f, .43f + Mathf.Sin(toAngle) * .265f);
                TownCourtyardSolid rim = Beam(from, to, .035f);
                mesh.Box(rim.Center, rim.Size, TownColor.Orange, rim.Rotation);
                if (i % 2 == 0)
                {
                    Vector3 bottom = new Vector3(Mathf.Cos(fromAngle) * .17f, 2.7f, .43f + Mathf.Sin(fromAngle) * .17f);
                    TownCourtyardSolid net = Beam(from, bottom, .012f);
                    mesh.Box(net.Center, net.Size, TownColor.White, net.Rotation);
                }
            }
            return mesh;
        }

        private static TownMeshBuilder FlowerBed()
        {
            var mesh = new TownMeshBuilder();
            mesh.Box(new Vector3(0, .08f, 0), new Vector3(2.6f, .16f, 1.2f), TownColor.Stone);
            foreach (float x in new[] { -1.24f, 1.24f })
                mesh.Box(new Vector3(x, .27f, 0), new Vector3(.12f, .3f, 1.2f), TownColor.Cream);
            foreach (float z in new[] { -.54f, .54f })
                mesh.Box(new Vector3(0, .27f, z), new Vector3(2.6f, .3f, .12f), TownColor.Cream);
            mesh.Ground(0, 0, 2.36f, .96f, .3f, TownColor.Soil);
            for (int i = 0; i < 7; i++)
            {
                Vector3 centre = new Vector3(-.96f + i * .32f, .49f, i % 2 == 0 ? -.17f : .17f);
                mesh.Ellipsoid(centre, new Vector3(.23f, .21f, .24f), TownColor.Leaf, 6, 3);
                TownNature.Flower(mesh, centre + Vector3.up * .2f, .13f, i % 2 == 0 ? TownColor.Pink : TownColor.Flowers);
            }
            return mesh;
        }

        private static TownMeshBuilder Lamp()
        {
            var mesh = new TownMeshBuilder();
            mesh.Cylinder(new Vector3(0, .1f, 0), .19f, .17f, .2f, TownColor.Frame);
            mesh.Cylinder(new Vector3(0, 1.3f, 0), .06f, .045f, 2.6f, TownColor.Frame);
            mesh.Box(new Vector3(0, 2.53f, 0), new Vector3(.4f, .06f, .4f), TownColor.Frame);
            mesh.Box(new Vector3(0, 2.72f, 0), new Vector3(.3f, .32f, .3f), TownColor.Light);
            foreach (float x in new[] { -.18f, .18f })
                foreach (float z in new[] { -.18f, .18f })
                    mesh.Box(new Vector3(x, 2.72f, z), new Vector3(.04f, .36f, .04f), TownColor.Frame);
            mesh.Cylinder(new Vector3(0, 2.95f, 0), .3f, .04f, .1f, TownColor.Frame, 6);
            return mesh;
        }

        private static TownMeshBuilder Hedge()
        {
            var mesh = new TownMeshBuilder();
            mesh.Box(new Vector3(0, .04f, 0), new Vector3(3, .08f, .85f), TownColor.Soil);
            for (int i = 0; i < 5; i++)
                mesh.Ellipsoid(new Vector3(-1.1f + i * .55f, .56f, 0), new Vector3(.4f, .5f, .425f),
                    i % 2 == 0 ? TownColor.LeafDark : TownColor.Leaf, 6, 3);
            return mesh;
        }

        private static TownMeshBuilder Fence()
        {
            var mesh = new TownMeshBuilder();
            foreach (TownCourtyardSolid solid in FenceSolids())
                mesh.Box(solid.Center, solid.Size, TownColor.Frame, solid.Rotation);
            return mesh;
        }
    }

    /// <summary>Immutable recipe. The caller owns and must save or destroy every mesh returned by BuildMesh.</summary>
    internal sealed class TownCourtyardModel
    {
        private readonly Func<TownMeshBuilder> recipe;
        private readonly Vector3 scale;
        private readonly float groundOffset;

        public string Id { get; }
        public string Label { get; }
        public TownCourtyardRole Role { get; }
        public Bounds VisualBounds { get; }
        public Vector2 ReservationSize { get; }
        public Vector3 AccessPoint { get; }
        public IReadOnlyList<TownCourtyardSolid> Solids { get; }

        public TownCourtyardModel(string id, string label, TownCourtyardRole role, Func<TownMeshBuilder> recipe,
            Vector2 reservationSize, Vector3 accessPoint, IReadOnlyList<TownCourtyardSolid> solids, Vector3 scale)
        {
            if (string.IsNullOrWhiteSpace(id) || recipe == null || solids == null || solids.Count == 0 ||
                reservationSize.x <= 0 || reservationSize.y <= 0 || scale.x <= 0 || scale.y <= 0 || scale.z <= 0)
                throw new ArgumentException("Courtyard models need an ID, a recipe, a reservation and positive physical sizes.");
            Id = id;
            Label = label;
            Role = role;
            this.recipe = recipe;
            this.scale = scale;
            ReservationSize = reservationSize;
            AccessPoint = accessPoint;

            Mesh probe = recipe().Build(id + " bounds probe");
            try
            {
                groundOffset = -probe.bounds.min.y * scale.y;
                VisualBounds = new Bounds(Vector3.Scale(probe.bounds.center, scale) + Vector3.up * groundOffset,
                    Vector3.Scale(probe.bounds.size, scale));
                if (VisualBounds.size.x > reservationSize.x + .001f || VisualBounds.size.z > reservationSize.y + .001f)
                    throw new ArgumentException(id + ": its visual silhouette does not fit its layout reservation.");
                Solids = Array.AsReadOnly(solids.Select(solid => solid.ScaledAndGrounded(scale, groundOffset)).ToArray());
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }

        public Mesh BuildMesh()
        {
            Mesh mesh = recipe().Build(Id);
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            var nativePositions = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = Vector3.Scale(vertices[i], scale) + Vector3.up * groundOffset;
                normals[i] = new Vector3(normals[i].x / scale.x, normals[i].y / scale.y, normals[i].z / scale.z).normalized;
                nativePositions[i] = new Vector2(vertices[i].x, vertices[i].z);
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv2 = nativePositions;
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>An actual solid, not the empty envelope around an activity zone.</summary>
    internal sealed class TownCourtyardSolid
    {
        public Vector3 Center { get; }
        public Vector3 Size { get; }
        public Quaternion Rotation { get; }
        public Bounds Bounds { get; }

        public TownCourtyardSolid(Vector3 center, Vector3 size, Quaternion rotation)
        {
            if (size.x <= 0 || size.y <= 0 || size.z <= 0)
                throw new ArgumentException("A physical solid needs positive dimensions.");
            Center = center;
            Size = size;
            Rotation = rotation;
            var bounds = new Bounds(center, Vector3.zero);
            foreach (float x in new[] { -1f, 1f })
                foreach (float y in new[] { -1f, 1f })
                    foreach (float z in new[] { -1f, 1f })
                        bounds.Encapsulate(center + rotation * Vector3.Scale(size * .5f, new Vector3(x, y, z)));
            Bounds = bounds;
        }

        internal TownCourtyardSolid ScaledAndGrounded(Vector3 scale, float groundOffset)
        {
            bool uniform = Mathf.Abs(scale.x - scale.y) < .00001f && Mathf.Abs(scale.x - scale.z) < .00001f;
            if (!uniform && Quaternion.Angle(Rotation, Quaternion.identity) > .0001f)
                throw new InvalidOperationException("A rotated solid cannot be non-uniformly scaled without introducing shear.");
            return new TownCourtyardSolid(Vector3.Scale(Center, scale) + Vector3.up * groundOffset,
                Vector3.Scale(Size, scale), Rotation);
        }
    }
}
