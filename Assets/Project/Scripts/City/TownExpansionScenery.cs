using System;
using UnityEngine;

namespace RetailEmpireTycoon.City
{
    [Serializable]
    public sealed class TownPlotScenery
    {
        public TerritoryId territory;
        public GameObject model;
    }

    /// <summary>Unbought store plots have visual greenery, never permanent driving or construction obstacles.</summary>
    public sealed class TownExpansionScenery : MonoBehaviour
    {
        public StoreProgression progression;
        public TownPlotScenery[] plots=Array.Empty<TownPlotScenery>();
        private void OnEnable()
        {
            if(progression!=null)progression.OnChanged+=Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            if(progression!=null)progression.OnChanged-=Refresh;
        }
        public void Refresh() {if(progression!=null)ApplyState(progression.State);}
        public void ApplyState(ProgressState state)
        {
            if(state==null)throw new ArgumentNullException(nameof(state));
            foreach(var plot in plots)if(plot.model!=null)plot.model.SetActive(!state.IsPurchased(plot.territory));
        }
    }
}
