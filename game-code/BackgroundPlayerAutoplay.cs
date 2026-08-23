using UnityEngine;

public class BackgroundPlayerAutoplay : MonoBehaviour
{
	private enum State
	{
		Walking,
		PreJump,
		InAir,
		AtSign
	}

	[Header("Movement")]
	[SerializeField]
	private float moveSpeed = 0.85f;

	[SerializeField]
	private float accelerationSmoothTime = 0.3f;

	[SerializeField]
	private float decelerationSmoothTime = 0.12f;

	[SerializeField]
	private float speedNoiseScale = 0.4f;

	[SerializeField]
	private float speedNoiseAmount = 0.07f;

	[Header("Sign Following")]
	[SerializeField]
	private float arrivalThreshold = 0.7f;

	[SerializeField]
	private float slowdownStartDistance = 3.5f;

	[SerializeField]
	private float minimumApproachSpeed = 0.2f;

	[Header("Obstacle Detection")]
	[Tooltip("How far ahead to BoxCast for walls.")]
	[SerializeField]
	private float wallLookAheadDistance = 3f;

	[Tooltip("How far ahead to sweep for gaps at ground level.")]
	[SerializeField]
	private float gapLookAheadDistance = 2.5f;

	[Tooltip("Step size for gap scan.")]
	[SerializeField]
	private float gapScanStepSize = 0.3f;

	[Tooltip("How far below feet to confirm ground presence.")]
	[SerializeField]
	private float groundConfirmDistance = 0.5f;

	[Tooltip("Minimum consecutive gap width before a jump is planned. Filters tilemap seams.")]
	[SerializeField]
	private float minimumGapWidth = 0.6f;

	[Tooltip("Obstacles shorter than this are walked over without jumping.")]
	[SerializeField]
	private float stepUpThreshold = 0.4f;

	[Tooltip("Distance from the obstacle face to stand before jumping.")]
	[SerializeField]
	private float jumpStandoffDistance = 1f;

	[Tooltip("How close to the standoff X before the jump fires.")]
	[SerializeField]
	private float jumpTriggerThreshold = 0.3f;

	[SerializeField]
	private float maxJumpableHeight = 2.5f;

	[SerializeField]
	private int heightCheckSteps = 5;

	[Tooltip("Very short-range check directly in front of the player. Catches anything the planner missed.")]
	[SerializeField]
	private float immediateObstacleDistance = 0.35f;

	[SerializeField]
	private LayerMask obstacleLayer;

	[Header("Jump")]
	[SerializeField]
	private float jumpCooldown = 0.8f;

	[SerializeField]
	private float minJumpHoldTime = 0.08f;

	[SerializeField]
	private float maxJumpHoldTime = 0.25f;

	[SerializeField]
	private float casualJumpChance = 0.004f;

	[SerializeField]
	private float casualJumpMinInterval = 3f;

	[Header("Natural Feel")]
	[SerializeField]
	private float idlePauseChance = 0.002f;

	[SerializeField]
	private float minIdlePauseDuration = 0.3f;

	[SerializeField]
	private float maxIdlePauseDuration = 0.8f;

	[Header("Recovery")]
	[SerializeField]
	private float stuckCheckInterval = 1.5f;

	[SerializeField]
	private float stuckMinDistance = 0.2f;

	[SerializeField]
	private float preJumpTimeout = 2.5f;

	[SerializeField]
	private float postLandObstacleCooldown = 0.6f;

	private State currentState;

	private Transform targetSign;

	private bool movingRight = true;

	private float currentSpeed;

	private float speedSmoothVelocity;

	private float noiseTimeOffset;

	private float jumpStandoffX;

	private float lastJumpTime;

	private bool isHoldingJump;

	private float jumpHoldTimer;

	private float jumpHoldDuration;

	private bool isPaused;

	private float pauseTimer;

	private float preJumpTimer;

	private float obstacleDetectionCooldown;

	private Vector3 lastStuckCheckPos;

	private float timeSinceStuckCheck;

	private float colHalfWidth;

	private float colHalfHeight;

	private Vector2 colOffset;

	private PlayerMovementController movementController;

	private PlayerStateMachine stateMachine;

	private Rigidbody2D rb;

	private Animator animator;

	private BoxCollider2D col;

	private static readonly int AnimStateHash = Animator.StringToHash("State");

	private void Awake()
	{
		movementController = GetComponent<PlayerMovementController>();
		stateMachine = GetComponent<PlayerStateMachine>();
		rb = GetComponent<Rigidbody2D>();
		animator = GetComponent<Animator>();
		col = GetComponent<BoxCollider2D>();
		CacheColliderDimensions();
		DisablePlayerControlSystems();
	}

	private void CacheColliderDimensions()
	{
		if (col != null)
		{
			colHalfWidth = col.size.x * 0.5f;
			colHalfHeight = col.size.y * 0.5f;
			colOffset = col.offset;
		}
		else
		{
			colHalfWidth = 0.5f;
			colHalfHeight = 0.5f;
			colOffset = Vector2.zero;
		}
	}

	private void DisablePlayerControlSystems()
	{
		if (stateMachine != null)
		{
			stateMachine.enabled = false;
		}
		PlayerInputReader component = GetComponent<PlayerInputReader>();
		if (component != null)
		{
			component.enabled = false;
		}
		PlayerCombatController component2 = GetComponent<PlayerCombatController>();
		if (component2 != null)
		{
			component2.enabled = false;
		}
		PlayerDashController component3 = GetComponent<PlayerDashController>();
		if (component3 != null)
		{
			component3.enabled = false;
		}
		PlayerHealthController component4 = GetComponent<PlayerHealthController>();
		if (component4 != null)
		{
			component4.enabled = false;
		}
	}

	private void Start()
	{
		lastJumpTime = 0f - jumpCooldown;
		lastStuckCheckPos = base.transform.position;
		noiseTimeOffset = Random.Range(0f, 1000f);
		MenuNavigationController menuNavigationController = Object.FindObjectOfType<MenuNavigationController>();
		if (menuNavigationController != null)
		{
			menuNavigationController.OnSelectionChanged += OnMenuSelectionChanged;
		}
	}

	private void OnMenuSelectionChanged(MenuButton button)
	{
		SetTargetSign(button.transform);
	}

	public void SetTargetSign(Transform sign)
	{
		targetSign = sign;
		isPaused = false;
		obstacleDetectionCooldown = 0f;
		TransitionTo(State.Walking);
	}

	private void TransitionTo(State newState)
	{
		currentState = newState;
		if (newState == State.PreJump)
		{
			preJumpTimer = 0f;
		}
	}

	private void Update()
	{
		UpdateTimers();
		UpdateJumpHold();
		UpdateStuckCheck();
		switch (currentState)
		{
		case State.Walking:
			UpdateWalking();
			break;
		case State.PreJump:
			UpdatePreJump();
			break;
		case State.InAir:
			UpdateInAir();
			break;
		case State.AtSign:
			UpdateAtSign();
			break;
		}
		UpdateAnimations();
	}

	private void FixedUpdate()
	{
		if (!(movementController == null))
		{
			if (isHoldingJump)
			{
				movementController.ApplyGravity();
			}
			else if (currentState == State.InAir)
			{
				movementController.ApplyLowJumpGravity();
			}
		}
	}

	private void UpdateTimers()
	{
		if (isPaused)
		{
			pauseTimer -= Time.deltaTime;
			if (pauseTimer <= 0f)
			{
				isPaused = false;
			}
		}
		if (obstacleDetectionCooldown > 0f)
		{
			obstacleDetectionCooldown -= Time.deltaTime;
		}
	}

	private void UpdateWalking()
	{
		if (targetSign == null)
		{
			SmoothedStop();
			return;
		}
		float num = targetSign.position.x - base.transform.position.x;
		float num2 = Mathf.Abs(num);
		if (num2 <= arrivalThreshold)
		{
			SmoothedStop();
			TransitionTo(State.AtSign);
			return;
		}
		movingRight = num > 0f;
		if (IsGrounded() && obstacleDetectionCooldown <= 0f && IsObstacleImmediatelyAhead())
		{
			jumpStandoffX = base.transform.position.x;
			TransitionTo(State.PreJump);
			return;
		}
		if (IsGrounded() && obstacleDetectionCooldown <= 0f && TryPlanObstacleJump(out var standoffX))
		{
			jumpStandoffX = standoffX;
			TransitionTo(State.PreJump);
			return;
		}
		if (!isPaused && IsGrounded() && CanJump() && Random.value < casualJumpChance && Time.time - lastJumpTime >= casualJumpMinInterval)
		{
			ExecuteJump(isObstacleJump: false);
			return;
		}
		if (!isPaused && IsGrounded() && Random.value < idlePauseChance)
		{
			isPaused = true;
			pauseTimer = Random.Range(minIdlePauseDuration, maxIdlePauseDuration);
		}
		if (isPaused)
		{
			SmoothedStop();
			return;
		}
		float num3 = ((num2 < slowdownStartDistance) ? Mathf.Clamp01(num2 / slowdownStartDistance) : 1f);
		float num4 = 1f + (Mathf.PerlinNoise(Time.time * speedNoiseScale + noiseTimeOffset, 0f) * 2f - 1f) * speedNoiseAmount;
		float targetFraction = Mathf.Max(minimumApproachSpeed, num3 * num4);
		SmoothSpeed(targetFraction, accelerationSmoothTime);
		movementController?.Move((movingRight ? 1f : (-1f)) * currentSpeed * moveSpeed);
	}

	private void UpdatePreJump()
	{
		preJumpTimer += Time.deltaTime;
		if (preJumpTimer >= preJumpTimeout)
		{
			if (IsGrounded())
			{
				ExecuteJump(isObstacleJump: true);
			}
			if (currentState == State.PreJump)
			{
				obstacleDetectionCooldown = postLandObstacleCooldown;
				TransitionTo(State.Walking);
			}
			return;
		}
		float num = jumpStandoffX - base.transform.position.x;
		float num2 = Mathf.Abs(num);
		if (num2 <= jumpTriggerThreshold)
		{
			SmoothedStop();
			if (CanJump())
			{
				ExecuteJump(isObstacleJump: true);
			}
		}
		else
		{
			bool flag = num > 0f;
			float b = Mathf.Clamp01(num2 / 1.5f);
			float targetFraction = Mathf.Max(0.3f, b);
			float smoothTime = ((num2 < 1f) ? decelerationSmoothTime : accelerationSmoothTime);
			SmoothSpeed(targetFraction, smoothTime);
			movementController?.Move((flag ? 1f : (-1f)) * currentSpeed * moveSpeed);
		}
	}

	private void UpdateInAir()
	{
		if (!(Time.time - lastJumpTime < 0.15f))
		{
			if (IsGrounded())
			{
				obstacleDetectionCooldown = postLandObstacleCooldown;
				TransitionTo(State.Walking);
			}
			else
			{
				currentSpeed = Mathf.Lerp(currentSpeed, 1f, Time.deltaTime * 3f);
				movementController?.Move((movingRight ? 1f : (-1f)) * currentSpeed * moveSpeed);
			}
		}
	}

	private void UpdateAtSign()
	{
		SmoothedStop();
		if (!(targetSign == null) && Mathf.Abs(targetSign.position.x - base.transform.position.x) > arrivalThreshold)
		{
			TransitionTo(State.Walking);
		}
	}

	private bool IsObstacleImmediatelyAhead()
	{
		if ((int)obstacleLayer == 0)
		{
			return false;
		}
		float num = (movingRight ? 1f : (-1f));
		Vector2 bodyCheckOrigin = GetBodyCheckOrigin();
		float y = ((col != null) ? (col.size.y * 0.75f) : 1f);
		Vector2 size = new Vector2(0.05f, y);
		if (Physics2D.BoxCast(bodyCheckOrigin, size, 0f, Vector2.right * num, immediateObstacleDistance, obstacleLayer).collider == null)
		{
			return false;
		}
		float obstacleHeight = GetObstacleHeight(bodyCheckOrigin, num);
		if (obstacleHeight > 0f && obstacleHeight <= stepUpThreshold)
		{
			return false;
		}
		return true;
	}

	private bool TryPlanObstacleJump(out float standoffX)
	{
		standoffX = 0f;
		if ((int)obstacleLayer == 0 || targetSign == null)
		{
			return false;
		}
		float num = (movingRight ? 1f : (-1f));
		float x = targetSign.position.x;
		Vector2 bodyCheckOrigin = GetBodyCheckOrigin();
		Vector2 feetOrigin = GetFeetOrigin();
		float y = ((col != null) ? (col.size.y * 0.7f) : 1f);
		Vector2 size = new Vector2(0.05f, y);
		RaycastHit2D raycastHit2D = Physics2D.BoxCast(bodyCheckOrigin, size, 0f, Vector2.right * num, wallLookAheadDistance, obstacleLayer);
		if (raycastHit2D.collider != null)
		{
			float x2 = raycastHit2D.point.x;
			if ((x2 - base.transform.position.x) * num >= (x - base.transform.position.x) * num)
			{
				return false;
			}
			float obstacleHeight = GetObstacleHeight(bodyCheckOrigin, num);
			if (obstacleHeight > 0f && obstacleHeight <= stepUpThreshold)
			{
				return false;
			}
			if (obstacleHeight <= 0f || obstacleHeight > maxJumpableHeight)
			{
				return false;
			}
			standoffX = x2 - num * jumpStandoffDistance;
			if ((standoffX - base.transform.position.x) * num < 0f)
			{
				standoffX = base.transform.position.x;
			}
			return true;
		}
		float num2 = FindGapEdge(feetOrigin, num, x);
		if (num2 != float.MaxValue)
		{
			standoffX = num2 - num * jumpStandoffDistance;
			if ((standoffX - base.transform.position.x) * num < 0f)
			{
				standoffX = base.transform.position.x;
			}
			return true;
		}
		return false;
	}

	private float FindGapEdge(Vector2 feetOrigin, float direction, float signX)
	{
		float x = ((col != null) ? (col.size.x * 0.9f) : 0.8f);
		Vector2 size = new Vector2(x, 0.05f);
		float num = float.MaxValue;
		float num2 = 0f;
		for (float num3 = gapScanStepSize; num3 <= gapLookAheadDistance; num3 += gapScanStepSize)
		{
			float num4 = feetOrigin.x + direction * num3;
			if ((num4 - signX) * direction > 0f)
			{
				break;
			}
			if (Physics2D.BoxCast(new Vector2(num4, feetOrigin.y), size, 0f, Vector2.down, groundConfirmDistance, obstacleLayer).collider == null)
			{
				if (num == float.MaxValue)
				{
					num = num4;
				}
				num2 += gapScanStepSize;
				if (num2 >= minimumGapWidth)
				{
					return num;
				}
			}
			else
			{
				num = float.MaxValue;
				num2 = 0f;
			}
		}
		return float.MaxValue;
	}

	private float GetObstacleHeight(Vector2 checkOrigin, float direction)
	{
		float num = wallLookAheadDistance / (float)heightCheckSteps;
		float num2 = 0f;
		for (int i = 1; i <= heightCheckSteps; i++)
		{
			RaycastHit2D raycastHit2D = Physics2D.Raycast(checkOrigin + Vector2.right * direction * (num * (float)i), Vector2.up, maxJumpableHeight + 1f, obstacleLayer);
			if (raycastHit2D.collider != null)
			{
				float num3 = raycastHit2D.point.y - base.transform.position.y;
				if (num3 > num2)
				{
					num2 = num3;
				}
			}
		}
		return num2;
	}

	private Vector2 GetBodyCheckOrigin()
	{
		float num = (movingRight ? 1f : (-1f));
		return (Vector2)base.transform.position + colOffset + Vector2.right * num * colHalfWidth;
	}

	private Vector2 GetFeetOrigin()
	{
		float num = (movingRight ? 1f : (-1f));
		return (Vector2)base.transform.position + colOffset + Vector2.down * colHalfHeight + Vector2.right * num * colHalfWidth;
	}

	private void ExecuteJump(bool isObstacleJump)
	{
		if (CanJump())
		{
			movementController?.Jump();
			isHoldingJump = true;
			jumpHoldTimer = 0f;
			jumpHoldDuration = (isObstacleJump ? Random.Range(maxJumpHoldTime * 0.8f, maxJumpHoldTime) : Random.Range(minJumpHoldTime, minJumpHoldTime + 0.06f));
			lastJumpTime = Time.time;
			TransitionTo(State.InAir);
		}
	}

	private void UpdateJumpHold()
	{
		if (isHoldingJump)
		{
			jumpHoldTimer += Time.deltaTime;
			if (jumpHoldTimer >= jumpHoldDuration)
			{
				isHoldingJump = false;
				movementController?.CutJump();
			}
		}
	}

	private bool CanJump()
	{
		if (IsGrounded() && Time.time - lastJumpTime >= jumpCooldown)
		{
			return !isHoldingJump;
		}
		return false;
	}

	private bool IsGrounded()
	{
		if (movementController != null)
		{
			return movementController.IsGrounded;
		}
		return false;
	}

	private void SmoothSpeed(float targetFraction, float smoothTime)
	{
		currentSpeed = Mathf.SmoothDamp(currentSpeed, targetFraction, ref speedSmoothVelocity, smoothTime);
	}

	private void SmoothedStop()
	{
		SmoothSpeed(0f, decelerationSmoothTime);
		movementController?.Move(0f);
	}

	private void UpdateStuckCheck()
	{
		if (currentState == State.AtSign || currentState == State.InAir)
		{
			return;
		}
		timeSinceStuckCheck += Time.deltaTime;
		if (timeSinceStuckCheck < stuckCheckInterval)
		{
			return;
		}
		if (Vector3.Distance(base.transform.position, lastStuckCheckPos) < stuckMinDistance && !isPaused)
		{
			if (CanJump())
			{
				ExecuteJump(isObstacleJump: true);
			}
			else
			{
				obstacleDetectionCooldown = postLandObstacleCooldown;
				TransitionTo(State.Walking);
				isPaused = true;
				pauseTimer = 0.4f;
			}
		}
		lastStuckCheckPos = base.transform.position;
		timeSinceStuckCheck = 0f;
	}

	private void UpdateAnimations()
	{
		if (!(animator == null))
		{
			PlayerAnimationState value = ((currentState == State.InAir && rb != null) ? ((rb.linearVelocity.y > 0.2f) ? PlayerAnimationState.Jump : PlayerAnimationState.Fall) : (((currentState == State.Walking || currentState == State.PreJump) && Mathf.Abs(currentSpeed) > 0.05f && !isPaused) ? PlayerAnimationState.Move : PlayerAnimationState.Idle));
			animator.SetInteger(AnimStateHash, (int)value);
		}
	}

	private void OnDrawGizmosSelected()
	{
		if ((int)obstacleLayer != 0)
		{
			if (col == null)
			{
				col = GetComponent<BoxCollider2D>();
			}
			CacheColliderDimensions();
			Vector2 bodyCheckOrigin = GetBodyCheckOrigin();
			Vector2 feetOrigin = GetFeetOrigin();
			float num = (movingRight ? 1f : (-1f));
			float y = ((col != null) ? (col.size.y * 0.75f) : 1f);
			Gizmos.color = Color.red;
			Gizmos.DrawWireCube(bodyCheckOrigin + Vector2.right * num * (immediateObstacleDistance * 0.5f), new Vector3(immediateObstacleDistance, y, 0f));
			Gizmos.color = Color.green;
			Gizmos.DrawLine(bodyCheckOrigin, bodyCheckOrigin + Vector2.right * num * wallLookAheadDistance);
			Gizmos.color = Color.cyan;
			Gizmos.DrawLine(feetOrigin, feetOrigin + Vector2.right * num * gapLookAheadDistance);
			if (currentState == State.PreJump)
			{
				Gizmos.color = Color.yellow;
				Gizmos.DrawWireSphere(new Vector3(jumpStandoffX, base.transform.position.y, 0f), 0.3f);
			}
			if (targetSign != null)
			{
				Gizmos.color = Color.magenta;
				Gizmos.DrawWireSphere(new Vector3(targetSign.position.x, base.transform.position.y, 0f), arrivalThreshold);
			}
		}
	}

	private void OnDestroy()
	{
		if (stateMachine != null)
		{
			stateMachine.enabled = true;
		}
		MenuNavigationController menuNavigationController = Object.FindObjectOfType<MenuNavigationController>();
		if (menuNavigationController != null)
		{
			menuNavigationController.OnSelectionChanged -= OnMenuSelectionChanged;
		}
	}
}
