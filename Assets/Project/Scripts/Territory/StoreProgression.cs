using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using RetailEmpireTycoon.Territory;

public sealed class StoreProgression : MonoBehaviour
{
    public static StoreProgression Instance { get; private set; }

    public ProgressState State { get; private set; } = new();
    public event Action OnChanged;
    [SerializeField] private TerritorySubdivisionRules landRules;
    public TerritorySubdivisionRules LandRules=>landRules;
    // Sub-parcels and compatibility aliases must not both increase shopper/staff difficulty.
    public int CompletedRegionCount => State.Purchased.Count(id => (int)id >= 0 && (int)id < 9);

    public IReadOnlyCollection<TerritoryId> Purchased => State.Purchased;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }


    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsTerritoryAvailable(TerritoryId id)
    {
        if(landRules!=null)return landRules.IsAvailable(id,State.Purchased);
        if (State.IsPurchased(id)) return false;

        bool purpleBought = State.IsPurchased(TerritoryId.Purple);
        bool redBought = State.IsPurchased(TerritoryId.Red);
        bool greenBought = State.IsPurchased(TerritoryId.Green);
        bool yellowBought = State.IsPurchased(TerritoryId.Yellow);
        bool pinkBought = State.IsPurchased(TerritoryId.Pink);

        if (!purpleBought)
            return id == TerritoryId.Purple;

        bool phase2 = purpleBought && !(redBought && greenBought) && !(yellowBought || pinkBought);
        if (phase2)
            return id == TerritoryId.Red || id == TerritoryId.Green;

        bool phase3 = redBought && greenBought && !(yellowBought && pinkBought);
        if (phase3)
            return id == TerritoryId.Yellow || id == TerritoryId.Pink;

        if (purpleBought && redBought && greenBought && yellowBought && pinkBought)
        {
            switch (id)
            {
                case TerritoryId.EastSouth:
                case TerritoryId.NorthWest: return true;
                case TerritoryId.EastNorth: return State.IsPurchased(TerritoryId.EastSouth);
                case TerritoryId.NorthEast: return State.IsPurchased(TerritoryId.NorthWest);
            }
        }
        return false;
    }

    public StoreLevelId PredictLevelAfterPurchase(TerritoryId buyId)
    {
        var predicted=new HashSet<TerritoryId>(State.Purchased){buyId};
        if(landRules!=null)landRules.AddCompletedRegions(predicted);
        bool purple = predicted.Contains(TerritoryId.Purple);
        bool red = predicted.Contains(TerritoryId.Red);
        bool green = predicted.Contains(TerritoryId.Green);
        bool yellow = predicted.Contains(TerritoryId.Yellow);
        bool pink = predicted.Contains(TerritoryId.Pink);

        if (!purple) return StoreLevelId.Lvl1;
        if (purple && !(red || green)) return StoreLevelId.Lvl2;

        if (purple && (red ^ green))
            return red ? StoreLevelId.Lvl3_1 : StoreLevelId.Lvl3_2;

        if (purple && red && green && !(yellow || pink))
            return StoreLevelId.Lvl4;

        if (purple && red && green && (yellow ^ pink))
            return pink ? StoreLevelId.Lvl5_1 : StoreLevelId.Lvl5_2;

        if (purple && red && green && yellow && pink)
            return StoreLevelId.Lvl6;

        return State.CurrentLevel;
    }

    public void MarkPurchased(TerritoryId id)
    {
        if (State.IsPurchased(id)) return;
        if (landRules != null && !landRules.IsAvailable(id, State.Purchased)) return;

        State.Purchased.Add(id);
        if(landRules!=null)landRules.AddCompletedRegions(State.Purchased);
        State.CurrentLevel = PredictLevelAfterPurchase(id);
        OnChanged?.Invoke();
    }

    public TerritorySaveData BuildSaveData()
    {
        var d = new TerritorySaveData{layoutVersion=landRules!=null?TerritorySubdivisionRules.CurrentVersion:0};
        foreach (var id in State.Purchased.OrderBy(id => (int)id))
            if(landRules==null||landRules.Contains(id))d.purchased.Add(id.ToString());
        d.storeLevel = State.CurrentLevel.ToString();
        return d;
    }

    public void ApplySaveData(TerritorySaveData d)
    {
        State.Purchased.Clear();

        if(landRules!=null)State.Purchased.UnionWith(landRules.Restore(d));

        if (landRules==null&&d?.purchased != null)
        {
            foreach (var s in d.purchased)
                if (Enum.TryParse<TerritoryId>(s, out var id) && Enum.IsDefined(typeof(TerritoryId), id))
                    State.Purchased.Add(id);
        }

        if (!string.IsNullOrEmpty(d?.storeLevel) &&
            Enum.TryParse<StoreLevelId>(d.storeLevel, out var lvl))
            State.CurrentLevel = lvl;
        else
            State.CurrentLevel = StoreLevelId.Lvl1;

        OnChanged?.Invoke();
    }
}
