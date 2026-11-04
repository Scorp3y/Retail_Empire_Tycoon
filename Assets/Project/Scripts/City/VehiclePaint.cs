using UnityEngine;

namespace RetailEmpireTycoon.City
{
    public static class VehiclePaint
    {
        public const string StarterRenderer = "car SUVs pickups lights body";
        public const string StarterMesh = "VehicleTuning/StarterPaintBody";
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        internal static Color32 FactoryColor(GameObject root, PlayerVehicleDefinition vehicle)
        {
            if (vehicle != null)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        if (IsBody(material, vehicle)) return material.GetColor(BaseColor);
            }
            return new Color32(202, 80, 39, 255);
        }

        internal static void Apply(GameObject root, PlayerVehicleDefinition vehicle, Color32? color)
        {
            var block = new MaterialPropertyBlock();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (vehicle != null)
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (!IsBody(materials[i], vehicle)) continue;
                        renderer.GetPropertyBlock(block, i);
                        block.SetColor(BaseColor, color.HasValue ? (Color)color.Value : materials[i].GetColor(BaseColor));
                        renderer.SetPropertyBlock(block, i);
                    }
                }
                else if (renderer.name == StarterRenderer && renderer.TryGetComponent<MeshFilter>(out var filter))
                {
                    ApplyStarter(renderer, filter, color, block);
                }
            }
        }

        private static bool IsBody(Material material, PlayerVehicleDefinition vehicle) =>
            material != null && material.name == vehicle.id && material.HasProperty(BaseColor);

        private static void ApplyStarter(Renderer renderer, MeshFilter filter, Color32? color, MaterialPropertyBlock block)
        {
            if (filter.sharedMesh == null) return;
            if (filter.sharedMesh.subMeshCount == 1)
            {
                if (!color.HasValue) return;
                var paintMesh = Resources.Load<Mesh>(StarterMesh);
                if (paintMesh == null) return;
                var original = renderer.sharedMaterial;
                filter.sharedMesh = paintMesh;
                renderer.sharedMaterials = new[] {original, original};
            }
            if (filter.sharedMesh.name != "StarterPaintBody") return;
            var material = renderer.sharedMaterial;
            renderer.GetPropertyBlock(block, 1);
            block.SetTexture(BaseMap, color.HasValue ? Texture2D.whiteTexture : material.GetTexture(BaseMap));
            block.SetColor(BaseColor, color.HasValue ? (Color)color.Value : material.GetColor(BaseColor));
            renderer.SetPropertyBlock(block, 1);
        }
    }
}
