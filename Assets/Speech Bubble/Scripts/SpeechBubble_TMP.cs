using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace SpeechBubble
{
    /// <summary>
    /// Used for TMP text
    /// </summary>
    public class SpeechBubble_TMP : SpeechBubble
    {
        [SerializeField]
        [Tooltip("The text that holds the dialogue")]
        private TMP_Text dialogueTextComponent;

        private void Start()
        {
            updateSpeechBubble();
        }

        /// <summary>
        /// Sets the dialogue text to the given string
        /// </summary>
        public override void setDialogueText(string text)
        {
            dialogueText = text;
            if (dialogueTextComponent != null)
                dialogueTextComponent.text = text;
        }

        /// <summary>
        /// Sets the dialogue text color
        /// </summary>
        public override void setDialogueTextColor(Color color)
        {
            if (dialogueTextComponent != null)
                dialogueTextComponent.color = color;
        }

        /// <summary>
        /// Updates the text graphics
        /// </summary>
        protected override void updateTextGraphics()
        {
            if (dialogueTextComponent != null)
                dialogueTextComponent.text = dialogueText;
        }

        /// <summary>
        /// Проксирует тип пузыря в базовый метод
        /// </summary>
        public void SetBubbleType(SpeechBubbleType newType)
        {
            setBubbleType(newType); // вызов из базового класса
        }
    }
}
