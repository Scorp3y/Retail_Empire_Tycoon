using UnityEngine;

internal sealed partial class AutoWorksModel
{
    private void BuildLot()
    {
        foreach (var patch in OutsidePits(Rect.MinMaxRect(-42, -18, 42, 15.9f)))
            Block(yard, "Yard and hall floor", Finish.Asphalt,
                new Vector3(patch.center.x, -.075f, patch.center.y), new Vector3(patch.width, .15f, patch.height));
        Block(yard, "West apron edge", Finish.Asphalt, new Vector3(10.55f, -.075f, 16.95f), new Vector3(62.9f, .15f, 2.1f));
        Block(yard, "East apron edge", Finish.Asphalt, new Vector3(-34.55f, -.075f, 16.95f), new Vector3(14.9f, .15f, 2.1f));
        Box(yard, Finish.Concrete, 17.3f, .0155f, -1.85f, 41.8f, .021f, 1.80f);
        Box(yard, Finish.Concrete, 7, .0155f, -16f, 2f, .021f, 4);
        var paint = Group(yard, "Unnumbered west parking");
        for (int i = 0; i <= 3; i++)
            Surface(paint, Finish.Line).Ground(38 - 3.1f * i, 2.1f, .09f, 6, .007f, White);
        Surface(paint, Finish.Line).Ground(33.305f, -.855f, 9.39f, .09f, .007f, White);
        for (int i = 0; i < 24; i++)
            Box(yard, Finish.Concrete, 37.8f - 1.76f * i, .0264f, -1.85f, .018f, .001f, 1.72f);
        DressYard();

        FenceRun(new Vector3(40.77f, 0, 17.48f), new Vector3(-20.5f, 0, 17.48f), true, "West frontage");
        FenceRun(new Vector3(-27.5f, 0, 17.48f), new Vector3(-41.87f, 0, 17.48f), true, "East frontage");
        FenceRun(new Vector3(40.9f, 0, 17.47f), new Vector3(40.9f, 0, -16f), false, "West side");
        FenceRun(new Vector3(-41.87f, 0, 17.47f), new Vector3(-41.87f, 0, -16f), false, "East side");
        FenceRun(new Vector3(40.9f, 0, -16), new Vector3(8, 0, -16), false, "West rear");
        FenceRun(new Vector3(6, 0, -16), new Vector3(-41.87f, 0, -16), false, "East rear");
        foreach (float x in new[] {40.74f, -20.25f, -27.75f, -41.65f})
        {
            Box(fence, Finish.Brick, x, .785f, 17.48f, .49f, 1.57f, .49f, .021f);
            BrickFront(fence, x, .49f, 0, 1.56f, 17.727f);
            Box(fence, Finish.Concrete, x, 1.61f, 17.48f, .59f, .13f, .58f, .026f);
            Solid(fence, "Brick pier collision", new Vector3(x, .83f, 17.48f), new Vector3(.50f, 1.66f, .50f));
            Sconce(fence, x, 1.24f, 17.70f, false);
        }

        var nav = Group(canopy, "Timber frame");
        PanelRoof(nav, Finish.Olive, 24, 39, -11, -3, 3.65f, 3.39f, 17);
        foreach (float x in new[] {24.6f, 31.5f, 38.45f})
            foreach (float z in new[] {-10.45f, -3.64f})
            {
                float y = z < -7 ? 3.47f : 3.25f;
                Block(nav, "Timber post collision", Finish.Timber, new Vector3(x, y / 2, z), new Vector3(.24f, y, .25f), .023f);
                Box(nav, Finish.Concrete, x, .11f, z, .33f, .23f, .34f, .018f);
                float toward = x > 35 ? -1 : 1;
                Beam(nav, Finish.Timber, new(x, y - .87f, z), new(x + .77f * toward, y - .03f, z), .145f);
                Beam(nav, Finish.Timber, new(x, y - .81f, z), new(x, y + .005f, z + (z < -7 ? .73f : -.73f)), .142f);
            }
        foreach (float z in new[] {-10.45f, -3.64f})
            Box(nav, Finish.Timber, 31.5f, z < -7 ? 3.46f : 3.23f, z, 14.3f, .20f, .25f, .024f);
        Box(nav, Finish.Concrete, 31.5f, .017f, -7.1f, 14.75f, .027f, 7.5f);
        Box(nav, Finish.Plaster, 31.5f, 1.6f, -10.79f, 14.48f, 3.2f, .17f);
        Box(nav, Finish.Brick, 31.5f, .38f, -10.68f, 14.48f, .76f, .12f);
        Strip(nav, new Vector3(27, 3.18f, -6.45f), 1.7f);
        Strip(nav, new Vector3(36, 3.18f, -6.45f), 1.7f);
        Lamp(new Vector3(32, 2.95f, -7.6f), 10f, 5.0f, "Canopy warm light");
        var tools = Group(canopy, "Tyres rims and workbench");
        Shelving(tools, 36.2f, -10.02f, true);
        Desk(tools, 27.45f, -10.07f, true);
        Pegboard(tools, 27.36f, 1.98f, -10.65f);
        ToolChest(tools, 25.75f, -10.01f, true);
        ToolChest(tools, 29.53f, -10.01f, false);
        Hose(tools, 31.22f, 1.81f, -10.61f);
        Compressor(tools, 31.22f, -9.8f);

        var reception = Group(props, "Bench and cycle rack");
        Bench(reception, 8.5f, -2.64f);
        for (int i = 0; i < 4; i++)
        {
            float x = -.45f - .46f * i;
            const float low = .025f;
            Vector3 p = new(x, low, -2.77f);
            foreach (var q in new[] {new Vector3(x,.53f,-2.77f),new Vector3(x,.64f,-2.68f),new Vector3(x,.64f,-2.24f),new Vector3(x,.54f,-2.15f),new Vector3(x,low,-2.15f)})
            {
                Rod(reception, Finish.Olive, p, q, .037f, 6);
                p = q;
            }
        }
        PlantYard();
    }

    private void FenceRun(Vector3 start, Vector3 end, bool brick, string name)
    {
        var run = Group(fence, name);
        float length = Vector3.Distance(start, end);
        var axis = (end - start).normalized;
        run.localPosition = (start + end) * .5f;
        run.localRotation = Quaternion.FromToRotation(Vector3.right, axis);
        float bottom = brick ? .59f : .17f;
        Solid(run, "Fence collision", new Vector3(0, .72f, 0), new Vector3(length, 1.46f, .22f));
        if (brick)
        {
            Box(run, Finish.Brick, 0, .276f, 0, length, .552f, .26f);
            Box(run, Finish.Concrete, 0, .56f, 0, length, .069f, .30f);
            // Frontage runs face the street in world/local space; mortar faces outward after rotation.
            var mortar = Surface(run, Finish.Plaster);
            float z = -.131f;
            for (int i = 0; i < 3; i++)
            {
                float y = .128f + .134f * i;
                mortar.Quad(new(length/2,y,z),new(-length/2,y,z),new(-length/2,y+.014f,z),new(length/2,y+.014f,z),White);
                for (float x = -length / 2 + .2f + .26f * (i % 2); x < length / 2 - .1f; x += .53f)
                    mortar.Quad(new(x+.015f,y,z),new(x,y,z),new(x,y+.134f,z),new(x+.015f,y+.134f,z),White);
            }
        }
        foreach (float y in new[] {bottom + .065f, 1.40f})
            Box(run, Finish.Steel, 0, y, 0, length, .075f, .085f);
        int bays = Mathf.CeilToInt(length / 3.65f);
        for (int k = 0; k <= bays; k++)
            Box(run, Finish.Steel, -length / 2 + k * length / bays, (bottom + 1.46f) / 2, 0,
                .112f, 1.46f - bottom, .127f, .011f);
        var rods = Surface(run, Finish.Steel);
        float barSpacing = brick ? .395f : .45f;
        for (float x = -length / 2 + barSpacing; x < length / 2 - .08f; x += barSpacing)
        {
            // Thin infill bars are welded to the rails. The separate posts retain full profiles.
            var a = new Vector3(x - .023f, bottom + .04f, -.018f);
            var b = new Vector3(x + .023f, bottom + .04f, .018f);
            var rise = Vector3.up * (1.36f - bottom);
            rods.Quad(a, b, b + rise, a + rise, White);
            rods.Quad(b, a, a + rise, b + rise, White);
        }
    }

    private void Bench(Transform group, float x, float z)
    {
        foreach (float dx in new[] {-.88f, .88f})
        {
            Beam(group, Finish.Steel, new(x + dx, .025f, z - .19f), new(x + dx, .88f, z - .25f), .071f);
            Beam(group, Finish.Steel, new(x + dx, .025f, z + .20f), new(x + dx, .45f, z + .19f), .069f);
        }
        for (int i = 0; i < 4; i++)
            Box(group, Finish.Timber, x, .45f, z - .16f + i * .106f, 2.12f, .071f, .093f, .012f);
        for (int i = 0; i < 2; i++)
            Box(group, Finish.Timber, x, .66f + i * .17f, z - .227f, 2.12f, .137f, .062f, .014f);
    }

    private void Compressor(Transform group, float x, float z)
    {
        var p = new Vector3(x, .35f, z);
        Rod(group, Finish.Yellow, p - Vector3.right * .36f, p + Vector3.right * .36f, .24f, 12);
        var shell = Surface(group, Finish.Yellow);
        foreach (float sign in new[] {-1f, 1f})
            shell.Ellipsoid(p + Vector3.right * (.34f * sign), new Vector3(.16f, .24f, .24f), White, 10, 4);
        Box(group, Finish.Steel, x + .05f, .67f, z, .36f, .26f, .27f, .031f);
        for (int i = 0; i < 5; i++) Box(group, Finish.Roof, x - .09f + i * .067f, .76f, z, .022f, .16f, .31f);
        Rod(group, Finish.Steel, p + new Vector3(-.51f, .08f, .07f), p + new Vector3(-.52f, .38f, .07f), .029f, 6);
        foreach (float dx in new[] {-.34f, .34f})
            Rod(group, Finish.Steel, new(x + dx - .042f, .115f, z + .13f), new(x + dx + .042f, .115f, z + .13f), .112f, 8);
    }

    private void PlantYard()
    {
        var planting = Group(greenery, "Perimeter planting strips");
        Box(planting, Finish.Green, 10.21f, .011f, 16.72f, 60.6f, .018f, .99f);
        Box(planting, Finish.Green, -34.12f, .011f, 16.72f, 12.55f, .018f, .99f);
        Box(planting, Finish.Green, 40.37f, .011f, 1.35f, .95f, .018f, 29.1f);
        Box(planting, Finish.Green, -41.30f, .011f, 1.15f, .77f, .018f, 31.5f);
        for (int i = 0; i < 21; i++)
        {
            float x = 39.65f - i * 2.89f;
            Shrub(planting, x, 16.67f, .27f + .09f * (i % 3));
            if (i % 3 == 1) Grass(planting, x - .75f, 16.63f);
        }
        for (int i = 0; i < 5; i++)
        {
            Shrub(planting, -29.1f - 2.58f * i, 16.63f, .38f);
            Grass(planting, -30.05f - 2.44f * i, 16.63f);
        }
        for (int i = 0; i < 11; i++)
        {
            float z = 14.5f - i * 2.58f;
            Shrub(planting, 40.36f, z, .30f + .06f * (i % 3));
            Shrub(planting, -41.29f, z, .29f + .04f * (i % 3));
            if (i % 3 == 0) Grass(planting, 40.4f, z - .85f);
        }
        var flowers = Group(greenery, "Reception flower box");
        Box(flowers, Finish.Timber, 11.9f, .18f, -2.56f, 1.62f, .32f, .56f, .031f);
        Box(flowers, Finish.Olive, 11.9f, .338f, -2.56f, 1.47f, .033f, .46f);
        for (int i = 0; i < 6; i++)
        {
            float x = 11.32f + .232f * i, z = -2.56f + (i % 2 == 0 ? .11f : -.10f);
            var p = new Vector3(x, .61f + .042f * (i % 3), z);
            Rod(flowers, Finish.Green, new(x,.33f,z), p, .023f, 5);
            Surface(flowers, i % 3 == 0 ? Finish.Line : Finish.Red).Ellipsoid(p, new(.09f,.076f,.092f),White, 6, 3);
        }
    }

    private void Shrub(Transform group, float x, float z, float size, float bottom = 0)
    {
        Surface(group, Finish.Olive).Ellipsoid(new Vector3(x, bottom + size * .53f, z), new Vector3(size, size * .82f, size * .79f), White, 7, 3);
        Surface(group, Finish.Green).Ellipsoid(new Vector3(x + size * .34f, bottom + size * .66f, z + .03f),
            new Vector3(size * .74f, size * .73f, size * .70f), White, 6, 3);
    }

    private void Grass(Transform group, float x, float z)
    {
        var leaves = Surface(group, Finish.Green);
        for (int i = 0; i < 7; i++)
        {
            float angle = i * 2.39996f;
            var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var p = new Vector3(x, .022f, z) + dir * .045f;
            var across = Vector3.Cross(Vector3.up, dir) * .028f;
            var tip = p + Vector3.up * (.30f + (i % 3) * .047f) + dir * .23f;
            leaves.Triangle(p-across,p+across,tip,White);
            leaves.Triangle(p+across,p-across,tip,White);
        }
    }

    private void Pot(Transform group, Vector3 p, float radius, float height)
    {
        Cylinder(group, Finish.Brick, p + Vector3.up * radius, radius * .73f, radius, radius * 2, 9);
        Cylinder(group, Finish.Timber, p + Vector3.up * radius * 1.92f, radius * 1.02f, radius * 1.07f, radius * .28f, 9);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 2.39f;
            var tip = p + new Vector3(Mathf.Cos(angle) * radius, height + .10f * (i % 3), Mathf.Sin(angle) * radius);
            Rod(group, Finish.Olive, p + Vector3.up * radius * 2, tip, .017f, 5);
            Surface(group, Finish.Green).Ellipsoid(tip, new Vector3(.13f, .105f, .14f), White, 6, 3);
        }
    }
}




