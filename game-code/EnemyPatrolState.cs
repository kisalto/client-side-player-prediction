using UnityEngine;

public class EnemyPatrolState : IEnemyState
{
	private readonly EnemyController enemy;

	private readonly EnemyConfig config;

	private readonly EnemyDetection detection;

	private readonly EnemyStateFactory stateFactory;

	private float timeSinceEntered;

	private const float BOUNDARY_CHECK_GRACE_PERIOD = 0.3f;

	public EnemyAnimationState AnimationState => EnemyAnimationState.Patrol;

	public EnemyPatrolState(EnemyController enemyController, EnemyConfig enemyConfig, EnemyDetection enemyDetection, EnemyStateFactory factory)
	{
		enemy = enemyController;
		config = enemyConfig;
		detection = enemyDetection;
		stateFactory = factory;
	}

	public void OnEnter()
	{
		timeSinceEntered = 0f;
		enemy.Movement.ResetStuckDetection();
	}

	public void OnUpdate()
	{
		timeSinceEntered += Time.deltaTime;
		if (detection.CheckForPlayer() && config.canChase)
		{
			enemy.ChangeState(stateFactory.Chase);
		}
		else if (timeSinceEntered > 0.3f && (enemy.Movement.IsAtPatrolBoundary() || enemy.Movement.ShouldStopPatrol()))
		{
			enemy.ChangeState(stateFactory.Idle);
		}
	}

	public void OnFixedUpdate()
	{
		enemy.Movement.Patrol();
	}

	public void OnExit()
	{
	}
}
