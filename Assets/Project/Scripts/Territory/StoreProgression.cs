using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class StoreProgression : MonoBehaviour
{
    /// <summary>
    /// Единственный источник истины прогресса магазина.
    ///
    /// В проекте легко случайно получить дубликаты StoreProgression внутри префабов магазина.
    /// Тогда разные системы (зоны, контроллер покупки, сохранение/загрузка, спавнер префабов)
    /// начинают ссылаться на РАЗНЫЕ инстансы. Симптомы:
    /// - покупка территории "не сохраняется";
    /// - затемнение/смена префаба не срабатывает или срабатывает не тот уровень;
    /// - доступность зон/уровней не обновляется.
    ///
    /// Поэтому делаем StoreProgression singleton'ом и уничтожаем дубликаты.
    /// </summary>
    public static StoreProgression Instance { get; private set; }

    public ProgressState State { get; private set; } = new();
    public event Action OnChanged;

    public IReadOnlyCollection<TerritoryId> Purchased => State.Purchased;

    private void Awake()
    {
        // Singleton guard: оставляем самый первый StoreProgression в сцене.
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[StoreProgression] Duplicate instance detected on '{name}'. Destroying duplicate.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsTerritoryAvailable(TerritoryId id)
    {
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
        if (purple && !(red || green)) return StoreLevelId.Lvl2;

        if (purple && (red ^ green)) // ровно один
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

        State.Purchased.Add(id);
        State.CurrentLevel = PredictLevelAfterPurchase(id);
        OnChanged?.Invoke();
    }

    public TerritorySaveData BuildSaveData()
    {
        var d = new TerritorySaveData();
        foreach (var id in State.Purchased)
            d.purchased.Add(id.ToString());
        d.storeLevel = State.CurrentLevel.ToString();
        return d;
    }

    public void ApplySaveData(TerritorySaveData d)
    {
        State.Purchased.Clear();

        if (d?.purchased != null)
        {
            foreach (var s in d.purchased)
                if (Enum.TryParse<TerritoryId>(s, out var id))
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
