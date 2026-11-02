using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RetailEmpireTycoon.City
{
    /// <summary>Map-only pointer input; selection, pan and zoom never leak into the driving camera.</summary>
    public sealed class TownMapInput : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        private CityMapGraphic map;
        private TownNavigation navigation;
        private Action changed;
        private bool dragged;
        public void Initialize(CityMapGraphic map, TownNavigation navigation, Action changed)
        { this.map = map; this.navigation = navigation; this.changed = changed; map.raycastTarget = true; }
        private Vector2 Local(PointerEventData data)
        { RectTransformUtility.ScreenPointToLocalPointInRectangle(map.rectTransform, data.position, data.pressEventCamera, out var p); return p; }
        public void OnBeginDrag(PointerEventData data) { dragged = true; }
        public void OnPointerDown(PointerEventData data) { dragged = false; }
        public void OnDrag(PointerEventData data)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(map.rectTransform, data.position - data.delta, data.pressEventCamera, out var before);
            map.Projection.Pan(Local(data) - before, map.rectTransform.rect); map.SetVerticesDirty(); changed?.Invoke();
        }
        public void OnScroll(PointerEventData data)
        { map.Projection.ZoomAt(Mathf.Pow(1.2f, data.scrollDelta.y), Local(data), map.rectTransform.rect); map.SetVerticesDirty(); changed?.Invoke(); }
        public void OnPointerClick(PointerEventData data)
        {
            if (dragged) { dragged = false; return; }
            if (data.button == PointerEventData.InputButton.Right) { navigation.Clear(); changed?.Invoke(); return; }
            if (data.button != PointerEventData.InputButton.Left) return;
            var local = Local(data); TownMapPoint nearest = null; float distance = 22 * 22;
            foreach (var point in map.Data.Points)
            { if (!map.Visible(point)) continue; float d = (map.Project(point.Position) - local).sqrMagnitude; if (d >= distance) continue; nearest = point; distance = d; }
            if (nearest != null) navigation.Select(nearest); else navigation.SetWaypoint(map.Projection.Unproject(local, map.rectTransform.rect));
            changed?.Invoke();
        }
    }
}
