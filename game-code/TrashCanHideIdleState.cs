public class TrashCanHideIdleState : IEnemyState
{
	private readonly EnemyController enemy;

	private readonly EnemyConfig config;

	private readonly EnemyDetection detection;

	private readonly EnemyStateFactory stateFactory;

	private readonly TrashCanBehavior trashCanBehavior;

	public EnemyAnimationState AnimationState => EnemyAnimationState.HideIdle;

	public TrashCanHideIdleState(EnemyController enemyController, EnemyConfig enemyConfig, EnemyDetection enemyDetection, EnemyStateFactory factory, TrashCanBehavior behavior)
	{
		enemy = enemyController;
		config = enemyConfig;
		detection = enemyDetection;
		stateFactory = factory;
		trashCanBehavior = behavior;
	}

	public void OnEnter()
	{
		enemy.Movement.Stop();
		trashCanBehavior.SetHiddenState(shouldHide: true);
	}

	public void OnUpdate()
	{
		detection.CheckForPlayer();
		if (detection.HasLostPlayer())
		{
			detection.ClearDetectedPlayer();
		}
		else if (detection.HasDetectedPlayer && !trashCanBehavior.IsPlayerInApproachRange() && trashCanBehavior.IsPlayerInAttackRange())
		{
			TrashCanRevealState customState = stateFactory.GetCustomState<TrashCanRevealState>();
			if (customState != null)
			{
				enemy.ChangeState(customState);
			}
		}
	}

	public void OnFixedUpdate()
	{
	}

	public void OnExit()
	{
	}
}
