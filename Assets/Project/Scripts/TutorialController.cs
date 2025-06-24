using UnityEngine;

public class TutorialController : MonoBehaviour
{
    public GameObject tutorialChoiceUI; 
    public GameObject tutorialUI;
    public TutorialDialogue tutorialDialogue;
    public GameObject characterRoot;

    void Start()
    {
        tutorialChoiceUI.SetActive(PlayerPrefs.GetInt("hasCompletedTutorial", 0) == 0);
        tutorialUI.SetActive(false);
        characterRoot.SetActive(false); 
    }

    public void OnChooseTutorialYes()
    {
        tutorialChoiceUI.SetActive(false);
        tutorialUI.SetActive(true);
        characterRoot.SetActive(true);
        tutorialDialogue.StartDialogue(); 
    }


    public void OnChooseTutorialNo()
    {
        PlayerPrefs.SetInt("hasCompletedTutorial", 1);
        PlayerPrefs.Save();

        tutorialChoiceUI.SetActive(false);
        tutorialUI.SetActive(false);
        characterRoot.SetActive(false);
    }

    [ContextMenu("Сбросить обучение")]
    public void ResetTutorial()
    {
        PlayerPrefs.DeleteKey("hasCompletedTutorial");
        PlayerPrefs.Save();
        Debug.Log("Обучение сброшено.");
    }
}
