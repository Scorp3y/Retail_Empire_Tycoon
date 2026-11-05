using System.Collections.Generic;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Editor.TownKit;
using UnityEditor;
using UnityEngine;

internal sealed partial class AutoWorksModel
{
    private void BuildDoors()
    {
        var rails = Group(interior, "Sectional door rails");
        var hall = Solid(doors, "Common hall occupancy", new Vector3(-24, 2.42f, -8), new Vector3(34.36f, 4.84f, 13.36f), true);
        var basicPanel = new TownMeshBuilder();
        var glazedPanel = new TownMeshBuilder();
        var glazing = new TownMeshBuilder();
        var panelSeam = new TownMeshBuilder();
        const float glassWidth = .63f;
        const float windowHeight = .43f;
        const float panelWidth = Width + .04f;
        const float gap = .009f;
        float band = (GarageDoor.SectionHeight - gap - windowHeight) * .5f;
        BeveledBox(basicPanel, Vector3.zero, new Vector3(panelWidth, GarageDoor.SectionHeight - gap, .115f), .025f);
        foreach (float dy in new[] {-.245f, .225f})
            BeveledBox(basicPanel, new Vector3(0, dy, .058f), new Vector3(panelWidth - .05f, .033f, .027f), .007f);
        foreach (float dy in new[] {-1f, 1f})
        {
            float extension = dy > 0 ? .023f : 0;
            BeveledBox(glazedPanel, new Vector3(0, ((windowHeight + band) / 2 + extension / 2) * dy, 0),
                new Vector3(panelWidth, band + extension, .115f), .017f);
        }
        float left = -panelWidth * .5f;
        for (int i = 0; i < 6; i++)
        {
            float x = (i - 2.5f) * .79f;
            float edge = x - glassWidth / 2;
            BeveledBox(glazedPanel, new Vector3((left + edge) / 2, 0, 0), new Vector3(edge - left, windowHeight, .115f), .011f);
            glazing.Box(new Vector3(x, 0, .004f), new Vector3(glassWidth, windowHeight, .037f), White);
            left = x + glassWidth / 2;
        }
        BeveledBox(glazedPanel, new Vector3((left + panelWidth / 2) / 2, 0, 0), new Vector3(panelWidth / 2 - left, windowHeight, .115f), .011f);
        float half = panelWidth / 2, bottom = -GarageDoor.SectionHeight / 2 - .012f;
        var a = new Vector3(-half, bottom, .006f);
        var b = new Vector3(half, bottom, .006f);
        var c = b + Vector3.up * .042f;
        var d = a + Vector3.up * .042f;
        panelSeam.Quad(a, b, c, d, White);
        panelSeam.Quad(b, a, d, c, White);
        var seamMesh = SaveMesh(panelSeam, "Section_gasket_module");
        var panelMesh = SaveMesh(basicPanel, "Section_module");
        var topMesh = SaveMesh(glazedPanel, "Glazed_section_module");
        var glassMesh = SaveMesh(glazing, "Section_window_module");
        for (int bay = 0; bay < BayX.Length; bay++)
        {
            float x = BayX[bay];
            // Fill the setback between the masonry and the guide tracks, outside the clear opening.
            foreach (float side in new[] {-1f, 1f})
                Box(rails, Finish.Olive, x + side * (Width / 2 + .14f), Height / 2, -1.50f,
                    .28f, Height, .40f);
            Box(rails, Finish.Plaster, x, (Height + Eaves) / 2, -1.393f,
                Width, Eaves - Height, .155f);
            var door = Group(doors, "Door_" + (bay + 1), false);
            door.localPosition = new Vector3(x, 0, DoorPlane);
            var sensor = Solid(door, "8 m approach trigger", new Vector3(0, 2.34f, Facade - DoorPlane + 4f),
                new Vector3(Width, 4.68f, 8f), true);
            var throat = Solid(door, "Threshold and interior approach trigger", new Vector3(0, 2.34f, -1.03f),
                new Vector3(Width, 4.68f, 3.29f), true);
            var barrier = Solid(door, "Closed aperture blocker", new Vector3(0, Height * .5f, 0),
                new Vector3(Width, Height, .128f));
            var sections = new List<Transform>();
            for (int i = 0; i < GarageDoor.SectionCount; i++)
            {
                var section = Group(door, "Section_" + (i + 1), false);
                bool top = i == GarageDoor.SectionCount - 1;
                AttachMesh(section, "Olive leaf", top ? topMesh : panelMesh, Finish.Olive, false);
                if (top) AttachMesh(section, "Glazing", glassMesh, Finish.Glass, false);
                AttachMesh(section, "Panel seam", seamMesh, Finish.Steel, false);
                GarageDoor.SetSectionPose(section, i, 0);
                sections.Add(section);
            }

            var audio = door.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1;
            audio.maxDistance = 17;
            var behavior = door.gameObject.AddComponent<GarageDoor>();
            var settings = new SerializedObject(behavior);
            SetArray(settings.FindProperty("sections"), sections);
            settings.FindProperty("approach").objectReferenceValue = sensor;
            settings.FindProperty("throat").objectReferenceValue = throat;
            settings.FindProperty("hall").objectReferenceValue = hall;
            settings.FindProperty("closedBarrier").objectReferenceValue = barrier;
            settings.ApplyModifiedPropertiesWithoutUndo();

            foreach (float side in new[] {-1f, 1f})
            {
                float edge = x + side * (Width / 2 + .066f);
                Vector3 At(float distance) => GarageDoor.Track(distance) + new Vector3(edge, 0, DoorPlane);
                float bendEnd = GarageDoor.BendStart + GarageDoor.BendRadius * Mathf.PI * .5f;
                Beam(rails, Finish.Steel, At(.08f), At(GarageDoor.BendStart), .089f);
                for (int part = 0; part < 7; part++)
                    Beam(rails, Finish.Steel, At(Mathf.Lerp(GarageDoor.BendStart, bendEnd, part / 7f)),
                        At(Mathf.Lerp(GarageDoor.BendStart, bendEnd, (part + 1) / 7f)), .089f);
                Beam(rails, Finish.Steel, At(bendEnd), At(GarageDoor.OpenTravel + Height + .12f), .089f);
                foreach (float distance in new[] {bendEnd + .72f, bendEnd + 3.75f})
                    Beam(rails, Finish.Steel, At(distance), At(distance) + Vector3.up * .28f, .056f);
            }
        }
    }
}

