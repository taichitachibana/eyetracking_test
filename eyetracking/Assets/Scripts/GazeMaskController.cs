using UnityEngine;

[RequireComponent(typeof(Camera))]
public class GazeMaskController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _gazeTarget; // GazeSphere の Transform

    [Header("Mask Settings")]
    [SerializeField] private float _radius = 0.25f;
    [SerializeField] private float _softness = 0.15f;
    [SerializeField] private float _opacity = 1.0f;

    private Material _maskMaterial;
    private Camera _camera;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        var shader = Shader.Find("Custom/GazeMask");
        _maskMaterial = new Material(shader);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (_gazeTarget != null)
        {
            // GazeSphere のワールド座標 → スクリーンUV(0-1)に変換
            Vector3 screenPos = _camera.WorldToViewportPoint(_gazeTarget.position);
            _maskMaterial.SetVector("_GazePos", new Vector2(screenPos.x, screenPos.y));
        }

        _maskMaterial.SetFloat("_Radius", _radius);
        _maskMaterial.SetFloat("_Softness", _softness);
        _maskMaterial.SetFloat("_Opacity", _opacity);

        Graphics.Blit(src, dst, _maskMaterial);
    }

    [Header("Shrink Settings")]
    [SerializeField] private bool _enableShrink = false;
    [SerializeField] private float _initialRadius = 0.5f;
    [SerializeField] private float _finalRadius = 0.05f;
    [SerializeField] private float _shrinkDuration = 60f;

    private float _elapsedTime = 0f;

    private void Update()
    {
        if (!_enableShrink) return;

        _elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsedTime / _shrinkDuration);
        _radius = Mathf.Lerp(_initialRadius, _finalRadius, t);
    }
}