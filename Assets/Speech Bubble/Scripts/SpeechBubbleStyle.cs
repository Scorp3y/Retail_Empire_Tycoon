using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpeechBubble
{
    [CreateAssetMenu]
    public class SpeechBubbleStyle : ScriptableObject
    {
        [Tooltip("The color for the fill")]
        public Color fillColor = Color.white;

        [Tooltip("The color for the outline")]
        public Color outlineColor = Color.black;

        [Tooltip("The color for the dialogue text")]
        public Color dialogueTextColor = Color.black;
    }
}
