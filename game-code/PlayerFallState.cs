using UnityEngine;

public class PlayerFallState : PlayerStateBase
{
	public override PlayerAnimationState AnimationState => PlayerAnimationState.Fall;

	public PlayerFallState(PlayerStateMachine player)
		: base(player)
	{
	}

	public override void OnEnter()
	{
	}

	public override void OnUpdate()
	{
		CheckTransitions();
	}

	public override void OnFixedUpdate()
	{
		player.Movement.Move(player.Input.MoveInput.x);
		player.Movement.ApplyGravity();
	}

	public override void OnExit()
	{
	}

	private void CheckTransitions()
	{
		if (CheckCommonTransitions())
		{
			return;
		}
		if (player.Movement.LadderCollided)
		{
			bool num = Mathf.Abs(player.Input.MoveInput.y) > player.Data.movementThreshold;
			bool stickJumpPressed = player.Input.StickJumpPressed;
			if (num | stickJumpPressed)
			{
				player.ChangeState<PlayerClimbState>();
				return;
			}
		}
		if (player.Movement.IsGrounded)
		{
			if (player.Input.HasBufferedJump)
			{
				player.Input.ConsumeJumpBuffer();
				player.ChangeState<PlayerJumpState>();
			}
			else if (IsMovementInputActive())
			{
				player.ChangeState<PlayerMoveState>();
			}
			else
			{
				player.ChangeState<PlayerIdleState>();
			}
		}
		else if (player.Input.JumpPressed && player.Movement.CanCoyoteJump)
		{
			player.ChangeState<PlayerJumpState>();
		}
		else if (player.Input.AttackPressed)
		{
			TransitionToAttackOrDeflect();
		}
	}
}
