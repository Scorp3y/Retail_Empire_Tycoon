using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.City
{
    /// <summary>Crisp map symbols, built from the same flat shapes as the store interface.</summary>
    internal static class TownMapGlyph
    {
        internal static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color color)
        { int i = mesh.currentVertCount; mesh.AddVert(a, color, Vector2.zero); mesh.AddVert(b, color, Vector2.zero); mesh.AddVert(c, color, Vector2.zero); mesh.AddTriangle(i, i + 1, i + 2); }
        internal static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            if ((b - a).sqrMagnitude < .000001f) return;
            var side = new Vector2(-(b - a).y, (b - a).x).normalized * width / 2; int i = mesh.currentVertCount;
            foreach (var p in new[] { a - side, a + side, b + side, b - side }) mesh.AddVert(p, color, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
        internal static void Circle(VertexHelper mesh, Vector2 center, float radius, Color color, int count = 12)
        {
            // Shared fan vertices keep the full town comfortably inside Unity UI's 65k limit.
            int start = mesh.currentVertCount; mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < count; i++)
            { float a = i * Mathf.PI * 2 / count; mesh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, Vector2.zero); }
            for (int i = 0; i < count; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % count);
        }
        internal static void Draw(VertexHelper mesh, TownMapSymbol symbol, Vector2 center, float radius, Color color)
        {
            Vector2 P(float x, float y) => center + new Vector2(x, y) * radius;
            void L(float ax, float ay, float bx, float by, float w = .22f) => Line(mesh, P(ax, ay), P(bx, by), radius * w, color);
            void C(float x, float y, float r) => Circle(mesh, P(x, y), radius * r, color);
            void T(float ax, float ay, float bx, float by, float cx, float cy) => Triangle(mesh, P(ax, ay), P(bx, by), P(cx, cy), color);
            switch (symbol)
            {
                case TownMapSymbol.Home: L(-.6f, -.65f, -.6f, .1f); L(.6f, -.65f, .6f, .1f); L(-.6f, -.65f, .6f, -.65f); L(-.8f, .05f, 0, .8f); L(0, .8f, .8f, .05f); L(0, -.65f, 0, -.05f); break;
                case TownMapSymbol.Cart: L(-.8f, .6f, -.5f, .6f); L(-.5f, .6f, -.3f, -.35f); L(-.3f, -.35f, .65f, -.35f); L(-.4f, .35f, .75f, .35f); L(.75f, .35f, .55f, -.1f); L(-.3f, -.1f, .55f, -.1f); C(-.2f, -.7f, .16f); C(.5f, -.7f, .16f); break;
                case TownMapSymbol.Leaf: T(-.7f, -.4f, -.4f, .6f, .7f, .65f); T(-.7f, -.4f, .7f, .65f, .4f, -.45f); L(-.65f, -.7f, .5f, .45f, .13f); break;
                case TownMapSymbol.Bread: L(-.65f, -.35f, .65f, -.35f, .6f); C(-.45f, .05f, .35f); C(0, .13f, .4f); C(.45f, .05f, .35f); break;
                case TownMapSymbol.Shelf: L(-.65f, -.75f, -.65f, .75f); L(.65f, -.75f, .65f, .75f); for (int i = -1; i <= 1; i++) L(-.6f, i * .5f, .6f, i * .5f); break;
                case TownMapSymbol.Hammer: L(-.5f, -.7f, .35f, .35f, .28f); L(-.12f, .7f, .75f, .05f, .5f); break;
                case TownMapSymbol.Flower: for (int i = 0; i < 5; i++) { float a = i * Mathf.PI * 2 / 5; C(Mathf.Cos(a) * .45f, Mathf.Sin(a) * .45f, .28f); } C(0, 0, .25f); break;
                case TownMapSymbol.Car: L(-.65f, -.1f, .65f, -.1f, .6f); L(-.4f, .4f, .4f, .4f, .5f); C(-.45f, -.65f, .2f); C(.45f, -.65f, .2f); break;
                case TownMapSymbol.Unload: L(-.75f, .6f, -.75f, -.65f); L(-.75f, -.65f, .1f, -.65f); L(-.75f, .6f, .1f, .6f); L(.8f, 0, -.25f, 0); L(-.25f, 0, .1f, .35f); L(-.25f, 0, .1f, -.35f); break;
                case TownMapSymbol.Fuel: L(-.45f, -.65f, -.45f, .5f, .65f); L(-.8f, -.65f, 0, -.65f); L(0, .65f, .6f, .35f, .13f); L(.6f, .35f, .6f, -.45f, .13f); break;
                case TownMapSymbol.Train: L(-.45f, -.25f, -.45f, .45f, .3f); L(.45f, -.25f, .45f, .45f, .3f); L(-.55f, .6f, .55f, .6f); L(-.55f, -.25f, .55f, -.25f); L(-.2f, -.25f, -.65f, -.8f); L(.2f, -.25f, .65f, -.8f); break;
                case TownMapSymbol.Park: C(0, .25f, .55f); L(0, -.2f, 0, -.8f, .25f); break;
                default: C(0, .2f, .45f); T(-.4f, 0, .4f, 0, 0, -.85f); break;
            }
        }
    }
}
