using UnityEngine;

public sealed class TerritoryVisual : MonoBehaviour
{
    [Header("Fill (optional)")]
    [SerializeField] private Renderer _fillRenderer;

    [Header("Border")]
    [SerializeField] private LineRenderer _border;
    [SerializeField] private Transform _hoverRoot;

    [SerializeField] private float _hoverScale = 1.06f;
    [SerializeField] private float _hoverSpeed = 12f;

    private Vector3 _baseScale;
    private bool _hover;
    private bool _allowHover;
    private bool _visible = true;


    public enum TerritoryViewState { Locked, Available, Purchased }

    private void Awake()
    {
        if (_hoverRoot == null) _hoverRoot = transform;
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

        if (_fillRenderer != null) _fillRenderer.enabled = visible;
        if (_border != null) _border.enabled = visible;

        if (!visible)
        {
            _hover = false;
            _allowHover = false;
            if (_hoverRoot != null) _hoverRoot.localScale = _baseScale;
        }
    }

    public void SetState(TerritoryViewState state)
    {
        _allowHover = state == TerritoryViewState.Available;

        if (!_visible)
        {
            _hover = false;
            _allowHover = false;
        }
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
    }
}
