using UnityEngine;
using RetailEmpireTycoon.Editor.TownKit;

internal sealed partial class AutoWorksModel
{
    private void BuildInterior()
    {
        var floor = Group(interior, "Concrete and bay markings");
        foreach (var patch in OutsidePits(Rect.MinMaxRect(East, Rear, West, Facade)))
            Surface(floor, Finish.Concrete).Ground(patch.center.x, patch.center.y, patch.width, patch.height, .003f, White);
        foreach (float x in new[] {-9.7f, -18.9f, -29.1f, -38.3f})
            Surface(floor, Finish.Yellow).Ground(x, -7.65f, .105f, 11.6f, .007f, White);
        foreach (float x in BayX)
        {
            Surface(floor, Finish.Yellow).Ground(x, -13.07f, 4.2f, .10f, .007f, White);
        }
        BuildInspectionPits();
        var frame = Group(interior, "Steel frames");
        foreach (float x in new[] {East + .55f, -35.6f, -29.8f, -24f, -18.2f, -12.4f, West - .55f})
        {
            Beam(frame, Finish.Olive, new(x, 5.50f, -14.63f), new(x, 6.29f, -8), .16f, .12f);
            Beam(frame, Finish.Olive, new(x, 6.29f, -8), new(x, 5.50f, -1.37f), .16f, .12f);
            Beam(frame, Finish.Olive, new(x, 5.48f, -14.63f), new(x, 5.48f, -1.37f), .125f);
            Beam(frame, Finish.Olive, new(x, 5.48f, -10.8f), new(x, 6.29f, -8), .083f);
            Beam(frame, Finish.Olive, new(x, 5.48f, -5.2f), new(x, 6.29f, -8), .083f);
        }
        foreach (float x in new[] {East + .43f, West - .43f})
            foreach (float z in new[] {-14.42f, -10.1f, -5.9f, -1.68f})
                Block(frame, "Steel column collider", Finish.Olive, new Vector3(x, 2.76f, z), new Vector3(.19f, 5.52f, .23f), .013f);
        foreach (float x in new[] {-35f, -30.6f, -17.4f, -13f})
        {
            Strip(frame, new Vector3(x, 5.37f, -10.9f), 2.38f);
            Strip(frame, new Vector3(x, 5.37f, -4.2f), 2.38f);
        }
        Lamp(new Vector3(-16, 4.98f, -7.4f), 11, 6.0f, "West workshop fill");
        Lamp(new Vector3(-31, 4.98f, -7.4f), 11, 6.0f, "East workshop fill");

        var equipment = Group(props, "Workshop wall equipment");
        var west = Group(equipment, "West side tools");
        west.localPosition = new Vector3(-7.29f, 0, -7.8f);
        west.localRotation = Quaternion.Euler(0, -90, 0);
        ToolChest(west, -1.7f, 0, true);
        Desk(west, .44f, -.04f, true);
        Pegboard(west, .14f, 1.95f, -.47f);
        var east = Group(equipment, "East side tools");
        east.localPosition = new Vector3(-40.77f, 0, -9.75f);
        east.localRotation = Quaternion.Euler(0, 90, 0);
        Lockers(east, 1.48f, 0);
        Hose(east, -.2f, 1.53f, -.31f);
        Jack(east, -.97f, .12f);
        var back = Group(equipment, "Rear wall storage");
        back.localPosition = new Vector3(-24, 0, -14.12f);
        Shelving(back, -12.8f, 0, false);
        Shelving(back, 12.8f, 0, false);
        DressWorkshopWalls();
    }

    private void Strip(Transform parent, Vector3 p, float length)
    {
        Box(parent, Finish.Steel, p, new Vector3(.23f, .085f, length), .019f);
        Box(parent, Finish.Lamp, p + Vector3.down * .052f, new Vector3(.145f, .029f, length - .13f), .008f);
    }

    private void Desk(Transform parent, float x, float z, bool vise)
    {
        Block(parent, "Workbench top collision", Finish.Timber, new Vector3(x, 1.02f, z), new Vector3(2.42f, .15f, .71f), .031f);
        foreach (float dx in new[] {-1.09f, 1.09f})
            foreach (float dz in new[] {-.27f, .27f})
                Box(parent, Finish.Steel, x + dx, .49f, z + dz, .105f, .98f, .10f, .013f);
        Box(parent, Finish.Roof, x, .32f, z, 2.15f, .065f, .55f);
        if (vise)
        {
            Box(parent, Finish.Steel, x + .80f, 1.17f, z + .20f, .28f, .15f, .32f, .023f);
            foreach (float dx in new[] {.68f, .89f}) Box(parent, Finish.Roof, x + dx, 1.28f, z + .2f, .13f, .12f, .29f, .01f);
            Rod(parent, Finish.Concrete, new(x + .97f, 1.16f, z + .21f), new(x + .97f, 1.16f, z + .48f), .025f, 6);
        }
        float arm = x - .78f;
        Cylinder(parent, Finish.Steel, new Vector3(arm, 1.115f, z - .15f), .16f, .13f, .06f);
        Rod(parent, Finish.Steel, new(arm, 1.13f, z - .15f), new(arm + .16f, 1.58f, z - .12f), .034f, 6);
        Rod(parent, Finish.Steel, new(arm + .16f, 1.58f, z - .12f), new(arm + .36f, 1.65f, z + .02f), .028f, 6);
        Cylinder(parent, Finish.Olive, new Vector3(arm + .36f, 1.61f, z + .02f), .18f, .068f, .18f);
        Cylinder(parent, Finish.Lamp, new Vector3(arm + .36f, 1.52f, z + .02f), .126f, .145f, .036f);
    }

    private void ToolChest(Transform parent, float x, float z, bool red)
    {
        var finish = red ? Finish.Red : Finish.Roof;
        Block(parent, "Tool cabinet collision", finish, new Vector3(x, .64f, z), new Vector3(.91f, .99f, .57f), .042f);
        Box(parent, Finish.Steel, x, 1.17f, z, .98f, .085f, .60f, .023f);
        for (int i = 0; i < 5; i++)
        {
            float y = .29f + i * .16f;
            Box(parent, finish, x, y, z + .297f, .79f, .138f, .028f, .009f);
            Box(parent, Finish.Concrete, x, y + .035f, z + .32f, .47f, .035f, .035f, .009f);
        }
        foreach (float dx in new[] {-.32f, .32f})
            foreach (float dz in new[] {-.20f, .20f})
                Rod(parent, Finish.Steel, new(x + dx - .047f, .105f, z + dz), new(x + dx + .047f, .105f, z + dz), .096f, 8);
    }

    private void Pegboard(Transform parent, float x, float y, float z)
    {
        Box(parent, Finish.Timber, x, y, z, 2.14f, .93f, .077f, .023f);
        var holes = Surface(parent, Finish.Steel);
        for (int col = 0; col < 19; col++)
            for (int row = 0; row < 7; row++)
            {
                var p = new Vector3(x - .96f + col * .106f, y - .36f + row * .12f, z + .039f);
                holes.Triangle(p + new Vector3(-.012f, -.009f), p + new Vector3(.012f, -.009f), p + new Vector3(0, .012f), White);
            }
        for (int i = 0; i < 8; i++)
        {
            float px = x - .82f + .235f * i, top = y + .29f - .14f * (i % 2), end = top - .25f - .047f * (i % 3);
            var finish = i % 3 == 1 ? Finish.Steel : Finish.Concrete;
            Beam(parent, finish, new(px, top, z + .066f), new(px - .035f, end, z + .066f), .029f);
            Rod(parent, finish, new(px, top, z + .045f), new(px, top, z + .08f), .051f, 6);
            if (i % 3 == 1) Box(parent, Finish.Red, px - .035f, end + .05f, z + .074f, .05f, .16f, .043f, .013f);
        }
    }

    private void Shelving(Transform parent, float x, float z, bool wheels)
    {
        float width = wheels ? 3.45f : 2.35f;
        Block(parent, "Shelf base collision", Finish.Steel, new Vector3(x, .20f, z), new Vector3(width, .18f, .66f), .021f);
        foreach (float dx in new[] {-width / 2 + .04f, width / 2 - .04f})
            foreach (float dz in new[] {-.30f, .30f})
                Box(parent, Finish.Steel, x + dx, 1.13f, z + dz, .082f, 2.26f, .077f);
        if (wheels)
        {
            for (int row = 0; row < 2; row++)
            {
                float y = .35f + row * 1.01f;
                Box(parent, Finish.Roof, x, y, z, width, .09f, .67f);
                for (int j = 0; j < 5; j++) Tire(parent, x - 1.35f + j * .27f, y + .43f, z, false);
                for (int j = 0; j < 2; j++) Tire(parent, x + .57f + j * .72f, y + .39f, z, true);
            }
            return;
        }
        for (int row = 0; row < 3; row++)
        {
            float y = .25f + row * .67f;
            Box(parent, Finish.Roof, x, y, z, width, .083f, .69f);
            for (int j = 0; j < 3; j++)
            {
                float px = x - .73f + j * .70f;
                float h = .30f + .10f * ((row + j) % 3);
                Box(parent, Finish.Timber, px, y + .043f + h / 2, z, .50f, h, .46f, .017f);
                Box(parent, Finish.Concrete, px, y + .046f + h, z, .078f, .009f, .46f);
            }
        }
    }

    private void Tire(Transform group, float x, float y, float z, bool rim)
    {
        var axle = rim ? Vector3.forward : Vector3.right;
        var center = new Vector3(x, y, z);
        float thickness = rim ? .14f : .19f;
        var rotation = Quaternion.FromToRotation(Vector3.up, axle);
        var rubber = Surface(group, Finish.Steel);
        Vector3 Point(int i, float radius, float along)
        {
            float angle = i * Mathf.PI / 6;
            return center + rotation * new Vector3(Mathf.Cos(angle) * radius, along, Mathf.Sin(angle) * radius);
        }
        for (int i = 0; i < 12; i++)
        {
            float h = thickness * .5f;
            rubber.Quad(Point(i,.35f,-h),Point(i,.35f,h),Point(i+1,.35f,h),Point(i+1,.35f,-h),White);
            rubber.Quad(Point(i,.35f,h),Point(i,.30f,h+.032f),Point(i+1,.30f,h+.032f),Point(i+1,.35f,h),White);
            rubber.Quad(Point(i,.30f,-h-.032f),Point(i,.35f,-h),Point(i+1,.35f,-h),Point(i+1,.30f,-h-.032f),White);
            rubber.Triangle(center+axle*(h+.032f),Point(i+1,.30f,h+.032f),Point(i,.30f,h+.032f),White);
            rubber.Triangle(center-axle*(h+.032f),Point(i,.30f,-h-.032f),Point(i+1,.30f,-h-.032f),White);
        }
        if (!rim) return;
        var hub = center + Vector3.forward * .095f;
        Rod(group, Finish.Concrete, hub, hub + Vector3.forward * .025f, .244f, 12);
        Rod(group, Finish.Steel, hub + Vector3.forward * .025f, hub + Vector3.forward * .044f, .195f, 12);
        for (int spoke = 0; spoke < 5; spoke++)
        {
            var tip = Quaternion.Euler(0, 0, 72 * spoke) * Vector3.up * .205f;
            Beam(group, Finish.Concrete, hub + Vector3.forward * .049f, hub + Vector3.forward * .049f + tip, .064f, .029f);
        }
        Rod(group, Finish.Roof, hub + Vector3.forward * .05f, hub + Vector3.forward * .063f, .064f, 8);
    }

    private void Lockers(Transform group, float x, float z)
    {
        Block(group, "Locker collision", Finish.Olive, new Vector3(x, 1.04f, z), new Vector3(1.61f, 2.04f, .53f), .031f);
        for (int i = 0; i < 3; i++)
        {
            float px = x - .51f + i * .51f;
            Box(group, Finish.Green, px, 1.08f, z + .278f, .48f, 1.89f, .033f, .012f);
            Box(group, Finish.Concrete, px + .16f, 1.04f, z + .307f, .039f, .19f, .032f, .009f);
            for (int j = 0; j < 4; j++) Box(group, Finish.Steel, px, 1.68f + j * .047f, z + .297f, .24f, .017f, .013f);
        }
    }

    private void Hose(Transform group, float x, float y, float z)
    {
        for (int i = 0; i < 22; i++)
        {
            float a = i * Mathf.PI / 8, b = (i + 1) * Mathf.PI / 8;
            Vector3 Point(float angle) => new(x + Mathf.Cos(angle) * .235f, y + Mathf.Sin(angle) * .32f, z + .065f * angle);
            Rod(group, Finish.Green, Point(a), Point(b), .033f, 6);
        }
    }

    private void Jack(Transform group, float x, float z)
    {
        Box(group, Finish.Red, x, .12f, z, .37f, .19f, .74f, .036f);
        Cylinder(group, Finish.Steel, new Vector3(x, .26f, z + .20f), .11f, .12f, .079f);
        Rod(group, Finish.Steel, new(x, .16f, z - .24f), new(x, .84f, z + .05f), .035f, 8);
        Box(group, Finish.Roof, x, .84f, z + .05f, .21f, .063f, .061f, .019f);
    }
}


