using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class DesiredProduct
{
    public string productName;
    public int quantity;
    public DesiredProduct(string name, int qty)
    {
        productName = name;
        quantity = qty;
    }
}

public class Customer : MonoBehaviour
{
    public List<DesiredProduct> desiredProducts = new List<DesiredProduct>();
    public Transform doorPoint;
    public Transform cashRegisterPoint;
    public QueueManager queueManager;
    public Transform exitPoint;
    public MainDoor mainDoor;
    public Transform spawnPoint;

    public float waitTimeAtShelf = 2f;
    public float waitTimeAtCash = 5f;

    private NavMeshAgent agent;
    private int currentProductIndex = -1;
    private bool isShopping = true;
    private bool isProcessing = false;
    private int totalSpent = 0;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        GoToDoor();
    }

    void GoToDoor()
    {
        if (agent.isOnNavMesh)
        {
            agent.SetDestination(doorPoint.position);
            mainDoor?.RegisterCustomer(transform);
        }
    }

    void Update()
    {
        if (!agent.isOnNavMesh) return;

        if (!agent.pathPending && agent.remainingDistance < 0.5f && !isProcessing)
        {
            isProcessing = true;

            if (currentProductIndex == -1)
            {
                currentProductIndex = 0;
                GoToNextProduct();
                isProcessing = false;
            }
            else if (isShopping)
            {
                StartCoroutine(TakeProduct());
            }
            else if (queueManager.IsCustomerFirst(this) && isInQueuePosition)
            {
                StartCoroutine(PayAndLeave());
                queueManager.LeaveQueue(this);
            }
            else
            {
                isProcessing = false;
            }
        }

        if (!isInQueuePosition && !agent.pathPending && agent.remainingDistance < 0.5f && !isShopping)
        {
            isInQueuePosition = true;
        }

        GetComponent<Animator>()?.SetBool("isWalking", agent.velocity.magnitude > 0.1f);
    }

    void GoToNextProduct()
    {
        if (currentProductIndex >= desiredProducts.Count)
        {
            isShopping = false;
            queueManager.JoinQueue(this);
            return;
        }

        var desired = desiredProducts[currentProductIndex];

        var product = WarehouseManager.Instance.GetProduct(desired.productName);
        if (product == null || product.shelfPoint == null)
        {
            currentProductIndex++;
            isProcessing = false;
            GoToNextProduct();
            return;
        }

        agent.SetDestination(product.shelfPoint.position);
    }


    IEnumerator TakeProduct()
    {
        yield return new WaitForSeconds(waitTimeAtShelf);

        var desired = desiredProducts[currentProductIndex];
        var product = WarehouseManager.Instance.GetProduct(desired.productName);

        if (product == null)
        {
            currentProductIndex++;
            isProcessing = false;
            GoToNextProduct();
            yield break;
        }

        bool success = WarehouseManager.Instance.TryTakeProduct(desired.productName, desired.quantity);

        if (success)
        {
            totalSpent += product.sellPrice * desired.quantity;
        }

        currentProductIndex++;
        isProcessing = false;
        GoToNextProduct();
    }


    private bool isInQueuePosition = false;

    public void SetQueueDestination(Vector3 position)
    {
        if (agent.isOnNavMesh)
        {
            agent.SetDestination(position);
            isInQueuePosition = false; 
        }
    }

    IEnumerator PayAndLeave()
    {     
        yield return new WaitForSeconds(waitTimeAtCash);

        if (totalSpent > 0)
            GameManager.Instance.AddMoney(totalSpent);
        else if (queueManager.IsCustomerFirst(this) && agent.remainingDistance < 0.5f)
        {
            StartCoroutine(PayAndLeave());
            queueManager.LeaveQueue(this);
        }

        mainDoor?.UnregisterCustomer(transform);

        agent.SetDestination(exitPoint.position);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance < 0.5f);
        agent.SetDestination(spawnPoint.position);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance < 0.5f);
        Destroy(gameObject);
    }
}
