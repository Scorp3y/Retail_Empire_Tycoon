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
        private bool _hasBrowsingParameter;
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
            SetBrowsing(false);
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
            _hasBrowsingParameter = false;
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                foreach (var parameter in _animator.parameters)
                {
                    if (parameter.name == "isWalking" && parameter.type == AnimatorControllerParameterType.Bool) _hasWalkingParameter = true;
                    if (parameter.name == "isBrowsing" && parameter.type == AnimatorControllerParameterType.Bool) _hasBrowsingParameter = true;
                }
                if (_hasBrowsingParameter) _animator.Play("Idle", 0, Random.value);
            }
        }
        public void SetBrowsing(bool browsing)
        {
            if (_hasBrowsingParameter && _animator != null) _animator.SetBool("isBrowsing", browsing);
            GetComponentInChildren<StaffAppearance>()?.SetWorking(browsing);
        }
        public void FaceToward(Vector3 point, float seconds)
        {
            var direction = point - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > .00001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Mathf.Clamp01(seconds * 12));
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
            float timeBudget = deltaTime;
            // Do not lose one frame per cell; spend the remaining distance on the next waypoint.
            while (_waypoint < _path.Count)
            {
                Vector3 target = _path[_waypoint];
                if(!_navigation.TryUseTransport(this,transform.position,target)) {SetWalking(false);return;}
                if (!_navigation.IsWalkable(target, true) || !_navigation.CanTraverse(transform.position, target))
                {
                    IsBlocked = true; SetWalking(false);
                    if (_retry <= 0) RebuildRoute();
                    return;
                }
                Vector3 delta = target - transform.position;
                float distance = delta.magnitude;
                if (distance <= .001f) { transform.position = target; _waypoint++; continue; }
                Vector3 horizontal=delta; horizontal.y=0;
                if(horizontal.sqrMagnitude>.00001f)transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(horizontal), deltaTime * 12);
                float speed = _walkingSpeed * _navigation.TravelSpeedMultiplier(transform.position,target);
                float step = Mathf.Min(timeBudget * speed, distance);
                transform.position = Vector3.MoveTowards(transform.position, target, step);
                timeBudget -= step / speed;
                if (step >= distance) _waypoint++;
                if (timeBudget <= .00001f) break;
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
