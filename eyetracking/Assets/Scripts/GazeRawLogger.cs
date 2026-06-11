using System;
using System.IO;
using System.Text;
using UnityEngine;

public class GazeRawLogger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _gazeTarget;

    [Header("Trial Settings")]
    [SerializeField] private float _trialDuration = 1.15f;
    [SerializeField] private int _trialsPerBlock = 10;
    [SerializeField] private float _blinkConfidenceThreshold = 0.3f;

    private bool _isRecording = false;
    private float _trialTimer = 0f;
    private int _currentTrial = 0;
    private int _blockNumber = 0;
    private int _frameIndex = 0;

    private Camera _cam;
    private string _rawCsvPath;
    private StreamWriter _rawWriter;

    private Quaternion _headRefRotation = Quaternion.identity;

    private void Awake()
    {
        _cam = Camera.main;

#if UNITY_EDITOR
        string dataDir = Path.Combine(Application.dataPath, "data");
#else
        string dataDir = Path.Combine(Application.persistentDataPath, "data");
#endif
        Directory.CreateDirectory(dataDir);

        string stamp = DateTime.Now.ToString("yyyy_MM_dd_HHmm");
        _rawCsvPath = Path.Combine(dataDir, stamp + "_raw.csv");

        InitRawCSV();
    }

    private void OnDestroy()
    {
        _rawWriter?.Flush();
        _rawWriter?.Close();
    }

    private void Update()
    {
        if (!_isRecording)
        {
            if (Input.GetKeyDown(KeyCode.Return)) BeginRecording();
            return;
        }

        SampleFrame();

        _trialTimer += Time.deltaTime;
        if (_trialTimer >= _trialDuration)
        {
            _trialTimer -= _trialDuration;
            _currentTrial++;
            if (_currentTrial >= _trialsPerBlock)
            {
                _blockNumber++;
                _currentTrial = 0;
            }
        }
    }

    private void BeginRecording()
    {
        _isRecording = true;
        _trialTimer = 0f;
        _currentTrial = 0;
        _blockNumber = 0;
        _frameIndex = 0;
        _headRefRotation = _cam != null ? _cam.transform.rotation : Quaternion.identity;
    }

    private void SampleFrame()
    {
        if (_cam == null) return;

        OVRPlugin.EyeGazesState eyeState = default;
        bool stateValid = OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref eyeState);

        var left = stateValid ? eyeState.EyeGazes[(int)OVRPlugin.Eye.Left] : default;
        var right = stateValid ? eyeState.EyeGazes[(int)OVRPlugin.Eye.Right] : default;

        float leftConf = left.IsValid ? left.Confidence : 0f;
        float rightConf = right.IsValid ? right.Confidence : 0f;
        bool isBlink = Mathf.Max(leftConf, rightConf) < _blinkConfidenceThreshold;

        Vector3 headPos = _cam.transform.position;
        Quaternion headRot = _cam.transform.rotation;

        Vector3 leftDirLocal = left.IsValid ? (left.Pose.ToOVRPose().orientation * Vector3.forward) : Vector3.forward;
        Vector3 rightDirLocal = right.IsValid ? (right.Pose.ToOVRPose().orientation * Vector3.forward) : Vector3.forward;

        Vector3 leftDirWorld = headRot * leftDirLocal;
        Vector3 rightDirWorld = headRot * rightDirLocal;

        Vector3 combinedDir = ((leftConf > 0f ? leftDirWorld : Vector3.zero)
                             + (rightConf > 0f ? rightDirWorld : Vector3.zero)).normalized;
        if (combinedDir == Vector3.zero) combinedDir = headRot * Vector3.forward;

        Vector3 vpRaw = _cam.WorldToViewportPoint(headPos + combinedDir);
        float gazeRawX = vpRaw.x - 0.5f;
        float gazeRawY = vpRaw.y - 0.5f;

        Quaternion headDelta = Quaternion.Inverse(_headRefRotation) * headRot;
        Vector3 correctedDir = Quaternion.Inverse(headDelta) * combinedDir;
        Vector3 vpCorrected = _cam.WorldToViewportPoint(headPos + correctedDir);
        float gazeCorrX = vpCorrected.x - 0.5f;
        float gazeCorrY = vpCorrected.y - 0.5f;

        WriteRawRow(
            _frameIndex, _blockNumber, _currentTrial, Time.time,
            headPos, headRot,
            leftDirWorld, leftConf, rightDirWorld, rightConf,
            gazeRawX, gazeRawY, gazeCorrX, gazeCorrY,
            isBlink
        );

        _frameIndex++;
    }

    private void InitRawCSV()
    {
        _rawWriter = new StreamWriter(_rawCsvPath, append: false, encoding: Encoding.UTF8);
        _rawWriter.WriteLine(
            "FrameIndex,Block,Trial,Time," +
            "HeadPosX,HeadPosY,HeadPosZ," +
            "HeadRotX,HeadRotY,HeadRotZ,HeadRotW," +
            "LeftGazeDirX,LeftGazeDirY,LeftGazeDirZ,LeftConf," +
            "RightGazeDirX,RightGazeDirY,RightGazeDirZ,RightConf," +
            "GazeRawX,GazeRawY," +
            "GazeCorrX,GazeCorrY," +
            "IsBlink"
        );
        _rawWriter.Flush();
    }

    private void WriteRawRow(
        int frameIndex, int block, int trial, float time,
        Vector3 headPos, Quaternion headRot,
        Vector3 leftDir, float leftConf,
        Vector3 rightDir, float rightConf,
        float gazeRawX, float gazeRawY,
        float gazeCorrX, float gazeCorrY,
        bool isBlink)
    {
        var sb = new StringBuilder();
        sb.Append($"{frameIndex},{block},{trial},{time:F4},");
        sb.Append($"{headPos.x:F4},{headPos.y:F4},{headPos.z:F4},");
        sb.Append($"{headRot.x:F4},{headRot.y:F4},{headRot.z:F4},{headRot.w:F4},");
        sb.Append($"{leftDir.x:F4},{leftDir.y:F4},{leftDir.z:F4},{leftConf:F3},");
        sb.Append($"{rightDir.x:F4},{rightDir.y:F4},{rightDir.z:F4},{rightConf:F3},");
        sb.Append($"{gazeRawX:F4},{gazeRawY:F4},");
        sb.Append($"{gazeCorrX:F4},{gazeCorrY:F4},");
        sb.Append(isBlink ? "1" : "0");
        _rawWriter.WriteLine(sb.ToString());
    }
}