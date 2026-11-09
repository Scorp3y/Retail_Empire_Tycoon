using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    public static class FloorPattern
    {
        public static int Facing(int facing) => (facing % 4 + 4) % 4;

        public static Vector3Int Edge(int facing)
        {
            switch (Facing(facing))
            {
                case 1: return Vector3Int.right;
                case 2: return new Vector3Int(0, 0, -1);
                case 3: return Vector3Int.left;
                default: return new Vector3Int(0, 0, 1);
            }
        }

        public static Vector2 Rotate(Vector2 uv, int facing)
        {
            switch (Facing(facing))
            {
                case 1: return new Vector2(-uv.y, uv.x);
                case 2: return -uv;
                case 3: return new Vector2(uv.y, -uv.x);
                default: return uv;
            }
        }

        public static Vector2 StripeUV(Vector2Int cell, Vector2 cellUV, int facing, bool showStripe)
        {
            // Each atlas half is a four-cell repeat of concrete, with padded samples for each cell.
            Vector2 center = Rotate((Vector2)cell + Vector2.one * .5f, facing);
            var column = Mathf.FloorToInt(center.x) & 3;
            var row = Mathf.FloorToInt(center.y) & 3;
            Vector2 local = Rotate(cellUV - Vector2.one * .5f, facing) + Vector2.one * .5f;
            local = Vector2.one / 32f + local * (15f / 16f);
            return new Vector2((local.x + column + (showStripe ? 4 : 0)) / 8, (local.y + row) / 4);
        }
    }
}
