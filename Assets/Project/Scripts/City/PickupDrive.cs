using UnityEngine;

namespace RetailEmpireTycoon.City
{
    /// <summary>
    /// Accessible four-wheel driving. Wheel contacts provide traction, suspension and chassis
    /// pitch; keyboard or scripted input supplies the same smoothed throttle and steering.
    /// </summary>
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
        [System.Serializable]
        public sealed class Wheel
        {
            public WheelCollider contact;
            public Transform visual;
            public bool front;
        }
        public Wheel[] wheels = System.Array.Empty<Wheel>();
        private float smoothThrottle, smoothSteering;
        public float CameraHeight { get; private set; } = 1.5f;
        private Wheel[] originalWheels;
        private GameObject selectedModel;
        private readonly System.Collections.Generic.Dictionary<GameObject, bool> originalChildren = new System.Collections.Generic.Dictionary<GameObject, bool>();
        private Vector3 originalBoxCenter, originalBoxSize;
        private BoxCollider chassis;
        public float SteeringAngle { get; private set; }
        public int GroundedWheels { get; private set; }
        public float Speed => body == null ? 0 : body.velocity.magnitude;
        public Rigidbody Body => body;
        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.mass = 1400;
            body.constraints = RigidbodyConstraints.None;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.centerOfMass = new Vector3(0, .48f, -.05f);
            body.angularDrag = 2.5f;
            body.drag = .04f;
            body.solverIterations = 12;
            body.solverVelocityIterations = 4;
            // Substeps are a runtime physics setting, not a serialized prefab property.
            foreach (var wheel in wheels)
                if (wheel.contact != null) wheel.contact.ConfigureVehicleSubsteps(5, 8, 12);
        }
        private void Update()
        {
            if (InputEnabled && ReadKeyboard)
                SetInput(Input.GetAxisRaw("Vertical"), Input.GetAxisRaw("Horizontal"), Input.GetKey(KeyCode.Space));
        }

        /// <summary>Normalized throttle/steering in [-1, 1]. Brake overrides motor torque.</summary>
        public void SetInput(float forward, float turn, bool brake)
        {
            throttle = float.IsNaN(forward) ? 0 : Mathf.Clamp(forward, -1, 1);
            steering = float.IsNaN(turn) ? 0 : Mathf.Clamp(turn, -1, 1);
            handbrake = brake;
        }

        private void FixedUpdate()
        {
            if (wheels.Length < 4) return;
            smoothThrottle = Mathf.MoveTowards(smoothThrottle, InputEnabled ? throttle : 0, 2.2f * Time.fixedDeltaTime);
            smoothSteering = Mathf.MoveTowards(smoothSteering, InputEnabled ? steering : 0, 2.6f * Time.fixedDeltaTime);
            float speed = Vector3.Dot(body.velocity, transform.forward);
            float limit = smoothThrottle >= 0 ? topSpeed : reverseSpeed;
            float desired = smoothThrottle * limit;
            bool reversing = speed * smoothThrottle < -.3f;
            float force = Mathf.Clamp((desired - speed) * 2, -acceleration, acceleration);
            SteeringAngle = smoothSteering * Mathf.Lerp(42, 15, Mathf.Clamp01(Mathf.Abs(speed) / topSpeed));
            GroundedWheels = 0;

            foreach (var wheel in wheels)
            {
                var contact = wheel.contact;
                if (contact == null) continue;
                contact.steerAngle = wheel.front ? SteeringAngle : 0;
                contact.motorTorque = !InputEnabled || handbrake || reversing || Mathf.Abs(smoothThrottle) < .01f
                    ? 0 : body.mass * force * contact.radius / wheels.Length;
                contact.brakeTorque = BrakeTorque(contact, reversing);
                if (contact.isGrounded) GroundedWheels++;
            }
        }

        private float BrakeTorque(WheelCollider contact, bool reversing)
        {
            if (!InputEnabled || handbrake) return body.mass * braking * contact.radius / wheels.Length;
            if (reversing) return 1600;
            return Mathf.Abs(smoothThrottle) < .01f ? 45 : 0;
        }

        private void LateUpdate()
        {
            foreach (var wheel in wheels)
            {
                if (wheel.contact == null || wheel.visual == null) continue;
                wheel.contact.GetWorldPose(out var position, out var rotation);
                wheel.visual.SetPositionAndRotation(position, rotation);
            }
        }

        /// <summary>Stops the chassis for parking/relocation and clears stale control input.</summary>
        public void Stop()
        {
            throttle = steering = smoothThrottle = smoothSteering = 0;
            handbrake = true;
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            foreach (var wheel in wheels)
            {
                if (wheel.contact == null) continue;
                wheel.contact.motorTorque = 0;
                wheel.contact.brakeTorque = 4000;
            }
        }

        /// <summary>Called only while stopped at the showroom or when entering town. Supports two or three axles.</summary>
        public void SetVehicle(PlayerVehicleDefinition definition)
        {
            Stop();
            if (originalWheels == null)
            {
                originalWheels = wheels; chassis = GetComponent<BoxCollider>();
                originalBoxCenter = chassis.center; originalBoxSize = chassis.size;
                foreach (Transform child in transform) originalChildren.Add(child.gameObject, child.gameObject.activeSelf);
            }
            if (selectedModel != null) { selectedModel.SetActive(false); Destroy(selectedModel); }
            foreach (var pair in originalChildren) if (pair.Key != null) pair.Key.SetActive(definition == null && pair.Value);
            if (definition == null)
            {
                wheels = originalWheels; chassis.center = originalBoxCenter; chassis.size = originalBoxSize;
                body.mass = 1400; body.centerOfMass = new Vector3(0,.48f,-.05f);
                topSpeed = 14; acceleration = 5; CameraHeight = 1.5f;
                return;
            }
            selectedModel = Instantiate(definition.model, transform);
            selectedModel.transform.localPosition = Vector3.zero; selectedModel.transform.localRotation = Quaternion.identity;
            var bindings = new System.Collections.Generic.List<Wheel>();
            var mounts = selectedModel.GetComponent<PlayerVehicleModel>().wheels;
            foreach (var mount in mounts)
            {
                var node = new GameObject("Suspension"); node.transform.SetParent(selectedModel.transform, false);
                node.transform.localPosition = mount.pivot.localPosition + Vector3.up * .12f;
                var contact = node.AddComponent<WheelCollider>();
                contact.radius = definition.wheelRadius; contact.mass = 25; contact.suspensionDistance = .24f;
                contact.forceAppPointDistance = .15f; contact.wheelDampingRate = 1.2f;
                contact.suspensionSpring = new JointSpring { spring = definition.mass * 120 / mounts.Length, damper = definition.mass * 16 / mounts.Length, targetPosition = .5f };
                var forward = contact.forwardFriction; forward.stiffness = 1.5f; contact.forwardFriction = forward;
                var side = contact.sidewaysFriction; side.stiffness = 1.65f; contact.sidewaysFriction = side;
                contact.ConfigureVehicleSubsteps(5,8,12);
                bindings.Add(new Wheel { contact = contact, visual = mount.pivot, front = mount.front });
            }
            wheels = bindings.ToArray(); body.mass = definition.mass;
            body.centerOfMass = new Vector3(0, Mathf.Min(.72f, definition.height * .28f), -.12f);
            chassis.center = new Vector3(0, (definition.height + .52f) * .5f, 0);
            chassis.size = new Vector3(definition.width - .12f, definition.height - .52f, definition.length - .12f);
            topSpeed = definition.topSpeed; acceleration = definition.acceleration; CameraHeight = definition.height * .55f;
            Stop();
        }
    }
}
