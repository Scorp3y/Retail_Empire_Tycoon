#if UNITY_EDITOR
using UnityEngine;

/// <summary>Disposable collision observation for the isolated driving test; never saved into game scenes.</summary>
public sealed class TownPickupImpactProbe : MonoBehaviour
{
    public Collider Target;
    public bool Hit;
    public Quaternion ContactHeading;

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.collider!=Target||Hit)return;
        Hit=true;
        ContactHeading=transform.rotation;
    }
}
#endif
