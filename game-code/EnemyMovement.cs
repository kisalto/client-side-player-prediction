using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
	private Rigidbody2D rb;

	private Transform cachedTransform;

	private EnemyConfig config;

	private Actor actor;

	private Vector3 startPosition;

	private float patrolDirection = 1f;

	private bool isFacingRight = true;

	private bool startFacingRight = true;

	private bool hasReachedBoundary;

	private Vector3 lastPosition;

	private float stuckCheckTimer;

	private const float STUCK_CHECK_INTERVAL = 0.5f;

	private const float STUCK_MOVEMENT_THRESHOLD = 0.1f;

	private const float STUCK_VELOCITY_THRESHOLD = 0.2f;

	private BoxCollider2D boxCollider;

	private const float PLAYER_OVERLAP_NUDGE_SPEED = 2f;

	private Transform overlappingPlayer;

	public Vector3 StartPosition => startPosition;

	public bool IsFacingRight => isFacingRight;

	public void Initialize(EnemyConfig enemyConfig, Rigidbody2D rigidbody, bool resetStartPosition = true)
	{
		config = enemyConfig;
		rb = rigidbody;
		cachedTransform = base.transform;
		boxCollider = GetComponent<BoxCollider2D>();
		actor = GetComponent<Actor>();
		isFacingRight = config.startFacingRight;
		if (resetStartPosition || startPosition == Vector3.zero)
		{
			startPosition = cachedTransform.position;
			startFacingRight = config.startFacingRight;
			patrolDirection = (isFacingRight ? 1f : (-1f));
		}
		Vector3 localScale = cachedTransform.localScale;
		localScale.x = Mathf.Abs(localScale.x) * (float)(isFacingRight ? 1 : (-1));
		cachedTransform.localScale = localScale;
		lastPosition = cachedTransform.position;
	}

	private void FixedUpdate()
	{
		ResolvePlayerOverlap();
	}

	private void ResolvePlayerOverlap()
	{
		if (boxCollider == null || config == null)
		{
			return;
		}
		Bounds bounds = boxCollider.bounds;
		float num = bounds.size.y * 0.25f;
		Vector2 point = new Vector2(bounds.center.x, bounds.max.y - num * 0.5f);
		Vector2 size = new Vector2(bounds.size.x * 0.8f, num);
		Collider2D collider2D = Physics2D.OverlapBox(point, size, 0f, config.playerLayer);
		if (collider2D == null)
		{
			if (overlappingPlayer != null && !IsBeingKnockedBack())
			{
				Vector2 linearVelocity = rb.linearVelocity;
				linearVelocity.x = 0f;
				rb.linearVelocity = linearVelocity;
			}
			overlappingPlayer = null;
			return;
		}
		overlappingPlayer = collider2D.transform;
		if (!IsBeingKnockedBack())
		{
			float num2 = ((cachedTransform.position.x < collider2D.transform.position.x) ? (-1f) : 1f);
			Vector2 linearVelocity2 = rb.linearVelocity;
			linearVelocity2.x = num2 * 2f;
			rb.linearVelocity = linearVelocity2;
		}
	}

	public bool IsPlayerOnTop()
	{
		return overlappingPlayer != null;
	}

	public void Patrol()
	{
		float num = Mathf.Abs(cachedTransform.position.x - startPosition.x);
		if (num >= config.patrolDistance && !hasReachedBoundary)
		{
			hasReachedBoundary = true;
		}
		else if (num < config.patrolDistance - config.patrolTurnBuffer)
		{
			hasReachedBoundary = false;
		}
		Move(patrolDirection * config.moveSpeed);
	}

	public bool ShouldStopPatrol()
	{
		if (!IsAtEdge())
		{
			return CheckIfStuck();
		}
		return true;
	}

	public void ChaseTarget(Vector3 targetPosition)
	{
		if (IsAtEdge())
		{
			Stop();
			return;
		}
		float num = Mathf.Sign(targetPosition.x - cachedTransform.position.x);
		Move(num * config.moveSpeed * config.chaseSpeedMultiplier);
	}

	public bool IsAtEdge()
	{
		if (!config.detectEdges)
		{
			return false;
		}
		if (boxCollider == null)
		{
			boxCollider = GetComponent<BoxCollider2D>();
		}
		Vector2 vector = (isFacingRight ? Vector2.right : Vector2.left);
		float num = boxCollider.size.x * 0.5f * Mathf.Abs(cachedTransform.localScale.x);
		float y = cachedTransform.position.y - boxCollider.size.y * 0.5f * Mathf.Abs(cachedTransform.localScale.y) + boxCollider.offset.y;
		return Physics2D.Raycast(new Vector2(cachedTransform.position.x + boxCollider.offset.x + vector.x * (num + config.edgeCheckOffset), y), Vector2.down, config.edgeDetectionDistance, config.groundLayer).collider == null;
	}

	private bool CheckIfStuck()
	{
		stuckCheckTimer += Time.deltaTime;
		if (stuckCheckTimer >= 0.5f)
		{
			float num = Mathf.Abs(cachedTransform.position.x - lastPosition.x);
			float num2 = Mathf.Abs(rb.linearVelocity.x);
			bool result = num < 0.1f && num2 < 0.2f;
			lastPosition = cachedTransform.position;
			stuckCheckTimer = 0f;
			return result;
		}
		return false;
	}

	public void Move(float speed)
	{
		if (!IsBeingKnockedBack())
		{
			Vector2 linearVelocity = rb.linearVelocity;
			linearVelocity.x = speed;
			rb.linearVelocity = linearVelocity;
			if (speed > 0.01f)
			{
				Flip(facingRight: true);
			}
			else if (speed < -0.01f)
			{
				Flip(facingRight: false);
			}
		}
	}

	public void Stop()
	{
		if (!IsBeingKnockedBack())
		{
			Vector2 linearVelocity = rb.linearVelocity;
			linearVelocity.x = 0f;
			rb.linearVelocity = linearVelocity;
		}
	}

	public void Flip(bool facingRight)
	{
		if (isFacingRight != facingRight)
		{
			isFacingRight = facingRight;
			Vector3 localScale = cachedTransform.localScale;
			localScale.x = Mathf.Abs(localScale.x) * (float)(facingRight ? 1 : (-1));
			cachedTransform.localScale = localScale;
		}
	}

	public void ReversePatrolDirection()
	{
		patrolDirection *= -1f;
		Flip(patrolDirection > 0f);
		hasReachedBoundary = false;
	}

	public void ReturnToStart()
	{
		float num = Vector2.Distance(cachedTransform.position, startPosition);
		if (num < 0.3f)
		{
			Stop();
			if (num < 0.1f)
			{
				cachedTransform.position = new Vector3(startPosition.x, cachedTransform.position.y, cachedTransform.position.z);
			}
		}
		else if (IsAtEdge())
		{
			Stop();
		}
		else
		{
			float num2 = Mathf.Sign(startPosition.x - cachedTransform.position.x);
			Move(num2 * config.moveSpeed);
		}
	}

	public bool IsAtStartPosition()
	{
		return Vector2.Distance(cachedTransform.position, startPosition) < 0.3f;
	}

	public bool IsAtPatrolBoundary()
	{
		return Mathf.Abs(cachedTransform.position.x - startPosition.x) >= config.patrolDistance - 0.1f;
	}

	public void ResetStuckDetection()
	{
		lastPosition = cachedTransform.position;
		stuckCheckTimer = 0f;
	}

	public void SetNewStartPosition(Vector3 newPosition)
	{
		startPosition = newPosition;
		patrolDirection = 1f;
		ResetStuckDetection();
	}

	public void SetNewStartPositionToCurrent()
	{
		SetNewStartPosition(cachedTransform.position);
	}

	public void RestoreStartFacingDirection()
	{
		Flip(startFacingRight);
	}

	private void OnDrawGizmos()
	{
		if (!(config == null) && config.detectEdges && !(boxCollider == null))
		{
			Vector2 vector = (isFacingRight ? Vector2.right : Vector2.left);
			float num = boxCollider.size.x * 0.5f * Mathf.Abs(base.transform.localScale.x);
			float y = base.transform.position.y - boxCollider.size.y * 0.5f * Mathf.Abs(base.transform.localScale.y) + boxCollider.offset.y;
			Vector2 vector2 = new Vector2(base.transform.position.x + boxCollider.offset.x + vector.x * (num + config.edgeCheckOffset), y);
			Gizmos.color = Color.red;
			Gizmos.DrawLine(vector2, vector2 + Vector2.down * config.edgeDetectionDistance);
			Gizmos.DrawWireSphere(vector2, 0.1f);
		}
	}

	private bool IsBeingKnockedBack()
	{
		if (actor != null)
		{
			return actor.isBeingKnockedBack;
		}
		return false;
	}
}
