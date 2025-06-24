using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpeechBubble;

public class TutorialDialogue : MonoBehaviour
{
    public SpeechBubble_TMP speechBubble;
    public GameObject arrowToSettingsButton, arrowToMoneyPanel, arrowToPauseButton, arrowToStoreButton, arrowToStorePanel, arrowToBuildPanel, arrowToWarehouse;
    public GameObject dialogueRoot;

    [TextArea(2, 5)]
    public List<string> dialogueLines;
    public float delayBetweenLines = 5f;

    private int currentLineIndex = 0;
    private bool isPaused = false;

    public void StartDialogue()
    {
        currentLineIndex = 0;
        dialogueRoot.SetActive(true);
        StartCoroutine(PlayDialogue());
    }

    private IEnumerator PlayDialogue()
    {
        while (currentLineIndex < dialogueLines.Count)
        {
            HideAllArrows();

            speechBubble.setDialogueText(dialogueLines[currentLineIndex]);

            if (currentLineIndex == 3)
            {
                isPaused = true;
                StartCoroutine(ShowArrowNextFrame(arrowToSettingsButton));
                yield break;
            }

            if (currentLineIndex == 4)
            {
                arrowToMoneyPanel.SetActive(true);
            }

            if (currentLineIndex == 6)
            {
                arrowToPauseButton.SetActive(true);
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

        dialogueRoot.SetActive(true);
        StartCoroutine(PlayDialogue());
    }
    public void HideDialogueTemporarily()
    {
        dialogueRoot.SetActive(false);
        arrowToSettingsButton.SetActive(false);
    }
    public void ResumeDialogue()
    {
        dialogueRoot.SetActive(true);

        if (isPaused)
        {
            ContinueDialogueAfterButton(); 
        }
        else
        {
            StartCoroutine(PlayDialogue());
        }
    }


    private void HideAllArrows()
    {
        arrowToSettingsButton?.SetActive(false);
        arrowToMoneyPanel?.SetActive(false);
        arrowToPauseButton?.SetActive(false);
        /*arrowToStoreButton?.SetActive(false);
        arrowToStorePanel?.SetActive(false);
        arrowToBuildPanel?.SetActive(false);
        arrowToWarehouse?.SetActive(false); */
    }

    private IEnumerator ShowArrowNextFrame(GameObject arrow)
    {
        yield return null;
        arrow?.SetActive(true);
    }

    private void OnDialogueFinished()
    {
        Debug.Log("Диалог завершён.");
    }
}
