using System.Collections.Generic;
using UnityEngine;

namespace RetailEmpireTycoon.StoreOperations
{
    public sealed class ShopCharacter : MonoBehaviour
    {
        private StoreNavigation _navigation;
        private List<Vector3> _path = new List<Vector3>();
        private int _waypoint;
        private Animator _animator;
        private bool _hasWalkingParameter;
        private float _walkingSpeed, _retry;
        private Vector3 _destination;
        public bool HasDestination { get; private set; }
        public bool Arrived { get; private set; } = true;
        public bool IsBlocked { get; private set; }
        public bool MoveTo(Vector3 target)
        {
            _destination = target; HasDestination = true; Arrived = false; _retry = 0;
            return RebuildRoute();
        }
        public void Stop()
        {
            _path.Clear(); _waypoint = 0; HasDestination = false; Arrived = true; IsBlocked = false;
            SetWalking(false);
        }
        public void Initialize(StoreNavigation navigation, float walkingSpeed = 0.65f, bool takeOverLegacy = true)
        {
            _navigation = navigation;
            _walkingSpeed = Mathf.Max(0.1f, walkingSpeed);
            if (takeOverLegacy)
                foreach (var avatar in GetComponentsInChildren<Avatar>()) avatar.enabled = false;
            foreach (var agent in GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>()) agent.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            _animator = GetComponentInChildren<Animator>();
            _hasWalkingParameter = false;
            if (_animator != null)
                foreach (var parameter in _animator.parameters)
                    if (parameter.name == "isWalking" && parameter.type == AnimatorControllerParameterType.Bool) _hasWalkingParameter = true;
        }
        private bool RebuildRoute()
        {
            _retry = .4f;
            if (_navigation == null || !_navigation.TryPath(transform.position, _destination, out var route, true))
            {
                _path.Clear(); _waypoint = 0; IsBlocked = true; SetWalking(false); return false;
            }
            _path = route; _waypoint = 0; IsBlocked = false;
            return true;
        }
        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0 || !HasDestination || Arrived) { SetWalking(false); return; }
            _retry -= deltaTime;
            _navigation?.NotifyMovement(transform.position);
            if (IsBlocked && (_retry > 0 || !RebuildRoute())) return;
            float distanceBudget = deltaTime * _walkingSpeed;
            // Do not lose one frame per cell; spend the remaining distance on the next waypoint.
            while (_waypoint < _path.Count)
            {
                Vector3 target = _path[_waypoint];
                if (!_navigation.IsWalkable(target, true) || !_navigation.CanTraverse(transform.position, target))
                {
                    IsBlocked = true; SetWalking(false);
                    if (_retry <= 0) RebuildRoute();
                    return;
                }
                Vector3 delta = target - transform.position; delta.y = 0;
                float distance = delta.magnitude;
                if (distance <= .001f) { transform.position = target; _waypoint++; continue; }
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta), deltaTime * 12);
                float step = Mathf.Min(distanceBudget, distance);
                transform.position = Vector3.MoveTowards(transform.position, target, step);
                distanceBudget -= step;
                if (step >= distance) _waypoint++;
                if (distanceBudget <= .00001f) break;
            }
            Arrived = _waypoint >= _path.Count;
            SetWalking(!Arrived && !IsBlocked);
        }
        private void SetWalking(bool walking)
        {
            if (_hasWalkingParameter && _animator != null) _animator.SetBool("isWalking", walking);
        }
    }
}
