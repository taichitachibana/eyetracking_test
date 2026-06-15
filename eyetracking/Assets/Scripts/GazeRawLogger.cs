using System;
using System.Collections.Generic;
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

    // 60Hz固定サンプリング
    private const float SampleInterval = 1f / 60f;
    private float _sampleTimer = 0f;

    private bool _isRecording = false;
    private float _trialTimer = 0f;
    private int _currentTrial = 0;
    private int _blockNumber = 0;
    private int _frameIndex = 0;

    private Camera _cam;
    private string _rawCsvPath;
    private StreamWriter _rawWriter;

    private Quaternion _headRefRotation = Quaternion.identity;
    private Vector3 _headRefPosition = Vector3.zero;   // 記録開始時の頭部位置（原点）

    // 試行ごとのバッファ
    private List<string> _trialBuffer = new List<string>();

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
        // 未書き出しのバッファが残っていれば書き出す
        FlushBuffer();
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

        // 60Hzサンプリング
        _sampleTimer += Time.deltaTime;
        if (_sampleTimer >= SampleInterval)
        {
            _sampleTimer -= SampleInterval;
            SampleFrame();
        }

        // 試行タイマー
        _trialTimer += Time.deltaTime;
        if (_trialTimer >= _trialDuration)
        {
            _trialTimer -= _trialDuration;

            // 試行終了 → バッファを書き出し
            FlushBuffer();

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
        _sampleTimer = 0f;
        _currentTrial = 0;
        _blockNumber = 0;
        _frameIndex = 0;
        _headRefRotation = _cam != null ? _cam.transform.rotation : Quaternion.identity;
        _headRefPosition = _cam != null ? _cam.transform.position : Vector3.zero;
        _trialBuffer.Clear();
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

        // 頭部位置差分（記録開始時を原点）
        Vector3 headPos = _cam.transform.position;
        Quaternion headRot = _cam.transform.rotation;
        Vector3 headDelta = headPos - _headRefPosition;

        // 注視方向（両眼合成）
        Vector3 leftDirLocal = left.IsValid
            ? (left.Pose.ToOVRPose().orientation * Vector3.forward) : Vector3.forward;
        Vector3 rightDirLocal = right.IsValid
            ? (right.Pose.ToOVRPose().orientation * Vector3.forward) : Vector3.forward;

        Vector3 leftDirWorld = headRot * leftDirLocal;
        Vector3 rightDirWorld = headRot * rightDirLocal;

        Vector3 combinedDir = ((leftConf > 0f ? leftDirWorld : Vector3.zero)
                             + (rightConf > 0f ? rightDirWorld : Vector3.zero)).normalized;
        if (combinedDir == Vector3.zero) combinedDir = headRot * Vector3.forward;

        // 頭部回転補正済み注視座標（視界中心 = 0,0）
        Quaternion headDeltaRot = Quaternion.Inverse(_headRefRotation) * headRot;
        Vector3 correctedDir = Quaternion.Inverse(headDeltaRot) * combinedDir;
        Vector3 vpCorrected = _cam.WorldToViewportPoint(headPos + correctedDir);
        float gazeCorrX = vpCorrected.x - 0.5f;
        float gazeCorrY = vpCorrected.y - 0.5f;

        // PC絶対時間
        string absTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        var sb = new StringBuilder();
        sb.Append($"{_frameIndex},{_blockNumber},{_currentTrial},{absTime},");
        sb.Append($"{headDelta.x:F4},{headDelta.y:F4},{headDelta.z:F4},");
        sb.Append($"{gazeCorrX:F4},{gazeCorrY:F4},");
        sb.Append(isBlink ? "1" : "0");

        _trialBuffer.Add(sb.ToString());
        _frameIndex++;
    }

    // バッファをまとめてCSVに書き出す
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
        _rawWriter = new StreamWriter(_rawCsvPath, append: false, encoding: Encoding.UTF8);
        _rawWriter.WriteLine(
            "FrameIndex,Block,Trial,AbsTime," +
            "HeadDeltaX,HeadDeltaY,HeadDeltaZ," +
            "GazeCorrX,GazeCorrY," +
            "IsBlink"
        );
        _rawWriter.Flush();
    }
}