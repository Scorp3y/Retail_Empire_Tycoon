using System.Collections;
using UnityEngine;

public class TutorialController : MonoBehaviour
{
    public GameObject tutorialChoiceUI;
    public GameObject tutorialUI;
    public TutorialDialogue tutorialDialogue;
    public GameObject characterRoot;
    public MonoBehaviour cameraControlScript;
    public Animator trainerAnimator;
    public ParticleSystem disappearParticles;
    public ButtonFadeIn showToSettings, showToMoney, showToPause, showToWarehouse, showToStore, showToButtonBuild, showToButtonStore;

    void Start()
    {
        if (disappearParticles != null)
            disappearParticles.Stop();

        bool showTutorial = PlayerPrefs.GetInt("hasCompletedTutorial", 0) == 0;

        tutorialChoiceUI.SetActive(showTutorial);
        tutorialUI.SetActive(false);
        characterRoot.SetActive(showTutorial);

        if (cameraControlScript != null)
            cameraControlScript.enabled = !showTutorial;
    }


    public void OnChooseTutorialYes()
    {
        trainerAnimator?.SetTrigger("Yes");
        if (cameraControlScript != null)
            cameraControlScript.enabled = false;

        tutorialChoiceUI.SetActive(false);
        tutorialUI.SetActive(true);
        characterRoot.SetActive(true);
        tutorialDialogue.StartDialogue();
    }

    public void OnChooseTutorialNo()
    {
        StartCoroutine(PlayNoThenDie());
    }

    public IEnumerator PlayNoThenDie()
    {
        PlayerPrefs.SetInt("hasCompletedTutorial", 1);
        PlayerPrefs.Save();

        trainerAnimator.SetTrigger("No");
        yield return new WaitForSeconds(0.8f);

        trainerAnimator.SetTrigger("Die");
        yield return new WaitForSeconds(0.1f);

        if (disappearParticles != null)
            disappearParticles.Play();

        yield return new WaitForSeconds(0.2f);

        if (characterRoot != null)
            Destroy(characterRoot);

        if (cameraControlScript != null)
            cameraControlScript.enabled = true;

        tutorialChoiceUI.SetActive(false);
        tutorialUI.SetActive(false);

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
