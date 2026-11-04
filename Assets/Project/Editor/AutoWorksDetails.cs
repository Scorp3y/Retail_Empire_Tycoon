using UnityEditor;
using UnityEngine;

internal sealed partial class AutoWorksModel
{
    private void BuildSign(Transform office)
    {
        var sign = Group(office, "AUTO WORKS sign");
        sign.localPosition = new Vector3(5, 3.65f, -3.83f);
        Box(sign, Finish.Steel, 0, 0, .05f, 6.6f, .74f, .16f, .047f);
        Box(sign, Finish.Olive, 0, 0, .135f, 6.43f, .63f, .035f, .013f);
        foreach (float side in new[] {-1f, 1f})
            for (int i = 0; i < 3; i++)
            {
                float x = (2.90f + .10f * i) * side;
                Beam(sign, Finish.Yellow, new(x - .055f, -.105f, .158f), new(x + .055f, .105f, .158f), .043f, .019f);
            }

        var word = Group(sign, "Editable letters");
        word.localPosition = new Vector3(0, -.013f, .164f);
        word.localRotation = Quaternion.Euler(0, 180, 0);
        var text = word.gameObject.AddComponent<TextMesh>();
        text.text = "AUTO WORKS";
        text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Plugins/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf");
        if (text.font == null) throw new System.InvalidOperationException("The workshop sign font is missing.");
        text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        text.characterSize = .077f;
        text.fontSize = 100;
        text.anchor = TextAnchor.MiddleCenter;
        text.color = new Color32(255, 236, 193, 255);
    }

    private void DressYard()
    {
        var reception = Group(props, "Reception forecourt details");
        Cylinder(reception, Finish.Olive, new Vector3(10.35f, .34f, -2.49f), .22f, .25f, .64f);
        Cylinder(reception, Finish.Steel, new Vector3(10.35f, .68f, -2.49f), .27f, .22f, .095f);
        Box(reception, Finish.Steel, 2.66f, .98f, -3.76f, .27f, .20f, .08f);
        Box(reception, Finish.Concrete, 2.66f, 1.01f, -3.708f, .19f, .038f, .019f);
        Box(reception, Finish.Steel, 5, .029f, -3.21f, 2.30f, .019f, .52f);
        for (int i = 0; i < 5; i++) Box(reception, Finish.Roof, 5, .042f, -3.40f + i * .09f, 2.25f, .009f, .022f);

        var canopyDetails = Group(canopy, "Parts delivery and service supplies");
        for (int i = 0; i < 3; i++) Box(canopyDetails, Finish.Timber, 38.10f, .076f, -8.16f + i * .28f, 1.24f, .12f, .15f);
        for (int i = 0; i < 3; i++) Box(canopyDetails, Finish.Timber, 37.63f + i * .47f, .148f, -7.88f, .13f, .048f, .80f);
        foreach (float x in new[] {37.82f, 38.35f})
        {
            Box(canopyDetails, Finish.Timber, x, .389f, -7.9f, .45f, .43f, .49f, .019f);
            Box(canopyDetails, Finish.Line, x, .606f, -7.9f, .056f, .005f, .49f);
        }
        Canister(canopyDetails, new Vector3(25.18f, .03f, -9.26f), Finish.Red);
        Canister(canopyDetails, new Vector3(25.61f, .03f, -9.32f), Finish.Olive);

        var fittings = Group(yard, "Yard service fittings");
        foreach (float x in new[] {36.45f, 33.35f, 30.25f})
            Box(fittings, Finish.Concrete, x, .036f, -.60f, 1.39f, .070f, .21f, .018f);
        DrainCover(fittings, 24.5f, .39f);
        DrainCover(fittings, -38.7f, 8.05f);
        foreach (float z in new[] {1.7f, 3.9f})
        {
            Cylinder(fittings, Finish.Olive, new Vector3(39.7f, .27f, z), .36f, .43f, .53f, 8);
            Shrub(fittings, 39.7f, z, .37f, .47f);
        }
    }

    private void DrainCover(Transform group, float x, float z)
    {
        Box(group, Finish.Steel, x, .0025f, z, .49f, .005f, .67f);
        for (int i = 0; i < 6; i++) Box(group, Finish.Roof, x, .006f, z - .255f + .102f * i, .415f, .003f, .042f);
    }

    private void DressWorkshopWalls()
    {
        var wall = Group(props, "Workshop fixtures and safety equipment");
        foreach (float x in new[] {-18.8f, -29.2f})
        {
            Box(wall, Finish.Steel, x, 1.20f, -14.65f, .23f, .57f, .07f);
            Cylinder(wall, Finish.Red, new Vector3(x, 1.14f, -14.48f), .105f, .105f, .43f);
            Cylinder(wall, Finish.Steel, new Vector3(x, 1.39f, -14.48f), .057f, .044f, .085f, 6);
            Box(wall, Finish.Line, x, 1.19f, -14.369f, .112f, .14f, .007f);
            Rod(wall, Finish.Steel, new(x,1.39f,-14.45f), new(x+.12f,1.27f,-14.40f), .018f, 6);
            Box(wall, Finish.Red, x, 1.68f, -14.65f, .23f, .19f, .045f);
            Box(wall, Finish.Line, x, 1.69f, -14.624f, .036f, .11f, .010f);
        }
        Beam(wall, Finish.Roof, new(-40.4f,2.45f,-14.62f), new(-7.6f,2.45f,-14.62f), .053f);
        foreach (float x in new[] {-34.7f, -24, -13.3f})
        {
            Beam(wall, Finish.Roof, new(x,2.45f,-14.62f), new(x,1.28f,-14.62f), .042f);
            Box(wall, Finish.Roof, x, 1.22f, -14.616f, .23f, .14f, .078f);
            Box(wall, Finish.Concrete, x, 1.22f, -14.569f, .15f, .095f, .016f);
        }
        PressureStation(wall, -21.6f);
        PressureStation(wall, -32.5f);

        var bench = props.Find("Workshop wall equipment/West side tools");
        Canister(bench, new Vector3(1.40f, 1.095f, -.18f), Finish.Olive);
        Box(bench, Finish.Steel, .48f, 1.132f, .15f, .31f, .066f, .18f);
        Box(bench, Finish.Line, .48f, 1.175f, .15f, .22f, .025f, .13f);
        Box(bench, Finish.Red, -.28f, 1.126f, .10f, .26f, .058f, .19f);

        var rear = props.Find("Workshop wall equipment/Rear wall storage");
        Canister(rear, new Vector3(-12.81f, .97f, .22f), Finish.Yellow);
        Canister(rear, new Vector3(12.86f, 1.64f, .22f), Finish.Red);
    }

    private void PressureStation(Transform group, float x)
    {
        Box(group, Finish.Roof, x, 1.63f, -14.60f, .43f, .54f, .15f, .018f);
        Rod(group, Finish.Line, new(x,1.77f,-14.53f), new(x,1.77f,-14.51f), .106f);
        Beam(group, Finish.Red, new(x,1.77f,-14.504f), new(x+.05f,1.822f,-14.504f), .019f, .013f);
        var hose = new Vector3(x, 1.44f, -14.49f);
        for (int i = 1; i <= 9; i++)
        {
            float angle = i * Mathf.PI * 2 / 9;
            var next = new Vector3(x + .16f * Mathf.Sin(angle), 1.23f + .21f * Mathf.Cos(angle), -14.49f);
            Beam(group, Finish.Steel, hose, next, .030f);
            hose = next;
        }
    }

    private void Canister(Transform group, Vector3 p, Finish color)
    {
        Box(group, color, p + Vector3.up * .155f, new Vector3(.22f, .31f, .14f), .023f);
        Box(group, Finish.Steel, p + new Vector3(-.062f, .325f, 0), new Vector3(.068f, .047f, .075f));
        Box(group, Finish.Line, p + new Vector3(0, .18f, .073f), new Vector3(.104f, .106f, .009f));
    }
}
