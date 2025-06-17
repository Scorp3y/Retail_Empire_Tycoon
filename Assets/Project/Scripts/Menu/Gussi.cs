using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gussi : MonoBehaviour
{
    float speed = 200f; 
    public RectTransform canvasRect; 
    private RectTransform rectTransform;
    private bool movingRight = true;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        float direction = movingRight ? 1 : -1;
        rectTransform.anchoredPosition += new Vector2(direction * speed * Time.deltaTime, 0);
        float gooseWidth = rectTransform.rect.width;
        float canvasWidth = canvasRect.rect.width;
        if (movingRight && rectTransform.anchoredPosition.x + gooseWidth / 2 > canvasWidth / 2)
        {
            movingRight = false;
            Flip();
        }
        else if (!movingRight && rectTransform.anchoredPosition.x - gooseWidth / 2 < -canvasWidth / 2)
        {
            movingRight = true;
            Flip();
        }
    }

    void Flip()
    {
        Vector3 scale = rectTransform.localScale;
        scale.x *= -1;
        rectTransform.localScale = scale;
    }
}
