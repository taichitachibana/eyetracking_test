using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(-800)]
public class BlinkEventLogger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OVRFaceExpressions _faceExpressions;

    [Header("Blink Detection")]
    [SerializeField, Range(0f, 1f)] private float _blinkStartThreshold = 0.49f;
    [SerializeField, Range(0f, 1f)] private float _blinkEndThreshold = 0.2f;
    [SerializeField, Min(0f)] private float _minimumBlinkDuration = 0.05f;
    [SerializeField, Min(1f)] private float _sampleRateHz = 60f;

    private StreamWriter _writer;
    private bool _recording;
    private float _sampleTimer;
    private int _sampleIndex;
    private int _eventId;

    private bool _blinkActive;
    private double _blinkStartElapsed;
    private int _blinkStartSampleIndex;
    private double _blinkClosureSum;
    private int _blinkClosureSampleCount;
    private float _blinkMaxClosure;
    private bool _blinkTrackingInterrupted;

    private bool _trackingFailureActive;
    private double _trackingFailureStartElapsed;
    private int _trackingFailureStartSampleIndex;

    private void Awake()
    {
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

    private void OnValidate()
    {
        if (_blinkEndThreshold > _blinkStartThreshold)
            _blinkEndThreshold = _blinkStartThreshold;
    }

    private void LateUpdate()
    {
        if (!_recording)
            return;

        _sampleTimer += Time.unscaledDeltaTime;
        float sampleInterval = 1f / _sampleRateHz;

        if (_sampleTimer >= sampleInterval)
        {
            _sampleTimer -= sampleInterval;
            SampleBlink();
            _sampleIndex++;
        }
    }

    private void BeginRecording()
    {
        if (_recording)
            return;

        OpenWriter();
        _recording = true;
        _sampleTimer = 0f;
        _sampleIndex = 0;
        _eventId = 0;
        ResetBlinkState();
        ResetTrackingFailureState();
    }

    private void EndRecording()
    {
        if (!_recording)
            return;

        double elapsed = ExperimentEventLogger.ElapsedSeconds;

        if (_blinkActive)
        {
            _blinkTrackingInterrupted = true;
            FinishBlink(elapsed, _sampleIndex, "RecordingStopped");
        }

        if (_trackingFailureActive)
        {
            FinishTrackingFailure(
                elapsed,
                _sampleIndex,
                "RecordingStopped");
        }

        _recording = false;
        _writer?.Flush();
        _writer?.Close();
        _writer = null;
    }

    private void SampleBlink()
    {
        double elapsed = ExperimentEventLogger.ElapsedSeconds;

        if (!TryGetEyeClosure(out float leftClosed, out float rightClosed))
        {
            if (!_trackingFailureActive)
            {
                _trackingFailureActive = true;
                _trackingFailureStartElapsed = elapsed;
                _trackingFailureStartSampleIndex = _sampleIndex;
                Debug.LogWarning("Blink face-expression tracking was lost.");
            }

            if (_blinkActive)
                _blinkTrackingInterrupted = true;

            return;
        }

        if (_trackingFailureActive)
        {
            FinishTrackingFailure(elapsed, _sampleIndex, "TrackingRecovered");
            Debug.Log("Blink face-expression tracking recovered.");
        }

        float closure = (leftClosed + rightClosed) * 0.5f;

        if (!_blinkActive)
        {
            if (closure >= _blinkStartThreshold)
                StartBlink(elapsed, closure);

            return;
        }

        AddBlinkClosureSample(closure);

        if (closure <= _blinkEndThreshold)
            FinishBlink(elapsed, _sampleIndex, "EyesOpened");
    }

    private bool TryGetEyeClosure(
        out float leftClosed,
        out float rightClosed)
    {
        leftClosed = 0f;
        rightClosed = 0f;

        if (_faceExpressions == null)
            return false;

        bool leftValid = _faceExpressions.TryGetFaceExpressionWeight(
            OVRFaceExpressions.FaceExpression.EyesClosedL,
            out leftClosed);

        bool rightValid = _faceExpressions.TryGetFaceExpressionWeight(
            OVRFaceExpressions.FaceExpression.EyesClosedR,
            out rightClosed);

        return leftValid && rightValid;
    }

    private void StartBlink(double elapsed, float closure)
    {
        _blinkActive = true;
        _blinkStartElapsed = elapsed;
        _blinkStartSampleIndex = _sampleIndex;
        _blinkClosureSum = 0d;
        _blinkClosureSampleCount = 0;
        _blinkMaxClosure = 0f;
        _blinkTrackingInterrupted = false;
        AddBlinkClosureSample(closure);
    }

    private void AddBlinkClosureSample(float closure)
    {
        _blinkClosureSum += closure;
        _blinkClosureSampleCount++;
        _blinkMaxClosure = Mathf.Max(_blinkMaxClosure, closure);
    }

    private void FinishBlink(
        double endElapsed,
        int endSampleIndex,
        string endReason)
    {
        double durationSeconds = Math.Max(0d, endElapsed - _blinkStartElapsed);
        double meanClosure =
            _blinkClosureSampleCount > 0
                ? _blinkClosureSum / _blinkClosureSampleCount
                : 0d;
        bool accepted = durationSeconds >= _minimumBlinkDuration;

        WriteEvent(
            "Blink",
            _blinkStartElapsed,
            endElapsed,
            _blinkStartSampleIndex,
            endSampleIndex,
            _blinkMaxClosure,
            meanClosure,
            accepted ? "1" : "0",
            _blinkTrackingInterrupted ? "1" : "0",
            endReason);

        ResetBlinkState();
    }

    private void FinishTrackingFailure(
        double endElapsed,
        int endSampleIndex,
        string endReason)
    {
        WriteEvent(
            "TrackingFailure",
            _trackingFailureStartElapsed,
            endElapsed,
            _trackingFailureStartSampleIndex,
            endSampleIndex,
            null,
            null,
            string.Empty,
            "1",
            endReason);

        ResetTrackingFailureState();
    }

    private void WriteEvent(
        string eventType,
        double startElapsed,
        double endElapsed,
        int startSampleIndex,
        int endSampleIndex,
        double? maxClosure,
        double? meanClosure,
        string isAccepted,
        string trackingInterrupted,
        string endReason)
    {
        if (_writer == null)
            return;

        _eventId++;
        DateTimeOffset startAbsolute =
            ExperimentEventLogger.AbsoluteTimeAtElapsed(startElapsed);
        DateTimeOffset endAbsolute =
            ExperimentEventLogger.AbsoluteTimeAtElapsed(endElapsed);

        _writer.WriteLine(string.Join(",", new[]
        {
            ExperimentEventLogger.EscapeCsv(ExperimentEventLogger.SessionId),
            ExperimentEventLogger.RecordingId.ToString(CultureInfo.InvariantCulture),
            ExperimentEventLogger.EscapeCsv(
                ExperimentEventLogger.CurrentCondition),
            ExperimentEventLogger.EscapeCsv(eventType),
            _eventId.ToString(CultureInfo.InvariantCulture),
            ExperimentEventLogger.EscapeCsv(
                ExperimentEventLogger.FormatAbsoluteTime(startAbsolute)),
            ExperimentEventLogger.EscapeCsv(
                ExperimentEventLogger.FormatAbsoluteTime(endAbsolute)),
            ExperimentEventLogger.FormatDouble(startElapsed),
            ExperimentEventLogger.FormatDouble(endElapsed),
            ExperimentEventLogger.FormatDouble(
                Math.Max(0d, endElapsed - startElapsed) * 1000d,
                "F3"),
            startSampleIndex.ToString(CultureInfo.InvariantCulture),
            endSampleIndex.ToString(CultureInfo.InvariantCulture),
            maxClosure.HasValue
                ? ExperimentEventLogger.FormatDouble(maxClosure.Value)
                : string.Empty,
            meanClosure.HasValue
                ? ExperimentEventLogger.FormatDouble(meanClosure.Value)
                : string.Empty,
            isAccepted,
            trackingInterrupted,
            ExperimentEventLogger.EscapeCsv(endReason)
        }));
        _writer.Flush();
    }

    private void ResetBlinkState()
    {
        _blinkActive = false;
        _blinkStartElapsed = 0d;
        _blinkStartSampleIndex = 0;
        _blinkClosureSum = 0d;
        _blinkClosureSampleCount = 0;
        _blinkMaxClosure = 0f;
        _blinkTrackingInterrupted = false;
    }

    private void ResetTrackingFailureState()
    {
        _trackingFailureActive = false;
        _trackingFailureStartElapsed = 0d;
        _trackingFailureStartSampleIndex = 0;
    }

    private void OpenWriter()
    {
        string path = Path.Combine(
            ExperimentEventLogger.GetDataDirectory(),
            $"{ExperimentEventLogger.RecordingFileStamp}_blink_events.csv");

        _writer = new StreamWriter(
            path,
            false,
            new UTF8Encoding(false));

        _writer.WriteLine(
            "SessionId,RecordingId,Condition,EventType,EventId," +
            "StartAbsTime,EndAbsTime,StartElapsedSec,EndElapsedSec," +
            "DurationMs,StartSampleIndex,EndSampleIndex," +
            "MaxClosure,MeanClosure,IsAccepted," +
            "TrackingInterrupted,EndReason");
        _writer.Flush();
    }
}
