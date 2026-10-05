using UnityEngine;

/// <summary>Presentation of a purchasable plot; never changes prices or progression.</summary>
public sealed class TerritoryConstructionBarrier : MonoBehaviour
{
    [SerializeField] private GameObject _model;
    [SerializeField] private Renderer _fenceRenderer;
    [SerializeField] private TextMesh _priceLabel;
    private MaterialPropertyBlock _properties;

    public void SetPrice(int price)
    {
        if (_priceLabel != null)
            _priceLabel.text = "Расширить\n" + price.ToString("N0").Replace(',', ' ') + " $";
    }

    public void SetVisible(bool visible)
    {
        if (_model != null) _model.SetActive(visible);
        if (!visible) SetHover(false);
    }

    public void SetHover(bool hover)
    {
        if (_fenceRenderer == null) return;
        // Unity can recreate components without retaining non-serialized state.
        // Create the native property block on first use, on the main thread.
        if (_properties == null) _properties = new MaterialPropertyBlock();
        // A subtle material tint, not scaling the zone and its click collider.
        Material[] materials = _fenceRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null || !materials[i].HasProperty("_BaseColor")) continue;
            _properties.Clear();
            Color color = materials[i].GetColor("_BaseColor");
            _properties.SetColor("_BaseColor", hover ? color * 1.15f : color);
            _fenceRenderer.SetPropertyBlock(_properties, i);
        }
    }
}
