using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(-900)]
public class GazeSummaryLogger : MonoBehaviour
{
    [Header("Sampling")]
    [SerializeField, Min(0.1f)] private float _windowDuration = 0.5f;
    [SerializeField, Min(1f)] private float _sampleRateHz = 60f;
    [SerializeField, Range(0f, 1f)] private float _confidenceThreshold = 0.5f;

    private Camera _camera;
    private StreamWriter _writer;
    private bool _recording;
    private float _sampleTimer;
    private int _windowIndex;
    private double _windowStartElapsed;
    private Quaternion _headReferenceRotation = Quaternion.identity;

    private int _sampleCount;
    private int _validSampleCount;
    private double _meanX;
    private double _meanY;
    private double _m2X;
    private double _m2Y;
    private double _coMoment;

    private void Awake()
    {
        _camera = Camera.main;
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

        if (_recording)
            EndRecording();
    }

    private void OnDestroy()
    {
        _writer?.Flush();
        _writer?.Close();
        _writer = null;
    }

    private void LateUpdate()
    {
        if (!_recording)
            return;

        double elapsed = ExperimentEventLogger.ElapsedSeconds;
        double windowEnd = _windowStartElapsed + _windowDuration;

        while (elapsed >= windowEnd)
        {
            WriteWindow(windowEnd);
            _windowStartElapsed = windowEnd;
            _windowIndex++;
            ResetWindowStatistics();
            windowEnd = _windowStartElapsed + _windowDuration;
        }

        _sampleTimer += Time.unscaledDeltaTime;
        float sampleInterval = 1f / _sampleRateHz;

        if (_sampleTimer >= sampleInterval)
        {
            _sampleTimer -= sampleInterval;
            SampleGaze();
        }
    }

    private void BeginRecording()
    {
        if (_recording)
            return;

        _camera = Camera.main;
        OpenWriter();
        _recording = true;
        _sampleTimer = 0f;
        _windowIndex = 0;
        _windowStartElapsed = ExperimentEventLogger.ElapsedSeconds;
        _headReferenceRotation =
            _camera != null ? _camera.transform.rotation : Quaternion.identity;
        ResetWindowStatistics();
    }

    private void EndRecording()
    {
        if (!_recording)
            return;

        double endElapsed = ExperimentEventLogger.ElapsedSeconds;
        if (_sampleCount > 0 && endElapsed > _windowStartElapsed)
            WriteWindow(endElapsed);

        _recording = false;
        _writer?.Flush();
        _writer?.Close();
        _writer = null;
    }

    private void SampleGaze()
    {
        _sampleCount++;

        if (_camera == null)
        {
            _camera = Camera.main;
            if (_camera == null)
                return;
        }

        OVRPlugin.EyeGazesState eyeState = default;
        if (!OVRPlugin.GetEyeGazesState(
                OVRPlugin.Step.Render,
                -1,
                ref eyeState))
        {
            return;
        }

        var left = eyeState.EyeGazes[(int)OVRPlugin.Eye.Left];
        var right = eyeState.EyeGazes[(int)OVRPlugin.Eye.Right];

        bool leftValid =
            left.IsValid &&
            IsValidQuaternion(left.Pose.Orientation) &&
            left.Confidence >= _confidenceThreshold;

        bool rightValid =
            right.IsValid &&
            IsValidQuaternion(right.Pose.Orientation) &&
            right.Confidence >= _confidenceThreshold;

        if (!leftValid && !rightValid)
            return;

        Quaternion headRotation = _camera.transform.rotation;
        Vector3 combinedDirection = Vector3.zero;

        if (leftValid)
        {
            Vector3 leftLocal =
                left.Pose.ToOVRPose().orientation * Vector3.forward;
            combinedDirection += headRotation * leftLocal;
        }

        if (rightValid)
        {
            Vector3 rightLocal =
                right.Pose.ToOVRPose().orientation * Vector3.forward;
            combinedDirection += headRotation * rightLocal;
        }

        if (combinedDirection.sqrMagnitude <= Mathf.Epsilon)
            return;

        combinedDirection.Normalize();

        Quaternion headDeltaRotation =
            Quaternion.Inverse(_headReferenceRotation) * headRotation;
        Vector3 correctedDirection =
            Quaternion.Inverse(headDeltaRotation) * combinedDirection;

        Vector3 viewport = _camera.WorldToViewportPoint(
            _camera.transform.position + correctedDirection);

        float gazeX = viewport.x - 0.5f;
        float gazeY = viewport.y - 0.5f;

        if (!IsFinite(gazeX) || !IsFinite(gazeY))
            return;

        AddValidSample(gazeX, gazeY);
    }

    private void AddValidSample(double x, double y)
    {
        _validSampleCount++;

        double deltaX = x - _meanX;
        double deltaY = y - _meanY;

        _meanX += deltaX / _validSampleCount;
        _meanY += deltaY / _validSampleCount;

        _m2X += deltaX * (x - _meanX);
        _m2Y += deltaY * (y - _meanY);
        _coMoment += deltaX * (y - _meanY);
    }

    private void WriteWindow(double windowEndElapsed)
    {
        if (_writer == null)
            return;

        DateTimeOffset startAbsolute =
            ExperimentEventLogger.AbsoluteTimeAtElapsed(_windowStartElapsed);
        DateTimeOffset endAbsolute =
            ExperimentEventLogger.AbsoluteTimeAtElapsed(windowEndElapsed);

        string meanX = string.Empty;
        string meanY = string.Empty;
        string varianceX = string.Empty;
        string varianceY = string.Empty;
        string covariance = string.Empty;
        string dispersion = string.Empty;

        if (_validSampleCount > 0)
        {
            double varX = _m2X / _validSampleCount;
            double varY = _m2Y / _validSampleCount;
            double cov = _coMoment / _validSampleCount;

            meanX = ExperimentEventLogger.FormatDouble(_meanX);
            meanY = ExperimentEventLogger.FormatDouble(_meanY);
            varianceX = ExperimentEventLogger.FormatDouble(varX);
            varianceY = ExperimentEventLogger.FormatDouble(varY);
            covariance = ExperimentEventLogger.FormatDouble(cov);
            dispersion = ExperimentEventLogger.FormatDouble(
                Math.Sqrt(Math.Max(0d, varX + varY)));
        }

        _writer.WriteLine(string.Join(",", new[]
        {
            ExperimentEventLogger.EscapeCsv(ExperimentEventLogger.SessionId),
            ExperimentEventLogger.RecordingId.ToString(CultureInfo.InvariantCulture),
            ExperimentEventLogger.EscapeCsv(
                ExperimentEventLogger.CurrentCondition),
            _windowIndex.ToString(CultureInfo.InvariantCulture),
            ExperimentEventLogger.EscapeCsv(
                ExperimentEventLogger.FormatAbsoluteTime(startAbsolute)),
            ExperimentEventLogger.EscapeCsv(
                ExperimentEventLogger.FormatAbsoluteTime(endAbsolute)),
            ExperimentEventLogger.FormatDouble(_windowStartElapsed),
            ExperimentEventLogger.FormatDouble(windowEndElapsed),
            _sampleCount.ToString(CultureInfo.InvariantCulture),
            _validSampleCount.ToString(CultureInfo.InvariantCulture),
            meanX,
            meanY,
            varianceX,
            varianceY,
            covariance,
            dispersion,
            (_sampleCount - _validSampleCount).ToString(
                CultureInfo.InvariantCulture)
        }));
        _writer.Flush();
    }

    private void ResetWindowStatistics()
    {
        _sampleCount = 0;
        _validSampleCount = 0;
        _meanX = 0d;
        _meanY = 0d;
        _m2X = 0d;
        _m2Y = 0d;
        _coMoment = 0d;
    }

    private void OpenWriter()
    {
        string path = Path.Combine(
            ExperimentEventLogger.GetDataDirectory(),
            $"{ExperimentEventLogger.RecordingFileStamp}_gaze_summary.csv");

        _writer = new StreamWriter(
            path,
            false,
            new UTF8Encoding(false));

        _writer.WriteLine(
            "SessionId,RecordingId,Condition,WindowIndex," +
            "WindowStartAbsTime,WindowEndAbsTime," +
            "WindowStartElapsedSec,WindowEndElapsedSec," +
            "SampleCount,ValidSampleCount," +
            "MeanGazeX,MeanGazeY," +
            "VarianceGazeX,VarianceGazeY,CovarianceXY,Dispersion," +
            "TrackingFailureCount");
        _writer.Flush();
    }

    private static bool IsValidQuaternion(OVRPlugin.Quatf value)
    {
        return value.x != 0f || value.y != 0f ||
               value.z != 0f || value.w != 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
