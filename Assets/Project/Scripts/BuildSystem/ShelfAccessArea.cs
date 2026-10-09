using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.Core;
using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>Shared geometry for build validation and the buyer-side preview.</summary>
    public static class ShelfAccessArea
    {
        public static IEnumerable<Vector3Int> Cells(GridSystem grid,PlacementRequest request,int side=0)
        {
            var footprint=grid.GetFootprintCells(request.anchorCell,request.item.footprint,request.rotated,request.item.pivotOffset).ToArray();
            int facing=(request.facing+request.item.frontFacing+side*2)%4;
            Vector3Int forward=facing==0?Vector3Int.forward:facing==1?Vector3Int.right:facing==2?Vector3Int.back:Vector3Int.left;
            int maximum=footprint.Max(c=>c.x*forward.x+c.z*forward.z);
            foreach(var edge in footprint.Where(c=>c.x*forward.x+c.z*forward.z==maximum))
                for(int depth=1;depth<=2;depth++) yield return edge+forward*depth;
        }
    }
}
