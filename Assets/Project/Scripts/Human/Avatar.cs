using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Avatar : MonoBehaviour
{
    public Transform[] waypoints;
    public float speed = 1f;
    private int currentWaypointIndex = 0;
    private Animator animator;
    private RetailEmpireTycoon.StoreOperations.StoreNavigation navigation;
    private RetailEmpireTycoon.StoreOperations.ShopCharacter movement;
    [SerializeField] private RetailEmpireTycoon.StoreOperations.StoreOperations shop;
    public void InitializeNavigation(RetailEmpireTycoon.StoreOperations.StoreNavigation value)
    {
        navigation = value;
        if (navigation != null)
        {
            movement = GetComponent<RetailEmpireTycoon.StoreOperations.ShopCharacter>() ?? gameObject.AddComponent<RetailEmpireTycoon.StoreOperations.ShopCharacter>();
            movement.Initialize(navigation, speed, false);
        }
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navigation != null && agent != null) agent.enabled = false;
    }


    void Start()
    {
        animator = GetComponent<Animator>();
        if (navigation == null && shop != null) InitializeNavigation(shop.Navigation);
    }


    void Update()
    {
        if (waypoints == null || currentWaypointIndex >= waypoints.Length) return;
        if (navigation != null) { AdvanceRoute(); return; }

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

    private void AdvanceRoute()
    {
        var waypoint = waypoints[currentWaypointIndex];
        if (waypoint == null) { movement.Stop(); currentWaypointIndex++; return; }
        if (!movement.HasDestination) movement.MoveTo(waypoint.position);
        movement.Advance(Time.deltaTime);
        if (movement.Arrived)
        {
            movement.Stop(); currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Length) Destroy(gameObject);
        }
    }

}
