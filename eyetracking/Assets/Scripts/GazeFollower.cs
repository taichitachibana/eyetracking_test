using UnityEngine;

public class GazeFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _pointerObject;
    [SerializeField] private Transform _centerEyeAnchor;

    [Header("Settings")]
    [SerializeField] private float _distance = 1.0f;
    [SerializeField] private float _smoothSpeed = 15f;
    [SerializeField] private float _confidenceThreshold = 0.5f;
    [SerializeField, Min(0f)] private float _validTrackingDuration = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugRay = true;

    private bool _initialized = false;
    private float _validTrackingTimer = 0f;


    // GazeRawLogger から参照できる最新の信頼度
    public float LatestLeftConfidence { get; private set; }
    public float LatestRightConfidence { get; private set; }
    public bool IsTrackingReady { get; private set; }

    // ゼロクォータニオン（無効値）を弾くガード
    private bool IsValidQuat(OVRPlugin.Quatf q)
    {
        return (q.x != 0f || q.y != 0f || q.z != 0f || q.w != 0f);
    }

    private void LateUpdate()
    {
        // 毎フレームリセット（データ取得失敗時は0として扱う）
        LatestLeftConfidence = 0f;
        LatestRightConfidence = 0f;
        IsTrackingReady = false;

        if (_pointerObject == null || _centerEyeAnchor == null)
        {
            _validTrackingTimer = 0f;
            Debug.Log("参照がnullです");
            return;
        }

        OVRPlugin.EyeGazesState eyeGazesState = default;
        if (!OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref eyeGazesState))
        {
            _validTrackingTimer = 0f;
            Debug.Log("視線データ取得失敗");
            return;
        }

        var leftEye = eyeGazesState.EyeGazes[(int)OVRPlugin.Eye.Left];
        var rightEye = eyeGazesState.EyeGazes[(int)OVRPlugin.Eye.Right];

        float leftConf = leftEye.IsValid && IsValidQuat(leftEye.Pose.Orientation) ? leftEye.Confidence : 0f;
        float rightConf = rightEye.IsValid && IsValidQuat(rightEye.Pose.Orientation) ? rightEye.Confidence : 0f;

        // 外部公開（瞬き検出に使う）
        LatestLeftConfidence = leftConf;
        LatestRightConfidence = rightConf;

        Debug.Log($"Left Confidence: {leftConf}, Right Confidence: {rightConf}");

        bool hasValidTracking =
            leftConf >= _confidenceThreshold || rightConf >= _confidenceThreshold;

        if (!hasValidTracking)
        {
            _validTrackingTimer = 0f;
            return;
        }

        _validTrackingTimer += Time.deltaTime;
        IsTrackingReady = _validTrackingTimer >= _validTrackingDuration;
        if (!IsTrackingReady) return;

        // 信頼度が高い方を使う（両方高ければ平均）
        OVRPlugin.Posef eyePose;
        if (leftConf >= _confidenceThreshold && rightConf >= _confidenceThreshold)
        {
            if (!IsValidQuat(leftEye.Pose.Orientation) || !IsValidQuat(rightEye.Pose.Orientation)) return;

            var leftPose = leftEye.Pose.ToOVRPose().ToWorldSpacePose(Camera.main);
            var rightPose = rightEye.Pose.ToOVRPose().ToWorldSpacePose(Camera.main);
            eyePose = new OVRPlugin.Posef
            {
                Orientation = OVRPlugin.Quatf.identity,
            };
            Vector3 pos = (leftPose.position + rightPose.position) * 0.5f;
            Quaternion rot = Quaternion.Slerp(leftPose.orientation, rightPose.orientation, 0.5f);
            Vector3 gazeDir = rot * Vector3.forward;
            Vector3 targetPosition = pos + gazeDir * _distance;

            Debug.Log($"eyePos: {pos}, gazeDir: {gazeDir}, target: {targetPosition}");

            if (!_initialized)
            {
                _pointerObject.position = targetPosition;
                _initialized = true;
                return;
            }

            _pointerObject.position = Vector3.Lerp(
                _pointerObject.position,
                targetPosition,
                Time.deltaTime * _smoothSpeed
            );

            if (_showDebugRay)
                Debug.DrawRay(pos, gazeDir * _distance, Color.cyan);
        }
        else
        {
            var selectedEye = (leftConf >= _confidenceThreshold ? leftEye : rightEye);

            if (!IsValidQuat(selectedEye.Pose.Orientation)) return;

            var pose = selectedEye.Pose.ToOVRPose().ToWorldSpacePose(Camera.main);
            Vector3 gazeDir = pose.orientation * Vector3.forward;
            Vector3 targetPosition = pose.position + gazeDir * _distance;

            if (!_initialized)
            {
                _pointerObject.position = targetPosition;
                _initialized = true;
                return;
            }

            _pointerObject.position = Vector3.Lerp(
                _pointerObject.position,
                targetPosition,
                Time.deltaTime * _smoothSpeed
            );
        }
    }
}
