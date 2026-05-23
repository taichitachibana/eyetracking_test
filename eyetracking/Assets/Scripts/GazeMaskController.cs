using UnityEngine;

public class GazeMaskController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _gazeTarget;   // GazeSphere

    [Header("Mask Settings")]
    [SerializeField] private float _radius = 0.25f;
    [SerializeField] private float _softness = 0.15f;
    [SerializeField] private float _opacity = 1.0f;

    [Header("Shrink Settings")]
    [SerializeField] private bool _enableShrink = false;
    [SerializeField] private float _initialRadius = 0.5f;
    [SerializeField] private float _finalRadius = 0.05f;
    [SerializeField] private float _shrinkDuration = 60f;

    private Material _mat;
    private Camera _cam;
    private float _elapsed = 0f;

    private void Awake()
    {
        _mat = GetComponent<Renderer>().material;
        _cam = Camera.main;
        if (_enableShrink) _radius = _initialRadius;
    }

    private void Update()
    {
        // カメラの正面に追従
        transform.position = _cam.transform.position + _cam.transform.forward * 0.31f;
        transform.rotation = _cam.transform.rotation;

        // 時間縮小ギミック
        if (_enableShrink)
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _shrinkDuration);
            _radius = Mathf.Lerp(_initialRadius, _finalRadius, t);
        }

        // 視線ターゲットをスクリーンUVに変換
        if (_gazeTarget != null && _cam != null)
        {
            Vector3 screen = _cam.WorldToViewportPoint(_gazeTarget.position);
            _mat.SetVector("_GazePos", new Vector4(screen.x, screen.y, 0, 0));
        }

        _mat.SetFloat("_Radius", _radius);
        _mat.SetFloat("_Softness", _softness);
        _mat.SetFloat("_Opacity", _opacity);
    }
}