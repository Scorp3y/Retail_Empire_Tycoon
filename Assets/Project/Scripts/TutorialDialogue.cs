using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpeechBubble;

public class TutorialDialogue : MonoBehaviour
{
    [Header("Настройки")]
    public SpeechBubble_TMP speechBubble;        // Ссылка на компонент SpeechBubble
    [TextArea(2, 5)]
    public List<string> dialogueLines;           // Реплики для показа
    public float delayBetweenLines = 5f;         // Интервал между строками (5 сек)

    private int currentLineIndex = 0;

    public void StartDialogue()
    {
        currentLineIndex = 0;
        StartCoroutine(PlayDialogue());
    }

    private IEnumerator PlayDialogue()
    {
        while (currentLineIndex < dialogueLines.Count)
        {
            speechBubble.setDialogueText(dialogueLines[currentLineIndex]);
            currentLineIndex++;
            yield return new WaitForSeconds(delayBetweenLines);
        }

        OnDialogueFinished();
    }

    private void OnDialogueFinished()
    {
        Debug.Log("Диалог завершён.");
        // Можно вызывать следующий этап туториала или скрывать UI
    }
}
