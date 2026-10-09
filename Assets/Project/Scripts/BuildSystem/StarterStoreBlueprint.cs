using UnityEngine;

namespace RetailEmpireTycoon.BuildSystem
{
    /// <summary>An authored new-game snapshot. Loading an existing save never copies this layout over it.</summary>
    [CreateAssetMenu(menuName = "Retail Empire Tycoon/Starter Store")]
    public sealed class StarterStoreBlueprint : ScriptableObject
    {
        public GameData initialState = new GameData();
        public Rect serviceYard;
        public Vector3 pickupPosition;
        public Vector3 pickupEuler;
        public Vector3 cameraFocus;
        public GameData CreateNewGame(int startingMoney)
        {
            var result = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(initialState));
            result.usesModularStore = true;
            result.playerMoney = startingMoney;
            return result;
        }
    }
}
