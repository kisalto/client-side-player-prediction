using UnityEngine;

public class EnemyAttackState : IEnemyState
{
	private readonly EnemyController enemy;

	private readonly EnemyConfig config;

	private readonly EnemyDetection detection;

	private readonly EnemyCombat combat;

	private readonly EnemyStateFactory stateFactory;

	private bool hasAttacked;

	private float postAttackTimer;

	private const float POST_ATTACK_IDLE_DURATION = 0.3f;

	public EnemyAnimationState AnimationState
	{
		get
		{
			if (!combat.IsAttacking)
			{
				return EnemyAnimationState.Idle;
			}
			return EnemyAnimationState.Attack;
		}
	}

	public EnemyAttackState(EnemyController enemyController, EnemyConfig enemyConfig, EnemyDetection enemyDetection, EnemyCombat enemyCombat, EnemyStateFactory factory)
	{
		enemy = enemyController;
		config = enemyConfig;
		detection = enemyDetection;
		combat = enemyCombat;
		stateFactory = factory;
	}

	public void OnEnter()
	{
		enemy.Movement.Stop();
		hasAttacked = false;
		postAttackTimer = 0f;
		FacePlayer();
		PerformAttack();
	}

	public void OnUpdate()
	{
		if (detection.IsBeingKnockedBack())
		{
			return;
		}
		if (!detection.HasDetectedPlayer)
		{
			enemy.ChangeState(stateFactory.Idle);
		}
		else if (detection.HasLostPlayer())
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
			if (combat.IsAttacking || !hasAttacked)
			{
				return;
			}
			postAttackTimer += Time.deltaTime;
			if (postAttackTimer < 0.3f)
			{
				return;
			}
			if (detection.GetDistanceToPlayer() <= config.meleeAttackRange)
			{
				if (combat.CanAttack)
				{
					hasAttacked = false;
					postAttackTimer = 0f;
					FacePlayer();
					PerformAttack();
				}
			}
			else
			{
				enemy.ChangeState(stateFactory.Chase);
			}
		}
	}

	public void OnFixedUpdate()
	{
		enemy.Movement.Stop();
		if (detection.DetectedPlayer != null && !combat.IsAttacking)
		{
			FacePlayer();
		}
	}

	public void OnExit()
	{
	}

	private void PerformAttack()
	{
		if (!(detection.DetectedPlayer == null))
		{
			if (config.enemyType == EnemyType.Melee)
			{
				combat.StartMeleeAttack();
			}
			else if (config.enemyType == EnemyType.Ranged)
			{
				combat.StartRangedAttack();
			}
			hasAttacked = true;
		}
	}

	private void FacePlayer()
	{
		if (!(detection.DetectedPlayer == null))
		{
			bool facingRight = detection.DetectedPlayer.position.x > enemy.transform.position.x;
			enemy.Movement.Flip(facingRight);
		}
	}
}
