using UnityEngine;

public class EnemyIdleState : IEnemyState
{
	private readonly EnemyController enemy;

	private readonly EnemyConfig config;

	private readonly EnemyDetection detection;

	private readonly EnemyStateFactory stateFactory;

	private const float IDLE_DURATION = 2f;

	private float idleTimer;

	public EnemyAnimationState AnimationState => EnemyAnimationState.Idle;

	public EnemyIdleState(EnemyController enemyController, EnemyConfig enemyConfig, EnemyDetection enemyDetection, EnemyStateFactory factory)
	{
		enemy = enemyController;
		config = enemyConfig;
		detection = enemyDetection;
		stateFactory = factory;
	}

	public void OnEnter()
	{
		enemy.Movement.Stop();
		idleTimer = 0f;
	}

	public void OnUpdate()
	{
		if (detection.CheckForPlayer() && config.canChase)
		{
			enemy.ChangeState(stateFactory.Chase);
			return;
		}
		idleTimer += Time.deltaTime;
		if (idleTimer >= 2f && config.canPatrol)
		{
			enemy.ChangeState(stateFactory.Patrol);
		}
	}

	public void OnFixedUpdate()
	{
	}

	public void OnExit()
	{
		if (config.canPatrol)
		{
			enemy.Movement.ReversePatrolDirection();
		}
		idleTimer = 0f;
	}
}
