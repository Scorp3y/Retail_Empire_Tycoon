using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    public GameObject carPrefab;
    public Transform[] waypoints;
    public Transform spawnPoint;
    public float spawnInterval = 5f;

    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnCar();
            timer = 0f;
        }
    }

    void SpawnCar()
    {
        GameObject car = Instantiate(carPrefab, spawnPoint.position, spawnPoint.rotation);
        Car controller = car.GetComponent<Car>();
        controller.waypoints = waypoints;
    }
}
