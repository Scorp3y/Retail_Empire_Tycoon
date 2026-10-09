using System;
using System.Collections.Generic;
using RetailEmpireTycoon.BuildSystem;
using UnityEngine;

namespace RetailEmpireTycoon.Territory
{
    /// <summary>Purchased land is independent of starter-building prefabs and saved furniture poses.</summary>
    public sealed class TerritoryPlotLayout : MonoBehaviour
    {
        [Serializable] public sealed class Plot
        {
            public TerritoryId id;
            public Rect bounds;
        }
        public GridSystem grid;
        public TerritoryManager territory;
        public StoreProgression progression;
        public Rect initialBounds;
        public List<Plot> plots = new List<Plot>();
        [HideInInspector] public int layoutVersion;
        private Rect initialServiceYard;
        public void SetInitialServiceYard(Rect bounds) { initialServiceYard = bounds; Apply(); }

        private void OnEnable()
        {
            if (progression != null) progression.OnChanged += Apply;
            Apply();
        }
        private void OnDisable()
        {
            if (progression != null) progression.OnChanged -= Apply;
        }
        public void Apply()
        {
            if (grid == null || territory == null || progression == null) return;
            territory.ClearPurchased();
            Add(initialBounds);
            if (initialServiceYard.width > 0 && initialServiceYard.height > 0) Add(initialServiceYard);
            foreach (var plot in plots)
                if (progression.State.IsPurchased(plot.id)) Add(plot.bounds);
        }
        private void Add(Rect bounds)
        {
            var min = grid.WorldToCell(new Vector3(bounds.xMin + .001f, 0, bounds.yMin + .001f));
            var max = grid.WorldToCell(new Vector3(bounds.xMax - .001f, 0, bounds.yMax - .001f));
            territory.AddPurchasedRect(min, max);
        }
    }
}
