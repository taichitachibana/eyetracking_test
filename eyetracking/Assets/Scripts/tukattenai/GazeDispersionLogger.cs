using UnityEngine;
using System;
using System.IO;
using System.Text;

public class GazeDispersionLogger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _gazeTarget;

    [Header("Trial Settings")]
    [SerializeField] private float _trialDuration = 1.15f;
    [SerializeField] private int _trialsPerBlock = 10;
    [SerializeField] private float _blinkConfidenceThreshold = 0.3f;

    [Header("Grid Overlay")]
    [SerializeField] private bool _showGrid = true;
    [SerializeField] private Color _gridColor = Color.white;
    [SerializeField][Range(0.001f, 0.01f)] private float _gridLineWidth = 0.003f;

    private bool _isRecording = false;
    private float _trialTimer = 0f;
    private int _currentTrial = 0;
    private int _blockNumber = 0;

    private readonly int[] _cellCounts = new int[9];
    private int _validSamples = 0;

    private Camera _cam;
    private string _csvPath;
    private LineRenderer[] _gridLines;

    private const float GridDepth = 0.29f;

    private void Awake()
    {
        _cam = Camera.main;

#if UNITY_EDITOR
        string dataDir = Path.Combine(Application.dataPath, "data");
#else
        string dataDir = Path.Combine(Application.persistentDataPath, "data");
#endif
        Directory.CreateDirectory(dataDir);
        string fileName = DateTime.Now.ToString("yyyy_MM_dd_HHmm") + ".csv";
        _csvPath = Path.Combine(dataDir, fileName);

        InitCSV();
        BuildGridLines();
    }

    private void Update()
    {
        RefreshGridLines();

        if (!_isRecording)
        {
            if (Input.GetKeyDown(KeyCode.Return)) BeginRecording();
            return;
        }

        TrySampleGaze();

        _trialTimer += Time.deltaTime;
        if (_trialTimer >= _trialDuration)
        {
            _trialTimer -= _trialDuration;
            _currentTrial++;
            if (_currentTrial >= _trialsPerBlock)
            {
                FlushBlock();
                _currentTrial = 0;
                ResetAccumulator();
            }
        }
    }

    private void BeginRecording()
    {
        _isRecording = true;
        _trialTimer = 0f;
        _currentTrial = 0;
        ResetAccumulator();
    }

    private void TrySampleGaze()
    {
        if (_gazeTarget == null || _cam == null) return;

        OVRPlugin.EyeGazesState eyeState = default;
        if (!OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref eyeState)) return;

        var left = eyeState.EyeGazes[(int)OVRPlugin.Eye.Left];
        var right = eyeState.EyeGazes[(int)OVRPlugin.Eye.Right];

        float maxConf = Mathf.Max(
            left.IsValid ? left.Confidence : 0f,
            right.IsValid ? right.Confidence : 0f
        );

        if (maxConf < _blinkConfidenceThreshold) return;

        Vector3 vp = _cam.WorldToViewportPoint(_gazeTarget.position);
        if (vp.z < 0f) return;

        int col = Mathf.Min(2, (int)(Mathf.Clamp01(vp.x) * 3));
        int row = Mathf.Min(2, (int)(Mathf.Clamp01(vp.y) * 3));
        int cellIndex = (2 - row) * 3 + col;

        _cellCounts[cellIndex]++;
        _validSamples++;
    }

    private void FlushBlock()
    {
        _blockNumber++;
        var sb = new StringBuilder();
        sb.Append(_blockNumber);

        for (int i = 0; i < 9; i++)
        {
            float pct = _validSamples > 0 ? _cellCounts[i] * 100f / _validSamples : 0f;
            sb.Append($",{pct:F2}");
        }

        float centerPct = _validSamples > 0 ? _cellCounts[4] * 100f / _validSamples : 0f;
        sb.Append($",{centerPct:F2}");

        File.AppendAllText(_csvPath, sb.ToString() + "\n");
    }

    private void ResetAccumulator()
    {
        for (int i = 0; i < 9; i++) _cellCounts[i] = 0;
        _validSamples = 0;
    }

    private void InitCSV()
    {
        var sb = new StringBuilder();
        sb.Append("block");
        for (int i = 1; i <= 9; i++) sb.Append($",cell{i}_pct");
        sb.Append(",center_pct");
        File.WriteAllText(_csvPath, sb.ToString() + "\n");
    }

    private void BuildGridLines()
    {
        _gridLines = new LineRenderer[4];
        var mat = new Material(Shader.Find("Unlit/Color")) { color = _gridColor };

        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject($"GridLine_{i}");
            go.transform.SetParent(transform);

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = _gridLineWidth;
            lr.endWidth = _gridLineWidth;
            lr.useWorldSpace = true;
            lr.material = mat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            _gridLines[i] = lr;
        }
    }

    private void RefreshGridLines()
    {
        if (_gridLines == null || _cam == null) return;

        foreach (var lr in _gridLines) lr.gameObject.SetActive(_showGrid);
        if (!_showGrid) return;

        foreach (var lr in _gridLines)
        {
            lr.material.color = _gridColor;
            lr.startWidth = _gridLineWidth;
            lr.endWidth = _gridLineWidth;
        }

        _gridLines[0].SetPosition(0, VP2W(1f / 3f, 0f));
        _gridLines[0].SetPosition(1, VP2W(1f / 3f, 1f));

        _gridLines[1].SetPosition(0, VP2W(2f / 3f, 0f));
        _gridLines[1].SetPosition(1, VP2W(2f / 3f, 1f));

        _gridLines[2].SetPosition(0, VP2W(0f, 1f / 3f));
        _gridLines[2].SetPosition(1, VP2W(1f, 1f / 3f));

        _gridLines[3].SetPosition(0, VP2W(0f, 2f / 3f));
        _gridLines[3].SetPosition(1, VP2W(1f, 2f / 3f));
    }

    private Vector3 VP2W(float x, float y) =>
        _cam.ViewportToWorldPoint(new Vector3(x, y, GridDepth));
}