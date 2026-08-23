public interface IState
{
	PlayerAnimationState AnimationState { get; }

	void OnEnter();

	void OnUpdate();

	void OnFixedUpdate();

	void OnExit();
}
