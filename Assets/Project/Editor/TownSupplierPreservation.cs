using System.Collections.Generic;
using System.Linq;
using RetailEmpireTycoon.City;
using RetailEmpireTycoon.Logistics;
using UnityEditor;
using UnityEngine;

internal static class TownSupplierPreservation
{
    public static Dictionary<SupplierKind,string> Capture()
    {
        var root=PrefabUtility.LoadPrefabContents(TownWorldAssembler.PrefabPath);
        var areas=new Dictionary<SupplierKind,Rect>();
        try
        {
            foreach(var court in root.GetComponentsInChildren<SupplierLoadingCourt>())
            {
                if(court.checkpoint.supplier==SupplierKind.Farmers)continue;
                // Include the public frontage, whole plot and the driveway up to the unchanged street.
                areas.Add(court.checkpoint.supplier,new Rect(-282,court.bounds.yMin-16,70,62));
            }
            var showroom=root.GetComponent<TownWorldPlaces>().suppliers.Single(c=>c.supplier==SupplierKind.Showroom);
            areas.Add(showroom.supplier,new Rect(-282,118,70,64));
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
        return areas.ToDictionary(a=>a.Key,a=>TownFarmPreservation.Capture(a.Value,a.Key));
    }
}
