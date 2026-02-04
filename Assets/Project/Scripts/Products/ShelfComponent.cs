using UnityEngine;
using MyShopGame.Core;

namespace MyShopGame.Products
{
    [DisallowMultipleComponent]
    public sealed class ShelfComponent : MonoBehaviour
    {
        [Header("Data")]
        public ShelfData shelfData;

        [Header("Runtime")]
        [Min(1)]
        public int tierIndex = 1;

        [Range(0f, 1f)]
        public float fill01;

        public void SetFill(float fill01)
        {
            this.fill01 = Mathf.Clamp01(fill01);
            ApplyFillVisual();
        }

        public void SetTier(int tierIndex)
        {
            this.tierIndex = Mathf.Max(1, tierIndex);
            ApplyTierVisual();
        }

        private void Start()
        {
            ApplyTierVisual();
            ApplyFillVisual();
        }

        private void ApplyTierVisual()
        {
            if (shelfData == null)
                return;

            var tier = shelfData.GetTier(tierIndex);

            if (tier.tierVisualOverride != null)
            {
                // replace visual
            }

            if (tier.tierMaterialOverride != null)
            {
                // override material
            }
        }

        private void ApplyFillVisual()
        {
            var profile = shelfData != null ? shelfData.fillVisualProfile : null;
            if (profile == null || profile.stages == null)
                return;

            var stage = PickStage(profile, fill01);
            if (stage == null)
                return;

            Toggle(stage.enableObjects, true);
            Toggle(stage.disableObjects, false);
        }

        private static ShelfFillVisualProfile.ShelfFillStage PickStage(ShelfFillVisualProfile profile, float fill01)
        {
            ShelfFillVisualProfile.ShelfFillStage best = null;

            foreach (var s in profile.stages)
            {
                if (s == null)
                    continue;

                var ok = fill01 >= s.minFill01;
                if (!ok)
                    continue;

                if (best == null || s.minFill01 > best.minFill01)
                    best = s;
            }

            return best;
        }

        private static void Toggle(GameObject[] arr, bool on)
        {
            if (arr == null)
                return;

            foreach (var go in arr)
            {
                if (go != null)
                    go.SetActive(on);
            }
        }
    }
}
