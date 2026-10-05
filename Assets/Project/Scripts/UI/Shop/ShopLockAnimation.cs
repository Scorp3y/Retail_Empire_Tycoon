using Store = RetailEmpireTycoon.StoreOperations.StoreOperations;
using UnityEngine;

namespace RetailEmpireTycoon.UI.Shop
{
    /// <summary>Animates the shackle, including keyboard-triggered state changes, without delaying store logic.</summary>
    public sealed class ShopLockAnimation : MonoBehaviour
    {
        private Store _store;
        private ShopIconGraphic _graphic;
        private float _progress;
        public float Progress => _progress;

        public void Initialize(Store store, ShopIconGraphic graphic)
        {
            _store = store;
            _graphic = graphic;
            _progress = store.IsOpen ? 1 : 0;
            _graphic.LockProgress = _progress;
        }

        private void Update()
        {
            if (_store == null || _graphic == null) return;
            // Resuming the app can produce a long frame. Keep enough visible frames for the shackle movement.
            _progress = Mathf.MoveTowards(_progress, _store.IsOpen ? 1 : 0, Mathf.Min(Time.unscaledDeltaTime, .05f) / .28f);
            _graphic.LockProgress = Mathf.SmoothStep(0, 1, _progress);
        }
    }
}
