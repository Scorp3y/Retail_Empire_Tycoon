using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RGB : MonoBehaviour
{
    public Image targetImage; 
    float colorChangeSpeed = 0.3f; 

    void Update()
    {
        if (targetImage == null) return;

        float hue = Mathf.PingPong(Time.time * colorChangeSpeed, 1f); 
        Color rgbColor = Color.HSVToRGB(hue, 1f, 1f); 

        targetImage.color = rgbColor;
    }
}
