using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class StoreProgression : MonoBehaviour
{
    public ProgressState State { get; private set; } = new();
    public event Action OnChanged;

    public IReadOnlyCollection<TerritoryId> Purchased => State.Purchased;

    public bool IsTerritoryAvailable(TerritoryId id)
    {
        if (State.IsPurchased(id)) return false;

        bool purpleBought = State.IsPurchased(TerritoryId.Purple);
        bool redBought = State.IsPurchased(TerritoryId.Red);
        bool greenBought = State.IsPurchased(TerritoryId.Green);

        if (!purpleBought)
            return id == TerritoryId.Purple;

        bool phase2 = purpleBought && !(redBought && greenBought);
        if (phase2)
            return id == TerritoryId.Red || id == TerritoryId.Green;

        bool phase3 = redBought && greenBought;
        if (phase3)
            return id == TerritoryId.Yellow || id == TerritoryId.Pink;

        return false;
    }

    public StoreLevelId PredictLevelAfterPurchase(TerritoryId buyId)
    {
        bool purple = State.IsPurchased(TerritoryId.Purple) || buyId == TerritoryId.Purple;
        bool red = State.IsPurchased(TerritoryId.Red) || buyId == TerritoryId.Red;
        bool green = State.IsPurchased(TerritoryId.Green) || buyId == TerritoryId.Green;
        bool yellow = State.IsPurchased(TerritoryId.Yellow) || buyId == TerritoryId.Yellow;
        bool pink = State.IsPurchased(TerritoryId.Pink) || buyId == TerritoryId.Pink;

        if (!purple) return StoreLevelId.Lvl1;

        if (purple && !(red || green))
            return StoreLevelId.Lvl2;

        if (purple && (red ^ green)) 
        {
            return red ? StoreLevelId.Lvl3_1 : StoreLevelId.Lvl3_2;
        }

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

        State.Purchased.Add(id);
        State.CurrentLevel = PredictLevelAfterPurchase(id);

        OnChanged?.Invoke();
    }
}
