using System.Collections.Generic;
using UnityEngine;

public class QueueManager : MonoBehaviour
{
    public List<Transform> queuePoints;  
    private List<Customer> customersInQueue = new List<Customer>();

    public void JoinQueue(Customer customer)
    {
        customersInQueue.Add(customer);
        UpdateQueuePositions();
    }

    public void LeaveQueue(Customer customer)
    {
        customersInQueue.Remove(customer);
        UpdateQueuePositions();
    }

    private void UpdateQueuePositions()
    {
        for (int i = 0; i < customersInQueue.Count; i++)
        {
            if (i < queuePoints.Count)
                customersInQueue[i].SetQueueDestination(queuePoints[i].position);
        }
    }

    public bool IsCustomerFirst(Customer customer)
    {
        return customersInQueue.Count > 0 && customersInQueue[0] == customer;
    }
}
