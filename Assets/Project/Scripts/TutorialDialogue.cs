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

            speechBubble.setDialogueText(dialogueLines[currentLineIndex]);

            if (currentLineIndex == 0)
            { 
                speechBubble.SetBubbleType(SpeechBubbleType.Note); 

                PlayAnimation("Idle");
                yield return new WaitForSeconds(0.5f);
                PlayAnimation("HoldLeft");
            }
            if (currentLineIndex == 1) 
            {
                yield return new WaitForSeconds(0.5f);
                PlayAnimation("Idle");
            }
            
            if (currentLineIndex == 3)
            {
                PlayAnimation("Pickup");
                yield return new WaitForSeconds(0.5f);
                PlayAnimation("Idle");
                speechBubble.SetBubbleType(SpeechBubbleType.Whisper);
                isPaused = true;
                showToSettings.Show();
                StartCoroutine(ShowArrowNextFrame(arrowToSettingsButton));
                yield break;
            }

            if (currentLineIndex == 4)
            {
                PlayAnimation("Pickup");
                yield return new WaitForSeconds(0.5f);
                PlayAnimation("Idle");
                speechBubble.SetBubbleType(SpeechBubbleType.Whisper);
                showToMoney.Show();
                arrowToMoneyPanel.SetActive(true);
            }

            if (currentLineIndex == 5) 
            {
                PlayAnimation("HoldLeft");
                speechBubble.SetBubbleType(SpeechBubbleType.Note); 
            }

            if (currentLineIndex == 6)
            {
                PlayAnimation("Pickup");
                yield return new WaitForSeconds(0.5f);
                PlayAnimation("Idle");
                speechBubble.SetBubbleType(SpeechBubbleType.Whisper);
                showToPause.Show();
                arrowToPauseButton.SetActive(true);
            }

            if (currentLineIndex == 7)
            {
                PlayAnimation("Pickup");
                yield return new WaitForSeconds(0.5f);
                PlayAnimation("Idle");
                speechBubble.SetBubbleType(SpeechBubbleType.Stress);
                showToWarehouse.Show();
                isPaused = true;
                StartCoroutine(ShowArrowNextFrame(arrowToWarehouse));
                yield break;
            }

            if (currentLineIndex == 8)
            {
                PlayAnimation("Pickup");
                yield return new WaitForSeconds(0.5f);
                PlayAnimation("Idle");
                speechBubble.SetBubbleType(SpeechBubbleType.Whisper);
                showToStore.Show();
                isPaused = true;
                StartCoroutine(ShowArrowNextFrame(arrowToStoreButton));
                yield break;
            }

            if (currentLineIndex == 9) 
            {
                PlayAnimation("Fall");
                yield return new WaitForSeconds(0.5f);
                PlayAnimation("Idle");
                speechBubble.SetBubbleType(SpeechBubbleType.Note);
            }

            if (currentLineIndex == 10)
            {
                PlayAnimation("HoldLeft");
                yield return new WaitForSeconds(0.5f);
                PlayAnimation("Idle");
                speechBubble.SetBubbleType(SpeechBubbleType.Note);
                showToButtonBuild.Show();
                isPaused = true;
                StartCoroutine(ShowArrowNextFrame(arrowToBuildPanel));
                yield break;
            }

            if (currentLineIndex == 11)
            {
                speechBubble.SetBubbleType(SpeechBubbleType.Note);
                isPaused = true;
                StartCoroutine(ShowArrowNextFrame(arrowToBuyShelf));
                yield break;
            }

            if (currentLineIndex == 12)
            {
                PlayAnimation("Pickup");
                yield return new WaitForSeconds(0.5f);
                PlayAnimation("Idle");
                speechBubble.SetBubbleType(SpeechBubbleType.Note);
                showToButtonStore.Show();
                isPaused = true;
                StartCoroutine(ShowArrowNextFrame(arrowToStorePanel));
                yield break;
            }

            if (currentLineIndex == 13)
            {
                PlayAnimation("HoldLeft");
                yield return new WaitForSeconds(0.5f);
                PlayAnimation("Idle");
                speechBubble.SetBubbleType(SpeechBubbleType.Whisper);
                isPaused = true;
                StartCoroutine(ShowArrowNextFrame(arrowToBuyProduct));
                yield break;
            }

            if (currentLineIndex == 14) 
            {
                PlayAnimation("Fall");
                yield return new WaitForSeconds(0.5f);
                PlayAnimation("Idle");
                speechBubble.SetBubbleType(SpeechBubbleType.Note); 
            }

            if (currentLineIndex == 16)
            {
                PlayAnimation("Die");
                speechBubble.SetBubbleType(SpeechBubbleType.Note);
                StartCoroutine(FinishTutorialAfterDialogue());
                yield break;
            }

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


}
