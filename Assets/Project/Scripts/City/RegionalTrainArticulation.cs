using System;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Five folds span each pair of car-end sockets, including bends and gradients.</summary>
    public sealed class RegionalTrainArticulation : MonoBehaviour
    {
        [Serializable] public sealed class Link
        {
            public Transform from, to, root, tunnel;
            public Transform[] ribs;
        }
        public TownRailTraffic service;
        public Link[] links = Array.Empty<Link>();
        public void Refresh()
        {
            foreach (var link in links)
            {
                bool visible = service == null || service.IsInService;
                link.root.gameObject.SetActive(visible);
                if (!visible) continue;
                var delta = link.from.position - link.to.position;
                if (delta.sqrMagnitude < .00001f) continue;
                var rotation = Quaternion.LookRotation(delta, (link.from.up + link.to.up).normalized);
                float scale = Mathf.Max(.0001f, Mathf.Abs(link.root.lossyScale.z));
                link.tunnel.SetPositionAndRotation((link.from.position + link.to.position) * .5f, rotation);
                link.tunnel.localScale = new Vector3(1, 1, delta.magnitude / scale);
                for (int i = 0; i < link.ribs.Length; i++)
                    link.ribs[i].SetPositionAndRotation(Vector3.Lerp(link.to.position, link.from.position,
                        (i + .5f) / link.ribs.Length), rotation);
            }
        }
    }
}
