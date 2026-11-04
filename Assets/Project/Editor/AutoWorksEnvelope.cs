using UnityEngine;
using UnityEditor;

internal sealed partial class AutoWorksModel
{
    private void BuildEnvelope()
    {
        var garage = Group(building, "Garage shell");
        float right = West;
        foreach (float x in BayX)
        {
            float jamb = x + Width * .5f;
            FacadePier(garage, jamb, right);
            Block(garage, "Header clearance", Finish.Plaster, new Vector3(x, (Height + Eaves) * .5f, Facade - Wall * .5f),
                new Vector3(Width, Eaves - Height, Wall));
            Box(garage, Finish.Olive, x, Height + .12f, Facade + .10f, Width + .35f, .23f, .29f, .024f);
            Box(garage, Finish.Olive, jamb + .10f, Height * .5f, Facade + .085f, .20f, Height, .27f, .014f);
            Box(garage, Finish.Olive, x - Width * .5f - .10f, Height * .5f, Facade + .085f, .20f, Height, .27f, .014f);
            right = x - Width * .5f;
        }
        FacadePier(garage, East, right);
        foreach (float x in new[] {-8.6f, -19f, -29f, -39.6f}) Sconce(garage, x, 4.09f, -.71f, false);
        Drainpipe(garage, -7f, 5.44f, -.72f);
        Drainpipe(garage, -40.95f, 5.44f, -.72f);

        var rear = Group(garage, "Rear wall and strip windows");
        float rearZ = Rear + Wall * .5f;
        WindowWall(rear, new Vector3(-24, 0, rearZ), 35, -10.8f, 10.8f, 3.14f, 4.65f, false);
        var sides = Group(garage, "Side walls and high windows");
        foreach (float x in new[] {West - Wall * .5f, East + Wall * .5f})
            WindowWall(sides, new Vector3(x, 0, -8), 14 - Wall * 2, -4.8f, 4.8f, 3.32f, 4.74f, true);

        var roof = Group(building, "Standing seam roof");
        RoofBay(roof, East - .23f, -34.2f, -15.23f, -8, -.77f);
        RoofBay(roof, -13.8f, West + .23f, -15.23f, -8, -.77f);
        RoofBay(roof, -34.2f, -13.8f, -15.23f, -9.19f, -.77f, -6.81f);
        foreach (float x in new[] {East, West})
        {
            var gable = Surface(roof, Finish.Plaster);
            var a = new Vector3(x, Eaves, -15);
            var b = new Vector3(x, Eaves, -1);
            var c = new Vector3(x, 6.45f, -8);
            gable.Triangle(a, c, b, White);
            gable.Triangle(a, b, c, White);
        }
        Box(roof, Finish.Roof, -24, 5.54f, -.77f, 35.6f, .13f, .16f, .026f);
        Box(roof, Finish.Roof, -24, 5.54f, -15.23f, 35.6f, .13f, .16f, .026f);
        Vent(roof, -37.6f, -10.6f, 6.03f);
        Vent(roof, -9.6f, -10.1f, 6.13f);

        var lantern = Group(roof, "Clerestory");
        foreach (float z in new[] {-9.18f, -6.82f})
        {
            Box(lantern, Finish.Olive, -24, 6.43f, z, 20.35f, .23f, .15f, .012f);
            Window(lantern, new Vector3(-24, 6.82f, z), 20.05f, .53f, 16, false);
        }
        foreach (float x in new[] {-34.10f, -13.9f}) Box(lantern, Finish.Olive, x, 6.77f, -8, .18f, .75f, 2.41f, .02f);
        PanelRoof(lantern, Finish.Roof, -34.2f, -13.8f, -9.4f, -6.6f, 7.08f, 7.22f, 20);

        var office = Group(building, "Reception");
        Block(office, "Rear reception wall", Finish.Plaster, new Vector3(5, 2.05f, -13.86f), new Vector3(17, 4.1f, .28f));
        foreach (float x in new[] {-3.36f, 13.36f})
            Block(office, "Reception return", Finish.Plaster, new Vector3(x, 2.05f, -9), new Vector3(.28f, 4.1f, 9.72f));
        Block(office, "Reception lintel", Finish.Plaster, new Vector3(5, 3.43f, -4), new Vector3(17, 1.34f, .29f));
        Box(office, Finish.Brick, 5, .38f, -13.7f, 16.8f, .76f, .08f);
        foreach (float x in new[] {-2.84f, 12.84f})
        {
            Block(office, "Reception brick column", Finish.Brick, new Vector3(x, 2.05f, -3.84f), new Vector3(.89f, 4.1f, .47f), .015f);
            BrickFront(office, x, .89f, 0, 4.1f, -3.601f);
            Box(office, Finish.Concrete, x, 4.12f, -3.84f, 1.02f, .16f, .59f, .025f);
        }
        PanelRoof(office, Finish.Roof, -3.7f, 13.7f, -14.2f, -3.8f, 4.14f, 4.33f, 18);
        PanelRoof(office, Finish.Olive, -2.3f, 12.3f, -3.78f, -2.68f, 3.15f, 2.82f, 12);
        Vent(office, 8.5f, -10.4f, 4.21f);
        Vent(office, -.7f, -9.1f, 4.24f);
        Sconce(office, .6f, 3.65f, -3.80f, true);
        Sconce(office, 9.4f, 3.65f, -3.80f, false);
        Drainpipe(office, -3.28f, 4.11f, -3.48f);
        BuildSign(office);

        foreach (float x in new[] {.65f, 9.35f})
        {
            Box(office, Finish.Brick, x, .38f, -3.94f, 5.75f, .76f, .32f);
            BrickFront(office, x, 5.75f, 0, .76f, -3.777f);
            Window(office, new Vector3(x, 1.72f, -3.84f), 5.73f, 1.94f, 4, false);
            Box(office, Finish.Olive, x, 2.14f, -3.759f, 5.73f, .063f, .09f);
        }
        GlassDoor(office, 5, -3.83f, true);
        GlassDoor(office, 5, -14.04f, true);
        Box(office, Finish.Concrete, 5, .018f, -9, 16.6f, .034f, 9.7f);
        Box(office, Finish.Plaster, 5, 3.94f, -9, 16.8f, .10f, 9.8f);
        Desk(office, 8, -5.22f, false);
        for (int i = 0; i < 2; i++) Pot(office, new Vector3(i == 0 ? -1.55f : 10.5f, .03f, -4.89f), .27f, .75f);
        Lamp(new Vector3(5, 2.65f, -6.3f), 9f, 3.8f, "Reception warm light");

        var service = Group(building, "Service block");
        Block(service, "Service solid", Finish.Plaster, new Vector3(18.5f, 1.75f, -9.25f), new Vector3(10, 3.5f, 9.5f), .018f);
        Box(service, Finish.Brick, 18.5f, .43f, -4.485f, 10, .86f, .17f);
        BrickFront(service, 18.5f, 10, 0, .86f, -4.395f);
        Box(service, Finish.Brick, 23.25f, 1.75f, -4.44f, .52f, 3.5f, .26f, .014f);
        BrickFront(service, 23.25f, .52f, .86f, 3.5f, -4.305f);
        Box(service, Finish.Concrete, 23.25f, 3.56f, -4.43f, .65f, .15f, .37f, .018f);
        PanelRoof(service, Finish.Roof, 13.3f, 23.7f, -14.2f, -4.25f, 3.51f, 3.72f, 11);
        GlassDoor(service, 17.5f, -4.348f, false);
        Box(service, Finish.Olive, 19.15f, 2.73f, -4.33f, .93f, .52f, .12f, .02f);
        for (int i = 0; i < 6; i++) Box(service, Finish.Steel, 19.15f, 2.54f + .073f * i, -4.255f, .81f, .021f, .031f);
        Drainpipe(service, 22.8f, 3.56f, -4.25f);
    }

    private void FacadePier(Transform group, float min, float max)
    {
        float x = (min + max) * .5f;
        float width = max - min;
        Block(group, "Facade solid pier", Finish.Plaster, new Vector3(x, Eaves * .5f, Facade - Wall * .5f),
            new Vector3(width, Eaves, Wall));
        Box(group, Finish.Brick, x, .44f, Facade + .032f, width, .88f, .105f);
        BrickFront(group, x, width, 0, .88f, Facade + .087f);
        float trim = Mathf.Min(1.08f, width - .40f);
        Box(group, Finish.Brick, x, 3.17f, Facade + .074f, trim, 4.58f, .18f, .015f);
        BrickFront(group, x, trim, .88f, 5.43f, Facade + .166f);
        Box(group, Finish.Concrete, x, 5.43f, Facade + .07f, trim + .19f, .19f, .29f, .023f);
    }

    private void BrickFront(Transform group, float x, float width, float bottom, float top, float z)
    {
        z += .010f;
        var mortar = Surface(group, Finish.Plaster);
        float min = x - width * .5f + .019f, max = x + width * .5f - .019f;
        const float rise = .155f, run = .365f;
        int row = 0;
        for (float y = bottom + .065f; y < top - .018f; y += rise, row++)
        {
            mortar.Quad(new(min, y, z), new(max, y, z), new(max, y + .014f, z), new(min, y + .014f, z), White);
            for (float px = min + (row % 2 == 0 ? run : run * .5f); px < max - .016f; px += run)
            {
                float end = Mathf.Min(y + rise, top - .016f);
                mortar.Quad(new(px, y, z), new(px + .012f, y, z), new(px + .012f, end, z), new(px, end, z), White);
            }
        }
    }

    private void WindowWall(Transform group, Vector3 origin, float width, float first, float last, float sill, float top, bool side)
    {
        Vector3 At(float u, float y) => origin + (side ? new Vector3(0, y, u) : new Vector3(u, y, 0));
        Vector3 Size(float w, float h, float d) => side ? new Vector3(d, h, w) : new Vector3(w, h, d);
        void Slab(float a, float b, float lower, float upper, Finish finish)
            => Block(group, "Window wall solid", finish, At((a + b) * .5f, (lower + upper) * .5f), Size(b - a, upper - lower, Wall));
        Slab(-width / 2, width / 2, 0, .84f, Finish.Brick);
        Slab(-width / 2, width / 2, .84f, sill, Finish.Plaster);
        Slab(-width / 2, width / 2, top, Eaves, Finish.Plaster);
        Slab(-width / 2, first, sill, top, Finish.Plaster);
        Slab(last, width / 2, sill, top, Finish.Plaster);
        Window(group, At((first + last) / 2, (sill + top) / 2), last - first, top - sill, side ? 8 : 15, side);
    }

    private void Window(Transform group, Vector3 p, float w, float h, int panes, bool side)
    {
        Vector3 At(float u, float y) => p + (side ? new Vector3(0, y, u) : new Vector3(u, y, 0));
        Vector3 Size(float x, float y, float z) => side ? new Vector3(z, y, x) : new Vector3(x, y, z);
        Box(group, Finish.Glass, p, Size(w - .07f, h - .07f, .038f));
        foreach (float y in new[] {-h / 2, h / 2}) Box(group, Finish.Olive, At(0, y), Size(w + .13f, .11f, .18f), .012f);
        for (int i = 0; i <= panes; i++) Box(group, Finish.Olive, At(-w * .5f + w * i / panes, 0), Size(.079f, h, .16f));
        if (h > 1) Box(group, Finish.Olive, At(0, .09f), Size(w, .066f, .14f));
    }

    private void GlassDoor(Transform group, float x, float z, bool doubleLeaf)
    {
        float w = doubleLeaf ? 2.88f : 1.3f;
        float h = doubleLeaf ? 2.67f : 2.28f;
        var door = Group(group, doubleLeaf ? (z > -5 ? "Reception glazed entrance" : "Rear pedestrian entrance") : "Service door");
        if (!doubleLeaf) Box(door, Finish.Olive, x, h * .5f, z, w, h, .12f, .025f);
        foreach (float dx in doubleLeaf ? new[] {-w / 2, 0f, w / 2} : new[] {-w / 2, w / 2})
            Box(door, Finish.Olive, x + dx, h * .5f, z + .017f, .11f, h, .17f, .011f);
        foreach (float y in new[] {.08f, .49f, h})
            Box(door, Finish.Olive, x, y, z + .017f, w + .11f, .10f, .17f, .011f);
        if (doubleLeaf)
        {
            Box(door, Finish.Glass, x, 1.56f, z + .013f, w - .09f, 2.15f, .042f);
            Box(door, Finish.Olive, x, .27f, z, w, .39f, .12f);
            foreach (float dx in new[] {-.16f, .16f})
                Box(door, Finish.Concrete, x + dx, 1.29f, z + .132f, .055f, .28f, .075f, .014f);
            Solid(door, "Glazed door collision", new Vector3(x, h * .5f, z), new Vector3(w, h, .13f));
        }
        else
        {
            Box(door, Finish.Steel, x, 1.74f, z + .072f, .68f, .56f, .04f, .01f);
            Box(door, Finish.Glass, x, 1.74f, z + .102f, .55f, .42f, .021f);
            Box(door, Finish.Lamp, x, 1.74f, z + .088f, .54f, .40f, .012f);
            Box(door, Finish.Olive, x, 1.74f, z + .124f, .035f, .43f, .023f);
            Box(door, Finish.Concrete, x + .43f, 1.09f, z + .12f, .19f, .052f, .07f, .011f);
        }
    }

    private void RoofBay(Transform roof, float min, float max, float back, float ridge, float front, float frontInner = float.NaN)
    {
        float Y(float z) => 6.45f - Mathf.Abs(z + 8) * .9f / 7.23f;
        int seams = Mathf.CeilToInt((max - min) / 1.13f);
        PanelRoof(roof, Finish.Roof, min, max, back, ridge, Y(back), Y(ridge), seams);
        if (float.IsNaN(frontInner)) frontInner = ridge;
        PanelRoof(roof, Finish.Roof, min, max, frontInner, front, Y(frontInner), Y(front), seams);
    }

    private void PanelRoof(Transform group, Finish finish, float left, float right, float back, float front,
        float backY, float frontY, int seams)
    {
        var a = new Vector3(left, backY, back);
        var b = new Vector3(left, frontY, front);
        var c = new Vector3(right, frontY, front);
        var d = new Vector3(right, backY, back);
        var roof = Surface(group, finish);
        var down = Vector3.down * .13f;
        roof.Quad(a, b, c, d, White);
        roof.Quad(d + down, c + down, b + down, a + down, White);
        roof.Quad(b + down, c + down, c, b, White);
        roof.Quad(d + down, a + down, a, d, White);
        roof.Quad(a + down, b + down, b, a, White);
        roof.Quad(c + down, d + down, d, c, White);
        for (int i = 0; i <= seams; i++)
        {
            float x = Mathf.Lerp(left + .055f, right - .055f, (float)i / seams);
            // Raised standing seams share the roof surface. Their underside is inside the sheet.
            var p = new Vector3(x, backY - .002f, back + .015f);
            var q = new Vector3(x, frontY - .002f, front - .015f);
            var u = Vector3.right * .024f;
            var v = Vector3.right * .014f;
            var lip = Vector3.up * .050f;
            roof.Quad(p - u, q - u, q - v + lip, p - v + lip, White);
            roof.Quad(p - v + lip, q - v + lip, q + v + lip, p + v + lip, White);
            roof.Quad(p + v + lip, q + v + lip, q + u, p + u, White);
            roof.Quad(p + u, p - u, p - v + lip, p + v + lip, White);
            roof.Quad(q - u, q + u, q + v + lip, q - v + lip, White);
        }
    }

    private void Drainpipe(Transform group, float x, float height, float z)
    {
        Rod(group, Finish.Roof, new Vector3(x, .21f, z), new Vector3(x, height - .18f, z), .078f);
        Rod(group, Finish.Roof, new Vector3(x, height - .18f, z), new Vector3(x, height, z - .17f), .08f);
        Rod(group, Finish.Roof, new Vector3(x, .21f, z), new Vector3(x, .12f, z + .16f), .08f);
        for (float y = .6f; y < height - .2f; y += 1.4f)
            Cylinder(group, Finish.Concrete, new Vector3(x, y, z), .098f, .098f, .055f);
    }

    private void Vent(Transform group, float x, float z, float y)
    {
        Box(group, Finish.Roof, x, y + .23f, z, .42f, .53f, .42f, .035f);
        Box(group, Finish.Steel, x, y + .52f, z, .56f, .105f, .56f, .028f);
    }

    private void Sconce(Transform group, float x, float y, float z, bool realLight)
    {
        Box(group, Finish.Steel, x, y - .11f, z, .15f, .34f, .09f, .023f);
        Rod(group, Finish.Steel, new(x, y, z), new(x, y + .20f, z + .21f), .038f, 6);
        Rod(group, Finish.Steel, new(x, y + .20f, z + .21f), new(x, y + .18f, z + .43f), .038f, 6);
        Rod(group, Finish.Steel, new(x, y + .18f, z + .43f), new(x, y + .032f, z + .51f), .038f, 6);
        Cylinder(group, Finish.Steel, new(x, y - .021f, z + .51f), .22f, .071f, .116f, 10);
        Cylinder(group, Finish.Lamp, new(x, y - .083f, z + .51f), .147f, .163f, .033f, 10);
        if (realLight) Lamp(new(x, y - .27f, z + .55f), 4.9f, 1.45f, "Facade warm sconce");
    }
}


