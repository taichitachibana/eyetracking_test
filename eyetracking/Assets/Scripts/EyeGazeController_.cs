using UnityEngine;
using UnityEngine.XR;

public class EyeGazeController_OpenXR : MonoBehaviour
{
    public GameObject RightTargetObject;
    public GameObject LeftTargetObject;
    public Camera RightTargetCamera;
    public Camera LeftTargetCamera;

    private InputDevice eyeDevice;

    void Start()
    {
        eyeDevice = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
    }

    void Update()
    {
        if (!eyeDevice.isValid)
        {
            eyeDevice = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            return;
        }

        if (eyeDevice.TryGetFeatureValue(CommonUsages.leftEyeRotation, out Quaternion leftRot) &&
            eyeDevice.TryGetFeatureValue(CommonUsages.rightEyeRotation, out Quaternion rightRot))
        {
            Vector3 leftDir = leftRot * Vector3.forward;
            Vector3 rightDir = rightRot * Vector3.forward;

            Vector3 leftPos = LeftTargetCamera.transform.position;
            Vector3 rightPos = RightTargetCamera.transform.position;

            if (Physics.Raycast(leftPos, leftDir, out RaycastHit leftHit))
            {
                LeftTargetObject.transform.position = leftHit.point;
            }

            if (Physics.Raycast(rightPos, rightDir, out RaycastHit rightHit))
            {
                RightTargetObject.transform.position = rightHit.point;
            }
        }
    }
}
