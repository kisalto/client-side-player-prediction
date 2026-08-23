public class PlayerDashState : IState
{
	private readonly PlayerStateMachine player;

	public PlayerAnimationState AnimationState => PlayerAnimationState.Dash;

	public PlayerDashState(PlayerStateMachine player)
	{
		this.player = player;
	}

	public void OnEnter()
	{
		if (!player.Dash.CanDash)
		{
			TransitionToDefaultState();
			return;
		}
		float direction = (player.Movement.IsFacingRight ? 1f : (-1f));
		player.Movement.Dash(direction);
		player.Dash.StartDash();
	}

	public void OnUpdate()
	{
		if (!player.Dash.IsDashing)
		{
			TransitionToDefaultState();
		}
	}

	public void OnFixedUpdate()
	{
	}

	public void OnExit()
	{
		player.Movement.StopMovement();
	}

	private void TransitionToDefaultState()
	{
		if (player.Movement.IsGrounded)
		{
			player.ChangeState<PlayerIdleState>();
		}
		else
		{
			player.ChangeState<PlayerFallState>();
		}
	}
}
