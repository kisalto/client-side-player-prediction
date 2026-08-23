using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
	[Header("Movement Settings")]
	[SerializeField]
	private Transform[] waypoints;

	[SerializeField]
	private float moveSpeed = 2f;

	[SerializeField]
	private float pauseAtPoint = 0.5f;

	[Header("Options")]
	[SerializeField]
	private int startWaypointIndex;

	[SerializeField]
	private bool useLocalPositions;

	[SerializeField]
	private bool stayAtFinalWaypoint;

	[Header("Player Attachment")]
	[SerializeField]
	private string playerTag = "Player";

	[SerializeField]
	private float playerCheckRadius = 2f;

	[SerializeField]
	private LayerMask playerLayer;

	[Header("Door Colliders")]
	[SerializeField]
	private BoxCollider2D leftDoorCollider;

	[SerializeField]
	private BoxCollider2D rightDoorCollider;

	[Header("Door Animation")]
	[SerializeField]
	private Animator doorAnimator;

	[SerializeField]
	private string animationStateParameter = "State";

	[SerializeField]
	private int idleOpenedState;

	[SerializeField]
	private int closingAnimationState = 1;

	[SerializeField]
	private int idleClosedState = 2;

	[SerializeField]
	private int openingAnimationState = 3;

	[SerializeField]
	private float doorCloseDuration = 1f;

	[SerializeField]
	private float doorOpenDuration = 1f;

	[Header("Reactive Settings")]
	[SerializeField]
	private float lowerDistance = 0.1f;

	[SerializeField]
	private float reactiveSpeed = 5f;

	[Header("Return Settings")]
	[SerializeField]
	private float returnDelay = 1f;

	[Header("Debug")]
	[SerializeField]
	private bool enableDebugLogs;

	private Vector3[] waypointPositions;

	private int currentWaypointIndex;

	private int targetWaypointIndex;

	private int direction = 1;

	private float pauseTimer;

	private float returnDelayTimer;

	private bool isPaused;

	private bool playerOnPlatform;

	private bool playerUnderPlatform;

	private bool returningToStart;

	private bool waitingToReturn;

	private bool reachedFinalWaypoint;

	private bool isDoorsOpen = true;

	private bool isAnimatingDoors;

	private bool hasStartedMoving;

	private Vector3 reactiveOffset = Vector3.zero;

	private Transform cachedTransform;

	private Rigidbody2D platformRigidbody;

	private Transform playerTransform;

	private Rigidbody2D playerRigidbody;

	private BoxCollider2D platformCollider;

	private Vector3 targetPosition;

	private Vector2 platformVelocity;

	private Vector2 previousPosition;

	private Vector2 previousPlatformVelocity;

	private const float ARRIVAL_THRESHOLD = 0.1f;

	public bool IsPlayerOnPlatform => playerOnPlatform;

	private void Awake()
	{
		cachedTransform = base.transform;
		platformRigidbody = GetComponent<Rigidbody2D>();
		platformCollider = GetComponent<BoxCollider2D>();
		if (platformRigidbody == null)
		{
			platformRigidbody = base.gameObject.AddComponent<Rigidbody2D>();
		}
		platformRigidbody.bodyType = RigidbodyType2D.Kinematic;
		platformRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
		platformRigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
		InitializeWaypoints();
		previousPosition = platformRigidbody.position;
		previousPlatformVelocity = Vector2.zero;
	}

	private void Start()
	{
		if (waypointPositions.Length == 0)
		{
			Debug.LogError("MovingPlatform: No waypoints assigned!", this);
			base.enabled = false;
			return;
		}
		currentWaypointIndex = Mathf.Clamp(startWaypointIndex, 0, waypointPositions.Length - 1);
		targetWaypointIndex = currentWaypointIndex;
		targetPosition = waypointPositions[currentWaypointIndex];
		platformRigidbody.position = targetPosition;
		previousPosition = platformRigidbody.position;
		InitializeDoors();
		if (enableDebugLogs)
		{
			Debug.Log($"[MovingPlatform] Initialized at waypoint {currentWaypointIndex}, position: {waypointPositions[currentWaypointIndex]}");
		}
	}

	private void InitializeDoors()
	{
		if (leftDoorCollider != null)
		{
			leftDoorCollider.enabled = false;
		}
		if (rightDoorCollider != null)
		{
			rightDoorCollider.enabled = false;
		}
		if (doorAnimator != null)
		{
			doorAnimator.SetInteger(animationStateParameter, idleOpenedState);
		}
		isDoorsOpen = true;
		if (enableDebugLogs)
		{
			Debug.Log("[MovingPlatform] Doors initialized: Both colliders disabled, State: Idle Opened");
		}
	}

	private void Update()
	{
		if (waypointPositions.Length < 2)
		{
			return;
		}
		CheckPlayerStatus();
		if (isAnimatingDoors)
		{
			return;
		}
		UpdateReactiveOffset();
		if (waitingToReturn)
		{
			returnDelayTimer -= Time.deltaTime;
			if (returnDelayTimer <= 0f)
			{
				waitingToReturn = false;
				returningToStart = true;
				if (enableDebugLogs)
				{
					Debug.Log($"[MovingPlatform] Return delay finished, returning from waypoint {currentWaypointIndex} to {targetWaypointIndex}");
				}
			}
		}
		else if (isPaused)
		{
			pauseTimer -= Time.deltaTime;
			if (pauseTimer <= 0f)
			{
				isPaused = false;
				if (enableDebugLogs)
				{
					Debug.Log($"[MovingPlatform] Pause ended, resuming movement to waypoint {targetWaypointIndex}");
				}
			}
		}
		else if (playerOnPlatform)
		{
			returningToStart = false;
			waitingToReturn = false;
			if (!hasStartedMoving)
			{
				StartCoroutine(StartPlatformMovement());
			}
		}
		else
		{
			_ = returningToStart;
		}
	}

	private void FixedUpdate()
	{
		if (waypointPositions.Length < 2)
		{
			return;
		}
		Vector2 vector = previousPosition;
		bool flag = false;
		if (playerOnPlatform && hasStartedMoving && !isPaused && !isAnimatingDoors)
		{
			if (!playerUnderPlatform && (!reachedFinalWaypoint || !stayAtFinalWaypoint))
			{
				flag = true;
			}
		}
		else if (returningToStart && !isPaused && !isAnimatingDoors && !playerUnderPlatform)
		{
			flag = true;
		}
		if (flag)
		{
			MovePlatform();
		}
		previousPosition = platformRigidbody.position;
		Vector2 vector2 = previousPosition - vector;
		if (playerOnPlatform && playerRigidbody != null && vector2 != Vector2.zero)
		{
			playerRigidbody.position += vector2;
		}
	}

	private void CheckPlayerStatus()
	{
		if (playerTransform != null)
		{
			bool flag = CheckPlayerOnPlatform(playerTransform);
			if (playerOnPlatform && !flag)
			{
				if (enableDebugLogs)
				{
					Debug.Log("[MovingPlatform] Player detected leaving platform");
				}
				DetachPlayer();
			}
		}
		CheckPlayerUnderneath();
	}

	private bool CheckPlayerOnPlatform(Transform player)
	{
		if (player == null)
		{
			return false;
		}
		Vector2 vector = new Vector2(cachedTransform.position.x, cachedTransform.position.y);
		Vector2 vector2 = new Vector2(player.position.x, player.position.y);
		float num = Mathf.Abs(vector2.x - vector.x);
		float num2 = vector2.y - cachedTransform.position.y;
		if (platformCollider != null)
		{
			float x = platformCollider.bounds.size.x;
			if (num > x * 0.5f + 0.5f)
			{
				return false;
			}
		}
		else if (num > playerCheckRadius)
		{
			return false;
		}
		if (num2 < -1f || num2 > 2f)
		{
			return false;
		}
		return true;
	}

	private void CheckPlayerUnderneath()
	{
		if (platformCollider == null)
		{
			return;
		}
		Bounds bounds = platformCollider.bounds;
		Vector2 point = new Vector2(bounds.center.x, bounds.min.y - 0.2f);
		Vector2 size = new Vector2(bounds.size.x, 0.4f);
		Collider2D collider2D = Physics2D.OverlapBox(point, size, 0f, playerLayer);
		bool flag = playerUnderPlatform;
		playerUnderPlatform = collider2D != null && !playerOnPlatform;
		if (playerUnderPlatform && !flag)
		{
			if (enableDebugLogs)
			{
				Debug.Log("[MovingPlatform] Player detected underneath platform");
			}
		}
		else if ((!playerUnderPlatform & flag) && enableDebugLogs)
		{
			Debug.Log("[MovingPlatform] Player no longer underneath platform");
		}
	}

	private void InitializeWaypoints()
	{
		if (waypoints == null || waypoints.Length == 0)
		{
			waypointPositions = new Vector3[0];
			return;
		}
		waypointPositions = new Vector3[waypoints.Length];
		for (int i = 0; i < waypoints.Length; i++)
		{
			if (waypoints[i] != null)
			{
				waypointPositions[i] = (useLocalPositions ? waypoints[i].localPosition : waypoints[i].position);
				continue;
			}
			Debug.LogWarning($"MovingPlatform: Waypoint at index {i} is null!", this);
			waypointPositions[i] = cachedTransform.position;
		}
	}

	private void UpdateReactiveOffset()
	{
		Vector3 target = (playerOnPlatform ? new Vector3(0f, 0f - lowerDistance, 0f) : Vector3.zero);
		reactiveOffset = Vector3.MoveTowards(reactiveOffset, target, reactiveSpeed * Time.deltaTime);
	}

	private IEnumerator StartPlatformMovement()
	{
		hasStartedMoving = true;
		isAnimatingDoors = true;
		if (enableDebugLogs)
		{
			Debug.Log("[MovingPlatform] Player on platform, closing doors before movement");
		}
		yield return StartCoroutine(CloseDoors());
		isAnimatingDoors = false;
		if (currentWaypointIndex != targetWaypointIndex || reachedFinalWaypoint)
		{
			yield break;
		}
		int num = currentWaypointIndex + direction;
		if (num >= 0 && num < waypointPositions.Length)
		{
			targetWaypointIndex = num;
			if (enableDebugLogs)
			{
				Debug.Log($"[MovingPlatform] Doors closed, starting movement to waypoint {targetWaypointIndex}");
			}
		}
	}

	private IEnumerator CloseDoors()
	{
		if (leftDoorCollider != null)
		{
			leftDoorCollider.enabled = true;
		}
		if (rightDoorCollider != null)
		{
			rightDoorCollider.enabled = true;
		}
		if (doorAnimator != null)
		{
			doorAnimator.SetInteger(animationStateParameter, closingAnimationState);
		}
		isDoorsOpen = false;
		if (enableDebugLogs)
		{
			Debug.Log("[MovingPlatform] Closing doors animation started");
		}
		yield return new WaitForSeconds(doorCloseDuration);
		if (doorAnimator != null)
		{
			doorAnimator.SetInteger(animationStateParameter, idleClosedState);
		}
		if (enableDebugLogs)
		{
			Debug.Log("[MovingPlatform] Doors closed, both colliders enabled");
		}
	}

	private IEnumerator OpenDoors()
	{
		isAnimatingDoors = true;
		if (doorAnimator != null)
		{
			doorAnimator.SetInteger(animationStateParameter, openingAnimationState);
		}
		if (enableDebugLogs)
		{
			Debug.Log("[MovingPlatform] Opening doors animation started");
		}
		yield return new WaitForSeconds(doorOpenDuration);
		if (leftDoorCollider != null)
		{
			leftDoorCollider.enabled = false;
		}
		if (rightDoorCollider != null)
		{
			rightDoorCollider.enabled = false;
		}
		if (doorAnimator != null)
		{
			doorAnimator.SetInteger(animationStateParameter, idleOpenedState);
		}
		isDoorsOpen = true;
		isAnimatingDoors = false;
		if (enableDebugLogs)
		{
			Debug.Log("[MovingPlatform] Doors opened, both colliders disabled");
		}
	}

	private void MovePlatform()
	{
		targetPosition = waypointPositions[targetWaypointIndex] + reactiveOffset;
		Vector2 position = platformRigidbody.position;
		Vector2 position2 = Vector2.MoveTowards(position, targetPosition, moveSpeed * Time.fixedDeltaTime);
		platformRigidbody.MovePosition(position2);
		if (Vector2.Distance(position, targetPosition) < 0.1f)
		{
			ReachWaypoint();
		}
	}

	private void ReachWaypoint()
	{
		if (enableDebugLogs)
		{
			Debug.Log($"[MovingPlatform] Reached waypoint {targetWaypointIndex}");
		}
		currentWaypointIndex = targetWaypointIndex;
		if (returningToStart)
		{
			if (currentWaypointIndex <= 0)
			{
				returningToStart = false;
				reachedFinalWaypoint = false;
				hasStartedMoving = false;
				direction = 1;
				targetWaypointIndex = 0;
				if (!isDoorsOpen)
				{
					StartCoroutine(OpenDoors());
				}
				if (enableDebugLogs)
				{
					Debug.Log("[MovingPlatform] Returned to start (waypoint 0), opening doors");
				}
			}
			else
			{
				targetWaypointIndex = currentWaypointIndex - 1;
				if (enableDebugLogs)
				{
					Debug.Log($"[MovingPlatform] Continuing return to waypoint {targetWaypointIndex}");
				}
				if (pauseAtPoint > 0f)
				{
					isPaused = true;
					pauseTimer = pauseAtPoint;
				}
			}
			return;
		}
		if (currentWaypointIndex >= waypointPositions.Length - 1)
		{
			reachedFinalWaypoint = true;
			if (stayAtFinalWaypoint)
			{
				if (!isDoorsOpen)
				{
					StartCoroutine(OpenDoors());
				}
				return;
			}
			direction = -1;
		}
		else if (currentWaypointIndex <= 0)
		{
			direction = 1;
			reachedFinalWaypoint = false;
		}
		int num = currentWaypointIndex + direction;
		if (num >= 0 && num < waypointPositions.Length)
		{
			targetWaypointIndex = num;
			if (pauseAtPoint > 0f)
			{
				isPaused = true;
				pauseTimer = pauseAtPoint;
			}
		}
	}

	private void OnCollisionEnter2D(Collision2D collision)
	{
		if (!collision.gameObject.CompareTag(playerTag))
		{
			return;
		}
		ContactPoint2D[] contacts = collision.contacts;
		foreach (ContactPoint2D contactPoint2D in contacts)
		{
			if (contactPoint2D.normal.y < -0.5f)
			{
				AttachPlayer(collision.gameObject);
				break;
			}
		}
	}

	private void OnCollisionStay2D(Collision2D collision)
	{
		if (!collision.gameObject.CompareTag(playerTag) || playerOnPlatform)
		{
			return;
		}
		ContactPoint2D[] contacts = collision.contacts;
		foreach (ContactPoint2D contactPoint2D in contacts)
		{
			if (contactPoint2D.normal.y < -0.5f)
			{
				AttachPlayer(collision.gameObject);
				break;
			}
		}
	}

	private void AttachPlayer(GameObject player)
	{
		if (!playerOnPlatform)
		{
			playerOnPlatform = true;
			playerTransform = player.transform;
			playerRigidbody = player.GetComponent<Rigidbody2D>();
			waitingToReturn = false;
			returnDelayTimer = 0f;
			if (enableDebugLogs)
			{
				Debug.Log($"[MovingPlatform] Player attached to platform at waypoint {currentWaypointIndex}");
			}
		}
	}

	private void DetachPlayer()
	{
		if (!playerOnPlatform)
		{
			return;
		}
		playerOnPlatform = false;
		playerTransform = null;
		playerRigidbody = null;
		if (enableDebugLogs)
		{
			Debug.Log($"[MovingPlatform] Player detached. Cur: {currentWaypointIndex}, Tgt was: {targetWaypointIndex}");
		}
		targetWaypointIndex = currentWaypointIndex;
		if (returnDelay > 0f)
		{
			waitingToReturn = true;
			returnDelayTimer = returnDelay;
			if (enableDebugLogs)
			{
				Debug.Log($"[MovingPlatform] Waiting {returnDelay}s before returning from waypoint {currentWaypointIndex}");
			}
		}
		else
		{
			returningToStart = true;
		}
	}

	private void OnDrawGizmos()
	{
		if (waypoints == null || waypoints.Length < 2)
		{
			return;
		}
		Gizmos.color = Color.green;
		for (int i = 0; i < waypoints.Length; i++)
		{
			if (waypoints[i] != null)
			{
				Vector3 vector = (useLocalPositions ? waypoints[i].localPosition : waypoints[i].position);
				Gizmos.DrawWireSphere(vector, 0.3f);
				if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
				{
					Vector3 to = (useLocalPositions ? waypoints[i + 1].localPosition : waypoints[i + 1].position);
					Gizmos.DrawLine(vector, to);
				}
			}
		}
		if (Application.isPlaying && waypointPositions != null && waypointPositions.Length != 0)
		{
			if (playerOnPlatform && targetWaypointIndex >= 0 && targetWaypointIndex < waypointPositions.Length)
			{
				Gizmos.color = Color.yellow;
				Gizmos.DrawLine(base.transform.position, waypointPositions[targetWaypointIndex]);
			}
			else if (returningToStart)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawLine(base.transform.position, waypointPositions[targetWaypointIndex]);
			}
			else if (waitingToReturn)
			{
				Gizmos.color = Color.magenta;
				Gizmos.DrawLine(base.transform.position, waypointPositions[targetWaypointIndex]);
			}
			if (platformCollider != null)
			{
				Bounds bounds = platformCollider.bounds;
				Vector2 vector2 = new Vector2(bounds.center.x, bounds.min.y - 0.2f);
				Vector2 vector3 = new Vector2(bounds.size.x, 0.4f);
				Gizmos.color = (playerUnderPlatform ? Color.red : Color.yellow);
				Gizmos.DrawWireCube(vector2, vector3);
			}
		}
	}
}
