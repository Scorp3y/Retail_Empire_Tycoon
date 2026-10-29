using RetailEmpireTycoon.BuildSystem;
using UnityEngine;

/// <summary>Installed walls stay visible during construction. Only a wall being moved uses its placement ghost.</summary>
[DisallowMultipleComponent]
public sealed class StoreWallVisibility : MonoBehaviour
{
    [SerializeField] private BuildController buildController;
    private StoreWallOccluder[] _walls = new StoreWallOccluder[0];
    private float _nextRefresh;

    private void LateUpdate()
    {
        if (Time.unscaledTime >= _nextRefresh)
        {
            _walls = FindObjectsOfType<StoreWallOccluder>();
            _nextRefresh = Time.unscaledTime + 0.25f;
        }
        var editing=buildController!=null?buildController.GetComponent<BuildEditingController>():null;
        foreach (var wall in _walls)
        {
            if (wall == null) continue;
            wall.SetHidden(editing!=null && editing.Selected!=null && wall.GetComponentInParent<PlacedObject>()==editing.Selected);
        }
    }

    private void OnDisable()
    {
        foreach (var wall in _walls) if (wall != null) wall.SetHidden(false);
    }
}
