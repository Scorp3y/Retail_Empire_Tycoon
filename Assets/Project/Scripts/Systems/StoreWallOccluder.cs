using UnityEngine;

/// <summary>Store-wall marker: hiding rendering never changes physics or build occupancy.</summary>
[DisallowMultipleComponent]
public sealed class StoreWallOccluder : MonoBehaviour
{
    private Renderer[] _renderers;
    private bool[] _originalHidden;
    public Bounds WorldBounds
    {
        get
        {
            EnsureRenderers();
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;
                // Renderer.bounds may be stale after the shop scene is suspended for a city trip.
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter==null||filter.sharedMesh==null)continue;
                var local=filter.sharedMesh.bounds;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    var point=renderer.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z)));
                    if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
    }

    public void SetHidden(bool hidden)
    {
        EnsureRenderers();
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].forceRenderingOff = hidden || _originalHidden[i];
    }

    private void EnsureRenderers()
    {
        if (_renderers != null) return;
        _renderers = GetComponentsInChildren<Renderer>(true);
        _originalHidden = new bool[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _originalHidden[i] = _renderers[i].forceRenderingOff;
    }

    private void OnDisable() { SetHidden(false); }
}
