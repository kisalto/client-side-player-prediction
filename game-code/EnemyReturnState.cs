using UnityEngine;

public class EnemyReturnState : IEnemyState
{
	private readonly EnemyController enemy;

	private readonly EnemyConfig config;

	private readonly EnemyDetection detection;

	private readonly EnemyStateFactory stateFactory;

	private float returnTimer;

	private float timeoutTimer;

	private float stuckCheckTimer;

	private Vector3 lastPosition;

	private float totalDistanceMoved;

	private bool hasReachedStartPosition;

	public EnemyAnimationState AnimationState => EnemyAnimationState.Patrol;

	public EnemyReturnState(EnemyController enemyController, EnemyConfig enemyConfig, EnemyDetection enemyDetection, EnemyStateFactory factory)
	{
		enemy = enemyController;
		config = enemyConfig;
		detection = enemyDetection;
		stateFactory = factory;
	}

	public void OnEnter()
	{
		returnTimer = (config.canPatrol ? config.returnToPatrolDelay : 0f);
		timeoutTimer = config.returnTimeoutDuration;
		stuckCheckTimer = config.stuckCheckInterval;
		lastPosition = enemy.transform.position;
		totalDistanceMoved = 0f;
		hasReachedStartPosition = false;
		detection.ClearDetectedPlayer();
		if (!config.canPatrol)
		{
			bool facingRight = Mathf.Sign(enemy.Movement.StartPosition.x - enemy.transform.position.x) > 0f;
			enemy.Movement.Flip(facingRight);
		}
	}

	public void OnUpdate()
	{
		returnTimer -= Time.deltaTime;
		timeoutTimer -= Time.deltaTime;
		stuckCheckTimer -= Time.deltaTime;
		if (detection.CheckForPlayer() && config.canChase)
		{
			enemy.ChangeState(stateFactory.Chase);
			return;
		}
		if (stuckCheckTimer <= 0f)
		{
			CheckIfStuck();
			stuckCheckTimer = config.stuckCheckInterval;
		}
		if (timeoutTimer <= 0f)
		{
			HandleReturnFailure("Timeout reached");
			return;
		}
		if (enemy.Movement.IsAtStartPosition() && !hasReachedStartPosition)
		{
			hasReachedStartPosition = true;
			enemy.Movement.Stop();
			enemy.Movement.RestoreStartFacingDirection();
		}
		if (hasReachedStartPosition && returnTimer <= 0f)
		{
			TransitionToIdle();
		}
	}

	public void OnFixedUpdate()
	{
		if (!hasReachedStartPosition)
		{
			enemy.Movement.ReturnToStart();
		}
	}

	public void OnExit()
	{
		hasReachedStartPosition = false;
	}

	private void CheckIfStuck()
	{
		Vector3 position = enemy.transform.position;
		float num = Vector3.Distance(lastPosition, position);
		totalDistanceMoved += num;
		if (num < config.minimumProgressDistance)
		{
			HandleReturnFailure("Enemy stuck, no progress made");
		}
		lastPosition = position;
	}

	private void HandleReturnFailure(string reason)
	{
		Debug.Log("[" + enemy.name + "] Cannot return to original position: " + reason + ". Establishing new patrol position.");
		enemy.Movement.SetNewStartPositionToCurrent();
		enemy.ChangeState(stateFactory.Idle);
	}

	private void TransitionToIdle()
	{
		IEnemyState enemyState;
		if (!config.canPatrol)
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
		enemy.ChangeState(newState);
	}
}
