using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class DeleteSave : MonoBehaviour
{
    public Button deleteButton;

    private string saveFilePath;

    void Start()
    {
        saveFilePath = Application.persistentDataPath + "/gameData.json";

        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(DeleteSaveFile);
        }
    }

    public void DeleteSaveFile()
    {
        if (File.Exists(saveFilePath))
        {
            File.Delete(saveFilePath);
        }
    }
}
