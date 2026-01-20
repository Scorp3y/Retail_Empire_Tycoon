using UnityEngine;

public sealed class TerritoryVisual : MonoBehaviour
{
    [Header("Fill (optional)")]
    [SerializeField] private Renderer _fillRenderer;
    [SerializeField] private Color _fillAvailable = new Color(0f, 0.35f, 0f, 0.25f);
    [SerializeField] private Color _fillLocked = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color _fillPurchased = new Color(0f, 0.6f, 0f, 0.18f);

    [Header("Border")]
    [SerializeField] private LineRenderer _border;
    [SerializeField] private Color _borderAvailable = new Color(0.1f, 1f, 0.2f, 1f);
    [SerializeField] private Color _borderLocked = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color _borderPurchased = new Color(0.2f, 1f, 0.2f, 1f);

    [Header("Hover")]
    [SerializeField] private Transform _hoverRoot;
    [SerializeField] private float _hoverScale = 1.06f;
    [SerializeField] private float _hoverSpeed = 12f;

    private Vector3 _baseScale;
    private bool _hover;
    private bool _allowHover;
    private TerritoryViewState _state;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public enum TerritoryViewState
    {
        Locked,
        Available,
        Purchased
    }

    private void Awake()
    {
        if (_hoverRoot == null) _hoverRoot = transform;
        _baseScale = _hoverRoot.localScale;
    }

    private void Update()
    {
        Vector3 target = _baseScale * (_hover && _allowHover ? _hoverScale : 1f);
        _hoverRoot.localScale = Vector3.Lerp(_hoverRoot.localScale, target, Time.unscaledDeltaTime * _hoverSpeed);
    }

    public void SetState(TerritoryViewState state)
    {
        _state = state;
        _allowHover = state == TerritoryViewState.Available;

        Color fill = state switch
        {
            TerritoryViewState.Available => _fillAvailable,
            TerritoryViewState.Purchased => _fillPurchased,
            _ => _fillLocked
        };

        Color border = state switch
        {
            TerritoryViewState.Available => _borderAvailable,
            TerritoryViewState.Purchased => _borderPurchased,
            _ => _borderLocked
        };

        if (_fillRenderer != null)
        {
            var mat = _fillRenderer.material;
            if (mat.HasProperty(BaseColor)) mat.SetColor(BaseColor, fill);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", fill);
        }

        if (_border != null)
        {
            _border.startColor = border;
            _border.endColor = border;
        }

        if (!_allowHover) _hover = false;
    }

    public void SetHover(bool on)
    {
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
