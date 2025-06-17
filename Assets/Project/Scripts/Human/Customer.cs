using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class DesiredProduct
{
    public ProductData productData;
    public int quantity;
    public DesiredProduct(ProductData data, int qty) { productData = data; quantity = qty; }
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
    public float waitTimeAtCash = 3f;

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
            else if (queueManager.IsCustomerFirst(this))
            {
                StartCoroutine(PayAndLeave());
                queueManager.LeaveQueue(this);
            }
            else
            {
                isProcessing = false;
            }
        }


        GetComponent<Animator>()?.SetBool("isWalking", agent.velocity.magnitude > 0.1f);
    }

    void GoToNextProduct()
    {
        if (currentProductIndex >= desiredProducts.Count)
        {
            isShopping = false;
            queueManager.JoinQueue(this);
        }
        else
        {
            var desired = desiredProducts[currentProductIndex];
            agent.SetDestination(desired.productData.shelfPosition.position);
        }
    }

    IEnumerator TakeProduct()
    {
        yield return new WaitForSeconds(waitTimeAtShelf);

        var desired = desiredProducts[currentProductIndex];
        bool success = WarehouseManager.Instance.TryTakeProduct(desired.productData.productName, desired.quantity);

        if (success)
        {
            totalSpent += desired.productData.price * desired.quantity;
            currentProductIndex++;
        }
        else
        {
            isShopping = false;
        }

        isProcessing = false;
        GoToNextProduct();
    }

    private bool isInQueue = false;
    public void SetQueueDestination(Vector3 position)
    {
        if (agent.isOnNavMesh)
            agent.SetDestination(position);
    }

    IEnumerator PayAndLeave()
    {
        yield return new WaitForSeconds(waitTimeAtCash);

        if (totalSpent > 0)
            GameManager.Instance.AddMoney(totalSpent);

        mainDoor?.UnregisterCustomer(transform);

        agent.SetDestination(exitPoint.position);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance < 0.5f);
        agent.SetDestination(spawnPoint.position);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance < 0.5f);
        Destroy(gameObject);
    }
}
