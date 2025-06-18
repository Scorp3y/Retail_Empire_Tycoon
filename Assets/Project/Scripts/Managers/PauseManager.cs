using UnityEngine;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    public Sprite pauseSprite, playSprite; 
    public Button pauseButton; 
    private Image buttonImage; 

    private bool isPaused = false;

    void Start()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick.AddListener(TogglePause);
            buttonImage = pauseButton.GetComponent<Image>();
            UpdateButtonSprite();
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        UpdateButtonSprite();
    }

    private void UpdateButtonSprite()
    {
        if (buttonImage != null)
        {
            buttonImage.sprite = isPaused ? playSprite : pauseSprite;
        }
    }
}
