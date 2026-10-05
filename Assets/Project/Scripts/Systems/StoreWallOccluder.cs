using UnityEngine;

/// <summary>Store-wall marker: hiding rendering never changes physics or build occupancy.</summary>
[DisallowMultipleComponent]
public sealed class StoreWallOccluder : MonoBehaviour
{
    private Renderer[] _renderers;
    private bool[] _originalHidden;
    public Bounds WorldBounds
    {
        get
        {
            EnsureRenderers();
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }
    }

    public void SetHidden(bool hidden)
    {
        EnsureRenderers();
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].forceRenderingOff = hidden || _originalHidden[i];
    }

    private void EnsureRenderers()
    {
        if (_renderers != null) return;
        _renderers = GetComponentsInChildren<Renderer>(true);
        _originalHidden = new bool[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _originalHidden[i] = _renderers[i].forceRenderingOff;
    }

    private void OnDisable() { SetHidden(false); }
}
