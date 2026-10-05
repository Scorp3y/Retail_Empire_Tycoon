using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RetailEmpireTycoon.StoreOperations
{
    /// <summary>Uses UI drag events so screen scaling and fast mouse gestures do not lose the initial press.</summary>
    public sealed class WorkProductDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private RectTransform _rect;
        private Vector2 _home;
        private Action<Vector2> _drop;
        private Action _pick;
#if UNITY_EDITOR
        public string LastEvent { get; private set; } = "No drag events";
#endif
        public void Initialize(Action<Vector2> drop, Action pick)
        {
            _rect = (RectTransform)transform;
            _home = _rect.anchoredPosition;
            _drop = drop;
            _pick = pick;
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) _pick?.Invoke();
        }
        public void OnBeginDrag(PointerEventData data)
        {
#if UNITY_EDITOR
            LastEvent = "begin " + data.position;
#endif
            if (data.button == PointerEventData.InputButton.Left) _rect.position = data.position;
        }
        public void OnDrag(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) _rect.position = data.position;
        }
        public void OnEndDrag(PointerEventData data)
        {
#if UNITY_EDITOR
            LastEvent += " -> end " + data.position;
#endif
            _rect.anchoredPosition = _home;
            if (data.button == PointerEventData.InputButton.Left) _drop?.Invoke(data.position);
        }
    }
}
