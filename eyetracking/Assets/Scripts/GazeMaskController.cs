using UnityEngine;

[RequireComponent(typeof(Camera))]
public class GazeMaskController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _gazeTarget;

    [Header("Mask Settings")]
    [SerializeField] private float _radius = 0.25f;
    [SerializeField] private float _softness = 0.15f;
    [SerializeField] private float _opacity = 1.0f;

    [Header("Shrink Settings")]
    [SerializeField] private bool _enableShrink = false;
    [SerializeField] private float _initialRadius = 0.5f;
    [SerializeField] private float _finalRadius = 0.05f;
    [SerializeField] private float _shrinkDuration = 60f;

    private Material _maskMaterial;
    private Camera _camera;
    private float _elapsedTime = 0f;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        var shader = Shader.Find("Custom/GazeMask");
        _maskMaterial = new Material(shader);

        if (_enableShrink)
            _radius = _initialRadius;
    }

    private void Update()
    {
        if (!_enableShrink) return;

        _elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsedTime / _shrinkDuration);
        _radius = Mathf.Lerp(_initialRadius, _finalRadius, t);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (_maskMaterial == null)
        {
            Graphics.Blit(src, dst);
            return;
        }

        if (_gazeTarget != null)
        {
            // ç∂ñ⁄ÇÃç¿ïW
            Matrix4x4 leftView = _camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
            Matrix4x4 leftProj = _camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
            Vector3 leftPos = WorldToViewportStereo(_gazeTarget.position, leftView, leftProj);

            // âEñ⁄ÇÃç¿ïW
            Matrix4x4 rightView = _camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right);
            Matrix4x4 rightProj = _camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
            Vector3 rightPos = WorldToViewportStereo(_gazeTarget.position, rightView, rightProj);

            _maskMaterial.SetVector("_GazePos", new Vector2(leftPos.x, leftPos.y));
            _maskMaterial.SetVector("_GazePosR", new Vector2(rightPos.x, rightPos.y));
        }

        _maskMaterial.SetFloat("_Radius", _radius);
        _maskMaterial.SetFloat("_Softness", _softness);
        _maskMaterial.SetFloat("_Opacity", _opacity);

        Graphics.Blit(src, dst, _maskMaterial);
    }

    private Vector3 WorldToViewportStereo(Vector3 worldPos, Matrix4x4 viewMatrix, Matrix4x4 projMatrix)
    {
        Vector4 clipPos = projMatrix * viewMatrix * new Vector4(worldPos.x, worldPos.y, worldPos.z, 1f);
        if (clipPos.w == 0f) return Vector3.zero;
        Vector3 ndc = new Vector3(clipPos.x / clipPos.w, clipPos.y / clipPos.w, clipPos.z / clipPos.w);
        return new Vector3(ndc.x * 0.5f + 0.5f, ndc.y * 0.5f + 0.5f, ndc.z * 0.5f + 0.5f);
    }
}