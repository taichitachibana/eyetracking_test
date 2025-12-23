using UnityEngine;

public class EyeGazeController_ : MonoBehaviour
{
    private OVRPlugin.EyeGazesState EyeGazeState;
    public GameObject RightTargetObject;
    public GameObject LeftTargetObject;
    public Camera RightTargetCamera;
    public Camera LeftTargetCamera;
    void Update()
    {

        if (OVRPlugin.GetEyeGazesState(OVRPlugin.Step. Render, -1, ref EyeGazeState))
        {
            var LeftEyegaze = EyeGazeState.EyeGazes[(int)OVRPlugin.Eye.Left];
            var RightEyegaze = EyeGazeState.EyeGazes[(int)OVRPlugin.Eye.Right];

            if (LeftEyegaze.IsValid)
            {
                var LeftPose = LeftEyegaze.Pose.ToOVRPose();
                var RightPose = RightEyegaze.Pose.ToOVRPose();

                Vector3 GazeLeftDirection = LeftPose. orientation * Vector3. forward;
                Vector3 GazeRightDirection = RightPose. orientation* Vector3. forward;

                Vector3 GazeLeftPosition = LeftTargetCamera.transform.position;
                Vector3 GazeRightPosition = RightTargetCamera.transform.position;

                if (Physics.Raycast(GazeLeftPosition, GazeLeftDirection, out RaycastHit lefthitinfo))
                {
                    LeftTargetObject. transform. position = lefthitinfo. point;
                }
                if (Physics.Raycast(GazeRightPosition, GazeRightDirection, out RaycastHit righthitinfo))
                {
                    RightTargetObject.transform.position = righthitinfo. point;
                }
            }
        }

    }
}
