using UnityEngine;

namespace MyShopGame.UI.Context
{
    public sealed class ObjectContextMenu : MonoBehaviour
    {
        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
