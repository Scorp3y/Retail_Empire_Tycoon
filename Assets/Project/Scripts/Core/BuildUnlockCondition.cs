using UnityEngine;

namespace MyShopGame.Core
{
    [CreateAssetMenu(menuName = "My Shop Game/Unlock Condition", fileName = "UnlockCondition_")]
    public class BuildUnlockCondition : ScriptableObject
    {
        [Header("Progress")]
        public int requiredStoreLevel;

        [Header("Quest")]
        public string requiredQuestId;

        [Header("Territory")]
        public string requiredTerritoryId;

        [Header("Dependencies")]
        public bool requiresPreviousItemPurchased;
        public string previousItemId;
    }
}
