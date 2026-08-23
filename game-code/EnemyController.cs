using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(EnemyDetection))]
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyCombat))]
[RequireComponent(typeof(EnemyAnimationController))]
public class EnemyController : Actor
{
	[Header("Enemy Configuration")]
	[SerializeField]
	private EnemyConfig config;

	[Header("Range Overrides (Leave at 0 to use Config values)")]
	[SerializeField]
	private bool enableRangeOverrides;

	[Tooltip("Override detection range (0 = use config value)")]
	[SerializeField]
	private float detectionRangeOverride;

	[Tooltip("Override lose player range (0 = use config value)")]
	[SerializeField]
	private float losePlayerRangeOverride;

	[Tooltip("Override melee attack range (0 = use config value)")]
	[SerializeField]
	private float meleeAttackRangeOverride;

	[Tooltip("Override ranged attack range (0 = use config value)")]
	[SerializeField]
	private float rangedAttackRangeOverride;

	[Tooltip("Override trash can approach range (0 = use config value)")]
	[SerializeField]
	private float trashCanApproachRangeOverride;

	[Tooltip("Override detection angle in degrees (0 = use config value)")]
	[SerializeField]
	private float detectionAngleOverride;

	[Header("Range Additions (Added to Config or Override values)")]
	[Tooltip("Add to detection range (can be negative)")]
	[SerializeField]
	private float detectionRangeAddition;

	[Tooltip("Add to lose player range (can be negative)")]
	[SerializeField]
	private float losePlayerRangeAddition;

	[Tooltip("Add to melee attack range (can be negative)")]
	[SerializeField]
	private float meleeAttackRangeAddition;

	[Tooltip("Add to ranged attack range (can be negative)")]
	[SerializeField]
	private float rangedAttackRangeAddition;

	[Tooltip("Add to trash can approach range (can be negative)")]
	[SerializeField]
	private float trashCanApproachRangeAddition;

	[Tooltip("Add to detection angle in degrees (can be negative)")]
	[SerializeField]
	private float detectionAngleAddition;

	[Header("Direction Override")]
	[SerializeField]
	private bool overrideStartFacingDirection;

	[Tooltip("Override initial facing direction (only applies if override is enabled)")]
	[SerializeField]
	private bool startFacingRight = true;

	[Header("Death Settings")]
	[SerializeField]
	private float deathDestroyDelay = 5f;

	[SerializeField]
	private float blinkStartTime = 3f;

	[SerializeField]
	private float blinkInterval = 0.2f;

	[Header("Hit From Behind Settings")]
	[SerializeField]
	private float behindDetectionTime = 0.5f;

	[SerializeField]
	private float behindDetectionRange = 4f;

	[Header("Debug")]
	[SerializeField]
	private bool showDebugInfo = true;

	[SerializeField]
	private bool showGizmos = true;

	[SerializeField]
	private Color detectionRangeColor = Color.yellow;

	[SerializeField]
	private Color attackRangeColor = Color.red;

	[SerializeField]
	private Color losePlayerRangeColor = new Color(1f, 0.5f, 0f);

	private EnemyStateMachine stateMachine;

	private EnemyStateFactory stateFactory;

	private EnemyAnimationController animController;

	private EnemyConfig runtimeConfig;

	private EnemyConfig cachedDisplayConfig;

	private string currentStateName = "None";

	private bool wasHitFromBehind;

	private static readonly int CONE_SEGMENTS = 20;

	public EnemyDetection Detection { get; private set; }

	public EnemyMovement Movement { get; private set; }

	public EnemyCombat Combat { get; private set; }

	public EnemyConfig Config => runtimeConfig;

	public EnemyStateFactory States => stateFactory;

	public static event Action OnAttackedWhileHiding;

	protected override void Awake()
	{
		base.Awake();
		runtimeConfig = CreateRuntimeConfig();
		Detection = GetComponent<EnemyDetection>();
		Movement = GetComponent<EnemyMovement>();
		Combat = GetComponent<EnemyCombat>();
		animController = GetComponent<EnemyAnimationController>();
		Detection.Initialize(runtimeConfig, Movement);
		Movement.Initialize(runtimeConfig, rb);
		Combat.Initialize(runtimeConfig);
		maxHealthPoints = runtimeConfig.maxHealth;
		currentHealthPoints = maxHealthPoints;
		hurtEffectName = runtimeConfig.hurtEffectName;
		deathEffectName = runtimeConfig.deathEffectName;
		enableHurtEffect = runtimeConfig.enableHurtEffect;
		enableDeathEffect = runtimeConfig.enableDeathEffect;
		stateMachine = new EnemyStateMachine();
		stateFactory = new EnemyStateFactory(this, runtimeConfig, Detection, Combat);
		TrashCanBehavior component = GetComponent<TrashCanBehavior>();
		if (component != null)
		{
			component.Initialize(runtimeConfig, Detection);
			component.InitializeStates(this, stateFactory);
		}
	}

	private void Start()
	{
		if (runtimeConfig.isTrashCanEnemy && GetComponent<TrashCanBehavior>() != null)
		{
			TrashCanHideIdleState customState = stateFactory.GetCustomState<TrashCanHideIdleState>();
			if (customState != null)
			{
				stateMachine.ChangeState(customState);
				return;
			}
		}
		IEnemyState enemyState;
		if (!runtimeConfig.canPatrol)
		{
			IEnemyState idle = stateFactory.Idle;
			enemyState = idle;
		}
		else
		{
			IEnemyState idle = stateFactory.Patrol;
			enemyState = idle;
		}
		IEnemyState newState = enemyState;
		stateMachine.ChangeState(newState);
	}

	private EnemyConfig CreateRuntimeConfig()
	{
		EnemyConfig enemyConfig = UnityEngine.Object.Instantiate(config);
		ApplyRangeModifiers(enemyConfig);
		if (overrideStartFacingDirection)
		{
			enemyConfig.startFacingRight = startFacingRight;
		}
		return enemyConfig;
	}

	private void ApplyRangeModifiers(EnemyConfig target)
	{
		if (enableRangeOverrides)
		{
			if (detectionRangeOverride > 0f)
			{
				target.detectionRange = detectionRangeOverride;
			}
			if (losePlayerRangeOverride > 0f)
			{
				target.losePlayerRange = losePlayerRangeOverride;
			}
			if (meleeAttackRangeOverride > 0f)
			{
				target.meleeAttackRange = meleeAttackRangeOverride;
			}
			if (rangedAttackRangeOverride > 0f)
			{
				target.rangedAttackRange = rangedAttackRangeOverride;
			}
			if (trashCanApproachRangeOverride > 0f)
			{
				target.trashCanApproachRange = trashCanApproachRangeOverride;
			}
			if (detectionAngleOverride > 0f)
			{
				target.detectionAngle = detectionAngleOverride;
			}
		}
		target.detectionRange += detectionRangeAddition;
		target.losePlayerRange += losePlayerRangeAddition;
		target.meleeAttackRange += meleeAttackRangeAddition;
		target.rangedAttackRange += rangedAttackRangeAddition;
		target.trashCanApproachRange += trashCanApproachRangeAddition;
		target.detectionAngle += detectionAngleAddition;
	}

	private void Update()
	{
		if (base.IsAlive)
		{
			if (!base.isBeingKnockedBack)
			{
				stateMachine.Update();
			}
			Combat.UpdateCooldowns(Time.deltaTime);
			if (showDebugInfo)
			{
				UpdateDebugInfo();
			}
		}
	}

	private void FixedUpdate()
	{
		if (base.IsAlive && !base.isBeingKnockedBack)
		{
			stateMachine.FixedUpdate();
		}
	}

	public void ChangeState(IEnemyState newState)
	{
		stateMachine.ChangeState(newState);
	}

	public EnemyAnimationState GetCurrentAnimationState()
	{
		return stateMachine.GetCurrentAnimationState();
	}

	public override bool TakeDamage(int damageAmount)
	{
		if (!base.IsAlive)
		{
			return false;
		}
		NotifyIfHiddenTrashCan();
		base.TakeDamage(damageAmount);
		if (base.IsAlive)
		{
			animController?.TriggerHurt();
		}
		return true;
	}

	public override bool TakeDamage(int damageAmount, Vector2 knockbackDirection, float knockbackForce)
	{
		if (!base.IsAlive)
		{
			return false;
		}
		wasHitFromBehind = IsHitFromBehind(knockbackDirection);
		if (Combat.IsAttacking)
		{
			Combat.InterruptAttack();
		}
		NotifyIfHiddenTrashCan();
		base.TakeDamage(damageAmount, knockbackDirection, knockbackForce);
		if (base.IsAlive)
		{
			animController?.TriggerHurt();
			if (wasHitFromBehind)
			{
				StartCoroutine(HandleHitFromBehind());
			}
		}
		return true;
	}

	private void NotifyIfHiddenTrashCan()
	{
		if (stateMachine.CurrentState is TrashCanHideIdleState)
		{
			OnAttackedWhileHiding?.Invoke();
		}
	}

	private bool IsHitFromBehind(Vector2 knockbackDirection)
	{
		if (knockbackDirection == Vector2.zero)
		{
			return false;
		}
		bool num = knockbackDirection.x > 0f;
		bool isFacingRight = Movement.IsFacingRight;
		return num == isFacingRight;
	}

	private IEnumerator HandleHitFromBehind()
	{
		yield return new WaitForSeconds(knockbackDuration);
		if (!base.IsAlive)
		{
			yield break;
		}
		Movement.Flip(!Movement.IsFacingRight);
		float elapsedTime = 0f;
		bool playerDetected = false;
		while (elapsedTime < behindDetectionTime)
		{
			Collider2D collider2D = Physics2D.OverlapCircle(base.transform.position, behindDetectionRange, runtimeConfig.playerLayer);
			if (collider2D != null)
			{
				Detection.SetDetectedPlayer(collider2D.transform);
				playerDetected = true;
				break;
			}
			elapsedTime += Time.deltaTime;
			yield return null;
		}
		if (!playerDetected)
		{
			Movement.RestoreStartFacingDirection();
		}
		wasHitFromBehind = false;
		EvaluateStateAfterHit();
	}

	private void EvaluateStateAfterHit()
	{
		if (!base.IsAlive || runtimeConfig == null)
		{
			return;
		}
		Detection.CheckForPlayer();
		if (Detection.HasDetectedPlayer && runtimeConfig.canChase)
		{
			ChangeState(stateFactory.Chase);
			return;
		}
		if (runtimeConfig.returnsToPatrol && !Movement.IsAtStartPosition())
		{
			ChangeState(stateFactory.Return);
			return;
		}
		IEnemyState enemyState;
		if (!runtimeConfig.canPatrol)
		{
			IEnemyState idle = stateFactory.Idle;
			enemyState = idle;
		}
		else
		{
			IEnemyState idle = stateFactory.Patrol;
			enemyState = idle;
		}
		IEnemyState newState = enemyState;
		ChangeState(newState);
	}

	protected override void Die()
	{
		base.Die();
		animController?.TriggerDeath();
		ChangeState(stateFactory.Dead);
		LevelStatsManager.Instance?.RegisterEnemyKill();
		StartCoroutine(DeathSequence());
	}

	private IEnumerator DeathSequence()
	{
		yield return new WaitForSeconds(blinkStartTime);
		float blinkDuration = deathDestroyDelay - blinkStartTime;
		float elapsed = 0f;
		float initialBlinkInterval = blinkInterval;
		float finalBlinkInterval = 0.05f;
		float currentBlinkInterval;
		for (; elapsed < blinkDuration; elapsed += currentBlinkInterval)
		{
			float t = elapsed / blinkDuration;
			currentBlinkInterval = Mathf.Lerp(initialBlinkInterval, finalBlinkInterval, t);
			spriteRenderer.enabled = !spriteRenderer.enabled;
			yield return new WaitForSeconds(currentBlinkInterval);
		}
		UnityEngine.Object.Destroy(base.gameObject);
	}

	private void UpdateDebugInfo()
	{
		if (stateMachine.GetCurrentState() != null)
		{
			currentStateName = stateMachine.GetCurrentState().GetType().Name;
		}
	}

	[ContextMenu("Reset Start Position to Current")]
	private void ResetStartPositionToCurrent()
	{
		if (Movement != null)
		{
			Movement.SetNewStartPositionToCurrent();
		}
	}

	public void OnAttackAnimationEvent()
	{
		if (Combat == null)
		{
			return;
		}
		if (runtimeConfig.enemyType == EnemyType.Melee)
		{
			Vector3 attackPosition = ((Detection.DetectedPlayer != null) ? Detection.DetectedPlayer.position : (base.transform.position + (Movement.IsFacingRight ? Vector3.right : Vector3.left)));
			Combat.ExecuteMeleeAttack(attackPosition);
		}
		else if (runtimeConfig.enemyType == EnemyType.Ranged)
		{
			if (Detection.DetectedPlayer != null)
			{
				Vector3 normalized = (Detection.DetectedPlayer.position - base.transform.position).normalized;
				Combat.ExecuteRangedAttack(normalized);
			}
		}
		else if (runtimeConfig.enemyType == EnemyType.TrashCan)
		{
			GetComponent<TrashCanBehavior>()?.ExecuteRocketAttack();
		}
	}

	protected override void OnKnockbackEnd()
	{
		base.OnKnockbackEnd();
		if (base.IsAlive && !(runtimeConfig == null) && !wasHitFromBehind)
		{
			EvaluateStateAfterHit();
		}
	}

	public void OnAttackCompleted()
	{
		Combat?.CompleteAttack();
	}

	private void OnDrawGizmos()
	{
		if (showGizmos && !(config == null))
		{
			EnemyConfig displayConfig = GetDisplayConfig();
			Vector3 position = base.transform.position;
			if (displayConfig.useDirectionalDetection)
			{
				DrawDetectionCone(position, displayConfig);
			}
			else
			{
				Gizmos.color = detectionRangeColor;
				Gizmos.DrawWireSphere(position, displayConfig.detectionRange);
			}
			Gizmos.color = attackRangeColor;
			Gizmos.DrawWireSphere(position, displayConfig.meleeAttackRange);
			Gizmos.color = losePlayerRangeColor;
			Gizmos.DrawWireSphere(position, displayConfig.losePlayerRange);
			if (displayConfig.isTrashCanEnemy)
			{
				Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
				Gizmos.DrawWireSphere(position, displayConfig.trashCanApproachRange);
			}
		}
	}

	private void DrawDetectionCone(Vector3 position, EnemyConfig displayConfig)
	{
		Vector2 vector = (((Movement != null) ? Movement.IsFacingRight : displayConfig.startFacingRight) ? Vector2.right : Vector2.left);
		float num = displayConfig.detectionAngle / 2f;
		Vector3 vector2 = Quaternion.Euler(0f, 0f, num) * vector * displayConfig.detectionRange;
		Vector3 vector3 = Quaternion.Euler(0f, 0f, 0f - num) * vector * displayConfig.detectionRange;
		Gizmos.color = detectionRangeColor;
		Gizmos.DrawLine(position, position + vector2);
		Gizmos.DrawLine(position, position + vector3);
		Vector3 vector4 = position + vector2;
		for (int i = 1; i <= CONE_SEGMENTS; i++)
		{
			float z = Mathf.Lerp(num, 0f - num, (float)i / (float)CONE_SEGMENTS);
			Vector3 vector5 = position + Quaternion.Euler(0f, 0f, z) * vector * displayConfig.detectionRange;
			Gizmos.DrawLine(vector4, vector5);
			vector4 = vector5;
		}
	}

	private EnemyConfig GetDisplayConfig()
	{
		if (runtimeConfig != null)
		{
			return runtimeConfig;
		}
		EnemyConfig enemyConfig = UnityEngine.Object.Instantiate(config);
		ApplyRangeModifiers(enemyConfig);
		return enemyConfig;
	}
}
