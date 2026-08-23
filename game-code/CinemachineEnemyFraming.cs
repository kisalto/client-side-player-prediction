using Unity.Cinemachine;
using UnityEngine;

[ExecuteInEditMode]
[SaveDuringPlay]
public class CinemachineEnemyFraming : CinemachineExtension
{
	[Header("Enemy Detection")]
	[SerializeField]
	private LayerMask enemyLayer;

	[SerializeField]
	private float detectionRadius = 15f;

	[SerializeField]
	private float maxEnemyInfluenceDistance = 10f;

	[Header("Camera Offset Settings")]
	[SerializeField]
	private float maxOffsetDistance = 3f;

	[SerializeField]
	private float offsetStrength = 0.5f;

	[SerializeField]
	private float transitionSpeed = 2f;

	[Header("Player Safety")]
	[SerializeField]
	private float playerMinScreenDistance = 0.3f;

	[SerializeField]
	private bool keepPlayerCentered;

	[Header("Debug")]
	[SerializeField]
	private bool showDebugGizmos = true;

	[SerializeField]
	private Color detectionRangeColor = new Color(1f, 0.5f, 0f, 0.3f);

	[SerializeField]
	private Color enemyLineColor = Color.red;

	private Transform playerTransform;

	private Vector3 currentOffset;

	private Vector3 targetOffset;

	private Vector3 closestEnemyPosition;

	private bool hasNearbyEnemies;

	private CinemachineBossFraming bossFraming;

	protected override void Awake()
	{
		base.Awake();
		bossFraming = GetComponent<CinemachineBossFraming>();
		if ((int)enemyLayer == 0)
		{
			enemyLayer = LayerMask.GetMask("Enemy");
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
				UpdateEnemyDetection();
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

	private void UpdateEnemyDetection()
	{
		Collider2D[] array = Physics2D.OverlapCircleAll(playerTransform.position, detectionRadius, enemyLayer);
		hasNearbyEnemies = false;
		float num = float.MaxValue;
		Vector3 vector = Vector3.zero;
		Collider2D[] array2 = array;
		foreach (Collider2D collider2D in array2)
		{
			if (collider2D == null || !collider2D.gameObject.activeInHierarchy)
			{
				continue;
			}
			EnemyController component = collider2D.GetComponent<EnemyController>();
			if (!(component == null) && component.IsAlive)
			{
				float num2 = Vector2.Distance(playerTransform.position, collider2D.transform.position);
				if (num2 < num && num2 <= maxEnemyInfluenceDistance)
				{
					num = num2;
					vector = collider2D.transform.position;
					hasNearbyEnemies = true;
				}
			}
		}
		if (hasNearbyEnemies)
		{
			closestEnemyPosition = vector;
		}
	}

	private void UpdateCameraOffset(float deltaTime)
	{
		if (hasNearbyEnemies)
		{
			Vector3 vector = closestEnemyPosition - playerTransform.position;
			Vector3 normalized = vector.normalized;
			float magnitude = vector.magnitude;
			float num = 1f - Mathf.Clamp01(magnitude / maxEnemyInfluenceDistance);
			float num2 = Mathf.Clamp(vector.magnitude * offsetStrength * num, 0f, maxOffsetDistance);
			targetOffset = normalized * num2;
			if (keepPlayerCentered)
			{
				targetOffset *= 0.5f;
			}
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
			Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
			Gizmos.DrawWireSphere(playerTransform.position, maxEnemyInfluenceDistance);
			if (hasNearbyEnemies && Application.isPlaying)
			{
				Gizmos.color = enemyLineColor;
				Gizmos.DrawLine(playerTransform.position, closestEnemyPosition);
				Gizmos.DrawWireSphere(closestEnemyPosition, 0.5f);
				Vector3 vector = playerTransform.position + currentOffset;
				Gizmos.color = Color.cyan;
				Gizmos.DrawLine(playerTransform.position, vector);
				Gizmos.DrawWireSphere(vector, 0.3f);
			}
		}
	}

	private void OnDrawGizmosSelected()
	{
		if (showDebugGizmos)
		{
			if (!(playerTransform != null))
			{
				_ = base.transform;
			}
			else
			{
				_ = playerTransform;
			}
		}
	}
}
