using UnityEngine;

public class TrashCanAttackState : IEnemyState
{
	private readonly EnemyController enemy;

	private readonly EnemyConfig config;

	private readonly EnemyDetection detection;

	private readonly EnemyCombat combat;

	private readonly EnemyStateFactory stateFactory;

	private readonly TrashCanBehavior trashCanBehavior;

	private bool hasAttacked;

	private bool animationCompleted;

	private float attackCooldownTimer;

	public EnemyAnimationState AnimationState
	{
		get
		{
			if (!animationCompleted)
			{
				return EnemyAnimationState.Attack;
			}
			return EnemyAnimationState.Idle;
		}
	}

	public TrashCanAttackState(EnemyController enemyController, EnemyConfig enemyConfig, EnemyDetection enemyDetection, EnemyStateFactory factory, TrashCanBehavior behavior)
	{
		enemy = enemyController;
		config = enemyConfig;
		detection = enemyDetection;
		combat = enemy.Combat;
		stateFactory = factory;
		trashCanBehavior = behavior;
		combat.OnAttackCompleted += OnAttackAnimationCompleted;
	}

	public void OnEnter()
	{
		enemy.Movement.Stop();
		hasAttacked = false;
		animationCompleted = false;
		attackCooldownTimer = 0f;
		PerformAttack();
	}

	public void OnUpdate()
	{
		if (detection.IsBeingKnockedBack())
		{
			return;
		}
		if (trashCanBehavior.IsPlayerInApproachRange())
		{
			TransitionToHide();
		}
		else if (detection.HasLostPlayer())
		{
			TransitionToHide();
		}
		else
		{
			if (!animationCompleted || !hasAttacked)
			{
				return;
			}
			attackCooldownTimer -= Time.deltaTime;
			if (!(attackCooldownTimer > 0f))
			{
				if (!trashCanBehavior.IsPlayerInAttackRange())
				{
					TransitionToHide();
				}
				else
				{
					PerformAttack();
				}
			}
		}
	}

	public void OnFixedUpdate()
	{
		enemy.Movement.Stop();
	}

	public void OnExit()
	{
		attackCooldownTimer = 0f;
		animationCompleted = false;
	}

	private void OnAttackAnimationCompleted()
	{
		animationCompleted = true;
		attackCooldownTimer = config.trashCanAttackCooldown;
	}

	private void PerformAttack()
	{
		if (!(trashCanBehavior == null) && combat.CanAttack)
		{
			animationCompleted = false;
			trashCanBehavior.StartRocketAttack();
			hasAttacked = true;
		}
	}

	private void TransitionToHide()
	{
		TrashCanHideState customState = stateFactory.GetCustomState<TrashCanHideState>();
		if (customState != null)
		{
			enemy.ChangeState(customState);
		}
	}
}
