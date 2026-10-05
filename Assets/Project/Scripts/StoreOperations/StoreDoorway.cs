using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.StoreOperations
{
    /// <summary>Opens the authored door leaves as an actor approaches; frame and collision opening remain fixed.</summary>
    public sealed class StoreDoorway : MonoBehaviour
    {
        private readonly List<Transform> _leaves = new List<Transform>();
        private readonly List<Quaternion> _closed = new List<Quaternion>();
        private float _passageUntil, _opening;
        private void Awake()
        {
            foreach (var leaf in GetComponentsInChildren<Transform>(true))
            {
                if (leaf.name != "door-left" && leaf.name != "door-right") continue;
                _leaves.Add(leaf); _closed.Add(leaf.localRotation);
            }
            if (_leaves.Count > 0)
            {
                var animator = GetComponent<Animator>();
                if (animator != null) animator.enabled = false;
            }
        }
        public void RequestPassage() { _passageUntil = Time.time + 1; }
        private void Update()
        {
            _opening = Mathf.MoveTowards(_opening, Time.time < _passageUntil ? 1 : 0, Time.deltaTime * 3);
            for (int i = 0; i < _leaves.Count; i++)
                if (_leaves[i] != null) _leaves[i].localRotation = _closed[i] * Quaternion.Euler(0, (_leaves[i].name == "door-left" ? -80 : 80) * _opening, 0);
        }
    }
}
