using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCamera : MonoBehaviour
{
    float zoomSpeed = 2f;
    float moveSpeed = 1f;

    private Vector3 lastMousePos;

    public float minX = -2f;
    public float maxX = 10f;
    public float minZ = -10f;
    public float maxZ = -1f;
    public float minY = 5f;
    public float maxY = 6f;
    public float minZoomDistance = 5f;
    public float maxZoomDistance = 15f;


    void Update()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Vector3 zoomDirection = transform.forward * scroll * zoomSpeed;
        Vector3 newPosition = transform.position + zoomDirection;

        float distanceFromCenter = Vector3.Distance(newPosition, Vector3.zero);
        float newY = newPosition.y;

        if (distanceFromCenter >= minZoomDistance && distanceFromCenter <= maxZoomDistance && newY >= minY && newY <= maxY)
        {
            transform.position = newPosition;
        }


        if (Input.GetMouseButtonDown(0))
        {
            lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            Vector3 move = new Vector3(-delta.x, -delta.y, 0) * moveSpeed * Time.deltaTime;
            transform.Translate(move, Space.Self);
            lastMousePos = Input.mousePosition;

            Vector3 clampedPos = transform.position;
            clampedPos.x = Mathf.Clamp(clampedPos.x, minX, maxX);
            clampedPos.z = Mathf.Clamp(clampedPos.z, minZ, maxZ);
            clampedPos.y = Mathf.Clamp(clampedPos.y, minY, maxY);
            transform.position = clampedPos;
        }
    }
}
