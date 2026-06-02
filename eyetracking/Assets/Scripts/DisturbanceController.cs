using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class DisturbanceController : MonoBehaviour
{
    public enum FlowMode { ForwardToBack, BackToForward }

    [Header("Button")]
    [SerializeField] private OVRInput.Button _toggleButton = OVRInput.Button.One;

    [Header("Timing")]
    [SerializeField] private float _displayDuration = 3.0f;
    [SerializeField] private float _interval = 60.0f;

    [Header("Flow")]
    [SerializeField] private bool _randomizeMode = true;
    [SerializeField] private FlowMode _fixedMode = FlowMode.ForwardToBack;
    [SerializeField] private float _minSpeed = 1.0f;
    [SerializeField] private float _maxSpeed = 2.5f;

    [Header("Sphere Shell")]
    [SerializeField] private float _innerRadius = 2.5f;
    [SerializeField] private float _outerRadius = 4.0f;

    [Header("Peripheral Mask")]
    [SerializeField] private float _centerExcludeDeg = 15.0f;
    [SerializeField] private float _verticalOffsetDeg = -10.0f;

    [Header("Particles")]
    [SerializeField] private int _particleCount = 200;
    [SerializeField] private float _minSize = 0.02f;
    [SerializeField] private float _maxSize = 0.06f;
    [SerializeField] private Color _particleColor = Color.white;

    [Header("Vection")]
    [Tooltip("距離による速さの補正強度。1=線形補正、0=補正なし")]
    [SerializeField, Range(0f, 1f)] private float _speedDepthScale = 1.0f;
    [Tooltip("距離によるサイズの補正強度。1=線形補正、0=補正なし")]
    [SerializeField, Range(0f, 1f)] private float _sizeDepthScale = 1.0f;

    private ParticleSystem _ps;
    private ParticleSystem.Particle[] _particles;

    // 点の位置・速度をカメラローカル座標で管理
    private Vector3[] _localPositions;
    private float[] _speeds;

    private Camera _cam;

    private bool _sessionActive = false;
    private bool _presenting = false;
    private float _timer = 0f;
    private float _intervalTimer = 0f;
    private FlowMode _currentMode;

    // ローカル座標での流れ方向（ForwardToBack = -Z、BackToForward = +Z）
    private Vector3 _localFlowDir;

    private void Awake()
    {
        _cam = Camera.main;
        _ps = GetComponent<ParticleSystem>();
        _particles = new ParticleSystem.Particle[_particleCount];
        _localPositions = new Vector3[_particleCount];
        _speeds = new float[_particleCount];
        ConfigureParticleSystem();
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Update()
    {
        if (OVRInput.GetDown(_toggleButton) || Input.GetKeyDown(KeyCode.Space))
        {
            if (_sessionActive) StopSession();
            else StartSession();
        }

        if (!_sessionActive) return;

        if (_presenting)
        {
            _timer -= Time.deltaTime;
            UpdateParticles();
            if (_timer <= 0f) EndPresentation();
        }
        else
        {
            _intervalTimer -= Time.deltaTime;
            if (_intervalTimer <= 0f) BeginPresentation();
        }
    }

    private void StartSession()
    {
        _sessionActive = true;
        BeginPresentation();
    }

    private void StopSession()
    {
        _sessionActive = false;
        EndPresentation();
    }

    private void BeginPresentation()
    {
        _presenting = true;
        _timer = _displayDuration;
        _intervalTimer = _interval;

        _currentMode = _randomizeMode ? (FlowMode)Random.Range(0, 2) : _fixedMode;
        _localFlowDir = (_currentMode == FlowMode.ForwardToBack) ? -Vector3.forward : Vector3.forward;

        InitParticles();
        _ps.Play();
    }

    private void EndPresentation()
    {
        _presenting = false;
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void InitParticles()
    {
        _ps.Emit(_particleCount);
        int count = _ps.GetParticles(_particles);

        for (int i = 0; i < count; i++)
        {
            _speeds[i] = Random.Range(_minSpeed, _maxSpeed);
            ResetParticle(i, scatter: true);
        }

        ApplyLocalToWorld(count);
        _ps.SetParticles(_particles, count);
    }

    private void UpdateParticles()
    {
        int count = _ps.GetParticles(_particles);

        for (int i = 0; i < count; i++)
        {
            // 距離に反比例した速さでローカル座標移動（近いほど速く）
            float dist0 = _localPositions[i].magnitude;
            float tNorm = Mathf.Clamp01((dist0 - _innerRadius) / (_outerRadius - _innerRadius));
            float speedMul = Mathf.Lerp(2.0f, 0.5f, tNorm);  // innerRadius側=2倍、outerRadius側=0.5倍
            float effectiveSpeed = _speeds[i] * Mathf.Lerp(1.0f, speedMul, _speedDepthScale);
            _localPositions[i] += _localFlowDir * effectiveSpeed * Time.deltaTime;

            // 中心視野侵入チェック（ローカルZのXY成分の角度）
            float angle = Vector3.Angle(Vector3.forward, _localPositions[i]);
            bool inCenter = angle < _centerExcludeDeg;

            // 球殻範囲チェック
            float dist = _localPositions[i].magnitude;
            bool outOfBounds = dist < _innerRadius || dist > _outerRadius;

            if (inCenter || outOfBounds)
                ResetParticle(i, scatter: false);
        }

        ApplyLocalToWorld(count);
        _ps.SetParticles(_particles, count);
    }

    // ローカル座標をワールド座標に変換してParticleSystemに反映
    private void ApplyLocalToWorld(int count)
    {
        for (int i = 0; i < count; i++)
            _particles[i].position = _cam.transform.TransformPoint(_localPositions[i]);
    }

    private void ResetParticle(int i, bool scatter)
    {
        Vector3 localDir = RandomPeripheralLocalDirection();
        float radius = Random.Range(_innerRadius, _outerRadius);
        Vector3 pos = localDir * radius;

        if (scatter)
        {
            float travelRange = _outerRadius - _innerRadius;
            pos += _localFlowDir * Random.Range(0f, travelRange);

            // 球殻内に収める
            float d = pos.magnitude;
            if (d > _outerRadius) pos = pos.normalized * _outerRadius;
            if (d < _innerRadius) pos = pos.normalized * _innerRadius;

            // 中心視野に入っていたら再抽選
            if (Vector3.Angle(Vector3.forward, pos) < _centerExcludeDeg)
                pos = localDir * radius;
        }

        _localPositions[i] = pos;

        // 距離に反比例したサイズ（近いほど大きく）
        float distN = Mathf.Clamp01((pos.magnitude - _innerRadius) / (_outerRadius - _innerRadius));
        float sizeMul = Mathf.Lerp(2.0f, 0.5f, distN);
        float baseSize = Random.Range(_minSize, _maxSize);
        _particles[i].startSize = baseSize * Mathf.Lerp(1.0f, sizeMul, _sizeDepthScale);
        _particles[i].startColor = _particleColor;
        _particles[i].remainingLifetime = float.MaxValue;
    }

    // カメラローカル座標系で周辺視野帯内のランダム方向を返す
    private Vector3 RandomPeripheralLocalDirection()
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            Vector3 dir = Random.onUnitSphere;
            float angle = Vector3.Angle(Vector3.forward, dir);
            if (angle >= _centerExcludeDeg) return dir;
        }
        return Vector3.right;
    }

    private void ConfigureParticleSystem()
    {
        var main = _ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = _particleCount;
        main.startLifetime = float.MaxValue;
        main.startSpeed = 0f;
        main.startSize = _minSize;
        main.startColor = _particleColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = _ps.emission;
        emission.enabled = false;

        var shape = _ps.shape;
        shape.enabled = false;
    }
}