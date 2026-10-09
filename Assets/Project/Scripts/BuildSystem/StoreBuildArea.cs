using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.Territory
{
    public sealed class StoreBuildArea : MonoBehaviour
    {
        public List<BoxCollider> areaRects = new List<BoxCollider>();
        [HideInInspector] public int landscapeLayoutVersion;
    }
}
