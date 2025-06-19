using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeamlessUITiling
{
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(RectTransform))]
    public class UIMatchSize : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The rect to match the size of (usually the immediate parent)")]
        private RectTransform targetRect;

        [SerializeField]
        [Tooltip("If true then the scale of this UI will be modified to better fit targetRect's delta size. But it may cause uneven stretching in its children")]
        private bool affectScale;

        private RectTransform myRect;
        private Image myImage;

        //a more accurate sizeDelta for targetRect
        private Vector2 sizeDelta;

        // Start is called before the first frame update
        void Start()
        {
            myRect = gameObject.GetComponent<RectTransform>();
            myImage = gameObject.GetComponent<Image>();
            checkForWarnings();
        }

        // Update is called once per frame
        void Update()
        {
            updateSize();
        }

        public void updateSize()
        {
            sizeDelta = targetRect.GetImprovedDeltaSize();
            if (myImage.type == Image.Type.Tiled)
            {
                tiledSetSize();
            }
            else
            {
                basicSetSize();
            }
        }

        /// <summary>
        /// Debugs any issues about UIMatchSize script
        /// </summary>
        private void checkForWarnings()
        {
            if (myRect.anchorMax != myRect.anchorMin)
            {
                Debug.LogWarning("A gameobject with UIMatchSize script (" + gameObject.name + ") should not have its rect transform set to stretched because it will produce inaccurate scaling.");
            }
        }

        /// <summary>
        /// Sets the size of the rect to match the size of its parent. No additional considerations about seamless tiling.
        /// </summary>
        private void basicSetSize()
        {
            resetScale();
            myRect.sizeDelta = sizeDelta;
        }

        private void resetScale()
        {
            //reset the scale (in case it was set by the tiledSetSize function)
            myRect.localScale = Vector2.one;
        }

        /// <summary>
        /// Sets the size making sure the tiling is seamless.
        /// Also may change the number of segments
        /// </summary>
        private void tiledSetSize()
        {
            //border.x is left offset
            //border.z is right offset
            //border.w is top offset
            //border.y is bottom offset
            //all border values are positive
            Vector4 border = myImage.sprite.border;

            //offset.x is the combined width of the left and right edges, and offset.y is the combined height of the top and bottom edges
            Vector2 offset;
            offset.x = border.x + border.z;
            offset.y = border.w + border.y;

            //the dimensions of the imported image sprite in pixels (not taking into account pixelsPerUnit)
            Vector2 imageDimensions = myImage.sprite.rect.size;

            //Multiplier is the width (multiplier.x) and height (multiplier.y) of the tileable square in center of image
            Vector2 multiplier = imageDimensions - offset;

            float pixelScale = myImage.pixelsPerUnitMultiplier * myImage.pixelsPerUnit;
            offset = offset / pixelScale;
            multiplier = multiplier / pixelScale;

            //The number of repeating segments (not counting the corners)
            //floorToInt rounds down which means the textbox will never appear outside the size of targetRect
            Vector2Int segments = Vector2Int.FloorToInt((sizeDelta - offset) / multiplier);
            myRect.sizeDelta = (segments * multiplier) + offset;

            if (affectScale)
            {
                applyScaling();
            }
            else
            {
                resetScale();
            }

        }

        /// <summary>
        /// Applies scaling to stretch this to the exact size of targetRect. Note: X and Y stretch could be unequal. 
        /// Children will also stretch, so this effect might not be desirable if this object has a child with a Text component.
        /// </summary>
        private void applyScaling()
        {
            Vector2 scaleFactor = sizeDelta / myRect.sizeDelta;
            myRect.localScale = scaleFactor;
        }

    }

}