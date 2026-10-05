using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Resolution-independent capsule for small slider rails and handles.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ShopCapsuleGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            mesh.AddVert(rect.center, color, Vector2.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                    corner < 2 ? rect.yMax - radius : rect.yMin + radius);
                for (int step = 0; step <= 8; step++)
                {
                    float angle = (corner * 90 + step * 90f / 8) * Mathf.Deg2Rad;
                    mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                }
            }
            for (int i = 1; i < mesh.currentVertCount; i++)
                mesh.AddTriangle(0, i, i == mesh.currentVertCount - 1 ? 1 : i + 1);
        }
    }
}
