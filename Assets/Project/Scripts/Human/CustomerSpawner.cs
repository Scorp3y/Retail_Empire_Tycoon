using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CustomerSpawner : MonoBehaviour
{
    public QueueManager queueManager;

    public GameObject customerPrefab;
    public Transform spawnPoint;
    public Transform doorPoint;
    public Transform cashRegister;
    public List<Transform> queuePoints;
    public Transform exitPoint;
    public List<ProductData> availableProducts;
    public MainDoor mainDoor;


    [Range(15f, 35f)]
    public float spawnInterval = 10f;

    void Start()
    {
        float randomStartDelay = Random.Range(0f, spawnInterval);
        InvokeRepeating(nameof(SpawnCustomer), randomStartDelay, spawnInterval);
    }

    void SpawnCustomer()
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(spawnPoint.position, out hit, 1f, NavMesh.AllAreas))
        {
            GameObject newCustomer = Instantiate(customerPrefab, hit.position, Quaternion.identity);
            Customer customer = newCustomer.GetComponent<Customer>();

            int productCount = Random.Range(1, 4);
            List<DesiredProduct> customerList = new List<DesiredProduct>();
            List<ProductData> copy = new List<ProductData>(availableProducts);

            for (int i = 0; i < productCount && copy.Count > 0; i++)
            {
                int index = Random.Range(0, copy.Count);
                ProductData chosen = copy[index];
                int quantity = Random.Range(1, 4);
                customerList.Add(new DesiredProduct(chosen, quantity));
                copy.RemoveAt(index);
            }

            customer.desiredProducts = customerList;
            customer.doorPoint = doorPoint;
            customer.cashRegisterPoint = cashRegister;
            customer.exitPoint = exitPoint;
            customer.mainDoor = mainDoor;
            customer.queueManager = queueManager;
            customer.spawnPoint = spawnPoint;
        }
       
    }
}
