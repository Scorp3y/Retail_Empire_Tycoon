using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Aspect-preserving conversion between native town metres and map UI pixels.</summary>
    public sealed class TownMapProjection
    {
        public Rect Bounds { get; }
        public Vector2 Center { get; private set; }
        public float Zoom { get; private set; } = 1;
        public float Heading { get; set; }
        public float LocalDiameter { get; set; }
        public TownMapProjection(Rect bounds) { Bounds = bounds; Reset(); }
        public void Reset() { Center = Bounds.center; Zoom = 1; Heading = 0; }
        public void Follow(Vector3 nativePosition) => Center = new Vector2(nativePosition.x, nativePosition.z);
        public float PixelsPerMetre(Rect view) => LocalDiameter > 0
            ? Mathf.Min(view.width, view.height) / LocalDiameter
            : Mathf.Min(view.width / Bounds.width, view.height / Bounds.height) * Zoom;
        public Vector2 Project(Vector3 native, Rect view)
        {
            var delta = new Vector2(native.x, native.z) - Center;
            float a = Heading * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return view.center + new Vector2(c * delta.x - s * delta.y, s * delta.x + c * delta.y) * PixelsPerMetre(view);
        }
        public Vector3 Unproject(Vector2 pixels, Rect view)
        {
            var p = (pixels - view.center) / PixelsPerMetre(view);
            float a = Heading * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector3(Center.x + c * p.x + s * p.y, 0, Center.y - s * p.x + c * p.y);
        }
        public void Pan(Vector2 pixels, Rect view)
        {
            var before = Unproject(view.center, view); var after = Unproject(view.center - pixels, view);
            Center += new Vector2(after.x - before.x, after.z - before.z);
            ClampToView(view);
        }
        private void ClampToView(Rect view)
        {
            float halfX = view.width / PixelsPerMetre(view) / 2, halfY = view.height / PixelsPerMetre(view) / 2;
            Center = new Vector2(halfX * 2 >= Bounds.width ? Bounds.center.x : Mathf.Clamp(Center.x, Bounds.xMin + halfX, Bounds.xMax - halfX),
                halfY * 2 >= Bounds.height ? Bounds.center.y : Mathf.Clamp(Center.y, Bounds.yMin + halfY, Bounds.yMax - halfY));
        }
        public void ZoomAt(float factor, Vector2 pixels, Rect view)
        {
            var before = Unproject(pixels, view); Zoom = Mathf.Clamp(Zoom * factor, 1, 16);
            var after = Unproject(pixels, view); Center += new Vector2(before.x - after.x, before.z - after.z);
            if (Zoom <= 1.001f) Reset();
            ClampToView(view);
        }
    }
}
