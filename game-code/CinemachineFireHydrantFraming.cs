using Unity.Cinemachine;
using UnityEngine;

[ExecuteInEditMode]
[SaveDuringPlay]
public class CinemachineFireHydrantFraming : CinemachineExtension
{
	[Header("Fire Hydrant Detection")]
	[SerializeField]
	private LayerMask fireHydrantLayer;

	[SerializeField]
	private string fireHydrantTag = "FireHydrant";

	[SerializeField]
	private float detectionRadius = 8f;

	[SerializeField]
	private float activeUseDistance = 3f;

	[Header("Camera Framing Settings")]
	[SerializeField]
	private float frameOffsetDistance = 2f;

	[SerializeField]
	private float frameStrength = 0.6f;

	[SerializeField]
	private float transitionSpeed = 3f;

	[Header("Player Safety")]
	[SerializeField]
	private float playerMinScreenDistance = 0.35f;

	[Header("Debug")]
	[SerializeField]
	private bool showDebugGizmos = true;

	[SerializeField]
	private Color detectionRangeColor = new Color(0f, 0.8f, 1f, 0.3f);

	[SerializeField]
	private Color hydrantLineColor = Color.cyan;

	private Transform playerTransform;

	private Vector3 currentOffset;

	private Vector3 targetOffset;

	private Vector3 activeHydrantPosition;

	private bool hasNearbyHydrant;

	private GameObject nearestHydrant;

	private CinemachineBossFraming bossFraming;

	protected override void Awake()
	{
		base.Awake();
		bossFraming = GetComponent<CinemachineBossFraming>();
		if ((int)fireHydrantLayer == 0)
		{
			fireHydrantLayer = LayerMask.GetMask("Default");
		}
	}

	private void Start()
	{
		CinemachineCamera component = GetComponent<CinemachineCamera>();
		if (component != null && component.Follow != null)
		{
			playerTransform = component.Follow;
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
		}
		if (playerTransform != null && deltaTime >= 0f)
		{
			if (bossFraming != null && bossFraming.IsBossFramingActive)
			{
				currentOffset = Vector3.Lerp(currentOffset, Vector3.zero, transitionSpeed * deltaTime);
				ApplyOffsetToCamera(ref state);
			}
			else
			{
				UpdateHydrantDetection();
				UpdateCameraOffset(deltaTime);
				ApplyOffsetToCamera(ref state);
			}
		}
		else if (deltaTime < 0f)
		{
			currentOffset = Vector3.zero;
			targetOffset = Vector3.zero;
		}
	}

	private void UpdateHydrantDetection()
	{
		GameObject[] array = GameObject.FindGameObjectsWithTag(fireHydrantTag);
		hasNearbyHydrant = false;
		float num = float.MaxValue;
		GameObject gameObject = null;
		GameObject[] array2 = array;
		foreach (GameObject gameObject2 in array2)
		{
			if (!(gameObject2 == null) && gameObject2.activeInHierarchy)
			{
				float num2 = Vector2.Distance(playerTransform.position, gameObject2.transform.position);
				if (num2 < num && num2 <= detectionRadius)
				{
					num = num2;
					gameObject = gameObject2;
					hasNearbyHydrant = true;
				}
			}
		}
		if (hasNearbyHydrant && num <= activeUseDistance)
		{
			nearestHydrant = gameObject;
			activeHydrantPosition = gameObject.transform.position;
		}
		else
		{
			nearestHydrant = null;
			hasNearbyHydrant = false;
		}
	}

	private void UpdateCameraOffset(float deltaTime)
	{
		if (hasNearbyHydrant && nearestHydrant != null)
		{
			Vector3 vector = activeHydrantPosition - playerTransform.position;
			Vector3 normalized = vector.normalized;
			float magnitude = vector.magnitude;
			float num = 1f - Mathf.Clamp01(magnitude / activeUseDistance);
			float num2 = frameOffsetDistance * frameStrength * num;
			targetOffset = normalized * num2;
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
			Vector3 rawPosition = state.RawPosition + currentOffset;
			float num = Mathf.Abs(playerTransform.position.x - rawPosition.x);
			float num2 = state.Lens.OrthographicSize * state.Lens.Aspect;
			if (num > num2 * (1f - playerMinScreenDistance))
			{
				float num3 = num2 * (1f - playerMinScreenDistance);
				float num4 = Mathf.Sign(currentOffset.x) * num3;
				rawPosition.x = playerTransform.position.x + num4;
			}
			state.RawPosition = rawPosition;
		}
	}

	private void OnDrawGizmos()
	{
		if (showDebugGizmos && !(playerTransform == null))
		{
			Gizmos.color = detectionRangeColor;
			Gizmos.DrawWireSphere(playerTransform.position, detectionRadius);
			Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
			Gizmos.DrawWireSphere(playerTransform.position, activeUseDistance);
			if (hasNearbyHydrant && nearestHydrant != null && Application.isPlaying)
			{
				Gizmos.color = hydrantLineColor;
				Gizmos.DrawLine(playerTransform.position, activeHydrantPosition);
				Gizmos.DrawWireSphere(activeHydrantPosition, 0.5f);
				Vector3 vector = playerTransform.position + currentOffset;
				Gizmos.color = Color.yellow;
				Gizmos.DrawLine(playerTransform.position, vector);
				Gizmos.DrawWireSphere(vector, 0.3f);
			}
		}
	}
}
