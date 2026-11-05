using System.Collections.Generic;
using UnityEngine;

internal sealed partial class AutoWorksModel
{
    internal const float PitWidth = 1.18f;
    internal const float PitLength = 6.4f;
    internal const float PitDepth = 1.45f;
    internal const float PitZ = -8f;

    internal static Rect InspectionPit(float x) => new(x - PitWidth / 2, PitZ - PitLength / 2, PitWidth, PitLength);

    private static IEnumerable<Rect> OutsidePits(Rect surface)
    {
        var first = InspectionPit(BayX[0]);
        yield return Rect.MinMaxRect(surface.xMin, surface.yMin, surface.xMax, first.yMin);
        yield return Rect.MinMaxRect(surface.xMin, first.yMax, surface.xMax, surface.yMax);
        float start = surface.xMin;
        for (int i = BayX.Length - 1; i >= 0; i--)
        {
            var hole = InspectionPit(BayX[i]);
            yield return Rect.MinMaxRect(start, hole.yMin, hole.xMin, hole.yMax);
            start = hole.xMax;
        }
        yield return Rect.MinMaxRect(start, first.yMin, surface.xMax, first.yMax);
    }

    private void BuildInspectionPits()
    {
        var group = Group(interior, "Inspection pits");
        var support = Group(yard, "Load-bearing pit grating");
        for (int i = 0; i < BayX.Length; i++)
        {
            float x = BayX[i];
            var opening = InspectionPit(x);
            var pit = Group(group, "Inspection pit " + (i + 1));
            Block(pit, "Pit bottom", Finish.Roof, new Vector3(x, -PitDepth - .065f, PitZ),
                new Vector3(PitWidth + .18f, .13f, PitLength + .18f));
            foreach (float side in new[] {-1f, 1f})
            {
                float edge = x + side * PitWidth / 2;
                Block(pit, "Retaining side", Finish.Concrete, new Vector3(edge + side * .055f, -PitDepth / 2, PitZ),
                    new Vector3(.11f, PitDepth, PitLength));
                Surface(pit, Finish.Steel).Ground(edge + side * .046f, PitZ, .092f, PitLength + .18f, .009f, White);
                Box(pit, Finish.Lamp, edge - side * .016f, -.43f, PitZ, .031f, .082f, 2.25f);
            }
            foreach (float z in new[] {opening.yMin, opening.yMax})
            {
                float outward = Mathf.Sign(z - PitZ);
                Block(pit, "Retaining end", Finish.Concrete, new Vector3(x, -PitDepth / 2, z + outward * .055f),
                    new Vector3(PitWidth, PitDepth, .11f));
                Surface(pit, Finish.Steel).Ground(x, z + outward * .045f, PitWidth, .090f, .009f, White);
                for (int mark = 0; mark < 4; mark++)
                    Surface(pit, Finish.Yellow).Ground(x - .42f + mark * .28f, z + outward * .15f, .16f, .12f, .007f, White);
            }
            float ladderZ = opening.yMin + .16f;
            foreach (float side in new[] {-1f, 1f})
                Box(pit, Finish.Steel, x + side * .21f, -.72f, ladderZ, .045f, 1.31f, .052f);
            for (int rung = 0; rung < 5; rung++)
                Box(pit, Finish.Yellow, x, -.19f - .25f * rung, ladderZ, .40f, .041f, .062f);

            var grate = Surface(pit, Finish.Steel);
            for (int bar = 0; bar < 16; bar++)
            {
                float z = opening.yMin + .16f + .405f * bar;
                Vector3 At(float px, float py, float pz) => new(x + px, py, z + pz);
                float w = PitWidth / 2, d = .034f;
                var a = At(-w, 0, d); var b = At(w, 0, d);
                var c = At(w, 0, -d); var e = At(-w, 0, -d);
                grate.Quad(a, b, c, e, White);
                var down = Vector3.down * .07f;
                grate.Quad(a + down, b + down, b, a, White);
                grate.Quad(c + down, e + down, e, c, White);
            }
            // WheelCollider samples are rays; a single support surface models the load-bearing grate
            // without tyres sinking between the visible bars during turns.
            Solid(support, "Pit grating " + (i + 1), new Vector3(x, -.035f, PitZ),
                new Vector3(PitWidth, .07f, PitLength));
        }
    }
}
