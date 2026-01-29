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
    public MainDoor mainDoor;
    public List<Product> availableProducts;

    [Range(15f, 35f)]
    public float spawnInterval = 10f;

    void Start()
    {
        float randomStartDelay = Random.Range(0f, spawnInterval);
        InvokeRepeating(nameof(SpawnCustomer), randomStartDelay, spawnInterval);
    }

    void SpawnCustomer()
    {
        if (!NavMesh.SamplePosition(spawnPoint.position, out var hit, 1f, NavMesh.AllAreas))
            return;

        GameObject newCustomer = Instantiate(customerPrefab, hit.position, Quaternion.identity);
        Customer customer = newCustomer.GetComponent<Customer>();

        int productCount = Random.Range(1, 4);
        List<DesiredProduct> customerList = new List<DesiredProduct>();
        List<Product> copy = new List<Product>(availableProducts);

        for (int i = 0; i < productCount && copy.Count > 0; i++)
        {
            int index = Random.Range(0, copy.Count);
            Product chosen = copy[index];
            int quantity = Random.Range(1, 4);

            customerList.Add(new DesiredProduct(chosen.productName, quantity));
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
