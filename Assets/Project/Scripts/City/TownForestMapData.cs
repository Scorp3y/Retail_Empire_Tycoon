using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Read-only authored map footprint of the woodland district. Does not alter the older road graph.</summary>
    [DisallowMultipleComponent]
    public sealed class TownForestMapData : MonoBehaviour
    {
        [SerializeField] private Rect districtBounds;
        [SerializeField] private Rect[] roadAreas=Array.Empty<Rect>();
        private IReadOnlyList<Rect> view;
        public Rect DistrictBounds=>districtBounds;
        public IReadOnlyList<Rect> RoadAreas=>view??=Array.AsReadOnly(roadAreas);

        /// <summary>Initialises generated scenery. Rejects invalid or out-of-district roads before changing state.</summary>
        public void Configure(Rect bounds,IReadOnlyList<Rect> roads)
        {
            if(!Valid(bounds))throw new ArgumentException("Finite positive district bounds required.",nameof(bounds));
            if(roads==null||roads.Count==0)throw new ArgumentException("District roads required.",nameof(roads));
            var copy=new Rect[roads.Count];
            for(int i=0;i<roads.Count;i++)
            {
                var r=roads[i];if(!Valid(r)||r.xMin<bounds.xMin||r.xMax>bounds.xMax||r.yMin<bounds.yMin||r.yMax>bounds.yMax)
                    throw new ArgumentException("A road lies outside its district bounds.",nameof(roads));
                copy[i]=r;
            }
            districtBounds=bounds;roadAreas=copy;view=Array.AsReadOnly(copy);
        }
        private static bool Valid(Rect r)=>Finite(r.x)&&Finite(r.y)&&Finite(r.width)&&Finite(r.height)&&Finite(r.xMax)&&Finite(r.yMax)&&r.width>0&&r.height>0;
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
