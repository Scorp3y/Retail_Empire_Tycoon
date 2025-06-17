using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Avatar : MonoBehaviour
{
    public Transform[] waypoints;
    public float speed = 1f;
    private int currentWaypointIndex = 0;
    private Animator animator;


    void Start()
    {
        animator = GetComponent<Animator>();
    }


    void Update()
    {
        if (waypoints.Length == 0) return;

        Transform target = waypoints[currentWaypointIndex];
        Vector3 direction = (target.position - transform.position);

        if (direction.magnitude > 0.01f)
        {
            animator.SetBool("isWalking", true);
            Vector3 moveDir = direction.normalized;
            transform.position += moveDir * speed * Time.deltaTime;
            transform.LookAt(target);
        }
        else
        {
            animator.SetBool("isWalking", false);
        }

        if (Vector3.Distance(transform.position, target.position) < 0.5f)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Length)
            {
                Destroy(gameObject);
            }
        }
    }

}
