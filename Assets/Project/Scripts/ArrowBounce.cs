using UnityEngine;

public class ArrowBounce : MonoBehaviour
{
    public float moveDistance = 20f;
    public float speed = 10f;
    public Vector2 moveDirection = new Vector2(1, 1);

    private Vector3 startLocalPosition;
    private Vector3 normalizedDirection;

    private void Start()
    {
        startLocalPosition = transform.localPosition;
        normalizedDirection = transform.right.normalized;

    }

    private void Update()
    {
        float offset = Mathf.Sin(Time.time * speed) * moveDistance;
        transform.localPosition = startLocalPosition + (Vector3)(normalizedDirection * offset);
    }
}
