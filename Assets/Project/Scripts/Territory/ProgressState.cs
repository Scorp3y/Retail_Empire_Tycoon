using System;
using System.Collections.Generic;

[Serializable]
public sealed class ProgressState
{
    public HashSet<TerritoryId> Purchased = new();
    public StoreLevelId CurrentLevel = StoreLevelId.Lvl1;
    public bool IsPurchased(TerritoryId id) => Purchased.Contains(id);
}
