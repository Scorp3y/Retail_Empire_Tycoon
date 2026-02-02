using UnityEngine;

public sealed class TerritoryVisual : MonoBehaviour
{
    [Header("Fill (optional)")]
    [SerializeField] private Renderer _fillRenderer;

    [Header("Border")]
    [SerializeField] private LineRenderer _border;
    [Tooltip("Optional. Builds a mesh fence from the border points and renders it with a neon material.")]
    [SerializeField] private TerritoryLaserFenceMesh _fence;
    [SerializeField] private Transform _hoverRoot;

    [SerializeField] private float _hoverScale = 1.06f;
    [SerializeField] private float _hoverSpeed = 12f;

    private Vector3 _baseScale;
    private bool _hover;
    private bool _allowHover;
    private bool _visible = true;


    public enum TerritoryViewState { Locked, Available, Purchased }
    private TerritoryViewState _state = TerritoryViewState.Locked;

    private void Awake()
    {
        if (_hoverRoot == null) _hoverRoot = transform;
        if (_fence == null) _fence = GetComponentInChildren<TerritoryLaserFenceMesh>(true);
        _baseScale = _hoverRoot.localScale;
    }

    private void Update()
    {
        if (!_visible) return;

        Vector3 target = _baseScale * (_hover && _allowHover ? _hoverScale : 1f);
        _hoverRoot.localScale = Vector3.Lerp(_hoverRoot.localScale, target, Time.unscaledDeltaTime * _hoverSpeed);
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;

        if (!visible)
        {
            _hover = false;
            _allowHover = false;
            if (_hoverRoot != null) _hoverRoot.localScale = _baseScale;
        }

        SetState(_state);
    }

    public void SetState(TerritoryViewState state)
    {
        _state = state;

        _allowHover = _visible && state == TerritoryViewState.Available;

        // If we have a mesh fence, prefer it over the LineRenderer to avoid double outlines.
        bool wantBorder = _visible && state == TerritoryViewState.Available && _fence == null;

        if (_border != null)
            _border.enabled = wantBorder;

        if (_fence != null)
            _fence.SetVisible(_visible && state == TerritoryViewState.Available);

        if (_fillRenderer != null)
            _fillRenderer.enabled = _visible && state != TerritoryViewState.Purchased;
    }


    public void SetHover(bool on)
    {
        if (!_visible) return;
        if (!_allowHover) return;

        _hover = on;

        if (_border != null)
        {
            float w = on ? 0.16f : 0.12f;
            _border.startWidth = w;
            _border.endWidth = w;
        }

        if (_fence != null)
            _fence.SetHover(on);
    }
}
