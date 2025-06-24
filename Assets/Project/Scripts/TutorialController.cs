using UnityEngine;

public class TutorialController : MonoBehaviour
{
    public GameObject tutorialChoiceUI; 
    public GameObject tutorialUI;
    public TutorialDialogue tutorialDialogue;
    void Start()
    {
        if (PlayerPrefs.GetInt("hasCompletedTutorial", 0) == 1)
        {
            tutorialChoiceUI.SetActive(false);
            tutorialUI.SetActive(false);
        }
        else
        {
            tutorialChoiceUI.SetActive(true);
        }
    }

    public void OnChooseTutorialYes()
    {
        tutorialChoiceUI.SetActive(false);
        tutorialUI.SetActive(true);
        tutorialDialogue.StartDialogue(); 
    }


    public void OnChooseTutorialNo()
    {
        PlayerPrefs.SetInt("hasCompletedTutorial", 1);
        PlayerPrefs.Save();

        tutorialChoiceUI.SetActive(false);
        tutorialUI.SetActive(false);
    }

    [ContextMenu("Сбросить обучение")]
    public void ResetTutorial()
    {
        PlayerPrefs.DeleteKey("hasCompletedTutorial");
        PlayerPrefs.Save();
        Debug.Log("Обучение сброшено (hasCompletedTutorial удалён).");
    }
}
