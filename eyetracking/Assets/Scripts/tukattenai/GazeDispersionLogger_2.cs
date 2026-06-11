using System;
using System.IO;
using System.Text;
using UnityEngine;

public class GazeDispersionLogger_2 : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _centerEyeAnchor;

    [Header("Timing")]
    [SerializeField] private float _trialDurationMs = 1150f;
    [SerializeField] private int   _trialsPerBlock  = 10;

    [Header("Eye Tracking")]
    [SerializeField] private float _confidenceThreshold = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugLog = true;

    private bool   _measuring    = false;
    private int    _currentTrial = 0;
    private float  _trialTimer   = 0f;
    private int    _blockNumber  = 1;

    private int[] _trialCounts = new int[9];
    private int[] _blockCounts = new int[9];

    private string _csvPath;

    private void Start()
    {
        string dir = Path.Combine(Application.dataPath, "data");
        Directory.CreateDirectory(dir);
        _csvPath = Path.Combine(dir, "gaze_dispersion.csv");
        InitCsv();

        if (_showDebugLog)
            Debug.Log($"[GazeDispersionLogger_2] CSV: {_csvPath}  Enter で計測開始");
    }

    private void Update()
    {
        if (!_measuring)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                StartMeasurement();
            return;
        }

        Vector2? vp = GetViewportGaze();
        if (vp.HasValue)
            _trialCounts[ClassifyCell(vp.Value)]++;

        _trialTimer += Time.deltaTime * 1000f;
        if (_trialTimer >= _trialDurationMs)
            OnTrialEnd();
    }

    private void StartMeasurement()
    {
        _measuring    = true;
        _currentTrial = 0;
        _blockNumber  = 1;
        _trialTimer   = 0f;
        Array.Clear(_trialCounts, 0, 9);
        Array.Clear(_blockCounts, 0, 9);

        if (_showDebugLog)
            Debug.Log("[GazeDispersionLogger_2] 計測開始");
    }

    private void OnTrialEnd()
    {
        for (int i = 0; i < 9; i++)
            _blockCounts[i] += _trialCounts[i];

        Array.Clear(_trialCounts, 0, 9);
        _trialTimer = 0f;
        _currentTrial++;

        if (_showDebugLog)
            Debug.Log($"[GazeDispersionLogger_2] 試行 {_currentTrial} 終了");

        if (_currentTrial >= _trialsPerBlock)
            OnBlockEnd();
    }

    private void OnBlockEnd()
    {
        int total = 0;
        for (int i = 0; i < 9; i++) total += _blockCounts[i];

        float[] ratios = new float[9];
        if (total > 0)
            for (int i = 0; i < 9; i++)
                ratios[i] = _blockCounts[i] / (float)total * 100f;

        float centerRate = ratios[4];
        AppendCsvRow(_blockNumber, ratios, centerRate);

        if (_showDebugLog)
        {
            var sb = new StringBuilder();
            sb.Append($"[GazeDispersionLogger_2] Block {_blockNumber}  Total={total}  ");
            for (int i = 0; i < 9; i++)
                sb.Append($"Cell{i + 1}={ratios[i]:F1}%  ");
            sb.Append($"CenterRate={centerRate:F1}%");
            Debug.Log(sb.ToString());
        }

        Array.Clear(_blockCounts, 0, 9);
        _currentTrial = 0;
        _blockNumber++;
    }

    private Vector2? GetViewportGaze()
    {
        OVRPlugin.EyeGazesState state = default;
        if (!OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref state))
            return null;

        var left  = state.EyeGazes[(int)OVRPlugin.Eye.Left];
        var right = state.EyeGazes[(int)OVRPlugin.Eye.Right];

        float leftConf  = left.IsValid  ? left.Confidence  : 0f;
        float rightConf = right.IsValid ? right.Confidence : 0f;

        if (leftConf < _confidenceThreshold && rightConf < _confidenceThreshold)
            return null;

        Vector3 gazeDir;
        if (leftConf >= _confidenceThreshold && rightConf >= _confidenceThreshold)
        {
            var lPose = left.Pose.ToOVRPose().ToWorldSpacePose(Camera.main);
            var rPose = right.Pose.ToOVRPose().ToWorldSpacePose(Camera.main);
            gazeDir = (lPose.orientation * Vector3.forward
                     + rPose.orientation * Vector3.forward).normalized;
        }
        else
        {
            var pose = (leftConf >= _confidenceThreshold ? left : right)
                       .Pose.ToOVRPose().ToWorldSpacePose(Camera.main);
            gazeDir = pose.orientation * Vector3.forward;
        }

        Camera cam = Camera.main;
        if (cam == null) return null;

        Vector3 worldPoint    = cam.transform.position + gazeDir * 1.0f;
        Vector3 viewportPoint = cam.WorldToViewportPoint(worldPoint);

        if (viewportPoint.z <= 0f) return null;

        return new Vector2(viewportPoint.x, viewportPoint.y);
    }

    private static int ClassifyCell(Vector2 vp)
    {
        float x = Mathf.Clamp01(vp.x);
        float y = Mathf.Clamp01(vp.y);

        int col = x < 0.3333f ? 0 : (x < 0.6667f ? 1 : 2);
        int row = y >= 0.6667f ? 0 : (y >= 0.3333f ? 1 : 2);

        return row * 3 + col;
    }

    private void InitCsv()
    {
        if (!File.Exists(_csvPath))
        {
            using var sw = new StreamWriter(_csvPath, append: false, encoding: Encoding.UTF8);
            sw.WriteLine("Block,Cell1,Cell2,Cell3,Cell4,Cell5,Cell6,Cell7,Cell8,Cell9,CenterRate");
        }
    }

    private void AppendCsvRow(int block, float[] ratios, float centerRate)
    {
        try
        {
            using var sw = new StreamWriter(_csvPath, append: true, encoding: Encoding.UTF8);
            var sb = new StringBuilder();
            sb.Append(block);
            for (int i = 0; i < 9; i++)
                sb.Append($",{ratios[i]:F2}");
            sb.Append($",{centerRate:F2}");
            sw.WriteLine(sb.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GazeDispersionLogger_2] CSV 書き込みエラー: {ex.Message}");
        }
    }

    public void StopMeasurement()
    {
        if (!_measuring) return;
        _measuring = false;
        if (_showDebugLog)
            Debug.Log("[GazeDispersionLogger_2] 計測停止");
    }

    public void FlushAndStop()
    {
        if (!_measuring) return;
        int total = 0;
        for (int i = 0; i < 9; i++) total += _blockCounts[i];
        if (total > 0) OnBlockEnd();
        StopMeasurement();
    }
}