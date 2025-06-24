using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpeechBubble;

public class TutorialDialogue : MonoBehaviour
{
    public SpeechBubble_TMP speechBubble;
    [TextArea(2, 5)]
    public List<string> dialogueLines;
    public float delayBetweenLines = 5f;

    public GameObject arrowToSettingsButton; 

    private int currentLineIndex = 0;
    private bool isPaused = false;

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

            if (currentLineIndex == 2)
            {
                arrowToSettingsButton.SetActive(true);
                isPaused = true;
                Debug.Log("Ожидается нажатие на кнопку Настройки.");
                yield break; 
            }

            currentLineIndex++;
            yield return new WaitForSeconds(delayBetweenLines);
        }

        OnDialogueFinished();
    }

    public void ContinueDialogueAfterButton()
    {
        if (!isPaused) return;

        arrowToSettingsButton.SetActive(false); 
        isPaused = false;
        currentLineIndex++;
        StartCoroutine(PlayDialogue());
    }

    private void OnDialogueFinished()
    {
        Debug.Log("Диалог завершён.");
    }
}
