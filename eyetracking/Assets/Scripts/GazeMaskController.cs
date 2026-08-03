using UnityEngine;

public class GazeMaskController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _gazeTarget;   // GazeSphere

    [Header("Mask Settings")]
    [SerializeField] private float _radius = 0.25f;
    [SerializeField] private float _softness = 0.15f;
    [SerializeField] private float _opacity = 1.0f;
    [SerializeField, Min(0.01f)] private float _horizontalScale = 1.5f;

    [Header("Shrink Settings")]
    [SerializeField] private bool _enableShrink = false;
    [SerializeField, Range(0.1f, 179f)] private float _initialVisibleFov = 90f;
    [SerializeField, Range(0.1f, 179f)] private float _finalVisibleFov = 18.3f;
    [SerializeField] private float _shrinkDuration = 600f;

    private Material _mat;
    private Camera _cam;
    private float _elapsed = 0f;

    private void Awake()
    {
        _mat = GetComponent<Renderer>().material;
        _cam = Camera.main;
        _enableShrink = false;
        _elapsed = 0f;
        _radius = VisibleFovToViewportRadius(_finalVisibleFov);
    }

    private void OnEnable()
    {
        ExperimentEventLogger.RecordingStarted += StartShrink;
    }

    private void OnDisable()
    {
        ExperimentEventLogger.RecordingStarted -= StartShrink;
    }

    private void Update()
    {
        // Enter繧ｭ繝ｼ縺ｧ繝ｪ繧ｻ繝・ヨ
        // 繧ｫ繝｡繝ｩ縺ｮ豁｣髱｢縺ｫ霑ｽ蠕・
        transform.position = _cam.transform.position + _cam.transform.forward * 0.31f;
        transform.rotation = _cam.transform.rotation;

        // 譎る俣邵ｮ蟆上ぐ繝溘ャ繧ｯ
        if (_enableShrink)
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _shrinkDuration);
            float visibleFov = Mathf.Lerp(_initialVisibleFov, _finalVisibleFov, t);
            _radius = VisibleFovToViewportRadius(visibleFov);
        }

        // 隕也ｷ壹ち繝ｼ繧ｲ繝・ヨ繧偵せ繧ｯ繝ｪ繝ｼ繝ｳUV縺ｫ螟画鋤
        if (_gazeTarget != null && _cam != null)
        {
            Vector3 screen = _cam.WorldToViewportPoint(_gazeTarget.position);
            _mat.SetVector("_GazePos", new Vector4(screen.x, screen.y, 0, 0));
        }

        _mat.SetFloat("_Radius", _radius);
        _mat.SetFloat("_Softness", _softness);
        _mat.SetFloat("_Opacity", _opacity);
        _mat.SetFloat("_HorizontalScale", _horizontalScale);
    }

    private void StartShrink()
    {
        _enableShrink = true;
        _elapsed = 0f;
        _radius = VisibleFovToViewportRadius(_initialVisibleFov);
    }

    // 可視領域の直径（角度）を、シェーダーで使う画面UV半径へ変換する。
    private float VisibleFovToViewportRadius(float visibleFov)
    {
        if (_cam == null) return _radius;

        float verticalFov = _cam.fieldOfView;
        if (_cam.stereoEnabled)
        {
            Matrix4x4 projection =
                _cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
            verticalFov = 2f * Mathf.Atan(1f / projection.m11) * Mathf.Rad2Deg;
        }

        return Mathf.Tan(visibleFov * 0.5f * Mathf.Deg2Rad) /
               (2f * Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad));
    }
}
