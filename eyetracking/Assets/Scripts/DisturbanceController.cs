using UnityEngine;

public class DisturbanceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer _renderer;

    [Header("Button Settings")]
    [SerializeField] private OVRInput.Button _toggleButton = OVRInput.Button.One;

    [Header("Display Duration")]
    [SerializeField] private float _displayDuration = 2.0f;

    [Header("Position")]
    [SerializeField] private float _forwardDistance = 0.30f;
    [SerializeField] private float _verticalOffset = 0.18f;

    [Header("Disturbance Size")]
    [SerializeField, Range(0.01f, 0.3f)] private float _minScale = 0.05f;
    [SerializeField, Range(0.01f, 0.3f)] private float _maxScale = 0.15f;

    [Header("Flicker")]
    [SerializeField] private float _flickerFrequency = 8f;
    [SerializeField] private float _flickerFreqVariation = 3f;
    [SerializeField, Range(0f, 1f)] private float _flickerMin = 0.0f;
    [SerializeField, Range(0f, 1f)] private float _flickerMax = 1.0f;

    [Header("Circle Edge")]
    [SerializeField, Range(0f, 0.5f)] private float _edgeSoftness = 0.1f;

    [Header("Color")]
    [SerializeField] private Color _disturbanceColor = Color.white;

    private Material _mat;
    private Camera _cam;
    private bool _isActive = false;
    private float _timer = 0f;

    private static readonly int ID_Brightness = Shader.PropertyToID("_Brightness");
    private static readonly int ID_EdgeSoftness = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int ID_Color = Shader.PropertyToID("_DisturbanceColor");
    private static readonly int ID_Time = Shader.PropertyToID("_DisturbTime");

    private void Awake()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        _mat = _renderer.material;
        _cam = Camera.main;
        _renderer.enabled = false;
    }

    private void Update()
    {
        if (OVRInput.GetDown(_toggleButton) || Input.GetKeyDown(KeyCode.Space))
        {
            Show();
        }

        if (!_isActive) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f) { Hide(); return; }

        Vector3 forward = _cam.transform.forward;
        Vector3 up = _cam.transform.up;
        transform.position = _cam.transform.position + forward * _forwardDistance + up * _verticalOffset;
        transform.rotation = _cam.transform.rotation;

        float freq = _flickerFrequency + Random.Range(-_flickerFreqVariation, _flickerFreqVariation);
        float sine = Mathf.Sin(Time.time * freq * Mathf.PI * 2f) * 0.5f + 0.5f;
        float bright = Mathf.Lerp(_flickerMin, _flickerMax, sine);

        _mat.SetFloat(ID_Brightness, bright);
        _mat.SetFloat(ID_EdgeSoftness, _edgeSoftness);
        _mat.SetColor(ID_Color, _disturbanceColor);
        _mat.SetFloat(ID_Time, Time.time);
    }

    private void Show()
    {
        float s = Random.Range(_minScale, _maxScale);
        transform.localScale = new Vector3(s, s, 1f);
        _timer = _displayDuration;
        _isActive = true;
        _renderer.enabled = true;
    }

    private void Hide()
    {
        _isActive = false;
        _renderer.enabled = false;
    }

    public void Activate() => Show();
    public void Deactivate() => Hide();
}