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

    [Header("Debug")]
    [SerializeField] private bool _showDebugRay = true;

    private bool _initialized = false;

    private void LateUpdate()
    {
        if (_pointerObject == null || _centerEyeAnchor == null)
        {
            Debug.Log("参照がnullです");
            return;
        }

        OVRPlugin.EyeGazesState eyeGazesState = default;
        if (!OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref eyeGazesState))
        {
            Debug.Log("視線データ取得失敗");
            return;
        }

        var leftEye = eyeGazesState.EyeGazes[(int)OVRPlugin.Eye.Left];
        var rightEye = eyeGazesState.EyeGazes[(int)OVRPlugin.Eye.Right];

        float leftConf = leftEye.IsValid ? leftEye.Confidence : 0f;
        float rightConf = rightEye.IsValid ? rightEye.Confidence : 0f;

        Debug.Log($"Left Confidence: {leftConf}, Right Confidence: {rightConf}");

        if (leftConf < _confidenceThreshold && rightConf < _confidenceThreshold) return;

        // 信頼度が高い方を使う（両方高ければ平均）
        OVRPlugin.Posef eyePose;
        if (leftConf >= _confidenceThreshold && rightConf >= _confidenceThreshold)
        {
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
            var pose = (leftConf >= _confidenceThreshold ? leftEye : rightEye)
                       .Pose.ToOVRPose().ToWorldSpacePose(Camera.main);
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