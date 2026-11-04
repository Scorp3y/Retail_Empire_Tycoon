using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal sealed partial class AutoWorksModel
{
    internal const string Root = "Assets/Art/AutoWorks";
    internal const string Prefab = Root + "/AutoWorks.prefab";
    internal const float Width = 5.4f;
    internal const float Facade = -1f;
    internal const float Rear = -15f;
    internal const float Eaves = 5.55f;
    internal static readonly Vector3 TownPosition = new(120, .09f, -168);
    internal static readonly Quaternion TownRotation = Quaternion.Euler(0, 180, 0);
    internal static readonly float[] BayX = {-14, -24, -34};
    private const float West = -6.5f, East = -41.5f, Wall = .32f;
    private const float DoorPlane = -1.56f;
    private const float Height = GarageDoor.ClearHeight;
    private const TownColor White = TownColor.White;
    private enum Finish { Plaster, Brick, Concrete, Olive, Roof, Glass, Timber, Asphalt, Steel, Lamp, Yellow, Red, Green, Line }

    private readonly Dictionary<Finish, Material> palette = new();
    private readonly Dictionary<(Transform, Finish), TownMeshBuilder> surfaces = new();
    private readonly List<Light> lamps = new();
    private readonly List<Renderer> glowing = new();
    private readonly Transform site;
    private readonly Transform building, canopy, interior, doors, fence, yard, greenery, lights, props;

    private AutoWorksModel()
    {
        site = Group(null, "STO_Root");
        building = Group(site, "Building");
        canopy = Group(site, "Canopy");
        interior = Group(site, "Interior");
        doors = Group(site, "Doors", false);
        fence = Group(site, "Fence");
        yard = Group(site, "Yard");
        greenery = Group(site, "Greenery");
        lights = Group(site, "Lights", false);
        props = Group(site, "Props");
    }

    internal static GameObject Save()
    {
        foreach (string folder in new[] {"Materials", "Meshes", "TownCuts/Meshes"})
            Directory.CreateDirectory(Root + "/" + folder);
        AssetDatabase.Refresh();
        var model = new AutoWorksModel();
        try
        {
            model.MakePalette();
            model.BuildEnvelope();
            model.BuildInterior();
            model.BuildLot();
            model.BuildDoors();
            model.BakeSurfaces();
            var lighting = model.site.gameObject.AddComponent<AutoWorksLighting>();
            var settings = new SerializedObject(lighting);
            SetArray(settings.FindProperty("emissiveSurfaces"), model.glowing);
            SetArray(settings.FindProperty("localLights"), model.lamps);
            settings.ApplyModifiedPropertiesWithoutUndo();
            var saved = PrefabUtility.SaveAsPrefabAsset(model.site.gameObject, Prefab);
            if (saved == null) throw new System.InvalidOperationException("Auto Works prefab could not be saved.");
            return saved;
        }
        finally { Object.DestroyImmediate(model.site.gameObject); }
    }

    private void MakePalette()
    {
        var colors = new[] {"#D9BE8F", "#8F4A3A", "#CDBA96", "#4F6545", "#454A50", "#668891", "#B9793D",
            "#575B5E", "#292E2D", "#FFC66B", "#E3B93C", "#BC4433", "#71854B", "#EEE5CE"};
        foreach (Finish finish in System.Enum.GetValues(typeof(Finish)))
        {
            if (finish == Finish.Asphalt)
            {
                // The street and the retained ramp use this same atlas and ground shader.
                var road = AssetDatabase.LoadAssetAtPath<Material>(TownCityCorrectionsPlacement.Root + "/Materials/Ground0.mat");
                if (road == null) throw new System.InvalidOperationException("The city road material is missing.");
                palette.Add(finish, road);
                continue;
            }
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) {name = "AW " + finish};
            ColorUtility.TryParseHtmlString(colors[(int)finish], out var color);
            material.SetFloat("_Smoothness", finish == Finish.Glass ? .48f : .13f);
            if (finish == Finish.Glass)
            {
                color.a = .46f;
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
            }
            if (finish == Finish.Lamp)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.15f);
            }
            material.SetColor("_BaseColor", color);
            string path = Root + "/Materials/" + finish + ".mat";
            TownBakeryPlacement.SaveAsset(material, path);
            palette.Add(finish, AssetDatabase.LoadAssetAtPath<Material>(path));
        }
    }

    private static Transform Group(Transform parent, string name, bool isStatic = true)
    {
        var node = new GameObject(name).transform;
        node.SetParent(parent, false);
        node.gameObject.isStatic = isStatic && parent != null;
        return node;
    }

    private TownMeshBuilder Surface(Transform parent, Finish finish)
    {
        var key = (parent, finish);
        if (!surfaces.TryGetValue(key, out var mesh))
            surfaces.Add(key, mesh = new TownMeshBuilder(finish == Finish.Asphalt ? _ => TownPalette.UV(TownColor.Asphalt) : null));
        return mesh;
    }

    private void BakeSurfaces()
    {
        var names = new HashSet<string>();
        foreach (var pair in surfaces)
        {
            var (parent, finish) = pair.Key;
            string pathKey = parent.name;
            for (var ancestor = parent.parent; ancestor != site && ancestor != null; ancestor = ancestor.parent)
                pathKey = ancestor.name + "_" + pathKey;
            string id = pathKey.Replace(' ', '_') + "_" + finish;
            if (!names.Add(id)) throw new System.InvalidOperationException("Duplicate Auto Works batch name: " + id);
            var draft = pair.Value.Build(id);
            if (finish == Finish.Asphalt)
                draft.uv2 = draft.vertices.Select(p =>
                {
                    var city = TownPosition + TownRotation * parent.TransformPoint(p);
                    return new Vector2(city.x, city.z);
                }).ToArray();
            var mesh = SaveMesh(draft, id);
            var renderer = AttachMesh(parent, finish.ToString(), mesh, finish, parent.gameObject.isStatic);
            if (finish == Finish.Lamp) glowing.Add(renderer);
        }
    }

    private static Mesh SaveMesh(TownMeshBuilder geometry, string name)
        => SaveMesh(geometry.Build(name), name);

    private static Mesh SaveMesh(Mesh geometry, string name)
    {
        string path = Root + "/Meshes/" + name + ".asset";
        TownBakeryPlacement.SaveAsset(geometry, path);
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    private MeshRenderer AttachMesh(Transform parent, string name, Mesh mesh, Finish finish, bool isStatic)
    {
        var node = Group(parent, name, isStatic);
        node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = node.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = palette[finish];
        if (finish is Finish.Glass or Finish.Asphalt) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }

    private static void SetArray<T>(SerializedProperty property, IReadOnlyList<T> values) where T : Object
    {
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private void Box(Transform node, Finish finish, Vector3 position, Vector3 size, float bevel = 0)
        => BeveledBox(Surface(node, finish), position, size, bevel);

    private void Box(Transform node, Finish finish, float x, float y, float z, float w, float h, float d, float bevel = 0)
        => Box(node, finish, new Vector3(x, y, z), new Vector3(w, h, d), bevel);

    private static void BeveledBox(TownMeshBuilder mesh, Vector3 center, Vector3 size, float bevel)
    {
        if (bevel < .017f || Mathf.Min(size.x, Mathf.Min(size.y, size.z)) < .115f) { mesh.Box(center, size, White); return; }
        var h = size * .5f;
        float b = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * .8f);
        Vector3 Point(int corner, int ring)
        {
            float x = h.x - (ring == 0 || ring == 3 ? b : 0);
            float z = h.z - (ring == 0 || ring == 3 ? b : 0);
            float k = ring == 0 || ring == 3 ? b * .34f : b;
            float y = ring <= 1 ? -h.y + (ring == 1 && b >= .03f ? b : 0) : h.y - (ring == 2 && b >= .03f ? b : 0);
            Vector2 p = (corner % 8) switch
            {
                0 => new(-x + k, z), 1 => new(x - k, z), 2 => new(x, z - k), 3 => new(x, -z + k),
                4 => new(x - k, -z), 5 => new(-x + k, -z), 6 => new(-x, -z + k), _ => new(-x, z - k)
            };
            return center + new Vector3(p.x, y, p.y);
        }
        int firstRing = b >= .03f ? 0 : 1;
        int lastRing = b >= .03f ? 3 : 2;
        for (int i = 0; i < 8; i++)
            for (int ring = firstRing; ring < lastRing; ring++)
                mesh.Quad(Point(i, ring), Point(i + 1, ring), Point(i + 1, ring + 1), Point(i, ring + 1), White);
        for (int i = 1; i < 7; i++)
        {
            mesh.Triangle(Point(0, lastRing), Point(i, lastRing), Point(i + 1, lastRing), White);
            mesh.Triangle(Point(0, firstRing), Point(i + 1, firstRing), Point(i, firstRing), White);
        }
    }

    private static BoxCollider Solid(Transform parent, string name, Vector3 center, Vector3 size, bool trigger = false)
    {
        var node = Group(parent, name, parent.gameObject.isStatic && !trigger);
        node.localPosition = center;
        var box = node.gameObject.AddComponent<BoxCollider>();
        box.size = size;
        box.isTrigger = trigger;
        return box;
    }

    private void Block(Transform node, string name, Finish finish, Vector3 center, Vector3 size, float bevel = 0)
    {
        Box(node, finish, center, size, bevel);
        Solid(node, name, center, size);
    }

    private void Beam(Transform node, Finish finish, Vector3 a, Vector3 b, float width, float depth = 0)
        => Surface(node, finish).Box((a + b) * .5f, new Vector3(width, (b - a).magnitude, depth > 0 ? depth : width),
            White, Quaternion.FromToRotation(Vector3.up, b - a));

    private void Rod(Transform node, Finish finish, Vector3 a, Vector3 b, float radius, int sides = 8)
        => Surface(node, finish).Cylinder((a + b) * .5f, radius, radius, (b - a).magnitude, White, sides,
            Quaternion.FromToRotation(Vector3.up, b - a));

    private void Cylinder(Transform node, Finish finish, Vector3 p, float bottom, float top, float height, int sides = 8)
        => Surface(node, finish).Cylinder(p, bottom, top, height, White, sides);

    private void Lamp(Vector3 p, float range, float intensity, string name)
    {
        var light = Group(lights, name, false).gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.transform.localPosition = p;
        light.range = range;
        light.intensity = intensity;
        light.color = new Color(1, .81f, .58f);
        lamps.Add(light);
    }
}




