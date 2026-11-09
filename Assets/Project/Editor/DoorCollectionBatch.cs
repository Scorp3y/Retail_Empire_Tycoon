using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.BuildSystem;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Style = DoorCollectionDesigns.Style;

public static class DoorCollectionBatch
{
    public const string Root = "Assets/Art/DoorCollection";
    public const string Catalog = "Assets/Resources/DoorCollection";
    public const string Pictures = "Docs/DoorCollection";
    public const string ScenePath = Root + "/DoorCollectionShowcase.unity";
    private const float Cell = ShopProportions.CellSize;
    private const float PersonTop = GridSystem.StoreyHeight;
    private const float PersonFloor = .096f * ShopProportions.Scale;
    private const float CompactGateClearHeight = 4f;
    private enum Finish { Pine, Sage, Cream, Oak, Golden, Steel, Dark, Clay, Glass, Lamp }
    private static readonly string[] Palette = { "294c3d", "859773", "e2ceaa", "ac783f", "c99842", "687476", "343c40", "aa5d46", "73afc8", "f39234" };
    private static Material[] materials;
    private static BuildItemData legacy;

    [MenuItem("Retail Empire/Art/Build door collection")]
    public static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (string folder in new[] { Catalog, Pictures, Root + "/Meshes", Root + "/Materials", Root + "/Prefabs", Root + "/Icons" })
            Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        materials = Enumerable.Range(0, Palette.Length).Select(i => MaterialFor((Finish)i)).ToArray();
        var framePalette = DoorFrameFinishSetup.CreatePalette();
        legacy = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Prefabs/Structures/Door.asset");
        var entries = new List<BuildItemData>();
        var report = new List<string> { "Style,Triangles,WidthCells,DepthCells,Price" };
        foreach (var style in DoorCollectionDesigns.All)
        {
            var model = new Model(style);
            try
            {
                model.Build();
                int triangles = model.SaveGeometry();
                DoorFrameFinishSetup.BindCollection(model.Root, style.Number, framePalette);
                int limit = style.Number is 1 or 2 or 3 or 4 or 5 or 8 or 11 or 12 or 14 ? 300 : 1000;
                if (triangles > limit) throw new InvalidOperationException($"{style.Id}: {triangles} triangles exceeds {limit}.");
                var prefab = PrefabUtility.SaveAsPrefabAsset(model.Root, $"{Root}/Prefabs/{style.Id}.prefab");
                var icon = ShopUiPreviews.Render(prefab, style.Id, 17, outputFolder: Root + "/Icons",
                    prepare: preview => AddWallEnds(preview.transform, style, .28f));
                entries.Add(WriteItem(style, model, prefab, icon));
                report.Add($"{style.Id},{triangles},{style.Cells},{style.Depth},{style.Price}");
            }
            finally { Object.DestroyImmediate(model.Root); }
        }
        AssetDatabase.SaveAssets();
        CreateShowcase(entries);
        File.WriteAllLines(Pictures + "/Meshes.csv", report);
        Debug.Log("Door Collection: built 24 door prefabs, icons and catalog entries.");
    }

    private static Material MaterialFor(Finish finish)
    {
        string path = $"{Root}/Materials/{finish}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        ColorUtility.TryParseHtmlString("#" + Palette[(int)finish], out var color);
        color.a = finish == Finish.Glass ? .34f : 1;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", finish == Finish.Glass ? .65f : .22f);
        material.SetFloat("_Metallic", finish == Finish.Steel ? .24f : 0);
        material.enableInstancing = true;
        if (finish == Finish.Glass)
        {
            material.SetFloat("_Surface", 1);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
        }
        if (finish == Finish.Lamp)
        {
            material.SetColor("_EmissionColor", color * 1.8f);
            material.EnableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static BuildItemData WriteItem(Style style, Model model, GameObject prefab, Sprite icon)
    {
        string path = $"{Catalog}/{style.Id}.asset";
        var item = AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<BuildItemData>();
            AssetDatabase.CreateAsset(item, path);
        }
        item.id = style.Id;
        item.displayName = style.English; item.description = style.EnglishDescription;
        item.russianName = style.Russian; item.russianDescription = style.RussianDescription;
        item.category = BuildCategory.Structures; item.placementKind = PlacementKind.Wall; item.isDoorway = true;
        item.unlockedByDefault = true; item.price = style.Price;
        item.weightKg = style.Vehicle ? 85 : style.Depth > 1 ? 55 : 35;
        item.cargoVolumeM3 = style.Vehicle ? .55f : .22f;
        item.footprint = new Vector2Int(style.Cells, style.Depth);
        item.alignModelToFootprint = true; item.placementBounds = model.PlacementBounds;
        item.allowRotation = true;
        item.ruleFlags = PlacementRuleFlags.InsidePurchasedArea | PlacementRuleFlags.NoOverlap;
        item.prefab = prefab; item.icon = icon;
        item.previewValidMaterial = legacy.previewValidMaterial; item.previewInvalidMaterial = legacy.previewInvalidMaterial;
        EditorUtility.SetDirty(item);
        return item;
    }

    private sealed class Model
    {
        private readonly Style style;
        public GameObject Root { get; }
        private readonly Transform frame;
        private readonly List<CollectionDoor.Part> parts = new();
        private readonly List<BoxCollider> approaches = new(), barriers = new();
        private readonly List<Transform> lamps = new();
        private readonly List<Bounds> sweeps = new();
        private readonly Dictionary<(Transform parent, bool transparent), Geometry> geometry = new();
        private Transform drum;
        private float section, bend, radius, distance;
        private float Width => style.Cells * Cell;
        private float Depth => style.Depth * Cell;
        private float Bottom => style.Vehicle ? .015f : PersonFloor;
        private float Top => style.Vehicle ? Bottom + (style.CompactGate ? CompactGateClearHeight : GarageDoor.ClearHeight) : PersonTop - .09f;
        private float Height => Top - Bottom;
        public Bounds PlacementBounds => new Bounds(new Vector3(0, (Top + .09f) / 2, 0), new Vector3(Width, Top + .09f, Depth));
        public Model(Style style)
        {
            this.style = style;
            Root = new GameObject(style.Id);
            frame = Node(Root.transform, "Frame");
            GameObjectUtility.SetStaticEditorFlags(frame.gameObject, StaticEditorFlags.BatchingStatic);
        }

        public void Build()
        {
            if (style.Vehicle) Vehicle();
            else if (style.Number == 19) Corner();
            else if (style.Motion == DoorMechanism.Arc) Curved();
            else if (style.Motion == DoorMechanism.Revolve) Revolving();
            else
            {
                Finish header = style.Powered && style.Number != 16 ? Finish.Cream : Finish.Pine;
                StraightFrame(frame, Width, header);
                switch (style.Motion)
                {
                    case DoorMechanism.Swing: Swing(); break;
                    case DoorMechanism.Slide: Slide(); break;
                    case DoorMechanism.Fold: Fold(); break;
                }
                if (style.Powered && style.Number != 23) Lamp(frame, 0, Top + .042f, .086f);
            }
            Root.AddComponent<CollectionDoor>();
            Root.AddComponent<AudioSource>().playOnAwake = false;
            // Sensor renderers are assigned after geometry has been consolidated.
        }

        private void StraightFrame(Transform parent, float span, Finish header)
        {
            const float jamb = .072f;
            foreach (int side in new[] { -1, 1 })
            {
                var center = new Vector3(side * (span - jamb) * .5f, Top * .5f, 0);
                var size = new Vector3(jamb, Top, style.Vehicle ? Depth : .16f);
                Box(parent, Finish.Pine, center, size);
                Collider(parent, "Jamb collider", center, size);
            }
            Box(parent, header, new Vector3(0, Top + .045f, 0), new Vector3(span, .09f, style.Vehicle ? Depth : .21f));
        }

        private void Portal(Transform parent, float center, float width, float depthOffset = 0)
        {
            float depth = style.Vehicle ? 2.4f : 1.65f;
            if (style.Vehicle) depth = 2.5f;
            for (int i = -1; i <= 1; i += 2)
                approaches.Add(Collider(parent, i > 0 ? "Front approach" : "Rear approach",
                    new Vector3(center, Bottom + Height * .5f, depthOffset + i * depth * .5f),
                    new Vector3(width, Height, depth), true));
            barriers.Add(Collider(parent, "Closed opening", new Vector3(center, Bottom + Height * .5f, depthOffset),
                new Vector3(width, Height, .12f)));
        }

        private void Sweep(Vector3 center, Vector3 size) => sweeps.Add(new Bounds(center, size));
        private void Leaf(Transform node, Vector3 travel, float angle = 0) =>
            parts.Add(new CollectionDoor.Part { node = node, rest = node.localPosition, travel = travel, angle = angle });

        private void Swing()
        {
            bool pair = style.Number is 3 or 8 or 23;
            float usable = Width - .144f, w = usable / (pair ? 2 : 1);
            for (int index = 0; index < (pair ? 2 : 1); index++)
            {
                int direction = index == 0 ? 1 : -1;
                var hinge = Node(Root.transform, index == 0 ? "Leaf_L" : "Leaf_R", new Vector3(-direction * usable / 2, Bottom, 0));
                float center = w * .5f * direction;
                float pull = direction * (w - .11f);
                switch (style.Number)
                {
                    case 1:
                        Panel(hinge, Finish.Oak, center, w, Height);
                        Box(hinge, Finish.Golden, new Vector3(center, Height * .73f, .027f), new Vector3(w * .77f, Height * .33f, .012f));
                        Box(hinge, Finish.Golden, new Vector3(center, Height * .30f, .027f), new Vector3(w * .77f, Height * .40f, .012f));
                        Lever(hinge, pull); break;
                    case 2:
                        Glazed(hinge, center, w, Height, Finish.Pine);
                        Pull(hinge, pull); break;
                    case 3:
                        Glazed(hinge, center, w, Height, Finish.Cream, lower: Finish.Cream);
                        Box(hinge, Finish.Steel, new Vector3(pull, .75f, .049f), new Vector3(.024f, .27f, .045f));
                        break;
                    case 4:
                        Glazed(hinge, center, w, Height, Finish.Cream, lower: Finish.Sage);
                        Lever(hinge, pull); break;
                    case 5:
                        Panel(hinge, Finish.Dark, center, w, Height);
                        Box(hinge, Finish.Steel, new Vector3(center, .72f, .055f), new Vector3(w * .74f, .058f, .063f));
                        break;
                    case 8:
                        PortholeLeaf(hinge, center, w);
                        break;
                    case 23:
                        Glazed(hinge, center, w, Height, Finish.Pine);
                        Box(frame, Finish.Dark, new Vector3(-direction * Width * .32f, Top + .04f, .11f), new Vector3(.24f, .064f, .075f));
                        Beam(hinge, Finish.Steel, new Vector3(direction * .04f, Height + .015f, .087f),
                            new Vector3(direction * w * .43f, Height - .016f, .01f), .025f);
                        break;
                }
                if (!pair)
                    foreach (float y in new[] { .27f, Height - .22f })
                        Box(frame, Finish.Steel, new Vector3(hinge.localPosition.x, Bottom + y, .042f), new Vector3(.045f, .095f, .05f));
                Leaf(hinge, Vector3.zero, direction * (style.Number == 3 ? -96f : 96f));
            }
            Portal(Root.transform, 0, usable);
            Sweep(new Vector3(0, Bottom + Height / 2, 0), new Vector3(usable - .02f, Height, w * 2 + .12f));
        }

        private void Slide()
        {
            float opening = Width - .144f;
            var track = Node(frame, "Track");
            if (style.Number == 11)
            {
                float z = .34f;
                var leaf = Node(Root.transform, "Leaf", new Vector3(0, Bottom, z));
                Panel(leaf, Finish.Oak, 0, opening, Height);
                foreach (int sign in new[] { -1, 1 })
                    Beam(leaf, Finish.Golden, new Vector3(-opening * .44f, Height * (.5f - sign * .39f), .04f),
                        new Vector3(opening * .44f, Height * (.5f + sign * .39f), .04f), .085f);
                Box(leaf, Finish.Dark, new Vector3(opening * .32f, .68f, .056f), new Vector3(.055f, .17f, .06f));
                Box(track, Finish.Dark, new Vector3(-opening * .54f, Top + .04f, z), new Vector3(opening * 2.2f, .045f, .067f));
                foreach (int side in new[] { -1, 1 })
                    Box(leaf, Finish.Dark, new Vector3(side * opening * .31f, Height + .018f, .015f), new Vector3(.09f, .10f, .065f));
                Leaf(leaf, Vector3.left * (opening + .05f));
                Portal(Root.transform, 0, opening);
                Sweep(new Vector3(-opening / 2, Bottom + Height / 2, z), new Vector3(opening * 2.12f, Height, .12f));
                return;
            }
            if (style.Number is 12 or 16)
            {
                float half = opening * .5f;
                var leaf = Node(Root.transform, "Leaf", new Vector3(-half / 2, Bottom, -.025f));
                if (style.Number == 12)
                {
                    Panel(leaf, Finish.Sage, 0, half, Height);
                    Box(leaf, Finish.Dark, new Vector3(-half * .34f, .70f, .037f), new Vector3(.055f, .13f, .013f));
                    foreach (int side in new[] { -1, 1 })
                        Box(frame, Finish.Cream, new Vector3(half / 2, Bottom + Height / 2, side * .096f), new Vector3(half, Height, .032f));
                    Collider(frame, "Pocket wall", new Vector3(half / 2, Bottom + Height / 2, 0), new Vector3(half, Height, .17f));
                }
                else FixedGlazing(frame, half / 2, half, .053f);
                if (style.Number == 16) Glazed(leaf, 0, half, Height, Finish.Pine);
                Leaf(leaf, Vector3.right * half);
                Portal(Root.transform, -half / 2, half);
            }
            else if (style.Number == 17)
            {
                float quarter = opening / 4;
                foreach (int side in new[] { -1, 1 })
                {
                    FixedGlazing(frame, side * quarter * 1.5f, quarter, .06f);
                    var leaf = Node(Root.transform, side < 0 ? "Leaf_L" : "Leaf_R", new Vector3(side * quarter / 2, Bottom, -.023f));
                    Glazed(leaf, 0, quarter, Height, Finish.Pine);
                    Leaf(leaf, Vector3.right * (side * quarter));
                }
                Portal(Root.transform, 0, quarter * 2);
            }
            else
            {
                float third = opening / 3;
                FixedGlazing(frame, third, third, .085f);
                for (int i = 0; i < 2; i++)
                {
                    var leaf = Node(Root.transform, $"Leaf_{i + 1}", new Vector3((i - 1) * third, Bottom, -.075f + i * .072f));
                    Glazed(leaf, 0, third, Height, Finish.Pine);
                    Leaf(leaf, Vector3.right * (third * (2 - i)));
                }
                Portal(Root.transform, -third / 2, third * 2);
            }
            Box(track, Finish.Dark, new Vector3(0, Top - .011f, 0), new Vector3(opening, .025f, .19f));
            Sweep(new Vector3(0, Bottom + Height / 2, 0), new Vector3(opening - .025f, Height, .20f));
        }

        private void Fold()
        {
            bool paired = style.Number == 24;
            int count = style.Number == 15 ? 6 : 2;
            float opening = Width - .144f, width = opening / (paired ? 2 : 1) / count;
            var track = Node(frame, "Track");
            Box(track, Finish.Dark, new Vector3(0, Top - .012f, 0), new Vector3(opening, .028f, .13f));
            for (int group = 0; group < (paired ? 2 : 1); group++)
            {
                int sign = group == 0 ? -1 : 1;
                for (int i = 0; i < count; i++)
                {
                    float rest = sign * (opening / 2 - (i + .5f) * width);
                    var leaf = Node(Root.transform, $"Leaf_{group + 1}_{i + 1}", new Vector3(rest, Bottom, 0));
                    if (paired) Glazed(leaf, 0, width, Height, Finish.Pine);
                    else Panel(leaf, style.Number == 15 ? Finish.Oak : Finish.Cream, 0, width, Height, bevel: true);
                    if (!paired && i != count - 1)
                        Box(leaf, Finish.Steel, new Vector3(-sign * width * .46f, Height * .65f, .029f), new Vector3(.029f, .11f, .028f));
                    int depthDirection = paired ? 1 : -1;
                    Leaf(leaf, new Vector3(sign * (i + .5f) * width, 0, depthDirection * width * .5f),
                        sign * depthDirection * (i % 2 == 0 ? 85 : -85));
                }
            }
            Portal(Root.transform, 0, opening);
            Sweep(new Vector3(0, Bottom + Height / 2, 0), new Vector3(opening - .02f, Height, width * 2.2f));
        }

        private void Corner()
        {
            foreach (int axis in new[] { 0, 1 })
            {
                var leg = Node(frame, axis == 0 ? "Front frame" : "Side frame", axis == 0
                    ? new Vector3(0, 0, Depth * .5f - .11f) : new Vector3(Width * .5f - .11f, 0, 0));
                leg.localRotation = Quaternion.Euler(0, axis * 90, 0);
                float span = Width - .13f, half = (span - .144f) / 2;
                StraightFrame(leg, span, Finish.Cream);
                int direction = axis == 0 ? 1 : -1;
                FixedGlazing(leg, -direction * half / 2, half, .04f);
                var slider = Node(leg, "Leaf", new Vector3(direction * half / 2, Bottom, -.046f));
                Glazed(slider, 0, half, Height, Finish.Pine);
                Leaf(slider, Vector3.left * (direction * half));
                Lamp(leg, 0, Top + .04f, .115f);
                Portal(leg, direction * half / 2, half);
            }
            Sweep(new Vector3(0, Bottom + Height / 2, 0), new Vector3(Width - .025f, Height, Depth - .025f));
        }

        private void Curved()
        {
            float r = Width * .46f;
            var arc = Node(frame, "Curved track", new Vector3(0, 0, -Depth / 2 + .12f));
            RingTrack(arc, r, -83, 83, Top + .042f, Finish.Pine, 8);
            foreach (int side in new[] { -1, 1 })
            {
                ArcPanel(arc, r, side * 63f, 39f, fixedPanel: true);
                var pivot = Node(arc, side < 0 ? "Leaf_L" : "Leaf_R");
                ArcPanel(pivot, r - .075f, side * 20.5f, 42f, fixedPanel: false);
                Leaf(pivot, Vector3.zero, side * 42f);
            }
            Lamp(arc, 0, Top + .04f, r + .08f);
            Portal(arc, 0, r * 1.24f, r * .87f);
            Sweep(new Vector3(0, Bottom + Height / 2, 0), new Vector3(Width - .045f, Height, Depth - .025f));
        }

        private void Revolving()
        {
            int count = style.Number == 21 ? 3 : 4;
            float r = Width * .46f;
            Cylinder(frame, style.Number == 21 ? Finish.Cream : Finish.Sage, new Vector3(0, Top + .06f, 0),
                new Vector3(r * 2, r * 2, .12f), Quaternion.Euler(90, 0, 0), 14);
            var rotor = Node(Root.transform, "Rotor");
            Cylinder(rotor, Finish.Dark, new Vector3(0, Bottom + Height / 2, 0), new Vector3(.11f, .11f, Height),
                Quaternion.Euler(90, 0, 0), 8);
            for (int i = 0; i < count; i++)
            {
                var wing = Node(rotor, $"Wing_{i + 1}", new Vector3(0, Bottom, 0));
                wing.localRotation = Quaternion.Euler(0, i * 360f / count, 0);
                Glazed(wing, r * .47f, r * .91f, Height, Finish.Pine);
            }
            foreach (int side in new[] { -1, 1 })
                foreach (float angle in new[] { 57f, 90f, 123f })
                    ArcPanel(frame, r, side * angle, 33f, true);
            Leaf(rotor, Vector3.zero);
            Portal(Root.transform, 0, r * 1.26f, r * .77f);
            Portal(Root.transform, 0, r * 1.26f, -r * .77f);
            Sweep(new Vector3(0, Bottom + Height / 2, 0), new Vector3(r * 2, Height, r * 2));
        }

        private void Vehicle()
        {
            float opening = style.CompactGate ? 3.6f : 5.4f;
            var tracks = Node(frame, "Track");
            Finish trim = style.Number is 27 or 30 ? Finish.Cream : style.Number == 26 ? Finish.Steel : Finish.Pine;
            float sideSize = (Width - opening) / 2;
            float frameTop = style.Number == 26 ? Top + Height + .48f : Top + .24f;
            foreach (int side in new[] { -1, 1 })
            {
                var center = new Vector3(side * (opening + sideSize) / 2, frameTop / 2, 0);
                var size = new Vector3(sideSize, frameTop, Depth);
                Box(frame, trim, center, size, bevel: true);
                Collider(frame, "Steel post", center, size);
            }
            var headerCenter = new Vector3(0, frameTop - .11f, 0);
            var headerSize = new Vector3(Width, .22f, Depth);
            Box(frame, trim, headerCenter, headerSize, bevel: true);
            if (style.CompactGate) Collider(frame, "Header collider", headerCenter, headerSize);
            Lamp(frame, 0, Top + .12f, Depth / 2 + .02f);
            Portal(Root.transform, 0, opening);
            switch (style.Motion)
            {
                case DoorMechanism.Lift:
                case DoorMechanism.Tilt:
                {
                    bool tilt = style.Motion == DoorMechanism.Tilt;
                    var leaf = Node(Root.transform, "Leaf", new Vector3(0, tilt ? Top : Bottom, 0));
                    float y = tilt ? -Height / 2 : Height / 2;
                    Box(leaf, tilt ? Finish.Golden : Finish.Pine, new Vector3(0, y, 0),
                        new Vector3(opening - .035f, Height, .17f), bevel: true);
                    foreach (float fraction in new[] { -.25f, 0f, .25f })
                        Box(leaf, tilt ? Finish.Oak : Finish.Sage, new Vector3(0, y + Height * fraction, .093f),
                            new Vector3(opening - .11f, .048f, .027f));
                    if (tilt)
                        foreach (int side in new[] { -1, 1 })
                            Beam(leaf, Finish.Steel, new Vector3(side * opening * .47f, -.16f, -.20f),
                                new Vector3(side * opening * .47f, -Height * .52f, .03f), .09f);
                    Leaf(leaf, tilt ? Vector3.zero : Vector3.up * (Height + .10f), tilt ? -92f : 0f);
                    Sweep(new Vector3(0, tilt ? Height / 2 : Top, tilt ? Height / 2 : 0),
                        new Vector3(opening - .02f, tilt ? Top + .45f : frameTop * 1.02f, tilt ? Height + .25f : .35f));
                    break;
                }
                case DoorMechanism.Roll:
                    section = Height / 10; bend = Top; radius = .24f; distance = Height + .24f;
                    drum = Node(frame, "Drum", new Vector3(0, Top + .23f, -.21f));
                    Cylinder(drum, Finish.Steel, Vector3.zero, new Vector3(.56f, .56f, opening), Quaternion.Euler(0, 90, 0), 12);
                    for (int i = 0; i < 10; i++)
                    {
                        var slat = Node(Root.transform, $"Slat_{i + 1:00}", new Vector3(0, Bottom + (i + .5f) * section, 0));
                        Box(slat, i % 2 == 0 ? Finish.Steel : Finish.Dark, Vector3.zero,
                            new Vector3(opening - .035f, section - .016f, .11f), bevel: true);
                        Leaf(slat, Vector3.zero);
                    }
                    Sweep(new Vector3(0, (Top + .51f) * .5f, -.21f), new Vector3(opening - .02f, Top + .51f, .56f));
                    break;
                case DoorMechanism.Sectional:
                    float trackScale = Height / GarageDoor.ClearHeight;
                    section = Height / GarageDoor.SectionCount;
                    bend = GarageDoor.BendStart * trackScale;
                    radius = GarageDoor.BendRadius * trackScale;
                    distance = GarageDoor.OpenTravel * trackScale;
                    var sectional = Node(Root.transform, "Sections", Vector3.up * Bottom);
                    for (int i = 0; i < GarageDoor.SectionCount; i++)
                    {
                        var panel = Node(sectional, $"Section_{i + 1:00}");
                        Box(panel, Finish.Cream, Vector3.zero, new Vector3(opening - .035f, section - .023f, .14f), bevel: true);
                        foreach (int side in new[] { -1, 1 })
                            Box(panel, Finish.Dark, new Vector3(side * opening * .35f, -section * .37f, .087f), new Vector3(.12f, .12f, .04f));
                        Leaf(panel, Vector3.zero);
                    }
                    foreach (int side in new[] { -1, 1 })
                        Rail(tracks, d => Vector3.up * Bottom + GarageDoor.Track(d, bend, radius) + Vector3.right * (side * opening / 2),
                            distance + Height, 6);
                    Sweep(new Vector3(0, Top * .5f, 0), new Vector3(opening - .02f, Top * 1.04f, .25f));
                    Sweep(new Vector3(0, Top + .39f, -Height * .52f), new Vector3(opening + .1f, .63f, Height * 1.12f));
                    break;
                case DoorMechanism.SideSectional:
                    section = opening / 7; bend = opening; radius = .55f; distance = opening + .90f;
                    for (int i = 0; i < 7; i++)
                    {
                        var panel = Node(Root.transform, $"Section_{i + 1:00}", new Vector3(-opening / 2, Bottom + Height / 2, 0));
                        Box(panel, Finish.Pine, Vector3.zero, new Vector3(section - .018f, Height, .14f), bevel: true);
                        Leaf(panel, Vector3.zero);
                    }
                    foreach (float y in new[] { Bottom + .023f, Top })
                        Rail(tracks, d =>
                        {
                            var point = GarageDoor.Track(d, bend, radius);
                            return new Vector3(-opening / 2 + point.y, y, point.z);
                        }, distance + opening, 7);
                    Sweep(new Vector3(0, Top * .5f, 0), new Vector3(opening - .02f, Top, .22f));
                    float railDepth = -GarageDoor.Track(distance + opening, bend, radius).z;
                    Sweep(new Vector3(opening / 2 + radius * .55f, Top / 2, -railDepth * .5f),
                        new Vector3(radius * 1.55f, Top, railDepth + .16f));
                    break;
            }
        }

        private void Rail(Transform target, Func<float, Vector3> path, float length, int count)
        {
            // Dense only around the bend; the main vertical and horizontal runs stay simple.
            var cuts = new List<float> { 0, bend };
            for (int i = 1; i <= count - 2; i++) cuts.Add(bend + Mathf.PI * .5f * radius * i / (count - 2));
            cuts.Add(length);
            for (int i = 1; i < cuts.Count; i++) Beam(target, Finish.Steel, path(cuts[i - 1]), path(cuts[i]), .075f);
        }

        private void RingTrack(Transform target, float r, float start, float end, float y, Finish finish, int segments)
        {
            for (int i = 0; i < segments; i++)
                Beam(target, finish, ArcPoint(r, Mathf.Lerp(start, end, (float)i / segments), y),
                    ArcPoint(r, Mathf.Lerp(start, end, (float)(i + 1) / segments), y), .115f);
        }

        private void ArcPanel(Transform parent, float r, float angle, float degrees, bool fixedPanel)
        {
            var a = ArcPoint(r, angle - degrees / 2, Bottom);
            var b = ArcPoint(r, angle + degrees / 2, Bottom);
            var panel = Node(parent, fixedPanel ? "Fixed curved glass" : "Glass", (a + b) * .5f);
            panel.localRotation = Quaternion.FromToRotation(Vector3.right, (b - a).normalized);
            float span = Vector3.Distance(a, b);
            Glazed(panel, 0, span + .02f, Height, Finish.Pine);
            if (fixedPanel) Collider(panel, "Fixed glass collider", Vector3.up * Height * .5f, new Vector3(span, Height, .10f));
        }

        private static Vector3 ArcPoint(float r, float angle, float y)
        {
            float a = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r);
        }

        private void FixedGlazing(Transform target, float x, float w, float z)
        {
            var panel = Node(target, "Fixed glass", new Vector3(x, Bottom, z));
            Glazed(panel, 0, w, Height, Finish.Pine);
            Collider(panel, "Fixed panel collider", Vector3.up * Height / 2, new Vector3(w, Height, .045f));
        }

        private void Lamp(Transform parent, float x, float y, float z)
        {
            var sensor = Node(parent, "Sensor", new Vector3(x, y, z));
            Box(sensor, Finish.Dark, Vector3.zero, new Vector3(.21f, .062f, .046f));
            var lens = Node(sensor, "Indicator", Vector3.forward * .025f);
            Box(lens, Finish.Lamp, Vector3.zero, new Vector3(.125f, .024f, .010f));
            lamps.Add(lens);
        }

        private void Panel(Transform target, Finish finish, float x, float w, float h, bool bevel = true) =>
            Box(target, finish, new Vector3(x, h / 2, 0), new Vector3(w - .022f, h - .022f, .061f), bevel: bevel);

        private void Glazed(Transform parent, float x, float w, float h, Finish finish, Finish? lower = null)
        {
            float edge = Mathf.Min(.059f, w * .13f), inset = .013f;
            float lowerHeight = lower.HasValue ? .38f * h : edge;
            foreach (int side in new[] { -1, 1 })
                Box(parent, finish, new Vector3(x + side * (w / 2 - edge / 2 - inset), h / 2, 0), new Vector3(edge, h - .022f, .067f));
            float inner = w - 2 * (edge + inset);
            Box(parent, finish, new Vector3(x, h - edge / 2 - inset, 0), new Vector3(inner, edge, .067f));
            Box(parent, lower ?? finish, new Vector3(x, lowerHeight / 2 + inset, 0), new Vector3(inner, lowerHeight, .061f));
            Box(parent, Finish.Glass, new Vector3(x, (h + lowerHeight - edge) / 2, 0),
                new Vector3(inner + .006f, h - lowerHeight - edge - inset, .013f));
        }

        private void PortholeLeaf(Transform parent, float x, float w)
        {
            // Four opaque pieces surround the real glazed opening.
            float hole = .24f, y = Height * .69f, half = w * .5f - .014f;
            Box(parent, Finish.Clay, new Vector3(x, (y - hole / 2) / 2, 0),
                new Vector3(half * 2, y - hole / 2, .06f));
            Box(parent, Finish.Clay, new Vector3(x, (Height + y + hole / 2) / 2, 0),
                new Vector3(half * 2, Height - y - hole / 2 - .012f, .06f));
            foreach (int side in new[] { -1, 1 })
                Box(parent, Finish.Clay, new Vector3(x + side * (half + hole / 2) / 2, y, 0),
                    new Vector3(half - hole / 2, hole, .06f));
            Buffer(parent, Finish.Dark).SquareToRound(Finish.Dark, new Vector3(x, y, 0), hole * .66f, hole * .48f, .032f);
            Cylinder(parent, Finish.Glass, new Vector3(x, y, 0), new Vector3(hole, hole, .016f), Quaternion.identity, 8);
            Box(parent, Finish.Dark, new Vector3(x, .14f, .035f), new Vector3(half * 1.95f, .25f, .021f));
        }

        private void Lever(Transform parent, float x)
        {
            Box(parent, Finish.Dark, new Vector3(x, .71f, .042f), new Vector3(.058f, .084f, .032f));
            Box(parent, Finish.Steel, new Vector3(x - .05f, .71f, .071f), new Vector3(.132f, .029f, .046f));
        }

        private void Pull(Transform parent, float x)
        {
            foreach (float y in new[] { .63f, .88f })
                Box(parent, Finish.Steel, new Vector3(x, y, .066f), new Vector3(.033f, .036f, .075f));
            Box(parent, Finish.Steel, new Vector3(x, .755f, .105f), new Vector3(.034f, .29f, .038f), bevel: true);
        }

        private void Beam(Transform parent, Finish finish, Vector3 a, Vector3 b, float width) =>
            Box(parent, finish, (a + b) / 2, new Vector3(Vector3.Distance(a, b) + .006f, width, width),
                Quaternion.FromToRotation(Vector3.right, b - a));

        private static Transform Node(Transform parent, string name, Vector3 position = default)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false); node.localPosition = position;
            return node;
        }

        private static BoxCollider Collider(Transform parent, string name, Vector3 center, Vector3 size, bool trigger = false)
        {
            var node = Node(parent, name);
            var collider = node.gameObject.AddComponent<BoxCollider>();
            collider.center = center; collider.size = size; collider.isTrigger = trigger;
            return collider;
        }

        private void Box(Transform parent, Finish finish, Vector3 center, Vector3 size, Quaternion rotation = default, bool bevel = false)
        {
            float x = size.x * .5f, y = size.y * .5f;
            float r = Mathf.Min(.018f, Mathf.Min(x, y) * .25f);
            var polygon = bevel
                ? new[] { new Vector2(-x+r,-y), new Vector2(x-r,-y), new Vector2(x,-y+r), new Vector2(x,y-r),
                    new Vector2(x-r,y), new Vector2(-x+r,y), new Vector2(-x,y-r), new Vector2(-x,-y+r) }
                : new[] { new Vector2(-x,-y), new Vector2(x,-y), new Vector2(x,y), new Vector2(-x,y) };
            Buffer(parent, finish).Prism(finish, polygon, size.z, center, rotation.Equals(default(Quaternion)) ? Quaternion.identity : rotation);
        }

        private void Cylinder(Transform parent, Finish finish, Vector3 center, Vector3 size, Quaternion rotation, int sides)
        {
            var polygon = Enumerable.Range(0, sides).Select(i =>
            {
                float angle = i * Mathf.PI * 2 / sides;
                return new Vector2(Mathf.Cos(angle) * size.x / 2, Mathf.Sin(angle) * size.y / 2);
            }).ToArray();
            Buffer(parent, finish).Prism(finish, polygon, size.z, center, rotation);
        }

        private Geometry Buffer(Transform parent, Finish finish)
        {
            var key = (parent, finish == Finish.Glass);
            if (!geometry.TryGetValue(key, out var buffer)) geometry.Add(key, buffer = new Geometry());
            return buffer;
        }

        public int SaveGeometry()
        {
            string path = $"{DoorCollectionBatch.Root}/Meshes/{style.Id}.asset";
            var oldMeshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
            var previous = oldMeshes.ToDictionary(mesh => mesh.name);
            int triangleCount = 0, index = 0;
            foreach (var entry in geometry)
            {
                var data = entry.Value;
                string name = $"{style.Id}_{index++:00}";
                var mesh = data.Make(name);
                triangleCount += data.TriangleCount;
                if (previous.TryGetValue(name, out var existing))
                {
                    EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing;
                }
                else if (AssetDatabase.LoadMainAssetAtPath(path) == null) AssetDatabase.CreateAsset(mesh, path);
                else AssetDatabase.AddObjectToAsset(mesh, path);
                var container = Node(entry.Key.parent, entry.Key.transparent ? "Glass" : "Geometry");
                container.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = container.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = data.Finishes.Select(finish => materials[(int)finish]).ToArray();
                if (entry.Key.transparent) renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (container.IsChildOf(frame) && !parts.Any(part => container.IsChildOf(part.node)) &&
                    (drum == null || !container.IsChildOf(drum)))
                    GameObjectUtility.SetStaticEditorFlags(container.gameObject, StaticEditorFlags.BatchingStatic);
            }
            foreach (var obsolete in oldMeshes.Where(mesh => !mesh.name.StartsWith(style.Id + "_", StringComparison.Ordinal) ||
                int.TryParse(mesh.name.Split('_').Last(), out int number) && number >= index))
                Object.DestroyImmediate(obsolete, true);
            var controller = Root.GetComponent<CollectionDoor>();
            controller.Configure(style.Motion, parts.ToArray(), approaches.ToArray(), barriers.ToArray(),
                lamps.Select(lamp => lamp.GetComponentInChildren<Renderer>()).ToArray(), sweeps.ToArray(),
                style.Vehicle, style.Motion == DoorMechanism.Swing && style.Number != 23, style.Number == 8,
                style.Vehicle ? 1.75f : .65f, section, bend, radius, distance, drum);
            controller.SetPose(0);
            return triangleCount;
        }
    }

    private sealed class Geometry
    {
        private readonly List<Vector3> vertices = new();
        private readonly Dictionary<Finish, List<int>> submeshes = new();
        public IEnumerable<Finish> Finishes => submeshes.Keys;
        public int TriangleCount => submeshes.Values.Sum(indices => indices.Count / 3);
        public void Prism(Finish finish, Vector2[] outline, float depth, Vector3 center, Quaternion rotation)
        {
            if (!submeshes.TryGetValue(finish, out var triangles)) submeshes.Add(finish, triangles = new List<int>());
            var front = outline.Select(p => center + rotation * new Vector3(p.x, p.y, depth * .5f)).ToArray();
            var rear = outline.Select(p => center + rotation * new Vector3(p.x, p.y, -depth * .5f)).ToArray();
            Face(front, triangles); Face(rear.Reverse().ToArray(), triangles);
            for (int i = 0; i < outline.Length; i++)
            {
                int j = (i + 1) % outline.Length;
                Face(new[] { front[j], front[i], rear[i], rear[j] }, triangles);
            }
        }
        public void SquareToRound(Finish finish, Vector3 center, float outer, float inner, float depth)
        {
            if (!submeshes.TryGetValue(finish, out var triangles)) submeshes.Add(finish, triangles = new List<int>());
            Vector3 Point(int i, bool outside, int side)
            {
                var angle = i * Mathf.PI / 4;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float r = outside ? outer / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y)) : inner;
                return center + new Vector3(direction.x * r, direction.y * r, side * depth);
            }
            foreach (int side in new[] { -1, 1 })
                for (int i = 0; i < 8; i++)
                {
                    var face = new[] { Point(i, true, side), Point(i + 1, true, side),
                        Point(i + 1, false, side), Point(i, false, side) };
                    Face(side > 0 ? face : face.Reverse().ToArray(), triangles);
                }
        }
        private void Face(Vector3[] points, List<int> indices)
        {
            int first = vertices.Count;
            vertices.AddRange(points);
            for (int i = 1; i < points.Length - 1; i++) indices.AddRange(new[] { first, first + i, first + i + 1 });
        }
        public Mesh Make(string name)
        {
            var mesh = new Mesh { name = name, subMeshCount = submeshes.Count };
            mesh.SetVertices(vertices);
            int index = 0;
            foreach (var indices in submeshes.Values) mesh.SetTriangles(indices, index++);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    private static void AddWallEnds(Transform parent, Style style, float scale)
    {
        if (style.Number is 19 or 20 or 21 or 22) return;
        var item = AssetDatabase.LoadAssetAtPath<BuildItemData>("Assets/Resources/WallCollection/wall_finish_01.asset");
        foreach (int side in new[] { -1, 1 })
        {
            var wall = (GameObject)PrefabUtility.InstantiatePrefab(item.prefab, parent);
            wall.name = side < 0 ? "Wall section left" : "Wall section right";
            wall.transform.localPosition = Vector3.right * side * (style.Cells + scale) * Cell / 2;
            var original = wall.transform.localScale;
            wall.transform.localScale = new Vector3(original.x * scale / item.footprint.x,
                original.y * (style.Vehicle ? (style.CompactGate ? CompactGateClearHeight : GarageDoor.ClearHeight) / PersonTop : 1), original.z);
        }
    }

    private static void CreateShowcase(List<BuildItemData> items)
    {
        var previous = EditorSceneManager.GetActiveScene();
        bool additive = previous.IsValid() && !string.IsNullOrEmpty(previous.path);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
        EditorSceneManager.SetActiveScene(scene);
        try
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.57f, .54f, .49f);
            Light("Soft daylight", new Vector3(37, -30, 0), 1.10f);
            Light("Fill", new Vector3(35, 142, 0), .48f);
            var collection = new GameObject("Catalog boards");
            var doors = new List<GameObject>();
            for (int i = 0; i < items.Count; i++)
            {
                var style = DoorCollectionDesigns.All[i];
                var model = (GameObject)PrefabUtility.InstantiatePrefab(items[i].prefab, collection.transform);
                AddWallEnds(model.transform, style, .6f);
                doors.Add(model);
                float scale = style.Vehicle ? style.Number == 26 ? .27f : .41f : 1.18f;
                model.transform.localScale = Vector3.one * scale;
                model.transform.position = new Vector3((5 - i % 6 - 2.5f) * 4.10f, (3 - i / 6) * 3.05f, 0);
                Label(collection.transform, $"{style.Number:00}  {style.English}", model.transform.position + new Vector3(0, -.26f, .20f), .026f);
            }
            Label(collection.transform, "RETAIL EMPIRE TYCOON  /  DOOR COLLECTION", new Vector3(0, 12.55f, .25f), .048f);
            var camera = new GameObject("Collection camera", typeof(Camera)).GetComponent<Camera>();
            camera.scene = scene; camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.94f, .89f, .80f); camera.farClipPlane = 180;
            Aim(camera, new Vector3(0, 6.12f, 0), 7.45f, new Vector3(-5.4f, 2.4f, 40));
            AddFullScaleTestAreas(items);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save door showcase.");
            Capture(camera, "DoorCollection", 2200, 1230);
            for (int i = 0; i < doors.Count; i++) doors[i].GetComponent<CollectionDoor>().SetPose(
                DoorCollectionDesigns.All[i].Motion is DoorMechanism.Swing or DoorMechanism.Fold ? .53f : .79f);
            Capture(camera, "DoorCollectionOpen", 2200, 1230);
            Aim(camera, new Vector3(2, 4.2f, 0), 2.35f, new Vector3(-6.8f, 3.4f, 28));
            Capture(camera, "DoorComplexEntries", 1920, 740);
            Aim(camera, new Vector3(-4.1f, .9f, 0), 2.20f, new Vector3(-6.8f, 4.2f, 28));
            Capture(camera, "DoorVehicleTracks", 1550, 750);
        }
        finally
        {
            if (additive) { EditorSceneManager.CloseScene(scene, true); EditorSceneManager.SetActiveScene(previous); }
        }
    }

    private static void AddFullScaleTestAreas(List<BuildItemData> items)
    {
        var gallery = new GameObject("Full-scale pedestrian gallery").transform;
        gallery.position = new Vector3(42, 0, 28);
        AddTestSurface(gallery, "Pedestrian floor", new Vector3(18, -.12f, 9), new Vector3(43, .24f, 25));
        var lane = new GameObject("Full-scale vehicle test lane").transform;
        lane.position = new Vector3(-42, 0, 30);
        AddTestSurface(lane, "Vehicle runway", new Vector3(0, -.15f, 35), new Vector3(13, .30f, 112));
        int foot = 0, vehicle = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var style = DoorCollectionDesigns.All[i];
            var model = (GameObject)PrefabUtility.InstantiatePrefab(items[i].prefab, style.Vehicle ? lane : gallery);
            if (style.Vehicle) model.transform.localPosition = Vector3.forward * (9 + vehicle++ * 15);
            else
            {
                model.transform.localPosition = new Vector3(foot % 7 * 6, 0, foot / 7 * 8);
                foot++;
            }
            AddWallEnds(model.transform, style, 2);
        }
        var pickup = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/City/Pickup.prefab");
        var vehicleTest = (GameObject)PrefabUtility.InstantiatePrefab(pickup, lane);
        vehicleTest.name = "Player pickup for test drives";
        vehicleTest.transform.localPosition = new Vector3(0, .1f, .5f);
        var laneCamera = new GameObject("Vehicle lane camera", typeof(Camera)).GetComponent<Camera>();
        laneCamera.transform.SetParent(vehicleTest.transform, false);
        laneCamera.transform.localPosition = new Vector3(0, 3.2f, -6);
        laneCamera.transform.localRotation = Quaternion.Euler(13, 0, 0);
        laneCamera.enabled = false;
    }

    private static void AddTestSurface(Transform parent, string name, Vector3 position, Vector3 size)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = name;
        ground.transform.SetParent(parent, false);
        ground.transform.localPosition = position;
        ground.transform.localScale = size;
        ground.GetComponent<Renderer>().sharedMaterial = materials[(int)Finish.Steel];
        GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);
    }

    private static void Label(Transform parent, string text, Vector3 position, float size)
    {
        var label = new GameObject(text, typeof(TextMesh)).GetComponent<TextMesh>();
        label.transform.SetParent(parent, false); label.transform.localPosition = position;
        label.transform.localRotation = Quaternion.Euler(0, 180, 0);
        label.text = text; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
        label.fontSize = 48; label.characterSize = size; label.color = new Color(.12f, .23f, .17f);
    }
    private static void Light(string name, Vector3 rotation, float intensity)
    {
        var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional; light.intensity = intensity; light.transform.rotation = Quaternion.Euler(rotation);
    }
    private static void Aim(Camera camera, Vector3 target, float size, Vector3 offset)
    {
        camera.transform.position = target + offset; camera.transform.LookAt(target); camera.orthographicSize = size;
    }
    private static void Capture(Camera camera, string name, int width, int height)
    {
        var previous = RenderTexture.active;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes($"{Pictures}/{name}.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image);
        }
    }
}
