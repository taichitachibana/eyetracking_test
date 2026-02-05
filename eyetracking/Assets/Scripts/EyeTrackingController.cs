using UnityEngine;

public class SimpleGazeRaycaster : MonoBehaviour
{
    [Header("参照設定")]
    [SerializeField] private Transform _pointerObject;   // 移動させるカーソル
    [SerializeField] private Transform _eyeTracker;      // ステップ1で作ったEyeTrackerオブジェクト
    [SerializeField] private LayerMask _gazeLayer;       // 透明な壁のレイヤー

    [Header("設定")]
    [SerializeField] private float _maxDistance = 10f;

    private void Update()
    {
        // 難しい計算は不要。EyeTrackerは既に視線の方向を向いているため、
        // 単純にその forward (前方) を使うだけでOKです。

        Vector3 rayOrigin = _eyeTracker.position;
        Vector3 rayDirection = _eyeTracker.forward; // ここが自動更新されています

        // レイキャスト実行
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, _maxDistance, _gazeLayer))
        {
            _pointerObject.position = hit.point;

            // カーソルをカメラの方に向ける
            _pointerObject.LookAt(rayOrigin);
        }
    }
}