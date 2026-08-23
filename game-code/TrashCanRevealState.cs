using UnityEngine;

public class TrashCanRevealState : IEnemyState
{
	private readonly EnemyController enemy;

	private readonly EnemyConfig config;

	private readonly EnemyDetection detection;

	private readonly EnemyStateFactory stateFactory;

	private readonly TrashCanBehavior trashCanBehavior;

	private readonly EnemyAnimationController animationController;

	private float revealTimer;

	private const float REVEAL_DURATION = 0.5f;

	public EnemyAnimationState AnimationState => EnemyAnimationState.Reveal;

	public TrashCanRevealState(EnemyController enemyController, EnemyConfig enemyConfig, EnemyDetection enemyDetection, EnemyStateFactory factory, TrashCanBehavior behavior)
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
		revealTimer = 0f;
	}

	public void OnUpdate()
	{
		revealTimer += Time.deltaTime;
		if (trashCanBehavior.IsPlayerInApproachRange())
		{
			TrashCanHideState customState = stateFactory.GetCustomState<TrashCanHideState>();
			if (customState != null)
			{
				enemy.ChangeState(customState);
			}
		}
		else if (detection.HasLostPlayer())
		{
			TrashCanHideState customState2 = stateFactory.GetCustomState<TrashCanHideState>();
			if (customState2 != null)
			{
				enemy.ChangeState(customState2);
			}
		}
		else
		{
			if (!(revealTimer >= 0.5f) && (!(animationController != null) || !animationController.HasAnimationFinished()))
			{
				return;
			}
			if (trashCanBehavior.IsPlayerInAttackRange())
			{
				TrashCanAttackState customState3 = stateFactory.GetCustomState<TrashCanAttackState>();
				if (customState3 != null)
				{
					enemy.ChangeState(customState3);
				}
			}
			else
			{
				TrashCanHideState customState4 = stateFactory.GetCustomState<TrashCanHideState>();
				if (customState4 != null)
				{
					enemy.ChangeState(customState4);
				}
			}
		}
	}

	public void OnFixedUpdate()
	{
	}

	public void OnExit()
	{
		trashCanBehavior.SetHiddenState(shouldHide: false);
	}
}
