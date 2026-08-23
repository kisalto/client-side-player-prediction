using UnityEngine;

public class TrashCanHideState : IEnemyState
{
	private readonly EnemyController enemy;

	private readonly EnemyConfig config;

	private readonly EnemyDetection detection;

	private readonly EnemyStateFactory stateFactory;

	private readonly TrashCanBehavior trashCanBehavior;

	private readonly EnemyAnimationController animationController;

	private float hideTimer;

	private const float HIDE_DURATION = 0.5f;

	private bool transitionToScared;

	public EnemyAnimationState AnimationState => EnemyAnimationState.Hide;

	public TrashCanHideState(EnemyController enemyController, EnemyConfig enemyConfig, EnemyDetection enemyDetection, EnemyStateFactory factory, TrashCanBehavior behavior)
	{
		enemy = enemyController;
		config = enemyConfig;
		detection = enemyDetection;
		stateFactory = factory;
		trashCanBehavior = behavior;
		animationController = enemy.GetComponent<EnemyAnimationController>();
	}

	public void OnEnter()
	{
		enemy.Movement.Stop();
		hideTimer = 0f;
		transitionToScared = trashCanBehavior.IsPlayerInApproachRange();
	}

	public void OnUpdate()
	{
		hideTimer += Time.deltaTime;
		if (trashCanBehavior.IsPlayerInApproachRange())
		{
			transitionToScared = true;
		}
		if (!(hideTimer >= 0.5f) && (!(animationController != null) || !animationController.HasAnimationFinished()))
		{
			return;
		}
		if (transitionToScared)
		{
			TrashCanHideScaredState customState = stateFactory.GetCustomState<TrashCanHideScaredState>();
			if (customState != null)
			{
				enemy.ChangeState(customState);
			}
		}
		else
		{
			TrashCanHideIdleState customState2 = stateFactory.GetCustomState<TrashCanHideIdleState>();
			if (customState2 != null)
			{
				enemy.ChangeState(customState2);
			}
		}
	}

	public void OnFixedUpdate()
	{
	}

	public void OnExit()
	{
		trashCanBehavior.SetHiddenState(shouldHide: true);
	}
}
