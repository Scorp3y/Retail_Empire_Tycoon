using UnityEngine;

[ExecuteAlways]
public sealed class TerritoryClickPlane : MonoBehaviour
{
    [SerializeField] private BoxCollider _collider;
    [SerializeField] private Vector2 _sizeXZ = new Vector2(6f, 6f);
    [SerializeField] private float _height = 1f;
    [SerializeField] private float _yOffset = 0.2f;

    private void Reset()
    {
        Ensure();
        Apply();
    }

    private void OnValidate()
    {
        Ensure();
        Apply();
    }

    private void Ensure()
    {
        if (_collider == null)
        {
            _collider = GetComponent<BoxCollider>();
            if (_collider == null) _collider = gameObject.AddComponent<BoxCollider>();
        }
    }

    private void Apply()
    {
        gameObject.layer = LayerMask.NameToLayer("Territory");

        _collider.isTrigger = false;
        _collider.center = new Vector3(0f, _yOffset, 0f);
        _collider.size = new Vector3(_sizeXZ.x, _height, _sizeXZ.y);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Ensure();
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Gizmos.DrawCube(_collider.center, _collider.size);
    }
#endif
}
