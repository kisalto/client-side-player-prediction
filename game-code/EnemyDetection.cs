using UnityEngine;

public class EnemyDetection : MonoBehaviour
{
	private EnemyConfig config;

	private Transform cachedTransform;

	private EnemyMovement movement;

	private Actor actor;

	public Transform DetectedPlayer { get; private set; }

	public bool HasDetectedPlayer => DetectedPlayer != null;

	public void Initialize(EnemyConfig enemyConfig, EnemyMovement enemyMovement)
	{
		config = enemyConfig;
		movement = enemyMovement;
		cachedTransform = base.transform;
		actor = GetComponent<Actor>();
	}

	public bool CheckForPlayer()
	{
		Collider2D collider2D = Physics2D.OverlapCircle(cachedTransform.position, config.detectionRange, config.playerLayer);
		if (collider2D != null)
		{
			if (config.useDirectionalDetection)
			{
				if (IsPlayerInFront(collider2D.transform))
				{
					DetectedPlayer = collider2D.transform;
					return true;
				}
				return false;
			}
			DetectedPlayer = collider2D.transform;
			return true;
		}
		return false;
	}

	private bool IsPlayerInFront(Transform playerTransform)
	{
		Vector2 to = (playerTransform.position - cachedTransform.position).normalized;
		return Vector2.Angle(movement.IsFacingRight ? Vector2.right : Vector2.left, to) <= config.detectionAngle / 2f;
	}

	public bool IsPlayerInRange(float range)
	{
		if (DetectedPlayer == null)
		{
			return false;
		}
		return Vector2.Distance(cachedTransform.position, DetectedPlayer.position) <= range;
	}

	public bool IsBeingKnockedBack()
	{
		if (actor != null)
		{
			return actor.isBeingKnockedBack;
		}
		return false;
	}

	public bool HasLostPlayer()
	{
		if (DetectedPlayer == null)
		{
			return true;
		}
		if (Vector2.Distance(cachedTransform.position, DetectedPlayer.position) > config.losePlayerRange)
		{
			return true;
		}
		if (config.useDirectionalDetection && !IsPlayerInFront(DetectedPlayer))
		{
			return true;
		}
		return false;
	}

	public float GetDistanceToPlayer()
	{
		if (DetectedPlayer == null)
		{
			return float.MaxValue;
		}
		return Vector2.Distance(cachedTransform.position, DetectedPlayer.position);
	}

	public void ClearDetectedPlayer()
	{
		DetectedPlayer = null;
	}

	public void SetDetectedPlayer(Transform player)
	{
		DetectedPlayer = player;
	}

	private void OnDrawGizmosSelected()
	{
		if (!(config == null) && !(movement == null))
		{
			Vector3 position = base.transform.position;
			if (config.useDirectionalDetection)
			{
				DrawDetectionCone(position);
			}
			else
			{
				Gizmos.color = Color.yellow;
				Gizmos.DrawWireSphere(position, config.detectionRange);
			}
			Gizmos.color = new Color(1f, 0.5f, 0f);
			Gizmos.DrawWireSphere(position, config.losePlayerRange);
		}
	}

	private void DrawDetectionCone(Vector3 position)
	{
		Vector2 vector = (movement.IsFacingRight ? Vector2.right : Vector2.left);
		float num = config.detectionAngle / 2f;
		Vector3 vector2 = Quaternion.Euler(0f, 0f, num) * vector * config.detectionRange;
		Vector3 vector3 = Quaternion.Euler(0f, 0f, 0f - num) * vector * config.detectionRange;
		Gizmos.color = Color.yellow;
		Gizmos.DrawLine(position, position + vector2);
		Gizmos.DrawLine(position, position + vector3);
		int num2 = 20;
		Vector3 vector4 = position + vector2;
		for (int i = 1; i <= num2; i++)
		{
			float z = Mathf.Lerp(num, 0f - num, (float)i / (float)num2);
			Vector3 vector5 = position + Quaternion.Euler(0f, 0f, z) * vector * config.detectionRange;
			Gizmos.DrawLine(vector4, vector5);
			vector4 = vector5;
		}
	}
}
