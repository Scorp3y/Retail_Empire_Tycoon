using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SeamlessUITiling
{
    public static class RectTransformExtension
    {
        public static Canvas GetCanvas(this RectTransform rt)
        {
            return rt.gameObject.GetComponentInParent<Canvas>();
        }

        /// <summary>
        /// Gets an accurate delta size regardless of the anchors (including stretched), parent anchors, or canvas scaling
        /// </summary>
        /// <param name="rt">the rect to get the size of</param>
        /// <returns>an accurate deltaSize</returns>
        public static Vector2 GetImprovedDeltaSize(this RectTransform rt)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            float maxX = corners[0].x;
            float minX = corners[0].x;
            float maxY = corners[0].y;
            float minY = corners[0].y;

            for (int i = 1; i < corners.Length; i++)
            {
                maxX = Mathf.Max(maxX, corners[i].x);
                maxY = Mathf.Max(maxY, corners[i].y);
                minX = Mathf.Min(minX, corners[i].x);
                minY = Mathf.Min(minY, corners[i].y);
            }

            return new Vector2(maxX - minX, maxY - minY) / rt.GetCanvas().scaleFactor;
        }

    }
}