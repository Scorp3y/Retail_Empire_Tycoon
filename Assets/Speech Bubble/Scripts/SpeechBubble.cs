using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpeechBubble
{
    /// <summary>
    /// Base for a speech bubble.
    /// SpeechBubble_TMP and SpeechBubble_Legacy are the two scripts that use this abstract class
    /// </summary>
    public abstract class SpeechBubble : MonoBehaviour
    {
        #region all variables
        #region bubble
        [Header("Bubble")]
        [Tooltip("The speech bubble's fill")]
        [SerializeField]
        private GameObject fill;

        [SerializeField]
        [Tooltip("The speech bubble's outline")]
        private GameObject outline;

        [SerializeField]
        [Tooltip("The style of the box (determines colors)")]
        private SpeechBubbleStyle style;

        [SerializeField]
        [Tooltip("The emotion of the bubble and its outline")]
        private SpeechBubbleType bubbleType;
        #endregion

        [Header("Text")]
        [SerializeField]
        [TextArea]
        protected string dialogueText;
        #endregion

        #region editor only
#if UNITY_EDITOR

        //runs when you make a change to the inspector
        private void OnValidate()
        {
            updateEditor();
        }

        private AnimationClip FindAnimation(Animator animator, string name)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == name)
                {
                    return clip;
                }
            }

            return null;
        }

        /// <summary>
        /// This is for Unity editor only and so it should not be called.
        /// It forcefully applies changes made in the inspector to the bubble.
        /// </summary>
        public void updateEditor()
        {
            if (UnityEditor.EditorApplication.isPlaying)
            {
                updateSpeechBubble();
            }
            else if (UnityEditor.Selection.activeObject == gameObject)
            {
                UnityEditor.AnimationMode.StartAnimationMode();
                UnityEditor.AnimationMode.BeginSampling();
                UnityEditor.AnimationMode.SampleAnimationClip(gameObject, FindAnimation(gameObject.GetComponent<Animator>(), bubbleType.ToString()), 0);
                UnityEditor.AnimationMode.EndSampling();
                UnityEditor.AnimationMode.StopAnimationMode();

                //for some reason the animation does not correctly set the image type in editor mode, so set the image type correctly
                if (bubbleType == SpeechBubbleType.Stress)
                {
                    fill.gameObject.GetComponent<Image>().type = Image.Type.Sliced;
                    outline.gameObject.GetComponent<Image>().type = Image.Type.Sliced;
                }
                else
                {
                    fill.gameObject.GetComponent<Image>().type = Image.Type.Tiled;
                    outline.gameObject.GetComponent<Image>().type = Image.Type.Tiled;
                }

                UnityEditor.SceneView.RepaintAll();

                //updates speech bubble (without updating animator since animator is inactive in editor mode)
                updateTextGraphics();
                revertStyle();
                gameObject.GetComponent<Animator>().enabled = true;
            }
        }
#endif

#endregion


        #region abstract functions
        /// <summary>
        /// Sets the dialogue text to the string
        /// </summary>
        /// <param name="text"></param>
        public abstract void setDialogueText(string text);

        /// <summary>
        /// Sets the dialogue text to the color
        /// </summary>
        /// <param name="color"></param>
        public abstract void setDialogueTextColor(Color color);

        /// <summary>
        /// Updates the text graphics to match with the inspector values
        /// </summary>
        protected abstract void updateTextGraphics();
        #endregion


        #region public functions

        /// <summary>
        /// Sets the fill, outline, and dialogue text to match the style parameter. Individual colors can still be changed without affecting the original SpeechBubbleStyle.
        /// </summary>
        /// <param name="style">The style to copy</param>
        public void setStyle(SpeechBubbleStyle style)
        {
            this.style = style;
            revertStyle();
        }

        /// <summary>
        /// Resets the colors to match with the current speech bubble's style
        /// </summary>
        public void revertStyle()
        {
            setFillColor(style.fillColor);
            setOutlineColor(style.outlineColor);
            setDialogueTextColor(style.dialogueTextColor);
        }

        /// <summary>
        /// Sets the fill color
        /// </summary>
        /// <param name="fillColor"></param>
        public void setFillColor(Color fillColor)
        {
            fill.GetComponent<Image>().color = fillColor;
        }

        /// <summary>
        /// Sets the outline color
        /// </summary>
        /// <param name="outlineColor"></param>
        public void setOutlineColor(Color outlineColor)
        {
            outline.GetComponent<Image>().color = outlineColor;
        }

        /// <summary>
        /// Sets the Bubble type to the correct graphics type
        /// </summary>
        /// <param name="type">The bubble type</param>
        public void setBubbleType(SpeechBubbleType type)
        {
            bubbleType = type;
            updateAnimator();
        }

        /// <summary>
        /// Updates the entire Speech Bubble and dialogue text to match with the values in the inspector (including reverting the colors to the Speech Bubble Style's colors)
        /// </summary>
        public void updateSpeechBubble()
        {
            updateTextGraphics();
            updateAnimator();
            revertStyle();
        }
        #endregion

        #region private functions
        /// <summary>
        /// Updates the animator to play the bubbleType animation
        /// </summary>
        private void updateAnimator()
        {
            //bubbleType
            gameObject.GetComponent<Animator>().Play(bubbleType.ToString());
        }

        #endregion

    }
}
