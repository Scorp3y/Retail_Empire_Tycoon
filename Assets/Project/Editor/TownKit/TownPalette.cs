using UnityEngine;

namespace RetailEmpireTycoon.Editor.TownKit
{
    internal enum TownColor
    {
        Cream, Brick, Mint, RoofDark, Frame, Glass, White, Asphalt,
        Pavement, Wood, WoodLight, Leaf, LeafLight, LeafDark, Trunk, Grass,
        Yellow, Orange, Red, Blue, Box, BoxDark, Metal, Rubber,
        Flowers, Sand, Stone, Soil, GreenStripe, Light, Pink, Charcoal
    }

    internal static class TownPalette
    {
        public static readonly Color32[] Colors =
        {
            new Color32(227,218,190,255), new Color32(174,98,72,255), new Color32(112,167,143,255), new Color32(64,79,77,255),
            new Color32(48,69,65,255), new Color32(109,159,176,255), new Color32(240,235,216,255), new Color32(48,52,54,255),
            new Color32(175,175,158,255), new Color32(115,77,44,255), new Color32(180,135,76,255), new Color32(76,127,56,255),
            new Color32(120,162,74,255), new Color32(49,96,62,255), new Color32(109,78,54,255), new Color32(125,163,90,255),
            new Color32(234,190,48,255), new Color32(221,112,46,255), new Color32(183,57,42,255), new Color32(54,107,147,255),
            new Color32(192,151,95,255), new Color32(137,98,56,255), new Color32(111,124,120,255), new Color32(36,39,40,255),
            new Color32(221,151,77,255), new Color32(194,176,133,255), new Color32(136,141,137,255), new Color32(99,84,54,255),
            new Color32(44,113,88,255), new Color32(249,223,126,255), new Color32(188,105,151,255), new Color32(64,65,62,255)
        };

        public static Vector2 UV(TownColor color) => new Vector2(((int)color + .5f) / Colors.Length, .5f);
    }
}
