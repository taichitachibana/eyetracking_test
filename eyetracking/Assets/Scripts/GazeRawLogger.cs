using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class GazeRawLogger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _gazeTarget;
    [SerializeField] private GazeFollower _gazeFollower;
    [SerializeField] private OVRFaceExpressions _faceExpressions;

    [Header("Trial Settings")]
    [SerializeField] private float _trialDuration = 1.15f;
    [SerializeField] private int _trialsPerBlock = 10;

    [Header("Blink Settings")]
    [SerializeField, Range(0f, 1f)]
    private float _blinkThreshold = 0.5f;

    private struct BlinkSample
    {
        public int Value;
        public string Status;
        public float LeftClosed;
        public float RightClosed;
        public float Mean;

        public BlinkSample(
            int value,
            string status,
            float leftClosed,
            float rightClosed,
            float mean)
        {
            Value = value;
            Status = status;
            LeftClosed = leftClosed;
            RightClosed = rightClosed;
            Mean = mean;
        }
    }

    private const float SampleInterval = 1f / 60f;
    private float _sampleTimer = 0f;

    private bool _isRecording = false;
    private float _trialTimer = 0f;
    private int _currentTrial = 0;
    private int _blockNumber = 0;
    private int _frameIndex = 0;

    private Camera _cam;
    private StreamWriter _rawWriter;

    private Quaternion _headRefRotation = Quaternion.identity;
    private Vector3 _headRefPosition = Vector3.zero;

    private readonly List<string> _trialBuffer = new();

    private string _lastBlinkDiagnosticStatus;

    private void Awake()
    {
        _cam = Camera.main;

    }

    private void OnEnable()
    {
        ExperimentEventLogger.RecordingStarted += BeginRecording;
        ExperimentEventLogger.RecordingStopping += EndRecording;

        if (ExperimentEventLogger.IsRecording)
            BeginRecording();
    }

    private void OnDisable()
    {
        ExperimentEventLogger.RecordingStarted -= BeginRecording;
        ExperimentEventLogger.RecordingStopping -= EndRecording;

        if (_isRecording)
            EndRecording();
    }

    private void OnDestroy()
    {
        FlushBuffer();
        _rawWriter?.Flush();
        _rawWriter?.Close();
    }

    private void Update()
    {
        if (!_isRecording)
            return;

        _trialTimer += Time.deltaTime;

        if (_trialTimer >= _trialDuration)
        {
            _trialTimer -= _trialDuration;

            FlushBuffer();

            _currentTrial++;

            if (_currentTrial >= _trialsPerBlock)
            {
                _blockNumber++;
                _currentTrial = 0;
            }
        }
    }

    private void LateUpdate()
    {
        if (!_isRecording) return;

        _sampleTimer += Time.deltaTime;

        if (_sampleTimer >= SampleInterval)
        {
            _sampleTimer -= SampleInterval;
            SampleFrame();
        }
    }

    private void BeginRecording()
    {
        if (_isRecording) return;

        InitRawCSV();
        _isRecording = true;

        _trialTimer = 0f;
        _sampleTimer = 0f;
        _currentTrial = 0;
        _blockNumber = 0;
        _frameIndex = 0;

        _headRefRotation =
            _cam != null ? _cam.transform.rotation : Quaternion.identity;

        _headRefPosition =
            _cam != null ? _cam.transform.position : Vector3.zero;

        _trialBuffer.Clear();
        _lastBlinkDiagnosticStatus = null;
    }

    private void EndRecording()
    {
        if (!_isRecording) return;

        FlushBuffer();
        _isRecording = false;
        _rawWriter?.Flush();
        _rawWriter?.Close();
        _rawWriter = null;
    }

    private BlinkSample GetBlinkSample()
    {
        if (_faceExpressions == null)
        {
            return new BlinkSample(
                0,
                "FaceExpressionsNull",
                0f,
                0f,
                0f);
        }

        float leftClosed = 0f;
        float rightClosed = 0f;

        bool leftValid =
            _faceExpressions.TryGetFaceExpressionWeight(
                OVRFaceExpressions.FaceExpression.EyesClosedL,
                out leftClosed);

        bool rightValid =
            _faceExpressions.TryGetFaceExpressionWeight(
                OVRFaceExpressions.FaceExpression.EyesClosedR,
                out rightClosed);

        if (!leftValid || !rightValid)
        {
            string status = !leftValid && !rightValid
                ? "ReadFailed_LR"
                : (!leftValid ? "ReadFailed_L" : "ReadFailed_R");

            return new BlinkSample(
                0,
                status,
                leftClosed,
                rightClosed,
                0f);
        }

        float eyeClosureMean =
            (leftClosed + rightClosed) * 0.5f;

        bool isBlink = eyeClosureMean >= _blinkThreshold;
        return new BlinkSample(
            isBlink ? 1 : 0,
            isBlink ? "Detected" : "BelowThreshold",
            leftClosed,
            rightClosed,
            eyeClosureMean);
    }

    private void LogBlinkDiagnostic(BlinkSample sample)
    {
        if (sample.Status == _lastBlinkDiagnosticStatus)
            return;

        _lastBlinkDiagnosticStatus = sample.Status;
        string message =
            $"Blink status={sample.Status}, " +
            $"L={sample.LeftClosed:F3}, " +
            $"R={sample.RightClosed:F3}, " +
            $"Mean={sample.Mean:F3}, " +
            $"Threshold={_blinkThreshold:F3}";

        if (sample.Status.StartsWith("ReadFailed") ||
            sample.Status == "FaceExpressionsNull")
        {
            Debug.LogWarning(message);
        }
        else
        {
            Debug.Log(message);
        }
    }

    private void SampleFrame()
    {
        if (_cam == null) return;

        BlinkSample blinkSample = GetBlinkSample();
        LogBlinkDiagnostic(blinkSample);

        Vector3 headPos = _cam.transform.position;
        Quaternion headRot = _cam.transform.rotation;
        Vector3 headDelta = headPos - _headRefPosition;

        float leftConf =
            _gazeFollower != null
            ? _gazeFollower.LatestLeftConfidence
            : 0f;

        float rightConf =
            _gazeFollower != null
            ? _gazeFollower.LatestRightConfidence
            : 0f;

        OVRPlugin.EyeGazesState eyeState = default;

        bool stateValid =
            OVRPlugin.GetEyeGazesState(
                OVRPlugin.Step.Render,
                -1,
                ref eyeState);

        var left =
            stateValid
            ? eyeState.EyeGazes[(int)OVRPlugin.Eye.Left]
            : default;

        var right =
            stateValid
            ? eyeState.EyeGazes[(int)OVRPlugin.Eye.Right]
            : default;

        Vector3 leftDirLocal =
            left.IsValid
            ? left.Pose.ToOVRPose().orientation * Vector3.forward
            : Vector3.forward;

        Vector3 rightDirLocal =
            right.IsValid
            ? right.Pose.ToOVRPose().orientation * Vector3.forward
            : Vector3.forward;

        Vector3 leftDirWorld = headRot * leftDirLocal;
        Vector3 rightDirWorld = headRot * rightDirLocal;

        Vector3 combinedDir =
            (
                (leftConf > 0f ? leftDirWorld : Vector3.zero) +
                (rightConf > 0f ? rightDirWorld : Vector3.zero)
            ).normalized;

        if (combinedDir == Vector3.zero)
            combinedDir = headRot * Vector3.forward;

        Quaternion headDeltaRot =
            Quaternion.Inverse(_headRefRotation) * headRot;

        Vector3 correctedDir =
            Quaternion.Inverse(headDeltaRot) * combinedDir;

        Vector3 vpCorrected =
            _cam.WorldToViewportPoint(headPos + correctedDir);

        float gazeCorrX = vpCorrected.x - 0.5f;
        float gazeCorrY = vpCorrected.y - 0.5f;

        string absTime =
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        var sb = new StringBuilder();

        sb.Append($"{_frameIndex},{_blockNumber},{_currentTrial},{absTime},");
        sb.Append($"{headDelta.x:F4},{headDelta.y:F4},{headDelta.z:F4},");
        sb.Append($"{gazeCorrX:F4},{gazeCorrY:F4},");
        sb.Append($"{blinkSample.Value},");
        sb.Append($"{blinkSample.Status},");
        sb.Append($"{blinkSample.LeftClosed:F4},{blinkSample.RightClosed:F4},");
        sb.Append($"{blinkSample.Mean:F4}");

        _trialBuffer.Add(sb.ToString());

        _frameIndex++;
    }

    private void FlushBuffer()
    {
        if (_trialBuffer.Count == 0) return;

        foreach (var line in _trialBuffer)
            _rawWriter.WriteLine(line);

        _rawWriter.Flush();
        _trialBuffer.Clear();
    }

    private void InitRawCSV()
    {
        string rawCsvPath = Path.Combine(
            ExperimentEventLogger.GetDataDirectory(),
            $"{ExperimentEventLogger.RecordingFileStamp}_gaze_raw.csv");

        _rawWriter = new StreamWriter(
            rawCsvPath,
            append: false,
            encoding: Encoding.UTF8);

        _rawWriter.WriteLine(
            "FrameIndex,Block,Trial,AbsTime," +
            "HeadDeltaX,HeadDeltaY,HeadDeltaZ," +
            "GazeCorrX,GazeCorrY," +
            "IsBlink,BlinkStatus," +
            "LeftEyeClosure,RightEyeClosure,EyeClosureMean");

        _rawWriter.Flush();
    }
}
