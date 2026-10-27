using System;
using UnityEngine;

namespace RetailEmpireTycoon.StoreOperations
{
    /// <summary>Fits the authored rest pose, independently of the inflated skinned-renderer culling bounds.</summary>
    public sealed class ShopperAppearance : MonoBehaviour
    {
        [SerializeField] private int number;
        [SerializeField] private Bounds restBounds;
        public int Number => number;
        public Bounds RestBounds => restBounds;

        public void Configure(int variant, Bounds bounds)
        {
            if (variant < 1 || variant > 50 || bounds.size.y < .01f)
                throw new ArgumentException("Shopper needs a numbered variant and valid rest-pose bounds.");
            number = variant;
            restBounds = bounds;
        }

        public void FitHeight(float height)
        {
            if (height <= 0 || float.IsNaN(height) || float.IsInfinity(height) || restBounds.size.y < .01f)
                throw new ArgumentOutOfRangeException(nameof(height));
            float factor = height / restBounds.size.y;
            transform.localScale = Vector3.one * factor;
            transform.localPosition = Vector3.down * restBounds.min.y * factor;
        }
    }
}
