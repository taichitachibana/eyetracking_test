using UnityEngine;

public class EyeGazeCombiner : MonoBehaviour
{
    [Header("参照設定")]
    [SerializeField] private Transform _leftEye;
    [SerializeField] private Transform _rightEye;

    [Header("デバッグ")]
    [SerializeField] private bool _showDebugRay = true;

    private void LateUpdate()
    {
        if (_leftEye == null || _rightEye == null) return;

        // 1. 位置の合成（単純な平均）
        // 左目の位置と右目の位置の中間点を取ります
        Vector3 centerPos = (_leftEye.position + _rightEye.position) * 0.5f;
        transform.position = centerPos;

        // 2. 回転の合成（球面線形補間）
        // 左目の向きと右目の向きの中間（0.5）を取ります
        // Slerpを使うことで、回転として自然な中間値になります
        Quaternion centerRot = Quaternion.Slerp(_leftEye.rotation, _rightEye.rotation, 0.5f);
        transform.rotation = centerRot;

        // デバッグ用の線（確認用）
        if (_showDebugRay)
        {
            Debug.DrawRay(transform.position, transform.forward * 3f, Color.cyan);
        }
    }
}