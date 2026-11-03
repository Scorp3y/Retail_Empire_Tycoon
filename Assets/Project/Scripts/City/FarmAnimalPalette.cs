using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Applies the authored solid palette explicitly per surface. This also keeps
    /// cloned store-view renderers independent of URP's shared material constant-buffer cache.</summary>
    [ExecuteAlways]
    public sealed class FarmAnimalPalette : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private void OnEnable() => Apply();

        public void Apply()
        {
            var properties = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(BaseColor)) continue;
                renderer.GetPropertyBlock(properties);
                properties.SetColor(BaseColor, material.GetColor(BaseColor));
                renderer.SetPropertyBlock(properties);
                properties.Clear();
            }
        }
    }
}
