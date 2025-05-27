using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainDoor : MonoBehaviour
{
    public string openTriggerName = "open";
    public string closeTriggerName = "close";
    public float activationDistance = 2f;

    private Transform customer;
    private Animator animator;
    private bool isOpen = false;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (customer == null) return;

        float distance = Vector3.Distance(transform.position, customer.position);

        if (!isOpen && distance < activationDistance)
        {
            animator.SetTrigger(openTriggerName);
            isOpen = true;
        }
        else if (isOpen && distance > activationDistance + 1f)
        {
            animator.SetTrigger(closeTriggerName);
            isOpen = false;
        }
    }

    public void SetCustomer(Transform newCustomer)
    {
        customer = newCustomer;
    }
}
