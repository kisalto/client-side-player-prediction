using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteInEditMode]
[SaveDuringPlay]
public class CinemachineBossFraming : CinemachineExtension
{
	[Header("Boss Reference")]
	[SerializeField]
	private BigBobBoss boss;

	[Header("Framing Settings")]
	[Tooltip("Multiplier applied to the combined player+boss bounds. 1 = pixel-tight, 1.1 = slight breathing room.")]
	[SerializeField]
	private float padding = 1.25f;

	[Tooltip("The orthographic size will never go below this value.")]
	[SerializeField]
	private float minOrthographicSize = 2f;

	[Tooltip("The orthographic size will never exceed this value when zooming out.")]
	[SerializeField]
	private float maxOrthographicSize = 14f;

	[Header("Tracking Smoothing")]
	[Tooltip("SmoothDamp time for the camera position tracking the player+boss center. Higher = smoother but slower to follow.")]
	[SerializeField]
	private float positionSmoothTime = 0.15f;

	[Tooltip("SmoothDamp time for the orthographic size adjustments.")]
	[SerializeField]
	private float orthoSmoothTime = 0.2f;

	[Header("Transition")]
	[Tooltip("Approximate time in seconds for the blend to reach the boss framing.")]
	[SerializeField]
	private float transitionDuration = 1f;

	[Tooltip("Approximate time in seconds for the blend to return to normal after the boss fight ends.")]
	[SerializeField]
	private float returnDuration = 1.2f;

	[Tooltip("How close the blend factor must be to zero before pixel-perfect is re-enabled.")]
	[SerializeField]
	private float settleThreshold = 0.05f;

	[Header("Debug")]
	[SerializeField]
	private bool showDebugGizmos = true;

	private Transform playerTransform;

	private SpriteRenderer playerRenderer;

	private SpriteRenderer bossRenderer;

	private CinemachinePixelPerfect cinemachinePixelPerfect;

	private CinemachinePlatformFraming platformFraming;

	private PixelPerfectCamera urpPixelPerfect;

	private float blendFactor;

	private float blendVelocity;

	private float cachedPPOrtho;

	private bool isPixelPerfectDisabled;

	private Vector3 rawBossPosition;

	private float rawBossOrtho;

	private Vector3 smoothedBossPosition;

	private float smoothedBossOrtho;

	private Vector3 positionVelocity;

	private float orthoVelocity;

	private bool hasInitializedSmoothing;

	private bool pendingSnapBake;

	private Vector3 snapCorrectionCarryOver;

	public bool IsBossFramingActive => IsBossZoomActive();

	public bool IsFullySettled => blendFactor < settleThreshold;

	protected override void Awake()
	{
		base.Awake();
		cinemachinePixelPerfect = GetComponent<CinemachinePixelPerfect>();
		platformFraming = GetComponent<CinemachinePlatformFraming>();
	}

	private void Start()
	{
		CinemachineCamera component = GetComponent<CinemachineCamera>();
		if (component != null && component.Follow != null)
		{
			playerTransform = component.Follow;
			playerRenderer = playerTransform.GetComponentInChildren<SpriteRenderer>();
		}
		if (Camera.main != null)
		{
			urpPixelPerfect = Camera.main.GetComponent<PixelPerfectCamera>();
		}
		TryFindBoss();
	}

	private void Update()
	{
		if (urpPixelPerfect == null)
		{
			return;
		}
		bool flag = (boss != null && boss.IsBossSequenceActive) || BossDialogue.IsDialogueActive;
		bool flag2 = platformFraming != null && platformFraming.IsFramingActive;
		if (flag && !isPixelPerfectDisabled)
		{
			if (Camera.main != null)
			{
				cachedPPOrtho = Camera.main.orthographicSize;
			}
			pendingSnapBake = true;
			urpPixelPerfect.enabled = false;
			isPixelPerfectDisabled = true;
		}
		else if (!flag && !flag2 && isPixelPerfectDisabled && IsFullySettled)
		{
			urpPixelPerfect.enabled = true;
			isPixelPerfectDisabled = false;
			hasInitializedSmoothing = false;
		}
		if (cinemachinePixelPerfect != null)
		{
			if (isPixelPerfectDisabled && !flag2)
			{
				cinemachinePixelPerfect.PixelSnapBlend = 0f;
			}
			else if (!isPixelPerfectDisabled && !flag2)
			{
				cinemachinePixelPerfect.PixelSnapBlend = 1f;
			}
		}
	}

	protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
	{
		if (stage != CinemachineCore.Stage.Finalize)
		{
			return;
		}
		if (playerTransform == null && vcam.Follow != null)
		{
			playerTransform = vcam.Follow;
			playerRenderer = playerTransform.GetComponentInChildren<SpriteRenderer>();
		}
		if (boss == null)
		{
			TryFindBoss();
		}
		if (deltaTime < 0f)
		{
			blendFactor = 0f;
			blendVelocity = 0f;
			snapCorrectionCarryOver = Vector3.zero;
			pendingSnapBake = false;
			positionVelocity = Vector3.zero;
			orthoVelocity = 0f;
			hasInitializedSmoothing = false;
		}
		else
		{
			if (playerTransform == null)
			{
				return;
			}
			if (pendingSnapBake && cinemachinePixelPerfect != null)
			{
				pendingSnapBake = false;
				float num = 1f / cinemachinePixelPerfect.pixelsPerUnit;
				Vector3 rawPosition = state.RawPosition;
				Vector3 vector = default(Vector3);
				vector.x = Mathf.Round(rawPosition.x / num) * num;
				vector.y = Mathf.Round(rawPosition.y / num) * num;
				vector.z = rawPosition.z;
				snapCorrectionCarryOver = vector - rawPosition;
			}
			bool num2 = IsBossZoomActive();
			float num3 = (num2 ? 1f : 0f);
			float smoothTime = (num2 ? transitionDuration : returnDuration);
			blendFactor = Mathf.SmoothDamp(blendFactor, num3, ref blendVelocity, smoothTime);
			if (Mathf.Abs(blendFactor - num3) < 0.001f)
			{
				blendFactor = num3;
				blendVelocity = 0f;
			}
			float num4 = SmoothBlend();
			if (ComputeBossFraming(state, out var targetPosition, out var targetOrthoSize))
			{
				rawBossPosition = targetPosition;
				rawBossOrtho = targetOrthoSize;
				if (!hasInitializedSmoothing)
				{
					smoothedBossPosition = state.RawPosition;
					smoothedBossOrtho = ((cachedPPOrtho > 0f) ? cachedPPOrtho : state.Lens.OrthographicSize);
					positionVelocity = Vector3.zero;
					orthoVelocity = 0f;
					hasInitializedSmoothing = true;
				}
				else
				{
					smoothedBossPosition = Vector3.SmoothDamp(smoothedBossPosition, rawBossPosition, ref positionVelocity, positionSmoothTime);
					smoothedBossOrtho = Mathf.SmoothDamp(smoothedBossOrtho, rawBossOrtho, ref orthoVelocity, orthoSmoothTime);
				}
			}
			if (snapCorrectionCarryOver.sqrMagnitude > 1E-05f)
			{
				state.RawPosition += snapCorrectionCarryOver * (1f - num4);
				if (num4 > 0.99f)
				{
					snapCorrectionCarryOver = Vector3.zero;
				}
			}
			if (isPixelPerfectDisabled && num4 > 0.001f)
			{
				LensSettings lens = state.Lens;
				lens.OrthographicSize = Mathf.Lerp(cachedPPOrtho, smoothedBossOrtho, num4);
				state.Lens = lens;
				Vector3 b = smoothedBossPosition;
				b.z = state.RawPosition.z;
				state.RawPosition = Vector3.Lerp(state.RawPosition, b, num4);
			}
			else if (isPixelPerfectDisabled)
			{
				LensSettings lens2 = state.Lens;
				lens2.OrthographicSize = cachedPPOrtho;
				state.Lens = lens2;
			}
		}
	}

	private float SmoothBlend()
	{
		return Mathf.SmoothStep(0f, 1f, blendFactor);
	}

	private bool IsBossZoomActive()
	{
		if (!(boss != null) || !boss.IsBossActive)
		{
			return BossDialogue.IsDialogueActive;
		}
		return true;
	}

	private void TryFindBoss()
	{
		if (boss == null)
		{
			boss = Object.FindFirstObjectByType<BigBobBoss>();
		}
		if (boss != null && bossRenderer == null)
		{
			bossRenderer = boss.GetComponent<SpriteRenderer>();
		}
	}

	private bool ComputeBossFraming(CameraState state, out Vector3 targetPosition, out float targetOrthoSize)
	{
		targetPosition = state.RawPosition;
		targetOrthoSize = state.Lens.OrthographicSize;
		if (bossRenderer == null)
		{
			if (boss != null)
			{
				bossRenderer = boss.GetComponent<SpriteRenderer>();
			}
			if (bossRenderer == null)
			{
				return false;
			}
		}
		Bounds bounds = bossRenderer.bounds;
		Bounds bounds2 = ((playerRenderer != null) ? playerRenderer.bounds : new Bounds(playerTransform.position, Vector3.zero));
		float num = Mathf.Min(bounds2.min.x, bounds.min.x);
		float num2 = Mathf.Max(bounds2.max.x, bounds.max.x);
		float num3 = Mathf.Min(bounds2.min.y, bounds.min.y);
		float num4 = Mathf.Max(bounds2.max.y, bounds.max.y);
		float x = (num + num2) * 0.5f;
		float y = (num3 + num4) * 0.5f;
		targetPosition = new Vector3(x, y, state.RawPosition.z);
		float num5 = (num2 - num) * 0.5f * padding;
		float a = (num4 - num3) * 0.5f * padding;
		float aspect = state.Lens.Aspect;
		targetOrthoSize = Mathf.Max(a, num5 / aspect);
		targetOrthoSize = Mathf.Clamp(targetOrthoSize, minOrthographicSize, maxOrthographicSize);
		return true;
	}

	private void OnDrawGizmos()
	{
		if (showDebugGizmos && Application.isPlaying && !(boss == null) && !(bossRenderer == null) && boss.IsBossSequenceActive)
		{
			Bounds bounds = bossRenderer.bounds;
			Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
			Gizmos.DrawWireCube(bounds.center, bounds.size);
			if (playerRenderer != null)
			{
				Gizmos.color = new Color(0f, 0.6f, 1f, 0.5f);
				Gizmos.DrawWireCube(playerRenderer.bounds.center, playerRenderer.bounds.size);
			}
			if (playerTransform != null)
			{
				Gizmos.color = Color.yellow;
				Gizmos.DrawLine(playerTransform.position, bounds.center);
			}
		}
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
