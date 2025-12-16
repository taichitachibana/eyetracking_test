using UnityEngine;

public class EyeGazePointAR : MonoBehaviour
{
    public Transform rightEye;
    public Transform leftEye;
    public GameObject gazePointPrefab;

    GameObject gazePoint;

    void Start()
    {
        gazePoint = Instantiate(gazePointPrefab);
    }

    void Update()
    {
        Vector3 origin =
            (rightEye.position + leftEye.position) * 0.5f;

        Vector3 direction =
            (rightEye.forward + leftEye.forward).normalized;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, 10f))
        {
            gazePoint.transform.position = hit.point;
            gazePoint.SetActive(true);
        }
        else
        {
            gazePoint.SetActive(false);
        }
        Debug.DrawRay(origin, direction * 2f, Color.red);

    }
}
