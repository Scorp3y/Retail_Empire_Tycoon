using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Turns the workshop lamps on or off. A future day/night controller can call SetLights.</summary>
    [DisallowMultipleComponent]
    public sealed class AutoWorksLighting : MonoBehaviour
    {
        [SerializeField] private bool lightsOn = true;
        [SerializeField] private Renderer[] emissiveSurfaces;
        [SerializeField] private Light[] localLights;
        private MaterialPropertyBlock properties;
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        private void Awake() => SetLights(lightsOn);

        public void SetLights(bool on)
        {
            lightsOn = on;
            properties ??= new MaterialPropertyBlock();
            foreach (var surface in emissiveSurfaces)
            {
                surface.GetPropertyBlock(properties);
                properties.SetColor(Emission, on ? surface.sharedMaterial.GetColor(Emission) : Color.black);
                surface.SetPropertyBlock(properties);
            }
            foreach (var lamp in localLights) lamp.enabled = on;
        }
    }
}
