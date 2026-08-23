public class EnemyStateMachine
{
	private IEnemyState currentState;

	public IEnemyState CurrentState => currentState;

	public void ChangeState(IEnemyState newState)
	{
		currentState?.OnExit();
		currentState = newState;
		currentState?.OnEnter();
	}

	public void Update()
	{
		currentState?.OnUpdate();
	}

	public void FixedUpdate()
	{
		currentState?.OnFixedUpdate();
	}

	public IEnemyState GetCurrentState()
	{
		return currentState;
	}

	public EnemyAnimationState GetCurrentAnimationState()
	{
		return currentState?.AnimationState ?? EnemyAnimationState.Idle;
	}
}
