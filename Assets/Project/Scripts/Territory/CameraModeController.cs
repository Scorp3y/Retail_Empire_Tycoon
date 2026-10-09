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
    [SerializeField] private Transform _moveRoot;
    [SerializeField] private MonoBehaviour _userCameraInput;
    [SerializeField] private RetailEmpireTycoon.Territory.TerritoryPlotLayout _plotLayout;

    [Header("Purchase Pose")]
    [SerializeField] private CameraPose _purchasePose;

    [Header("Tween")]
    [SerializeField] private float _moveDuration = 0.55f;
    [SerializeField] private AnimationCurve _ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine _routine;
    private CameraPose _savedPose;
    private bool _locked;

    public bool IsLocked => _locked;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera != null && _moveRoot == null) _moveRoot = _camera.transform;
    }

    private void Reset()
    {
        _camera = Camera.main;
        if (_camera != null)
            _moveRoot = _camera.transform; 
    }

    private Transform MoveT => _moveRoot != null ? _moveRoot : _camera.transform;

    public void EnterPurchaseMode()
    {

        if (_routine != null) StopCoroutine(_routine);

        SaveCurrentPose();
        SetUserInputEnabled(false);
        _locked = true;
        _routine = StartCoroutine(AnimateTo(AvailableLandPose()));
    }

    private CameraPose AvailableLandPose()
    {
        if (_plotLayout == null || _plotLayout.progression == null) return _purchasePose;
        bool found = false;
        Rect area = default;
        foreach (var plot in _plotLayout.plots)
        {
            if (!_plotLayout.progression.IsTerritoryAvailable(plot.id)) continue;
            if (!found) { area = plot.bounds; found = true; }
            else area = Rect.MinMaxRect(Mathf.Min(area.xMin,plot.bounds.xMin),Mathf.Min(area.yMin,plot.bounds.yMin),Mathf.Max(area.xMax,plot.bounds.xMax),Mathf.Max(area.yMax,plot.bounds.yMax));
        }
        if (!found) return _purchasePose;
        // Frame today's available plots, not the entire map: early purchases must remain easy to see and click.
        float halfHeight = Mathf.Tan(_purchasePose.Fov * Mathf.Deg2Rad * .5f);
        float aspect = _camera != null ? Mathf.Max(.6f,_camera.aspect) : 1.77f;
        float distance = Mathf.Max(area.width / (2 * halfHeight * aspect), area.height * .9063f / (2 * halfHeight)) * 1.35f + 4;
        var rotation = Quaternion.Euler(65,0,0);
        var target = new Vector3(area.center.x,0,area.center.y);
        return new CameraPose {Position=target-rotation*Vector3.forward*distance,Euler=rotation.eulerAngles,Fov=_purchasePose.Fov};
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
        var t = MoveT;
        _savedPose = new CameraPose
        {
            Position = t.position,
            Euler = t.rotation.eulerAngles,
            Fov = _camera != null ? _camera.fieldOfView : 60f
        };
    }

    private IEnumerator AnimateTo(CameraPose target)
    {
        var t = MoveT;

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

            if (_camera != null)
                _camera.fieldOfView = Mathf.LerpUnclamped(startFov, endFov, e);

            yield return null;
        }

        t.position = endPos;
        t.rotation = endRot;
        if (_camera != null) _camera.fieldOfView = endFov;

        _routine = null;
    }

    private void SetUserInputEnabled(bool enabled)
    {
        if (_userCameraInput != null)
            _userCameraInput.enabled = enabled;
    }
}
