using RetailEmpireTycoon.BuildSystem;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Cutaway view of camera-facing store walls while placing furniture.</summary>
[DisallowMultipleComponent]
public sealed class StoreWallVisibility : MonoBehaviour
{
    [SerializeField] private MainCamera cameraInput;
    [SerializeField] private BuildController buildController;
    [SerializeField, Range(1, 4)] private int maximumHiddenWalls = 4;
    private readonly List<StoreWallOccluder> _candidates = new List<StoreWallOccluder>();
    private StoreWallOccluder[] _walls = new StoreWallOccluder[0];
    private float _nextRefresh;

    private void LateUpdate()
    {
        if (Time.unscaledTime >= _nextRefresh)
        {
            _walls = FindObjectsOfType<StoreWallOccluder>();
            _nextRefresh = Time.unscaledTime + 0.25f;
        }
        bool building = buildController != null && buildController.mode == BuildMode.Build;
        Vector3 focus = cameraInput != null ? cameraInput.FocusPoint : transform.position + transform.forward * 10f;
        if (building && buildController.TryGetPlacementFocus(out var placementFocus)) focus = placementFocus;
        _candidates.Clear();
        foreach (var wall in _walls)
        {
            if (wall == null) continue;
            wall.SetHidden(false);
            if (building && ShouldHide(wall.WorldBounds, transform.position, focus)) _candidates.Add(wall);
        }
        // Prefer the wall sections crossing the line of sight to the placement cursor.
        Vector3 direction = (transform.position - focus).normalized;
        _candidates.Sort((a, b) => SightlineDistance(a.WorldBounds.center, focus, direction)
            .CompareTo(SightlineDistance(b.WorldBounds.center, focus, direction)));
        for (int i = 0; i < Mathf.Min(maximumHiddenWalls, _candidates.Count); i++) _candidates[i].SetHidden(true);
    }

    private static float SightlineDistance(Vector3 point, Vector3 origin, Vector3 direction)
    {
        Vector3 delta = point - origin;
        return Vector3.Cross(delta, direction).sqrMagnitude;
    }

    public static bool ShouldHide(Bounds wall, Vector3 cameraPosition, Vector3 focus)
    {
        Vector3 toCamera = cameraPosition - focus;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.01f) return false;
        Vector3 offset = wall.center - focus;
        offset.y = 0f;
        float depth = Vector3.Dot(offset, toCamera.normalized);
        if (depth <= 0.15f || depth >= toCamera.magnitude) return false;
        Vector3 normal = wall.size.x > wall.size.z ? Vector3.forward : Vector3.right;
        bool corner = Mathf.Abs(wall.size.x - wall.size.z) < 0.1f;
        return corner || Mathf.Abs(Vector3.Dot(normal, toCamera.normalized)) > 0.4f;
    }

    private void OnDisable()
    {
        foreach (var wall in _walls) if (wall != null) wall.SetHidden(false);
    }
}
