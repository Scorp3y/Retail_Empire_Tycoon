using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a vertical "laser fence" mesh from a LineRenderer's points.
/// Intended to replace LineRenderer visuals for territory borders.
/// </summary>
[ExecuteAlways]
public sealed class TerritoryLaserFenceMesh : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private LineRenderer _sourceLine;
    [Tooltip("If true, uses the LineRenderer positions as a closed loop (last->first).")]
    [SerializeField] private bool _closedLoop = true;

    [Header("Fence")]
    [SerializeField] private float _height = 1.5f;
    [Tooltip("Optional: small thickness by duplicating faces. 0 = single ribbon.")]
    [SerializeField] private float _thickness = 0.0f;
    [Tooltip("UV tiling scale along the perimeter. Bigger = more repetitions.")]
    [SerializeField] private float _uvTiling = 1.0f;

    [Header("Rendering")]
    [Tooltip("If enabled, the generated mesh is rendered on a child object so we don't override existing MeshFilter/MeshRenderer on this GameObject.")]
    [SerializeField] private bool _renderOnChild = true;

    [SerializeField] private Transform _meshHolder;
    [SerializeField] private MeshRenderer _meshRenderer;
    [SerializeField] private MeshFilter _meshFilter;
    [SerializeField] private Material _material;

    [Header("Hover")]
    [SerializeField] private float _intensityMultiplierNormal = 1.0f;
    [SerializeField] private float _intensityMultiplierHover = 1.25f;

    private Mesh _mesh;
    private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
    private float _baseIntensity = -1f;

    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private void Reset()
    {
        _sourceLine = GetComponent<LineRenderer>();
        EnsureMeshObjects();
    }

    private void OnEnable()
    {
        EnsureMeshObjects();
        Rebuild();
        ApplyMaterial();
        CacheBaseIntensity();
    }

    private void OnDisable()
    {
        if (_meshFilter != null) _meshFilter.sharedMesh = null;
    }

    private void OnValidate()
    {
        _height = Mathf.Max(0.01f, _height);
        _uvTiling = Mathf.Max(0.01f, _uvTiling);
        _thickness = Mathf.Max(0.0f, _thickness);
        EnsureMeshObjects();
        Rebuild();
        ApplyMaterial();
        CacheBaseIntensity();
    }

    public void SetVisible(bool visible)
    {
        if (_meshRenderer != null)
            _meshRenderer.enabled = visible;
    }

    public void SetHover(bool on)
    {
        if (_meshRenderer == null) return;

        CacheBaseIntensity();

        _meshRenderer.GetPropertyBlock(_mpb);
        // If material has _Intensity, we multiply it via MPB.
        float mul = on ? _intensityMultiplierHover : _intensityMultiplierNormal;
        float intensity = _baseIntensity > 0f ? _baseIntensity * mul : mul;
        _mpb.SetFloat(IntensityId, intensity);
        _meshRenderer.SetPropertyBlock(_mpb);
    }

    private void CacheBaseIntensity()
    {
        if (_baseIntensity > 0f) return;
        var mat = _material != null ? _material : (_meshRenderer != null ? _meshRenderer.sharedMaterial : null);
        if (mat == null) return;
        if (mat.HasProperty(IntensityId))
            _baseIntensity = mat.GetFloat(IntensityId);
    }

    public void Rebuild()
    {
        EnsureMeshObjects();
        if (_sourceLine == null) _sourceLine = GetComponent<LineRenderer>();
        if (_sourceLine == null) return;

        int count = _sourceLine.positionCount;
        if (count < 2) return;

        var pts = new Vector3[count];
        _sourceLine.GetPositions(pts);

        // Convert to this transform local space.
        for (int i = 0; i < pts.Length; i++)
        {
            if (_sourceLine.useWorldSpace)
                pts[i] = transform.InverseTransformPoint(pts[i]);
        }

        BuildRibbonMesh(pts, _closedLoop);

        // If thickness requested, build a second ribbon offset slightly and stitch (simple approach: duplicate faces with flipped normals).
        // For ShaderGraph with Render Face = Both, single ribbon is usually enough.
    }

    private void EnsureMeshObjects()
    {
        if (_renderOnChild)
        {
            if (_meshHolder == null)
            {
                var existing = transform.Find("LaserFenceMesh");
                if (existing != null) _meshHolder = existing;
                else
                {
                    var go = new GameObject("LaserFenceMesh");
                    go.transform.SetParent(transform, false);
                    _meshHolder = go.transform;
                }
            }
        }
        else
        {
            _meshHolder = transform;
        }

        if (_meshFilter == null) _meshFilter = _meshHolder.GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = _meshHolder.GetComponent<MeshRenderer>();

        if (_meshFilter == null) _meshFilter = _meshHolder.gameObject.AddComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = _meshHolder.gameObject.AddComponent<MeshRenderer>();

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "TerritoryLaserFence" };
            _mesh.MarkDynamic();
        }
        _meshFilter.sharedMesh = _mesh;
    }

    private void ApplyMaterial()
    {
        if (_meshRenderer == null) return;
        if (_material != null) _meshRenderer.sharedMaterial = _material;
        _baseIntensity = -1f; // recache
    }

    private void BuildRibbonMesh(IReadOnlyList<Vector3> pts, bool closed)
    {
        int n = pts.Count;
        int segCount = closed ? n : n - 1;
        if (segCount <= 0) return;

        // We build each segment as its own quad (4 verts). This avoids messy joins.
        int vertCount = segCount * 4;
        int triCount = segCount * 2;

        var vertices = new Vector3[vertCount];
        var normals = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];
        var tris = new int[triCount * 3];

        // Compute cumulative distance for UV.x.
        float total = 0f;
        var cum = new float[segCount + 1];
        cum[0] = 0f;
        for (int s = 0; s < segCount; s++)
        {
            int i0 = s;
            int i1 = (s + 1) % n;
            if (!closed) i1 = s + 1;
            float d = Vector3.Distance(pts[i0], pts[i1]);
            total += d;
            cum[s + 1] = total;
        }
        float invTotal = total > 0.0001f ? 1f / total : 1f;

        for (int s = 0; s < segCount; s++)
        {
            int i0 = s;
            int i1 = (s + 1) % n;
            if (!closed) i1 = s + 1;

            Vector3 p0 = pts[i0];
            Vector3 p1 = pts[i1];

            Vector3 p0t = p0 + Vector3.up * _height;
            Vector3 p1t = p1 + Vector3.up * _height;

            // Segment normal (rough): perpendicular on XZ plane.
            Vector3 dir = (p1 - p0);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.000001f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 nrm = Vector3.Cross(Vector3.up, dir).normalized; // right-hand normal

            int v = s * 4;
            vertices[v + 0] = p0;
            vertices[v + 1] = p1;
            vertices[v + 2] = p0t;
            vertices[v + 3] = p1t;

            normals[v + 0] = nrm;
            normals[v + 1] = nrm;
            normals[v + 2] = nrm;
            normals[v + 3] = nrm;

            float u0 = (cum[s] * invTotal) * _uvTiling;
            float u1 = (cum[s + 1] * invTotal) * _uvTiling;

            uvs[v + 0] = new Vector2(u0, 0f);
            uvs[v + 1] = new Vector2(u1, 0f);
            uvs[v + 2] = new Vector2(u0, 1f);
            uvs[v + 3] = new Vector2(u1, 1f);

            int t = s * 6;
            // Tri 1: 0,2,1
            tris[t + 0] = v + 0;
            tris[t + 1] = v + 2;
            tris[t + 2] = v + 1;
            // Tri 2: 2,3,1
            tris[t + 3] = v + 2;
            tris[t + 4] = v + 3;
            tris[t + 5] = v + 1;
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.normals = normals;
        _mesh.uv = uvs;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
    }
}
