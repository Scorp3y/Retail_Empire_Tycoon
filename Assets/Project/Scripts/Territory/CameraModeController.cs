using System.Collections;
using UnityEngine;

public sealed class CameraModeController : MonoBehaviour
{
    [System.Serializable]
    public struct CameraPose
    {
        public Vector3 Position;
        public Vector3 Euler;
        public float Fov;
    }

    [Header("References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private MonoBehaviour _userCameraInput;

    [Header("Purchase Pose")]
    [SerializeField] private CameraPose _purchasePose;

    [Header("Tween")]
    [SerializeField] private float _moveDuration = 0.55f;
    [SerializeField] private AnimationCurve _ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine _routine;
    private CameraPose _savedPose;
    private bool _locked;

    public bool IsLocked => _locked;

    private void Reset()
    {
        _camera = Camera.main;
    }

    public void EnterPurchaseMode()
    {
        if (_routine != null) StopCoroutine(_routine);

        SaveCurrentPose();
        SetUserInputEnabled(false);
        _locked = true;

        _routine = StartCoroutine(AnimateTo(_purchasePose));
    }

    public void ExitPurchaseMode()
    {
        if (_routine != null) StopCoroutine(_routine);

        _routine = StartCoroutine(ExitRoutine());
    }

    private IEnumerator ExitRoutine()
    {
        yield return AnimateTo(_savedPose);
        SetUserInputEnabled(true);
        _locked = false;
        _routine = null;
    }

    private void SaveCurrentPose()
    {
        var t = _camera.transform;
        _savedPose = new CameraPose
        {
            Position = t.position,
            Euler = t.rotation.eulerAngles,
            Fov = _camera.fieldOfView
        };
    }

    private IEnumerator AnimateTo(CameraPose target)
    {
        var t = _camera.transform;

        Vector3 startPos = t.position;
        Quaternion startRot = t.rotation;
        float startFov = _camera.fieldOfView;

        Vector3 endPos = target.Position;
        Quaternion endRot = Quaternion.Euler(target.Euler);
        float endFov = target.Fov;

        float time = 0f;
        while (time < _moveDuration)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / _moveDuration);
            float e = _ease.Evaluate(k);

            t.position = Vector3.LerpUnclamped(startPos, endPos, e);
            t.rotation = Quaternion.SlerpUnclamped(startRot, endRot, e);
            _camera.fieldOfView = Mathf.LerpUnclamped(startFov, endFov, e);

            yield return null;
        }

        t.position = endPos;
        t.rotation = endRot;
        _camera.fieldOfView = endFov;

        _routine = null;
    }

    private void SetUserInputEnabled(bool enabled)
    {
        if (_userCameraInput != null)
            _userCameraInput.enabled = enabled;
    }
}
