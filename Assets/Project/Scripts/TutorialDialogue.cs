using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpeechBubble;
using DG.Tweening; 

public class TutorialDialogue : MonoBehaviour
{
    public SpeechBubble_TMP speechBubble;
    public GameObject arrowToSettingsButton, arrowToMoneyPanel, arrowToPauseButton, arrowToStoreButton, arrowToStorePanel, arrowToBuildPanel,
        arrowToBuyShelf, arrowToWarehouse, arrowToBuyProduct;
    public GameObject dialogueRoot;
    public ButtonFadeIn showToSettings, showToMoney, showToPause, showToWarehouse, showToStore, showToButtonBuild, showToButtonStore;
    public MonoBehaviour cameraControlScript;
    public TutorialController tutorialController;

    [Header("Transform moving")]
    public RectTransform bubbleTransform; 
    public List<Vector2> bubblePositions;
    public List<Vector3> characterPositions;

    [Header("Character Animation")]
    public Animator trainerAnimator; 
    public GameObject trainerObject; 

    [TextArea(2, 5)]
    public List<string> dialogueLines;

    public float delayBetweenLines = 5f;
    public float moveDuration = 0.5f;

    private int currentLineIndex = 0;
    private bool isPaused = false;

    public void StartDialogue()
    {
        speechBubble.SetBubbleType(SpeechBubbleType.Note);
        if (cameraControlScript != null)
            cameraControlScript.enabled = false;
        currentLineIndex = 0;
        dialogueRoot.SetActive(true);
        StartCoroutine(PlayDialogue());
    }

    private void PlayAnimation(string trigger)
    {
        if (trainerAnimator != null)
            trainerAnimator.SetTrigger(trigger);
    }

    private IEnumerator PlayDialogue()
    {
        while (currentLineIndex < dialogueLines.Count)
        {
            HideAllArrows();
            MoveToCurrentPositions();

            switch (currentLineIndex)
            {
                case 0:
                    speechBubble.SetBubbleType(SpeechBubbleType.Note);
                    PlayAnimation("Idle");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("HoldLeft");
                    break;

                case 1:
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    break;

                case 3:
                    speechBubble.SetBubbleType(SpeechBubbleType.Whisper);
                    PlayAnimation("Pickup");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    isPaused = true;
                    showToSettings.Show();
                    StartCoroutine(ShowArrowNextFrame(arrowToSettingsButton));
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    yield break;

                case 4:
                    speechBubble.SetBubbleType(SpeechBubbleType.Whisper);
                    PlayAnimation("Pickup");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    showToMoney.Show();
                    arrowToMoneyPanel.SetActive(true);
                    break;

                case 5:
                    speechBubble.SetBubbleType(SpeechBubbleType.Note);
                    PlayAnimation("HoldLeft");
                    break;

                case 6:
                    speechBubble.SetBubbleType(SpeechBubbleType.Whisper);
                    PlayAnimation("Pickup");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    showToPause.Show();
                    arrowToPauseButton.SetActive(true);
                    break;

                case 7:
                    speechBubble.SetBubbleType(SpeechBubbleType.Stress);
                    PlayAnimation("Pickup");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    showToWarehouse.Show();
                    isPaused = true;
                    StartCoroutine(ShowArrowNextFrame(arrowToWarehouse));
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    yield break;

                case 8:
                    speechBubble.SetBubbleType(SpeechBubbleType.Whisper);
                    PlayAnimation("Pickup");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    showToStore.Show();
                    isPaused = true;
                    StartCoroutine(ShowArrowNextFrame(arrowToStoreButton));
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    yield break;

                case 9:
                    speechBubble.SetBubbleType(SpeechBubbleType.Note);
                    PlayAnimation("Fall");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    break;

                case 10:
                    speechBubble.SetBubbleType(SpeechBubbleType.Note);
                    PlayAnimation("HoldLeft");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    showToButtonBuild.Show();
                    isPaused = true;
                    StartCoroutine(ShowArrowNextFrame(arrowToBuildPanel));
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    yield break;

                case 11:
                    speechBubble.SetBubbleType(SpeechBubbleType.Note);
                    isPaused = true;
                    StartCoroutine(ShowArrowNextFrame(arrowToBuyShelf));
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    yield break;

                case 12:
                    speechBubble.SetBubbleType(SpeechBubbleType.Note);
                    PlayAnimation("Pickup");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    showToButtonStore.Show();
                    isPaused = true;
                    StartCoroutine(ShowArrowNextFrame(arrowToStorePanel));
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    yield break;

                case 13:
                    speechBubble.SetBubbleType(SpeechBubbleType.Whisper);
                    PlayAnimation("HoldLeft");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    isPaused = true;
                    StartCoroutine(ShowArrowNextFrame(arrowToBuyProduct));
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    yield break;

                case 14:
                    speechBubble.SetBubbleType(SpeechBubbleType.Note);
                    PlayAnimation("Fall");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    break;

                case 16:
                    speechBubble.SetBubbleType(SpeechBubbleType.Note);
                    PlayAnimation("Die");
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    StartCoroutine(FinishTutorialAfterDialogue());
                    yield break;

                default:
                    break;
            }

            yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
            currentLineIndex++;
            yield return new WaitForSeconds(delayBetweenLines);
        }

        OnDialogueFinished();
    }


    private void MoveToCurrentPositions()
    {
        if (currentLineIndex < bubblePositions.Count)
            bubbleTransform.DOAnchorPos(bubblePositions[currentLineIndex], moveDuration);
    }

    public void ContinueDialogueAfterButton()
    {
        if (!isPaused) return;

        HideAllArrows();
        isPaused = false;
        currentLineIndex++;

        dialogueRoot.SetActive(true);
        StartCoroutine(PlayDialogue());
    }

    public void HideDialogueTemporarily()
    {
        dialogueRoot.SetActive(false);
        HideAllArrows();
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
        arrowToStoreButton?.SetActive(false);
        arrowToStorePanel?.SetActive(false);
        arrowToBuildPanel?.SetActive(false);
        arrowToWarehouse?.SetActive(false);
        arrowToBuyShelf?.SetActive(false);
        arrowToBuyProduct?.SetActive(false);
    }

    private IEnumerator ShowArrowNextFrame(GameObject arrow)
    {
        yield return null;
        arrow?.SetActive(true);
    }

    private void OnDialogueFinished()
    {
        Debug.Log("Диалог завершён.");
        if (cameraControlScript != null)
            cameraControlScript.enabled = true;

    }
    private IEnumerator FinishTutorialAfterDialogue()
    {
        yield return new WaitForSeconds(0.6f); 

        if (tutorialController != null)
        {
            yield return tutorialController.PlayNoThenDie(); 
        }
    }

    private IEnumerator TypeText(string fullText, float charDelay = 0.03f)
    {
        speechBubble.setDialogueText("");
        string currentText = "";

        foreach (char c in fullText)
        {
            currentText += c;
            speechBubble.setDialogueText(currentText);
            yield return new WaitForSeconds(charDelay);
        }
        yield return new WaitForSeconds(3f); 
    }
}
