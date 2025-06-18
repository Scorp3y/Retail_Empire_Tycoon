using System.Collections.Generic;
using UnityEngine;

public class QueueManager : MonoBehaviour
{
    public List<Transform> queuePoints;
    private List<Customer> customersInQueue = new List<Customer>();
    private Dictionary<Transform, Customer> pointAssignments = new Dictionary<Transform, Customer>();

    public void JoinQueue(Customer customer)
    {
        customersInQueue.Add(customer);
        UpdateQueuePositions();
    }

    public void LeaveQueue(Customer customer)
    {
        customersInQueue.Remove(customer);
        foreach (var kvp in pointAssignments)
        {
            if (kvp.Value == customer)
            {
                pointAssignments[kvp.Key] = null;
                break;
            }
        }

        UpdateQueuePositions();
    }

    private void UpdateQueuePositions()
    {
        pointAssignments.Clear();
        for (int i = 0; i < customersInQueue.Count; i++)
        {
            Customer customer = customersInQueue[i];

            if (i < queuePoints.Count)
            {
                Transform point = queuePoints[i];
                customer.SetQueueDestination(point.position);
                pointAssignments[point] = customer;
            }
        }
    }

    public bool IsCustomerFirst(Customer customer)
    {
        return customersInQueue.Count > 0 && customersInQueue[0] == customer;
    }
}
