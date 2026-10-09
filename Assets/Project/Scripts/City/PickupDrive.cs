using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>Arcade pickup controls. Contacts are resolved by physics; no on-foot or interior mode.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PickupDrive : MonoBehaviour
    {
        public float topSpeed = 14;
        public float reverseSpeed = 5;
        public float acceleration = 5;
        public float braking = 10;
        public bool InputEnabled { get; set; } = true;
        public bool ReadKeyboard { get; set; } = true;
        private Rigidbody body;
        private float throttle, steering;
        private bool handbrake;
        public float Speed => body == null ? 0 : body.velocity.magnitude;
        public Rigidbody Body => body;
        private void Awake()
        {
            body = GetComponent<Rigidbody>(); body.mass = 1400;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.centerOfMass = new Vector3(0, .35f, 0);
        }
        private void Update()
        {
            if (InputEnabled && ReadKeyboard) SetInput(Input.GetAxisRaw("Vertical"), Input.GetAxisRaw("Horizontal"), Input.GetKey(KeyCode.Space));
        }
        public void SetInput(float forward, float turn, bool brake)
        { throttle = Mathf.Clamp(forward,-1,1); steering = Mathf.Clamp(turn,-1,1); handbrake = brake; }
        private void FixedUpdate()
        {
            float forwardSpeed = Vector3.Dot(body.velocity, transform.forward);
            float desired = InputEnabled && !handbrake ? throttle * (throttle >= 0 ? topSpeed : reverseSpeed) : 0;
            float rate = handbrake || throttle == 0 || Mathf.Sign(desired) != Mathf.Sign(forwardSpeed) ? braking : acceleration;
            float speed = Mathf.MoveTowards(forwardSpeed, desired, rate * Time.fixedDeltaTime);
            bool grounded=false;
            foreach(var hit in Physics.RaycastAll(transform.position+Vector3.up*1.5f,Vector3.down,3f,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform) && hit.transform!=transform && hit.normal.y>.5f) {grounded=true;break;}
            if (grounded)
            {
                Vector3 planar = transform.forward * speed;
                body.velocity = new Vector3(planar.x, body.velocity.y, planar.z);
                float turn = InputEnabled ? steering * Mathf.Clamp01(Mathf.Abs(speed) / 3) * Mathf.Sign(speed) * 55 * Time.fixedDeltaTime : 0;
                body.MoveRotation(body.rotation * Quaternion.Euler(0, turn, 0));
            }
        }
        public void Stop()
        { throttle = steering = 0; handbrake = true; body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
    }
}
