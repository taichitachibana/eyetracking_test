using UnityEngine;

public class HeadFollower : MonoBehaviour
{
    [SerializeField] private Transform _targetToFollow; // 追従対象（CenterEyeAnchor）

    private void LateUpdate()
    {
        if (_targetToFollow != null)
        {
            // 位置（Position）だけコピーする
            transform.position = _targetToFollow.position;

            // 回転（Rotation）はコピーしない！
            // 常にワールド座標の「ゼロ（回転なし）」を保つ
            transform.rotation = Quaternion.identity;
        }
    }
}