using UnityEngine;

public class EyeGazePointer : MonoBehaviour
{
    public Transform eyeGazeRight;
    public Transform eyeGazeLeft;
    public GameObject pointerPrefab;

    private GameObject pointerInstance;

    void Start()
    {
        pointerInstance = Instantiate(pointerPrefab);
        pointerInstance.SetActive(false);
    }

    void Update()
    {
        // 両眼統合
        Vector3 origin =
            (eyeGazeRight.position + eyeGazeLeft.position) * 0.5f;

        Vector3 direction =
            (eyeGazeRight.forward + eyeGazeLeft.forward).normalized;

        // デバッグ用 Ray（Scene View で確認）
        Debug.DrawRay(origin, direction * 3f, Color.red);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, 3f))
        {
            pointerInstance.transform.position = hit.point;
            pointerInstance.SetActive(true);
        }
        else
        {
            pointerInstance.SetActive(false);
        }
    }
}
