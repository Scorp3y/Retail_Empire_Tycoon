using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainDoor : MonoBehaviour
{
    public float activationDistance = 2f;
    private Animator animator;
    private List<Transform> nearbyCustomers = new List<Transform>();
    private bool isPlaying = false;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (isPlaying) return;
        nearbyCustomers.RemoveAll(c => c == null);

        foreach (Transform customer in nearbyCustomers)
        {
            if (Vector3.Distance(transform.position, customer.position) < activationDistance)
            {
                animator.Play("OpenAndClose", 0, 0f);
                isPlaying = true;
                StartCoroutine(ResetAfterAnimation());
                break;
            }
        }
    }

    IEnumerator ResetAfterAnimation()
    {
        float duration = animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(duration);
        isPlaying = false;
    }

    public void RegisterCustomer(Transform customer)
    {
        if (!nearbyCustomers.Contains(customer))
            nearbyCustomers.Add(customer);
    }

    public void UnregisterCustomer(Transform customer)
    {
        nearbyCustomers.Remove(customer);
    }
}
