using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class ExperimentEventLogger : MonoBehaviour
{
    public enum ExperimentCondition
    {
        Unspecified,
        VignetteOff,
        VignetteOn
    }

    [Header("Recording")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.Return;
    [SerializeField] private bool _startRecordingOnAwake = false;

    [Header("Experiment")]
    [SerializeField] private ExperimentCondition _condition =
        ExperimentCondition.Unspecified;
    [SerializeField] private DisturbanceController _disturbanceController;

    public static event Action RecordingStarted;
    public static event Action RecordingStopping;

    public static bool IsRecording { get; private set; }
    public static int RecordingId { get; private set; }

    private static ExperimentEventLogger _instance;
    private static bool _sessionInitialized;
    private static string _sessionId;
    private static string _recordingFileStamp;
    private static DateTimeOffset _sessionStartAbsolute;
    private static double _sessionStartRealtime;

    private StreamWriter _writer;
    private bool _shuttingDown;
    private ParticleSystem _disturbanceParticles;
    private bool _disturbanceStateInitialized;
    private bool _lastDisturbancePlaying;

    public static string SessionId
    {
        get
        {
            EnsureSessionInitialized();
            return _sessionId;
        }
    }

    public static string CurrentCondition =>
        _instance != null
            ? _instance._condition.ToString()
            : ExperimentCondition.Unspecified.ToString();

    public static string RecordingFileStamp => _recordingFileStamp;

    public static double ElapsedSeconds
    {
        get
        {
            EnsureSessionInitialized();
            return Time.realtimeSinceStartupAsDouble - _sessionStartRealtime;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _sessionInitialized = false;
        _sessionId = null;
        _recordingFileStamp = null;
        _sessionStartAbsolute = default;
        _sessionStartRealtime = 0d;
        IsRecording = false;
        RecordingId = 0;
        RecordingStarted = null;
        RecordingStopping = null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("ExperimentEventLogger is duplicated. The duplicate was disabled.");
            enabled = false;
            return;
        }

        _instance = this;
        EnsureSessionInitialized();
        if (_startRecordingOnAwake)
            StartRecording();
    }

    private void Start()
    {
        // DisturbanceController.Awake has finished configuring and stopping
        // the ParticleSystem by this point, so the initial state is reliable.
        InitializeDisturbanceMonitoring();
    }

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey))
        {
            if (IsRecording)
                StopRecording();
            else
                StartRecording();
        }
    }

    private void LateUpdate()
    {
        RecordDisturbanceStateChange();
    }

    private void OnDestroy()
    {
        if (_instance != this)
            return;

        Shutdown();
        _instance = null;
    }

    private void OnApplicationQuit()
    {
        if (_instance == this)
            Shutdown();
    }

    public void StartRecording()
    {
        if (IsRecording)
            return;

        RecordingId++;
        _recordingFileStamp = DateTime.Now.ToString(
            "yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture);
        OpenWriter();
        IsRecording = true;
        WriteEvent("SessionCreated", string.Empty);
        WriteEvent("RecordingStart", string.Empty);
        RecordingStarted?.Invoke();
    }

    public void StopRecording()
    {
        if (!IsRecording)
            return;

        RecordingStopping?.Invoke();
        WriteEvent("RecordingStop", string.Empty);
        IsRecording = false;
        _writer?.Flush();
        _writer?.Close();
        _writer = null;
    }

    public void SetCondition(ExperimentCondition condition)
    {
        if (_condition == condition)
            return;

        string previous = _condition.ToString();
        _condition = condition;
        WriteEvent("ConditionChanged", $"From={previous};To={_condition}");
    }

    public static void RecordEvent(string eventType, string details = "")
    {
        if (_instance == null || _instance._writer == null)
        {
            Debug.LogWarning($"Experiment event could not be recorded: {eventType}");
            return;
        }

        _instance.WriteEvent(eventType, details);
    }

    public static DateTimeOffset AbsoluteTimeAtElapsed(double elapsedSeconds)
    {
        EnsureSessionInitialized();
        return _sessionStartAbsolute.AddSeconds(elapsedSeconds);
    }

    public static string GetDataDirectory()
    {
#if UNITY_EDITOR
        string dataDirectory = Path.Combine(Application.dataPath, "data");
#else
        string dataDirectory = Path.Combine(Application.persistentDataPath, "data");
#endif
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    public static string FormatDouble(double value, string format = "F6")
    {
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    public static string FormatAbsoluteTime(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    public static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (!value.Contains(",") && !value.Contains("\"") &&
            !value.Contains("\r") && !value.Contains("\n"))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static void EnsureSessionInitialized()
    {
        if (_sessionInitialized)
            return;

        _sessionStartAbsolute = DateTimeOffset.Now;
        _sessionStartRealtime = Time.realtimeSinceStartupAsDouble;
        _sessionId = _sessionStartAbsolute.ToString(
            "yyyyMMdd_HHmmss_fff",
            CultureInfo.InvariantCulture);
        _sessionInitialized = true;
    }

    private void OpenWriter()
    {
        string path = Path.Combine(
            GetDataDirectory(),
            $"{RecordingFileStamp}_experiment_events.csv");

        _writer = new StreamWriter(
            path,
            false,
            new UTF8Encoding(false));

        _writer.WriteLine(
            "SessionId,RecordingId,Condition,AbsTime,ElapsedSec,EventType,Details");
        _writer.Flush();
    }

    private void InitializeDisturbanceMonitoring()
    {
        if (_disturbanceController == null)
            return;

        _disturbanceParticles =
            _disturbanceController.GetComponent<ParticleSystem>();

        if (_disturbanceParticles == null)
            return;

        _lastDisturbancePlaying = _disturbanceParticles.isPlaying;
        _disturbanceStateInitialized = true;
    }

    private void RecordDisturbanceStateChange()
    {
        if (!_disturbanceStateInitialized)
        {
            InitializeDisturbanceMonitoring();
            return;
        }

        bool isPlaying = _disturbanceParticles.isPlaying;
        if (isPlaying == _lastDisturbancePlaying)
            return;

        _lastDisturbancePlaying = isPlaying;
        WriteEvent(
            isPlaying ? "DisturbanceStart" : "DisturbanceEnd",
            string.Empty);
    }

    private void WriteEvent(string eventType, string details)
    {
        if (_writer == null)
            return;

        double elapsed = ElapsedSeconds;
        DateTimeOffset absoluteTime = AbsoluteTimeAtElapsed(elapsed);

        _writer.WriteLine(string.Join(",", new[]
        {
            EscapeCsv(SessionId),
            RecordingId.ToString(CultureInfo.InvariantCulture),
            EscapeCsv(CurrentCondition),
            EscapeCsv(FormatAbsoluteTime(absoluteTime)),
            FormatDouble(elapsed),
            EscapeCsv(eventType),
            EscapeCsv(details)
        }));
        _writer.Flush();
    }

    private void Shutdown()
    {
        if (_shuttingDown)
            return;

        _shuttingDown = true;

        if (IsRecording)
            StopRecording();

        WriteEvent("SessionEnded", string.Empty);
        _writer?.Flush();
        _writer?.Close();
        _writer = null;
    }
}
