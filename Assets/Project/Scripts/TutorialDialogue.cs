using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpeechBubble;
using DG.Tweening;
using UnityEngine.UI;

public class TutorialDialogue : MonoBehaviour
{
    // Test
    public SpeechBubble_TMP speechBubble;
    public GameObject arrowToSettingsButton, arrowToMoneyPanel, arrowToPauseButton, arrowToStoreButton, arrowToStorePanel, arrowToBuildPanel,
        arrowToBuyShelf, arrowToWarehouse, arrowToBuyProduct;
    public Button settingBtnBlock, warehouseBtnBlock, storeBtnBlock, buildpanelBtnBlock, storepanelBtnBlock, exitStoreBttBlock, exitBuildpanelBlock;
    public GameObject dialogueRoot;
    public ButtonFadeIn showToSettings, showToMoney, showToPause, showToWarehouse, showToStore, showToButtonBuild, showToButtonStore, showToButtonStoreStore;
    public MonoBehaviour cameraControlScript;
    public TutorialController tutorialController;
    private TutorialButtonType currentExpectedButton = TutorialButtonType.None;

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
    private bool isTyping = false;
    private bool isTextTyping = false;
    private bool settingsOpened = false;


    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip clipMagicPop, clipTyping, clipMumbling, clipSwing;
    public AudioSource typingSource;

    public void StartDialogue()
    {
        speechBubble.SetBubbleType(SpeechBubbleType.Note);

        if (cameraControlScript != null)
            cameraControlScript.enabled = false;

        currentLineIndex = 0;
        dialogueRoot.SetActive(true);

        settingBtnBlock.gameObject.SetActive(false);
        settingBtnBlock.interactable = false;
        warehouseBtnBlock.gameObject.SetActive(false);
        warehouseBtnBlock.interactable = false;
        storeBtnBlock.gameObject.SetActive(false);
        storeBtnBlock.interactable = false;
        buildpanelBtnBlock.gameObject.SetActive(false);
        buildpanelBtnBlock.interactable = false;
        storepanelBtnBlock.gameObject.SetActive(false);
        storepanelBtnBlock.interactable |= false;
        exitStoreBttBlock.interactable = false;
        exitBuildpanelBlock.interactable = false;
        
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
            PlaySound(clipSwing);
            speechBubble.setDialogueText("");
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
                    yield return StartCoroutine(ShowArrowNextFrame(arrowToSettingsButton));

                    settingBtnBlock.gameObject.SetActive(true);
                    settingBtnBlock.interactable = false;  
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    settingBtnBlock.interactable = true; 
                    yield break;

                case 4:
                    settingBtnBlock.interactable = false;

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
                    currentExpectedButton = TutorialButtonType.Warehouse;
                    isPaused = true;
                    yield return StartCoroutine(ShowArrowNextFrame(arrowToWarehouse));
                    warehouseBtnBlock.gameObject.SetActive(true);
                    warehouseBtnBlock.interactable = false;
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    warehouseBtnBlock.interactable = true;
                    yield break;

                case 8:
                    warehouseBtnBlock.interactable = false;

                    speechBubble.SetBubbleType(SpeechBubbleType.Whisper);
                    PlayAnimation("Pickup");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    showToStore.Show();
                    currentExpectedButton = TutorialButtonType.Store;
                    isPaused = true;
                    yield return StartCoroutine(ShowArrowNextFrame(arrowToStoreButton));
                    storeBtnBlock.gameObject.SetActive(true);
                    storeBtnBlock.interactable = false;
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    storeBtnBlock.interactable = true;
                    yield break;

                case 9:
                    storeBtnBlock.interactable = false;

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
                    currentExpectedButton = TutorialButtonType.BuildPanel;

                    isPaused = true;
                    yield return StartCoroutine(ShowArrowNextFrame(arrowToBuildPanel));
                    buildpanelBtnBlock.gameObject.SetActive(true);
                    buildpanelBtnBlock.interactable = false;
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    buildpanelBtnBlock.interactable = true;
                    yield break;

                case 11:
                    buildpanelBtnBlock.interactable = false;

                    speechBubble.SetBubbleType(SpeechBubbleType.Note);
                    currentExpectedButton = TutorialButtonType.BuyShelf;
                    isPaused = true;
                    yield return StartCoroutine(ShowArrowNextFrame(arrowToBuyShelf));
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    yield break;

                case 12:
                    speechBubble.SetBubbleType(SpeechBubbleType.Note);
                    PlayAnimation("Pickup");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    showToButtonStore.Show();
                    currentExpectedButton = TutorialButtonType.StorePanel;

                    isPaused = true;
                    yield return StartCoroutine(ShowArrowNextFrame(arrowToStorePanel));
                    storepanelBtnBlock.gameObject.SetActive(true);
                    storepanelBtnBlock.interactable = false;
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    storepanelBtnBlock.interactable = true;
                    yield break;

                case 13:
                    storepanelBtnBlock.interactable = false;

                    showToButtonStoreStore.Show();
                    speechBubble.SetBubbleType(SpeechBubbleType.Whisper);
                    PlayAnimation("HoldLeft");
                    yield return new WaitForSeconds(0.5f);
                    PlayAnimation("Idle");
                    currentExpectedButton = TutorialButtonType.BuyProduct;
                    isPaused = true;
                    yield return StartCoroutine(ShowArrowNextFrame(arrowToBuyProduct));
                    yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
                    exitStoreBttBlock.interactable = true;
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

            }

            yield return StartCoroutine(TypeText(dialogueLines[currentLineIndex]));
            currentLineIndex++;
            yield return new WaitForSeconds(delayBetweenLines);

        }

        OnDialogueFinished();
    }

    public void OnTutorialButtonClicked(TutorialButtonType button)
    {
        if (isTextTyping) return;
        if (!isPaused) return;
        if (button != currentExpectedButton) return;

        if (button == TutorialButtonType.Settings)
        {
            settingBtnBlock.interactable = false; 
        }

        ContinueDialogueAfterButton();
    }


    private void BlockButton(TutorialButtonType button)
    {
        switch (button)
        {
            case TutorialButtonType.Settings:
                settingBtnBlock.interactable = false;
                break;
        }
    }


    private void MoveToCurrentPositions()
    {
        if (currentLineIndex < bubblePositions.Count)
            bubbleTransform.DOAnchorPos(bubblePositions[currentLineIndex], moveDuration);
    }

    public void ContinueDialogueAfterButton()
    {
        if (!isPaused) return;

        if (cameraControlScript != null)
            cameraControlScript.enabled = false;

        isPaused = false;
        dialogueRoot.SetActive(true);
        currentExpectedButton = TutorialButtonType.None;
        HideAllArrows();
        currentLineIndex++;
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

    private IEnumerator TypeText(string fullText, float charDelay = 0.03f)
    {
        isTextTyping = true;
        speechBubble.setDialogueText("");
        string currentText = "";

        if (clipTyping != null && typingSource != null)
        {
            typingSource.clip = clipTyping;
            typingSource.loop = true;
            typingSource.Play();
        }

        PlaySound(clipMumbling);

        foreach (char c in fullText)
        {
            currentText += c;
            speechBubble.setDialogueText(currentText);
            yield return new WaitForSeconds(charDelay);
        }

        if (typingSource != null && typingSource.isPlaying)
        {
            typingSource.Stop();
        }

        yield return new WaitForSeconds(3f);
        isTextTyping = false; 
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    private void OnDialogueFinished()
    {
        StartCoroutine(FinishTutorialLocally());
    }

    private IEnumerator FinishTutorialAfterDialogue()
    {
        yield return new WaitForSeconds(0.6f);

        if (tutorialController != null)
        {
            yield return tutorialController.PlayNoThenDie();
        }
    }

    private IEnumerator FinishTutorialLocally()
    {
        PlayerPrefs.SetInt("hasCompletedTutorial", 1);
        PlayerPrefs.Save();

        if (trainerAnimator != null)
        {
            trainerAnimator.SetTrigger("Die");
            yield return new WaitForSeconds(0.1f);
        }

        if (clipMagicPop != null && sfxSource != null)
            sfxSource.PlayOneShot(clipMagicPop);

        if (tutorialController != null && tutorialController.disappearParticles != null)
            tutorialController.disappearParticles.Play();

        yield return new WaitForSeconds(0.2f);

        if (trainerObject != null)
            Destroy(trainerObject);

        if (cameraControlScript != null)
            cameraControlScript.enabled = true;

        dialogueRoot.SetActive(false);

        showToSettings?.Show();
        showToMoney?.Show();
        showToPause?.Show();
        showToWarehouse?.Show();
        showToStore?.Show();
        showToButtonBuild?.Show();
        showToButtonStore?.Show();

        settingBtnBlock.gameObject.SetActive(true);
        settingBtnBlock.interactable = true;
        warehouseBtnBlock.gameObject.SetActive(true);
        warehouseBtnBlock.interactable = true;
        storeBtnBlock.gameObject.SetActive(true);
        storeBtnBlock.interactable = true;
        buildpanelBtnBlock.gameObject.SetActive(true);
        buildpanelBtnBlock.interactable = true;
        storepanelBtnBlock.gameObject.SetActive(true);
        storepanelBtnBlock.interactable |= true;
        exitStoreBttBlock.interactable = true;
        exitBuildpanelBlock.interactable = true;
    }
}

public enum TutorialButtonType
{
    None,
    Settings,
    Money,
    Pause,
    Warehouse,
    Store,
    BuildPanel,
    BuyShelf,
    StorePanel,
    BuyProduct
}

