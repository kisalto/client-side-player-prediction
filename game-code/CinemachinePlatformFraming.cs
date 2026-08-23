using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteInEditMode]
[SaveDuringPlay]
public class CinemachinePlatformFraming : CinemachineExtension
{
	[Header("Platform Reference")]
	[SerializeField]
	private MovingPlatform movingPlatform;

	[Tooltip("The platform Cinemachine camera whose orthographic size and position are used as the blend target.")]
	[SerializeField]
	private CinemachineCamera platformCamera;

	[Header("Framing Settings")]
	[Tooltip("If true, the camera position will blend toward the platform camera's world position.")]
	[SerializeField]
	private bool adjustPosition = true;

	[Tooltip("Extra orthographic size added during the transition. Peaks at mid-blend for a smooth zoom-out-then-in feel.")]
	[SerializeField]
	private float transitionZoomOut = 1.5f;

	[Header("Transition")]
	[Tooltip("Approximate time in seconds for the blend to reach the platform camera.")]
	[SerializeField]
	private float transitionDuration = 0.6f;

	[Tooltip("Approximate time in seconds for the blend to return to normal after leaving the platform.")]
	[SerializeField]
	private float returnDuration = 0.8f;

	[Tooltip("How close the blend factor must be to zero before pixel-perfect is re-enabled.")]
	[SerializeField]
	private float settleThreshold = 0.005f;

	private PixelPerfectCamera urpPixelPerfect;

	private CinemachinePixelPerfect cinemachinePixelPerfect;

	private bool isPixelPerfectDisabled;

	private float cachedPPOrtho;

	private float blendFactor;

	private float blendVelocity;

	public bool IsFramingActive => IsPlatformActive();

	public bool IsFullySettled => blendFactor < settleThreshold;

	protected override void Awake()
	{
		base.Awake();
		cinemachinePixelPerfect = GetComponent<CinemachinePixelPerfect>();
	}

	private void Start()
	{
		if (Camera.main != null)
		{
			urpPixelPerfect = Camera.main.GetComponent<PixelPerfectCamera>();
		}
	}

	private void Update()
	{
		if (urpPixelPerfect == null)
		{
			return;
		}
		bool flag = IsPlatformActive();
		if (flag && !isPixelPerfectDisabled)
		{
			if (Camera.main != null)
			{
				cachedPPOrtho = Camera.main.orthographicSize;
			}
			urpPixelPerfect.enabled = false;
			isPixelPerfectDisabled = true;
		}
		else if (!flag && isPixelPerfectDisabled && IsFullySettled)
		{
			urpPixelPerfect.enabled = true;
			isPixelPerfectDisabled = false;
		}
		if (cinemachinePixelPerfect != null && isPixelPerfectDisabled)
		{
			float num = SmoothBlend();
			cinemachinePixelPerfect.PixelSnapBlend = 1f - num;
		}
		else if (cinemachinePixelPerfect != null && !isPixelPerfectDisabled)
		{
			cinemachinePixelPerfect.PixelSnapBlend = 1f;
		}
	}

	protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
	{
		if (stage != CinemachineCore.Stage.Finalize || platformCamera == null)
		{
			return;
		}
		if (deltaTime < 0f)
		{
			blendFactor = 0f;
			blendVelocity = 0f;
			return;
		}
		bool num = IsPlatformActive();
		float num2 = (num ? 1f : 0f);
		float smoothTime = (num ? transitionDuration : returnDuration);
		blendFactor = Mathf.SmoothDamp(blendFactor, num2, ref blendVelocity, smoothTime);
		if (Mathf.Abs(blendFactor - num2) < 0.001f)
		{
			blendFactor = num2;
			blendVelocity = 0f;
		}
		float num3 = SmoothBlend();
		if (isPixelPerfectDisabled)
		{
			float orthographicSize = platformCamera.Lens.OrthographicSize;
			float num4 = 4f * num3 * (1f - num3);
			float num5 = transitionZoomOut * num4;
			LensSettings lens = state.Lens;
			lens.OrthographicSize = Mathf.Lerp(cachedPPOrtho, orthographicSize, num3) + num5;
			state.Lens = lens;
		}
		if (!(num3 < 0.001f) && adjustPosition)
		{
			Vector3 position = platformCamera.transform.position;
			position.z = state.RawPosition.z;
			state.RawPosition = Vector3.Lerp(state.RawPosition, position, num3);
		}
	}

	private float SmoothBlend()
	{
		return Mathf.SmoothStep(0f, 1f, blendFactor);
	}

	private bool IsPlatformActive()
	{
		if (movingPlatform != null && platformCamera != null)
		{
			return movingPlatform.IsPlayerOnPlatform;
		}
		return false;
	}

	private void OnDisable()
	{
		if (urpPixelPerfect != null && isPixelPerfectDisabled)
		{
			urpPixelPerfect.enabled = true;
			isPixelPerfectDisabled = false;
		}
		if (cinemachinePixelPerfect != null)
		{
			cinemachinePixelPerfect.PixelSnapBlend = 1f;
		}
	}
}
