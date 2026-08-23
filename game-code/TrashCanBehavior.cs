using UnityEngine;

public class TrashCanBehavior : MonoBehaviour
{
	[Header("Debug Visualization")]
	[SerializeField]
	private bool showDebugGizmos = true;

	private SpriteRenderer spriteRenderer;

	private Transform cachedTransform;

	private EnemyConfig config;

	private EnemyDetection detection;

	private EnemyCombat combat;

	private EnemyController enemyController;

	[Header("Rocket Settings")]
	[SerializeField]
	private Transform rocketSpawnPoint;

	[SerializeField]
	private string rocketPoolTag = "HomingRocket";

	[Header("Collider Settings")]
	[Tooltip("Collider size when trash can is revealed (lid open)")]
	[SerializeField]
	private Vector2 revealedColliderSize = new Vector2(1.25f, 1.25f);

	[SerializeField]
	private Vector2 revealedColliderOffset = new Vector2(0f, 0f);

	[Tooltip("Collider size when trash can is hidden (lid closed - smaller width and height)")]
	[SerializeField]
	private Vector2 hiddenColliderSize = new Vector2(0.85f, 0.75f);

	[SerializeField]
	private Vector2 hiddenColliderOffset = new Vector2(0f, -0.25f);

	private bool isHidden = true;

	private int normalLayer;

	private int hiddenLayer;

	private BoxCollider2D boxCollider;

	private bool lastAttackRangeState;

	private bool lastApproachRangeState;

	private Transform playerTransform;

	private void Awake()
	{
		spriteRenderer = GetComponent<SpriteRenderer>();
		cachedTransform = base.transform;
		boxCollider = GetComponent<BoxCollider2D>();
		enemyController = GetComponent<EnemyController>();
		normalLayer = LayerMask.NameToLayer("Enemy");
		hiddenLayer = LayerMask.NameToLayer("Blocked");
		if (boxCollider != null)
		{
			revealedColliderSize = boxCollider.size;
			revealedColliderOffset = boxCollider.offset;
		}
	}

	public void Initialize(EnemyConfig enemyConfig, EnemyDetection enemyDetection)
	{
		config = enemyConfig;
		detection = enemyDetection;
		combat = GetComponent<EnemyCombat>();
		if (combat == null)
		{
			Debug.LogError("[TrashCan] EnemyCombat component not found!");
		}
		GameObject gameObject = GameObject.FindGameObjectWithTag("Player");
		if (gameObject != null)
		{
			playerTransform = gameObject.transform;
		}
		if (config.isTrashCanEnemy)
		{
			SetHiddenState(shouldHide: true);
		}
	}

	public void InitializeStates(EnemyController controller, EnemyStateFactory stateFactory)
	{
		if (config.isTrashCanEnemy)
		{
			TrashCanHideIdleState state = new TrashCanHideIdleState(controller, config, controller.Detection, stateFactory, this);
			TrashCanHideState state2 = new TrashCanHideState(controller, config, controller.Detection, stateFactory, this);
			TrashCanHideScaredState state3 = new TrashCanHideScaredState(controller, config, controller.Detection, stateFactory, this);
			TrashCanRevealState state4 = new TrashCanRevealState(controller, config, controller.Detection, stateFactory, this);
			TrashCanAttackState state5 = new TrashCanAttackState(controller, config, controller.Detection, stateFactory, this);
			stateFactory.RegisterCustomState(state);
			stateFactory.RegisterCustomState(state2);
			stateFactory.RegisterCustomState(state3);
			stateFactory.RegisterCustomState(state4);
			stateFactory.RegisterCustomState(state5);
		}
	}

	private void Update()
	{
		if (ShouldFacePlayer())
		{
			FacePlayer();
		}
	}

	private bool ShouldFacePlayer()
	{
		if (enemyController == null || !enemyController.IsAlive)
		{
			return false;
		}
		if (detection == null || !detection.HasDetectedPlayer)
		{
			return false;
		}
		EnemyAnimationState currentAnimationState = enemyController.GetCurrentAnimationState();
		if (currentAnimationState != EnemyAnimationState.Reveal)
		{
			return currentAnimationState == EnemyAnimationState.Attack;
		}
		return true;
	}

	public void FacePlayer()
	{
		if (playerTransform != null && spriteRenderer != null)
		{
			bool flag = playerTransform.position.x > cachedTransform.position.x;
			spriteRenderer.flipX = !flag;
		}
	}

	public bool IsPlayerInAttackRange()
	{
		if (config == null || playerTransform == null)
		{
			return false;
		}
		bool flag = Physics2D.OverlapCircle(cachedTransform.position, config.rangedAttackRange, config.playerLayer) != null;
		if (showDebugGizmos && flag != lastAttackRangeState)
		{
			lastAttackRangeState = flag;
		}
		return flag;
	}

	public bool IsPlayerInApproachRange()
	{
		if (config == null || playerTransform == null)
		{
			return false;
		}
		bool flag = Physics2D.OverlapCircle(cachedTransform.position, config.trashCanApproachRange, config.playerLayer) != null;
		if (showDebugGizmos && flag != lastApproachRangeState)
		{
			lastApproachRangeState = flag;
		}
		return flag;
	}

	public void StartRocketAttack()
	{
		if (combat == null)
		{
			Debug.LogError("[TrashCan] Combat component is null!");
		}
		else if (combat.CanAttack)
		{
			combat.StartRangedAttack();
		}
	}

	public void ExecuteRocketAttack()
	{
		if (rocketSpawnPoint == null || ObjectPool.Instance == null)
		{
			return;
		}
		GameObject gameObject = ObjectPool.Instance.SpawnFromPool(rocketPoolTag, rocketSpawnPoint.position, Quaternion.identity);
		SFXManager.Instance?.Play("E_TrashCanAttack", base.transform.position);
		if (gameObject != null)
		{
			HomingRocket component = gameObject.GetComponent<HomingRocket>();
			if (component != null)
			{
				component.Initialize(base.gameObject);
			}
		}
	}

	public void SetHiddenState(bool shouldHide)
	{
		isHidden = shouldHide;
		if (shouldHide)
		{
			SFXManager.Instance?.Play("E_TrashCanHide", base.transform.position);
			base.gameObject.layer = hiddenLayer;
			UpdateColliderForHidden();
		}
		else
		{
			base.gameObject.layer = normalLayer;
			UpdateColliderForRevealed();
		}
	}

	private void UpdateColliderForHidden()
	{
		if (boxCollider != null)
		{
			boxCollider.size = hiddenColliderSize;
			boxCollider.offset = hiddenColliderOffset;
		}
	}

	private void UpdateColliderForRevealed()
	{
		if (boxCollider != null)
		{
			boxCollider.size = revealedColliderSize;
			boxCollider.offset = revealedColliderOffset;
		}
	}

	private void OnDrawGizmos()
	{
		if (showDebugGizmos && !(config == null))
		{
			Vector3 vector = ((cachedTransform != null) ? cachedTransform.position : base.transform.position);
			Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
			Gizmos.DrawWireSphere(vector, config.rangedAttackRange);
			Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
			Gizmos.DrawWireSphere(vector, config.trashCanApproachRange);
			if (boxCollider != null)
			{
				Gizmos.color = (isHidden ? Color.blue : Color.green);
				Gizmos.DrawWireCube(vector + (Vector3)boxCollider.offset, boxCollider.size);
			}
		}
	}
}
