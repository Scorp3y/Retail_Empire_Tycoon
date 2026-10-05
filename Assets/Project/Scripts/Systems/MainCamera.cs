using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Ground-focused orbit camera. Disabling input preserves its pose for modal windows.</summary>
[RequireComponent(typeof(Camera))]
public sealed class MainCamera : MonoBehaviour
{
    [Header("Focus bounds (world space)")]
    public float minX = -20f, maxX = 20f, minZ = -20f, maxZ = 10f;
    public float minY = 1.5f, maxY = 18f;
    public float minZoomDistance = 4f, maxZoomDistance = 25f;
    public Vector3 pivot;

    [Header("Controls")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 8f;
    [SerializeField, Min(0.1f)] private float zoomSpeed = 2f;
    [SerializeField, Min(0.01f)] private float rotationSensitivity = 0.25f;
    [SerializeField, Min(1f)] private float smoothing = 14f;
    [SerializeField, Range(15f, 70f)] private float minPitch = 30f;
    [SerializeField, Range(50f, 89f)] private float maxPitch = 80f;
    [SerializeField] private float groundHeight;
    [SerializeField] private RetailEmpireTycoon.StoreOperations.GameplayControls gameplayControls;

    private Camera _camera;
    private Vector3 _focus, _targetFocus, _homePosition;
    private Quaternion _homeRotation;
    private float _yaw, _pitch, _distance, _targetYaw, _targetPitch, _targetDistance;
    private float _perspectivePitch;
    private bool _topView, _panning, _orbiting;
    private Vector2 _lastPointer;
    public Vector3 FocusPoint => _focus;
    public bool IsTopView => _topView;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _homePosition = transform.position;
        _homeRotation = transform.rotation;
        SynchronizePose();
    }

    private void OnEnable()
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        // Purchase-mode animation and modal windows can move this transform.
        SynchronizePose();
    }

    private void OnDisable() { _panning = _orbiting = false; }

    private void Update()
    {
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        Vector2 pointer = Input.mousePosition;
        if (Input.GetMouseButtonUp(1)) _panning = false;
        if (Input.GetMouseButtonUp(2)) _orbiting = false;
        if (!overUI)
        {
            if (Input.GetMouseButtonDown(1)) { _panning = true; _lastPointer = pointer; }
            if (Input.GetMouseButtonDown(2)) { _orbiting = true; _lastPointer = pointer; }
            if (gameplayControls == null && Input.GetKeyDown(KeyCode.Home)) ResetView();
            if (gameplayControls == null && Input.GetKeyDown(KeyCode.V)) ToggleTopView();
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f) ZoomAtScreenPoint(scroll, pointer);
            float horizontal = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float vertical = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            Vector3 localMove = Vector3.ClampMagnitude(new Vector3(horizontal, 0f, vertical), 1f);
            float speed = moveSpeed * 15f * Mathf.Clamp(_targetDistance / 10f, 0.35f, 2.5f);
            if (Input.GetKey(KeyCode.LeftShift)) speed *= 2f;
            if (localMove.sqrMagnitude > 0f)
                Pan(Quaternion.Euler(0f, _targetYaw, 0f) * localMove * speed * Time.unscaledDeltaTime);
        }
        if (_orbiting && Input.GetMouseButton(2))
            Orbit((pointer.x - _lastPointer.x) * rotationSensitivity, -(pointer.y - _lastPointer.y) * rotationSensitivity);
        else if (_panning && Input.GetMouseButton(1)
            && TryGetGroundPoint(_lastPointer, out var previous) && TryGetGroundPoint(pointer, out var current))
            Pan(previous - current);
        _lastPointer = pointer;
    }

    private void LateUpdate()
    {
        Advance(Time.unscaledDeltaTime);
    }

    /// <summary>Apply a frame of smoothing without reading input; also usable by scripted camera motion.</summary>
    public void Advance(float unscaledDeltaTime)
    {
        float blend = 1f - Mathf.Exp(-smoothing * Mathf.Max(0f, unscaledDeltaTime));
        _focus = Vector3.Lerp(_focus, _targetFocus, blend);
        _yaw = Mathf.LerpAngle(_yaw, _targetYaw, blend);
        _pitch = Mathf.Lerp(_pitch, _targetPitch, blend);
        _distance = Mathf.Lerp(_distance, _targetDistance, blend);
        _distance = LimitDistance(_distance, _pitch);
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * _distance, rotation);
        pivot = _focus;
    }

    public void Pan(Vector3 worldDelta) { _targetFocus = ClampFocus(_targetFocus + worldDelta); }

    public void Orbit(float yawDelta, float pitchDelta)
    {
        _targetYaw = Mathf.Repeat(_targetYaw + yawDelta, 360f);
        if (Mathf.Abs(pitchDelta) > 0.001f) _topView = false;
        _targetPitch = _topView ? 89f : Mathf.Clamp(_targetPitch + pitchDelta, minPitch, maxPitch);
        _targetDistance = LimitDistance(_targetDistance, _targetPitch);
    }

    /// <summary>Zoom on the ground point under the cursor, not the world origin.</summary>
    public void ZoomAtScreenPoint(float scroll, Vector2 screenPoint)
    {
        float next = LimitDistance(_targetDistance * Mathf.Exp(-scroll * zoomSpeed), _targetPitch);
        if (TryGetGroundPoint(screenPoint, out var anchor) && _targetDistance > 0.001f)
            _targetFocus = ClampFocus(anchor + (_targetFocus - anchor) * (next / _targetDistance));
        _targetDistance = next;
    }

    public void ToggleTopView()
    {
        if (!_topView) _perspectivePitch = _targetPitch;
        _topView = !_topView;
        _targetPitch = _topView ? 89f : Mathf.Clamp(_perspectivePitch, minPitch, maxPitch);
        _targetDistance = LimitDistance(_targetDistance, _targetPitch);
    }

    public void ResetView()
    {
        var plane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
        var ray = new Ray(_homePosition, _homeRotation * Vector3.forward);
        _targetFocus = plane.Raycast(ray, out float length) ? ClampFocus(ray.GetPoint(length)) : ClampFocus(pivot);
        _targetYaw = _homeRotation.eulerAngles.y;
        _targetPitch = Mathf.Clamp(_homeRotation.eulerAngles.x, minPitch, maxPitch);
        _targetDistance = LimitDistance(Vector3.Distance(_homePosition, _targetFocus), _targetPitch);
        _topView = false;
    }

    public bool TryGetGroundPoint(Vector2 screenPoint, out Vector3 point)
    {
        point = default;
        if (_camera == null) return false;
        var plane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
        Ray ray = _camera.ScreenPointToRay(screenPoint);
        if (!plane.Raycast(ray, out float length) || length > 2000f) return false;
        point = ray.GetPoint(length);
        return true;
    }

    private Vector3 ClampFocus(Vector3 focus)
        => new Vector3(Mathf.Clamp(focus.x, minX, maxX), groundHeight, Mathf.Clamp(focus.z, minZ, maxZ));

    private float LimitDistance(float distance, float pitch)
    {
        float heightRatio = Mathf.Max(0.1f, Mathf.Sin(pitch * Mathf.Deg2Rad));
        float lower = Mathf.Max(minZoomDistance, (minY - groundHeight) / heightRatio);
        float upper = Mathf.Max(lower, Mathf.Min(maxZoomDistance, (maxY - groundHeight) / heightRatio));
        return Mathf.Clamp(distance, lower, upper);
    }

    private void SynchronizePose()
    {
        _panning = _orbiting = false;
        var plane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
        var ray = new Ray(transform.position, transform.forward);
        _focus = _targetFocus = plane.Raycast(ray, out float length) ? ray.GetPoint(length) : ClampFocus(pivot);
        _yaw = _targetYaw = transform.eulerAngles.y;
        _pitch = _targetPitch = transform.eulerAngles.x;
        _distance = _targetDistance = Vector3.Distance(transform.position, _focus);
        _topView = _pitch > maxPitch;
        _perspectivePitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
    }
}
