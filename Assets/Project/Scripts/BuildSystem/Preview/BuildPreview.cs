using UnityEngine;
using MyShopGame.Core;

namespace MyShopGame.BuildSystem
{
    [DisallowMultipleComponent]
    public sealed class BuildPreview : MonoBehaviour
    {
        private GameObject _instance;
        private Renderer[] _renderers = new Renderer[0];
        private Material _validMat;
        private Material _invalidMat;

        public void SetItem(BuildItemData item)
        {
            DestroyInstance();

            if (item == null || item.prefab == null)
                return;

            _validMat = item.previewValidMaterial;
            _invalidMat = item.previewInvalidMaterial;

            _instance = Instantiate(item.prefab, transform);
            _instance.name = "Preview_" + item.name;

            _renderers = _instance.GetComponentsInChildren<Renderer>(true);
            SetValid(false);
        }

        public void SetPose(Vector3 worldPos, Quaternion rot)
        {
            if (_instance == null)
                return;

            transform.position = worldPos;
            transform.rotation = rot;
        }

        public void SetValid(bool valid)
        {
            var mat = valid ? _validMat : _invalidMat;
            if (mat == null)
                return;

            foreach (var r in _renderers)
            {
                if (r == null)
                    continue;

                r.sharedMaterial = mat;
            }
        }

        public void Clear()
        {
            DestroyInstance();
        }

        private void DestroyInstance()
        {
            if (_instance == null)
                return;

            Destroy(_instance);
            _instance = null;
            _renderers = new Renderer[0];
        }
    }
}
