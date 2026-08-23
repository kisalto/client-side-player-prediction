using Unity.Cinemachine;
using UnityEngine;

public class CinemachinePixelPerfect : CinemachineExtension
{
	[Tooltip("Pixels per unit - match this to your sprite settings")]
	public float pixelsPerUnit = 16f;

	[Range(0f, 1f)]
	public float PixelSnapBlend = 1f;

	protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
	{
		if (stage == CinemachineCore.Stage.Finalize && !(PixelSnapBlend <= 0f))
		{
			Vector3 finalPosition = state.GetFinalPosition();
			float num = 1f / pixelsPerUnit;
			Vector3 vector = finalPosition;
			vector.x = Mathf.Round(finalPosition.x / num) * num;
			vector.y = Mathf.Round(finalPosition.y / num) * num;
			state.PositionCorrection = (vector - finalPosition) * PixelSnapBlend;
		}
	}
}
