using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.StoreOperations
{
    /// <summary>Two matching low-poly lock icons; geometry remains crisp at any canvas scale.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PadlockGraphic : MaskableGraphic
    {
        private bool _isOpen;
        public bool IsOpen
        {
            get => _isOpen;
            set { if (_isOpen == value) return; _isOpen = value; SetVerticesDirty(); }
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Vector2[] shackle = _isOpen
                ? new[] { new Vector2(.38f, .5f), new Vector2(.38f, .77f), new Vector2(.50f, .9f), new Vector2(.72f, .9f), new Vector2(.84f, .77f), new Vector2(.84f, .66f) }
                : new[] { new Vector2(.23f, .5f), new Vector2(.23f, .77f), new Vector2(.35f, .9f), new Vector2(.65f, .9f), new Vector2(.77f, .77f), new Vector2(.77f, .5f) };
            for (int i = 1; i < shackle.Length; i++) Segment(mesh, shackle[i - 1], shackle[i], .13f, Color.white);
            for (int i = 1; i < shackle.Length; i++) Segment(mesh, shackle[i - 1], shackle[i], .06f, new Color(.25f, .29f, .25f));
            Box(mesh, .12f, .08f, .88f, .57f, Color.white);
            Box(mesh, .18f, .14f, .82f, .51f, new Color(.36f, .4f, .32f));
            Box(mesh, .43f, .29f, .57f, .42f, new Color(.05f, .08f, .05f));
            Box(mesh, .47f, .21f, .53f, .34f, new Color(.05f, .08f, .05f));
        }
        private Vector2 Point(Vector2 p) => rectTransform.rect.min + Vector2.Scale(p, rectTransform.rect.size);
        private void Box(VertexHelper mesh, float left, float bottom, float right, float top, Color tint)
        {
            Quad(mesh, Point(new Vector2(left, bottom)), Point(new Vector2(left, top)), Point(new Vector2(right, top)), Point(new Vector2(right, bottom)), tint);
        }
        private void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            Quad(mesh, Point(a - normal), Point(a + normal), Point(b + normal), Point(b - normal), tint);
        }
        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero); mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
