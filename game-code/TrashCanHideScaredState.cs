using UnityEngine;

public class TrashCanHideScaredState : IEnemyState
{
	private readonly EnemyController enemy;

	private readonly EnemyConfig config;

	private readonly EnemyDetection detection;

	private readonly EnemyStateFactory stateFactory;

	private readonly TrashCanBehavior trashCanBehavior;

	private readonly EnemyAnimationController animationController;

	private float hideScaredTimer;

	private const float HIDE_SCARED_DURATION = 0.8f;

	private Vector3 originalPosition;

	private float shakeTimer;

	private const float SHAKE_FREQUENCY = 30f;

	private const float SHAKE_INTENSITY = 0.03f;

	private const float SHAKE_ROTATION_INTENSITY = 3f;

	private Quaternion originalRotation;

	public EnemyAnimationState AnimationState => EnemyAnimationState.HideScared;

	public TrashCanHideScaredState(EnemyController enemyController, EnemyConfig enemyConfig, EnemyDetection enemyDetection, EnemyStateFactory factory, TrashCanBehavior behavior)
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
		hideScaredTimer = 0f;
		shakeTimer = 0f;
		originalPosition = enemy.transform.position;
		originalRotation = enemy.transform.rotation;
	}

	public void OnUpdate()
	{
		hideScaredTimer += Time.deltaTime;
		shakeTimer += Time.deltaTime;
		ApplyShakeEffect();
		if (!trashCanBehavior.IsPlayerInApproachRange())
		{
			if (hideScaredTimer > 0.3f)
			{
				TrashCanHideIdleState customState = stateFactory.GetCustomState<TrashCanHideIdleState>();
				if (customState != null)
				{
					enemy.ChangeState(customState);
				}
			}
		}
		else if (hideScaredTimer >= 0.8f)
		{
			hideScaredTimer = 0f;
		}
	}

	public void OnFixedUpdate()
	{
	}

	public void OnExit()
	{
		enemy.transform.position = originalPosition;
		enemy.transform.rotation = originalRotation;
	}

	private void ApplyShakeEffect()
	{
		float x = Mathf.Sin(shakeTimer * 30f) * 0.03f;
		float y = Mathf.Sin(shakeTimer * 30f * 1.3f) * 0.03f * 0.5f;
		Vector3 vector = new Vector3(x, y, 0f);
		enemy.transform.position = originalPosition + vector;
		float z = Mathf.Sin(shakeTimer * 30f * 0.8f) * 3f;
		enemy.transform.rotation = originalRotation * Quaternion.Euler(0f, 0f, z);
	}
}
