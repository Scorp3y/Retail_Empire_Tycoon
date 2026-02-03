using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CorgiGodRays
{
    public class DemoRotator : MonoBehaviour
    {
        public float speed = 10f;

        private void Update()
        {
            transform.RotateAround(transform.position, Vector3.up, Time.deltaTime * speed); 
        }
    }
}
