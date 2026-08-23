using UnityEngine;

public class EnemyChaseState : IEnemyState
{
	private readonly EnemyController enemy;

	private readonly EnemyConfig config;

	private readonly EnemyDetection detection;

	private readonly EnemyCombat combat;

	private readonly EnemyStateFactory stateFactory;

	private float stuckCheckTimer;

	private Vector3 lastPosition;

	private int stuckCount;

	private const float STUCK_CHECK_INTERVAL = 1f;

	private const float STUCK_DISTANCE_THRESHOLD = 0.2f;

	private const int MAX_STUCK_COUNT = 3;

	private bool isMoving = true;

	private const float RANGED_NEAR_FACTOR = 0.35f;

	private const float RANGED_FAR_FACTOR = 0.85f;

	private const float RANGED_REACQUIRE_FACTOR = 0.95f;

	private bool isInPreferredRange;

	public EnemyAnimationState AnimationState
	{
		get
		{
			if (!isMoving)
			{
				return EnemyAnimationState.Idle;
			}
			return EnemyAnimationState.Chase;
		}
	}

	public EnemyChaseState(EnemyController enemyController, EnemyConfig enemyConfig, EnemyDetection enemyDetection, EnemyCombat enemyCombat, EnemyStateFactory factory)
	{
		enemy = enemyController;
		config = enemyConfig;
		detection = enemyDetection;
		combat = enemyCombat;
		stateFactory = factory;
	}

	public void OnEnter()
	{
		lastPosition = enemy.transform.position;
		stuckCheckTimer = 0f;
		stuckCount = 0;
		isMoving = true;
		isInPreferredRange = false;
	}

	public void OnUpdate()
	{
		if (detection.IsBeingKnockedBack())
		{
			isMoving = false;
			return;
		}
		if (detection.HasLostPlayer())
		{
			HandleLostPlayer();
			return;
		}
		stuckCheckTimer += Time.deltaTime;
		if (stuckCheckTimer >= 1f)
		{
			if (CheckIfStuck())
			{
				stuckCount++;
				if (stuckCount >= 3)
				{
					detection.ClearDetectedPlayer();
					HandleLostPlayer();
					return;
				}
			}
			else
			{
				stuckCount = 0;
			}
			lastPosition = enemy.transform.position;
			stuckCheckTimer = 0f;
		}
		if (CanAttack())
		{
			enemy.ChangeState(stateFactory.Attack);
		}
	}

	public void OnFixedUpdate()
	{
		if (detection.IsBeingKnockedBack())
		{
			isMoving = false;
		}
		else
		{
			if (detection.DetectedPlayer == null)
			{
				return;
			}
			if (enemy.Movement.IsPlayerOnTop())
			{
				isMoving = false;
				return;
			}
			float distanceToPlayer = detection.GetDistanceToPlayer();
			if (config.enemyType == EnemyType.Melee)
			{
				HandleMeleeMovement(distanceToPlayer);
			}
			else if (config.enemyType == EnemyType.Ranged)
			{
				HandleRangedMovement(distanceToPlayer);
			}
		}
	}

	private void HandleMeleeMovement(float distanceToPlayer)
	{
		isMoving = true;
		float num = config.meleeAttackRange * 0.5f;
		if (distanceToPlayer < num)
		{
			Vector2 vector = (enemy.transform.position - detection.DetectedPlayer.position).normalized;
			enemy.Movement.Move(vector.x * config.moveSpeed * 0.5f);
		}
		else
		{
			enemy.Movement.ChaseTarget(detection.DetectedPlayer.position);
		}
	}

	private void HandleRangedMovement(float distanceToPlayer)
	{
		float num = config.rangedAttackRange * 0.35f;
		float num2 = config.rangedAttackRange * 0.85f;
		float num3 = config.rangedAttackRange * 0.95f;
		if (isInPreferredRange)
		{
			if (distanceToPlayer < num)
			{
				isMoving = true;
				isInPreferredRange = false;
				Vector2 vector = (enemy.transform.position - detection.DetectedPlayer.position).normalized;
				enemy.Movement.Move(vector.x * config.moveSpeed);
			}
			else if (distanceToPlayer > num3)
			{
				isMoving = true;
				isInPreferredRange = false;
				enemy.Movement.ChaseTarget(detection.DetectedPlayer.position);
			}
			else
			{
				isMoving = false;
				enemy.Movement.Stop();
				bool facingRight = detection.DetectedPlayer.position.x > enemy.transform.position.x;
				enemy.Movement.Flip(facingRight);
			}
		}
		else if (distanceToPlayer < num)
		{
			isMoving = true;
			Vector2 vector2 = (enemy.transform.position - detection.DetectedPlayer.position).normalized;
			enemy.Movement.Move(vector2.x * config.moveSpeed);
		}
		else if (distanceToPlayer > num2)
		{
			isMoving = true;
			enemy.Movement.ChaseTarget(detection.DetectedPlayer.position);
		}
		else
		{
			isInPreferredRange = true;
			isMoving = false;
			enemy.Movement.Stop();
			bool facingRight2 = detection.DetectedPlayer.position.x > enemy.transform.position.x;
			enemy.Movement.Flip(facingRight2);
		}
	}

	private bool CheckIfStuck()
	{
		return Vector3.Distance(lastPosition, enemy.transform.position) < 0.2f;
	}

	private void HandleLostPlayer()
	{
		if (config.canPatrol)
		{
			IEnemyState enemyState;
			if (!config.returnsToPatrol)
			{
				IEnemyState idle = stateFactory.Idle;
				enemyState = idle;
			}
			else
			{
				IEnemyState idle = stateFactory.Return;
				enemyState = idle;
			}
			IEnemyState newState = enemyState;
			enemy.ChangeState(newState);
		}
		else
		{
			enemy.ChangeState(stateFactory.Return);
		}
	}

	private bool CanAttack()
	{
		if (!detection.HasDetectedPlayer || !combat.CanAttack)
		{
			return false;
		}
		if (detection.IsBeingKnockedBack())
		{
			return false;
		}
		float distanceToPlayer = detection.GetDistanceToPlayer();
		if (config.enemyType == EnemyType.Melee)
		{
			return distanceToPlayer <= config.meleeAttackRange;
		}
		if (config.enemyType == EnemyType.Ranged)
		{
			float num = config.rangedAttackRange * 0.35f;
			if (distanceToPlayer >= num)
			{
				return distanceToPlayer <= config.rangedAttackRange;
			}
			return false;
		}
		return false;
	}

	public void OnExit()
	{
		stuckCount = 0;
		stuckCheckTimer = 0f;
		isMoving = false;
		isInPreferredRange = false;
	}
}
