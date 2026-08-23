public interface IEnemyState
{
	EnemyAnimationState AnimationState { get; }

	void OnEnter();

	void OnUpdate();

	void OnFixedUpdate();

	void OnExit();
}
