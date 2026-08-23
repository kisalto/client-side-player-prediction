using Unity.Cinemachine;
using UnityEngine;

[ExecuteInEditMode]
[SaveDuringPlay]
public class CinemachineVerticalLookahead : CinemachineExtension
{
	[Header("Vertical Lookahead Settings")]
	[SerializeField]
	private float fallLookaheadDistance = 2f;

	[SerializeField]
	private float riseLookaheadDistance = 1.5f;

	[SerializeField]
	private float transitionSpeed = 3f;

	[Header("Velocity Thresholds")]
	[SerializeField]
	private float minFallVelocity = -2f;

	[SerializeField]
	private float minRiseVelocity = 2f;

	[Header("Debug")]
	[SerializeField]
	private bool showDebugGizmos;

	private Transform playerTransform;

	private Rigidbody2D playerRigidbody;

	private Vector3 currentOffset;

	private Vector3 targetOffset;

	private CinemachineBossFraming bossFraming;

	protected override void Awake()
	{
		base.Awake();
		bossFraming = GetComponent<CinemachineBossFraming>();
	}

	private void Start()
	{
		CinemachineCamera component = GetComponent<CinemachineCamera>();
		if (component != null && component.Follow != null)
		{
			playerTransform = component.Follow;
			playerRigidbody = playerTransform.GetComponent<Rigidbody2D>();
		}
	}

	protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
	{
		if (stage != CinemachineCore.Stage.Body)
		{
			return;
		}
		if (playerTransform == null && vcam.Follow != null)
		{
			playerTransform = vcam.Follow;
			playerRigidbody = playerTransform.GetComponent<Rigidbody2D>();
		}
		if (playerTransform != null && playerRigidbody != null && deltaTime >= 0f)
		{
			if (bossFraming != null && bossFraming.IsBossFramingActive)
			{
				currentOffset = Vector3.Lerp(currentOffset, Vector3.zero, transitionSpeed * deltaTime);
				ApplyOffsetToCamera(ref state);
			}
			else
			{
				UpdateVerticalLookahead(deltaTime);
				ApplyOffsetToCamera(ref state);
			}
		}
		else if (deltaTime < 0f)
		{
			currentOffset = Vector3.zero;
			targetOffset = Vector3.zero;
		}
	}

	private void UpdateVerticalLookahead(float deltaTime)
	{
		float y = playerRigidbody.linearVelocity.y;
		if (y <= minFallVelocity)
		{
			float num = Mathf.Clamp01(Mathf.Abs(y) / 10f);
			float num2 = fallLookaheadDistance * num;
			targetOffset = new Vector3(0f, 0f - num2, 0f);
		}
		else if (y >= minRiseVelocity)
		{
			float num3 = Mathf.Clamp01(y / 10f);
			float y2 = riseLookaheadDistance * num3;
			targetOffset = new Vector3(0f, y2, 0f);
		}
		else
		{
			targetOffset = Vector3.zero;
		}
		currentOffset = Vector3.Lerp(currentOffset, targetOffset, transitionSpeed * deltaTime);
	}

	private void ApplyOffsetToCamera(ref CameraState state)
	{
		if (!(currentOffset.sqrMagnitude < 0.001f))
		{
			state.RawPosition += currentOffset;
		}
	}

	private void OnDrawGizmos()
	{
		if (showDebugGizmos && !(playerTransform == null) && Application.isPlaying)
		{
			Gizmos.color = Color.green;
			Gizmos.DrawLine(playerTransform.position, playerTransform.position + currentOffset);
			Gizmos.DrawWireSphere(playerTransform.position + currentOffset, 0.3f);
			if (playerRigidbody != null)
			{
				Gizmos.color = Color.yellow;
				Vector3 to = playerTransform.position + (Vector3)playerRigidbody.linearVelocity.normalized * 2f;
				Gizmos.DrawLine(playerTransform.position, to);
			}
		}
	}
}
