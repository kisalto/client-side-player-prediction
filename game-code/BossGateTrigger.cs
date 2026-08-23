using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossGateTrigger : MonoBehaviour
{
	[Serializable]
	public class ObjectMovement
	{
		[Tooltip("The object to move")]
		public Transform targetObject;

		[Tooltip("Starting position (leave null to use object's current position)")]
		public Transform startPoint;

		[Tooltip("Ending position")]
		public Transform endPoint;

		[Tooltip("Time to complete the movement")]
		public float duration = 2f;

		[Tooltip("BoxCollider2D to enable (leave null to skip)")]
		public BoxCollider2D gateCollider;

		[Tooltip("When to enable the collider")]
		public ColliderEnableTime enableColliderTime = ColliderEnableTime.OnMovementEnd;

		[Header("Camera Shake Settings")]
		[Tooltip("Enable camera shake when gate reaches end position")]
		public bool enableCameraShake = true;

		[Tooltip("Camera shake amplitude")]
		public float shakeAmplitude = 0.3f;

		[Tooltip("Camera shake frequency")]
		public float shakeFrequency = 2f;

		[Tooltip("Camera shake duration")]
		public float shakeDuration = 0.4f;

		[HideInInspector]
		public Vector3 actualStartPosition;
	}

	public enum ColliderEnableTime
	{
		OnMovementStart,
		OnMovementEnd,
		Never
	}

	[Header("Movement Settings")]
	[SerializeField]
	private List<ObjectMovement> objectsToMove = new List<ObjectMovement>();

	[SerializeField]
	private float exponentialPower = 2f;

	[Header("Trigger Settings")]
	[SerializeField]
	private string playerTag = "Player";

	[Header("Debug")]
	[SerializeField]
	private bool showDebugInfo;

	[SerializeField]
	private bool showGizmos = true;

	private bool hasTriggered;

	private Collider2D triggerCollider;

	private void Awake()
	{
		triggerCollider = GetComponent<Collider2D>();
		if (triggerCollider != null)
		{
			triggerCollider.isTrigger = true;
		}
		foreach (ObjectMovement item in objectsToMove)
		{
			if (!(item.targetObject != null))
			{
				continue;
			}
			item.actualStartPosition = ((item.startPoint != null) ? item.startPoint.position : item.targetObject.position);
			if (item.gateCollider == null)
			{
				item.gateCollider = item.targetObject.GetComponent<BoxCollider2D>();
			}
			if (item.gateCollider != null)
			{
				item.gateCollider.enabled = false;
				if (showDebugInfo)
				{
					Debug.Log("[BossGateTrigger] Disabled collider on " + item.targetObject.name);
				}
			}
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (!hasTriggered && other.CompareTag(playerTag))
		{
			if (showDebugInfo)
			{
				Debug.Log("[BossGateTrigger] Player entered trigger, starting movement");
			}
			ActivateTrigger();
		}
	}

	private void ActivateTrigger()
	{
		hasTriggered = true;
		if (triggerCollider != null)
		{
			triggerCollider.enabled = false;
			if (showDebugInfo)
			{
				Debug.Log("[BossGateTrigger] Disabled trigger collider to prevent re-triggering");
			}
		}
		StartCoroutine(MoveAllObjects());
	}

	public void ReverseMovement()
	{
		if (hasTriggered)
		{
			if (showDebugInfo)
			{
				Debug.Log("[BossGateTrigger] Reversing gate movement");
			}
			StartCoroutine(ReverseAllObjects());
		}
	}

	private IEnumerator ReverseAllObjects()
	{
		List<Coroutine> list = new List<Coroutine>();
		foreach (ObjectMovement item2 in objectsToMove)
		{
			if (item2.targetObject != null)
			{
				Coroutine item = StartCoroutine(ReverseObject(item2));
				list.Add(item);
			}
		}
		foreach (Coroutine item3 in list)
		{
			yield return item3;
		}
		if (showDebugInfo)
		{
			Debug.Log("[BossGateTrigger] All objects finished reversing");
		}
	}

	private IEnumerator ReverseObject(ObjectMovement movement)
	{
		Vector3 startPos = movement.targetObject.position;
		Vector3 endPos = movement.actualStartPosition;
		float elapsed = 0f;
		if (showDebugInfo)
		{
			Debug.Log($"[BossGateTrigger] Reversing {movement.targetObject.name} from {startPos} to {endPos}");
		}
		if (movement.gateCollider != null)
		{
			movement.gateCollider.enabled = false;
			if (showDebugInfo)
			{
				Debug.Log("[BossGateTrigger] Disabled collider on " + movement.targetObject.name + " for reverse movement");
			}
		}
		while (elapsed < movement.duration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Pow(Mathf.Clamp01(elapsed / movement.duration), exponentialPower);
			movement.targetObject.position = Vector3.Lerp(startPos, endPos, t);
			yield return null;
		}
		movement.targetObject.position = endPos;
		SFXManager.Instance?.Play("ENV_FenceOpen", movement.targetObject.position);
		if (movement.enableCameraShake && CameraShakeManager.Instance != null)
		{
			CameraShakeManager.Instance.ShakeCamera(movement.shakeAmplitude, movement.shakeFrequency, movement.shakeDuration);
			if (showDebugInfo)
			{
				Debug.Log("[BossGateTrigger] Triggered camera shake for " + movement.targetObject.name);
			}
		}
		if (showDebugInfo)
		{
			Debug.Log("[BossGateTrigger] " + movement.targetObject.name + " finished reversing");
		}
	}

	private IEnumerator MoveAllObjects()
	{
		List<Coroutine> list = new List<Coroutine>();
		foreach (ObjectMovement item2 in objectsToMove)
		{
			if (item2.targetObject != null && item2.endPoint != null)
			{
				Coroutine item = StartCoroutine(MoveObject(item2));
				list.Add(item);
			}
			else
			{
				Debug.LogWarning("[BossGateTrigger] Skipping movement - missing target object or end point");
			}
		}
		foreach (Coroutine item3 in list)
		{
			yield return item3;
		}
		if (showDebugInfo)
		{
			Debug.Log("[BossGateTrigger] All objects finished moving");
		}
	}

	private IEnumerator MoveObject(ObjectMovement movement)
	{
		Vector3 startPos = movement.actualStartPosition;
		Vector3 endPos = movement.endPoint.position;
		float elapsed = 0f;
		if (showDebugInfo)
		{
			Debug.Log($"[BossGateTrigger] Moving {movement.targetObject.name} from {startPos} to {endPos}");
		}
		if (movement.enableColliderTime == ColliderEnableTime.OnMovementStart && movement.gateCollider != null)
		{
			movement.gateCollider.enabled = true;
			if (showDebugInfo)
			{
				Debug.Log("[BossGateTrigger] Enabled collider on " + movement.targetObject.name + " at movement start");
			}
		}
		while (elapsed < movement.duration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Pow(Mathf.Clamp01(elapsed / movement.duration), exponentialPower);
			movement.targetObject.position = Vector3.Lerp(startPos, endPos, t);
			yield return null;
		}
		movement.targetObject.position = endPos;
		SFXManager.Instance?.Play("ENV_FenceClose", movement.targetObject.position);
		if (movement.enableColliderTime == ColliderEnableTime.OnMovementEnd && movement.gateCollider != null)
		{
			movement.gateCollider.enabled = true;
			if (showDebugInfo)
			{
				Debug.Log("[BossGateTrigger] Enabled collider on " + movement.targetObject.name + " at movement end");
			}
		}
		if (movement.enableCameraShake && CameraShakeManager.Instance != null)
		{
			CameraShakeManager.Instance.ShakeCamera(movement.shakeAmplitude, movement.shakeFrequency, movement.shakeDuration);
			if (showDebugInfo)
			{
				Debug.Log("[BossGateTrigger] Triggered camera shake for " + movement.targetObject.name);
			}
		}
		if (showDebugInfo)
		{
			Debug.Log("[BossGateTrigger] " + movement.targetObject.name + " finished moving");
		}
	}

	private void OnDrawGizmos()
	{
		if (!showGizmos)
		{
			return;
		}
		Gizmos.color = (hasTriggered ? Color.grey : Color.green);
		Collider2D component = GetComponent<Collider2D>();
		if (component != null)
		{
			if (component is BoxCollider2D boxCollider2D)
			{
				Gizmos.DrawWireCube(base.transform.position + (Vector3)boxCollider2D.offset, boxCollider2D.size);
			}
			else if (component is CircleCollider2D circleCollider2D)
			{
				Gizmos.DrawWireSphere(base.transform.position + (Vector3)circleCollider2D.offset, circleCollider2D.radius);
			}
		}
		foreach (ObjectMovement item in objectsToMove)
		{
			if (!(item.targetObject == null) && !(item.endPoint == null))
			{
				Vector3 vector = ((item.startPoint != null) ? item.startPoint.position : item.targetObject.position);
				Vector3 position = item.endPoint.position;
				Gizmos.color = Color.cyan;
				Gizmos.DrawWireSphere(vector, 0.3f);
				Gizmos.color = Color.magenta;
				Gizmos.DrawWireSphere(position, 0.3f);
				Gizmos.color = Color.yellow;
				Gizmos.DrawLine(vector, position);
				DrawArrow(vector, position - vector, 0.5f);
				if (item.gateCollider != null)
				{
					Gizmos.color = (item.gateCollider.enabled ? Color.red : Color.grey);
					Gizmos.DrawWireCube(item.targetObject.position + (Vector3)item.gateCollider.offset, item.gateCollider.size);
				}
			}
		}
	}

	private void DrawArrow(Vector3 position, Vector3 direction, float arrowHeadLength)
	{
		if (!(direction.magnitude < 0.01f))
		{
			Vector3 vector = position + direction;
			Vector3 vector2 = Quaternion.Euler(0f, 0f, 150f) * direction.normalized * arrowHeadLength;
			Vector3 vector3 = Quaternion.Euler(0f, 0f, -150f) * direction.normalized * arrowHeadLength;
			Gizmos.DrawLine(vector, vector + vector2);
			Gizmos.DrawLine(vector, vector + vector3);
		}
	}

	private void OnDrawGizmosSelected()
	{
		_ = showDebugInfo;
	}
}
