using UnityEngine;

public class TutorialController : MonoBehaviour
{
    public GameObject tutorialChoiceUI;
    public GameObject tutorialUI;
    public TutorialDialogue tutorialDialogue;
    public GameObject characterRoot;
    public MonoBehaviour cameraControlScript;

    public ButtonFadeIn showToSettings, showToMoney, showToPause, showToWarehouse, showToStore, showToButtonBuild, showToButtonStore;

    void Start()
    {
        if (cameraControlScript != null)
            cameraControlScript.enabled = false;

        tutorialChoiceUI.SetActive(PlayerPrefs.GetInt("hasCompletedTutorial", 0) == 0);
        tutorialUI.SetActive(false);
        characterRoot.SetActive(true);
    }

    public void OnChooseTutorialYes()
    {
        if (cameraControlScript != null)
            cameraControlScript.enabled = false;

        tutorialChoiceUI.SetActive(false);
        tutorialUI.SetActive(true);
        characterRoot.SetActive(true);
        tutorialDialogue.StartDialogue();
    }

    public void OnChooseTutorialNo()
    {
        PlayerPrefs.SetInt("hasCompletedTutorial", 1);
        PlayerPrefs.Save();

        if (cameraControlScript != null)
            cameraControlScript.enabled = true;

        tutorialChoiceUI.SetActive(false);
        tutorialUI.SetActive(false);
        characterRoot.SetActive(false);

        showToSettings?.Show();
        showToMoney?.Show();
        showToPause?.Show();
        showToWarehouse?.Show();
        showToStore?.Show();
        showToButtonBuild?.Show();
        showToButtonStore?.Show();
    }

    [ContextMenu("Сбросить обучение")]
    public void ResetTutorial()
    {
        PlayerPrefs.DeleteKey("hasCompletedTutorial");
        PlayerPrefs.Save();
        Debug.Log("Обучение сброшено.");
    }
}
