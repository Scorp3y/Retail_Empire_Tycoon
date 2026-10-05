using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.StoreOperations
{
    /// <summary>Temporarily hides legacy canvases and blocks their input, preserving their exact prior state.</summary>
    public sealed class LegacyUiVisibility
    {
        private readonly List<(Canvas Canvas, bool Enabled)> _canvases = new List<(Canvas, bool)>();
        private readonly List<(GraphicRaycaster Raycaster, bool Enabled)> _raycasters = new List<(GraphicRaycaster, bool)>();

        public void Hide(Canvas[] canvases, Canvas activityCanvas)
        {
            Restore();
            var legacy = new HashSet<Canvas>(canvases ?? new Canvas[0]);
            // Some existing global UI (language selector) is created only at runtime.
            foreach (var canvas in Object.FindObjectsOfType<Canvas>(true))
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) legacy.Add(canvas);
            foreach (var canvas in legacy)
            {
                if (canvas == null || canvas == activityCanvas || canvas.transform.IsChildOf(activityCanvas.transform)) continue;
                _canvases.Add((canvas, canvas.enabled));
                canvas.enabled = false;
                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null) continue;
                _raycasters.Add((raycaster, raycaster.enabled));
                raycaster.enabled = false;
            }
        }
        public void Restore()
        {
            foreach (var state in _canvases) if (state.Canvas != null) state.Canvas.enabled = state.Enabled;
            foreach (var state in _raycasters) if (state.Raycaster != null) state.Raycaster.enabled = state.Enabled;
            _canvases.Clear();
            _raycasters.Clear();
        }
    }
}
