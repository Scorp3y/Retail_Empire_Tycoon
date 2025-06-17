using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AvatarSpawn : MonoBehaviour
{
    public GameObject npcPrefab;
    public Transform[] waypoints;
    public Transform spawnPoint;
    public float spawnInterval = 5f;

    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnAvatar();
            timer = 0f;
        }
    }

    void SpawnAvatar()
    {
        GameObject npc = Instantiate(npcPrefab, spawnPoint.position, spawnPoint.rotation);
        Avatar walk = npc.GetComponent<Avatar>();
        walk.waypoints = waypoints;
    }
}
