using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public Button star, settingOpen, settingExit, exit;
    public GameObject setting, menu; 

    void Start()
    {
    }

    public void StartGame()
    {
        SceneManager.LoadScene("Loading"); 
        
    }

    public void OpenSetting()
    {
           setting.SetActive(true);
        menu.SetActive(false);
    }

    public void ExitSetting() {
        setting.SetActive(false);
        menu.SetActive(true);
    }

    public void ExitGame()
    { 
        Application.Quit();
    }
}
