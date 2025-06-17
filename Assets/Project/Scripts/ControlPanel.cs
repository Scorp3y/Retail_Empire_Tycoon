using System.Collections;
using System.Collections.Generic;
using TMPro.Examples;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ControlPanel : MonoBehaviour
{
    public GameObject storePanel, buildPanel, warehousePanel, settingPanel;
    public MainCamera cameraController;
    void Start()
    {
        storePanel.SetActive(false);
        buildPanel.SetActive(false);
        warehousePanel.SetActive(false);
        settingPanel.SetActive(false);

        if (cameraController == null)
        {
            cameraController = Camera.main.GetComponent<MainCamera>(); 
        }
    }

    void LockCamera(bool locked)
    {
        if (cameraController != null)
        {
            cameraController.enabled = !locked; 
        }
    }

    public void OpenPanelStore()
    {
        storePanel.SetActive(true);
        buildPanel.SetActive(false);
        warehousePanel.SetActive(false);
        settingPanel.SetActive(false);
        LockCamera(true);
    }

    public void ExitPanelStore()
    {
        storePanel.SetActive(false);
        buildPanel.SetActive(false);
        warehousePanel.SetActive(false);
        settingPanel.SetActive(false);
        LockCamera(false);
    }

    public void TransitionStore()
    {
        storePanel.SetActive(true);
        buildPanel.SetActive(false);
        warehousePanel.SetActive(false);
        settingPanel.SetActive(false);
        LockCamera(true);
    }

    public void TransitionBuild()
    {
        storePanel.SetActive(false);
        warehousePanel.SetActive(false);
        settingPanel.SetActive(false);
        buildPanel.SetActive(true);
        LockCamera(true);
    }

    public void OpenPanelWarehouse()
    {
        warehousePanel.SetActive(true);
        storePanel.SetActive(false);
        buildPanel.SetActive (false);
        settingPanel.SetActive(false);
        LockCamera(true);
    }

    public void ExitWarehouse() 
    { 
        warehousePanel.SetActive(false);
        buildPanel.SetActive(false);
        storePanel.SetActive(false);
        settingPanel.SetActive(false);
        LockCamera(false);
    }

    public void OpenSetting()
    {
       settingPanel.SetActive(true);
        LockCamera(true);
    }

    public void ExitSetting ()
    {
        warehousePanel.SetActive(false);
        buildPanel.SetActive(false);
        storePanel.SetActive(false);
        settingPanel.SetActive(false);
        LockCamera(false);
    }

    public void BackMenu()
    {
        SceneManager.LoadScene("Menu");
    }
    public void ExitGame()
    {
        Application.Quit();
    }

}
