public class PlayerClimbState : PlayerStateBase
{
	private bool wasOnLadder;

	private bool autoJumpHandled;

	private bool wasClimbingUp;

	public override PlayerAnimationState AnimationState => PlayerAnimationState.Climb;

	public PlayerClimbState(PlayerStateMachine player)
		: base(player)
	{
	}

	public override void OnEnter()
	{
		player.Movement.StartClimbing();
		wasOnLadder = true;
		wasClimbingUp = false;
		autoJumpHandled = false;
	}

	public override void OnUpdate()
	{
		CheckTransitions();
	}

	public override void OnFixedUpdate()
	{
		float y = player.Input.MoveInput.y;
		float x = player.Input.MoveInput.x;
		player.Movement.Climb(y, x);
		if (x != 0f)
		{
			player.Movement.Flip(x > 0f);
		}
		if (y > 0f)
		{
			wasClimbingUp = true;
		}
		else if (y <= 0f)
		{
			wasClimbingUp = false;
		}
	}

	public override void OnExit()
	{
		if (autoJumpHandled)
		{
			wasOnLadder = false;
			wasClimbingUp = false;
			autoJumpHandled = false;
			return;
		}
		if (player.Input.JumpPressed)
		{
			player.Movement.StopClimbing(preserveVerticalVelocity: true);
			player.Movement.Jump();
		}
		else
		{
			player.Movement.StopClimbing();
		}
		wasOnLadder = false;
		wasClimbingUp = false;
		autoJumpHandled = false;
	}

	private void CheckTransitions()
	{
		if (CheckCommonTransitions())
		{
			return;
		}
		if (wasOnLadder && !player.Movement.LadderCollided && wasClimbingUp)
		{
			autoJumpHandled = true;
			player.Movement.StopClimbing(preserveVerticalVelocity: true);
			player.Movement.Jump();
			player.ChangeState<PlayerJumpState>();
		}
		else if (!player.Movement.IsOnLadder || !player.Movement.LadderCollided)
		{
			player.ChangeState<PlayerFallState>();
		}
		else if (player.Input.JumpPressed)
		{
			autoJumpHandled = true;
			player.Movement.StopClimbing(preserveVerticalVelocity: true);
			player.Movement.Jump();
			player.ChangeState<PlayerJumpState>();
		}
		else if (player.Input.AttackPressed)
		{
			TransitionToAttackOrDeflect();
		}
		else if (player.Movement.IsGrounded && player.Input.MoveInput.y <= 0f)
		{
			if (IsMovementInputActive())
			{
				player.ChangeState<PlayerMoveState>();
			}
			else
			{
				player.ChangeState<PlayerIdleState>();
			}
		}
	}
}
