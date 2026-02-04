using System;
using UnityEngine;

namespace MyShopGame.Core
{
    [CreateAssetMenu(menuName = "My Shop Game/Shelf Fill Visual Profile", fileName = "ShelfFillProfile_")]
    public class ShelfFillVisualProfile : ScriptableObject
    {
        public ShelfFillStage[] stages = Array.Empty<ShelfFillStage>();

        [Serializable]
        public class ShelfFillStage
        {
            public string stageName = "Empty";
            [Range(0f, 1f)]
            public float minFill01;
            public GameObject[] enableObjects = Array.Empty<GameObject>();
            public GameObject[] disableObjects = Array.Empty<GameObject>();
        }
    }
}
