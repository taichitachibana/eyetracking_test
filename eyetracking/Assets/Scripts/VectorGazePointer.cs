using UnityEngine;

public class VectorGazePointer : MonoBehaviour
{
    [Header("参照設定")]
    [SerializeField] private Transform _pointerObject; // 動かしたいカーソル
    [SerializeField] private Transform _eyeTracker;    // OVREyeGazeがついているオブジェクト

    [Header("挙動設定")]
    [SerializeField] private float _distance = 3.0f;   // 目の前から何メートル先に表示するか
    [SerializeField] private float _smoothSpeed = 30f; // 追従の滑らかさ（高いほどキビキビ）

    private void LateUpdate()
    {
        // 安全対策: 参照が外れていたら何もしない
        if (_pointerObject == null || _eyeTracker == null) return;

        // =========================================================
        // 【核心部分】ベクトル計算による座標の算出
        // =========================================================
        // 計算式: 「目の位置」から「視線の方向」に「指定した距離」だけ進んだ場所
        //
        // Origin (原点) = _eyeTracker.position
        // Direction (方向) = _eyeTracker.forward
        // Distance (距離) = _distance
        // 
        // Target = Origin + (Direction * Distance)
        // =========================================================

        Vector3 rayOrigin = _eyeTracker.position;
        Vector3 rayDirection = _eyeTracker.forward;

        // 仮想のターゲット座標を計算（これが透明な球体の表面と同じ意味になります）
        Vector3 targetPosition = rayOrigin + (rayDirection * _distance);

        // =========================================================
        // 反映処理
        // =========================================================

        // 1. 位置の更新（Lerpを使って滑らかに移動させる）
        _pointerObject.position = Vector3.Lerp(_pointerObject.position, targetPosition, Time.deltaTime * _smoothSpeed);

        // 2. 回転の更新（ポインターを常にカメラの方に向ける）
        // これをしないと、ポインターが真横を向いたりして見えにくくなります
        _pointerObject.LookAt(rayOrigin);
    }
}